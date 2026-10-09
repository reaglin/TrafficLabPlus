using System.IO;
using System.Text.Json;

namespace TrafficLabPlus.App;

/// <summary>The program's own settings and folders, kept under the user's local app data.</summary>
public sealed class AppSettings
{
    public string? DefaultAuthor { get; set; }

    public string? DefaultCourse { get; set; }

    // publishing (D15): where, and which studies are left out
    public string? GitHubAccount { get; set; }

    public string? Repository { get; set; }

    public string? SiteTitle { get; set; }

    /// <summary>Full paths of studies marked "Don't publish".</summary>
    public List<string> DontPublish { get; set; } = [];

    /// <summary>The website's address after the last publish, and the study folders on it then.</summary>
    public string? LastSiteUrl { get; set; }

    public List<string> LastPublished { get; set; } = [];

    /// <summary>Each published study's folder on the website, by its file's full path: once handed in,
    /// a link must not change because a file was renamed or another file took the name.</summary>
    public Dictionary<string, string> Slugs { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The titles of the studies on the website now, by folder, to say what comes off it.</summary>
    public Dictionary<string, string> PublishedTitles { get; set; } = [];

    public static string DataFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrafficLabPlus");

    /// <summary>Where studies are saved unless the student picks somewhere else.</summary>
    public static string StudiesFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrafficLabPlus", "Studies");

    /// <summary>Where the window writes the page it previews.</summary>
    public static string PreviewFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TrafficLabPlus", "Preview");

    public static string RecentFile => Path.Combine(DataFolder, "recent.txt");

    private static string File => Path.Combine(DataFolder, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            return System.IO.File.Exists(File) ? JsonSerializer.Deserialize<AppSettings>(System.IO.File.ReadAllText(File)) ?? new() : new();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(DataFolder);
            System.IO.File.WriteAllText(File, JsonSerializer.Serialize(this));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // settings are a convenience; failing to keep them is not worth stopping anyone for
        }
    }
}
