using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TrafficLabPlus.Core.Model;
using TrafficLabPlus.Core.Osm;

namespace TrafficLabPlus.Core.Counts;

/// <summary>One FDOT count: a stretch of road with its Annual Average Daily Traffic and the
/// design-hour factors FDOT publishes with it.</summary>
public sealed record FdotCount(
    string Site,
    int Year,
    int Aadt,
    double K,
    double D,
    string From,
    string To,
    string County,
    string Roadway,
    IReadOnlyList<(double Lat, double Lon)> Points)
{
    /// <summary>A one-way road: FDOT gives the whole flow one direction (D ≈ 100%), as on a ramp.</summary>
    public bool OneWay => D >= 0.99;

    public string Describe() =>
        string.Create(CultureInfo.CurrentCulture, $"FDOT count site {Site}, {Year}: {Aadt:#,0} vehicles a day{(OneWay ? " (one way)" : " (both directions)")}, from {Title(From)} to {Title(To)}");

    // as FDOT writes it: title case would spoil "SR-9" and "LPGA"
    private static string Title(string s) => string.IsNullOrWhiteSpace(s) || s == "N/A" ? "—" : s.Trim();
}

/// <summary>What TrafficLab+ makes of the counts for one road end.</summary>
public sealed class CountMatch
{
    public required string EndId { get; init; }
    public required string EndName { get; init; }
    public List<(FdotCount Count, bool Inbound)> Counts { get; } = [];
    public bool Use { get; set; } = true;

    /// <summary>K and D, editable; they start at FDOT's own factors for the matched count.</summary>
    public double K { get; set; } = FdotCounts.DefaultK;
    public double D { get; set; } = FdotCounts.DefaultD;

    public bool Found => Counts.Count > 0;

    /// <summary>Vehicles an hour coming in at this road end, in the design hour.</summary>
    public double Entering => Counts.Sum(c => FdotCounts.Entering(c.Count, c.Inbound, K, D));

    /// <summary>Vehicles an hour leaving the study here in the same hour (how busy it is as a destination).</summary>
    public double Exiting => Counts.Sum(c => FdotCounts.Exiting(c.Count, c.Inbound, K, D));
}

/// <summary>
/// FDOT's Annual Average Daily Traffic (Transportation Data and Analytics office), read from its
/// public ArcGIS layer, matched to a study's road ends, and turned into design-hour volumes with
/// the K and D factors: <c>AADT × K × D</c> vehicles an hour in the busier direction, which TrafficLab+
/// takes as the direction toward the intersection, so the study is tested at a busy hour.
/// </summary>
public static class FdotCounts
{
    public const string Layer = "https://services1.arcgis.com/O1JpcwDW8sjYuddV/arcgis/rest/services/Annual_Average_Daily_Traffic_TDA/FeatureServer/0";
    public const string Credit = "Florida Department of Transportation, Transportation Data and Analytics (Annual Average Daily Traffic)";
    public const double DefaultK = 0.09;   // FDOT's usual design-hour factor for urban arterials
    public const double DefaultD = 0.55;
    public const double MatchMetres = 40;

