using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using TrafficLabPlus.Core.Model;

namespace TrafficLabPlus.App.Views;

/// <summary>
/// Network: the drawing of the roads and intersections, with the form for whatever is chosen on it
/// underneath. Everything that can be clicked on the drawing can also be chosen from the list, for
/// a keyboard.
/// </summary>
public sealed class NetworkView : SectionView
{
    private readonly NetworkCanvas _canvas = new();
    private readonly ComboBox _chooser = new() { MinWidth = 300, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0), MinHeight = 18 };
    private readonly TextBlock _modeHelp = new() { TextWrapping = TextWrapping.Wrap, Foreground = Form.Res("MutedTextBrush") };
    private readonly StackPanel _details = new() { Margin = new Thickness(18, 10, 18, 24) };
    private readonly Dictionary<CanvasMode, ToggleButton> _modes = [];
    private bool _filling;

    public NetworkView()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 160 });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.9, GridUnitType.Star), MinHeight = 160 });

        var top = new StackPanel { Margin = new Thickness(18, 14, 18, 8) };
        top.Children.Add(Title("Network"));
        top.Children.Add(Lead("The roads and intersections the page simulates. Click one on the drawing (or choose it from the list below) to change it; drag an intersection or a road end to move it. The page on the right is rebuilt after every change. Ctrl+Z undoes."));
        var tools = new WrapPanel();
        AddMode(tools, CanvasMode.Select, "Choose and move", "Click something to change it; drag an intersection or road end to move it.");
        AddMode(tools, CanvasMode.AddSignal, "Add intersection", "Click on the drawing where the new intersection goes. It starts as a traffic signal; it needs 3 or 4 roads.");
        AddMode(tools, CanvasMode.AddEnd, "Add road end", "Click where traffic should come in and leave — the edge of the study, where a road leaves it.");
        AddMode(tools, CanvasMode.AddRoad, "Add road", "Click one intersection or road end, then another, to join them with a road.");
        var remove = new Button { Content = "Remove", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 0, 6, 6), ToolTip = "Remove what is chosen (Delete key). Ctrl+Z puts it back." };
        remove.Click += (_, _) => RemoveSelected();
        tools.Children.Add(remove);
        top.Children.Add(tools);
        top.Children.Add(_modeHelp);
        top.Children.Add(_status);
        Grid.SetRow(top, 0);
        root.Children.Add(top);

        var frame = new Border { BorderBrush = Form.Res("LineBrush"), BorderThickness = new Thickness(1), Margin = new Thickness(18, 0, 18, 0), Child = _canvas };
        AutomationProperties.SetName(_canvas, "Drawing of the network");
        Grid.SetRow(frame, 1);
        root.Children.Add(frame);

        var splitter = new GridSplitter { Height = 6, HorizontalAlignment = HorizontalAlignment.Stretch, Background = Form.Res("PageBrush"), ToolTip = "Drag to give the drawing or the form more room" };
        Grid.SetRow(splitter, 2);
        root.Children.Add(splitter);

        var lower = new DockPanel();
        var chooseRow = new StackPanel { Margin = new Thickness(18, 6, 18, 0) };
        var chooseLabel = new Label { Content = "_Change:", Padding = new Thickness(0, 0, 0, 2), FontWeight = FontWeights.SemiBold, Target = _chooser };
        chooseRow.Children.Add(chooseLabel);
        chooseRow.Children.Add(_chooser);
        AutomationProperties.SetName(_chooser, "Choose an intersection, road end or road to change");
        DockPanel.SetDock(chooseRow, Dock.Top);
        lower.Children.Add(chooseRow);
        lower.Children.Add(new ScrollViewer { Content = _details, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        Grid.SetRow(lower, 3);
        root.Children.Add(lower);
        Content = root;

        _canvas.SelectionChanged += (_, _) => { SyncChooser(); ShowDetails(); };
        _canvas.ModeChanged += (_, _) => ShowMode();
        _canvas.Message += (_, text) => _status.Text = text;
        _chooser.SelectionChanged += (_, _) =>
        {
            if (!_filling && _chooser.SelectedItem is ComboBoxItem { Tag: string what })
            {
                _canvas.Select(what.Length == 0 ? null : what);
            }
        };
        PreviewKeyDown += (_, e) =>
        {
            // Delete removes only from the drawing; in the form it would remove the thing being edited
            if (e.Key == Key.Delete && Keyboard.FocusedElement == _canvas)
            {
                RemoveSelected();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && _canvas.Mode != CanvasMode.Select)
            {
                _canvas.SetMode(CanvasMode.Select);
                _status.Text = "Stopped adding.";
                e.Handled = true;
            }
        };
        ShowMode();
    }

    private void AddMode(Panel tools, CanvasMode mode, string text, string help)
    {
        var button = new ToggleButton { Content = text, Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 0, 6, 6), ToolTip = help, Tag = help };
        // a second click on the mode in use goes back to choosing and moving
        button.Click += (_, _) => _canvas.SetMode(_canvas.Mode == mode && mode != CanvasMode.Select ? CanvasMode.Select : mode);
        _modes[mode] = button;
        tools.Children.Add(button);
    }

    private void ShowMode()
    {
        foreach ((CanvasMode mode, ToggleButton button) in _modes)
        {
            button.IsChecked = mode == _canvas.Mode;
        }

        _modeHelp.Text = (string)_modes[_canvas.Mode].Tag + (_canvas.Mode == CanvasMode.Select ? "" : " Press Esc to stop.");
    }

    /// <summary>Chooses a node or road, as a click on the drawing would (<c>node:I1</c>, <c>link:L1</c>).</summary>
    public void Choose(string what) => _canvas.Select(what);

    public override void Rebuild()
    {
        _canvas.Session = Session;
        if (Session is not null && _canvas.Selected is { } sel && !Exists(sel))
        {
            _canvas.Select(null);
        }

        FillChooser();
        ShowDetails();
        _canvas.Refit();
    }

    protected override void StudyChanged()
    {
        _canvas.InvalidateVisual();
        _canvas.Refit();
    }

    private bool Exists(string what) =>
        what.StartsWith("node:", StringComparison.Ordinal) ? Session!.Study.Node(what[5..]) is not null : Session!.Study.Link(what[5..]) is not null;

    private void FillChooser()
    {
        _filling = true;
        _chooser.Items.Clear();
        _chooser.Items.Add(new ComboBoxItem { Content = "(nothing chosen)", Tag = "" });
        if (Session is { } s)
        {
            foreach (StudyNode n in s.Study.Nodes.Where(n => n.Type != StudyNode.End))
            {
                _chooser.Items.Add(new ComboBoxItem { Content = "Intersection: " + n.Label, Tag = "node:" + n.Id });
            }

            foreach (StudyNode n in s.Study.Nodes.Where(n => n.Type == StudyNode.End))
            {
                _chooser.Items.Add(new ComboBoxItem { Content = "Road end: " + n.Label, Tag = "node:" + n.Id });
            }

            foreach (StudyLink l in s.Study.Links)
            {
                _chooser.Items.Add(new ComboBoxItem { Content = $"Road: {l.Title} ({s.Study.Node(l.A)?.Label} – {s.Study.Node(l.B)?.Label})", Tag = "link:" + l.Id });
            }
        }

        _filling = false;
        SyncChooser();
    }

    private void SyncChooser()
    {
        _filling = true;
        string tag = _canvas.Selected ?? "";
        ComboBoxItem? item = _chooser.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == tag);
        if (item is null)
        {
            FillChooserLater();
        }

        _chooser.SelectedItem = item;
        _filling = false;
    }

    private void FillChooserLater() => Dispatcher.BeginInvoke(FillChooser, System.Windows.Threading.DispatcherPriority.Background);

    private void RemoveSelected()
    {
        if (Session is not { } s || _canvas.Selected is not { } sel)
        {
            _status.Text = "Choose something on the drawing first, then Remove.";
            return;
        }

        string id = sel[5..];
        if (sel.StartsWith("node:", StringComparison.Ordinal))
        {
            string name = s.Study.Node(id)?.Label ?? id;
            int roads = s.Study.LinksAt(id).Count();
            s.Edit(null, () => StudyEdits.RemoveNode(s.Study, id));
            _status.Text = $"Removed \"{name}\"" + (roads > 0 ? $" and its {roads} road{(roads == 1 ? "" : "s")}" : "") + ". Ctrl+Z puts it back.";
        }
        else
        {
            string name = s.Study.Link(id)?.Title ?? id;
            s.Edit(null, () => StudyEdits.RemoveLink(s.Study, id));
            _status.Text = $"Removed the road \"{name}\". Ctrl+Z puts it back.";
        }

        _canvas.Select(null);
        FillChooser();
    }

    // ---------------------------------------------------------------- the form

    private void ShowDetails()
    {
        _details.Children.Clear();
        if (Session is not { } s)
        {
            return;
        }

        var form = new Form(_details, s);
        string? sel = _canvas.Selected;
        if (sel is not null && sel.StartsWith("node:", StringComparison.Ordinal) && s.Study.Node(sel[5..]) is { } n)
        {
            NodeForm(form, s, n);
        }
        else if (sel is not null && s.Study.Link(sel[5..]) is { } l)
        {
            LinkForm(form, s, l);
        }
        else
        {
            int signals = s.Study.Nodes.Count(x => x.Type == StudyNode.SignalType), rabs = s.Study.Nodes.Count(x => x.Type == StudyNode.Roundabout);
            int ends = s.Study.Nodes.Count(x => x.Type == StudyNode.End);
            form.Heading("Nothing chosen");
            form.Note($"This network has {signals} signal{(signals == 1 ? "" : "s")}, {rabs} roundabout{(rabs == 1 ? "" : "s")}, {ends} road ends and {s.Study.Links.Count} roads.");
            form.Note("Click an intersection, a road end or a road on the drawing — or choose one from the list above — to see and change its settings: names, lanes, speeds, signal timing, and the turn lanes that exist today.");
            form.Note("Green dots are traffic signals, green rings are roundabouts, black squares are road ends (where traffic comes in and goes out). Roads are drawn wider when they have more lanes.");
        }
    }

    private void NodeForm(Form form, StudySession s, StudyNode n)
    {
        string k = "node:" + n.Id + "/";
        bool end = n.Type == StudyNode.End;
        form.Heading(n.Label, end ? "A road end: where traffic comes into the study and leaves it." : "An intersection.");
        form.Choice("Kind", "What is here today. A plan on the page can turn a signal into a roundabout; it cannot turn a roundabout back.",
            [(StudyNode.SignalType, "Traffic signal"), (StudyNode.Roundabout, "Roundabout (one that exists today)"), (StudyNode.End, "Road end (traffic enters and leaves)")],
            () => n.Type, v => { StudyEdits.SetType(s.Study, n, v); RebuildSoon(); }, k + "type");
        form.Text("Name", "What people read on the page and on the printout, like \"LPGA & Williamson\".", () => n.Name, v => { n.Name = v; FillChooserLater(); }, k + "name");
        if (!end)
        {
            form.Text("Short name", "A shorter name for the page's tables, like \"Williamson\". Leave it empty to use the name.", () => n.Short, v => n.Short = v, k + "short");
        }

        if (end)
        {
            if (s.Study.Demand.IsVolumes)
            {
                form.Number("Vehicles entering here", "veh/h", "How many vehicles an hour come into the study at this end, at today's demand (0 if none).",
                    () => n.Volume, v => n.Volume = v, 0, 20000, k + "volume");
            }

            form.Number(s.Study.Demand.IsVolumes ? "How busy as a destination" : "How busy", "", s.Study.Demand.IsVolumes
                    ? "Compared with the other road ends: trips leaving the study are shared out by these, so an end with 900 receives about twice the trips of one with 450."
                    : "Compared with the other road ends: an end with 900 sends and receives about twice the trips of one with 450. Change these in the Traffic section to see them side by side.",
                () => n.Weight, v => n.Weight = v, 0, 100000, k + "weight");
        }

        if (n.Type == StudyNode.SignalType && n.Signal is { } sig)
        {
            form.Heading("Signal timing today", "The timing the page starts with. Players can retime it as part of their plan.");
            form.Number("Cycle length", "seconds", "The time for the lights to go all the way round once (40–180 s). Longer cycles move more cars but make each driver wait longer.",
                () => sig.Cycle, v => sig.Cycle = v, 40, 180, k + "cycle");
            form.Number("Main street's share of green", "%", "Of the green time, how much goes to the main street (20–80%). The side street gets the rest.",
                () => sig.Split, v => sig.Split = v, 20, 80, k + "split", 0, v => v * 100, v => v / 100);
            List<StudyLink> legs = s.Study.LinksAt(n.Id).ToList();
            form.Heading("Which roads are the main street", "The main street gets the longer share of green. If none are ticked, TrafficLab+ takes the two roads that are most opposite each other, widest first.");
            foreach (StudyLink leg in legs)
            {
                string other = s.Study.Node(leg.A == n.Id ? leg.B : leg.A)?.Label ?? "";
                form.Check($"{leg.Title} (toward {other})", "", () => sig.MainLinks?.Contains(leg.Id) == true, on =>
                {
                    sig.MainLinks ??= [];
                    if (on)
                    {
                        sig.MainLinks.Add(leg.Id);
                    }
                    else
                    {
                        sig.MainLinks.Remove(leg.Id);
                    }

                    if (sig.MainLinks.Count == 0)
                    {
                        sig.MainLinks = null;
                    }
                }, k + "mainLinks");
            }

            form.Check("Protected left turns on the main street today", "A green arrow phase for left turns from the main street. Without one, left turns wait for a gap in oncoming traffic.",
                () => sig.ProtMain, v => sig.ProtMain = v, k + "protMain");
            form.Check("Protected left turns on the side street today", "The same, for the side street.", () => sig.ProtSide, v => sig.ProtSide = v, k + "protSide");
        }

        form.Heading("Where it is", "Drag it on the drawing, or type the distances. Positions are measured from a corner of the drawing.");
        form.Number("East", "ft", "Distance to the east.", () => n.X, v => n.X = v, -1_000_000, 1_000_000, k + "position", 0, Units.Feet, Units.Metres);
        form.Number("South", "ft", "Distance to the south.", () => n.Y, v => n.Y = v, -1_000_000, 1_000_000, k + "position", 0, Units.Feet, Units.Metres);

        List<StudyLink> roads = s.Study.LinksAt(n.Id).ToList();
        form.Heading("Roads here", roads.Count == 0 ? "None yet. Use Add road above to join it to the network." : null);
        foreach (StudyLink road in roads)
        {
            string other = s.Study.Node(road.A == n.Id ? road.B : road.A)?.Label ?? "";
            var go = new Button { Content = $"{road.Title} — toward {other}", HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 2, 0, 2) };
            go.Click += (_, _) => _canvas.Select("link:" + road.Id);
            form.Panel.Children.Add(go);
        }

        form.Button(end ? "Remove this road end" : "Remove this intersection", "Its roads are removed with it. Ctrl+Z puts everything back.", RemoveSelected);
    }

    private void LinkForm(Form form, StudySession s, StudyLink l)
    {
        string k = "link:" + l.Id + "/";
        StudyNode? a = s.Study.Node(l.A), b = s.Study.Node(l.B);
        double metres = a is null || b is null ? 0 : Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
        form.Heading(l.Title, $"A road between {a?.Label} and {b?.Label}: {Length(metres)} long (move its ends to change that).");
        int sameStreet = s.Study.Links.Count(x => x.Name == l.Name && !string.IsNullOrWhiteSpace(l.Name));
        form.Text("Street name", "As people know it, like \"LPGA Blvd\". Renaming it renames the street everywhere: "
                  + (sameStreet > 1 ? $"all {sameStreet} roads with this name, " : "")
                  + "and the intersections and road ends named after it.", () => l.Name, v =>
        {
            string? old = l.Name;
            l.Name = v;
            if (old is not null && v is not null)
            {
                (int roads, int names) = StudyEdits.RenameStreet(s.Study, old, v);
                int others = roads + names;
                _status.Text = others == 0 ? $"Renamed the road to {v}."
                    : $"Renamed {old} to {v} everywhere: {roads + 1} road{(roads == 0 ? "" : "s")} and {names} intersection or road-end name{(names == 1 ? "" : "s")}. Ctrl+Z undoes it all.";
            }

            FillChooserLater();
            _canvas.InvalidateVisual();
        }, k + "name");
        form.Choice("Through lanes in each direction", "Lanes that carry traffic straight on, counted one way. Turn lanes are separate (below). A plan can add a lane each way.",
            [("1", "1 lane each way"), ("2", "2 lanes each way"), ("3", "3 lanes each way")],
            () => l.Lanes.ToString(CultureInfo.InvariantCulture), v => l.Lanes = int.Parse(v, CultureInfo.InvariantCulture), k + "lanes");
        form.Number("Speed limit", "mph", "The posted speed. Cars drive at about this speed when the road is clear.",
            () => l.Speed, v => l.Speed = v, 10, 75, k + "speed", 0, Units.Mph, Units.Mps);
        form.Check("Show the street name on the page's map", "Turn off for short pieces where the name would crowd the map.", () => l.Label != false, v => l.Label = v ? null : false, k + "label");

        bool any = false;
        foreach ((string dir, StudyNode? from, StudyNode? to) in new[] { ("ab", a, b), ("ba", b, a) })
        {
            if (to?.Type != StudyNode.SignalType)
            {
                continue;
            }

            if (!any)
            {
                form.Heading("Turn lanes that exist today", "Short extra lanes just before a signal, so turning cars wait out of the way of through traffic. These are free and part of today's network; players can buy more in their plan.");
                any = true;
            }

            string key = k + "pockets:" + dir;
            form.Check($"Left-turn lane arriving at {to.Label} (coming from {from?.Label})", "",
                () => StudyEdits.GetPocket(s.Study, l.Id, dir).Left, v => StudyEdits.SetPocket(s.Study, l.Id, dir, v, StudyEdits.GetPocket(s.Study, l.Id, dir).Right), key);
            form.Check($"Right-turn lane arriving at {to.Label} (coming from {from?.Label})", "",
                () => StudyEdits.GetPocket(s.Study, l.Id, dir).Right, v => StudyEdits.SetPocket(s.Study, l.Id, dir, StudyEdits.GetPocket(s.Study, l.Id, dir).Left, v), key);
        }

        form.Button("Remove this road", "Ctrl+Z puts it back.", RemoveSelected);
    }

    private static string Length(double metres)
    {
        double feet = Units.Feet(metres);
        return feet >= 2640 ? (feet / 5280).ToString("0.00", CultureInfo.CurrentCulture) + " mi" : Math.Round(feet).ToString("#,0", CultureInfo.CurrentCulture) + " ft";
    }
}
