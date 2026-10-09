using TrafficLabPlus.Core.Site;

namespace TrafficLabPlus.Core.Publish;

public sealed class PublishRequest
{
    public required string SiteFolder { get; init; }

    /// <summary>GitHub account or organisation. Blank means "whoever the token belongs to".</summary>
    public string Owner { get; init; } = string.Empty;

    /// <summary>The repository name: <see cref="SiteMarks.DefaultRepository"/> unless the student chose another.</summary>
    public required string Repository { get; init; }

    public string Branch { get; init; } = "main";

    public string CommitMessage { get; init; } = "Publish the traffic studies";

    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Make the repository private. It does not make the published site private — see
    /// <see cref="PublishWords.PrivateDoesNotMeanHidden"/>, which is what the teacher is told.
    /// </summary>
    public bool Private { get; init; }

    /// <summary>Create the repository when it is not there yet (token publishing only).</summary>
    public bool CreateIfMissing { get; init; } = true;

    public bool EnablePages { get; init; } = true;

    /// <summary>
    /// Where TrafficLab+ last published on this computer, or empty. A repository that already holds
    /// something, is not that, and does not carry the TrafficLab+ topic is somebody else's — and a
    /// publish is a force-push that would replace it.
    /// </summary>
    public string KnownAddress { get; init; } = string.Empty;

    /// <summary>
    /// Asked before replacing a repository TrafficLab+ did not make. Given the repository's
    /// address; true replaces it. Null refuses — nothing is overwritten without a yes.
    /// </summary>
    public Func<string, bool>? ConfirmReplace { get; init; }
}

public sealed class PublishResult
{
    /// <summary>The account the site went to — found from the token when none was typed.</summary>
    public string Owner { get; set; } = string.Empty;

    public string RepositoryUrl { get; set; } = string.Empty;

    /// <summary>The address players open.</summary>
    public string PagesUrl { get; set; } = string.Empty;

    /// <summary>"Git push" or "GitHub API".</summary>
    public string Method { get; set; } = string.Empty;

    public bool RepositoryCreated { get; set; }

    public bool PagesEnabled { get; set; }

    /// <summary>What the teacher still has to do by hand, if anything.</summary>
    public List<string> NextSteps { get; } = [];
}

/// <summary>A thing worth knowing before a site is pushed anywhere.</summary>
public sealed record PublishWarning(string Message, bool Blocking);

/// <summary>
/// Puts the built studies site on GitHub Pages (plan phase 7, ported from Gamify+ and LMS-2-Website). Two routes,
/// and the app prefers the first:
///
/// 1. <b>With a token</b> — the app finds or creates the repository, pushes the site (with Git
///    when it is installed, otherwise straight through the API), and switches Pages on. Nothing
///    to do on github.com.
/// 2. <b>Without a token</b> — a plain <c>git push</c> to a repository the teacher has already
///    made, signed in through Git Credential Manager. Pages is then switched on by hand, and the
///    result says exactly how.
///
/// A publish is a force-push: whatever was on the branch is replaced by the site — which is what
/// makes a study marked "Don't publish" disappear from it. That is why a repository that holds
/// something and is not marked as TrafficLab+'s (its topic, or where it last published) is never
/// replaced without a yes.
/// </summary>
public static class Publisher
{
    /// <summary>GitHub rejects a file over 100 MB outright and complains over 50 MB.</summary>
    private const long HardFileLimit = 100L * 1024 * 1024;

    private const long SoftFileLimit = 50L * 1024 * 1024;

    /// <summary>GitHub Pages will not serve a site built from a repository over 1 GB.</summary>
    private const long SiteLimit = 1024L * 1024 * 1024;

    /// <summary>Above this, uploading file by file through the API is too slow to be sensible.</summary>
    private const long ApiUploadLimit = 40L * 1024 * 1024;