    /// <summary>The question for every count in a box (south, west, north, east).</summary>
    public static string QueryUrl(double south, double west, double north, double east) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{Layer}/query?where=1%3D1&geometry={west:0.######},{south:0.######},{east:0.######},{north:0.######}&geometryType=esriGeometryEnvelope&inSR=4326&spatialRel=esriSpatialRelIntersects&outFields=YEAR_,COSITE,ROADWAY,DESC_FRM,DESC_TO,AADT,AADTFLG,KFCTR,DFCTR,TFCTR,COUNTY&returnGeometry=true&outSR=4326&geometryPrecision=6&f=json");

    /// <exception cref="Build.StudyFormatException">Not an answer from the counts layer.</exception>
    public static List<FdotCount> Parse(string json)
    {
        try
        {
            JsonNode root = JsonNode.Parse(json) ?? throw new JsonException("empty");
            if (root["error"] is JsonNode error)
            {
                throw new Build.StudyFormatException("FDOT's count service answered with an error: " + (error["message"]?.ToString() ?? "unknown") + ".");
            }

            var counts = new List<FdotCount>();
            foreach (JsonNode? f in root["features"]?.AsArray() ?? throw new JsonException("no features"))
            {
                JsonNode a = f!["attributes"]!;
                var points = new List<(double, double)>();
                foreach (JsonNode? path in f["geometry"]?["paths"]?.AsArray() ?? [])
                {
                    points.AddRange(path!.AsArray().Select(p => (p![1]!.GetValue<double>(), p[0]!.GetValue<double>())));
                }

                double k = Num(a["KFCTR"]) is > 0 and var kv ? kv / 100 : DefaultK;
                double d = Num(a["DFCTR"]) is > 0 and var dv ? dv / 100 : DefaultD;
                counts.Add(new FdotCount(
                    a["COSITE"]?.ToString() ?? "", (int)(Num(a["YEAR_"]) ?? 0), (int)(Num(a["AADT"]) ?? 0), k, d,
                    a["DESC_FRM"]?.ToString() ?? "", a["DESC_TO"]?.ToString() ?? "", a["COUNTY"]?.ToString() ?? "", a["ROADWAY"]?.ToString() ?? "", points));
            }

            return counts.Where(c => c.Aadt > 0 && c.Points.Count >= 2).ToList();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            throw new Build.StudyFormatException("FDOT's count service gave an answer TrafficLab+ could not read (" + ex.Message + ").", ex);
        }
    }

    private static double? Num(JsonNode? n) => n is JsonValue v && v.TryGetValue(out double d) ? d : null;

    public static double Entering(FdotCount c, bool inbound, double k, double d) =>
        c.OneWay ? (inbound ? c.Aadt * k : 0) : c.Aadt * k * d;

    public static double Exiting(FdotCount c, bool inbound, double k, double d) =>
        c.OneWay ? (inbound ? 0 : c.Aadt * k) : c.Aadt * k * (1 - d);

    /// <summary>The box to ask about: every node of the study, with a margin.</summary>
    public static (double South, double West, double North, double East) Box(Study study)
    {
        GeoAnchor g = study.Geo ?? throw new InvalidOperationException("The study has no place on the map.");
        var corners = study.Nodes.Select(n => g.ToLatLon(n.X, n.Y)).ToList();
        double pad = 150 / Geo.MetresPerDegreeLat;
        return (corners.Min(c => c.Lat) - pad, corners.Min(c => c.Lon) - pad, corners.Max(c => c.Lat) + pad, corners.Max(c => c.Lon) + pad);
    }

    /// <summary>
    /// For each road end, the count on the road that leads to it: a counted stretch that runs
    /// along the road (within 35° of it) and passes within 40 m of it, close to the intersection.
    /// One-way counts (ramps) are all kept, each marked as coming in or going out by FDOT's own
    /// description ("I-95/SR-9 to LPGA BLVD" comes in at LPGA).
    /// </summary>
    public static List<CountMatch> Match(Study study, IReadOnlyList<FdotCount> counts)
    {
        GeoAnchor g = study.Geo ?? throw new InvalidOperationException("The study has no place on the map.");
        var local = counts.Select(c => (Count: c, Line: c.Points.Select(p => g.ToLocal(p.Lat, p.Lon)).ToList())).ToList();
        var matches = new List<CountMatch>();
        foreach (StudyNode end in study.Nodes.Where(n => n.Type == StudyNode.End))
        {
            var m = new CountMatch { EndId = end.Id, EndName = end.Label };
            matches.Add(m);
            if (study.LinksAt(end.Id).FirstOrDefault() is not { } link || study.Node(link.A == end.Id ? link.B : link.A) is not { } at)
            {
                continue;
            }

            double len = Math.Sqrt(Math.Pow(end.X - at.X, 2) + Math.Pow(end.Y - at.Y, 2));
            if (len < 1)
            {
                continue;
            }

            (double ux, double uy) = ((end.X - at.X) / len, (end.Y - at.Y) / len);
            // two points on the road: near the intersection (counts often stop at it) and further out
            var samples = new[] { Math.Min(80, len * 0.5), Math.Min(160, len * 0.85) }.Select(t => (at.X + ux * t, at.Y + uy * t)).ToList();
            var near = new List<(FdotCount Count, double Dist)>();
            foreach ((FdotCount c, List<(double X, double Y)> line) in local)
            {
                double best = double.MaxValue;
                foreach ((double sx, double sy) in samples)
                {
                    for (int i = 1; i < line.Count; i++)
                    {
                        (double ax, double ay) = line[i - 1];
                        (double bx, double by) = line[i];
                        double sl = Math.Sqrt(Math.Pow(bx - ax, 2) + Math.Pow(by - ay, 2));
                        if (sl < 0.5)
                        {
                            continue;
                        }

                        double cos = Math.Abs((bx - ax) / sl * ux + (by - ay) / sl * uy);
                        if (cos < Math.Cos(35 * Math.PI / 180))
                        {
                            continue;   // a road crossing this one, not running along it
                        }

                        best = Math.Min(best, PointToSegment(sx, sy, ax, ay, bx, by));
                    }
                }

                if (best <= MatchMetres)
                {
                    near.Add((c, best));
                }
            }

            // a ramp is counted one way (FDOT's ramps); a street both ways, if such a count is there
            bool ramp = link.Name is "Ramp" or "Ramps";
            near = ramp ? near.Where(n => n.Count.OneWay).ToList()
                 : near.Any(n => !n.Count.OneWay) ? near.Where(n => !n.Count.OneWay).ToList()
                 : near;
            if (near.Count == 0)
            {
                continue;
            }

            (FdotCount first, _) = near.OrderBy(n => n.Dist).First();
            List<FdotCount> chosen = first.OneWay ? near.Where(n => n.Count.OneWay).Select(n => n.Count).ToList() : [first];
            string[] words = Words(at.Label);
            foreach (FdotCount c in chosen)
            {
                bool inbound = !c.OneWay || !Mentions(c.From, words) || Mentions(c.To, words);
                m.Counts.Add((c, inbound));
            }

            // the factors of the busiest count
            FdotCount main = chosen.OrderByDescending(c => c.Aadt).First();
            m.K = main.K;
            m.D = main.OneWay ? DefaultD : main.D;
        }

        return matches;
    }

    /// <summary>Puts the ticked counts into the study: typed volumes, with each counted road end's
    /// vehicles coming in and going out, and every other road end keeping its estimate.</summary>
    public static void Apply(Study study, IReadOnlyList<CountMatch> matches, int year)
    {
        Dictionary<string, double> estimate = DemandShares.Entering(study);
        study.Demand.Mode = Demand.Volumes;
        foreach (StudyNode end in study.Nodes.Where(n => n.Type == StudyNode.End))
        {
            CountMatch? m = matches.FirstOrDefault(x => x.EndId == end.Id);
            if (m is { Use: true, Found: true })
            {
                end.Volume = Math.Round(m.Entering);
                end.Weight = Math.Max(1, Math.Round(m.Exiting));
                StudyEdits.Mark(study, $"node:{end.Id}/volume", Origins.Fdot);
                StudyEdits.Mark(study, $"node:{end.Id}/weight", Origins.Fdot);
            }
            else if (end.Volume is null)
            {
                // the same veh/h scale as the counted ends: in and out alike
                end.Volume = Math.Round(estimate.GetValueOrDefault(end.Id));
                end.Weight = Math.Max(1, end.Volume.Value);
            }
        }

        StudyEdits.Mark(study, "demand/mode", Origins.Fdot);
        study.Sources ??= new Sources();
        study.Sources.Extra ??= [];
        study.Sources.Extra["counts"] = JsonSerializer.SerializeToElement(Credit + ", " + year.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Back to one total shared by how busy each road end is, keeping today's total; the
    /// counted weights (vehicles leaving) stay as the measure of how busy each end is.</summary>
    public static void StopUsing(Study study)
    {
        List<StudyNode> ends = study.Nodes.Where(n => n.Type == StudyNode.End).ToList();
        double total = ends.Sum(n => n.Volume ?? 0);
        study.Demand.Mode = Demand.Gravity;
        if (total > 0)
        {
            study.Demand.BaseTotal = Math.Round(total);
        }

        foreach (StudyNode n in ends)
        {
            n.Weight ??= Math.Max(1, n.Volume ?? 1);
            if (StudyEdits.OriginOf(study, $"node:{n.Id}/volume") == Origins.Fdot)
            {
                StudyEdits.Mark(study, $"node:{n.Id}/volume", Origins.Typed);
                StudyEdits.Mark(study, $"node:{n.Id}/weight", Origins.Typed);
            }
        }

        study.Sources?.Extra?.Remove("counts");
    }

    private static string[] Words(string name) =>
        name.ToUpperInvariant().Split([' ', '&', '/', '-', '(', ')', ','], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 4 && w is not ("BOULEVARD" or "STREET" or "AVENUE" or "ROAD" or "DRIVE" or "NORTH" or "SOUTH" or "EAST" or "WEST" or "RAMP" or "RAMPS"))
            .ToArray();

    private static bool Mentions(string text, string[] words) => words.Any(w => text.ToUpperInvariant().Contains(w, StringComparison.Ordinal));

    private static double PointToSegment(double px, double py, double ax, double ay, double bx, double by)
    {
        double dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy;
        double t = l2 == 0 ? 0 : Math.Clamp(((px - ax) * dx + (py - ay) * dy) / l2, 0, 1);
        return Math.Sqrt(Math.Pow(px - (ax + t * dx), 2) + Math.Pow(py - (ay + t * dy), 2));
    }
}

/// <summary>Asks FDOT's public count layer, with the same manners as the OpenStreetMap client.</summary>
public sealed class FdotClient(HttpClient http)
{
    public const string TypeInstead = "Choose \"Vehicles per hour typed for each road end\" under How traffic is given, then type them for each road end.";

    public const string NotFromMap = "Counts are found by where the roads are on the map, and this study was not made from the map (1 Map). " + TypeInstead;

    public const string NotInFlorida = "FDOT's counts cover Florida only, and this study is elsewhere. Many cities and states publish their own counts: " + TypeInstead + " Or press Estimate traffic with the AI… to have the AI estimate them for you to check.";

    public async Task<string> CountsAsync(Study study, CancellationToken cancel = default)
    {
        if (study.Geo is not { } g)
        {
            throw new OsmServiceException(NotFromMap);
        }

        if (!g.InFlorida)
        {
            throw new OsmServiceException(NotInFlorida);
        }

        (double s, double w, double n, double e) = FdotCounts.Box(study);
        try
        {
            using HttpResponseMessage r = await http.GetAsync(FdotCounts.QueryUrl(s, w, n, e), cancel).ConfigureAwait(false);
            if (r.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout)
            {
                throw new OsmServiceException("FDOT's count service is busy. Wait a minute and press the Look up button again.");
            }

            r.EnsureSuccessStatusCode();
            string json = await r.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
            _ = FdotCounts.Parse(json);
            return json;
        }
        catch (Build.StudyFormatException ex)
        {
            throw new OsmServiceException("FDOT's count service answered with something TrafficLab+ could not read. Try the Look up button again later. (Details: " + ex.Message + ")", ex);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancel.IsCancellationRequested)
        {
            throw new OsmServiceException("TrafficLab+ could not reach FDOT's count service. Check the internet connection and press the Look up button again. (Details: " + ex.Message + ")", ex);
        }
    }
}

/// <summary>FDOT's answer, kept in the study file as <c>fdot.json</c>, so an opened study shows its
/// counts without asking again.</summary>
public static class FdotCache
{
    public const string Entry = "fdot.json";

    public static byte[] ToBytes(string json, string fetched, IReadOnlyDictionary<string, (bool Use, double K, double D)>? choices = null)
    {
        var chosen = new JsonObject();
        foreach ((string end, (bool use, double k, double d)) in choices ?? new Dictionary<string, (bool, double, double)>())
        {
            chosen[end] = new JsonObject { ["use"] = use, ["k"] = k, ["d"] = d };
        }

        return Encoding.UTF8.GetBytes(new JsonObject
        {
            ["fetched"] = fetched,
            ["choices"] = chosen,
            ["answer"] = JsonNode.Parse(json),
        }.ToJsonString());
    }

    public static (string Json, string Fetched, Dictionary<string, (bool Use, double K, double D)> Choices)? FromBytes(byte[]? bytes)
    {
        if (bytes is null)
        {
            return null;
        }

        try
        {
            JsonNode? o = JsonNode.Parse(bytes);
            if (o?["answer"] is not JsonNode a)
            {
                return null;
            }

            var choices = new Dictionary<string, (bool, double, double)>(StringComparer.Ordinal);
            foreach ((string end, JsonNode? c) in o["choices"]?.AsObject() ?? [])
            {
                choices[end] = (c?["use"]?.GetValue<bool>() ?? true, c?["k"]?.GetValue<double>() ?? FdotCounts.DefaultK, c?["d"]?.GetValue<double>() ?? FdotCounts.DefaultD);
            }

            return (a.ToJsonString(), o["fetched"]?.GetValue<string>() ?? "", choices);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }
}
