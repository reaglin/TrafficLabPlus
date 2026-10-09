using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TrafficLabPlus.Core.Model;

namespace TrafficLabPlus.App.Views;

/// <summary>
/// Traffic: how much traffic there is today, where it comes in and goes out, trips that never
/// happen, and the demand buttons on the page. (FDOT counts and the AI's estimate come in phases 5
/// and 6.)
/// </summary>
public sealed class TrafficView : SectionView
{
    public const int MaxScenarios = 5;

    public override void Rebuild()
    {
        (ScrollViewer scroll, StackPanel panel) = Column();
        Content = scroll;
        if (Session is not { } s)
        {
            return;
        }

        Study st = s.Study;
        Demand dm = st.Demand;
        panel.Children.Add(Title("Traffic"));
        panel.Children.Add(Lead("How many vehicles use the network at today's demand, and where they come in and go out. Cars on the page pick their own routes between the road ends. The page's demand slider and its buttons (below) multiply today's numbers."));
        var form = new Form(panel, s);
        List<StudyNode> ends = st.Nodes.Where(n => n.Type == StudyNode.End).ToList();

        if (dm.Od is { Count: > 0 })
        {
            form.Note($"This study uses a typed table of {dm.Od.Count} trips between road ends, and the page uses it exactly as it stands. Editing that table arrives in a later version; the numbers below are not used while it is there.", Form.Res("ErrorBrush"));
        }

        form.Choice("How traffic is given", "Either one total that is shared out by how busy each road end is, or a number typed for each road end (from counts, for example).",
            [(Demand.Gravity, "One total, shared by how busy each road end is"), (Demand.Volumes, "Vehicles per hour typed for each road end")],
            () => dm.IsVolumes ? Demand.Volumes : Demand.Gravity, v => SwitchMode(st, ends, v), "demand/mode");

        Dictionary<string, double> entering = DemandShares.Entering(st);
        double totalIn = entering.Values.Sum();
        if (!dm.IsVolumes)
        {
            form.Number("Vehicles entering the whole study", "veh/h", "At today's demand, in the busiest hour, counting every vehicle that comes in at any road end.",
                () => dm.BaseTotal, v => { dm.BaseTotal = v; RebuildSoon(); }, 1, 50000, "demand/baseTotal");
        }

        form.Heading("Road ends", dm.IsVolumes
            ? "For each road end: the vehicles an hour that come in there, and how busy it is as a destination (trips leaving the study are shared out by these)."
            : "How busy each road end is, compared with the others: an end with 900 sends and receives about twice the trips of one with 450.");
        foreach (StudyNode n in ends)
        {
            string k = "node:" + n.Id + "/";
            double inHere = entering.GetValueOrDefault(n.Id);
            string share = !dm.IsVolumes && totalIn > 0
                ? $"{Math.Round(inHere):#,0} veh/h come in here today ({100 * inHere / totalIn:0}% of the total)."
                : "";
            if (dm.IsVolumes)
            {
                form.Number(n.Label + " — vehicles entering", "veh/h", "Vehicles an hour coming into the study here (0 if none).", () => n.Volume, v => n.Volume = v, 0, 20000, k + "volume");
            }

            form.Number(n.Label + (dm.IsVolumes ? " — how busy as a destination" : ""), "", share.Length > 0 ? share : "Compared with the other road ends.",
                () => n.Weight, v => { n.Weight = v; RebuildSoon(); }, 0, 100000, k + "weight");
        }

        NoTrips(form, s, ends);
        Scenarios(form, s);

        form.Heading("Real counts and estimates");
        form.Note("Looking up FDOT traffic counts for Florida roads, and asking the AI for an estimate, arrive in later versions of TrafficLab+.");
    }

    private void SwitchMode(Study st, List<StudyNode> ends, string mode)
    {
        Demand dm = st.Demand;
        double sum = ends.Sum(n => n.Weight ?? 0);
        if (mode == Demand.Volumes)
        {
            // start each end at its share of today's total, so the page does not change until a number does
            foreach (StudyNode n in ends)
            {
                n.Volume ??= sum > 0 && dm.BaseTotal is double t ? Math.Round(t * (n.Weight ?? 0) / sum) : 0;
            }
        }
        else
        {
            double typed = ends.Sum(n => n.Volume ?? 0);
            if (!(dm.BaseTotal > 0))
            {
                dm.BaseTotal = typed > 0 ? typed : 2400;
            }
        }

        dm.Mode = mode;
        RebuildSoon();
    }

