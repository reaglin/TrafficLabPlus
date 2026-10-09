using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using TrafficLabPlus.App.Views;
using TrafficLabPlus.Core.Ai;
using TrafficLabPlus.Core.Model;

namespace TrafficLabPlus.App.Ai;

/// <summary>
/// One window for the three AI helpers that propose changes (network, traffic, challenge): the
/// student says what they know, presses Ask, and sees every proposed change — what, from what to
/// what, and why — each ticked; Apply makes the ticked ones, as one undo. Nothing the AI says
/// reaches the study unseen (rule 4). The coach uses it too, with an answer and no changes.
/// </summary>
public sealed class AiWindow : Window
{
    private readonly StudySession _session;
    private readonly Func<Study, string, string> _prompt;
    private readonly Func<Study, string, AiProposals>? _read;
    private readonly string _purpose;
    private readonly string _system;
    private readonly TextBox _words = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90, Padding = new Thickness(4) };
    private readonly Button _ask = new() { Content = "Ask the AI", Style = (Style)Application.Current.Resources["Primary"], HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0) };
    private readonly TextBlock _state = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
    private readonly StackPanel _results = new();
    private readonly Button _apply = new() { Content = "Apply the ticked changes", Style = (Style)Application.Current.Resources["Primary"], Margin = new Thickness(0, 0, 8, 0), IsEnabled = false };
    private AiProposals? _proposals;
    private readonly CancellationTokenSource _stop = new();
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Foreground = Form.Res("MutedTextBrush"), FontSize = 12, Margin = new Thickness(0, 0, 0, 10) };

    public AiWindow(StudySession session, string title, string lead, string wordsLabel, string example, string purpose,
                    Func<Study, string, string> prompt, Func<Study, string, AiProposals>? read, string? system = null, bool wordsOptional = false)
    {
        _session = session;
        _prompt = prompt;
        _read = read;
        _purpose = purpose;
        _system = system ?? AiPrompts.System;
        Title = title + " — TrafficLab+";
        Width = 720;
        Height = 760;
        MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Form.Res("PageBrush");
        FontSize = 14;

        var p = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
        p.Children.Add(new TextBlock { Text = title, Style = (Style)Application.Current.Resources["SectionTitle"] });
        p.Children.Add(new TextBlock { Text = lead, Style = (Style)Application.Current.Resources["Lead"] });
        _status.Text = read is null ? TrafficAi.Shared.CoachStatus : TrafficAi.Shared.Status;
        p.Children.Add(_status);
        if (!TrafficAi.Shared.IsReady)
        {
            // set it up from here, rather than being sent away to find it
            var setup = new Button { Content = "Set up an AI now…", Padding = new Thickness(10, 4, 10, 4), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 10) };
            setup.Click += (_, _) =>
            {
                TrafficAi.Shared.ShowSettings(this);
                _status.Text = read is null ? TrafficAi.Shared.CoachStatus : TrafficAi.Shared.Status;
                _ask.IsEnabled = TrafficAi.Shared.IsReady;
                _state.Text = TrafficAi.Shared.IsReady ? "" : "No AI is set up yet.";
                setup.Visibility = TrafficAi.Shared.IsReady ? Visibility.Collapsed : Visibility.Visible;
            };
            p.Children.Add(setup);
        }


        var label = new StackPanel { Orientation = Orientation.Horizontal };
        label.Children.Add(new Label { Content = wordsLabel, Target = _words, Padding = new Thickness(0), FontWeight = FontWeights.SemiBold });
        label.Children.Add(Form.Chip(required: !wordsOptional));
        p.Children.Add(label);
        p.Children.Add(_words);
        AutomationProperties.SetName(_words, wordsLabel);
        p.Children.Add(new TextBlock { Text = "For example: " + example, TextWrapping = TextWrapping.Wrap, Foreground = Form.Res("MutedTextBrush"), Margin = new Thickness(0, 2, 0, 0) });
        p.Children.Add(_ask);
        p.Children.Add(_state);
        p.Children.Add(_results);
        _ask.IsEnabled = TrafficAi.Shared.IsReady;
        if (!TrafficAi.Shared.IsReady)
        {
            _state.Text = "No AI is set up yet: press Set up an AI now… above.";
        }

        _ask.Click += async (_, _) => await Ask(wordsOptional);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(20, 8, 20, 14) };
        if (read is not null)
        {
            buttons.Children.Add(_apply);
        }

        var close = new Button { Content = read is null ? "Close" : "Cancel", IsCancel = true, Padding = new Thickness(12, 6, 12, 6) };
        buttons.Children.Add(close);
        _apply.Click += (_, _) => Apply();

        var root = new DockPanel();
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(new ScrollViewer { Content = p, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = root;
        Loaded += (_, _) => _words.Focus();
        // closing while the AI is still answering stops the request
        Closed += (_, _) => _stop.Cancel();
    }

    /// <summary>How many changes were applied (0 if none, or closed).</summary>
    public int Applied { get; private set; }

    private async Task Ask(bool wordsOptional)
    {
        if (!wordsOptional && string.IsNullOrWhiteSpace(_words.Text))
        {
            _state.Text = "Type what you know or want first — the AI works from your words.";
            return;
        }

        _ask.IsEnabled = false;
        _results.Children.Clear();
        _apply.IsEnabled = false;
        _state.Text = "Asking the AI… (usually 10–40 seconds)";
        AiAnswer answer = await TrafficAi.Shared.AskAsync(_system, _prompt(_session.Study, _words.Text), _purpose, _stop.Token);
        if (_stop.IsCancellationRequested)
        {
            return;
        }

        _ask.IsEnabled = true;
        _ask.Content = "Ask again";
        if (!answer.Ok)
        {
            _state.Text = answer.Problem;
            return;
        }

        string cost = answer.Cost is double c ? $", about ${c.ToString("0.00", CultureInfo.InvariantCulture)}" : "";
        if (_read is null)
        {
            _state.Text = $"Answered by {answer.Provider} ({answer.Model}{cost}). A suggestion, not a verdict: test any idea on the page.";
            _results.Children.Add(new Border
            {
                Background = Form.Res("PanelBrush"),
                BorderBrush = Form.Res("LineBrush"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 8, 0, 0),
                Child = new TextBox { Text = AiApply.Tidy(answer.Text), TextWrapping = TextWrapping.Wrap, IsReadOnly = true, BorderThickness = new Thickness(0), Background = System.Windows.Media.Brushes.Transparent },
            });
            return;
        }

        try
        {
            _proposals = _read(_session.Study, answer.Text);
        }
        catch (AiReadException ex)
        {
            _state.Text = ex.Message;
            return;
        }

        _state.Text = _proposals.Changes.Count == 0
            ? $"Answered by {answer.Provider} ({answer.Model}{cost}), but it proposed nothing TrafficLab+ can use (see below). Press Ask again, perhaps with more detail."
            : $"Answered by {answer.Provider} ({answer.Model}{cost}): {_proposals.Changes.Count} change{(_proposals.Changes.Count == 1 ? "" : "s")} proposed. " +
              "Read each one; untick any you do not want; then Apply the ticked changes. Nothing has changed yet. Applying is one step: Edit ▸ Undo (Ctrl+Z) puts everything back.";
        if (_proposals.Notes.Length > 0)
        {
            _results.Children.Add(new TextBlock { Text = "The AI's notes: " + _proposals.Notes, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 4), FontStyle = FontStyles.Italic });
        }

        foreach (AiProposal pr in _proposals.Changes)
        {
            var text = new StackPanel();
            text.Children.Add(new TextBlock { Text = $"{pr.Target} — {pr.Setting}: {pr.Before} → {pr.After}", FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            if (pr.Reason.Length > 0)
            {
                text.Children.Add(new TextBlock { Text = "Why: " + pr.Reason, TextWrapping = TextWrapping.Wrap, Foreground = Form.Res("MutedTextBrush") });
            }

            var box = new CheckBox { Content = text, IsChecked = true, Margin = new Thickness(0, 6, 0, 0) };
            AutomationProperties.SetName(box, $"{pr.Target}, {pr.Setting}, from {pr.Before} to {pr.After}");
            box.Checked += (_, _) => { pr.Use = true; Count(); };
            box.Unchecked += (_, _) => { pr.Use = false; Count(); };
            _results.Children.Add(box);
        }

        if (_proposals.LeftOut.Count > 0)
        {
            _results.Children.Add(new TextBlock { Text = "Left out, because TrafficLab+ could not use them:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 2) });
            foreach (string why in _proposals.LeftOut)
            {
                _results.Children.Add(new TextBlock { Text = "• " + why, TextWrapping = TextWrapping.Wrap, Foreground = Form.Res("MutedTextBrush") });
            }
        }

        Count();
    }

    private void Count()
    {
        int n = _proposals?.Changes.Count(c => c.Use) ?? 0;
        _apply.IsEnabled = n > 0;
        _apply.Content = n switch { 0 => "Apply", 1 => "Apply the ticked change", _ => $"Apply the {n} ticked changes" };
    }

    private void Apply()
    {
        if (_proposals is null)
        {
            return;
        }

        int n = 0;
        _session.Edit(null, () => n = AiApply.Apply(_session.Study, _proposals.Changes));
        Applied = n;
        DialogResult = true;
    }
}
