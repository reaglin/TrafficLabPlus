using System.Reflection;

namespace TrafficLabPlus.Core.Build;

/// <summary>
/// The files the page is made of — <c>web/</c> in the repository — and the built-in example, embedded
/// in this assembly. Nothing is read from beside the program, so a page builds the same anywhere.
/// </summary>
public static class WebAssets
{
    private static readonly Assembly Here = typeof(WebAssets).Assembly;

    public const string Template = "web/template.html";
    public const string Core = "web/core/trafficlab-core.js";
    public const string Ui = "web/ui/trafficlab-ui.js";
    public const string Style = "web/ui/trafficlab.css";
    public const string LpgaExample = "samples/lpga.json";
    public const string FourWayExample = "samples/four-way.json";

    /// <summary>The fonts, inlined into every page: Overpass, an open descendant of the US
    /// highway-sign alphabet, under the SIL Open Font License (web/fonts/OFL-Overpass.txt).</summary>
    public static readonly IReadOnlyList<(string Family, int Weight, string Resource)> Fonts =
    [
        ("Overpass", 400, "web/fonts/overpass-latin-400-normal.woff2"),
        ("Overpass", 600, "web/fonts/overpass-latin-600-normal.woff2"),
        ("Overpass", 800, "web/fonts/overpass-latin-800-normal.woff2"),
        ("Overpass Mono", 400, "web/fonts/overpass-mono-latin-400-normal.woff2"),
        ("Overpass Mono", 600, "web/fonts/overpass-mono-latin-600-normal.woff2"),
    ];

    public static string Text(string name)
    {
        using Stream stream = Open(name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static byte[] Bytes(string name)
    {
        using Stream stream = Open(name);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    // Resource names carry the build machine's folder separators; look them up with '/' throughout.
    private static readonly Dictionary<string, string> Names =
        Here.GetManifestResourceNames().ToDictionary(n => n.Replace('\\', '/'), n => n, StringComparer.Ordinal);

    private static Stream Open(string name) =>
        (Names.TryGetValue(name, out string? real) ? Here.GetManifestResourceStream(real) : null)
        ?? throw new InvalidOperationException($"TrafficLab+ is missing a part of itself ({name}). Reinstall TrafficLab+.");
}
