using System.Globalization;
using System.Text.Json;
using TrafficLabPlus.Core.Build;

namespace TrafficLabPlus.Core.Osm;

public sealed record OsmNode(long Id, double Lat, double Lon, IReadOnlyDictionary<string, string> Tags)
{
    public string? Tag(string key) => Tags.GetValueOrDefault(key);
}

public sealed record OsmWay(long Id, IReadOnlyList<long> Nodes, IReadOnlyDictionary<string, string> Tags)
{
    public string? Tag(string key) => Tags.GetValueOrDefault(key);

    public string Highway => Tags.GetValueOrDefault("highway") ?? "";

    public bool IsRoundabout => Tag("junction") is "roundabout" or "circular";

    /// <summary>1 one way along the node order, −1 against it, 0 both ways. Roundabouts and motorways
    /// are one way unless tagged otherwise (OSM's rules).</summary>
    public int OneWay => Tag("oneway") switch
    {
        "yes" or "true" or "1" => 1,
        "-1" or "reverse" => -1,
        "no" or "false" or "0" => 0,
        _ => IsRoundabout || Highway is "motorway" or "motorway_link" ? 1 : 0,
    };

    /// <summary>What a person calls the road: its name, else its ref, else what kind of road it is.</summary>
    public string Label => Tag("name") ?? (Tag("ref") is { } r ? r.Replace(" ", "-", StringComparison.Ordinal) : null)
                           ?? (Highway is "motorway_link" or "trunk_link" ? "Ramp" : Highway.EndsWith("_link", StringComparison.Ordinal) ? "Slip lane" : "Unnamed road");
}

/// <summary>
/// What Overpass returned for a box: the roads (ways) and the points on them (nodes). Read from
/// Overpass's JSON (<c>[out:json]</c> with <c>out body</c>).
/// </summary>
public sealed class OsmData
{
    public Dictionary<long, OsmNode> Nodes { get; } = [];
    public List<OsmWay> Ways { get; } = [];

    /// <exception cref="StudyFormatException">The text is not an Overpass answer.</exception>
    public static OsmData Parse(string json)
    {
        var data = new OsmData();
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("elements", out JsonElement elements))
            {
                throw new StudyFormatException("OpenStreetMap's answer had no roads in it.");
            }

            foreach (JsonElement e in elements.EnumerateArray())
            {
                string type = e.GetProperty("type").GetString() ?? "";
                long id = e.GetProperty("id").GetInt64();
                var tags = new Dictionary<string, string>(StringComparer.Ordinal);
                if (e.TryGetProperty("tags", out JsonElement t))
                {
                    foreach (JsonProperty p in t.EnumerateObject())
                    {
                        tags[p.Name] = p.Value.GetString() ?? "";
                    }
                }

                if (type == "node")
                {
                    data.Nodes[id] = new OsmNode(id, e.GetProperty("lat").GetDouble(), e.GetProperty("lon").GetDouble(), tags);
                }
                else if (type == "way" && e.TryGetProperty("nodes", out JsonElement refs))
                {
                    data.Ways.Add(new OsmWay(id, refs.EnumerateArray().Select(r => r.GetInt64()).ToList(), tags));
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new StudyFormatException("OpenStreetMap's answer could not be read (" + ex.Message + ").", ex);
        }

        return data;
    }
}

/// <summary>The questions TrafficLab+ asks OpenStreetMap, and how it reads the tags it gets back.</summary>
public static class OsmQuery
{
    /// <summary>Roads cars use. Service roads (parking aisles, drives) are fetched for the backdrop
    /// but never become part of the simulated network.</summary>
    public const string DrivableClasses = "motorway|trunk|primary|secondary|tertiary|unclassified|residential|living_street|motorway_link|trunk_link|primary_link|secondary_link|tertiary_link";

    /// <summary>The biggest box TrafficLab+ asks for, km on a side — about a ten-signal corridor, and
    /// small enough to be a light request on a shared, free service.</summary>
    public const double MaxSideKm = 4;

