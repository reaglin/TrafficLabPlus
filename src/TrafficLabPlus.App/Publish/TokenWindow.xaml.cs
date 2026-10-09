using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using System.Windows.Navigation;
using TrafficLabPlus.Core.Publish;

namespace TrafficLabPlus.App.Publish;

/// <summary>
/// Paste a GitHub token, have it checked with GitHub, keep it. A token GitHub does not accept is
/// never saved: a publish that fails an hour later is worse than a box that says "no" now.
/// </summary>
public partial class TokenWindow : Window
{
    public TokenWindow()
    {
        InitializeComponent();
        ShowCurrent();
    }

    /// <summary>The GitHub account the saved token belongs to, once it has been checked here.</summary>
    public string? Account { get; private set; }

    /// <summary>Shows the window over <paramref name="owner"/>. True when a token was saved or removed.</summary>
    public static bool Show(Window owner, out string? account)
    {
        var window = new TokenWindow { Owner = owner };
        window.ShowDialog();
        account = window.Account;
        return window._changed;
    }

    /// <summary>
    /// Whether a token is saved, said so it cannot be missed (Ron, 2026-09-24): green with a tick
    /// when there is one, and whose account it is.
    /// </summary>
    private void ShowCurrent()
    {
        string? token = TokenStore.Load();
        string? account = TokenStore.LoadAccount();

        if (token is null)
        {
            Current.Text = "No token is saved yet.";
            Current.Foreground = (System.Windows.Media.Brush)FindResource("MutedTextBrush");
            Current.FontWeight = FontWeights.Normal;
        }
        else
        {
            Current.Text = "✓ A GitHub token is saved" + (account is null ? "" : $" — account {account}") +
                           $" ({TokenStore.Mask(token)}). Paste a new one only to replace it.";
            Current.Foreground = (System.Windows.Media.Brush)FindResource("OkBrush");
            Current.FontWeight = FontWeights.SemiBold;
        }

        RemoveButton.IsEnabled = token is not null;
    }

    private void Say(string text, bool good)
    {
        Result.Text = text;
        Result.Foreground = (System.Windows.Media.Brush)FindResource(good ? "OkBrush" : "WarnBrush");
        Result.FontWeight = FontWeights.SemiBold;
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        string token = TokenBox.Password.Trim();

        if (token.Length == 0)
        {
            Say("Paste the token into the box first.", good: false);
            return;
        }

        SaveButton.IsEnabled = false;
        Result.Text = "Checking the token with GitHub…";

        try
        {
            using var api = new GitHubApi(token);
            GitHubUser user = await api.GetUserAsync().ConfigureAwait(true);

            TokenStore.Save(token);
            TokenStore.SaveAccount(user.Login);
            Account = user.Login;
            TokenBox.Clear();
            ShowCurrent();
            Say($"✓ Token accepted and saved — it belongs to the GitHub account {user.Login}. Its permissions are tested on your " +
                "first publish; if GitHub refuses then, the message says which permission is missing. You can close this window and publish.", good: true);
            _changed = true;
        }
        catch (GitHubException ex)
        {
            Say("✗ Not saved. " + ex.Message, good: false);
        }
        catch (HttpRequestException ex)
        {
            Say("✗ Not saved: GitHub could not be reached. Check the internet connection and try again. (" + ex.Message + ")", good: false);
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private bool _changed;

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        TokenStore.Clear();
        _changed = true;
        ShowCurrent();
        Say("The token is removed from this computer. (It still exists on GitHub until you delete it there.)", good: false);
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnLink(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true })?.Dispose();
        e.Handled = true;
    }
}