    private void NoTrips(Form form, StudySession s, List<StudyNode> ends)
    {
        Demand dm = s.Study.Demand;
        form.Heading("Trips that do not happen", "Pairs of road ends with no trips between them — at LPGA, freeway traffic from the north ramps does not get off and back on to go south. Leave this empty unless you know of one.");
        foreach (List<string> pair in dm.NoTrips ?? [])
        {
            string a = s.Study.Node(pair.ElementAtOrDefault(0) ?? "")?.Label ?? "?", b = s.Study.Node(pair.ElementAtOrDefault(1) ?? "")?.Label ?? "?";
            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            var remove = new Button { Content = "Remove", Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(8, 0, 0, 0) };
            remove.Click += (_, _) => s.Edit("demand/noTrips", () =>
            {
                dm.NoTrips!.Remove(pair);
                if (dm.NoTrips.Count == 0)
                {
                    dm.NoTrips = null;
                }

                RebuildSoon();
            });
            DockPanel.SetDock(remove, Dock.Right);
            row.Children.Add(remove);
            row.Children.Add(new TextBlock { Text = $"No trips between {a} and {b}", VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
            form.Panel.Children.Add(row);
        }

        if (ends.Count >= 2)
        {
            var add = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            ComboBox first = EndsBox(ends), second = EndsBox(ends);
            var go = new Button { Content = "Add this pair", Padding = new Thickness(8, 2, 8, 2) };
            var why = new TextBlock { Foreground = Form.Res("ErrorBrush"), TextWrapping = TextWrapping.Wrap };
            go.Click += (_, _) =>
            {
                if (first.SelectedItem is not ComboBoxItem { Tag: string x } || second.SelectedItem is not ComboBoxItem { Tag: string y })
                {
                    why.Text = "Choose a road end in both lists.";
                    return;
                }

                if (x == y)
                {
                    why.Text = "Choose two different road ends.";
                    return;
                }

                s.Edit("demand/noTrips", () => (dm.NoTrips ??= []).Add([x, y]));
                RebuildSoon();
            };
            System.Windows.Automation.AutomationProperties.SetName(first, "First road end of the pair");
            System.Windows.Automation.AutomationProperties.SetName(second, "Second road end of the pair");
            add.Children.Add(first);
            add.Children.Add(new TextBlock { Text = " and ", VerticalAlignment = VerticalAlignment.Center });
            add.Children.Add(second);
            add.Children.Add(new Border { Width = 8 });
            add.Children.Add(go);
            form.Panel.Children.Add(add);
            form.Panel.Children.Add(why);
        }

        if (dm.NoTrips is { Count: > 0 })
        {
            form.Text("Why (shown on the page)", "One sentence, like \"Freeway-to-freeway trips stay on I-95.\"", () => dm.NoTripsWhy, v => dm.NoTripsWhy = v, "demand/noTripsWhy");
        }
    }

    private static ComboBox EndsBox(List<StudyNode> ends)
    {
        var box = new ComboBox { MinWidth = 180 };
        foreach (StudyNode n in ends)
        {
            box.Items.Add(new ComboBoxItem { Content = n.Label, Tag = n.Id });
        }

        return box;
    }

    private void Scenarios(Form form, StudySession s)
    {
        Demand dm = s.Study.Demand;
        form.Heading("Demand buttons on the page", $"Each is a button on the page's toolbar that sets demand to a share of today's: \"Today\" at 100%, a future year or a busy event day above it. Up to {MaxScenarios}. Players choose which to test their plan at.");
        List<Scenario> list = dm.Scenarios ?? [];
        for (int i = 0; i < list.Count; i++)
        {
            Scenario sc = list[i];
            form.Text($"Button {i + 1}: name", "What the button says.", () => sc.Name, v => sc.Name = v ?? "Today", "demand/scenarios");
            form.Number($"Button {i + 1}: demand", "% of today", "100 is today's traffic; 125 is a quarter more.", () => sc.Mult, v => sc.Mult = v, 10, 300, "demand/scenarios", 0, v => v * 100, v => v / 100);
            if (list.Count > 1)
            {
                var remove = new Button { Content = $"Remove button {i + 1}", HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(0, 4, 0, 4) };
                remove.Click += (_, _) => { s.Edit("demand/scenarios", () => list.Remove(sc)); RebuildSoon(); };
                form.Panel.Children.Add(remove);
            }
        }

        if (list.Count < MaxScenarios)
        {
            form.Button("Add a demand button", "", () =>
            {
                s.Edit("demand/scenarios", () => (dm.Scenarios ??= []).Add(new Scenario
                {
                    Name = list.Count == 0 ? "Today" : "Scenario " + (list.Count + 1).ToString(CultureInfo.CurrentCulture),
                    Mult = list.Count == 0 ? 1 : 1.25,
                }));
                RebuildSoon();
            });
        }
    }
}
