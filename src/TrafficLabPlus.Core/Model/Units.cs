namespace TrafficLabPlus.Core.Model;

/// <summary>US customary units for people (Ron, 2026-10-09); metres and m/s in the study.</summary>
public static class Units
{
    public const double FeetPerMetre = 1 / 0.3048;
    public const double MphPerMps = 3600 / 1609.344;

    public static double Feet(double metres) => metres * FeetPerMetre;

    public static double Metres(double feet) => feet / FeetPerMetre;

    public static double Mph(double mps) => mps * MphPerMps;

    public static double Mps(double mph) => mph / MphPerMps;
}

/// <summary>The last few studies opened or saved, newest first, kept in a small text file.</summary>
public sealed class RecentStudies(string file)
{
    public const int Max = 8;

    public IReadOnlyList<string> Load()
    {
        try
        {
            return File.Exists(file)
                ? File.ReadAllLines(file).Where(p => p.Length > 0 && File.Exists(p)).Distinct(StringComparer.OrdinalIgnoreCase).Take(Max).ToList()
                : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public void Add(string path)
    {
        string full = Path.GetFullPath(path);
        Write(new[] { full }.Concat(Load().Where(p => !string.Equals(p, full, StringComparison.OrdinalIgnoreCase))));
    }

    public void Remove(string path) =>
        Write(Load().Where(p => !string.Equals(p, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)));

    private void Write(IEnumerable<string> paths)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file)) ?? ".");
            File.WriteAllLines(file, paths.Take(Max));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // the list is a convenience; failing to keep it must never stop a save
        }
    }
}
