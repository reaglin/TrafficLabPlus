using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using TrafficLabPlus.Core.Model;
using TrafficLabPlus.Core.Osm;

namespace TrafficLabPlus.Tests;

/// <summary>OpenStreetMap into a study, on a saved Overpass answer for the LPGA corridor
/// (tests/fixtures/osm-lpga.json, fetched 2026-10-09) and on small made-up maps. No network.</summary>
public class OsmTests
{
    private static OsmData Lpga() => OsmData.Parse(File.ReadAllText(Path.Combine(Tools.RepoRoot, "tests", "fixtures", "osm-lpga.json")));

    // the five signals of Ron's LPGA Traffic Lab, as OSM names them
    private const string RampsEast = "j101035693", Outlet = "j3269748465", Williamson = "j4750842748", Cornerstone = "j3269758871", OutletSouth = "j3269758870";

    private static string TempFile(string name)
    {
        string dir = Path.Combine(Path.GetTempPath(), "TrafficLabPlusTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, name);
    }

    // ------------------------------------------------------------ the LPGA corridor

    [Fact]
    public void ADividedRoadsCrossingIsOneJunctionWithItsSignal()
    {
        OsmJunction j = OsmJunctions.Find(Lpga()).Single(j => j.Id == Williamson);

        Assert.Equal("LPGA Boulevard & Williamson Boulevard", j.Label);
        Assert.Equal(4, j.NodeIds.Count);   // two carriageways each way: four crossing points
        Assert.True(j.IsSignal);
    }

    [Fact]
    public void SlipLanesAreNotRoads()
    {
        Assert.DoesNotContain(OsmJunctions.Find(Lpga()), j => j.Roads.Contains("Slip lane"));
    }

    [Fact]
    public void TheLpgaCorridorFromOsmIsASoundStudyThatRuns()
    {
        OsmStudy made = NetworkFromOsm.Build(Lpga(), new OsmStudyRequest
        {
            Junctions = [RampsEast, Outlet, Williamson, Cornerstone, OutletSouth],
            Title = "LPGA from OSM",
            Budget = 5,
        });
        Study s = made.Study;

        Assert.Empty(StudyValidator.Check(s));
        Assert.Equal(5, s.Nodes.Count(n => n.Type == StudyNode.SignalType));
        Assert.All(s.Nodes.Where(n => n.Type != StudyNode.End), n => Assert.InRange(s.LinksAt(n.Id).Count(), 3, 4));
        Assert.True(s.Sources!.Osm);
        Assert.Equal("LPGA Boulevard & Williamson Boulevard", s.Node("I3")!.Name);
        Assert.Equal("Williamson Boulevard", s.Node("I3")!.Short);   // LPGA is the corridor

        // LPGA Blvd between Outlet and Williamson: three through lanes and turn lanes, all from OSM's tags
        StudyLink lpga = s.Links.Single(l => (l.A, l.B) == ("I2", "I3"));
        Assert.Equal(3, lpga.Lanes);
        Assert.Equal(Origins.Osm, StudyEdits.OriginOf(s, $"link:{lpga.Id}/lanes"));
        Assert.True(StudyEdits.GetPocket(s, lpga.Id, "ab").Left);
        Assert.True(StudyEdits.GetPocket(s, lpga.Id, "ba").Left);

        // honest about what it could not know
        Assert.Contains(made.Notes, n => n.Contains("no traffic signal", StringComparison.Ordinal));
        Assert.Equal(Origins.Default, StudyEdits.OriginOf(s, "node:I1/cycle"));

        string file = TempFile("osm-lpga.json");
        File.WriteAllText(file, StudyJson.Write(s));
        (int exit, string output) = Tools.Node("tools/engine-check.js", "run", file);
        Assert.True(exit == 0, output);
        Assert.Equal(0, JsonNode.Parse(output)!["unserved"]!.GetValue<double>());
    }

    [Fact]
    public void TheFreewayBecomesBackdropNotRoad()
    {
        Study s = NetworkFromOsm.Build(Lpga(), new OsmStudyRequest { Junctions = [RampsEast, Outlet] }).Study;

        Assert.True(s.Backdrop!["freeways"]!.AsArray().Count > 0);
        Assert.DoesNotContain(s.Links, l => l.Name == "I 95");
    }

    // ------------------------------------------------------------ made-up maps

    /// <summary>A small map in metres around a point, written as Overpass would answer.</summary>
    private sealed class Map
    {
        private readonly JsonArray _elements = [];
        private long _next = 1;

        public long Node(double eastMetres, double northMetres, params (string K, string V)[] tags)
        {
            long id = _next++;
            var e = new JsonObject
            {
                ["type"] = "node",
                ["id"] = id,
                ["lat"] = 29.0 + northMetres / Geo.MetresPerDegreeLat,
                ["lon"] = -81.0 + eastMetres / (Geo.MetresPerDegreeLon * Math.Cos(29.0 * Math.PI / 180)),
            };
            if (tags.Length > 0)
            {
                e["tags"] = Tags(tags);
            }

            _elements.Add(e);
            return id;
        }

        public void Way(long[] nodes, params (string K, string V)[] tags) =>
            _elements.Add(new JsonObject { ["type"] = "way", ["id"] = _next++, ["nodes"] = new JsonArray(nodes.Select(n => (JsonNode)n).ToArray()), ["tags"] = Tags(tags) });

        public OsmData Data() => OsmData.Parse(new JsonObject { ["elements"] = _elements }.ToJsonString());

        private static JsonObject Tags((string K, string V)[] tags)
        {
            var o = new JsonObject();
            foreach ((string k, string v) in tags)
            {
                o[k] = v;
            }

            return o;
        }
    }

    private static (string, string) Hw(string v) => ("highway", v);

    private static (string, string) Name(string v) => ("name", v);

    /// <summary>A crossroads: Main Street east–west (four lanes, 45 mph, a left lane each way), Oak
    /// Avenue north–south (two lanes, no speed tag), a signal on the crossing point.</summary>
    private static (Map Map, long Centre) Crossroads()
    {
        var m = new Map();
        long c = m.Node(0, 0, ("highway", "traffic_signals"));
        long w = m.Node(-400, 0), e = m.Node(400, 0), n = m.Node(0, 400), s = m.Node(0, -400);
        m.Way([w, c], Hw("secondary"), Name("Main Street"), ("lanes", "5"), ("maxspeed", "45 mph"), ("turn:lanes:forward", "left|through|through"), ("turn:lanes:backward", "through|through"));
        m.Way([c, e], Hw("secondary"), Name("Main Street"), ("lanes", "4"), ("maxspeed", "45 mph"), ("turn:lanes:backward", "left|through|through"));
        m.Way([n, c], Hw("residential"), Name("Oak Avenue"));
        m.Way([c, s], Hw("residential"), Name("Oak Avenue"));
        return (m, c);
    }

    [Fact]
    public void ACrossroadsBecomesOneSignalWithFourRoadsAndTheTagsItHas()
    {
        (Map m, long c) = Crossroads();
        OsmData data = m.Data();
        OsmJunction j = Assert.Single(OsmJunctions.Find(data));
        Assert.Equal("Main Street & Oak Avenue", j.Label);

        OsmStudy made = NetworkFromOsm.Build(data, new OsmStudyRequest { Junctions = [j.Id], Budget = 2 });
        Study s = made.Study;

        Assert.Empty(StudyValidator.Check(s));
        Assert.Equal(4, s.Links.Count);
        Assert.Equal(4, s.Nodes.Count(n => n.Type == StudyNode.End));
        StudyLink west = s.Links.Single(l => s.Node(l.B)!.Name == "Main Street (west)");
        Assert.Equal(2, west.Lanes);
        Assert.Equal(45, Units.Mph(west.Speed), 1);
        Assert.Equal(Origins.Osm, StudyEdits.OriginOf(s, $"link:{west.Id}/speed"));
        Assert.True(StudyEdits.GetPocket(s, west.Id, "ba").Left);   // arriving at the signal from the west
        StudyLink north = s.Links.Single(l => s.Node(l.B)!.Name == "Oak Avenue (north)");
        Assert.Equal(1, north.Lanes);
        Assert.Equal(25, Units.Mph(north.Speed), 1);
        Assert.Equal(Origins.Default, StudyEdits.OriginOf(s, $"link:{north.Id}/speed"));
        Assert.Empty(made.Notes);
        Assert.Equal(2, s.Budget);
    }

    [Fact]
    public void TwoChosenJunctionsAreJoinedByOneRoad()
    {
        var m = new Map();
        long a = m.Node(0, 0, ("highway", "traffic_signals")), b = m.Node(500, 0, ("highway", "traffic_signals"));
        long w = m.Node(-400, 0), e = m.Node(900, 0);
        m.Way([w, a, b, e], Hw("primary"), Name("Main Street"), ("lanes", "4"));
        foreach ((long x, double east) in new[] { (a, 0.0), (b, 500.0) })
        {
            m.Way([m.Node(east, 300), x, m.Node(east, -300)], Hw("tertiary"), Name("Cross " + x.ToString(CultureInfo.InvariantCulture)));
        }

        OsmData data = m.Data();
        List<OsmJunction> js = OsmJunctions.Find(data);
        Study s = NetworkFromOsm.Build(data, new OsmStudyRequest { Junctions = js.Select(j => j.Id).ToList() }).Study;

        Assert.Empty(StudyValidator.Check(s));
        Assert.Single(s.Links, l => l.A.StartsWith('I') && l.B.StartsWith('I'));
        Assert.Equal(6, s.Nodes.Count(n => n.Type == StudyNode.End));
        Assert.Equal("Main Street", s.Node("I1")!.Name!.Split(" & ")[0]);
    }

    [Fact]
    public void ARoundaboutRingIsOneRoundabout()
    {
        var m = new Map();
        long[] ring = Enumerable.Range(0, 8).Select(i => m.Node(25 * Math.Sin(i * Math.PI / 4), 25 * Math.Cos(i * Math.PI / 4))).ToArray();
        m.Way([.. ring, ring[0]], Hw("tertiary"), ("junction", "roundabout"));
        m.Way([m.Node(0, 400), ring[0]], Hw("tertiary"), Name("North Road"));
        m.Way([ring[2], m.Node(400, 0)], Hw("tertiary"), Name("East Road"));
        m.Way([ring[4], m.Node(0, -400)], Hw("tertiary"), Name("North Road"));
        m.Way([m.Node(-400, 0), ring[6]], Hw("tertiary"), Name("West Road"));

        OsmData data = m.Data();
        OsmJunction j = Assert.Single(OsmJunctions.Find(data));
        Assert.True(j.IsRoundabout);
        Study s = NetworkFromOsm.Build(data, new OsmStudyRequest { Junctions = [j.Id] }).Study;

        Assert.Equal(StudyNode.Roundabout, s.Node("I1")!.Type);
        Assert.Equal(4, s.LinksAt("I1").Count());
        Assert.Empty(StudyValidator.Check(s));
    }

    [Fact]
    public void AFifthRoadIsLeftOutAndSaysSo()
    {
        (Map m, long c) = Crossroads();
        m.Way([c, m.Node(300, 300)], Hw("residential"), Name("Diagonal Lane"));

        OsmData data = m.Data();
        OsmStudy made = NetworkFromOsm.Build(data, new OsmStudyRequest { Junctions = [OsmJunctions.Find(data).Single().Id] });

        Assert.Equal(4, made.Study.LinksAt("I1").Count());
        Assert.Contains(made.Notes, n => n.Contains("Diagonal Lane", StringComparison.Ordinal) && n.Contains("3 or 4 roads", StringComparison.Ordinal));
        Assert.Empty(StudyValidator.Check(made.Study));
    }

    [Fact]
    public void AJunctionWithNoSignalTaggedStartsAsASignalAndSaysSo()
    {
        var m = new Map();
        long c = m.Node(0, 0);
        m.Way([m.Node(-300, 0), c, m.Node(300, 0)], Hw("tertiary"), Name("Main Street"));
        m.Way([m.Node(0, 300), c], Hw("residential"), Name("Oak Avenue"));

        OsmData data = m.Data();
        OsmStudy made = NetworkFromOsm.Build(data, new OsmStudyRequest { Junctions = [OsmJunctions.Find(data).Single().Id] });

        Assert.Equal(StudyNode.SignalType, made.Study.Node("I1")!.Type);
        Assert.Equal(Origins.Default, StudyEdits.OriginOf(made.Study, "node:I1/type"));
        Assert.Contains(made.Notes, n => n.Contains("no traffic signal", StringComparison.Ordinal));
        Assert.Empty(StudyValidator.Check(made.Study));   // a T
    }

    [Theory]
    [InlineData("45 mph", 45.0)]
    [InlineData("70", 43.0)]
    [InlineData("50 km/h", 31.0)]
    [InlineData("signals", null)]
    [InlineData(null, null)]
    public void SpeedsAreReadAsPeopleTagThem(string? tag, double? mph) => Assert.Equal(mph, OsmQuery.MaxSpeedMph(tag));

    [Fact]
    public void TurnLanesSayWhatGoesStraightOn()
    {
        Assert.Equal((3, true, true), OsmQuery.TurnLanes("left|left|none|none|through;right|right") is { } a ? (a.Through - 0, a.Left, a.Right) : default);
        Assert.Equal((2, false, false), OsmQuery.TurnLanes("through|merge_to_left") is { } b ? (b.Through, b.Left, b.Right) : default);
        Assert.Null(OsmQuery.TurnLanes(""));
    }

    [Fact]
    public void TheQueryAsksForOneBoxOfRoads()
    {
        string q = OsmQuery.Roads(29.2185, -81.103, 29.229, -81.087);

        Assert.Contains("(29.2185,-81.103,29.229,-81.087)", q, StringComparison.Ordinal);
        Assert.StartsWith("[out:json]", q, StringComparison.Ordinal);
        Assert.Contains("out body", q, StringComparison.Ordinal);
    }

    [Fact]
    public void NotAnOverpassAnswerSaysSo()
    {
        Assert.Throws<TrafficLabPlus.Core.Build.StudyFormatException>(() => OsmData.Parse("{\"remark\":\"runtime error\"}"));
        Assert.Throws<TrafficLabPlus.Core.Build.StudyFormatException>(() => OsmData.Parse("<html>busy</html>"));
        _ = JsonSerializer.Serialize(1);
    }
}
