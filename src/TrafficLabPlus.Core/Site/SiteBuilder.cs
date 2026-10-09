using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TrafficLabPlus.Core.Build;
using TrafficLabPlus.Core.Model;

namespace TrafficLabPlus.Core.Site;

/// <summary>The marks that say a folder and a repository were made by TrafficLab+ (the LMS-2-Website
/// rule: a publish is a force-push, so it must never land on a repository made for something else).</summary>
public static class SiteMarks
{
    /// <summary>The one repository every study goes into (Ron, 2026-10-09: "one repo").</summary>
    public const string DefaultRepository = "TrafficLab";

    /// <summary>The topic put on the repository, so a TrafficLab+ site can be recognised.</summary>
    public const string Topic = "trafficlab-plus";

    /// <summary>Written at the root of every built site: what is in it and what made it.</summary>
    public const string SiteFile = "trafficlab-site.json";

    public const string RepoDescription = "Made with TrafficLab+";
}

/// <summary>Wording the app and the publish log must agree on.</summary>
public static class PublishWords
{
    public const string PrivateDoesNotMeanHidden =
        "Making the repository private does not make the website private. A GitHub Pages site is public on the " +
        "internet even when its repository is private — the setting hides the files, not the site. On a free " +
        "account a private repository cannot publish a site at all.";

    public const string WhatPublishingDoes =
        "Publishing puts every ticked study on your GitHub Pages website, each on its own page, with a " +
        "summary page linking to them all. Anyone with the address can open it. Your study files, notes and AI " +
        "settings stay on this computer.";
}

/// <summary>One study going onto the site: the study and the folder name its page lives in.</summary>
public sealed record SiteStudy(Study Study, string Slug);

/// <summary>
/// The website TrafficLab+ publishes (D15, Ron 2026-10-09): one folder per study holding its page,
/// and a summary page as the index with a card for each study linking to it. Built whole from the
/// studies chosen, every time, so a study left out is gone from the site.
/// </summary>
public static class SiteBuilder
{
    /// <summary>A folder name for a study: its file name, lower case, letters, digits and hyphens.</summary>
    public static string Slug(string name, ISet<string>? taken = null)
    {
        var sb = new StringBuilder();
        foreach (char c in name.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                sb.Append(c);
            }
            else if (sb.Length > 0 && sb[^1] != '-')
            {
                sb.Append('-');
            }
        }

        string slug = sb.ToString().Trim('-');
        if (slug.Length == 0)
        {
            slug = "study";
        }

        if (slug.Length > 60)
        {
            slug = slug[..60].Trim('-');
        }

        string unique = slug;
        for (int i = 2; taken is not null && taken.Contains(unique); i++)
        {
            unique = slug + "-" + i.ToString(CultureInfo.InvariantCulture);
        }

