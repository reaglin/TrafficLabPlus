using System.Text.Json;
using System.Text.Json.Nodes;

namespace TrafficLabPlus.Core.Ai;

/// <summary>The AI's answer could not be read as the JSON asked for; said in words a student can act on.</summary>
public sealed class AiReadException : Exception
{
    public AiReadException() { }

    public AiReadException(string message) : base(message) { }

    public AiReadException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Reads what an AI actually answers with (Gamify+'s DraftReader lesson): the JSON may come in a
/// code fence, after a sentence, before a sentence, with a trailing comma, or as a bare list where
/// an object was asked for. Each of those has a test.
/// </summary>
public static class AiReader
{
    private static readonly JsonDocumentOptions Lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    /// <exception cref="AiReadException">There is no JSON object in the answer.</exception>
    public static JsonObject Object(string answer, string listName)
    {
        string text = Unfence(answer ?? "");
        int open = text.IndexOfAny(['{', '[']);
        if (open < 0)
        {
            throw new AiReadException("The AI answered in words instead of the list of changes asked for. Press Ask again; if it happens twice, try another AI provider (Settings, on the left, then AI settings…).");
        }

        char close = text[open] == '{' ? '}' : ']';
        int end = text.LastIndexOf(close);
        if (end <= open)
        {
            throw new AiReadException("The AI's answer was cut off before it finished. Press Ask again, perhaps asking for fewer changes.");
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(text[open..(end + 1)], documentOptions: Lenient);
        }
        catch (JsonException ex)
        {
            throw new AiReadException("The AI's answer could not be read. Press Ask again.", ex);
        }

        return node switch
        {
            JsonObject o => o,
            JsonArray a => new JsonObject { [listName] = a.DeepClone() },   // a bare list where an object was asked for
            _ => throw new AiReadException("The AI's answer was not the list of changes asked for. Press Ask again."),
        };
    }

    private static string Unfence(string text)
    {
        int fence = text.IndexOf("```", StringComparison.Ordinal);
        if (fence < 0)
        {
            return text;
        }

        int start = text.IndexOf('\n', fence);
        int stop = text.IndexOf("```", fence + 3, StringComparison.Ordinal);
        return start >= 0 && stop > start ? text[(start + 1)..stop] : text;
    }

    public static string? Str(JsonNode? n) => n is JsonValue v && v.TryGetValue(out string? s) ? s : n is JsonValue v2 ? v2.ToString() : null;

    public static double? Num(JsonNode? n)
    {
        if (n is not JsonValue v)
        {
            return null;
        }

        if (v.TryGetValue(out double d))
        {
            return d;
        }

        // "120", "120 s", "45 mph", "3.5M"
        string s = new((v.ToString() ?? "").TakeWhile(c => char.IsDigit(c) || c is '.' or '-').ToArray());
        return double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double p) ? p : null;
    }

    public static bool? Bool(JsonNode? n) => n is JsonValue v
        ? v.TryGetValue(out bool b) ? b
        : (v.ToString() ?? "").Trim().ToLowerInvariant() switch { "true" or "yes" or "1" => true, "false" or "no" or "0" => false, _ => null }
        : null;
}
