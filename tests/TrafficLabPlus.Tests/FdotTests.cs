using System.Net;
using System.Text.Json.Nodes;
using TrafficLabPlus.Core.Counts;
using TrafficLabPlus.Core.Model;
using TrafficLabPlus.Core.Osm;

namespace TrafficLabPlus.Tests;

/// <summary>FDOT's counts for the LPGA corridor (tests/fixtures/fdot-lpga.json, fetched 2026-10-09)
/// matched to the LPGA study made from OpenStreetMap. No network.</summary>
public class FdotTests
{
    private static List<FdotCount> Counts() => FdotCounts.Parse(File.ReadAllText(Path.Combine(Tools.RepoRoot, "tests", "fixtures", "fdot-lpga.json")));

    private static Study LpgaFromOsm() => NetworkFromOsm.Build(
        OsmData.Parse(File.ReadAllText(Path.Combine(Tools.RepoRoot, "tests", "fixtures", "osm-lpga.json"))),
        new OsmStudyRequest { Junctions = ["j101035693", "j3269748465", "j4750842748", "j3269758871", "j3269758870"] }).Study;

    private static CountMatch End(List<CountMatch> ms, string name) => ms.Single(m => m.EndName == name);

    [Fact]
    public void TheCountsAreReadWithTheirFactors()
    {
        List<FdotCount> counts = Counts();

        FdotCount lpga = counts.Single(c => c.Site == "797025");
        Assert.Equal(29000, lpga.Aadt);
        Assert.Equal(2025, lpga.Year);
        Assert.Equal(0.09, lpga.K, 6);
        Assert.Equal(0.576, lpga.D, 6);
        Assert.False(lpga.OneWay);
        Assert.True(counts.Single(c => c.Site == "792025").OneWay);   // the I-95 off-ramp
    }

    [Fact]
    public void EachCountedRoadFindsItsCountAndLocalRoadsDoNot()
    {
        List<CountMatch> ms = FdotCounts.Match(LpgaFromOsm(), Counts());

        Assert.Equal("797025", Assert.Single(End(ms, "LPGA Boulevard (west)").Counts).Count.Site);
        Assert.Equal("797025", Assert.Single(End(ms, "LPGA Boulevard (northeast)").Counts).Count.Site);
        Assert.Equal("797087", Assert.Single(End(ms, "Williamson Boulevard (north)").Counts).Count.Site);
        Assert.Equal("797086", Assert.Single(End(ms, "Williamson Boulevard (southeast)").Counts).Count.Site);
        Assert.False(End(ms, "Cornerstone Boulevard (west)").Found);
        Assert.False(End(ms, "Ramp (west)").Found);   // a short ramp beside LPGA must not take LPGA's count
    }

    [Fact]
    public void ADesignHourIsAadtTimesKTimesD()
    {
        CountMatch lpga = End(FdotCounts.Match(LpgaFromOsm(), Counts()), "LPGA Boulevard (west)");

        Assert.Equal(29000 * 0.09 * 0.576, lpga.Entering, 6);
        Assert.Equal(29000 * 0.09 * (1 - 0.576), lpga.Exiting, 6);

        lpga.K = 0.10;   // the student knows better
        Assert.Equal(29000 * 0.10 * 0.576, lpga.Entering, 6);
    }

    [Fact]
    public void AnOffRampBringsTrafficInAndTakesNoneOut()
    {
        CountMatch ramp = End(FdotCounts.Match(LpgaFromOsm(), Counts()), "Ramp (southeast)");

        (FdotCount c, bool inbound) = Assert.Single(ramp.Counts);
        Assert.Equal("792025", c.Site);   // "I-95/SR-9" to "LPGA BLVD"
        Assert.True(inbound);
        Assert.Equal(12500 * 0.09, ramp.Entering, 6);
        Assert.Equal(0, ramp.Exiting);
    }

    [Fact]
    public void UsingTheCountsMakesASoundStudyThatSaysWhereTheyCameFrom()
    {
        Study s = LpgaFromOsm();
        List<CountMatch> ms = FdotCounts.Match(s, Counts());
        End(ms, "Williamson Boulevard (north)").Use = false;

        FdotCounts.Apply(s, ms, 2025);

        Assert.Equal(Demand.Volumes, s.Demand.Mode);
        StudyNode west = s.Nodes.Single(n => n.Name == "LPGA Boulevard (west)");
        Assert.Equal(Math.Round(29000 * 0.09 * 0.576), west.Volume);
        Assert.Equal(Origins.Fdot, StudyEdits.OriginOf(s, $"node:{west.Id}/volume"));
        StudyNode north = s.Nodes.Single(n => n.Name == "Williamson Boulevard (north)");
        Assert.NotEqual(Origins.Fdot, StudyEdits.OriginOf(s, $"node:{north.Id}/volume"));   // unticked: its estimate
        Assert.True(north.Volume > 0);
        Assert.Contains("Florida Department of Transportation", s.Sources!.Extra!["counts"].GetString(), StringComparison.Ordinal);
        Assert.Empty(StudyValidator.Check(s));

        string file = Path.Combine(Path.GetTempPath(), "TrafficLabPlusTests", Guid.NewGuid().ToString("N") + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, StudyJson.Write(s));
        (int exit, string output) = Tools.Node("tools/engine-check.js", "run", file);
        Assert.True(exit == 0, output);
        Assert.Equal(0, JsonNode.Parse(output)!["unserved"]!.GetValue<double>());
    }

