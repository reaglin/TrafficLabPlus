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

    protected static TextBlock Title(string text) => new() { Text = text, Style = (Style)Application.Current.Resources["SectionTitle"] };

    protected static TextBlock Lead(string text) => new() { Text = text, Style = (Style)Application.Current.Resources["Lead"] };
}
