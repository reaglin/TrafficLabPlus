using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TrafficLabPlus.Core.Counts;
using TrafficLabPlus.Core.Model;
using TrafficLabPlus.Core.Osm;

namespace TrafficLabPlus.App.Views;

/// <summary>
/// Traffic: how much traffic there is today, where it comes in and goes out, trips that never
/// happen, and the demand buttons on the page. For a Florida study made from the map, FDOT's counts
/// fill the volumes in (AADT × K × D), each one shown with its site, year and factors before it is used.
/// (The AI's estimate comes in phase 6.)
/// </summary>
public sealed class TrafficView : SectionView
{
    public const int MaxScenarios = 5;

    private readonly FdotClient _fdot = new(OsmClient.CreateHttp());
    private List<CountMatch>? _matches;
    private string? _answer;
    private string? _savedOn;
    private string? _lookupProblem;
    private object? _matchedFor;

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
        panel.Children.Add(Guides.Traffic());
        var form = new Form(panel, s);
        form.ChipLegend();
        List<StudyNode> ends = st.Nodes.Where(n => n.Type == StudyNode.End).ToList();

        if (dm.Od is { Count: > 0 })
        {
            form.Note($"This study uses a typed table of {dm.Od.Count} trips between road ends, and the page uses it exactly as it stands. Editing that table arrives in a later version; the numbers below are not used while it is there.", Form.Res("ErrorBrush"));
        }

        Counts(form, s);

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