    [Fact]
    public void TheEngineSendsInWhatTheCountsSay()
    {
        Study s = LpgaFromOsm();
        List<CountMatch> ms = FdotCounts.Match(s, Counts());
        FdotCounts.Apply(s, ms, 2025);

        Dictionary<string, double> entering = DemandShares.Entering(s);
        foreach (CountMatch m in ms.Where(m => m.Found && m.Entering > 0))
        {
            Assert.Equal(Math.Round(m.Entering), entering[m.EndId], 6);
        }
    }

    [Fact]
    public void TheStudentsChoicesAreKeptWithTheAnswer()
    {
        string json = File.ReadAllText(Path.Combine(Tools.RepoRoot, "tests", "fixtures", "fdot-lpga.json"));

        byte[] kept = FdotCache.ToBytes(json, "2026-10-09", new Dictionary<string, (bool, double, double)> { ["E2"] = (false, 0.10, 0.6) });
        var back = FdotCache.FromBytes(kept)!.Value;

        Assert.Equal("2026-10-09", back.Fetched);
        Assert.Equal((false, 0.10, 0.6), back.Choices["E2"]);
        Assert.Equal(9, FdotCounts.Parse(back.Json).Count);
    }

    [Fact]
    public void StoppingTheCountsGoesBackToOneTotalWithTheSameTraffic()
    {
        Study s = LpgaFromOsm();
        FdotCounts.Apply(s, FdotCounts.Match(s, Counts()), 2025);
        double total = s.Nodes.Sum(n => n.Volume ?? 0);

        FdotCounts.StopUsing(s);

        Assert.Equal(Demand.Gravity, s.Demand.Mode);
        Assert.Equal(Math.Round(total), s.Demand.BaseTotal);
        Assert.False(s.Sources!.Extra!.ContainsKey("counts"));
        Assert.DoesNotContain(s.Nodes, n => StudyEdits.OriginOf(s, $"node:{n.Id}/volume") == Origins.Fdot);
        Assert.Empty(StudyValidator.Check(s));
    }

    [Fact]
    public void TheStudyKnowsWhereItIsOnTheEarth()
    {
        Study s = LpgaFromOsm();
        StudyNode williamson = s.Node("I3")!;

        (double lat, double lon) = s.Geo!.ToLatLon(williamson.X, williamson.Y);

        Assert.Equal(29.22393, lat, 3);
        Assert.Equal(-81.09378, lon, 3);
        Assert.True(s.Geo.InFlorida);
    }

    [Fact]
    public async Task OutsideFloridaOrOffTheMapTheLookupSaysWhatToDoInstead()
    {
        var client = new FdotClient(new HttpClient(new Refuse()));

        Study layout = StudyTemplates.Create(new NewStudyRequest { Title = "T" });
        Assert.Contains("not made from the map", (await Assert.ThrowsAsync<OsmServiceException>(() => client.CountsAsync(layout))).Message, StringComparison.Ordinal);

        Study ohio = LpgaFromOsm();
        ohio.Geo!.Lat = 39.96;
        ohio.Geo.Lon = -83.0;
        Assert.Contains("Florida only", (await Assert.ThrowsAsync<OsmServiceException>(() => client.CountsAsync(ohio))).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheLookupAsksFdotsLayerForTheStudysBox()
    {
        var net = new Answer(File.ReadAllText(Path.Combine(Tools.RepoRoot, "tests", "fixtures", "fdot-lpga.json")));
        var client = new FdotClient(new HttpClient(net));

        string json = await client.CountsAsync(LpgaFromOsm());

        Assert.Equal(9, FdotCounts.Parse(json).Count);
        string url = net.Asked!.ToString();
        Assert.StartsWith(FdotCounts.Layer, url, StringComparison.Ordinal);
        Assert.Contains("geometryType=esriGeometryEnvelope", url, StringComparison.Ordinal);
        Assert.Contains("inSR=4326", url, StringComparison.Ordinal);
    }

    [Fact]
    public void AnErrorFromTheLayerIsSaidPlainly()
    {
        var ex = Assert.Throws<TrafficLabPlus.Core.Build.StudyFormatException>(() => FdotCounts.Parse("""{"error":{"code":400,"message":"Invalid query"}}"""));
        Assert.Contains("Invalid query", ex.Message, StringComparison.Ordinal);
    }

    private sealed class Refuse : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("no network in this test");
    }

    private sealed class Answer(string body) : HttpMessageHandler
    {
        public Uri? Asked { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }
}
