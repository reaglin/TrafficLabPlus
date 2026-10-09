using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace TrafficLabPlus.Core.Model;

/// <summary>
/// A study: everything one TrafficLab+ page needs (format 1, <c>docs/STUDY-FORMAT.md</c>). Metres,
/// metres per second, millions of dollars and vehicles per hour, as the page reads it.
///
/// Every class keeps the fields it does not know in <c>Extra</c>, so a study read and written
/// again loses nothing — a field added by hand, or by a later program, comes back as it was.
/// </summary>
public sealed class Study
{
    /// <summary>The study format this program writes and the newest it reads.</summary>
    public const int CurrentFormat = 1;

    public int Format { get; set; } = CurrentFormat;
    public string Title { get; set; } = "";
    public string? Short { get; set; }
    public string? Subtitle { get; set; }
    public string? Place { get; set; }
    public string? Author { get; set; }
    public string? Course { get; set; }
    public string? Intro { get; set; }
    public WorldBox? World { get; set; }
    public double Budget { get; set; }
    public Costs? Costs { get; set; }
    public Demand Demand { get; set; } = new();
    public List<StudyNode> Nodes { get; set; } = [];
    public List<StudyLink> Links { get; set; } = [];

    /// <summary>Turn lanes that exist today, keyed <c>link:ab</c> or <c>link:ba</c> — the approach
    /// arriving at the far node.</summary>
    public Dictionary<string, Pocket>? Pockets { get; set; }

    /// <summary>Decoration drawn under the roads (streets, blocks, places, freeways). The program
    /// keeps it as it came; it is never simulated.</summary>
    public JsonObject? Backdrop { get; set; }

    public Sources? Sources { get; set; }

    /// <summary>Where each value came from, for the editor: a key such as <c>node:I1/cycle</c> or
    /// <c>link:L1/lanes</c>, and one of the <see cref="Origins"/> words. The page ignores it.</summary>
    public Dictionary<string, string>? From { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    public StudyNode? Node(string id) => Nodes.Find(n => n.Id == id);

    public StudyLink? Link(string id) => Links.Find(l => l.Id == id);

    /// <summary>The roads that meet at a node.</summary>
    public IEnumerable<StudyLink> LinksAt(string nodeId) => Links.Where(l => l.A == nodeId || l.B == nodeId);
}

/// <summary>The words <see cref="Study.From"/> uses for where a value came from.</summary>
public static class Origins
{
    public const string Example = "example";
    public const string Default = "default";
    public const string Typed = "typed";
    public const string Osm = "osm";
    public const string Fdot = "fdot";
    public const string Ai = "ai";

    /// <summary>The words a person reads beside a value.</summary>
    public static string Describe(string? origin) => origin switch
    {
        Example => "from the built-in example",
        Default => "a starting value — change it to match the real road",
        Typed => "typed by you",
        Osm => "from OpenStreetMap",
        Fdot => "from FDOT traffic counts",
        Ai => "suggested by the AI",
        _ => "",
    };
}

public sealed class WorldBox
{
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double H { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>Prices of the fixes, $M. A missing one takes LPGA's (<see cref="Lpga"/>).</summary>
public sealed class Costs
{
    public double? AddLaneBase { get; set; }
    public double? AddLanePerKm { get; set; }
    public double? OneWay { get; set; }
    public double? PocketL { get; set; }
    public double? PocketR { get; set; }
    public double? Retime { get; set; }
    public double? ProtL { get; set; }
    public double? Roundabout { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    /// <summary>LPGA's prices — the engine's own defaults.</summary>
    public static Costs Lpga() => new()
    {
        AddLaneBase = 0.6,
        AddLanePerKm = 0.004 / 0.6 * 1000,
        OneWay = 0.15,
        PocketL = 0.45,
        PocketR = 0.35,
        Retime = 0.02,
        ProtL = 0.08,
        Roundabout = 2.8,
    };
}

public sealed class Demand
{
    public const string Gravity = "gravity";
    public const string Volumes = "volumes";

    public string? Mode { get; set; }
    public double? BaseTotal { get; set; }
    public List<OdRow>? Od { get; set; }
    public List<List<string>>? NoTrips { get; set; }
    public string? NoTripsWhy { get; set; }
    public List<Scenario>? Scenarios { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    [JsonIgnore]
    public bool IsVolumes => Mode == Volumes;
}

public sealed class OdRow
{
    public string O { get; set; } = "";
    public string D { get; set; } = "";
    public double V { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class Scenario
{
    public string Name { get; set; } = "";
    public double Mult { get; set; } = 1;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class StudyNode
{
    public const string End = "end";
    public const string SignalType = "signal";
    public const string Roundabout = "roundabout";

    public string Id { get; set; } = "";
    public string Type { get; set; } = End;
    public string? Name { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public string? Short { get; set; }
    public double? Weight { get; set; }
    public double? Volume { get; set; }
    public Signal? Signal { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    [JsonIgnore]
    public string Label => string.IsNullOrWhiteSpace(Name) ? Id : Name;
}

public sealed class Signal
{
    public double Cycle { get; set; } = 90;
    public double Split { get; set; } = 0.6;
    public List<string>? MainLinks { get; set; }
    public bool ProtMain { get; set; }
    public bool ProtSide { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class StudyLink
{
    public string Id { get; set; } = "";
    public string A { get; set; } = "";
    public string B { get; set; } = "";
    public string? Name { get; set; }
    public int Lanes { get; set; } = 1;
    public double Speed { get; set; } = 13.4;
    public bool? Label { get; set; }
    public double? LabelAt { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    [JsonIgnore]
    public string Title => string.IsNullOrWhiteSpace(Name) ? Id : Name;
}

/// <summary>Turn lanes on one approach: <c>L</c> and <c>R</c> are 1 when the lane exists.</summary>
public sealed class Pocket
{
    [JsonPropertyName("L")]
    public int? L { get; set; }

    [JsonPropertyName("R")]
    public int? R { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    [JsonIgnore]
    public bool IsEmpty => (L ?? 0) == 0 && (R ?? 0) == 0 && (Extra is null || Extra.Count == 0);
}

public sealed class Sources
{
    /// <summary>The roads came from OpenStreetMap: the page and printout credit its contributors.</summary>
    public bool? Osm { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}
