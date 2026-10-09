using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TrafficLabPlus.Core.Model;

namespace TrafficLabPlus.App.Views;

/// <summary>
/// Builds the editing forms: every field is a label, the box, a sentence on what it means, and —
/// for a value about the road — where the value came from (Ron's rule: say where every number came
/// from). A number that is out of range is refused beside the box, in words, and never reaches the
/// study. Every change goes through the session, so it can be undone.
/// </summary>
public sealed class Form(StackPanel panel, StudySession session)
{
    private static readonly CultureInfo Culture = CultureInfo.CurrentCulture;

    public StackPanel Panel => panel;

    public static Brush Res(string key) => (Brush)Application.Current.Resources[key];

    public void Heading(string text, string? help = null)
    {
        panel.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, panel.Children.Count == 0 ? 0 : 18, 0, 2),
            TextWrapping = TextWrapping.Wrap,
        });
        if (help is not null)
        {
            Note(help);
        }
    }

    public TextBlock Note(string text, Brush? colour = null)
    {
        var note = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = colour ?? Res("MutedTextBrush"),
            Margin = new Thickness(0, 0, 0, 8),
        };
        panel.Children.Add(note);
        return note;
    }

    public void Text(string label, string help, Func<string?> get, Action<string?> set, string? key = null, bool multiLine = false)
    {
        var box = new TextBox
        {
            Text = get() ?? "",
            AcceptsReturn = multiLine,
            TextWrapping = multiLine ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MinHeight = multiLine ? 60 : 0,
            Padding = new Thickness(3),
        };
        Field(label, help, box, key, out TextBlock? origin, out _);
        void Commit()
        {
            string? value = string.IsNullOrWhiteSpace(box.Text) ? null : box.Text.Trim();
            if (value != (get() ?? null))
            {
                session.Edit(key, () => set(value));
                Forget(box);
                ShowOrigin(origin, key);
            }
        }

        box.LostKeyboardFocus += (_, _) => Commit();
        if (!multiLine)
        {
            box.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    Commit();
                }
            };
        }
    }

    /// <summary>A number shown in the person's unit (<paramref name="show"/>) and kept in the
    /// study's (<paramref name="keep"/>), refused outside <paramref name="min"/>–<paramref name="max"/>.</summary>
    public void Number(string label, string unit, string help, Func<double?> get, Action<double> set,
                       double min, double max, string? key = null, int decimals = 0,
                       Func<double, double>? show = null, Func<double, double>? keep = null)
    {
        show ??= v => v;
        keep ??= v => v;
        var box = new TextBox { Width = 110, Padding = new Thickness(3), HorizontalContentAlignment = HorizontalAlignment.Right };
        var unitText = new TextBlock { Text = unit, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        row.Children.Add(box);
        row.Children.Add(unitText);
        void Fill() => box.Text = get() is double v ? Math.Round(show(v), decimals).ToString("0." + new string('#', decimals), Culture) : "";
        Fill();
        Field(label, help, row, key, out TextBlock? origin, out TextBlock error);
        AutomationProperties.SetName(box, label);

        void Commit()
        {
            string text = box.Text.Trim().Replace("$", "", StringComparison.Ordinal).Replace("%", "", StringComparison.Ordinal);
            if (!double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, Culture, out double typed) || !double.IsFinite(typed))
            {
                error.Text = $"Type a number from {Fmt(min)} to {Fmt(max)} {unit}.";
                error.Visibility = Visibility.Visible;
                return;
            }

            if (typed < min || typed > max)
            {
                error.Text = $"{Fmt(typed)} is outside what TrafficLab+ allows here: type a number from {Fmt(min)} to {Fmt(max)} {unit}.";
                error.Visibility = Visibility.Visible;
                return;
            }

            error.Visibility = Visibility.Collapsed;
            double kept = keep(typed);
            // unchanged unless the shown value changed: showing rounds, and rounding must not edit
            if (get() is double old && Math.Round(show(old), decimals) == Math.Round(typed, decimals))
            {
                return;
            }

            session.Edit(key, () => set(kept));
            Forget(box);
            ShowOrigin(origin, key);
        }

        box.LostKeyboardFocus += (_, _) => Commit();
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Commit();
            }
        };
        string Fmt(double v) => v.ToString("0.##", Culture);
    }

    public void Choice(string label, string help, IReadOnlyList<(string Value, string Text)> options, Func<string> get,
                       Action<string> set, string? key = null)
    {
        var combo = new ComboBox { MinWidth = 220, HorizontalAlignment = HorizontalAlignment.Left };
        foreach ((string value, string text) in options)
        {
            combo.Items.Add(new ComboBoxItem { Content = text, Tag = value });
        }

        combo.SelectedItem = combo.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == get());
        Field(label, help, combo, key, out TextBlock? origin, out _);
        AutomationProperties.SetName(combo, label);
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is ComboBoxItem { Tag: string value } && value != get())
            {
                session.Edit(key, () => set(value));
                ShowOrigin(origin, key);
            }
        };
    }

    public void Check(string text, string help, Func<bool> get, Action<bool> set, string? key = null)
    {
        var box = new CheckBox { Content = text, IsChecked = get(), Margin = new Thickness(0, 6, 0, 0) };
        panel.Children.Add(box);
        TextBlock? origin = OriginLine(key);
        panel.Children.Add(new TextBlock { Text = help, TextWrapping = TextWrapping.Wrap, Foreground = Res("MutedTextBrush"), Margin = new Thickness(20, 0, 0, origin is null ? 6 : 0) });
        if (origin is not null)
        {
            origin.Margin = new Thickness(20, 0, 0, 6);
            panel.Children.Add(origin);
        }

        void Changed()
        {
            bool value = box.IsChecked == true;
            if (value != get())
            {
                session.Edit(key, () => set(value));
                ShowOrigin(origin, key);
            }
        }

        box.Checked += (_, _) => Changed();
        box.Unchecked += (_, _) => Changed();
    }

    public Button Button(string text, string help, Action click, bool primary = false)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(10, 4, 10, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 6, 0, 2),
            ToolTip = help,
        };
        if (primary)
        {
            button.Style = (Style)Application.Current.Resources["Primary"];
        }

        button.Click += (_, _) => click();
        panel.Children.Add(button);
        if (help.Length > 0)
        {
            Note(help);
        }

        return button;
    }

    private void Field(string label, string help, FrameworkElement input, string? key, out TextBlock? origin, out TextBlock error)
    {
        var title = new Label { Content = label, Padding = new Thickness(0), Margin = new Thickness(0, 8, 0, 2), FontWeight = FontWeights.SemiBold, Target = input };
        panel.Children.Add(title);
        panel.Children.Add(input);
        if (input is not StackPanel)
        {
            AutomationProperties.SetName(input, label);
        }

        error = new TextBlock { Foreground = Res("ErrorBrush"), TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 2, 0, 0) };
        panel.Children.Add(error);
        panel.Children.Add(new TextBlock { Text = help, TextWrapping = TextWrapping.Wrap, Foreground = Res("MutedTextBrush"), Margin = new Thickness(0, 2, 0, 0) });
        origin = OriginLine(key);
        if (origin is not null)
        {
            panel.Children.Add(origin);
        }
    }

    /// <summary>Once a box's value is in the study, Ctrl+Z belongs to the study's undo: the box drops
    /// its own typing history, which would otherwise take the key and undo only the text.</summary>
    private static void Forget(TextBox box)
    {
        box.IsUndoEnabled = false;
        box.IsUndoEnabled = true;
    }

    /// <summary>Hands the value in the box being typed in to the study, before a save or a close
    /// reads the study (a box otherwise commits only on Enter or when it loses focus).</summary>
    public static void CommitPending()
    {
        if (Keyboard.FocusedElement is TextBox { IsReadOnly: false } box)
        {
            // leaving the box commits it; focus goes back once the value is in
            Keyboard.ClearFocus();
            box.Focus();
        }
    }

    private TextBlock? OriginLine(string? key)
    {
        // only values about the roads and the traffic say where they came from; the words and the
        // budget are the author's own
        if (key is null || !(key.StartsWith("node:", StringComparison.Ordinal) || key.StartsWith("link:", StringComparison.Ordinal)
                             || key.StartsWith("demand/", StringComparison.Ordinal) || key.StartsWith("costs/", StringComparison.Ordinal)))
        {
            return null;
        }

        var line = new TextBlock { FontSize = 12, FontStyle = FontStyles.Italic, Foreground = Res("MutedTextBrush"), TextWrapping = TextWrapping.Wrap };
        ShowOrigin(line, key);
        return line;
    }

    private void ShowOrigin(TextBlock? line, string? key)
    {
        if (line is null || key is null)
        {
            return;
        }

        string words = Origins.Describe(StudyEdits.OriginOf(session.Study, key));
        line.Text = words.Length == 0 ? "" : "Where this came from: " + words;
        line.Visibility = words.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }
}