    /// <summary>Every file under the site folder a publish will send: never anything inside .git.</summary>
    public static IEnumerable<string> PublishableFiles(string siteFolder)
    {
        if (!Directory.Exists(siteFolder))
        {
            yield break;
        }

        foreach (string file in Directory.EnumerateFiles(siteFolder, "*", SearchOption.AllDirectories))
        {
            if (!file.Replace('\\', '/').Contains("/.git/", StringComparison.Ordinal))
            {
                yield return file;
            }
        }
    }

    /// <summary>What GitHub will object to, checked before anything is sent.</summary>
    public static IReadOnlyList<PublishWarning> Preflight(string siteFolder)
    {
        List<PublishWarning> warnings = [];

        if (!File.Exists(Path.Combine(siteFolder, "index.html")))
        {
            return [new PublishWarning("There is nothing to publish: no study is ticked.", true)];
        }

        long total = 0;

        foreach (string file in PublishableFiles(siteFolder))
        {
            long length = new FileInfo(file).Length;
            total += length;
            string name = Path.GetRelativePath(siteFolder, file);

            if (length > HardFileLimit)
            {
                warnings.Add(new PublishWarning($"{name} is {FileSize(length)} — GitHub refuses any file over 100 MB.", true));
            }
            else if (length > SoftFileLimit)
            {
                warnings.Add(new PublishWarning($"{name} is {FileSize(length)} — GitHub warns above 50 MB but will take it.", false));
            }
        }

        if (total > SiteLimit)
        {
            warnings.Add(new PublishWarning(
                $"The site is {FileSize(total)}. GitHub Pages does not serve sites built from a repository over 1 GB.", true));
        }

        return warnings;
    }

    public static long SiteSize(string siteFolder) =>
        PublishableFiles(siteFolder).Sum(f => new FileInfo(f).Length);

    /// <summary>The address GitHub Pages serves a repository at, before GitHub has said so itself.</summary>
    public static string PagesAddress(string owner, string repository) =>
        $"https://{owner.Trim().ToLowerInvariant()}.github.io/{repository.Trim()}/";

    public static async Task<PublishResult> PublishAsync(
        PublishRequest request, string? token, Action<string> log,
        IProgress<(int Percent, string Message)>? progress = null, CancellationToken ct = default,
        HttpClient? http = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(log);

        foreach (PublishWarning blocker in Preflight(request.SiteFolder).Where(w => w.Blocking))
        {
            throw new InvalidOperationException(blocker.Message);
        }

        // A name GitHub would rewrite — slashes, spaces, a whole address — is refused here, in
        // words, instead of GitHub quietly making a repository with a mangled name (Ron,
        // 2026-09-24: it made GP-https-github.com-reaglin-GamifyPlus-Games).
        if (GitHubAddress.ProblemWithRepository(request.Repository) is string badName)
        {
            throw new InvalidOperationException("Not published: " + badName);
        }

        if (request.Owner.Trim().Length > 0 && GitHubAddress.ProblemWithAccount(request.Owner.Trim()) is string badOwner)
        {
            throw new InvalidOperationException("Not published: " + badOwner);
        }

        return string.IsNullOrWhiteSpace(token)
            ? await PublishWithGitOnlyAsync(request, log, ct).ConfigureAwait(false)
            : await PublishWithTokenAsync(request, token, log, progress, http, ct).ConfigureAwait(false);
    }

    // ── with a token ──────────────────────────────────────────────────────────

