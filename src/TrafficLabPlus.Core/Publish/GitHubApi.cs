using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace TrafficLabPlus.Core.Publish;

/// <summary>A GitHub call that came back with something other than success.</summary>
public sealed class GitHubException : Exception
{
    public HttpStatusCode Status { get; }
    public GitHubException(HttpStatusCode status, string message) : base(message) => Status = status;
}

public sealed record GitHubUser(string Login, string Name);
public sealed record GitHubRepo(string Owner, string Name, string DefaultBranch, string CloneUrl, bool Empty);
public sealed record PagesSite(string Url, string Status, string Branch);

/// <summary>
/// The slice of the GitHub REST API this app needs: who the token belongs to, does the repository
/// exist, create it, upload a folder as one commit, and switch GitHub Pages on.
///
/// The token is sent as a bearer token and never written anywhere by this class
/// (<see cref="TokenStore"/> owns storage).
/// </summary>
public sealed class GitHubApi : IDisposable
{
    private readonly HttpClient _http;

    public GitHubApi(string token, HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
        _http.BaseAddress ??= new Uri("https://api.github.com/");
        // No token is allowed: public repositories and Pages sites can be looked at without one
        // (at GitHub's lower anonymous rate), which is how the Export section can say whether a
        // website is already there before anything is saved.
        if (!string.IsNullOrWhiteSpace(token))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        }
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("TrafficLabPlus");
        _http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    // ── account ───────────────────────────────────────────────────────────────

    public async Task<GitHubUser> GetUserAsync(CancellationToken ct = default)
    {
        using var response = await _http.GetAsync("user", ct);
        var json = await ReadAsync(response, "check the token", ct);
        return new GitHubUser(
            json.GetProperty("login").GetString() ?? string.Empty,
            json.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString()! : string.Empty);
    }

    // ── repository ────────────────────────────────────────────────────────────

