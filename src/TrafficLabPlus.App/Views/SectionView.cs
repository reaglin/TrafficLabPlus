using System.Windows;
using System.Windows.Controls;

namespace TrafficLabPlus.App.Views;

/// <summary>
/// One section of the window (Network, Traffic, Challenge…). It is given the open study, rebuilds
/// itself when the study is replaced (open, new, undo), and is told about every other change.
/// </summary>
public abstract class SectionView : UserControl
{
    protected StudySession? Session { get; private set; }

    public void Attach(StudySession? session)
    {
        if (Session is not null)
        {
            Session.Reloaded -= OnReloaded;
            Session.Changed -= OnChanged;
        }

        Session = session;
        if (session is not null)
        {
            session.Reloaded += OnReloaded;
            session.Changed += OnChanged;
        }

        Rebuild();
    }

    /// <summary>Builds the section again from the study.</summary>
    public abstract void Rebuild();

    /// <summary>Any change. Most sections need nothing: their own boxes made it.</summary>
    protected virtual void StudyChanged()
    {
    }

    /// <summary>Rebuilds after the current input event, so a box is not taken away while it is
    /// still handing over its value.</summary>
    protected void RebuildSoon() => Dispatcher.BeginInvoke(Rebuild, System.Windows.Threading.DispatcherPriority.Background);

    private void OnReloaded(object? sender, EventArgs e) => Rebuild();

    private void OnChanged(object? sender, EventArgs e) => StudyChanged();

    /// <summary>A scrolling column for a form, with room to breathe.</summary>
    protected static (ScrollViewer Scroll, StackPanel Panel) Column()
    {
        var panel = new StackPanel { Margin = new Thickness(18, 14, 18, 24), MaxWidth = 900, HorizontalAlignment = HorizontalAlignment.Left };
        var scroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        return (scroll, panel);
    }

    /// <summary>An AI helper's button: says it uses AI, and opens the window that shows each proposed
    /// change before anything is applied.</summary>
    protected Button AiButton(string text, Func<Ai.AiWindow> window)
    {
        var b = new Button
        {
            Content = text,
            Padding = new Thickness(10, 4, 10, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 6, 4),
            ToolTip = "",
        };
        b.ToolTipOpening += (_, _) => b.ToolTip = "Uses AI. " + Ai.TrafficAi.Shared.Status;   // read when shown: it changes once a key is set up
        b.Click += (_, _) =>
        {
            Ai.AiWindow w = window();
            w.Owner = Window.GetWindow(this);
            if (w.ShowDialog() == true && w.Applied > 0)
            {
                Rebuild();
                MessageBox.Show(Window.GetWindow(this),
                    $"{w.Applied} change{(w.Applied == 1 ? "" : "s")} from the AI applied. Each is marked \"suggested by the AI\" under its value, and the page has been rebuilt.\n\nEdit ▸ Undo (Ctrl+Z) takes them all back in one step.",
                    "TrafficLab+", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        };
        return b;
    }

    protected static TextBlock Title(string text) => new() { Text = text, Style = (Style)Application.Current.Resources["SectionTitle"] };

    protected static TextBlock Lead(string text) => new() { Text = text, Style = (Style)Application.Current.Resources["Lead"] };
}