            bool counted = StudyEdits.OriginOf(st, k + "weight") == Origins.Fdot;
            form.Number(n.Label + (dm.IsVolumes ? " — how busy as a destination" : ""), counted ? "veh/h out" : "",
                counted ? "Vehicles an hour leaving the study here, from the count; trips leaving are shared out by these." : share.Length > 0 ? share : "Compared with the other road ends.",
                () => n.Weight, v => { n.Weight = v; RebuildSoon(); }, 0, 100000, k + "weight");
        }

        NoTrips(form, s, ends);
        Scenarios(form, s);

        form.Heading("Estimates from the AI", "For roads with no count: the AI estimates the vehicles an hour coming in at each road end, from the kind of road, the place, what you tell it, and any counts in use (which it keeps). You see each estimate with its reason and choose which to use.");
        form.Panel.Children.Add(AiButton("Estimate traffic with the AI…", () => new Ai.AiWindow(s, "Estimate traffic with the AI",
            "The AI estimates the busy-hour vehicles an hour coming in at each road end that has no count. Road ends with FDOT counts in use are kept as they are."
            + (s.Study.Demand.IsVolumes ? "" : " Applying any estimate switches How traffic is given to typed volumes; the other road ends keep today's numbers."),
            "Anything you know about the traffic",
            "\"Weekday evening rush. The outlet mall on Outlet Boulevard is busy; Cornerstone is a quiet back road.\"",
            "trafficlab-traffic", Core.Ai.AiPrompts.Traffic, Core.Ai.AiPrompts.ReadTraffic, wordsOptional: true)));
    }

    // ------------------------------------------------------------ FDOT counts

    private void Counts(Form form, StudySession s)
    {
        form.Heading("Traffic counts from FDOT (Florida roads)",
            "FDOT counts traffic on state and most county roads and publishes each count as AADT: the Annual Average Daily Traffic, "
            + "vehicles a day in both directions. Two factors turn a day into a busy hour: K, the share of the day's traffic in the design hour "
            + "(usually about 9%), and D, the share going the busier way in that hour (usually 55–58%). TrafficLab+ takes AADT × K × D as the "
            + "vehicles an hour coming in at a road end, as if the busier direction were toward the intersection — a busy hour to test against. "
            + "A ramp runs one way: an off-ramp brings AADT × K in; an on-ramp only takes traffic out.");

        if (!ReferenceEquals(_matchedFor, s.Document))
        {
            _matchedFor = s.Document;
            _matches = null;
            _lookupProblem = null;
            if (FdotCache.FromBytes(s.Document.Attachments.GetValueOrDefault(FdotCache.Entry)) is { } saved)
            {
                Match(s, saved.Json, saved.Choices);
                _savedOn = saved.Fetched;
            }
        }

        if (s.Study.Geo is not { } geo)
        {
            form.Note(FdotClient.NotFromMap);
            return;
        }

        if (!geo.InFlorida)
        {
            form.Note(FdotClient.NotInFlorida);
            return;
        }

        Button look = form.Button(_matches is null ? "Look up FDOT counts for these roads" : "Look up FDOT counts again",
            "Asks FDOT's public traffic-count map once; the answer is kept in the study file.", () => { }, primary: _matches is null);
        look.Click += async (_, _) =>
        {
            look.IsEnabled = false;
            look.Content = "Asking FDOT…";
            try
            {
                string json = await _fdot.CountsAsync(s.Study);
                Match(s, json, null);
                _savedOn = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                _lookupProblem = null;
                Keep(s);
            }
            catch (OsmServiceException ex)
            {
                _lookupProblem = ex.Message;
            }

            Rebuild();
        };

        if (_lookupProblem is not null)
        {
            form.Note(_lookupProblem, Form.Res("ErrorBrush"));
        }

        if (_matches is null)
        {
            return;
        }

        // what is in use is read from the study itself, so it stays true after an undo or a reopen
        List<StudyNode> ends = s.Study.Nodes.Where(n => n.Type == StudyNode.End).ToList();
        bool inUse = ends.Any(n => StudyEdits.OriginOf(s.Study, $"node:{n.Id}/volume") == Origins.Fdot);
        int found = _matches.Count(m => m.Found);
        if (found == 0)
        {
            form.Note("FDOT has no counts on these roads. " + FdotClient.TypeInstead);
            return;
        }

        form.Note(inUse
            ? $"Counts in use (looked up {_savedOn}): the road ends below say \"from FDOT traffic counts\", and the page credits FDOT."
            : $"Found FDOT counts for {found} of the {_matches.Count} road ends (looked up {_savedOn}). Check each one, change K or D if you know better, untick any that look wrong, then press Use the ticked counts.");
        form.Note("A count looks wrong if its from–to roads are not the road this end is on, or if it is many years old.");

        var pending = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Form.Res("ErrorBrush"), Margin = new Thickness(0, 6, 0, 0) };
        Button? useButton = null;
        void Refresh()
        {
            int ticked = _matches.Count(m => m.Found && m.Use);
            if (useButton is not null)
            {
                useButton.Content = $"Use the ticked counts ({ticked} of {_matches.Count} road ends)";
                useButton.IsEnabled = ticked > 0;
            }

            bool differs = inUse && _matches.Any(m =>
            {
                StudyNode? end = s.Study.Node(m.EndId);
                bool fromFdot = StudyEdits.OriginOf(s.Study, $"node:{m.EndId}/volume") == Origins.Fdot;
                return m.Found && (m.Use != fromFdot || (m.Use && end?.Volume != Math.Round(m.Entering)));
            });
            pending.Text = differs ? "Not applied yet: the figures above differ from what the study uses. Press Use the ticked counts to apply them." : "";
            pending.Visibility = differs ? Visibility.Visible : Visibility.Collapsed;
        }

        foreach (CountMatch m in _matches)
        {
            form.Panel.Children.Add(new TextBlock { Text = m.EndName, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 2), TextWrapping = TextWrapping.Wrap });
            if (!m.Found)
            {
                form.Note($"No FDOT count on this road — local and private roads are rarely counted. It keeps its estimate of {Estimate(s, m.EndId):#,0} veh/h coming in.");
                continue;
            }

            foreach ((FdotCount c, bool inbound) in m.Counts)
            {
                form.Note(c.Describe() + (c.OneWay ? (inbound ? " — comes in here." : " — leaves the study here.") : "."));
            }

            FdotCount main = m.Counts.OrderByDescending(c => c.Count.Aadt).First().Count;
            var row = new WrapPanel { Margin = new Thickness(0, 0, 0, 2) };
            var result = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
            var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Form.Res("ErrorBrush"), Visibility = Visibility.Collapsed };
            void Show()
            {
                result.Text = m.Use
                    ? $"→ {m.Entering:#,0} vehicles an hour come in here, {m.Exiting:#,0} go out."
                    : $"Not used: this road end keeps its estimate of {Estimate(s, m.EndId):#,0} veh/h coming in.";
                Refresh();
            }

            void Changed()
            {
                Show();
                Keep(s);
            }

            row.Children.Add(Factor("K", m.K, main.K, v => { m.K = v; Changed(); }, 1, 30, error,
                "Design-hour factor: the share of a day's traffic in the busy hour."));
            if (m.Counts.Any(c => !c.Count.OneWay))
            {
                // a one-way ramp has no busier direction: D does not apply
                row.Children.Add(Factor("D", m.D, main.D, v => { m.D = v; Changed(); }, 50, 100, error,
                    "Directional factor: the share going the busier way in that hour."));
            }

            var useText = new StackPanel { Orientation = Orientation.Horizontal };
            useText.Children.Add(new TextBlock { Text = "Use this count" });
            useText.Children.Add(Form.Chip(required: false));
            var use = new CheckBox { Content = useText, IsChecked = m.Use, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
            System.Windows.Automation.AutomationProperties.SetName(use, "Use this count (optional)");
            use.Checked += (_, _) => { m.Use = true; Changed(); };
            use.Unchecked += (_, _) => { m.Use = false; Changed(); };
            row.Children.Add(use);
            form.Panel.Children.Add(row);
            form.Panel.Children.Add(error);
            form.Panel.Children.Add(result);
            Show();
        }

        useButton = form.Button("Use the ticked counts",
            "Switches the study to typed volumes: each ticked road end gets its vehicles in and out from FDOT; the others keep their estimates. Ctrl+Z undoes it.",
            () =>
            {
                int year = _matches.Where(m => m.Found && m.Use).SelectMany(m => m.Counts).Select(c => c.Count.Year).DefaultIfEmpty(DateTime.Now.Year).Max();
                s.Edit("demand/counts", () => FdotCounts.Apply(s.Study, _matches, year));
                RebuildSoon();
            }, primary: true);
        form.Panel.Children.Add(pending);
        Refresh();

        if (inUse)
        {
            form.Button("Stop using the counts",
                "Goes back to one total shared out by how busy each road end is, keeping today's total. The counts stay listed here to use again.",
                () =>
                {
                    s.Edit("demand/counts", () => FdotCounts.StopUsing(s.Study));
                    RebuildSoon();
                });
        }
    }

    private static double Estimate(StudySession s, string endId) => DemandShares.Entering(s.Study).GetValueOrDefault(endId);

    /// <summary>Keeps FDOT's answer and the student's choices (ticks, K, D) in the study file, so a
    /// reopened study shows what was chosen. It is a change to the file: Save is asked for.</summary>
    private void Keep(StudySession s)
    {
        if (_answer is null || _matches is null)
        {
            return;
        }

        s.Document.Attachments[FdotCache.Entry] = FdotCache.ToBytes(_answer, _savedOn ?? "",
            _matches.Where(m => m.Found).ToDictionary(m => m.EndId, m => (m.Use, m.K, m.D)));
        s.AttachmentChanged();
    }

    private void Match(StudySession s, string json, IReadOnlyDictionary<string, (bool Use, double K, double D)>? choices)
    {
        try
        {
            List<FdotCount> counts = FdotCounts.Parse(json);
            _answer = json;
            _matches = FdotCounts.Match(s.Study, counts);
            foreach (CountMatch m in _matches)
            {
                if (choices is not null && choices.TryGetValue(m.EndId, out (bool Use, double K, double D) c))
                {
                    (m.Use, m.K, m.D) = c;
                }
            }
        }
        catch (TrafficLabPlus.Core.Build.StudyFormatException ex)
        {
            _matches = null;
            _lookupProblem = ex.Message;
        }
    }

    /// <summary>A small percentage box for K or D: commits on Enter or leaving it, says beside it
    /// what is wrong, and shows FDOT's own figure to go back to.</summary>
    private static StackPanel Factor(string name, double value, double fdot, Action<double> set, double min, double max, TextBlock error, string help)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 12, 0), ToolTip = help };
        var box = new TextBox { Width = 56, Text = (value * 100).ToString("0.#", CultureInfo.CurrentCulture), HorizontalContentAlignment = HorizontalAlignment.Right, Padding = new Thickness(2) };
        System.Windows.Automation.AutomationProperties.SetName(box, name + " factor, percent (optional)");
        System.Windows.Automation.AutomationProperties.SetHelpText(box, help);
        p.Children.Add(new TextBlock { Text = name + " ", FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        p.Children.Add(box);
        p.Children.Add(new TextBlock
        {
            Text = $" %  (FDOT's figure: {(fdot * 100).ToString("0.#", CultureInfo.CurrentCulture)}%)",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Form.Res("MutedTextBrush"),
        });
        p.Children.Add(Form.Chip(required: false));
        void Commit()
        {
            if (double.TryParse(box.Text.Replace("%", "", StringComparison.Ordinal), NumberStyles.Float, CultureInfo.CurrentCulture, out double v) && v >= min && v <= max)
            {
                box.ClearValue(Control.BorderBrushProperty);
                error.Visibility = Visibility.Collapsed;
                if (Math.Abs(v / 100 - value) > 1e-9)
                {
                    value = v / 100;
                    set(value);
                }
            }
            else
            {
                box.BorderBrush = Form.Res("ErrorBrush");
                error.Text = $"{name} must be a percentage from {min} to {max}; it is still {(value * 100).ToString("0.#", CultureInfo.CurrentCulture)}%.";
                error.Visibility = Visibility.Visible;
            }
        }

        box.LostKeyboardFocus += (_, _) => Commit();
        box.KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                Commit();
            }
        };
        return p;
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
            form.Text($"Button {i + 1}: name", "What the button says, like \"Today\", \"2035\" or \"Race Week\". Left empty, it says Today.", () => sc.Name, v => sc.Name = v ?? "Today", "demand/scenarios");
            form.Number($"Button {i + 1}: demand", "% of today", "100 is today's traffic; 125 is a quarter more.", () => sc.Mult, v => sc.Mult = v, 10, 300, "demand/scenarios", 0, v => v * 100, v => v / 100, required: false);
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
