using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using TrafficLabPlus.App.Views;
using TrafficLabPlus.Core.Build;
using TrafficLabPlus.Core.Model;
using TrafficLabPlus.Core.Osm;

namespace TrafficLabPlus.App;

/// <summary>
/// The window: the sections down the left in the order the work happens (Map ▸ Network ▸ Traffic ▸
/// Challenge ▸ Preview ▸ Publish), the section's form in the middle, and on the right the page
/// itself — built from the study after every change and shown in WebView2, so what the window
/// shows is the file that will be published (rule 1).
/// </summary>
public partial class MainWindow : Window
{
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly RecentStudies _recent = new(AppSettings.RecentFile);
    private readonly DispatcherTimer _rebuild = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly Dictionary<string, SectionView> _sections = [];
    private StudySession? _session;
    private string? _pagePath;
    private string? _builtJson;
    private bool _shownOnce;
    private GridLength _editorWidth = new(580);

    public MainWindow(string? openPath = null)
    {
        InitializeComponent();

        // tools/store-shots.ps1 sets the window to the Store screenshot size, e.g. TRAFFICLAB_WINDOW=1920x1080
        if (Environment.GetEnvironmentVariable("TRAFFICLAB_WINDOW") is { } size && size.Split('x') is [var w, var h]
            && double.TryParse(w, out double width) && double.TryParse(h, out double height))
        {
            Width = width / (VisualTreeHelperDpi());
            Height = height / (VisualTreeHelperDpi());
        }
        _sections["Start"] = new StartView(OpenExample, () => Go(NavMap), NewStudy, OpenStudy, OpenFile, () => _recent.Load());
        _sections["Map"] = new MapView(MakeFromMap);
        _sections["Network"] = new NetworkView();
        _sections["Traffic"] = new TrafficView();
        _sections["Challenge"] = new ChallengeView();
        _sections["Preview"] = new PreviewHelpView(async () =>
        {
            // the page's own read-only summary of its last test (TL_lastTest in trafficlab-ui.js)
            string? json = await Preview.RunScriptAsync("window.TL_lastTest ? JSON.stringify(window.TL_lastTest()) : 'null'");
            return json is null ? null : System.Text.Json.JsonSerializer.Deserialize<string>(json);
        });
        _sections["Publish"] = new PublishView(_settings, () => _recent.Load());
        _sections["Settings"] = new SettingsView(_settings);
        _sections["About"] = new AboutView();
        _rebuild.Tick += (_, _) => { _rebuild.Stop(); BuildPage(); };
        Closing += OnClosing;
        PreviewKeyDown += OnPreviewKeyDown;

        ShowStudyState();
        NavStart.IsChecked = true;
        if (openPath is not null)
        {
            Loaded += (_, _) => OpenFile(openPath);
        }
    }

    // the screen's scale, so a size in pixels comes out as that many pixels
    private static double VisualTreeHelperDpi()
    {
        using var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
        return g.DpiX / 96.0;
    }

    // ---------------------------------------------------------------- the sections

    private string _current = "Start";