    public async Task<GitHubRepo?> FindRepoAsync(string owner, string name, CancellationToken ct = default)
    {
        using var response = await _http.GetAsync($"repos/{owner}/{name}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        var json = await ReadAsync(response, $"look up {owner}/{name}", ct);
        var branch = json.GetProperty("default_branch").GetString() ?? "main";
        var repo = new GitHubRepo(owner, name, branch,
            json.GetProperty("clone_url").GetString() ?? $"https://github.com/{owner}/{name}.git",
            Empty: json.TryGetProperty("size", out var size) && size.GetInt64() == 0);
        return repo;
    }

    /// <summary>Creates a repository for the signed-in user (not an organisation).</summary>
    public async Task<GitHubRepo> CreateRepoAsync(string name, string description, bool isPrivate, CancellationToken ct = default)
    {
        using var response = await _http.PostAsJsonAsync("user/repos", new
        {
            name,
            description = Trim(description, 350),
            @private = isPrivate,
            auto_init = false,
            has_issues = false,
            has_wiki = false
        }, ct);
        var json = await ReadAsync(response, $"create the repository {name}", ct);
        return new GitHubRepo(
            json.GetProperty("owner").GetProperty("login").GetString() ?? string.Empty,
            json.GetProperty("name").GetString() ?? name,
            json.GetProperty("default_branch").GetString() ?? "main",
            json.GetProperty("clone_url").GetString() ?? string.Empty,
            Empty: true);
    }

    // ── one commit that contains the whole site ───────────────────────────────

    /// <summary>
    /// Uploads every file under <paramref name="folder"/> as a single commit on
    /// <paramref name="branch"/>, replacing whatever was there. Used when Git is not installed;
    /// each file is sent as its own blob, so a very large site is better served by the Git push.
    /// </summary>
    public async Task UploadFolderAsync(
        string owner, string repo, string branch, string folder, string message,
        IProgress<(int Percent, string Message)>? progress = null, CancellationToken ct = default)
    {
        var files = Publisher.PublishableFiles(folder).ToList();

        var tree = new List<object>(files.Count);
        for (int i = 0; i < files.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(folder, files[i]).Replace('\\', '/');
            progress?.Report((5 + 80 * i / Math.Max(1, files.Count), "Uploading " + relative));

            var sha = await CreateBlobAsync(owner, repo, await File.ReadAllBytesAsync(files[i], ct), ct);
            tree.Add(new { path = relative, mode = "100644", type = "blob", sha });
        }

        progress?.Report((88, "Building the commit"));
        var parent = await GetBranchHeadAsync(owner, repo, branch, ct);

        using var treeResponse = await _http.PostAsJsonAsync($"repos/{owner}/{repo}/git/trees", new { tree }, ct);
        var treeJson = await ReadAsync(treeResponse, "build the file tree", ct);
        var treeSha = treeJson.GetProperty("sha").GetString()!;

        object commitBody = parent == null
            ? new { message, tree = treeSha }
            : new { message, tree = treeSha, parents = new[] { parent } };
        using var commitResponse = await _http.PostAsJsonAsync($"repos/{owner}/{repo}/git/commits", commitBody, ct);
        var commitJson = await ReadAsync(commitResponse, "create the commit", ct);
        var commitSha = commitJson.GetProperty("sha").GetString()!;

        progress?.Report((95, "Moving the branch"));
        if (parent == null)
        {
            using var create = await _http.PostAsJsonAsync($"repos/{owner}/{repo}/git/refs",
                new { @ref = $"refs/heads/{branch}", sha = commitSha }, ct);
            await ReadAsync(create, $"create the branch {branch}", ct);
        }
        else
        {
            using var update = await _http.PatchAsJsonAsync($"repos/{owner}/{repo}/git/refs/heads/{branch}",
                new { sha = commitSha, force = true }, ct);
            await ReadAsync(update, $"move the branch {branch}", ct);
        }
        progress?.Report((100, "Uploaded"));
    }

    private async Task<string> CreateBlobAsync(string owner, string repo, byte[] content, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync($"repos/{owner}/{repo}/git/blobs",
            new { content = Convert.ToBase64String(content), encoding = "base64" }, ct);
        var json = await ReadAsync(response, "upload a file", ct);
        return json.GetProperty("sha").GetString()!;
    }

    private async Task<string?> GetBranchHeadAsync(string owner, string repo, string branch, CancellationToken ct)
    {
        using var response = await _http.GetAsync($"repos/{owner}/{repo}/git/ref/heads/{branch}", ct);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict) return null;
        var json = await ReadAsync(response, $"read the branch {branch}", ct);
        return json.GetProperty("object").GetProperty("sha").GetString();
    }

    // ── the topic that marks a generated site ─────────────────────────────────

    /// <summary>
    /// Makes sure <paramref name="topic"/> is among the repository's topics, leaving any others
    /// alone. GitHub only offers "replace them all", so the existing set is read first — a
    /// publish must not quietly strip topics somebody else put there.
    /// </summary>
    /// <summary>Whether the repository carries <paramref name="topic"/> — how a TrafficLab+ site is recognised.</summary>
    public async Task<bool> HasTopicAsync(string owner, string repo, string topic, CancellationToken ct = default)
    {
        using var read = await _http.GetAsync($"repos/{owner}/{repo}/topics", ct);
        if (!read.IsSuccessStatusCode) return false;
        var json = await ReadAsync(read, "read the repository topics", ct);
        return json.TryGetProperty("names", out var names) && names.ValueKind == JsonValueKind.Array
               && names.EnumerateArray().Any(n => string.Equals(n.GetString(), topic, StringComparison.OrdinalIgnoreCase));
    }

    public async Task EnsureTopicAsync(string owner, string repo, string topic, CancellationToken ct = default)
    {
        var topics = new List<string>();
        using (var read = await _http.GetAsync($"repos/{owner}/{repo}/topics", ct))
        {
            if (read.IsSuccessStatusCode)
            {
                var json = await ReadAsync(read, "read the repository topics", ct);
                if (json.TryGetProperty("names", out var names) && names.ValueKind == JsonValueKind.Array)
                    topics.AddRange(names.EnumerateArray()
                                         .Where(n => n.ValueKind == JsonValueKind.String)
                                         .Select(n => n.GetString()!));
            }
        }

        if (topics.Contains(topic, StringComparer.OrdinalIgnoreCase)) return;

        topics.Add(topic);
        using var write = await _http.PutAsJsonAsync($"repos/{owner}/{repo}/topics", new { names = topics }, ct);
        await ReadAsync(write, "mark the repository with its topic", ct);
    }