    private static async Task<PublishResult> PublishWithTokenAsync(
        PublishRequest request, string token, Action<string> log,
        IProgress<(int Percent, string Message)>? progress, HttpClient? http, CancellationToken ct)
    {
        var result = new PublishResult();

        // A client can be handed in so the tests can answer as GitHub would; the app never does.
        using var api = new GitHubApi(token, http);

        progress?.Report((2, "Checking the token"));
        GitHubUser user = await api.GetUserAsync(ct).ConfigureAwait(false);
        string owner = string.IsNullOrWhiteSpace(request.Owner) ? user.Login : request.Owner.Trim();
        result.Owner = owner;
        log($"Signed in to GitHub as {user.Login}.");

        progress?.Report((5, "Looking for the repository"));
        GitHubRepo? repo = await api.FindRepoAsync(owner, request.Repository, ct).ConfigureAwait(false);

        if (repo is null)
        {
            if (!request.CreateIfMissing)
            {
                throw new InvalidOperationException($"{owner}/{request.Repository} does not exist.");
            }

            if (!owner.Equals(user.Login, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"{owner}/{request.Repository} does not exist, and TrafficLab+ can only create repositories under " +
                    $"your own account ({user.Login}). Make it on github.com first, then publish again.");
            }

            log($"Creating {owner}/{request.Repository}…");
            repo = await api.CreateRepoAsync(request.Repository, Describe(request.Description), request.Private, ct).ConfigureAwait(false);
            result.RepositoryCreated = true;
        }

        result.RepositoryUrl = $"https://github.com/{owner}/{repo.Name}";

        if (!result.RepositoryCreated && !repo.Empty && !IsOwnSite(request, owner, repo.Name)
            && !await api.HasTopicAsync(owner, repo.Name, SiteMarks.Topic, ct).ConfigureAwait(false))
        {
            ConfirmOrStop(request, result.RepositoryUrl);
        }

        // Mark it as a generated site. Worth saying out loud but never worth failing a publish.
        try
        {
            await api.EnsureTopicAsync(owner, repo.Name, SiteMarks.Topic, ct).ConfigureAwait(false);
        }
        catch (GitHubException ex)
        {
            log($"Could not put the \"{SiteMarks.Topic}\" topic on the repository: {ex.Message}");
        }

        (bool gitInstalled, string version) = await GitCli.CheckInstalledAsync(ct).ConfigureAwait(false);
        long size = SiteSize(request.SiteFolder);

        if (gitInstalled)
        {
            log($"Pushing with {version}.");
            result.Method = "Git push";
            progress?.Report((10, "Pushing to GitHub"));
            await GitCli.PublishAsync(request.SiteFolder, GitCli.WithToken(repo.CloneUrl, token),
                request.Branch, request.CommitMessage, log, ct).ConfigureAwait(false);
        }
        else if (size <= ApiUploadLimit)
        {
            log("Git is not installed — uploading through the GitHub API instead.");
            result.Method = "GitHub API";
            await api.UploadFolderAsync(owner, repo.Name, request.Branch, request.SiteFolder,
                request.CommitMessage, progress, ct).ConfigureAwait(false);
        }
        else
        {
            throw new InvalidOperationException(
                $"The site is {FileSize(size)}, which is too much to upload file by file, and Git is not installed. " +
                "Install Git for Windows (https://git-scm.com/download/win) and publish again.");
        }

        if (request.EnablePages)
        {
            progress?.Report((97, "Switching GitHub Pages on"));

            try
            {
                PagesSite pages = await api.EnablePagesAsync(owner, repo.Name, request.Branch, ct).ConfigureAwait(false);
                result.PagesUrl = pages.Url;
                result.PagesEnabled = true;
                log($"GitHub Pages is serving {request.Branch} at {pages.Url} (status: {pages.Status}).");
                result.NextSteps.Add("The first time, GitHub takes a minute or two to put the site up — the address shows " +
                                     "a \"404\" page until it has finished. After that, each publish shows within a minute.");
            }
            catch (GitHubException ex)
            {
                // The site is pushed; only the switch failed. Say so rather than failing the publish.
                result.PagesUrl = PagesAddress(owner, repo.Name);
                log("The files are on GitHub, but Pages could not be switched on: " + ex.Message);
                result.NextSteps.Add($"Switch Pages on by hand: open {result.RepositoryUrl}/settings/pages (the repository's " +
                      "Settings tab → Pages); under Build and deployment set Source to " +
                      $"\"Deploy from a branch\", choose {request.Branch} and / (root), and press Save.");
            }
        }

        if (request.Private)
        {
            result.NextSteps.Add("The repository is private. " + PublishWords.PrivateDoesNotMeanHidden);
        }

        progress?.Report((100, "Published"));
        return result;
    }

