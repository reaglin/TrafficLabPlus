using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using TrafficLabPlus.Core.Build;
using TrafficLabPlus.Core.Model;

namespace TrafficLabPlus.App.Views;

/// <summary>Publish: GitHub Pages arrives in phase 7. Until then the page can be saved as a file.</summary>
public sealed class PublishView : SectionView
{
    public override void Rebuild()
    {
        (ScrollViewer scroll, StackPanel panel) = Column();
        Content = scroll;
        if (Session is not { } s)
        {
            return;
        }

        panel.Children.Add(Title("Publish"));
        panel.Children.Add(Lead("Putting the page on the web with GitHub Pages, and copying the link to hand in, arrive in a later version of TrafficLab+."));
        panel.Children.Add(Lead("Today you can save the page as a file. It is the same page as the one on the right: one file, with everything inside it. It opens in any web browser, with no internet needed, and can be put on any web site or sent to someone."));
        var form = new Form(panel, s);
        List<StudyProblem> problems = StudyValidator.Check(s.Study);
        if (problems.Count > 0)
        {
            form.Note($"The page cannot run yet: {problems.Count} problem{(problems.Count == 1 ? "" : "s")} to fix (listed at the top of the window). A page saved now shows those problems instead of the simulation.", Form.Res("ErrorBrush"));
        }

        form.Button("Save the page as a web page file…", "Saves an .html file wherever you choose.", () => SavePage(s, Window.GetWindow(this)), primary: true);
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
                Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            MessageBox.Show(owner, "The page could not be saved there: " + ex.Message + "\n\nTry another folder, such as Documents.", "TrafficLab+", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public static string Safe(string name)
    {
        string cleaned = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c)).Trim();
        return cleaned.Length == 0 ? "study" : cleaned;
    }
}

/// <summary>Settings: what a new study is filled in with, and where things are kept.</summary>
public sealed class SettingsView(AppSettings settings) : SectionView
{
    public override void Rebuild()
    {
        (ScrollViewer scroll, StackPanel panel) = Column();
        Content = scroll;
        panel.Children.Add(Title("Settings"));
        panel.Children.Add(Lead("What TrafficLab+ fills in for you, and where it keeps things. The AI's settings join these in a later version."));

        panel.Children.Add(Heading("For every new study"));
        Box(panel, "Author", "Your name, as it should appear on the pages you make.", settings.DefaultAuthor, v => settings.DefaultAuthor = v);
        Box(panel, "Course", "The class your studies are for, like \"CEN 3722\". Leave it empty if they are not for a class.", settings.DefaultCourse, v => settings.DefaultCourse = v);

        panel.Children.Add(Heading("Where things are kept"));
        Folder(panel, "Your studies are saved here unless you choose another place:", AppSettings.StudiesFolder);
        Folder(panel, "The page shown in the window is written here each time it is rebuilt:", AppSettings.PreviewFolder);
    }

    private void Box(StackPanel panel, string label, string help, string? value, Action<string?> set)
    {
        var box = new TextBox { Text = value ?? "", Padding = new Thickness(3) };
        System.Windows.Automation.AutomationProperties.SetName(box, label);
        box.LostKeyboardFocus += (_, _) =>
        {
            set(string.IsNullOrWhiteSpace(box.Text) ? null : box.Text.Trim());
            settings.Save();
        };
        panel.Children.Add(new Label { Content = label, Target = box, Padding = new Thickness(0), Margin = new Thickness(0, 8, 0, 2), FontWeight = FontWeights.SemiBold });
        panel.Children.Add(box);
        panel.Children.Add(new TextBlock { Text = help, TextWrapping = TextWrapping.Wrap, Foreground = Form.Res("MutedTextBrush") });
    }

