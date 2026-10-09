namespace TrafficLabPlus.Tests;

/// <summary>
/// The simulation engine is JavaScript, because it runs in the published page. Its tests are written
/// for Node (tests/js/engine.test.js) — including the yardstick: LPGA rebuilt from data must drive
/// car for car, and score plan for plan, exactly like Ron's original page. This runs them as part of
/// <c>dotnet test</c>.
/// </summary>
public class EngineTests
{
    [Fact]
    public void TheEngineTestsPassUnderNode()
    {
        (int exit, string output) = Tools.Node("--test", "tests/js/engine.test.js");

        Assert.True(exit == 0, output);
        Assert.Contains("fail 0", output, StringComparison.Ordinal);
    }

    [Fact]
    public void TheBuiltInExampleIsTheStudyTheToolWrites()
    {
        // samples/lpga.json is generated from the original page by tools/make-lpga-study.js;
        // running the tool again must not change it, so the example and its source cannot drift.
        string file = Path.Combine(Tools.RepoRoot, "samples", "lpga.json");
        string before = File.ReadAllText(file);

        (int exit, string output) = Tools.Node("tools/make-lpga-study.js");

        Assert.True(exit == 0, output);
        Assert.Equal(before, File.ReadAllText(file));
    }
}
