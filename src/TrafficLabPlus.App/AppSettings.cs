using System.IO;
using System.Text.Json;

namespace TrafficLabPlus.App;

/// <summary>The program's own settings and folders, kept under the user's local app data.</summary>
public sealed class AppSettings
{
    public string? DefaultAuthor { get; set; }

    public string? DefaultCourse { get; set; }

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