    private static void Folder(StackPanel panel, string text, string folder)
    {
        panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 2) });
        panel.Children.Add(new TextBox { Text = folder, IsReadOnly = true, BorderThickness = new Thickness(0), Background = Brushes.Transparent, TextWrapping = TextWrapping.Wrap });
        var open = new Button { Content = "Open this folder", HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 2, 0, 0) };
        open.Click += (_, _) =>
        {
            try
            {
                Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            {
                MessageBox.Show("The folder could not be opened: " + ex.Message, "TrafficLab+", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };
        panel.Children.Add(open);
    }

    private static TextBlock Heading(string text) => new() { Text = text, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 16, 0, 2) };
}

/// <summary>About: who made TrafficLab+, where to read more, and the credits.</summary>
public sealed class AboutView : SectionView
{
    public const string SiteUrl = "https://softwareplus.ai/trafficlab/";

    public override void Rebuild()
    {
        (ScrollViewer scroll, StackPanel panel) = Column();
        Content = scroll;
        panel.Children.Add(Title("About TrafficLab+"));
        string version = typeof(AboutView).Assembly.GetName().Version?.ToString(3) ?? "";
        panel.Children.Add(Lead($"Version {version}. TrafficLab+ turns a real intersection, or a corridor of them, into a web page with a live traffic simulation — a puzzle a student solves with a budget, and hands in as a link."));

        // Ron, 2026-09-25: "Created by Dr. Ron Eaglin" with his picture, in every program's About
        var card = new DockPanel { Margin = new Thickness(0, 4, 0, 12) };
        var picture = new Border
        {
            Width = 96,
            Height = 96,
            CornerRadius = new CornerRadius(12),
            Margin = new Thickness(0, 0, 16, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/Images/dr-ron-eaglin.jpg"))) { Stretch = Stretch.UniformToFill },
        };
        System.Windows.Automation.AutomationProperties.SetName(picture, "Dr. Ron Eaglin");
        DockPanel.SetDock(picture, Dock.Left);
        card.Children.Add(picture);
        var words = new StackPanel();
        words.Children.Add(new TextBlock { Text = "Created by Dr. Ron Eaglin", FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
        words.Children.Add(new TextBlock { Text = "TrafficLab+ grew out of his LPGA Traffic Lab, a classroom simulation of the LPGA Blvd corridor at I-95 in Daytona Beach — built into TrafficLab+ as its example.", TextWrapping = TextWrapping.Wrap });
        var link = new Hyperlink(new Run("softwareplus.ai/trafficlab")) { NavigateUri = new Uri(SiteUrl) };
        link.RequestNavigate += (_, e) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (System.ComponentModel.Win32Exception)
            {
                MessageBox.Show("No web browser opened. The address is " + SiteUrl, "TrafficLab+");
            }
        };
        var more = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        more.Inlines.Add(new Run("More about TrafficLab+, with help and examples: "));
        more.Inlines.Add(link);
        words.Children.Add(more);
        card.Children.Add(words);
        panel.Children.Add(card);

        panel.Children.Add(new TextBlock { Text = "Credits", FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 4) });
        foreach (string line in new[]
                 {
                     "The traffic model: car-following (the Intelligent Driver Model), fixed-time signals, roundabouts by the Highway Capacity Manual's entry capacity, and v/c and level of service by HCM thresholds. A teaching model — planning-level, not an engineering analysis.",
                     "Map, roads and place search: © OpenStreetMap contributors, under the Open Database License (openstreetmap.org/copyright). Roads come from the Overpass API and searches from Nominatim.",
                     "The map uses Leaflet, © Volodymyr Agafonkin and CloudMade, under the BSD 2-Clause License.",
                     "The pages use the Overpass typeface by Delve Withrington, Dave Bailey and Thomas Jockin, under the SIL Open Font License.",
                 })
        {
            panel.Children.Add(new TextBlock { Text = line, TextWrapping = TextWrapping.Wrap, Foreground = Form.Res("MutedTextBrush"), Margin = new Thickness(0, 0, 0, 6) });
        }
    }
}