    /// <summary>The repository description always says what made it.</summary>
    private static string Describe(string description)
    {
        string text = (description ?? string.Empty).Trim();

        if (text.Length == 0)
        {
            return $"Traffic studies. {SiteMarks.RepoDescription}.";
        }

        return text.Contains("TrafficLab+", StringComparison.OrdinalIgnoreCase)
            ? text
            : $"{text.TrimEnd('.', ' ')}. {SiteMarks.RepoDescription}.";
    }

    // ── without a token ───────────────────────────────────────────────────────

    private static async Task<PublishResult> PublishWithGitOnlyAsync(PublishRequest request, Action<string> log, CancellationToken ct)
    {
        (bool installed, string version) = await GitCli.CheckInstalledAsync(ct).ConfigureAwait(false);

        if (!installed)
        {
            throw new InvalidOperationException(
                "Publishing needs either a GitHub token or Git for Windows. The simplest is a token: press " +
                "GitHub token… and follow the steps. Or install Git from https://git-scm.com/download/win.");
        }

        string owner = request.Owner.Trim();

        if (owner.Length == 0)
        {
            throw new InvalidOperationException(
                "Without a token TrafficLab+ cannot tell which GitHub account to use. Type your GitHub user name in the " +
                "GitHub account box.");
        }

        string repoUrl = $"https://github.com/{owner}/{request.Repository}.git";
        log($"Pushing with {version}. If Windows asks you to sign in to GitHub, do so — that is Git Credential Manager.");

        var result = new PublishResult
        {
            Owner = owner,
            RepositoryUrl = $"https://github.com/{owner}/{request.Repository}",
            Method = "Git push",
            PagesUrl = PagesAddress(owner, request.Repository),
        };

        if (!IsOwnSite(request, owner, request.Repository)
            && await GitCli.RemoteHasBranchAsync(repoUrl, request.Branch, ct).ConfigureAwait(false))
        {
            ConfirmOrStop(request, result.RepositoryUrl);
        }

        await GitCli.PublishAsync(request.SiteFolder, repoUrl, request.Branch, request.CommitMessage, log, ct).ConfigureAwait(false);

        // Written for somebody who has not used GitHub before: one numbered pass, no jargon left
        // unexplained, and the address said plainly at the end.
        result.NextSteps.Add(
            "The files are on GitHub. One thing left, and only once — turn the website on:\n" +
            $"  1. Open {result.RepositoryUrl}/settings/pages — that is the repository's own Settings tab → Pages.\n" +
            "  2. Under \"Build and deployment\", set \"Source\" to \"Deploy from a branch\".\n" +
            $"  3. Set the branch to {request.Branch} and the folder to / (root), then press Save.\n" +
            $"  4. Wait a minute or two, then open {result.PagesUrl} — it shows a \"404\" page until the first build finishes.\n" +
            "Save a GitHub token (GitHub token…) and TrafficLab+ will do this part for you next time.");

        return result;
    }

    /// <summary>True when the repository is where TrafficLab+ last published from this computer.</summary>
    private static bool IsOwnSite(PublishRequest request, string owner, string repository) =>
        request.KnownAddress.Trim().Length > 0
        && string.Equals(request.KnownAddress.Trim().TrimEnd('/'), PagesAddress(owner, repository).TrimEnd('/'),
            StringComparison.OrdinalIgnoreCase);

    private static void ConfirmOrStop(PublishRequest request, string repositoryUrl)
    {
        if (request.ConfirmReplace?.Invoke(repositoryUrl) != true)
        {
            throw new InvalidOperationException(
                $"Not published: {repositoryUrl} already holds something that TrafficLab+ did not put there, " +
                "and it has been left alone. Choose a different repository name, then publish again.");
        }
    }

    /// <summary>"3.4 MB", for the size warnings.</summary>
    public static string FileSize(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024 * 1024):0.0} GB",
        >= 1024L * 1024 => $"{bytes / (1024.0 * 1024):0.0} MB",
        >= 1024 => $"{bytes / 1024.0:0} KB",
        _ => $"{bytes} bytes",
    };
}
