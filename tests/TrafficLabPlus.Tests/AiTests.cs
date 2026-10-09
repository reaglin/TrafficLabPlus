using TrafficLabPlus.Core.Ai;
using TrafficLabPlus.Core.Build;
using TrafficLabPlus.Core.Model;

namespace TrafficLabPlus.Tests;

/// <summary>What TrafficLab+ asks an AI and how it reads the answer. No AI is called: the answers
/// are the shapes models really send (Gamify+'s DraftReader lesson).</summary>
public class AiTests
{
    private static Study Lpga() => StudyJson.Read(PageBuilder.LpgaExampleJson());

    private const string Good = """{"changes":[{"id":"I3","setting":"cycleSeconds","value":120,"reason":"Busy junction"}],"notes":"Check the split."}""";

    [Theory]
    [InlineData(Good)]
    [InlineData("```json\n" + Good + "\n```")]
    [InlineData("Here are the changes you asked for:\n" + Good + "\nLet me know if you need more.")]
    [InlineData("""{"changes":[{"id":"I3","setting":"cycleSeconds","value":"120 s","reason":"Busy junction"},],}""")]
    [InlineData("""[{"id":"I3","setting":"cycleSeconds","value":120,"reason":"Busy junction"}]""")]
    public void TheAnswerIsReadHoweverItIsWrapped(string answer)
    {
        Study s = Lpga();
        s.Node("I3")!.Signal!.Cycle = 90;

        AiProposals p = AiPrompts.ReadNetwork(s, answer);

        AiProposal c = Assert.Single(p.Changes);
        Assert.Equal("120 s", c.After);
        Assert.Equal("Busy junction", c.Reason);
    }

    [Fact]
    public void AnAnswerWithNoJsonSaysWhatToDo()
    {
        var ex = Assert.Throws<AiReadException>(() => AiPrompts.ReadNetwork(Lpga(), "I'm sorry, I can't see the roads."));
        Assert.Contains("Ask again", ex.Message, StringComparison.Ordinal);
        Assert.Throws<AiReadException>(() => AiPrompts.ReadNetwork(Lpga(), """{"changes":[{"id":"I3",""" ));
    }

    [Fact]
    public void ChangesTheStudyCannotTakeAreLeftOutWithAReason()
    {
        const string answer = """
            {"changes":[
              {"id":"I9","setting":"cycleSeconds","value":120,"reason":"x"},
              {"id":"I3","setting":"cycleSeconds","value":300,"reason":"x"},
              {"id":"L1","setting":"throughLanesEachWay","value":5,"reason":"x"},
              {"id":"L1","setting":"colour","value":"red","reason":"x"},
              {"id":"L1","setting":"speedMph","value":45,"reason":"Posted 45"},
              {"id":"L2","setting":"leftTurnLaneAt","value":"I2","reason":"x"}
            ]}
            """;

        AiProposals p = AiPrompts.ReadNetwork(Lpga(), answer);

        Assert.Equal("Posted 45", Assert.Single(p.Changes).Reason);
        Assert.Equal(5, p.LeftOut.Count);
        Assert.Contains(p.LeftOut, w => w.Contains("no intersection or road with that id", StringComparison.Ordinal));
        Assert.Contains(p.LeftOut, w => w.Contains("40 to 180", StringComparison.Ordinal));
        Assert.Contains(p.LeftOut, w => w.Contains("1 to 3", StringComparison.Ordinal));
        Assert.Contains(p.LeftOut, w => w.Contains("already there", StringComparison.Ordinal));   // LPGA's L2 has that lane today
    }

    [Fact]
    public void OnlyTheTickedChangesAreAppliedAndEachSaysTheAiSuggestedIt()
    {
        Study s = Lpga();
        AiProposals p = AiPrompts.ReadNetwork(s, """
            {"changes":[
              {"id":"I3","setting":"protectedLeftSide","value":true,"reason":"Heavy lefts"},
              {"id":"L1","setting":"speedMph","value":35,"reason":"x"},
              {"id":"W2","setting":"rightTurnLaneAt","value":"I3","reason":"x"}
            ]}
            """);
        p.Changes[1].Use = false;

        int n = AiApply.Apply(s, p.Changes);

        Assert.Equal(2, n);
        Assert.True(s.Node("I3")!.Signal!.ProtSide);
        Assert.Equal(Origins.Ai, StudyEdits.OriginOf(s, "node:I3/protSide"));
        Assert.Equal(44, Units.Mph(s.Link("L1")!.Speed), 0);   // unticked: unchanged
        Assert.Empty(StudyValidator.Check(s));
    }

