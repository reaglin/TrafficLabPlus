using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Microsoft.Win32;
using TrafficLabPlus.App.Publish;
using TrafficLabPlus.Core.Build;
using TrafficLabPlus.Core.Model;
using TrafficLabPlus.Core.Publish;
using TrafficLabPlus.Core.Site;

namespace TrafficLabPlus.App.Views;

/// <summary>
/// Publish (D15, Ron 2026-10-09): one GitHub Pages website, every study in it except those marked
/// "Don't publish", a summary page as the index linking to each. The student ticks which studies go,
/// says where (their GitHub account, the repository), publishes, and copies the link to hand in.
/// </summary>
public sealed class PublishView(AppSettings settings, Func<IReadOnlyList<string>> knownFiles) : SectionView
{
    private sealed class Row
    {
        public required string Path { get; init; }
        public Study? Study { get; init; }
        public string? Problem { get; init; }
        public string Slug { get; set; } = "";
    }

    private readonly TextBox _log = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MinHeight = 80, MaxHeight = 200, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontSize = 12, Visibility = Visibility.Collapsed };
    private readonly ProgressBar _progress = new() { Height = 8, Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
    private readonly StackPanel _result = new();
    private bool _busy;

    public static string SiteFolder => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrafficLabPlus", "Site");

    public override void Rebuild()
    {
        (ScrollViewer scroll, StackPanel panel) = Column();
        Content = scroll;
        panel.Children.Add(Title("Publish"));
        panel.Children.Add(Lead("Put your studies on the web as one website — a summary page linking to a page for each study — and hand in the link."));
        panel.Children.Add(Guides.Publish());

        List<Row> rows = Rows();
        Heading(panel, "1  Your studies", "Every study saved on this computer that TrafficLab+ knows about (in Documents\\TrafficLabPlus\\Studies, or opened recently). Untick any you do not want on the website — it is left out, and taken off the website if it was there before. Your choice is remembered. Once a study is published, its address stays the same, even if you rename its file.");
        if (Session is { Path: null })
        {
            Note(panel, Session.IsExample
                ? "The study open now is a built-in example, so it is not listed. Save your own copy (Ctrl+S) to publish it."
                : "The study open now has not been saved, so it is not listed. Save it (Ctrl+S) to publish it.", true);
        }

        if (rows.Count == 0)
        {
            Note(panel, "No saved studies yet. Make one, save it (Ctrl+S), and it appears here.", false);
        }

        AssignSlugs(rows);
        foreach (Row r in rows)
        {
            var line = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            var tick = new CheckBox
            {
                IsChecked = r.Study is not null && !Excluded(r.Path),
                IsEnabled = r.Study is not null,
                Content = new TextBlock { Text = r.Study?.Title ?? System.IO.Path.GetFileName(r.Path), FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap },
            };
            AutomationProperties.SetName(tick, "Publish " + (r.Study?.Title ?? System.IO.Path.GetFileName(r.Path)));
            string path = r.Path;
            tick.Checked += (_, _) => SetExcluded(path, false);
            tick.Unchecked += (_, _) => SetExcluded(path, true);
            line.Children.Add(tick);
            line.Children.Add(new TextBlock { Text = r.Problem ?? r.Path, TextWrapping = TextWrapping.Wrap, Foreground = Form.Res(r.Problem is null ? "MutedTextBrush" : "ErrorBrush"), FontSize = 12, Margin = new Thickness(20, 0, 0, 0) });
            panel.Children.Add(line);
            if (settings.LastSiteUrl is { Length: > 0 } site && settings.LastPublished.Contains(r.Slug))
            {
                panel.Children.Add(LinkRow("On the website now:", site + r.Slug + "/", indent: 20));
            }
        }

        Heading(panel, "2  Where it goes", "Your GitHub account and the repository the website lives in. GitHub is a free service for keeping files online; GitHub Pages, part of it, turns a repository into a website at https://<account>.github.io/<repository>/.");
        TextBox account = Box(panel, "GitHub account", settings.GitHubAccount ?? TokenStore.LoadAccount() ?? "", "Your GitHub user name. With a saved token it can be left empty: the token says whose it is.", required: false);
        TextBox repo = Box(panel, "Repository", settings.Repository ?? SiteMarks.DefaultRepository, "Where the website is kept. Every study goes into this one repository; TrafficLab+ creates it the first time. Leave it as TrafficLab unless you have a reason.", required: true);
        TextBox siteTitle = Box(panel, "Website title", settings.SiteTitle ?? DefaultTitle(), "The heading of the summary page, like \"Maria Gomez — CEN 3722 traffic studies\".", required: true);
        account.TextChanged += (_, _) => settings.GitHubAccount = Blank(account.Text);
        repo.TextChanged += (_, _) => settings.Repository = Blank(repo.Text);
        siteTitle.TextChanged += (_, _) => settings.SiteTitle = Blank(siteTitle.Text);

        var tokenRow = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
        var tokenState = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0), TextWrapping = TextWrapping.Wrap };
        void ShowToken() => tokenState.Text = TokenStore.HasToken ? "A GitHub token is saved: TrafficLab+ can publish for you." : "No GitHub token saved yet (see New to GitHub? below).";
        var tokenButton = new Button { Content = "GitHub token…", Padding = new Thickness(10, 4, 10, 4) };
        tokenButton.Click += (_, _) =>
        {
            if (TokenWindow.Show(Window.GetWindow(this), out string? who) && who is { Length: > 0 })
            {
                account.Text = who;
            }

            ShowToken();
        };
        tokenRow.Children.Add(tokenButton);
        tokenRow.Children.Add(tokenState);
        ShowToken();
        panel.Children.Add(tokenRow);
        panel.Children.Add(NewToGitHub());

        Heading(panel, "3  Publish", PublishWords.WhatPublishingDoes);
        if (settings.LastSiteUrl is { Length: > 0 } summary)
        {
            panel.Children.Add(LinkRow("Your summary page, as last published:", summary, indent: 0));
        }

        int count = rows.Count(r => r.Study is not null && !Excluded(r.Path));
        var go = new Button
        {
            Content = count == 1 ? "Publish 1 study to GitHub Pages" : $"Publish {count} studies to GitHub Pages",
            Style = (Style)Application.Current.Resources["Primary"],
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 6, 0, 0),
        };
        go.Click += async (_, _) =>
        {
            if (_busy)
            {
                return;
            }

            settings.GitHubAccount = Blank(account.Text);
            settings.Repository = Blank(repo.Text) ?? SiteMarks.DefaultRepository;
            settings.SiteTitle = Blank(siteTitle.Text);
            settings.Save();
            await Publish(Rows(), settings.Repository, settings.SiteTitle ?? DefaultTitle());
        };
        panel.Children.Add(go);
        panel.Children.Add(_progress);
        panel.Children.Add(_log);
        panel.Children.Add(_result);

        Heading(panel, "Or: save one study's page as a file  (optional)", "One .html file, with nothing else needed: it opens in any browser with no internet, and can be emailed or put on any web site.");
        if (Session is { } s)
        {
            var save = new Button { Content = "Save the open study's page as a web page file…", Padding = new Thickness(10, 4, 10, 4), HorizontalAlignment = HorizontalAlignment.Left };
            save.Click += (_, _) => SavePage(s, Window.GetWindow(this));
            panel.Children.Add(save);
        }
    }

    private string DefaultTitle() => string.IsNullOrWhiteSpace(settings.DefaultAuthor) ? "Traffic studies" : settings.DefaultAuthor + " — traffic studies";

    private bool Excluded(string path) => settings.DontPublish.Contains(System.IO.Path.GetFullPath(path), StringComparer.OrdinalIgnoreCase);

    private void SetExcluded(string path, bool excluded)
    {
        string full = System.IO.Path.GetFullPath(path);
        settings.DontPublish.RemoveAll(p => string.Equals(p, full, StringComparison.OrdinalIgnoreCase));
        if (excluded)
        {
            settings.DontPublish.Add(full);
        }

        settings.Save();
        RebuildSoon();
    }

    /// <summary>Every saved study TrafficLab+ knows: the studies folder, recent studies, the open one.
    /// The open study is published as it is in the window now.</summary>
    private List<Row> Rows()
    {
        var paths = new List<string>();
        if (Directory.Exists(AppSettings.StudiesFolder))
        {
            paths.AddRange(Directory.GetFiles(AppSettings.StudiesFolder, "*" + StudyFile.Extension));
        }

        paths.AddRange(knownFiles());
        if (Session?.Path is { } open)
        {
            paths.Add(open);
        }

        var rows = new List<Row>();
        foreach (string p in paths.Select(System.IO.Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).Where(File.Exists).OrderBy(System.IO.Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            if (Session?.Path is { } o && string.Equals(System.IO.Path.GetFullPath(o), p, StringComparison.OrdinalIgnoreCase))
            {
                rows.Add(new Row { Path = p, Study = Session.Study });
                continue;
            }

            try
            {
                rows.Add(new Row { Path = p, Study = StudyFile.Load(p).Study });
            }
            catch (Exception ex) when (ex is StudyFormatException or IOException or UnauthorizedAccessException)
            {
                rows.Add(new Row { Path = p, Problem = "Could not be read, so it is not published: " + ex.Message });
            }
        }

        return rows;
    }

    /// <summary>A study keeps the folder it was first published in, so its link never changes;
    /// a new one gets a folder from its file name that no other study has.</summary>
    private void AssignSlugs(List<Row> rows)
    {
        var taken = new HashSet<string>(settings.Slugs.Values, StringComparer.OrdinalIgnoreCase);
        foreach (Row r in rows)
        {
            r.Slug = settings.Slugs.TryGetValue(r.Path, out string? kept)
                ? kept
                : SiteBuilder.Slug(System.IO.Path.GetFileNameWithoutExtension(r.Path), taken);
        }
    }

    private async Task Publish(List<Row> rows, string repository, string siteTitle)
    {
        AssignSlugs(rows);

        List<Row> going = rows.Where(r => r.Study is not null && !Excluded(r.Path)).ToList();
        Window owner = Window.GetWindow(this);
        if (going.Count == 0)
        {
            MessageBox.Show(owner, "No study is ticked to publish. Tick at least one under 1  Your studies.", "TrafficLab+", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        List<string> problems = going.Where(r => StudyValidator.Check(r.Study!).Count > 0).Select(r => r.Study!.Title).ToList();
        if (problems.Count > 0 && MessageBox.Show(owner,
                "These studies have problems, and their pages will show them instead of the simulation:\n\n• " + string.Join("\n• ", problems) +
                "\n\nPublish anyway? (No goes back, to fix them first.)", "TrafficLab+", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        // say before sending what comes off the website
        List<string> leaving = settings.LastPublished.Except(going.Select(r => r.Slug), StringComparer.OrdinalIgnoreCase).ToList();
        if (leaving.Count > 0 && MessageBox.Show(owner,
                "These studies are on your website now and will be taken off it, because they are unticked or no longer on this computer:\n\n• " +
                string.Join("\n• ", leaving.Select(slug => settings.PublishedTitles.GetValueOrDefault(slug, slug))) + "\n\nGo on?", "TrafficLab+", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
        {
            return;
        }

        _busy = true;
        _result.Children.Clear();
        _log.Clear();
        _log.Visibility = Visibility.Visible;
        _progress.Visibility = Visibility.Visible;
        _progress.Value = 0;
        void Log(string line) => Dispatcher.Invoke(() => { _log.AppendText(line + Environment.NewLine); _log.ScrollToEnd(); });
        try
        {
            Log($"Building the website: {going.Count} stud{(going.Count == 1 ? "y" : "ies")} and the summary page…");
            SiteBuilder.Build(SiteFolder, siteTitle, going.Select(r => new SiteStudy(r.Study!, r.Slug)).ToList(), DateTime.Now);
            var progress = new Progress<(int Percent, string Message)>(p => { _progress.Value = p.Percent; });
            PublishResult result = await Publisher.PublishAsync(new PublishRequest
            {
                SiteFolder = SiteFolder,
                Owner = settings.GitHubAccount ?? "",
                Repository = repository,
                Description = siteTitle,
                KnownAddress = settings.LastSiteUrl ?? "",
                ConfirmReplace = url => Dispatcher.Invoke(() => MessageBox.Show(owner,
                    $"{url} already holds something that TrafficLab+ did not put there — perhaps your own work.\n\nYes DELETES EVERYTHING IN IT and puts your studies there instead.\nNo leaves it alone; then type another name under Repository.\n\nReplace everything in it?",
                    "TrafficLab+ — replace a repository?", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes),
            }, TokenStore.Load(), Log, progress);

            settings.LastSiteUrl = result.PagesUrl.EndsWith('/') ? result.PagesUrl : result.PagesUrl + "/";
            settings.LastPublished = going.Select(r => r.Slug).ToList();
            settings.PublishedTitles = going.ToDictionary(r => r.Slug, r => r.Study!.Title, StringComparer.OrdinalIgnoreCase);
            foreach (Row r in going)
            {
                settings.Slugs[r.Path] = r.Slug;
            }

            settings.Save();
            Log("Published.");
            ShowResult(result, going);
        }
        catch (Exception ex) when (ex is InvalidOperationException or GitHubException or IOException or UnauthorizedAccessException or HttpRequestException or TaskCanceledException)
        {
            string words = ex is HttpRequestException or TaskCanceledException
                ? "GitHub could not be reached. Check the internet connection and press Publish again. Nothing was changed on your website."
                : ex.Message.StartsWith("Not published", StringComparison.Ordinal) ? ex.Message : "Not published: " + ex.Message;
            Log(words);
            _result.Children.Add(new TextBlock { Text = words, Foreground = Form.Res("ErrorBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
        }
        finally
        {
            _busy = false;
            _progress.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowResult(PublishResult r, List<Row> going)
    {
        string site = r.PagesUrl.EndsWith('/') ? r.PagesUrl : r.PagesUrl + "/";
        foreach (string step in r.NextSteps)
        {
            _result.Children.Add(new TextBlock { Text = step, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
        }

        _result.Children.Add(new TextBlock
        {
            Text = r.PagesEnabled
                ? "Published. Open a link first to check it is up (a minute or two the first time), then hand it in:"
                : "Almost done: turn the website on (the steps above), then hand in these links:",
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 4),
        });
        _result.Children.Add(LinkRow("The summary page (all your studies)", site, 0));
        foreach (Row g in going)
        {
            _result.Children.Add(LinkRow(g.Study!.Title, site + g.Slug + "/", 0));
        }
    }

    private static DockPanel LinkRow(string what, string url, int indent)
    {
        var row = new DockPanel { Margin = new Thickness(indent, 3, 0, 3) };
        var copy = new Button { Content = "Copy the link", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(6, 0, 0, 0) };
        copy.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(url);
                copy.Content = "Copied ✓";
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                copy.Content = "Could not copy — select the address and press Ctrl+C";
            }
        };
        AutomationProperties.SetName(copy, "Copy the link to " + what);
        var open = new Button { Content = "Open", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(6, 0, 0, 0) };
        open.Click += (_, _) => Open(url);
        DockPanel.SetDock(open, Dock.Right);
        DockPanel.SetDock(copy, Dock.Right);
        row.Children.Add(open);
        row.Children.Add(copy);
        var text = new StackPanel();
        text.Children.Add(new TextBlock { Text = what, TextWrapping = TextWrapping.Wrap });
        text.Children.Add(new TextBox { Text = url, IsReadOnly = true, BorderThickness = new Thickness(0), Background = System.Windows.Media.Brushes.Transparent, Foreground = Form.Res("AccentBrush") });
        row.Children.Add(text);
        return row;
    }

    private static Expander NewToGitHub()
    {
        var body = new StackPanel { Margin = new Thickness(4, 6, 0, 4) };
        foreach (string para in new[]
                 {
                     "GitHub (github.com) is a free service where people keep files online. GitHub Pages turns a repository — a folder of files on GitHub — into a website. TrafficLab+ puts your studies there; you hand in the address.",
                     "1. Make a free account at https://github.com/signup. Your user name becomes part of the address: https://<user name>.github.io/TrafficLab/.",
                     "2. Make a token — a password just for TrafficLab+: press GitHub token… above and follow its steps. It opens the right page on GitHub, says which boxes to tick, and checks the token works.",
                     "3. Press Publish. The first time, TrafficLab+ creates the TrafficLab repository and switches its website on. GitHub takes a minute or two to put it up; the address shows a \"404\" page until then.",
                     "4. Copy the link to hand in: the summary page, or one study's own page.",
                     "Everything on the website is public: anyone with the address can open it. Your study files stay on this computer.",
                 })
        {
            body.Children.Add(new TextBlock { Text = para, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) });
        }

        return new Expander { Header = new TextBlock { Text = "New to GitHub? How to set it up (click to open)", FontWeight = FontWeights.SemiBold }, Content = body, Margin = new Thickness(0, 8, 0, 0) };
    }

    private static void Heading(Panel p, string title, string help)
    {
        p.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 16, 0, 2) });
        p.Children.Add(new TextBlock { Text = help, TextWrapping = TextWrapping.Wrap, Foreground = Form.Res("MutedTextBrush") });
    }

    private static void Note(Panel p, string text, bool warn) =>
        p.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0), Foreground = Form.Res(warn ? "ErrorBrush" : "MutedTextBrush") });

    private static TextBox Box(Panel p, string label, string value, string help, bool required)
    {
        var box = new TextBox { Text = value, Padding = new Thickness(3) };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 2) };
        row.Children.Add(new Label { Content = label, Target = box, Padding = new Thickness(0), FontWeight = FontWeights.SemiBold });
        row.Children.Add(Form.Chip(required));
        p.Children.Add(row);
        AutomationProperties.SetName(box, label + (required ? " (needs a value)" : " (optional)"));
        p.Children.Add(box);
        p.Children.Add(new TextBlock { Text = help, TextWrapping = TextWrapping.Wrap, Foreground = Form.Res("MutedTextBrush"), Margin = new Thickness(0, 2, 0, 0) });
        return box;
    }

    private static string? Blank(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static void Open(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // the address is shown beside the button to copy
        }
    }

    private static void SavePage(StudySession s, Window owner)
    {
        Form.CommitPending();
        var dialog = new SaveFileDialog
        {
            Title = "Save the page",
            Filter = "Web page (*.html)|*.html",
            FileName = Safe(s.Study.Title) + ".html",
            InitialDirectory = Directory.Exists(AppSettings.StudiesFolder) ? AppSettings.StudiesFolder : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dialog.ShowDialog(owner) != true)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, PageBuilder.Build(s.Study));
            if (MessageBox.Show(owner, $"Saved the page as {dialog.FileName}.\n\nOpen it in your web browser now?", "TrafficLab+",
                    MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            {
                Open(dialog.FileName);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(owner, "The page could not be saved there: " + ex.Message + "\n\nTry another folder, such as Documents.", "TrafficLab+", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public static string Safe(string name)
    {
        string cleaned = string.Concat(name.Select(c => System.IO.Path.GetInvalidFileNameChars().Contains(c) ? '-' : c)).Trim();
        return cleaned.Length == 0 ? "study" : cleaned;
    }
}