        taken?.Add(unique);
        return unique;
    }

    /// <summary>Builds the site into <paramref name="folder"/>, replacing what was there (a .git folder is kept).</summary>
    public static void Build(string folder, string siteTitle, IReadOnlyList<SiteStudy> studies, DateTime when)
    {
        Directory.CreateDirectory(folder);
        foreach (string dir in Directory.GetDirectories(folder).Where(d => Path.GetFileName(d) != ".git"))
        {
            Directory.Delete(dir, recursive: true);
        }

        foreach (string file in Directory.GetFiles(folder))
        {
            File.Delete(file);
        }

        foreach (SiteStudy s in studies)
        {
            string dir = Path.Combine(folder, s.Slug);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "index.html"), PageBuilder.Build(s.Study));
        }

        File.WriteAllText(Path.Combine(folder, "index.html"), Index(siteTitle, studies, when));
        File.WriteAllText(Path.Combine(folder, ".nojekyll"), "");   // GitHub Pages serves the files as they are
        var marks = new JsonObject
        {
            ["generator"] = PageBuilder.Generator,
            ["version"] = typeof(SiteBuilder).Assembly.GetName().Version?.ToString(3),
            ["published"] = when.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["studies"] = new JsonArray(studies.Select(s => (JsonNode)new JsonObject { ["slug"] = s.Slug, ["title"] = s.Study.Title }).ToArray()),
        };
        File.WriteAllText(Path.Combine(folder, SiteMarks.SiteFile), marks.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>The summary page: one card per study, with what it is and a link to it. Self-contained
    /// like the study pages: no fonts, scripts or pictures from anywhere.</summary>
    public static string Index(string siteTitle, IReadOnlyList<SiteStudy> studies, DateTime when)
    {
        // only what HTML needs: "·" and "—" stay readable in the page's source
        static string H(string? s) => (s ?? "").Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal);
        var cards = new StringBuilder();
        foreach (SiteStudy s in studies)
        {
            Study st = s.Study;
            int signals = st.Nodes.Count(n => n.Type != StudyNode.End);
            string budget = st.Budget >= 1 ? "$" + st.Budget.ToString("0.##", CultureInfo.InvariantCulture) + "M" : "$" + (st.Budget * 1000).ToString("0", CultureInfo.InvariantCulture) + "K";
            string facts = string.Join(" · ", new[]
            {
                st.Place,
                signals == 1 ? "one intersection" : $"{signals} intersections",
                budget + " budget",
            }.Where(x => !string.IsNullOrWhiteSpace(x)));
            string about = !string.IsNullOrWhiteSpace(st.Subtitle) ? st.Subtitle! : !string.IsNullOrWhiteSpace(st.Intro) ? st.Intro! : $"Fix this network on a {budget} budget.";
            if (about.Length > 160)
            {
                about = about[..157].TrimEnd() + "…";
            }
            cards.Append(CultureInfo.InvariantCulture, $"""
                    <a class="card" href="{H(s.Slug)}/">
                      <span class="sign">{H(string.IsNullOrWhiteSpace(st.Short) ? "TL+" : st.Short)}</span>
                      <span class="body"><b>{H(st.Title)}</b><span class="facts">{H(facts)}</span><span class="about">{H(about)}</span><span class="go">Open the study →</span></span>
                    </a>

                """);
        }

        bool osm = studies.Any(s => s.Study.Sources?.Osm == true);
        string title = string.IsNullOrWhiteSpace(siteTitle) ? "Traffic studies" : siteTitle.Trim();
        string empty = studies.Count == 0 ? "<p>No studies are published here yet.</p>" : "";
        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="generator" content="{{PageBuilder.Generator}}">
            <title>{{H(title)}}</title>
            <style>
            :root { --bg: #eef1ef; --panel: #fff; --ink: #17201c; --muted: #5d6b65; --line: #d3dad6; --sign: #0f6b4a; --sign-ink: #fff; }
            @media (prefers-color-scheme: dark) { :root { --bg: #131917; --panel: #1b2320; --ink: #e5ece8; --muted: #93a39c; --line: #2c3833; --sign: #2f9b72; --sign-ink: #06110c; } }
            * { box-sizing: border-box; }
            body { margin: 0; background: var(--bg); color: var(--ink); font: 16px/1.5 system-ui, "Segoe UI", Roboto, sans-serif; }
            header { background: var(--sign); color: var(--sign-ink); padding: 28px 16px; }
            header .wrap, main, footer { max-width: 960px; margin: 0 auto; }
            h1 { margin: 0 0 4px; font-size: 28px; }
            header p { margin: 0; opacity: .9; }
            main { padding: 20px 16px; display: grid; gap: 14px; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); }
            .card { display: flex; gap: 14px; background: var(--panel); border: 1px solid var(--line); border-radius: 8px; padding: 16px; color: inherit; text-decoration: none; }
            .card:hover, .card:focus { border-color: var(--sign); outline: none; box-shadow: 0 0 0 2px var(--sign); }
            .sign { flex: none; min-width: 56px; height: 40px; padding: 0 8px; display: grid; place-items: center; background: var(--sign); color: var(--sign-ink); border-radius: 6px; font-weight: 800; letter-spacing: .5px; }
            .body { display: grid; gap: 4px; }
            .facts, .about { color: var(--muted); font-size: 14px; }
            .go { color: var(--sign); font-weight: 600; font-size: 14px; }
            footer { padding: 8px 16px 32px; color: var(--muted); font-size: 13px; }
            </style>
            </head>
            <body>
            <header><div class="wrap"><h1>{{H(title)}}</h1><p>Live traffic simulations. Open one, build a plan to fix it within its budget, test it, and submit it for a score.</p></div></header>
            <main>
            {{empty}}{{cards}}</main>
            <footer>Published {{when.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture)}} · Made with TrafficLab+{{(osm ? " · Road data © OpenStreetMap contributors" : "")}}</footer>
            </body>
            </html>
            """;
    }
}
