using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TrafficLabPlus.Core.Osm;

/// <summary>
/// The roads as OpenStreetMap gave them, kept inside the study file as <c>osm.json</c>: the box
/// that was asked for, when, the answer itself and the junctions chosen. An opened study shows
/// its map from this and never asks OpenStreetMap again.
/// </summary>
public sealed class OsmCache
{
    public const string Entry = "osm.json";

    public double[] Box { get; set; } = [];
    public string Fetched { get; set; } = "";
    public List<string> Junctions { get; set; } = [];
    public string Overpass { get; set; } = "";

    public byte[] ToBytes()
    {
        var o = new JsonObject
        {
            ["box"] = new JsonArray(Box.Select(v => (JsonNode)v).ToArray()),
            ["fetched"] = Fetched,
            ["junctions"] = new JsonArray(Junctions.Select(j => (JsonNode)j).ToArray()),
            ["overpass"] = JsonNode.Parse(Overpass),
        };
        return Encoding.UTF8.GetBytes(o.ToJsonString());
    }

    public static OsmCache? FromBytes(byte[]? bytes)
    {
        if (bytes is null)
        {
            return null;
        }

        try
        {
            JsonNode? o = JsonNode.Parse(bytes);
            return new OsmCache
            {
                Box = o?["box"]?.AsArray().Select(v => v!.GetValue<double>()).ToArray() ?? [],
                Fetched = o?["fetched"]?.GetValue<string>() ?? "",
                Junctions = o?["junctions"]?.AsArray().Select(v => v!.GetValue<string>()).ToList() ?? [],
                Overpass = o?["overpass"]?.ToJsonString() ?? "",
            };
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return null;   // a damaged copy: the map simply asks again
        }
    }
}
