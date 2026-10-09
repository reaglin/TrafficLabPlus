using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using TrafficLabPlus.Core.Build;

namespace TrafficLabPlus.Core.Model;

/// <summary>Study ⇄ JSON text, as the page reads it.</summary>
public static class StudyJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        // a hand-made study may write 5 as 5.0, or a number as a string by mistake; numbers only here
        NumberHandling = JsonNumberHandling.Strict,
    };

    /// <summary>Reads a study. A newer format is refused before anything is read, so it is never
    /// half-read and then saved back without what this program did not understand.</summary>
    /// <exception cref="StudyFormatException">Not a study, a newer format, or a value of the wrong kind.</exception>
    public static Study Read(string json)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new StudyFormatException($"This file is not a TrafficLab+ study: it could not be read ({ex.Message}).", ex);
        }

        if (node is not JsonObject obj)
        {
            throw new StudyFormatException("This file is not a TrafficLab+ study: it does not hold a study.");
        }

        if (obj["format"] is JsonValue f && f.TryGetValue(out double format) && format > Study.CurrentFormat)
        {
            throw new StudyFormatException(NewerFormat(format));
        }

        try
        {
            return obj.Deserialize<Study>(Options)
                   ?? throw new StudyFormatException("This file is not a TrafficLab+ study: it does not hold a study.");
        }
        catch (JsonException ex)
        {
            string where = string.IsNullOrEmpty(ex.Path) ? "" : " at " + ex.Path.TrimStart('$', '.');
            throw new StudyFormatException($"This study has a value of the wrong kind{where}, so it cannot be opened.", ex);
        }
    }

    public static string Write(Study study) => JsonSerializer.Serialize(study, Options);

    /// <summary>A deep copy, for undo and for building the page while the student keeps editing.</summary>
    public static Study Copy(Study study) => Read(Write(study));

    /// <summary>The engine's own words (<c>validate</c> in trafficlab-core.js).</summary>
    public static string NewerFormat(double format) =>
        $"This study was made by a newer TrafficLab+ (format {format}). Update TrafficLab+ to open it.";
}
