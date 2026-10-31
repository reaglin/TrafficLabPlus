using System.Text.RegularExpressions;

namespace TrafficLabPlus.Tests;

/// <summary>Help ▸ Help for this screen (F1) and the link under each step's guide open a page of TrafficLab+'s help on
/// SoftwarePlus.ai (UX review 2026-10-31). Each page they name must be one the help has.</summary>
public class HelpLinkTests
{
    private static readonly string Guides = Path.Combine(Tools.RepoRoot, "src", "TrafficLabPlus.App", "Views", "Guides.cs");

    private static List<string> Pages() =>
        [.. Regex.Matches(File.ReadAllText(Guides), "=> \"([a-z-]+\\.html)(?:#[a-z-]+)?\"").Select(m => m.Groups[1].Value).Distinct()];

    [Fact]
    public void Every_section_has_a_help_page()
    {
        string text = File.ReadAllText(Guides);
        foreach (string section in new[] { "Start", "Map", "Network", "Traffic", "Challenge", "Preview", "Publish", "Settings", "About" })
        {
            Assert.Contains($"\"{section}\" => \"", text, StringComparison.Ordinal);
        }

        Assert.Contains("https://softwareplus.ai/trafficlab/help/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_help_page_named_is_in_the_site()
    {
        // the help is written in the SoftwarePlus repository beside this one; a computer without it checks nothing more
        string help = Path.Combine(Tools.RepoRoot, "..", "SoftwarePlus", "site", "trafficlab", "help");
        if (!Directory.Exists(help))
        {
            return;
        }

        List<string> pages = Pages();
        Assert.NotEmpty(pages);
        Assert.All(pages, p => Assert.True(File.Exists(Path.Combine(help, p)), p + " is not in SoftwarePlus/site/trafficlab/help"));
    }
}
