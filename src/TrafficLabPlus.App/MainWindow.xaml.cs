using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using Microsoft.Win32;
using TrafficLabPlus.Core.Build;

namespace TrafficLabPlus.App;

/// <summary>
/// For now (phases 0–2): shows a study's page, built from the study, in the window — the LPGA
/// example to start with — and opens the same file in a browser.
/// </summary>
public partial class MainWindow : Window
{
    private string? _pagePath;

    public MainWindow()
    {
        InitializeComponent();
        Show(PageBuilder.LpgaExampleJson(), ExampleName);
    }

    // the example's preview has a name no study file gets, so opening an "lpga.json" never replaces it
    private const string ExampleName = "_example-lpga";

    /// <summary>Where built pages are written for the preview: Documents\TrafficLabPlus\Preview.</summary>
    private static string PreviewFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrafficLabPlus", "Preview");

    private void Show(string studyJson, string name)
    {
        try
        {
            string page = PageBuilder.Build(studyJson);
            Directory.CreateDirectory(PreviewFolder);
            _pagePath = Path.Combine(PreviewFolder, name + ".html");
            File.WriteAllText(_pagePath, page);
            StudyTitle.Text = JsonNode.Parse(studyJson)?["title"]?.GetValue<string>() ?? name;
            // the same address again (a study opened twice, after a correction) must still reload
            Preview.Source = null;
            Preview.Source = new Uri(_pagePath);
            BrowserButton.IsEnabled = true;
        }
        catch (StudyFormatException ex)
        {
            MessageBox.Show(this, ex.Message, "TrafficLab+", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, "The page could not be written to " + PreviewFolder + ": " + ex.Message,
                "TrafficLab+", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenStudy_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open a TrafficLab+ study",
            Filter = "TrafficLab+ study (*.json)|*.json|All files (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            Show(File.ReadAllText(dialog.FileName), Path.GetFileNameWithoutExtension(dialog.FileName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, "The study could not be read: " + ex.Message, "TrafficLab+",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Example_Click(object sender, RoutedEventArgs e) => Show(PageBuilder.LpgaExampleJson(), ExampleName);

    private void Browser_Click(object sender, RoutedEventArgs e)
    {
        if (_pagePath is not null)
        {
            try
            {
                Process.Start(new ProcessStartInfo(_pagePath) { UseShellExecute = true });
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                MessageBox.Show(this, "No web browser opened (" + ex.Message + "). The page is saved here, and any browser can open it:" +
                                      Environment.NewLine + Environment.NewLine + _pagePath,
                    "TrafficLab+", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