    /// <summary>One Overpass query for every road in a box (south, west, north, east), with its points.</summary>
    public static string Roads(double south, double west, double north, double east) =>
        string.Create(CultureInfo.InvariantCulture,
            $"[out:json][timeout:25];way[\"highway\"~\"^({DrivableClasses}|service)$\"]({south:0.######},{west:0.######},{north:0.######},{east:0.######});(._;>;);out body;");

    public static bool IsDrivable(OsmWay w) => DrivableClasses.Split('|').Contains(w.Highway);

    /// <summary>A slip lane: the short link road that lets right turns bypass an ordinary intersection
    /// (primary_link, secondary_link, tertiary_link). Freeway ramps are motorway or trunk links.</summary>
    public static bool IsSlipLane(OsmWay w) => w.Highway is "primary_link" or "secondary_link" or "tertiary_link";

    /// <summary>The roads that can become part of the simulated network: drivable, and not slip lanes,
    /// which act as right-turn lanes of the intersection they bypass.</summary>
    public static bool IsNetworkRoad(OsmWay w) => IsDrivable(w) && !IsSlipLane(w);

    /// <summary>How important a road is, for choosing which roads to keep and how busy its end is.</summary>
    public static int Rank(string highway) => highway switch
    {
        "motorway" or "trunk" => 7,
        "primary" or "motorway_link" or "trunk_link" => 6,
        "secondary" or "primary_link" => 5,
        "tertiary" or "secondary_link" => 4,
        "tertiary_link" or "unclassified" => 3,
        "residential" or "living_street" => 2,
        _ => 1,
    };

    /// <summary>A road end's starting weight (how busy), by the kind of road.</summary>
    public static double Weight(string highway) => Rank(highway) switch
    {
        >= 6 => 900,
        5 => 700,
        4 => 450,
        3 => 300,
        _ => 200,
    };

    /// <summary>The posted speed in mph from a <c>maxspeed</c> tag ("45 mph", "70" in km/h), or null.</summary>
    public static double? MaxSpeedMph(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        string t = tag.Trim().ToLowerInvariant();
        bool mph = t.EndsWith("mph", StringComparison.Ordinal);
        string number = new(t.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
        if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) || v <= 0)
        {
            return null;
        }

        return mph ? v : Math.Round(v / 1.609344);
    }

    /// <summary>The usual speed for a kind of road where OSM gives none, mph.</summary>
    public static double DefaultSpeedMph(string highway) => highway switch
    {
        "motorway" or "trunk" => 55,
        "primary" => 45,
        "secondary" => 40,
        "tertiary" => 35,
        "motorway_link" or "trunk_link" or "primary_link" or "secondary_link" or "tertiary_link" => 35,
        "unclassified" => 30,
        _ => 25,
    };

    /// <summary>Lanes on one side, read from <c>turn:lanes</c> ("left|none|none|right"): through lanes
    /// (any lane that goes straight on, or has no marking) and whether a left-only and a right-only
    /// lane exist. Null when the tag is missing.</summary>
    public static (int Through, bool Left, bool Right)? TurnLanes(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        int through = 0;
        bool left = false, right = false;
        foreach (string lane in tag.Split('|'))
        {
            string[] moves = lane.Split(';').Select(m => m.Trim()).ToArray();
            // a lane that only turns is a turn lane; one that also goes on, or merges, or is unmarked is a through lane
            bool turnsOnly = moves.All(m => m is "left" or "slight_left" or "sharp_left" or "reverse"
                                           or "right" or "slight_right" or "sharp_right");
            if (turnsOnly && moves.Any(m => m.Contains("left", StringComparison.Ordinal) || m == "reverse"))
            {
                left = true;
            }
            else if (turnsOnly)
            {
                right = true;
            }
            else
            {
                through++;
            }
        }

        return (through, left, right);
    }
}