    private void Section_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string name })
        {
            ShowSection(name);
        }
    }

    private void ShowSection(string name)
    {
        // the width the student gave the form is kept when a section without a page is shown
        if (EditorColumn.Width.IsAbsolute && PreviewPane.Visibility == Visibility.Visible && EditorColumn.Width.Value > 0)
        {
            _editorWidth = EditorColumn.Width;
        }

        _current = name;
        bool withPage = _session is not null && name is "Network" or "Traffic" or "Challenge" or "Publish" or "Preview";
        SectionHost.Content = _sections.GetValueOrDefault(name);
        if (_sections.TryGetValue(name, out SectionView? view))
        {
            view.Attach(name is "Start" or "Settings" or "About" ? null : _session);
        }

        EditorColumn.Width = name == "Preview" && withPage ? new GridLength(400) : withPage ? _editorWidth : new GridLength(1, GridUnitType.Star);
        Splitter.Visibility = withPage ? Visibility.Visible : Visibility.Collapsed;
        PreviewPane.Visibility = withPage ? Visibility.Visible : Visibility.Collapsed;
        PreviewColumn.Width = withPage ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        if (withPage && _builtJson is null)
        {
            BuildPage();
        }

        ShowSteps();
    }

    // ---------------------------------------------------------------- Previous / Next

    /// <summary>The work, in order. Previous and Next walk it.</summary>
    private static readonly (string Name, string Title)[] Steps =
    [
        ("Map", "Map"), ("Network", "Network"), ("Traffic", "Traffic"), ("Challenge", "Challenge"), ("Preview", "Preview"), ("Publish", "Publish"),
    ];

    private RadioButton NavFor(string name) => name switch
    {
        "Map" => NavMap,
        "Network" => NavNetwork,
        "Traffic" => NavTraffic,
        "Challenge" => NavChallenge,
        "Preview" => NavPreview,
        "Publish" => NavPublish,
        _ => NavStart,
    };

    private void ShowSteps()
    {
        int i = Array.FindIndex(Steps, s => s.Name == _current);
        StepBar.Visibility = i < 0 ? Visibility.Collapsed : Visibility.Visible;
        if (i < 0)
        {
            return;
        }

        StepTitle.Text = $"Step {i + 1} of {Steps.Length}: {Steps[i].Title}";
        PrevButton.Visibility = i > 0 ? Visibility.Visible : Visibility.Hidden;
        PrevButton.Content = i > 0 ? "← Previous: " + Steps[i - 1].Title : "";
        NextButton.Visibility = i < Steps.Length - 1 ? Visibility.Visible : Visibility.Hidden;
        NextButton.Content = i < Steps.Length - 1 ? "Next: " + Steps[i + 1].Title + " →" : "";
        // a screen reader says where the button goes, as the button does
        System.Windows.Automation.AutomationProperties.SetName(PrevButton, i > 0 ? "Previous step: " + Steps[i - 1].Title : "Previous step");
        System.Windows.Automation.AutomationProperties.SetName(NextButton, i < Steps.Length - 1 ? "Next step: " + Steps[i + 1].Title : "Next step");

        // the map is where a study starts: until there is one, the steps after it have nothing to show
        bool canGoOn = _session is not null;
        NextButton.IsEnabled = canGoOn;
        StepNote.Text = !canGoOn
            ? "Make a study first — press Make the study… on this page once you have chosen intersections — or start one from a layout or an example on the Start screen. Then Next takes you on."
            : _current switch
            {
                "Map" => "This step is optional if your study was made from a layout or an example: press Next.",
                "Publish" => "The last step. Go back to any step to change something; the page is rebuilt each time.",
                _ => "You can come back to any step at any time; the sections on the left go straight to one.",
            };
    }

    private void Prev_Click(object sender, RoutedEventArgs e)
    {
        int i = Array.FindIndex(Steps, s => s.Name == _current);
        if (i > 0)
        {
            Go(NavFor(Steps[i - 1].Name));
        }
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        int i = Array.FindIndex(Steps, s => s.Name == _current);
        if (i >= 0 && i < Steps.Length - 1 && _session is not null)
        {
            Go(NavFor(Steps[i + 1].Name));
        }
    }

    private void Go(RadioButton nav)
    {
        if (nav.IsChecked == true)
        {
            ShowSection((string)nav.Tag);
        }
        else
        {
            nav.IsChecked = true;
        }
    }

    // ---------------------------------------------------------------- the open study

    private void Open(StudySession session, string section = "Network")
    {
        if (_session is not null)
        {
            _session.Changed -= Session_Changed;
        }

        _session = session;
        session.Changed += Session_Changed;
        _builtJson = null;
        _shownOnce = false;
        ShowStudyState();
        CheckProblems();
        Go(section == "Network" ? NavNetwork : NavPreview);
        BuildPage();
    }

    private void Session_Changed(object? sender, EventArgs e)
    {
        ShowStudyState();
        CheckProblems();
        PreviewState.Text = "Rebuilding the page…";
        _rebuild.Stop();
        _rebuild.Start();
        CommandManager.InvalidateRequerySuggested();
    }

    private void ShowStudyState()
    {
        ShowSteps();
        bool open = _session is not null;
        // the map needs no study: it is where one starts
        foreach (RadioButton nav in new[] { NavNetwork, NavTraffic, NavChallenge, NavPreview, NavPublish })
        {
            nav.IsEnabled = open;
        }

        CloseMenu.IsEnabled = open;
        if (_session is not { } s)
        {
            Title = "TrafficLab+";
            StudyName.Text = "No study open";
            SaveState.Text = "Start a new study or open one.";
            return;
        }

        Title = s.DisplayName + (s.IsDirty ? " •" : "") + " — TrafficLab+";
        StudyName.Text = s.DisplayName;
        SaveState.Text = s.IsExample && !s.IsDirty ? "The built-in example. Save as… keeps your own copy."
            : s.Path is null ? "Not saved yet — File ▸ Save"
            : s.IsDirty ? "Changes not saved yet (Ctrl+S)"
            : "Saved";
    }

    private void CheckProblems()
    {
        List<StudyProblem> problems = _session is null ? [] : StudyValidator.Check(_session.Study);
        ProblemsBar.Visibility = problems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ProblemsTitle.Text = problems.Count == 1
            ? "The page cannot run until this is fixed (it shows this message instead of the simulation):"
            : $"The page cannot run until these {problems.Count} things are fixed (it shows them instead of the simulation):";
        ProblemsList.Items.Clear();
        foreach (StudyProblem p in problems.Take(6))
        {
            var link = new Button
            {
                Content = new TextBlock { Text = "• " + p.Message, TextWrapping = TextWrapping.Wrap },
                HorizontalAlignment = HorizontalAlignment.Left,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0, 1, 0, 1),
                Cursor = Cursors.Hand,
                ToolTip = "Go to it",
            };
            link.Click += (_, _) => GoToProblem(p);
            ProblemsList.Items.Add(link);
        }

        if (problems.Count > 6)
        {
            ProblemsList.Items.Add(new TextBlock { Text = $"…and {problems.Count - 6} more." });
        }
    }

    private void GoToProblem(StudyProblem p)
    {
        if (_session is not { } s)
        {
            return;
        }

        string? select = null;
        if (p.Path.StartsWith("nodes[", StringComparison.Ordinal) && int.TryParse(p.Path[6..^1], out int ni) && ni < s.Study.Nodes.Count)
        {
            select = "node:" + s.Study.Nodes[ni].Id;
        }
        else if (p.Path.StartsWith("node ", StringComparison.Ordinal))
        {
            select = "node:" + p.Path[5..];
        }
        else if (p.Path.StartsWith("links[", StringComparison.Ordinal) && int.TryParse(p.Path[6..^1], out int li) && li < s.Study.Links.Count)
        {
            select = "link:" + s.Study.Links[li].Id;
        }
        else if (p.Path.StartsWith("pockets.", StringComparison.Ordinal))
        {
            select = "link:" + p.Path[8..].Split(':')[0];
        }
        else if (p.Path == "budget")
        {
            Go(NavChallenge);
            return;
        }
        else if (p.Path.StartsWith("demand", StringComparison.Ordinal))
        {
            Go(NavTraffic);
            return;
        }

        Go(NavNetwork);
        if (select is not null && _sections["Network"] is NetworkView network)
        {
            Dispatcher.BeginInvoke(() => network.Choose(select), DispatcherPriority.Background);
        }
    }

    // ---------------------------------------------------------------- the page

    private void BuildPage()
    {
        if (_session is not { } s || PreviewPane.Visibility != Visibility.Visible)
        {
            return;
        }

        string json = StudyJson.Write(s.Study);
        if (json == _builtJson && _pagePath is not null)
        {
            PreviewState.Text = "Rebuilt after every change. Try it as a player would: build a plan, run a test, submit it.";
            return;
        }

        try
        {
            string page = PageBuilder.Build(s.Study);
            Directory.CreateDirectory(AppSettings.PreviewFolder);
            _pagePath = Path.Combine(AppSettings.PreviewFolder, (s.IsExample ? "_example-" : "") + PublishView.Safe(s.DisplayName.Replace(" (example)", "", StringComparison.Ordinal)) + ".html");
            File.WriteAllText(_pagePath, page);
            _builtJson = json;
            // the same address again must still reload: PreviewView reloads when it is set again
            Preview.Source = null;
            // the first showing of a study opens with the page's instructions, as a player sees it;
            // a rebuild after an edit goes straight back to the map
            Preview.Source = _shownOnce ? new UriBuilder(new Uri(_pagePath)) { Query = "nointro" }.Uri : new Uri(_pagePath);
            _shownOnce = true;
            BrowserButton.IsEnabled = true;
            PreviewState.Text = "Rebuilt at " + DateTime.Now.ToString("T", System.Globalization.CultureInfo.CurrentCulture) + ", after the last change. Try it as a player would: build a plan, run a test, submit it.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            PreviewState.Text = "The page could not be written to " + AppSettings.PreviewFolder + ": " + ex.Message;
        }
    }

    private void Browser_Click(object sender, RoutedEventArgs e)
    {
        if (_pagePath is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_pagePath) { UseShellExecute = true });
        }
        catch (Win32Exception ex)
        {
            MessageBox.Show(this, "No web browser opened (" + ex.Message + "). The page is saved here, and any browser can open it:" +
                                  Environment.NewLine + Environment.NewLine + _pagePath,
                "TrafficLab+", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // ---------------------------------------------------------------- files

    private void OpenExample(BuiltInExample example)
    {
        if (!ConfirmDiscard())
        {
            return;
        }

        Open(new StudySession(new StudyDocument { Study = example.Study() }, null, isExample: true));
    }

    private void NewStudy()
    {
        if (!ConfirmDiscard())
        {
            return;
        }

        var dialog = new NewStudyWindow(_settings) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Request is null)
        {
            return;
        }

        var session = new StudySession(new StudyDocument { Study = StudyTemplates.Create(dialog.Request) }, null, isExample: false);
        session.MarkUnsaved();
        Open(session);
    }

    /// <summary>The Map section's last step: name the study, set its budget, build it from the chosen
    /// intersections and open it, with OpenStreetMap's answer kept inside it.</summary>
    private Task MakeFromMap(OsmCache cache)
    {
        var map = (MapView)_sections["Map"];
        string? first = map.Junctions.FirstOrDefault(j => j.Id == cache.Junctions.FirstOrDefault())?.Label;
        string? title = first is null ? null : (cache.Junctions.Count == 1 ? first : first.Split(" & ")[0]) + " Traffic Lab";
        var dialog = new NewStudyWindow(_settings, cache.Junctions.Count, map.Area, title) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Request is not { } r || !ConfirmDiscard())
        {
            return Task.CompletedTask;
        }

        OsmStudy made;
        try
        {
            made = NetworkFromOsm.Build(OsmData.Parse(cache.Overpass), new OsmStudyRequest
            {
                Junctions = cache.Junctions,
                Title = r.Title,
                Place = r.Place,
                Author = r.Author,
                Course = r.Course,
                Budget = r.Budget,
            });
        }
        catch (Exception ex) when (ex is ArgumentException or StudyFormatException)
        {
            MessageBox.Show(this, "The study could not be made: " + ex.Message, "TrafficLab+", MessageBoxButton.OK, MessageBoxImage.Warning);
            return Task.CompletedTask;
        }

        var doc = new StudyDocument { Study = made.Study };
        doc.Attachments[OsmCache.Entry] = cache.ToBytes();
        if (made.Notes.Count > 0)
        {
            doc.Notes = "Made from OpenStreetMap " + cache.Fetched + ". Things to check:" + Environment.NewLine
                        + string.Join(Environment.NewLine, made.Notes.Select(n => "- " + n)) + Environment.NewLine;
        }

        var session = new StudySession(doc, null, isExample: false);
        session.MarkUnsaved();
        Open(session);
        int signals = made.Study.Nodes.Count(n => n.Type != StudyNode.End);
        MessageBox.Show(this,
            $"Made \"{made.Study.Title}\": {signals} intersection{(signals == 1 ? "" : "s")}, {made.Study.Links.Count} roads, with lanes, turn lanes, speeds and names from OpenStreetMap where it has them." + Environment.NewLine + Environment.NewLine
            + "Each road that leaves the study stops at a road end: an edge of the study, where cars come in and go out." + Environment.NewLine + Environment.NewLine
            + (made.Notes.Count > 0 ? "Things to check:" + Environment.NewLine + string.Join(Environment.NewLine, made.Notes.Take(5).Select(n => "• " + n))
                + (made.Notes.Count > 5 ? Environment.NewLine + $"…and {made.Notes.Count - 5} more" : "")
                + Environment.NewLine + "(All of them are kept in Challenge ▸ Your notes.)" + Environment.NewLine + Environment.NewLine : "")
            + "Signal timing and traffic volumes start at plain values: set them in Network and Traffic. Then save the study (Ctrl+S).",
            "TrafficLab+", MessageBoxButton.OK, MessageBoxImage.Information);
        return Task.CompletedTask;
    }

    private void OpenStudy()
    {
        if (!ConfirmDiscard())
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Open a TrafficLab+ study",
            Filter = "TrafficLab+ study (*.trafficlab)|*.trafficlab|Study as JSON (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = Directory.Exists(AppSettings.StudiesFolder) ? AppSettings.StudiesFolder : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dialog.ShowDialog(this) == true)
        {
            OpenFile(dialog.FileName, confirmed: true);
        }
    }

    private void OpenFile(string path) => OpenFile(path, confirmed: false);

    private void OpenFile(string path, bool confirmed)
    {
        if (!confirmed && !ConfirmDiscard())
        {
            return;
        }

        try
        {
            StudyDocument doc = StudyFile.Load(path);
            bool zip = path.EndsWith(StudyFile.Extension, StringComparison.OrdinalIgnoreCase);
            var session = new StudySession(doc, zip ? path : null, isExample: false);
            if (zip)
            {
                _recent.Add(path);
            }
            else
            {
                session.MarkUnsaved();   // a .json study is saved as a .trafficlab file, which can also hold notes
            }

            Open(session);
        }
        catch (StudyFormatException ex)
        {
            MessageBox.Show(this, ex.Message, "TrafficLab+", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _recent.Remove(path);
            MessageBox.Show(this, "The study could not be opened: " + ex.Message, "TrafficLab+", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private bool Save()
    {
        Form.CommitPending();
        if (_session is not { } s)
        {
            return false;
        }

        return s.Path is null ? SaveAs() : SaveTo(s.Path);
    }

    private bool SaveAs()
    {
        Form.CommitPending();
        if (_session is not { } s)
        {
            return false;
        }

        Directory.CreateDirectory(AppSettings.StudiesFolder);
        var dialog = new SaveFileDialog
        {
            Title = "Save the study",
            Filter = "TrafficLab+ study (*.trafficlab)|*.trafficlab",
            FileName = PublishView.Safe(s.Path is null ? s.Study.Title : Path.GetFileNameWithoutExtension(s.Path)) + StudyFile.Extension,
            InitialDirectory = s.Path is null ? AppSettings.StudiesFolder : Path.GetDirectoryName(s.Path),
        };
        return dialog.ShowDialog(this) == true && SaveTo(dialog.FileName);
    }

    private bool SaveTo(string path)
    {
        try
        {
            _session!.SaveAs(path);
            _recent.Add(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, "The study could not be saved: " + ex.Message + Environment.NewLine + Environment.NewLine +
                                  "Nothing was lost: it is still open here. Try Save as… and another folder, such as Documents.",
                "TrafficLab+", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    /// <summary>Asks before unsaved changes would be lost. False means stay.</summary>
    private bool ConfirmDiscard()
    {
        Form.CommitPending();
        if (_session is not { IsDirty: true } s)
        {
            return true;
        }

        MessageBoxResult answer = MessageBox.Show(this,
            $"Save the changes to \"{s.DisplayName}\" first?" + Environment.NewLine + Environment.NewLine +
            "Yes saves them, No throws them away, Cancel goes back to the study.",
            "TrafficLab+", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return answer switch
        {
            MessageBoxResult.Yes => Save(),
            MessageBoxResult.No => true,
            _ => false,
        };
    }

    /// <summary>Ctrl+Z in a box with nothing of its own to undo undoes the last change to the study,
    /// as it does everywhere else in the window (a box takes Ctrl+Z for its own typing first).</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_session is null || Keyboard.Modifiers != ModifierKeys.Control || Keyboard.FocusedElement is not TextBox box)
        {
            return;
        }

        if (e.Key == Key.Z && !box.CanUndo && _session.CanUndo)
        {
            _session.Undo();
            e.Handled = true;
        }
        else if (e.Key == Key.Y && !box.CanRedo && _session.CanRedo)
        {
            _session.Redo();
            e.Handled = true;
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!ConfirmDiscard())
        {
            e.Cancel = true;
        }
    }

    // ---------------------------------------------------------------- menu and keys

    private void New_Executed(object sender, ExecutedRoutedEventArgs e) => NewStudy();

    private void Open_Executed(object sender, ExecutedRoutedEventArgs e) => OpenStudy();

    private void Save_Executed(object sender, ExecutedRoutedEventArgs e) => Save();

    private void SaveAs_Executed(object sender, ExecutedRoutedEventArgs e) => SaveAs();

    private void Undo_Executed(object sender, ExecutedRoutedEventArgs e) => _session?.Undo();

    private void Redo_Executed(object sender, ExecutedRoutedEventArgs e) => _session?.Redo();

    private void HasStudy(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = _session is not null;

    private void CanUndo(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = _session?.CanUndo == true;

    private void CanRedo(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = _session?.CanRedo == true;

    private void ExamplesMenu_Opened(object sender, RoutedEventArgs e)
    {
        ExamplesMenu.Items.Clear();
        foreach (BuiltInExample example in Examples.All)
        {
            var item = new MenuItem { Header = example.Title, ToolTip = example.Description };
            item.Click += (_, _) => OpenExample(example);
            ExamplesMenu.Items.Add(item);
        }
    }

    private void CloseStudy_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscard())
        {
            return;
        }

        if (_session is not null)
        {
            _session.Changed -= Session_Changed;
        }

        _session = null;
        _builtJson = null;
        Preview.Source = null;
        BrowserButton.IsEnabled = false;
        ShowStudyState();
        CheckProblems();
        Go(NavStart);
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void StartScreen_Click(object sender, RoutedEventArgs e) => Go(NavStart);

    private void About_Click(object sender, RoutedEventArgs e) => Go(NavAbout);

    private void RecentMenu_Opened(object sender, RoutedEventArgs e)
    {
        RecentMenu.Items.Clear();
        IReadOnlyList<string> files = _recent.Load();
        if (files.Count == 0)
        {
            RecentMenu.Items.Add(new MenuItem { Header = "(none yet)", IsEnabled = false });
        }

        foreach (string file in files)
        {
            var item = new MenuItem { Header = Path.GetFileNameWithoutExtension(file).Replace("_", "__", StringComparison.Ordinal), ToolTip = file };
            item.Click += (_, _) => OpenFile(file);
            RecentMenu.Items.Add(item);
        }
    }
}
