using System.Text.RegularExpressions;

namespace TrafficLabPlus.Core.Publish;

/// <summary>What was read out of something typed or pasted into the account or repository box.</summary>
/// <param name="Owner">The GitHub account, when the text named one.</param>
/// <param name="Repository">The repository, when the text named one.</param>
/// <param name="Problem">Why the text cannot be used, in words; null when it can.</param>
/// <param name="Note">What was taken from a pasted address, to say back to the teacher; empty when nothing was.</param>
public sealed record GitHubTarget(string? Owner, string? Repository, string? Problem, string Note = "");

/// <summary>
/// Reads the account and repository out of whatever a teacher types or pastes (Ron, 2026-09-24:
/// "many users copy/paste the URL"): a bare name, <c>owner/repo</c>, the repository's address in
/// any of its forms, or the website's own address.
/// </summary>
public static partial class GitHubAddress
{
    // GitHub's own rules: an account is letters, digits and single hyphens, up to 39, not starting
    // or ending with a hyphen; a repository is letters, digits, '.', '-' and '_', up to 100.
    [GeneratedRegex(@"^[A-Za-z0-9](?:[A-Za-z0-9]|-(?=[A-Za-z0-9])){0,38}$")]
    private static partial Regex AccountRule();

    [GeneratedRegex(@"^[A-Za-z0-9._-]{1,100}$")]
    private static partial Regex RepositoryRule();

    // https://github.com/owner/repo(.git)(/anything) — with or without https://, www. or a trailing slash
    [GeneratedRegex(@"^(?:https?://)?(?:www\.)?github\.com/(?<owner>[^/\s]+)(?:/(?<repo>[^/\s?#]+))?", RegexOptions.IgnoreCase)]
    private static partial Regex RepositoryAddress();

    // git@github.com:owner/repo.git
    [GeneratedRegex(@"^git@github\.com:(?<owner>[^/\s]+)/(?<repo>[^/\s]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex SshAddress();

    // https://owner.github.io/repo/ — the website itself
    [GeneratedRegex(@"^(?:https?://)?(?<owner>[^./\s]+)\.github\.io(?:/(?<repo>[^/\s?#]+))?", RegexOptions.IgnoreCase)]
    private static partial Regex PagesAddress();

    /// <summary>Reads the repository box. A bare name is a repository; an address may carry the account too.</summary>
    public static GitHubTarget ReadRepository(string? typed)
    {
        string text = (typed ?? string.Empty).Trim();

        if (text.Length == 0)
        {
            return new GitHubTarget(null, null, null);
        }

        (string? owner, string? repo, bool wasAddress) = Split(text);

        if (repo is null)
        {
            // An address with only an account in it — github.com/reaglin — says who, not where.
            return owner is not null && wasAddress
                ? new GitHubTarget(owner, null,
                    "That address names a GitHub account but no repository. The repository name is the part after the " +
                    "account, as in github.com/" + owner + "/GP-maths — or type just a name, such as maths.")
                : new GitHubTarget(null, null, ProblemWithRepository(text));
        }

        repo = TrimGit(repo);
        string? problem = ProblemWithRepository(repo) ?? (owner is null ? null : ProblemWithAccount(owner));

        string note = wasAddress
            ? $"Read from the address you pasted: account {owner}, repository {repo}."
            : string.Empty;

        return new GitHubTarget(owner, repo, problem, note);
    }

    /// <summary>Reads the account box: a user name, or any address that starts with one.</summary>
    public static GitHubTarget ReadAccount(string? typed)
    {
        string text = (typed ?? string.Empty).Trim().TrimStart('@');

        if (text.Length == 0)
        {
            return new GitHubTarget(null, null, null);
        }

        (string? owner, string? repo, bool wasAddress) = Split(text);
        owner ??= text;

        string note = wasAddress
            ? $"Read from the address you pasted: account {owner}" + (repo is null ? "." : $", repository {TrimGit(repo)}.")
            : string.Empty;

        return new GitHubTarget(owner, repo is null ? null : TrimGit(repo), ProblemWithAccount(owner), note);
    }

    /// <summary>
    /// A repository name made from any text — a file name, say: letters, digits, '.', '-' and '_'
    /// kept, everything else a single hyphen, capitals kept. <paramref name="fallback"/> when
    /// nothing usable is left.
    /// </summary>
    public static string SafeRepositoryName(string? text, string fallback)
    {
        var name = new System.Text.StringBuilder();

        foreach (char c in (text ?? string.Empty).Trim())
        {
            bool keep = c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '.' or '-' or '_';

            if (keep)
            {
                name.Append(c);
            }
            else if (name.Length > 0 && name[^1] != '-')
            {
                name.Append('-');
            }
        }

        string result = name.ToString().Trim('-', '.');

        if (result.Length > 90)
        {
            result = result[..90].Trim('-', '.');
        }

        return result.Length == 0 ? fallback : result;
    }

    /// <summary>The user name out of whatever is in the account box, or the text as typed when it is not one.</summary>
    public static string CleanAccount(string? typed)
    {
        GitHubTarget read = ReadAccount(typed);
        return read.Problem is null ? read.Owner ?? string.Empty : (typed ?? string.Empty).Trim();
    }

    private static (string? Owner, string? Repo, bool WasAddress) Split(string text)
    {
        foreach (Regex form in new[] { SshAddress(), RepositoryAddress(), PagesAddress() })
        {
            Match m = form.Match(text);

            if (m.Success)
            {
                string? repo = m.Groups["repo"].Success && m.Groups["repo"].Value.Length > 0 ? m.Groups["repo"].Value : null;
                return (m.Groups["owner"].Value, repo, true);
            }
        }

        // owner/repo, typed
        string[] parts = text.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length == 2 ? (parts[0], parts[1], true) : (null, text, false);
    }

    private static string TrimGit(string repo) =>
        repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? repo[..^4] : repo;

    public static string? ProblemWithRepository(string repo)
    {
        if (RepositoryRule().IsMatch(repo) && repo is not "." and not "..")
        {
            return null;
        }

        return repo.Contains(' ', StringComparison.Ordinal)
            ? $"\"{repo}\" has a space in it. A repository name can use letters, digits, - . and _ — use a hyphen instead, as in TrafficLab."
            : $"\"{repo}\" cannot be a repository name. Use letters, digits, - . and _ only (up to 100), as in TrafficLab.";
    }

    public static string? ProblemWithAccount(string owner) =>
        AccountRule().IsMatch(owner)
            ? null
            : $"\"{owner}\" is not a GitHub user name. A user name is letters, digits and single hyphens — the part after " +
              "github.com/ in your profile's address.";
}
