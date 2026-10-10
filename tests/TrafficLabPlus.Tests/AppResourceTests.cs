using System.Text.RegularExpressions;

namespace TrafficLabPlus.Tests;

/// <summary>A window that asks for a resource App.xaml does not define closes the program (the token
/// window did, 2026-10-10: OkBrush and WarnBrush came over from Gamify+ without their colours).</summary>
public class AppResourceTests
{
    [Fact]
    public void Every_resource_the_app_asks_for_is_defined()
    {
        string app = Path.Combine(Tools.RepoRoot, "src", "TrafficLabPlus.App");
        var defined = Directory.EnumerateFiles(app, "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), "x:Key=\"([A-Za-z]+)\"").Select(m => m.Groups[1].Value))
            .ToHashSet();

        var missing = new SortedSet<string>();
        foreach (string f in Directory.EnumerateFiles(app, "*.*", SearchOption.AllDirectories)
                     .Where(f => (f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".xaml", StringComparison.Ordinal))
                                 && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                                 && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            string text = File.ReadAllText(f);
            // {StaticResource X}, {DynamicResource X}, and any quoted "...Brush" / "...Style" key in code
            foreach (Match m in Regex.Matches(text, @"(?:Static|Dynamic)Resource\s+([A-Za-z]+)\}|""([A-Za-z]+(?:Brush|Style))"""))
            {
                string key = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
                if (!defined.Contains(key) && key != "BorderBrush")
                {
                    missing.Add($"{key} ({Path.GetFileName(f)})");
                }
            }
        }

        Assert.Empty(missing);
    }
}
