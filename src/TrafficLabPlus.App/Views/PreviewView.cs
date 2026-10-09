using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace TrafficLabPlus.App.Views;

/// <summary>
/// engine (WebView2), so what the student sees here is what a visitor sees in a browser.
/// engine (WebView2), so what the teacher sees here is what a player sees in a browser.
///
/// Two things it takes care of so nothing else has to:
/// <list type="bullet">
/// <item>Its working folder is under the user's local app data. The default is next to the
/// program, which in a Store install is read-only, and the view would silently never start.</item>
/// <item>A machine without the WebView2 runtime (an old, locked-down Windows 10) gets a sentence
/// saying so and pointing at Open in your browser, rather than an empty grey box.</item>
/// </list>
/// </summary>
public sealed class PreviewView : ContentControl
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(Uri), typeof(PreviewView),
        new PropertyMetadata(null, (d, _) => ((PreviewView)d).Show()));

    private readonly WebView2 _web = new();
    private readonly TextBlock _problem = new()
    {
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(12),
        Visibility = Visibility.Collapsed,
    };

    private bool _starting;
    private bool _ready;

    public PreviewView()
    {
        var grid = new Grid();
        grid.Children.Add(_web);
        grid.Children.Add(_problem);
        Content = grid;

        _web.Visibility = Visibility.Collapsed;
        System.Windows.Automation.AutomationProperties.SetName(_web, "The page, as a visitor sees it");
    }

    /// <summary>The page to play. Null shows nothing.</summary>
    public Uri? Source
    {
        get => (Uri?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>Where WebView2 keeps its cache and settings for this app.</summary>
    public static string DataFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrafficLabPlus", "WebView2");

    private async void Show()
    {
        if (Source is null)
        {
            _web.Visibility = Visibility.Collapsed;
            return;
        }

        if (!_ready)
        {
            if (_starting)
            {
                return;   // the page is navigated to when the start finishes
            }

            _starting = true;

            try
            {
                CoreWebView2Environment environment =
                    await CoreWebView2Environment.CreateAsync(userDataFolder: DataFolder);
                await _web.EnsureCoreWebView2Async(environment);
                _ready = true;
            }
            catch (WebView2RuntimeNotFoundException)
            {
                ShowProblem("TrafficLab+ cannot show the page inside the window on this computer, because " +
                            "Microsoft's WebView2 (part of Edge) is not installed. Press Open in your " +
                            "browser above — the page works exactly the same there.");
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
                                           or System.Runtime.InteropServices.COMException)
            {
                ShowProblem("The page could not be shown inside the window (" + ex.Message + "). " +
                            "Press Open in your browser above — the page works exactly the same there.");
                return;
            }
            finally
            {
                _starting = false;
            }
        }

        if (Source is Uri page)
        {
            // navigating to the address already shown does not reload it; a rebuilt page must
            if (_web.CoreWebView2.Source == page.AbsoluteUri)
            {
                _problem.Visibility = Visibility.Collapsed;
                _web.Visibility = Visibility.Visible;
                _web.CoreWebView2.Reload();
                return;
            }

            _problem.Visibility = Visibility.Collapsed;
            _web.Visibility = Visibility.Visible;
            _web.CoreWebView2.Navigate(page.AbsoluteUri);
        }
    }

    /// <summary>Runs a script in the page shown and returns its JSON result, or null if no page is shown.</summary>
    public async Task<string?> RunScriptAsync(string script)
    {
        if (!_ready || _web.CoreWebView2 is null)
        {
            return null;
        }

        try
        {
            return await _web.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }

    private void ShowProblem(string message)
    {
        _web.Visibility = Visibility.Collapsed;
        _problem.Text = message;
        _problem.Visibility = Visibility.Visible;
    }
}
