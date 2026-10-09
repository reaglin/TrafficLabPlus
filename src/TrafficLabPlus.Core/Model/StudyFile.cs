using System.IO.Compression;
using System.Text;
using TrafficLabPlus.Core.Build;

namespace TrafficLabPlus.Core.Model;

/// <summary>An open study: the study itself, the student's notes, and anything else the file
/// carries (the OpenStreetMap extract, from phase 4), kept as it came.</summary>
public sealed class StudyDocument
{
    public Study Study { get; set; } = new();

    /// <summary>The student's own notes. Kept in the file, not put on the page.</summary>
    public string Notes { get; set; } = "";

    /// <summary>Other entries in the file, by name, kept so a save writes them back.</summary>
    public Dictionary<string, byte[]> Attachments { get; } = new(StringComparer.Ordinal);
}

/// <summary>
/// The <c>.trafficlab</c> file: a zip holding <c>study.json</c> (the study, as the page reads it),
/// <c>notes.md</c>, and later <c>osm.json</c> — the roads as OpenStreetMap gave them, so an opened
/// study never asks OpenStreetMap again. A plain <c>.json</c> study opens too.
/// </summary>
public static class StudyFile
{
    public const string Extension = ".trafficlab";
    public const string StudyEntry = "study.json";
    public const string NotesEntry = "notes.md";

    /// <exception cref="StudyFormatException">The file is not a study, or a newer TrafficLab+ made it.</exception>
    /// <exception cref="IOException">The file could not be read.</exception>
    public static StudyDocument Load(string path)
    {
        if (!path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
        {
            return new StudyDocument { Study = StudyJson.Read(File.ReadAllText(path)) };
        }

        try
        {
            using ZipArchive zip = ZipFile.OpenRead(path);
            ZipArchiveEntry entry = zip.GetEntry(StudyEntry)
                ?? throw new StudyFormatException("This file is not a TrafficLab+ study: it has no study inside it.");
            var doc = new StudyDocument { Study = StudyJson.Read(ReadText(entry)) };
            foreach (ZipArchiveEntry other in zip.Entries)
            {
                if (other.FullName == StudyEntry || other.FullName.EndsWith('/'))
                {
                    continue;
                }

                if (other.FullName == NotesEntry)
                {
                    doc.Notes = ReadText(other);
                }
                else
                {
                    using Stream s = other.Open();
                    using var m = new MemoryStream();
                    s.CopyTo(m);
                    doc.Attachments[other.FullName] = m.ToArray();
                }
            }

            return doc;
        }
        catch (InvalidDataException ex)
        {
            throw new StudyFormatException("This file is not a TrafficLab+ study: it is damaged or is another kind of file.", ex);
        }
    }

    /// <summary>Writes the file whole beside the old one first, then swaps it in, so a failed save
    /// never leaves half a study where the good one was.</summary>
    public static void Save(StudyDocument doc, string path)
    {
        string full = Path.GetFullPath(path);
        string dir = Path.GetDirectoryName(full) ?? ".";
        Directory.CreateDirectory(dir);
        string temp = Path.Combine(dir, "." + Path.GetFileName(full) + ".saving");
        try
        {
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteText(zip, StudyEntry, StudyJson.Write(doc.Study));
                if (doc.Notes.Length > 0)
                {
                    WriteText(zip, NotesEntry, doc.Notes);
                }

                foreach ((string name, byte[] bytes) in doc.Attachments)
                {
                    using Stream s = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
                    s.Write(bytes);
                }
            }

            File.Move(temp, full, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    private static string ReadText(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static void WriteText(ZipArchive zip, string name, string text)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
        writer.Write(text);
    }
}
