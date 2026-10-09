using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TrafficLabPlus.Core.Build;

/// <summary>
/// A study in, one self-contained web page out: the template with the stylesheet, the fonts, the
/// engine, the interface and the study written into it. The page needs nothing else — no server,
/// no CDN, no web font, no map tiles — so it opens from disk and from a GitHub Pages sub-path alike.
/// What the program previews is this file, byte for byte.
/// </summary>
public static class PageBuilder
{
    /// <summary>The machine marker in every page's <c>generator</c> meta. It names the code, not the
    /// product, and stays the same whatever the product is called.</summary>
    public const string Generator = "TrafficLabPlus";

    /// <summary>Builds the page for a study given as JSON text.</summary>
    /// <exception cref="StudyFormatException">The text is not a study.</exception>
    public static string Build(string studyJson)
    {
        JsonObject study = ReadStudy(studyJson);
        string title = study["title"]?.GetValue<string>() is { Length: > 0 } t ? t : "Traffic study";

        // Re-serialised with the default encoder, which escapes < > & — so a road called
        // "</script>" cannot break out of the script element the study sits in.
        string studyText = study.ToJsonString(new JsonSerializerOptions { WriteIndented = false });

        return WebAssets.Text(WebAssets.Template)
            .Replace("{{GENERATOR}}", Generator + " " + typeof(PageBuilder).Assembly.GetName().Version?.ToString(3), StringComparison.Ordinal)
            .Replace("{{TITLE}}", WebUtility.HtmlEncode(title), StringComparison.Ordinal)
            .Replace("/*{{FONTS}}*/", FontFaces(), StringComparison.Ordinal)
            .Replace("/*{{STYLE}}*/", WebAssets.Text(WebAssets.Style), StringComparison.Ordinal)
            .Replace("/*{{CORE}}*/", WebAssets.Text(WebAssets.Core), StringComparison.Ordinal)
            .Replace("/*{{UI}}*/", WebAssets.Text(WebAssets.Ui), StringComparison.Ordinal)
            .Replace("{{STUDY}}", studyText, StringComparison.Ordinal);
    }

    /// <summary>The built-in example: Dr. Ron Eaglin's LPGA Traffic Lab, as a study.</summary>
    public static string LpgaExampleJson() => WebAssets.Text(WebAssets.LpgaExample);

    private static JsonObject ReadStudy(string json)
    {
        try
        {
            return JsonNode.Parse(json) as JsonObject
                   ?? throw new StudyFormatException("This file is not a TrafficLab+ study: it does not hold a study.");
        }
        catch (JsonException ex)
        {
            throw new StudyFormatException($"This file is not a TrafficLab+ study: it could not be read ({ex.Message}).", ex);
        }
    }

    private static string FontFaces()
    {
        var css = new StringBuilder();
        foreach ((string family, int weight, string resource) in WebAssets.Fonts)
        {
            css.Append("@font-face{font-family:\"").Append(family).Append("\";font-style:normal;font-weight:")
               .Append(weight).Append(";font-display:swap;src:url(data:font/woff2;base64,")
               .Append(Convert.ToBase64String(WebAssets.Bytes(resource))).Append(") format(\"woff2\")}\n");
        }

        return css.ToString();
    }
}

/// <summary>A file that is not a study at all. (A study with mistakes in it still builds; the page
/// says what is wrong, in the words the engine's own check uses.)</summary>
public sealed class StudyFormatException : Exception
{
    public StudyFormatException() : base("This file is not a TrafficLab+ study.") { }

    public StudyFormatException(string message) : base(message) { }

    public StudyFormatException(string message, Exception inner) : base(message, inner) { }
}
