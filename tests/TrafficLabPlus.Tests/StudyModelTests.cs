using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using TrafficLabPlus.Core.Build;
using TrafficLabPlus.Core.Model;

namespace TrafficLabPlus.Tests;

public class StudyModelTests
{
    private static Study Lpga() => StudyJson.Read(PageBuilder.LpgaExampleJson());

    private static string TempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "TrafficLabPlusTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    // ------------------------------------------------------------- reading and writing

    [Fact]
    public void LpgaReadAndWrittenAgainLosesNothing()
    {
        JsonNode original = JsonNode.Parse(PageBuilder.LpgaExampleJson())!;
        JsonNode again = JsonNode.Parse(StudyJson.Write(Lpga()))!;

        Assert.True(JsonNode.DeepEquals(original, again), "LPGA came back different");
    }

    [Fact]
    public void FieldsThisProgramDoesNotKnowAreKept()
    {
        string json = """{"format":1,"title":"T","budget":5,"demand":{"baseTotal":100,"wind":3},"nodes":[{"id":"A","type":"end","x":0,"y":0,"colour":"red"}],"links":[],"teacherNote":"keep me"}""";

        JsonNode again = JsonNode.Parse(StudyJson.Write(StudyJson.Read(json)))!;

        Assert.Equal("keep me", again["teacherNote"]!.GetValue<string>());
        Assert.Equal(3, again["demand"]!["wind"]!.GetValue<int>());
        Assert.Equal("red", again["nodes"]![0]!["colour"]!.GetValue<string>());
    }

    [Fact]
    public void ANewerFormatIsRefusedNotHalfRead()
    {
        var ex = Assert.Throws<StudyFormatException>(() => StudyJson.Read("""{"format":2,"title":"T","nodes":"not even a list"}"""));

        Assert.Equal("This study was made by a newer TrafficLab+ (format 2). Update TrafficLab+ to open it.", ex.Message);
    }

