using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TrafficLabPlus.Core.Model;

namespace TrafficLabPlus.App.Views;

/// <summary>
/// The start screen: what TrafficLab+ is for, the steps from an intersection to a link, and the
/// ways in — the built-in examples first (LPGA, then the four-way intersection), because seeing a finished page is the quickest way to
/// understand what a study becomes.
/// </summary>
public sealed class StartView(Action<BuiltInExample> example, Action fromMap, Action newStudy, Action open, Action<string> openRecent, Func<IReadOnlyList<string>> recent) : SectionView
{
    public override void Rebuild()
    {
        (ScrollViewer scroll, StackPanel panel) = Column();
        panel.MaxWidth = 860;
        panel.HorizontalAlignment = HorizontalAlignment.Left;
        Content = scroll;

        panel.Children.Add(new TextBlock { Text = "TrafficLab+", FontSize = 28, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(Lead("Turn a real intersection — or a row of them along a corridor — into a web page with a live traffic simulation. Players see today's traffic, build a plan to fix it on a budget (turn lanes, extra lanes, signal timing, roundabouts), test it, and submit it for a score. Students hand in the page as a link."));

        string[] steps =
        [
            "Start a study — find your intersection on the map and bring in its real roads, or start from a simple layout.",
            "Shape the network: streets, lanes, speeds, the signal timing and turn lanes that exist today.",
            "Set the traffic, and the challenge: how many vehicles, the budget, what each fix costs.",
            "Check the page on the right as you go — it is exactly the page that will be published. Save it as a file; publishing to the web and handing in the link come in a later version.",
        ];
        for (int i = 0; i < steps.Length; i++)
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var dot = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(12), Background = Form.Res("AccentBrush"), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Top };
            dot.Child = new TextBlock { Text = (i + 1).ToString(System.Globalization.CultureInfo.CurrentCulture), Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(dot, Dock.Left);
            row.Children.Add(dot);
            row.Children.Add(new TextBlock { Text = steps[i], TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
            panel.Children.Add(row);
        }

        var ways = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        // the built-in examples first: a finished page is the quickest way to see what a study becomes
        foreach (BuiltInExample ex in Examples.All)
        {
            ways.Children.Add(Way("Open the " + ex.Title + " example", ex.Description + " Play it, change it, save your own copy.", () => example(ex), primary: ex == Examples.All[0]));
        }

        ways.Children.Add(Way("New study from the map", "Find your intersection, bring in its roads from OpenStreetMap, choose up to ten intersections.", fromMap));
        ways.Children.Add(Way("New study from a layout…", "One signal, a T, or a row of signals that you shape to match the real roads.", newStudy));
        ways.Children.Add(Way("Open a study file…", "A .trafficlab file you or someone else saved.", open));
        panel.Children.Add(ways);

        IReadOnlyList<string> files = recent();
        if (files.Count > 0)
        {
            panel.Children.Add(new TextBlock { Text = "Continue where you left off", FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 18, 0, 4) });
            foreach (string file in files)
            {
                var b = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(10, 4, 10, 4),
                    Margin = new Thickness(0, 2, 0, 2),
                    ToolTip = file,
                };
                var text = new TextBlock();
                text.Inlines.Add(new System.Windows.Documents.Run(Path.GetFileNameWithoutExtension(file)) { FontWeight = FontWeights.SemiBold });
                text.Inlines.Add(new System.Windows.Documents.Run("   " + Path.GetDirectoryName(file)) { Foreground = Form.Res("MutedTextBrush") });
                b.Content = text;
                b.Click += (_, _) => openRecent(file);
                panel.Children.Add(b);
            }
        }
    }

    private static Button Way(string title, string body, Action click, bool primary = false)
    {
        var content = new StackPanel { Width = 230 };
        content.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4), TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap, Opacity = 0.9 });
        var b = new Button
        {
            Content = content,
            MinHeight = 110,
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 10, 10),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Top,
        };
        if (primary)
        {
            b.Style = (Style)Application.Current.Resources["Primary"];
            b.HorizontalContentAlignment = HorizontalAlignment.Left;
        }

        System.Windows.Automation.AutomationProperties.SetName(b, title);
        b.Click += (_, _) => click();
        return b;
    }
}
