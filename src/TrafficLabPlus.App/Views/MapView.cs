using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using TrafficLabPlus.Core.Osm;

namespace TrafficLabPlus.App.Views;

/// <summary>
/// Map: find the place, load its roads from OpenStreetMap, choose up to ten intersections, and make
/// the study from them. The map is a Leaflet page bundled with the program (Map/map.html) shown in
/// WebView2, with OpenStreetMap's tiles; the steps are on the left, in order.
/// </summary>
public sealed class MapView : SectionView
{
    private const string Host = "map.trafficlab.local";

    private readonly Func<OsmCache, Task> _make;
    private readonly WebView2 _web = new();
    private readonly TextBox _search = new() { Padding = new Thickness(3) };
    private readonly ListBox _places = new() { MaxHeight = 150, Visibility = Visibility.Collapsed };
    private readonly Button _load = new() { Content = "Load the roads shown on the map", Padding = new Thickness(10, 4, 10, 4), HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock _loadState = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
    private readonly TextBlock _zoomNote = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0), Visibility = Visibility.Collapsed };
    private readonly TextBlock _limitNote = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0), Visibility = Visibility.Collapsed };
    private readonly TextBox _filter = new() { Padding = new Thickness(3) };
    private readonly ListBox _list = new() { MaxHeight = 220 };
    private readonly TextBlock _chosenText = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
    private readonly StackPanel _chosenPanel = new();
    private readonly Button _makeButton = new() { Content = "Make the study…", Style = (Style)Application.Current.Resources["Primary"], HorizontalAlignment = HorizontalAlignment.Left, IsEnabled = false };
    private readonly TextBlock _problem = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16), Visibility = Visibility.Collapsed };

    private readonly OsmClient _client = new(OsmClient.CreateHttp());
    private OsmCache? _cache;
    private List<OsmJunction> _junctions = [];
    private readonly List<string> _chosen = [];
    private double[]? _view;
    private bool _ready;
    private bool _syncing;
    private object? _shownFor;

    public MapView(Func<OsmCache, Task> make)
    {
        _make = make;
        // long names wrap rather than scroll sideways
        ScrollViewer.SetHorizontalScrollBarVisibility(_places, ScrollBarVisibility.Disabled);
        ScrollViewer.SetHorizontalScrollBarVisibility(_list, ScrollBarVisibility.Disabled);
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(420) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var (scroll, p) = Column();
        p.Children.Add(Title("Map"));
        p.Children.Add(Lead("Find your intersection, bring in its roads from OpenStreetMap, and choose the intersections to study — one, or up to ten along a corridor. TrafficLab+ makes the study from them: roads, lanes, turn lanes, speeds and names."));

        Step(p, "1  Find the place", "A street and a town works best, like \"LPGA Boulevard, Daytona Beach\". (OpenStreetMap does not find a crossing by its two street names: find one of the streets, then pick the crossing in step 3.) Or simply drag and zoom the map.");
        var searchRow = new DockPanel();
        var go = new Button { Content = "Search", Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(6, 0, 0, 0) };
        DockPanel.SetDock(go, Dock.Right);
        searchRow.Children.Add(go);
        searchRow.Children.Add(_search);
        AutomationProperties.SetName(_search, "Place to find");
        p.Children.Add(searchRow);
        p.Children.Add(_places);
        AutomationProperties.SetName(_places, "Places found");
        go.Click += async (_, _) => await Search();
        _search.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                await Search();
            }
        };
        _places.SelectionChanged += (_, _) =>
        {
            if (_places.SelectedItem is ListBoxItem { Tag: OsmPlace place })
            {
                Post(place.Box is { } b ? new { cmd = "goto", box = b } : new { cmd = "goto", lat = place.Lat, lon = place.Lon, zoom = 16 });
                Area = place.Area;
            }
        };

        Step(p, "2  Bring in the roads", $"Zoom the map so the intersections you want fill it (less than {OsmQuery.MaxSideKm:0} km across), then load. TrafficLab+ asks OpenStreetMap once and keeps the answer in your study.");
        p.Children.Add(_load);
        p.Children.Add(_zoomNote);
        p.Children.Add(_loadState);
        ToolTipService.SetShowOnDisabled(_load, true);
        _load.Click += async (_, _) => await LoadRoads();

        Step(p, "3  Choose the intersections", "Click the dots on the map, or tick them in the list. Green dot: a traffic signal on OpenStreetMap. Green ring: a roundabout. Grey dot: no signal on OpenStreetMap. Yellow, numbered: chosen. The dashed box is the area loaded; roads outside it are not in the study. For a corridor, put them in order along it (the arrows beside each chosen one move it).");
        var filterLabel = new Label { Content = "_Find by street name:", Target = _filter, Padding = new Thickness(0, 4, 0, 2) };
        p.Children.Add(filterLabel);
        p.Children.Add(_filter);
        AutomationProperties.SetName(_filter, "Find intersections by street name");
        p.Children.Add(_list);
        AutomationProperties.SetName(_list, "Intersections in the loaded area");
        _filter.TextChanged += (_, _) => FillList();
        p.Children.Add(_chosenText);
        p.Children.Add(_limitNote);
        p.Children.Add(_chosenPanel);

        Step(p, "4  Make the study", "You will give it a title and the budget players get. Lanes, turn lanes, speeds and names come from OpenStreetMap where it has them; anything it lacks starts at a plain value, marked as such in Network.");
        p.Children.Add(_makeButton);
        _makeButton.Click += async (_, _) =>
        {
            if (_cache is not null)
            {
                _cache.Junctions = [.. _chosen];
                await _make(_cache);
            }
        };

        p.Children.Add(new TextBlock
        {
            Text = "Map and roads © OpenStreetMap contributors (openstreetmap.org/copyright). Searches use Nominatim and roads come from Overpass — free services run by volunteers, which TrafficLab+ asks only when you press Search or Load.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = Form.Res("MutedTextBrush"),
            Margin = new Thickness(0, 18, 0, 0),
        });
        root.Children.Add(scroll);

        var mapArea = new Grid();
        mapArea.Children.Add(_web);
        mapArea.Children.Add(_problem);
        Grid.SetColumn(mapArea, 1);
        root.Children.Add(mapArea);
        AutomationProperties.SetName(_web, "Map");
        Content = root;
        Loaded += async (_, _) => await StartMap();
        UpdateChosen();
    }

    /// <summary>The town and state of the last place found, offered as the study's place.</summary>
    public string? Area { get; private set; }

    public IReadOnlyList<OsmJunction> Junctions => _junctions;

    private static void Step(Panel p, string title, string help)
    {
        p.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 16, 0, 2) });
        p.Children.Add(new TextBlock { Text = help, TextWrapping = TextWrapping.Wrap, Foreground = Form.Res("MutedTextBrush"), Margin = new Thickness(0, 0, 0, 6) });
    }

    public override void Rebuild()
    {
        object? now = Session?.Document;
        if (ReferenceEquals(_shownFor, now) || (_shownFor is null && now is not null && !HasSavedRoads(Session!)))
        {
            return;   // nothing changed, or a study without saved roads after roads the student loaded
        }

        // an opened study that came from the map shows its own roads, from the copy saved with it
        if (Session is { } s && OsmCache.FromBytes(s.Document.Attachments.GetValueOrDefault(OsmCache.Entry)) is { Overpass.Length: > 0 } cache)
        {
            _shownFor = s.Document;
            Show(cache, $"These are the roads saved with this study (loaded from OpenStreetMap {cache.Fetched}). Making a study from them makes a new study: the open one is not changed, and changes you made to it in Network, Traffic or Challenge are not carried over.");
            if (cache.Box.Length == 4)
            {
                Post(new { cmd = "goto", box = cache.Box });
            }

            return;
        }

        // the roads shown belonged to another study: they are not this one's
        _shownFor = null;
        _cache = null;
        _junctions = [];
        _chosen.Clear();
        _loadState.Text = "";
        FillList();
        UpdateChosen();
        SendJunctions();
    }

    private static bool HasSavedRoads(StudySession s) => s.Document.Attachments.ContainsKey(OsmCache.Entry);

    private async Task StartMap()
    {
        if (_ready)
        {
            return;
        }

        try
        {
            CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(userDataFolder: PreviewView.DataFolder);
            await _web.EnsureCoreWebView2Async(env);
            // OpenStreetMap's tile policy asks every program to say who it is
            _web.CoreWebView2.Settings.UserAgent = OsmClient.UserAgent;
            _web.CoreWebView2.SetVirtualHostNameToFolderMapping(Host, Path.Combine(AppContext.BaseDirectory, "Map"), CoreWebView2HostResourceAccessKind.DenyCors);
            _web.CoreWebView2.WebMessageReceived += OnMessage;
            _web.CoreWebView2.Navigate($"https://{Host}/map.html");
            _ready = true;
        }
        catch (Exception ex) when (ex is WebView2RuntimeNotFoundException or IOException or UnauthorizedAccessException or InvalidOperationException
                                       or System.Runtime.InteropServices.COMException)
        {
            _problem.Text = "The map cannot be shown on this computer (" + ex.Message + "). Microsoft's WebView2, part of Edge, is needed for it.";
            _problem.Visibility = Visibility.Visible;
            _web.Visibility = Visibility.Collapsed;
        }
    }

    private void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        using JsonDocument doc = JsonDocument.Parse(e.WebMessageAsJson);
        JsonElement m = doc.RootElement;
        switch (m.GetProperty("kind").GetString())
        {
            case "ready":
                if (_cache is not null)
                {
                    SendJunctions();
                    if (_cache.Box.Length == 4)
                    {
                        Post(new { cmd = "goto", box = _cache.Box });
                    }
                }

                break;
            case "view":
                _view = [m.GetProperty("s").GetDouble(), m.GetProperty("w").GetDouble(), m.GetProperty("n").GetDouble(), m.GetProperty("e").GetDouble()];
                double km = Math.Max(Geo.Metres(_view[0], _view[1], _view[2], _view[1]), Geo.Metres(_view[0], _view[1], _view[0], _view[3])) / 1000;
                _load.IsEnabled = km <= OsmQuery.MaxSideKm;
                string zoom = $"Zoom in to load: the map shows {km:0.#} km across, and roads are loaded for less than {OsmQuery.MaxSideKm:0} km.";
                _load.ToolTip = _load.IsEnabled ? null : zoom;
                _zoomNote.Text = zoom;
                _zoomNote.Visibility = _load.IsEnabled ? Visibility.Collapsed : Visibility.Visible;

                break;
            case "toggle":
                Toggle(m.GetProperty("id").GetString()!);
                break;
        }
    }

    private void Post(object message)
    {
        if (_ready && _web.CoreWebView2 is not null)
        {
            _web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message));
        }
    }

    private async Task Search()
    {
        if (string.IsNullOrWhiteSpace(_search.Text))
        {
            return;
        }

        _places.Items.Clear();
        _places.Visibility = Visibility.Visible;
        _places.Items.Add(new ListBoxItem { Content = "Searching OpenStreetMap…", IsEnabled = false });
        try
        {
            List<OsmPlace> found = await _client.SearchAsync(_search.Text);
            _places.Items.Clear();
            foreach (OsmPlace place in found)
            {
                _places.Items.Add(new ListBoxItem { Content = new TextBlock { Text = place.Name, TextWrapping = TextWrapping.Wrap }, Tag = place, ToolTip = "Show it on the map" });
            }

            if (found.Count == 0)
            {
                _places.Items.Add(new ListBoxItem { Content = new TextBlock { Text = "Nothing found. Try a street and a town, like \"Nova Road, Daytona Beach\", or a town alone, then drag the map.", TextWrapping = TextWrapping.Wrap }, IsEnabled = false });
            }
            else
            {
                _places.SelectedIndex = 0;
            }
        }
        catch (OsmServiceException ex)
        {
            _places.Items.Clear();
            _places.Items.Add(new ListBoxItem { Content = new TextBlock { Text = ex.Message, TextWrapping = TextWrapping.Wrap }, IsEnabled = false });
        }
    }

    private async Task LoadRoads()
    {
        if (_view is not { } v)
        {
            return;
        }

        if (_chosen.Count > 0 && MessageBox.Show(Window.GetWindow(this), "Loading the roads here clears the intersections you chose. Load anyway?", "TrafficLab+",
                MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
        {
            return;
        }

        _load.IsEnabled = false;
        _loadState.Text = "Asking OpenStreetMap for the roads… (a few seconds)";
        try
        {
            string json = await _client.RoadsAsync(v[0], v[1], v[2], v[3]);
            Show(new OsmCache { Box = v, Fetched = DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), Overpass = json }, null);
        }
        catch (OsmServiceException ex)
        {
            _loadState.Text = ex.Message;
        }
        finally
        {
            _load.IsEnabled = true;
        }
    }

    private void Show(OsmCache cache, string? note)
    {
        try
        {
            OsmData data = OsmData.Parse(cache.Overpass);
            _cache = cache;
            _junctions = OsmJunctions.Find(data);
            _chosen.Clear();
            _chosen.AddRange(cache.Junctions.Where(id => _junctions.Any(j => j.Id == id)));
            int roads = data.Ways.Count(OsmQuery.IsNetworkRoad);
            _loadState.Text = note ?? $"Loaded {roads} roads and found {_junctions.Count} intersections. Now choose the ones to study (step 3).";
            if (_junctions.Count == 0)
            {
                _loadState.Text = "No intersections in this area: move or zoom out a little and load again.";
            }

            FillList();
            UpdateChosen();
            SendJunctions();
        }
        catch (TrafficLabPlus.Core.Build.StudyFormatException ex)
        {
            _loadState.Text = ex.Message;
        }
    }

    private void SendJunctions() => Post(new
    {
        cmd = "junctions",
        list = _junctions.Select(j => new { id = j.Id, lat = j.Lat, lon = j.Lon, label = j.Label, signal = j.IsSignal, roundabout = j.IsRoundabout }),
        chosen = _chosen,
        box = _cache?.Box,
    });

    private void FillList()
    {
        _list.Items.Clear();
        string f = _filter.Text.Trim();
        foreach (OsmJunction j in _junctions.Where(j => f.Length == 0 || j.Label.Contains(f, StringComparison.OrdinalIgnoreCase)))
        {
            string text = j.Label + Kind(j);
            var box = new CheckBox
            {
                Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
                IsChecked = _chosen.Contains(j.Id),
                Tag = j.Id,
            };
            AutomationProperties.SetName(box, text);
            // Checked/Unchecked rather than Click: a screen reader or UI Automation ticks a box without clicking it
            void Changed(object? sender, RoutedEventArgs e)
            {
                if (!_syncing && box.IsChecked == true != _chosen.Contains(j.Id))
                {
                    Dispatcher.BeginInvoke(() => Toggle(j.Id));
                }
            }

            box.Checked += Changed;
            box.Unchecked += Changed;
            box.GotKeyboardFocus += (_, _) => Post(new { cmd = "focus", id = j.Id });
            _list.Items.Add(box);
        }

        if (_junctions.Count == 0)
        {
            _list.Items.Add(new TextBlock { Text = "Load the roads (step 2) to list the intersections here.", Foreground = Form.Res("MutedTextBrush"), TextWrapping = TextWrapping.Wrap });
        }
    }

    /// <summary>The same words as the map's tooltips.</summary>
    private static string Kind(OsmJunction j) => j.IsRoundabout ? " — roundabout" : j.IsSignal ? " — signal" : " — no signal on OpenStreetMap";

    private void Toggle(string id)
    {
        _limitNote.Visibility = Visibility.Collapsed;
        if (!_chosen.Remove(id))
        {
            if (_chosen.Count >= NetworkFromOsm.MaxJunctions)
            {
                _limitNote.Text = "Ten is the most TrafficLab+ studies at once: that one was not chosen. Remove one to choose another.";
                _limitNote.Visibility = Visibility.Visible;
                SyncTicks();
                return;
            }

            _chosen.Add(id);
        }

        SyncTicks();
        UpdateChosen();
        Post(new { cmd = "chosen", ids = _chosen });
    }

    /// <summary>Ticks the list's boxes to match what is chosen, without rebuilding the list.</summary>
    private void SyncTicks()
    {
        _syncing = true;
        foreach (CheckBox box in _list.Items.OfType<CheckBox>())
        {
            box.IsChecked = _chosen.Contains((string)box.Tag);
        }

        _syncing = false;
    }

    private void Move(int from, int to)
    {
        if (to < 0 || to >= _chosen.Count)
        {
            return;
        }

        (_chosen[from], _chosen[to]) = (_chosen[to], _chosen[from]);
        UpdateChosen();
        Post(new { cmd = "chosen", ids = _chosen });
    }

    private void UpdateChosen()
    {
        _chosenPanel.Children.Clear();
        _chosenText.Text = _chosen.Count == 0
            ? "None chosen yet."
            : $"Chosen: {_chosen.Count} of up to {NetworkFromOsm.MaxJunctions}, numbered as on the map:";
        for (int i = 0; i < _chosen.Count; i++)
        {
            OsmJunction? j = _junctions.FirstOrDefault(x => x.Id == _chosen[i]);
            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            string id = _chosen[i];
            var remove = new Button { Content = "Remove", Padding = new Thickness(6, 0, 6, 0), Margin = new Thickness(4, 0, 0, 0) };
            remove.Click += (_, _) => Toggle(id);
            int at = i;
            var down = new Button { Content = "↓", Padding = new Thickness(6, 0, 6, 0), Margin = new Thickness(4, 0, 0, 0), IsEnabled = i < _chosen.Count - 1, ToolTip = "Move later along the corridor" };
            var up = new Button { Content = "↑", Padding = new Thickness(6, 0, 6, 0), Margin = new Thickness(6, 0, 0, 0), IsEnabled = i > 0, ToolTip = "Move earlier along the corridor" };
            AutomationProperties.SetName(up, $"Move {j?.Label ?? id} earlier");
            AutomationProperties.SetName(down, $"Move {j?.Label ?? id} later");
            up.Click += (_, _) => Move(at, at - 1);
            down.Click += (_, _) => Move(at, at + 1);
            foreach (Button b in new[] { remove, down, up })
            {
                DockPanel.SetDock(b, Dock.Right);
                row.Children.Add(b);
            }

            row.Children.Add(new TextBlock { Text = $"{i + 1}. {j?.Label ?? id}", TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
            _chosenPanel.Children.Add(row);
        }

        _makeButton.IsEnabled = _chosen.Count > 0;
        _makeButton.Content = _chosen.Count switch
        {
            0 => "Make the study…",
            1 => "Make the study from this intersection…",
            _ => $"Make the study from these {_chosen.Count} intersections…",
        };
    }
}
