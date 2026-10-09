using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TrafficLabPlus.Core.Build;

namespace TrafficLabPlus.Tests;

public partial class PageBuilderTests
{
    private static string Lpga() => PageBuilder.Build(PageBuilder.LpgaExampleJson());

    [Fact]
    public void APageIsOneFileThatNeedsNothingElse()
    {
        string page = Lpga();

        // nothing fetched: no src or href to anywhere, no stylesheet link, no @import, no url() but data:
        Assert.DoesNotMatch(SrcOrHref(), page);
        Assert.DoesNotContain("<link", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@import", page, StringComparison.Ordinal);
        Assert.DoesNotContain("fonts.googleapis", page, StringComparison.Ordinal);
        foreach (Match url in CssUrl().Matches(page))
        {
            Assert.StartsWith("data:", url.Groups[1].Value, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void EveryPlaceholderIsFilled()
    {
        Assert.DoesNotContain("{{", Lpga(), StringComparison.Ordinal);
    }

    [Fact]
    public void ThePageCarriesTheFontsTheEngineAndTheInterface()
    {
        string page = Lpga();

        Assert.Equal(5, Regex.Matches(page, "@font-face").Count);
        Assert.Contains("TRAFFICLAB+ — SIMULATION CORE", page, StringComparison.Ordinal);
        Assert.Contains("TRAFFICLAB+ — USER INTERFACE", page, StringComparison.Ordinal);
        Assert.Contains("<title>LPGA Traffic Lab</title>", page, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryPageSaysWhatMadeIt()
    {
        Assert.Matches("<meta name=\"generator\" content=\"TrafficLabPlus \\d+\\.\\d+\\.\\d+\">", Lpga());
    }

    [Fact]
    public void ThePageOpensBlankAsAPuzzle()
    {
        // Ron, 2026-10-09: no plan is ever stored in a published page.
        string page = Lpga();
        JsonObject study = StudyIn(page);

        Assert.Null(study["plan"]);
        Assert.Contains("plan: TG.emptyPlan()", page, StringComparison.Ordinal);
    }

    [Fact]
    public void TheNameIsOptionalAndThePageSaysSo()
    {
        string page = Lpga();

        Assert.Contains("Your name — optional", page, StringComparison.Ordinal);
        Assert.Contains("Name not given", page, StringComparison.Ordinal);
    }

    [Fact]
    public void AStudyCannotBreakOutOfItsScript()
    {
        JsonObject study = JsonNode.Parse(PageBuilder.LpgaExampleJson())!.AsObject();
        study["title"] = "</script><script>alert(1)</script>";
        study["links"]![0]!["name"] = "</script>";

        string page = PageBuilder.Build(study.ToJsonString());

        Assert.Single(Regex.Matches(page, "<script type=\"application/json\""));
        Assert.DoesNotContain("<script>alert", page, StringComparison.Ordinal);
        Assert.Contains("&lt;/script&gt;", page, StringComparison.Ordinal);                  // the <title>
        Assert.Equal("</script>", StudyIn(page)["links"]![0]!["name"]!.GetValue<string>());   // read back intact
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1, 2, 3]")]
    public void SomethingThatIsNotAStudySaysSo(string text)
    {
        var ex = Assert.Throws<StudyFormatException>(() => PageBuilder.Build(text));
        Assert.StartsWith("This file is not a TrafficLab+ study", ex.Message, StringComparison.Ordinal);
    }

    private static JsonObject StudyIn(string page)
    {
        Match m = StudyScript().Match(page);
        Assert.True(m.Success);
        return JsonNode.Parse(m.Groups[1].Value)!.AsObject();
    }

    [GeneratedRegex("""\b(?:src|href)\s*=\s*["']?(?:https?:|//)""", RegexOptions.IgnoreCase)]
    private static partial Regex SrcOrHref();

    [GeneratedRegex("""url\(\s*["']?([^)"']+)""")]
    private static partial Regex CssUrl();

    [GeneratedRegex("""<script type="application/json" id="study">(.*?)</script>""", RegexOptions.Singleline)]
    private static partial Regex StudyScript();
}
