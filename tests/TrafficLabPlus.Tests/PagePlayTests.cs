using System.Text.Json;
using TrafficLabPlus.Core.Build;

namespace TrafficLabPlus.Tests;

/// <summary>
/// A page is HTML and JavaScript, so the honest check is to use it in a browser: tools/play-page.js
/// opens it in headless Chrome, starts it, buys a turn lane, submits the plan and reads the printout.
/// Skipped (passes with nothing checked) on a machine with no Chrome or Edge.
/// </summary>
public class PagePlayTests
{
    [Theory]
    [InlineData("Maria Gomez", "Name: Maria Gomez", "Maria Gomez")]
    [InlineData("-", "Add your name (optional)", "Name not given")]
    public void AVisitorCanPlayTheLpgaPageAndSubmitAPlan(string name, string who, string printedName)
    {
        if (!Tools.HasBrowser())
        {
            return;
        }

        string file = Path.Combine(Path.GetTempPath(), "tl-play-" + Guid.NewGuid().ToString("N") + ".html");
        File.WriteAllText(file, PageBuilder.Build(PageBuilder.LpgaExampleJson()));
        try
        {
            (int exit, string output) = Tools.Node("tools/play-page.js", file, name);
            Assert.True(exit == 0, output);

            using JsonDocument doc = JsonDocument.Parse(output.Trim().Split('\n')[^1]);
            JsonElement r = doc.RootElement;

            Assert.True(r.GetProperty("introOpen").GetBoolean(), "the intro shows first");
            Assert.True(r.GetProperty("introClosed").GetBoolean(), "Start closes the intro, name or no name");
            Assert.StartsWith(who, r.GetProperty("who").GetString()!.Trim(), StringComparison.Ordinal);
            Assert.Equal(1, r.GetProperty("planItems").GetInt32());
            Assert.True(r.GetProperty("printoutOpen").GetBoolean(), "Submit plan opens the printout");
            Assert.Contains(printedName, r.GetProperty("printout").GetString(), StringComparison.Ordinal);
            Assert.Contains("Plan — $0.", r.GetProperty("printout").GetString(), StringComparison.Ordinal);
            Assert.Contains("Submitted plan", r.GetProperty("printArea").GetString(), StringComparison.Ordinal);   // what reaches paper
            Assert.InRange(r.GetProperty("score").GetInt32(), 1, 100);
            Assert.Equal(18, r.GetProperty("approachRows").GetInt32());
            Assert.Equal(0, r.GetProperty("requests").GetArrayLength());
            Assert.Equal(0, r.GetProperty("errors").GetArrayLength());
            Assert.False(r.GetProperty("sideways").GetBoolean());
        }
        finally
        {
            File.Delete(file);
        }
    }
}