    // ── GitHub Pages ──────────────────────────────────────────────────────────

    /// <summary>Turns Pages on for a branch, or points an existing Pages site at it. Idempotent.</summary>
    public async Task<PagesSite> EnablePagesAsync(string owner, string repo, string branch, CancellationToken ct = default)
    {
        var body = new { source = new { branch, path = "/" } };
        using var create = await _http.PostAsJsonAsync($"repos/{owner}/{repo}/pages", body, ct);
        if (create.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest)
        {
            // Already on — just make sure it serves this branch.
            using var update = await _http.PutAsJsonAsync($"repos/{owner}/{repo}/pages", body, ct);
            if (!update.IsSuccessStatusCode && update.StatusCode != HttpStatusCode.NoContent)
                await ReadAsync(update, "point GitHub Pages at the branch", ct);
        }
        else if (!create.IsSuccessStatusCode)
        {
            await ReadAsync(create, "switch GitHub Pages on", ct);
        }

        return await GetPagesAsync(owner, repo, ct)
               ?? new PagesSite($"https://{owner}.github.io/{repo}/", "building", branch);
    }

    public async Task<PagesSite?> GetPagesAsync(string owner, string repo, CancellationToken ct = default)
    {
        using var response = await _http.GetAsync($"repos/{owner}/{repo}/pages", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        var json = await ReadAsync(response, "read the GitHub Pages settings", ct);
        return new PagesSite(
            json.GetProperty("html_url").GetString() ?? $"https://{owner}.github.io/{repo}/",
            json.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString()! : "unknown",
            json.TryGetProperty("source", out var src) ? src.GetProperty("branch").GetString() ?? "" : "");
    }

    // ── plumbing ──────────────────────────────────────────────────────────────

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response, string what, CancellationToken ct)
    {
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new GitHubException(response.StatusCode, Explain(response.StatusCode, what, text));
        if (string.IsNullOrWhiteSpace(text)) return default;
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    /// <summary>Turns a status code into something a person can act on.</summary>
    private static string Explain(HttpStatusCode status, string what, string body)
    {
        var detail = Message(body);
        return status switch
        {
            HttpStatusCode.Unauthorized =>
                $"GitHub did not accept the token when asked to {what} — it has probably expired or been deleted. " +
                "Press GitHub token… and paste a new one (the GitHub token… window says how to make one).",
            HttpStatusCode.Forbidden when detail.Contains("rate limit", StringComparison.OrdinalIgnoreCase) =>
                "GitHub's rate limit has been reached — wait a few minutes and try again.",
            HttpStatusCode.Forbidden =>
                $"The token is not allowed to {what}. It needs the \"Administration\", \"Contents\" and \"Pages\" permissions, each set to Read and write (or, for a classic token, the \"repo\" scope). {detail}",
            HttpStatusCode.NotFound =>
                $"GitHub could not find what it needed to {what}. {detail}",
            HttpStatusCode.UnprocessableEntity =>
                $"GitHub refused to {what}: {detail}",
            _ => $"GitHub returned {(int)status} {status} when asked to {what}. {detail}"
        };
    }

    private static string Message(string body)
    {
        try
        {
            var json = JsonDocument.Parse(body).RootElement;
            var message = json.TryGetProperty("message", out var m) ? m.GetString() ?? string.Empty : string.Empty;
            if (json.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
            {
                var first = errors.EnumerateArray().FirstOrDefault();
                if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("message", out var em))
                    message += " " + em.GetString();
            }
            return message.Trim();
        }
        catch (JsonException) { return Trim(body, 200); }
    }

    private static string Trim(string text, int max) =>
        text.Length <= max ? text : text[..max];

    public void Dispose() => _http.Dispose();
}
