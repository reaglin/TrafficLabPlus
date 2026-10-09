using System.IO;
using System.Net.Http;
using System.Windows;
using Eaglin.AiManager;
using Eaglin.AiManager.Wpf;

namespace TrafficLabPlus.App.Ai;

/// <summary>One answer from the AI, or the reason there is not one.</summary>
public sealed class AiAnswer
{
    public string Text { get; init; } = "";
    public string Provider { get; init; } = "";
    public string Model { get; init; } = "";
    public double? Cost { get; init; }
    public string Problem { get; init; } = "";

    public bool Ok => Problem.Length == 0;

    public static AiAnswer Failed(string problem) => new() { Problem = problem };
}

/// <summary>
/// Everything TrafficLab+ does with an AI, over the shared <c>Eaglin.AiManager</c> package (never a
/// provider SDK, never a second key store: the key typed here is the one every one of Ron Eaglin's
/// programs uses, and the usage ledger is shared). Opened lazily; never throws on the way in.
/// TrafficLab+ works fully without an AI: every AI button says what it needs when none is set up.
/// </summary>
public sealed class TrafficAi
{
    public const string AppName = "TrafficLabPlus";

    public const string NeedsSettings =
        "This uses an AI, and no AI key has been set up yet. An AI key is a password-like code from an AI company — Anthropic " +
        "(console.anthropic.com), Google (aistudio.google.com), OpenAI and others: you sign up there, add a way to pay (a question " +
        "usually costs a few cents), copy the key and paste it into AI settings (Settings, on the left, then AI settings…). Your " +
        "instructor may give the class one. It is kept on this computer only, and works in every one of Dr. Ron Eaglin's programs. " +
        "Everything else in TrafficLab+ works without it.";

    /// <summary>What the coach's window says: it explains, it changes nothing.</summary>
    public string CoachStatus => IsReady
        ? Status.Replace("Nothing it suggests changes your study until you tick it and press Apply.", "The coach only explains; it changes nothing.", StringComparison.Ordinal)
        : Status;

    private AiHub? _hub;
    private string _openProblem = "";

    public static TrafficAi Shared { get; } = new();

    public bool IsReady => Hub() is { } h && h.IsAvailable();

    /// <summary>What pressing an AI button sends and costs, ready or not. Never empty.</summary>
    public string Status
    {
        get
        {
            AiHub? hub = Hub();
            if (hub is null)
            {
                return _openProblem.Length > 0 ? "The AI could not be opened: " + _openProblem : NeedsSettings;
            }

            if (!hub.IsAvailable())
            {
                return NeedsSettings;
            }

            AiProviderType p = hub.DefaultProvider;
            return $"AI ready: {AiHub.DisplayName(p)} ({hub.DefaultModel(p)}). Asking sends your words and the study's roads, intersections and traffic " +
                   "(no names of people) to that provider, and charges the account whose key you entered — usually a few cents. " +
                   "Nothing it suggests changes your study until you tick it and press Apply. Settings, then What the AI has cost…, shows the total.";
        }
    }

    public bool ShowSettings(Window owner)
    {
        if (Hub() is not { } hub)
        {
            MessageBox.Show(owner, "The AI could not be opened.\n\n" + _openProblem + "\n\nEverything else in TrafficLab+ works without it.",
                "AI settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        bool saved = new AiSettingsWindow(hub) { Owner = owner }.ShowDialog() == true;
        hub.Reload();
        return saved;
    }

    public void ShowUsage(Window owner)
    {
        if (Hub() is not { } hub)
        {
            MessageBox.Show(owner, "The AI could not be opened, so there is nothing to show.\n\n" + _openProblem,
                "What the AI has cost", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        new UsageDashboardWindow(hub, AppName) { Owner = owner }.Show();
    }

    /// <summary>Sends one prompt. Anything that goes wrong comes back as <see cref="AiAnswer.Problem"/>, in words.</summary>
    public async Task<AiAnswer> AskAsync(string system, string prompt, string purpose, CancellationToken ct = default)
    {
        if (Hub() is not { } hub)
        {
            return AiAnswer.Failed(_openProblem.Length > 0 ? _openProblem : NeedsSettings);
        }

        if (!hub.IsAvailable())
        {
            return AiAnswer.Failed(NeedsSettings);
        }

        try
        {
            AiResponse r = await hub.CompleteAsync(new AiRequest { System = system, User = prompt, MaxTokens = 4000, Purpose = purpose }, ct).ConfigureAwait(true);
            return new AiAnswer { Text = r.Text, Provider = AiHub.DisplayName(r.Provider), Model = r.Model, Cost = r.EstimatedCostUsd };
        }
        catch (AiException ex) when (ex.Kind == AiErrorKind.Refused)
        {
            return AiAnswer.Failed("The AI declined this request. Try saying it another way, or choose another AI provider (Settings, on the left, then AI settings…). A refusal costs nothing.");
        }
        catch (AiException ex)
        {
            return AiAnswer.Failed(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return AiAnswer.Failed("TrafficLab+ could not reach the AI service. Check that this computer is online and try again. (Details: " + ex.Message + ")");
        }
        catch (TaskCanceledException)
        {
            return AiAnswer.Failed(ct.IsCancellationRequested ? "Stopped." : "The AI took too long and the request was stopped. Try again.");
        }
    }

    private AiHub? Hub()
    {
        if (_hub is not null)
        {
            return _hub;
        }

        try
        {
            _hub = AiHub.Open(AppName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _openProblem = ex.Message;
        }

        return _hub;
    }
}
