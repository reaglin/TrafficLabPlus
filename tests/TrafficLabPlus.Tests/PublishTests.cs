using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using TrafficLabPlus.Core.Build;
using TrafficLabPlus.Core.Model;
using TrafficLabPlus.Core.Publish;
using TrafficLabPlus.Core.Site;

namespace TrafficLabPlus.Tests;

/// <summary>
/// Publishing (D15, Ron 2026-10-09): one GitHub Pages repository, every study not marked "Don't
/// publish", a summary page as the index. Nothing here reaches github.com: Git pushes to a repository
/// in a temporary folder, and the API talks to a fake that answers as GitHub does.
/// </summary>
public sealed partial class PublishTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "tl-publish-" + Guid.NewGuid().ToString("N"));

    public PublishTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            foreach (string file in Directory.EnumerateFiles(_folder, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);   // Git marks its objects read-only
            }

            Directory.Delete(_folder, recursive: true);
        }
    }

    private static List<SiteStudy> Two() =>
    [
        new(StudyJson.Read(PageBuilder.LpgaExampleJson()), "lpga-traffic-lab"),
        new(Examples.All.Single(e => e.Id == "four-way").Study(), "four-way"),
    ];

    private string BuiltSite(List<SiteStudy>? studies = null)
    {
        string site = Path.Combine(_folder, "site");
        SiteBuilder.Build(site, "Maria Gomez — traffic studies", studies ?? Two(), new DateTime(2026, 10, 10));
        return site;
    }

    // ------------------------------------------------------------ the site

    [Fact]
    public void EveryStudyGetsItsOwnPageAndTheIndexLinksToEach()
    {
        string site = BuiltSite();

        Assert.True(File.Exists(Path.Combine(site, "lpga-traffic-lab", "index.html")));
        Assert.True(File.Exists(Path.Combine(site, "four-way", "index.html")));
        Assert.True(File.Exists(Path.Combine(site, ".nojekyll")));
        string index = File.ReadAllText(Path.Combine(site, "index.html"));
        Assert.Contains("href=\"lpga-traffic-lab/\"", index, StringComparison.Ordinal);
        Assert.Contains("href=\"four-way/\"", index, StringComparison.Ordinal);
        Assert.Contains("Maria Gomez — traffic studies", index, StringComparison.Ordinal);
        Assert.Contains("Daytona Beach, Florida · 5 intersections · $5M budget", index, StringComparison.Ordinal);
        Assert.Contains("one intersection · $2M budget", index, StringComparison.Ordinal);
        Assert.Contains("generator\" content=\"TrafficLabPlus", index, StringComparison.Ordinal);
    }

    [Fact]
    public void TheIndexNeedsNothingFromTheInternet()
    {
        string index = File.ReadAllText(Path.Combine(BuiltSite(), "index.html"));

        Assert.DoesNotMatch(Remote(), index);
        Assert.DoesNotContain("<script", index, StringComparison.OrdinalIgnoreCase);
    }

    // a load: an src, or a <link>'s href (a stylesheet). An <a> the reader clicks is not one.
    [GeneratedRegex("""(src\s*=|<link[^>]*href\s*=)\s*["']?(https?:)?//""", RegexOptions.IgnoreCase)]
    private static partial Regex Remote();

    [Fact]
    public void TheIndexSaysItWasCreatedWithTrafficLabAndLinksToSoftwarePlus()
    {
        string index = File.ReadAllText(Path.Combine(BuiltSite(), "index.html"));
        string footer = index[index.IndexOf("<footer>", StringComparison.Ordinal)..];

        Assert.Contains("Created with <a href=\"https://softwareplus.ai/trafficlab/\">TrafficLab+</a>", footer, StringComparison.Ordinal);
        Assert.Contains("<a href=\"https://softwareplus.ai/\">SoftwarePlus.ai</a>", footer, StringComparison.Ordinal);
    }

    [Fact]
    public void AStudyLeftOutIsGoneFromTheSiteAndGitIsKept()
    {
        string site = BuiltSite();
        Directory.CreateDirectory(Path.Combine(site, ".git"));
        File.WriteAllText(Path.Combine(site, ".git", "HEAD"), "ref: refs/heads/main");

        BuiltSite([Two()[0]]);

        Assert.False(Directory.Exists(Path.Combine(site, "four-way")));
        Assert.DoesNotContain("four-way", File.ReadAllText(Path.Combine(site, "index.html")), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(site, ".git", "HEAD")));
        Assert.Contains("\"slug\": \"lpga-traffic-lab\"", File.ReadAllText(Path.Combine(site, SiteMarks.SiteFile)), StringComparison.Ordinal);
    }

    [Fact]
    public void FolderNamesAreSafeAndNeverTwiceTheSame()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Assert.Equal("lpga-traffic-lab", SiteBuilder.Slug("LPGA Traffic Lab", taken));
        Assert.Equal("lpga-traffic-lab-2", SiteBuilder.Slug("LPGA  Traffic—Lab!", taken));
        Assert.Equal("study", SiteBuilder.Slug("???", taken));
    }

    [Fact]
    public void NothingIsPublishedWhenNoStudyIsTicked()
    {
        IReadOnlyList<PublishWarning> warnings = Publisher.Preflight(Path.Combine(_folder, "nothing"));

        Assert.Contains(warnings, w => w.Blocking && w.Message.Contains("no study", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------ to GitHub

    [Fact]
    public async Task GitPushesTheWholeSiteToTheRepository()
    {
        (bool installed, _) = await GitCli.CheckInstalledAsync();
        if (!installed)
        {
            return;   // no Git on this machine; the token route through the API is tested below
        }

        string site = BuiltSite();
        string remote = Path.Combine(_folder, "remote.git");
        Git(_folder, $"init --bare \"{remote}\"");

        await GitCli.PublishAsync(site, remote, "main", "Publish the traffic studies", _ => { });

        string files = Git(_folder, $"--git-dir=\"{remote}\" ls-tree -r --name-only main");
        Assert.Contains("index.html", files, StringComparison.Ordinal);
        Assert.Contains("four-way/index.html", files, StringComparison.Ordinal);
        Assert.Contains(SiteMarks.SiteFile, files, StringComparison.Ordinal);
        await GitCli.PublishAsync(site, remote, "main", "Publish the traffic studies", _ => { });   // again: not an error
    }

    [Fact]
    public async Task TheApiFindsTheAccountCreatesTheRepositoryAndSwitchesPagesOn()
    {
        var github = new FakeGitHub();
        using var api = new GitHubApi("github_pat_test", new HttpClient(github));

        GitHubUser user = await api.GetUserAsync();
        GitHubRepo created = await api.CreateRepoAsync(SiteMarks.DefaultRepository, "Traffic studies. Made with TrafficLab+.", isPrivate: false);
        PagesSite pages = await api.EnablePagesAsync(user.Login, created.Name, "main");

        Assert.Equal("https://reaglin.github.io/TrafficLab/", pages.Url);
        Assert.Contains("Bearer github_pat_test", github.Authorization, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARepositoryTrafficLabDidNotMakeIsNeverReplacedWithoutAYes()
    {
        bool asked = false;
        var request = new PublishRequest
        {
            SiteFolder = BuiltSite(),
            Repository = "Taken",
            ConfirmReplace = url =>
            {
                asked = url.EndsWith("/Taken", StringComparison.Ordinal);
                return false;
            },
        };

        InvalidOperationException stopped = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Publisher.PublishAsync(request, "github_pat_test", _ => { }, http: new HttpClient(new FakeGitHub())));

        Assert.True(asked);
        Assert.Contains("left alone", stopped.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARepositoryMarkedAsTrafficLabsIsNotAskedAbout()
    {
        var github = new FakeGitHub();
        using var api = new GitHubApi("github_pat_test", new HttpClient(github));

        Assert.True(await api.HasTopicAsync("reaglin", "Marked", SiteMarks.Topic));
        Assert.False(await api.HasTopicAsync("reaglin", "Taken", SiteMarks.Topic));
    }

    private static string Git(string folder, string args)
    {
        var info = new ProcessStartInfo("git", args) { WorkingDirectory = folder, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        using Process process = Process.Start(info)!;
        string output = process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        return output;
    }

    /// <summary>Answers the calls the publisher makes, the way GitHub does.</summary>
    private sealed class FakeGitHub : HttpMessageHandler
    {
        public string Authorization { get; private set; } = "";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString() ?? "";
            return (request.Method.Method, request.RequestUri!.AbsolutePath) switch
            {
                ("GET", "/user") => Answer(HttpStatusCode.OK, """{"login":"reaglin","name":"Ron"}"""),
                ("GET", "/repos/reaglin/TrafficLab") => Answer(HttpStatusCode.NotFound, """{"message":"Not Found"}"""),
                ("GET", "/repos/reaglin/Taken") => Answer(HttpStatusCode.OK, """{"name":"Taken","default_branch":"main","clone_url":"https://github.com/reaglin/Taken.git","size":120}"""),
                ("GET", "/repos/reaglin/Taken/topics") => Answer(HttpStatusCode.OK, """{"names":["homework"]}"""),
                ("GET", "/repos/reaglin/Marked/topics") => Answer(HttpStatusCode.OK, """{"names":["trafficlab-plus"]}"""),
                ("POST", "/user/repos") => Answer(HttpStatusCode.Created, """{"name":"TrafficLab","owner":{"login":"reaglin"},"default_branch":"main","clone_url":"https://github.com/reaglin/TrafficLab.git"}"""),
                ("POST", "/repos/reaglin/TrafficLab/pages") => Answer(HttpStatusCode.Created, "{}"),
                ("GET", "/repos/reaglin/TrafficLab/pages") => Answer(HttpStatusCode.OK, """{"html_url":"https://reaglin.github.io/TrafficLab/","status":"building","source":{"branch":"main"}}"""),
                _ => Answer(HttpStatusCode.NotFound, """{"message":"Not Found"}"""),
            };
        }

        private static Task<HttpResponseMessage> Answer(HttpStatusCode status, string json) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }
}
