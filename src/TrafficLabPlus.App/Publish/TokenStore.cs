using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TrafficLabPlus.App.Publish;

/// <summary>
/// Keeps the GitHub token on this machine, encrypted with DPAPI for the current Windows user, in
/// <c>%LOCALAPPDATA%\TrafficLabPlus\github-token.dat</c>. Nobody signed in as anyone else can read
/// it, and it never travels with a study site or a website. (Ported from LMS 2 Website, which
/// learned to keep it out of Documents: that folder is often synced to OneDrive, and a machine
/// secret has no business being copied anywhere.)
///
/// In the app and not in Core because DPAPI is a package, and Core has none.
/// </summary>
public static class TokenStore
{
    public static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrafficLabPlus");

    public static string TokenPath => Path.Combine(Folder, "github-token.dat");

    public static bool HasToken => File.Exists(TokenPath);

    /// <summary>
    /// The GitHub account the saved token belongs to, as GitHub said when it was checked. Not a
    /// secret — a user name — so it is kept in plain text beside the token, to be shown back.
    /// </summary>
    public static string AccountPath => Path.Combine(Folder, "github-account.txt");

    public static string? LoadAccount()
    {
        try
        {
            string account = File.Exists(AccountPath) ? File.ReadAllText(AccountPath).Trim() : string.Empty;
            return account.Length == 0 ? null : account;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public static void SaveAccount(string account)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(AccountPath, account.Trim());
    }

    public static void Save(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            Clear();
            return;
        }

        Directory.CreateDirectory(Folder);
        byte[] bytes = Encoding.UTF8.GetBytes(token.Trim());
        File.WriteAllBytes(TokenPath, ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser));
    }

    /// <summary>The stored token, or null when there is none or it cannot be decrypted here.</summary>
    public static string? Load()
    {
        if (!File.Exists(TokenPath))
        {
            return null;
        }

        try
        {
            byte[] plain = ProtectedData.Unprotect(File.ReadAllBytes(TokenPath), null, DataProtectionScope.CurrentUser);
            string token = Encoding.UTF8.GetString(plain).Trim();
            return token.Length == 0 ? null : token;
        }
        catch (CryptographicException)
        {
            return null;   // written by another user or on another machine
        }
        catch (IOException)
        {
            return null;
        }
    }

    public static void Clear()
    {
        if (File.Exists(TokenPath))
        {
            File.Delete(TokenPath);
        }

        if (File.Exists(AccountPath))
        {
            File.Delete(AccountPath);
        }
    }

    /// <summary>"gith…cdef" — enough to recognise, not enough to use.</summary>
    public static string Mask(string token) =>
        token.Length <= 10 ? new string('•', token.Length) : $"{token[..4]}…{token[^4..]}";
}