    [Fact]
    public void NotAStudySaysSo()
    {
        Assert.Throws<StudyFormatException>(() => StudyJson.Read("[1,2]"));
        Assert.Throws<StudyFormatException>(() => StudyJson.Read("not json"));
        var ex = Assert.Throws<StudyFormatException>(() => StudyJson.Read("""{"format":1,"title":"T","budget":"lots"}"""));
        Assert.Contains("budget", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheBuiltPageLeavesTheEditorsNotesOut()
    {
        Study s = Lpga();
        StudyEdits.Mark(s, "node:I1/cycle", Origins.Typed);

        string page = PageBuilder.Build(s);

        Assert.DoesNotContain("node:I1/cycle", page, StringComparison.Ordinal);
        Assert.Contains("LPGA Traffic Lab", page, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------- the .trafficlab file

    [Fact]
    public void AStudyFileRoundTripsWithItsNotesAndAttachments()
    {
        string path = Path.Combine(TempDir(), "lpga" + StudyFile.Extension);
        var doc = new StudyDocument { Study = Lpga(), Notes = "Try a roundabout at I3." };
        doc.Attachments["osm.json"] = [1, 2, 3];

        StudyFile.Save(doc, path);
        StudyDocument back = StudyFile.Load(path);

        Assert.Equal(StudyJson.Write(doc.Study), StudyJson.Write(back.Study));
        Assert.Equal("Try a roundabout at I3.", back.Notes);
        Assert.Equal([1, 2, 3], back.Attachments["osm.json"]);
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!));   // no half-written file left beside it
    }

    [Fact]
    public void SavingOverAStudyReplacesIt()
    {
        string path = Path.Combine(TempDir(), "s" + StudyFile.Extension);
        var doc = new StudyDocument { Study = Lpga() };
        StudyFile.Save(doc, path);

        doc.Study.Budget = 7.5;
        StudyFile.Save(doc, path);

        Assert.Equal(7.5, StudyFile.Load(path).Study.Budget);
    }

    [Fact]
    public void ANewerStudyFileIsRefused()
    {
        string path = Path.Combine(TempDir(), "new" + StudyFile.Extension);
        using (ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Create))
        using (var w = new StreamWriter(zip.CreateEntry(StudyFile.StudyEntry).Open()))
        {
            w.Write("""{"format":3,"title":"From the future"}""");
        }

        var ex = Assert.Throws<StudyFormatException>(() => StudyFile.Load(path));
        Assert.Contains("newer TrafficLab+", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFileThatIsNotAZipIsRefusedInPlainWords()
    {
        string path = Path.Combine(TempDir(), "bad" + StudyFile.Extension);
        File.WriteAllText(path, "hello");

        var ex = Assert.Throws<StudyFormatException>(() => StudyFile.Load(path));
        Assert.Contains("not a TrafficLab+ study", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APlainJsonStudyOpens()
    {
        string path = Path.Combine(TempDir(), "lpga.json");
        File.WriteAllText(path, PageBuilder.LpgaExampleJson());

        Assert.Equal("LPGA Traffic Lab", StudyFile.Load(path).Study.Title);
    }

    // ------------------------------------------------------------- the check, in both languages

    private static IEnumerable<Study> BrokenStudies()
    {
        Study Make(Action<Study> spoil)
        {
            Study s = StudyTemplates.Create(new NewStudyRequest { Title = "T", Signals = 2 });
            spoil(s);
            return s;
        }

        yield return Lpga();
        yield return Make(_ => { });
        yield return Make(s => s.Nodes.Clear());
        yield return Make(s => s.Nodes[1].Id = s.Nodes[0].Id);
        yield return Make(s => s.Nodes[0].Id = "");
        yield return Make(s => s.Nodes[1].Type = "bridge");
        yield return Make(s => s.Node("I1")!.Signal!.Cycle = 30);
        yield return Make(s => s.Node("I2")!.Signal!.Split = 0.9);
        yield return Make(s => s.Node("I1")!.Signal = null);
        yield return Make(s => s.Node("I1")!.Signal!.MainLinks = ["C2N"]);
        yield return Make(s => s.Links[0].Lanes = 4);
        yield return Make(s => s.Links[1].Speed = 0);
        yield return Make(s => s.Links[2].B = "nowhere");
        yield return Make(s => s.Links[2].B = s.Links[2].A);
        yield return Make(s => s.Links[3].Id = s.Links[0].Id);
        yield return Make(s => s.Links.RemoveAt(1));
        yield return Make(s => s.Links.Add(new StudyLink { Id = "Z", A = "I1", B = "S2", Lanes = 1, Speed = 10 }));
        yield return Make(s => s.Pockets = new() { ["Q9:ab"] = new Pocket { L = 1 }, ["M1:up"] = new Pocket { R = 1 } });
        yield return Make(s => s.Demand.BaseTotal = 0);
        yield return Make(s => { s.Demand.Mode = Demand.Volumes; s.Nodes[0].Volume = 400; });
        yield return Make(s => s.Budget = 0);
        yield return Make(s => { foreach (StudyNode n in s.Nodes.Where(n => n.Type == StudyNode.End).Skip(1)) { n.Type = StudyNode.Roundabout; } });
        yield return Make(s =>
        {
            // a fifth road at I1
            StudyNode extra = StudyEdits.AddNode(s, StudyNode.End, 100, 100);
            s.Links.Add(new StudyLink { Id = "X5", A = extra.Id, B = "I1", Lanes = 1, Speed = 10 });
        });
    }

    [Fact]
    public void TheWindowsCheckSaysExactlyWhatThePageWouldSay()
    {
        List<Study> studies = BrokenStudies().ToList();
        string file = Path.Combine(TempDir(), "studies.json");
        File.WriteAllText(file, "[" + string.Join(",", studies.Select(s => StudyJson.Write(s))) + "]");

        (int exit, string output) = Tools.Node("tools/engine-check.js", "validate", file);
        Assert.True(exit == 0, output);
        List<List<string>> fromPage = JsonSerializer.Deserialize<List<List<string>>>(output)!;

        for (int i = 0; i < studies.Count; i++)
        {
            Assert.Equal(fromPage[i], StudyValidator.Check(studies[i]).Select(p => p.Message).ToList());
        }

        Assert.Empty(fromPage[0]);   // LPGA
        Assert.Empty(fromPage[1]);   // a fresh template
        Assert.True(fromPage.Skip(2).All(m => m.Count > 0), "every spoiled study has something wrong with it");
    }

    // ------------------------------------------------------------- new studies

    public static TheoryData<int, bool> Shapes => new() { { 1, false }, { 1, true }, { 2, false }, { 5, false }, { StudyTemplates.MaxSignals, false } };

    [Theory]
    [MemberData(nameof(Shapes))]
    public void EveryStartingNetworkIsSoundAndRuns(int signals, bool tee)
    {
        Study s = StudyTemplates.Create(new NewStudyRequest { Title = "Mine", Signals = signals, Tee = tee, Budget = 3 });
        Assert.Empty(StudyValidator.Check(s));
        Assert.Equal(3, s.Budget);
        Assert.Equal(signals, s.Nodes.Count(n => n.Type == StudyNode.SignalType));

        string file = Path.Combine(TempDir(), "s.json");
        File.WriteAllText(file, StudyJson.Write(s));
        (int exit, string output) = Tools.Node("tools/engine-check.js", "run", file);
        Assert.True(exit == 0, output);

        JsonNode result = JsonNode.Parse(output)!;
        Assert.Equal(0, result["unserved"]!.GetValue<double>());
        Assert.True(result["done"]!.GetValue<int>() > 50, output);
    }

    [Fact]
    public void ANewStudyCarriesItsBudgetOntoThePage()
    {
        Study s = StudyTemplates.Create(new NewStudyRequest { Title = "Budget test", Budget = 2.25 });

        string page = PageBuilder.Build(s);

        Assert.Contains("\"budget\":2.25", page, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryStartingValueIsMarkedAsOne()
    {
        Study s = StudyTemplates.Create(new NewStudyRequest { Title = "T" });

        Assert.Equal(Origins.Default, StudyEdits.OriginOf(s, "link:M1/lanes"));
        StudyEdits.Mark(s, "link:M1/lanes", Origins.Typed);
        Assert.Equal(Origins.Typed, StudyEdits.OriginOf(s, "link:M1/lanes"));
    }

    [Fact]
    public void TheTrafficSectionShowsWhatTheEngineWillSendIn()
    {
        Study volumes = StudyTemplates.Create(new NewStudyRequest { Title = "V", Signals = 2 });
        volumes.Demand.Mode = Demand.Volumes;
        foreach (StudyNode n in volumes.Nodes.Where(n => n.Type == StudyNode.End))
        {
            n.Volume = 100 + n.Id.Length * 37;
        }

        foreach (Study s in new[] { Lpga(), StudyTemplates.Create(new NewStudyRequest { Title = "T", Signals = 3 }), volumes })
        {
            string file = Path.Combine(TempDir(), "s.json");
            File.WriteAllText(file, StudyJson.Write(s));
            (int exit, string output) = Tools.Node("tools/engine-check.js", "entering", file);
            Assert.True(exit == 0, output);
            Dictionary<string, double> engine = JsonSerializer.Deserialize<Dictionary<string, double>>(output)!;

            Dictionary<string, double> shown = DemandShares.Entering(s);
            foreach ((string id, double v) in engine)
            {
                Assert.Equal(v, shown[id], 6);
            }
        }
    }

    // ------------------------------------------------------------- editing

    [Fact]
    public void RemovingAnIntersectionLeavesNothingNamingIt()
    {
        Study s = StudyTemplates.Create(new NewStudyRequest { Title = "T", Signals = 2 });
        StudyEdits.SetPocket(s, "M2", "ab", left: true, right: false);
        s.Demand.NoTrips = [["N2", "S1"]];
        StudyEdits.Mark(s, "node:N2/weight", Origins.Typed);

        StudyEdits.RemoveNode(s, "I2");
        StudyEdits.RemoveNode(s, "N2");

        Assert.DoesNotContain(s.Links, l => l.A == "I2" || l.B == "I2");
        Assert.Null(s.Pockets);
        Assert.DoesNotContain("M2", s.Node("I1")!.Signal!.MainLinks!);
        Assert.Empty(s.Demand.NoTrips!);
        Assert.False(s.From!.ContainsKey("node:N2/weight"));
    }

    [Fact]
    public void RoadsThatCannotBeBuiltSayWhy()
    {
        Study s = StudyTemplates.Create(new NewStudyRequest { Title = "T", Signals = 2 });

        Assert.NotNull(StudyEdits.AddLink(s, "I1", "I1").Why);
        Assert.Contains("already joined", StudyEdits.AddLink(s, "I1", "I2").Why, StringComparison.Ordinal);
        Assert.Contains("only one road", StudyEdits.AddLink(s, "W", "I2").Why, StringComparison.Ordinal);
        Assert.Contains("already has 4 roads", StudyEdits.AddLink(s, "I1", StudyEdits.AddNode(s, StudyNode.End, 0, 0).Id).Why, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAddedRoadEndAndRoadMakeASoundStudy()
    {
        Study s = StudyTemplates.Create(new NewStudyRequest { Title = "T", Tee = true });
        StudyNode south = StudyEdits.AddNode(s, StudyNode.End, 300, 600);

        (StudyLink? road, string? why) = StudyEdits.AddLink(s, "I1", south.Id);

        Assert.Null(why);
        Assert.Equal("E1", south.Id);
        Assert.Equal(StudyEdits.DefaultEndWeight, south.Weight);
        Assert.Equal(1, road!.Lanes);
        Assert.Empty(StudyValidator.Check(s));
    }

    [Fact]
    public void ChangingANodesKindGivesItWhatThatKindNeeds()
    {
        Study s = StudyTemplates.Create(new NewStudyRequest { Title = "T" });
        StudyNode i1 = s.Node("I1")!;

        StudyEdits.SetType(s, i1, StudyNode.Roundabout);
        Assert.Null(i1.Signal);
        StudyEdits.SetType(s, i1, StudyNode.SignalType);
        Assert.Equal(90, i1.Signal!.Cycle);
        Assert.Empty(StudyValidator.Check(s));
    }

    [Fact]
    public void RenamingAStreetRenamesItEverywhereButOnlyThatStreet()
    {
        Study s = StudyTemplates.Create(new NewStudyRequest { Title = "T", Signals = 10, MainStreet = "Nova Road" });

        (int roads, int names) = StudyEdits.RenameStreet(s, "Cross Street 1", "Beville Road");

        Assert.Equal(2, roads);   // north and south halves
        Assert.Equal(3, names);   // the intersection and its two road ends
        Assert.Equal("Nova Road & Beville Road", s.Node("I1")!.Name);
        Assert.Equal("Beville Road", s.Node("I1")!.Short);
        Assert.Equal("Beville Road (north)", s.Node("N1")!.Name);
        Assert.Equal("Nova Road & Cross Street 10", s.Node("I10")!.Name);   // a whole name, not a prefix
        Assert.Equal("Cross Street 10", s.Link("C10N")!.Name);
    }

    [Fact]
    public void TurnLanesAreSetAndCleared()
    {
        Study s = StudyTemplates.Create(new NewStudyRequest { Title = "T" });

        StudyEdits.SetPocket(s, "M1", "ab", left: true, right: true);
        Assert.Equal((true, true), StudyEdits.GetPocket(s, "M1", "ab"));
        StudyEdits.SetPocket(s, "M1", "ab", left: false, right: true);
        Assert.Equal((false, true), StudyEdits.GetPocket(s, "M1", "ab"));
        StudyEdits.SetPocket(s, "M1", "ab", left: false, right: false);
        Assert.Null(s.Pockets);
    }

    [Fact]
    public void TheRecentListKeepsTheNewestFirstAndForgetsMissingFiles()
    {
        string dir = TempDir();
        var recent = new RecentStudies(Path.Combine(dir, "recent.txt"));
        string[] files = Enumerable.Range(1, 10).Select(i => Path.Combine(dir, $"s{i}.trafficlab")).ToArray();
        foreach (string f in files)
        {
            File.WriteAllText(f, "");
            recent.Add(f);
        }

        recent.Add(files[3]);
        File.Delete(files[9]);

        IReadOnlyList<string> list = recent.Load();
        Assert.Equal(files[3], list[0]);
        Assert.DoesNotContain(files[9], list);
        Assert.True(list.Count <= RecentStudies.Max);
    }

    [Fact]
    public void FeetAndMilesPerHourComeBackToTheSameMetres()
    {
        Assert.Equal(30, Units.Mph(StudyEdits.DefaultSpeed), 3);
        Assert.Equal(1000, Units.Feet(Units.Metres(1000)), 9);
        Assert.Equal(13.4112, Units.Mps(30), 9);
    }
}