    [Fact]
    public void TrafficEstimatesKeepFdotCountsAndSwitchToTypedVolumes()
    {
        Study s = StudyTemplates.Create(new NewStudyRequest { Title = "T" });
        StudyEdits.Mark(s, "node:N1/volume", Origins.Fdot);

        AiProposals p = AiPrompts.ReadTraffic(s, """{"ends":[{"id":"W","enteringPerHour":1200,"reason":"Arterial"},{"id":"N1","enteringPerHour":50},{"id":"S1","enteringPerHour":99999},{"id":"I1","enteringPerHour":10}]}""");
        AiApply.Apply(s, p.Changes);

        Assert.Single(p.Changes);
        Assert.Equal(3, p.LeftOut.Count);
        Assert.Equal(Demand.Volumes, s.Demand.Mode);
        Assert.Equal(1200, s.Node("W")!.Volume);
        Assert.True(s.Node("E")!.Volume > 0);   // the others keep what they had
        Assert.Equal(Origins.Ai, StudyEdits.OriginOf(s, "node:W/volume"));
        Assert.Empty(StudyValidator.Check(s));
    }

    [Fact]
    public void AChallengeIsProposedFieldByField()
    {
        Study s = Lpga();

        AiProposals p = AiPrompts.ReadChallenge(s, """
            {"title":"Tight Budget Lab","budgetMillions":2,"costsMillions":{"roundabout":9,"pocketL":0.45,"teleporter":1},
             "demandButtons":[{"name":"Today","percent":100},{"name":"Game Day","percent":160}],"reasons":"Two fixes fit; roundabouts priced out."}
            """);

        Assert.Contains(p.Changes, c => c.Setting == "Title" && c.After == "Tight Budget Lab");
        Assert.Contains(p.Changes, c => c.Setting == "Budget" && c.After == "$2M");
        Assert.Contains(p.Changes, c => c.Setting.Contains("roundabout", StringComparison.Ordinal));
        Assert.DoesNotContain(p.Changes, c => c.Setting.Contains("left-turn", StringComparison.Ordinal));   // the same price: not a change
        Assert.Contains(p.LeftOut, w => w.Contains("teleporter", StringComparison.Ordinal));

        AiApply.Apply(s, p.Changes);
        Assert.Equal(2, s.Budget);
        Assert.Equal(9, s.Costs!.Roundabout);
        Assert.Equal(1.6, s.Demand.Scenarios![1].Mult, 6);
        Assert.Empty(StudyValidator.Check(s));
    }

    [Fact]
    public void EveryPromptLeadsWithWhoIsAskingAndCarriesTheStudy()
    {
        Study s = Lpga();
        foreach (string prompt in new[] { AiPrompts.Network(s, "dual lefts"), AiPrompts.Traffic(s, ""), AiPrompts.Challenge(s, "hard"), AiPrompts.Coach(s, "{\"score\":40}", "why?") })
        {
            Assert.Matches("^An? (student|instructor)", prompt.TrimStart());   // "A student…", "An instructor…"
            Assert.Contains("LPGA & Williamson", prompt, StringComparison.Ordinal);
        }

        Assert.Contains("\"throughLanesEachWay\":2", AiPrompts.Describe(s), StringComparison.Ordinal);
        Assert.Contains("{\"score\":40}", AiPrompts.Coach(s, "{\"score\":40}", ""), StringComparison.Ordinal);
    }

    [Fact]
    public void TheCoachsAnswerLosesItsMarkdown() =>
        Assert.Equal("Williamson fails.\nTry a protected left.", AiApply.Tidy("## Williamson fails.\n**Try** a protected left."));
}
