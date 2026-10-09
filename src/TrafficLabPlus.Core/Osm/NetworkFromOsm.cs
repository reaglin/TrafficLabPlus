using System.Globalization;
using System.Text.Json.Nodes;
using TrafficLabPlus.Core.Model;

namespace TrafficLabPlus.Core.Osm;

/// <summary>What the map hands over: the chosen junctions, in order, and the study's own words.</summary>
public sealed record OsmStudyRequest
{
    public required IReadOnlyList<string> Junctions { get; init; }
    public string Title { get; init; } = "Traffic study";
    public string? Place { get; init; }
    public string? Author { get; init; }
    public string? Course { get; init; }
    public double Budget { get; init; } = StudyTemplates.SuggestedBudget;
}

/// <summary>A study made from OpenStreetMap, and what a person should know about how it was made.</summary>
public sealed record OsmStudy(Study Study, IReadOnlyList<string> Notes);

/// <summary>
/// Turns the junctions a student chose on the map into a study: each junction becomes a signal (or
/// a roundabout), each road between two chosen junctions becomes a road segment, and each road
/// leaving the group ends a short way out at a road end, where traffic comes in and goes out.
/// Lanes, turn lanes, speeds and names come from the OSM tags where they are there, and every
/// value says whether it came from OSM or is a starting value (rule 5).
/// </summary>
public static class NetworkFromOsm
{
    /// <summary>How far out a road leaving the study is drawn before it ends.</summary>
    public const double ArmMetres = 250;

    /// <summary>How far along a road TrafficLab+ looks for the next chosen junction.</summary>
    public const double SearchMetres = 2500;

    public const int MaxJunctions = 10;

    private sealed class Leg
    {
        public required OsmJunction From { get; init; }
        public required OsmWay FirstWay { get; init; }
        public required int Along { get; init; }          // +1 walking with the way's node order, −1 against
        public required List<(double Lat, double Lon)> Path { get; init; }
        public OsmJunction? To { get; set; }
        public double Bearing { get; set; }
        public bool Inbound => FirstWay.OneWay == 0 || FirstWay.OneWay == -Along;
        public bool Outbound => FirstWay.OneWay == 0 || FirstWay.OneWay == Along;
        public string Label => FirstWay.Label;
    }

    private sealed class Arm
    {
        public required OsmJunction At { get; init; }
        public List<Leg> Legs { get; } = [];
        public OsmJunction? To => Legs.Select(l => l.To).FirstOrDefault(t => t is not null);
        public string Label => Legs.GroupBy(l => l.Label).OrderByDescending(g => g.Count()).First().Key;
        public string Highway => Legs.Select(l => l.FirstWay.Highway).OrderByDescending(OsmQuery.Rank).First();
        public double Bearing => Legs[0].Bearing;
    }

    public static OsmStudy Build(OsmData data, OsmStudyRequest request)
    {
        var notes = new List<string>();
        Dictionary<string, OsmJunction> all = OsmJunctions.Find(data).ToDictionary(j => j.Id);
        List<OsmJunction> chosen = request.Junctions.Distinct().Select(id => all.GetValueOrDefault(id)).OfType<OsmJunction>().Take(MaxJunctions).ToList();
        if (chosen.Count == 0)
        {
            throw new ArgumentException("Choose at least one intersection on the map.", nameof(request));
        }

        var nodeOwner = new Dictionary<long, OsmJunction>();
        foreach (OsmJunction j in chosen)
        {
            foreach (long n in j.NodeIds)
            {
                nodeOwner[n] = j;
            }
        }

        List<OsmWay> roads = data.Ways.Where(OsmQuery.IsNetworkRoad).ToList();
        var waysAt = new Dictionary<long, List<OsmWay>>();
        foreach (OsmWay w in roads)
        {
            foreach (long n in w.Nodes.Distinct())
            {
                (waysAt.TryGetValue(n, out List<OsmWay>? list) ? list : waysAt[n] = []).Add(w);
            }
        }

        // ------------------------------------------------ walk out from each junction along each road
        var arms = new List<Arm>();
        foreach (OsmJunction j in chosen)
        {
            double exit = Math.Max(35, j.NodeIds.Max(n => Geo.Metres(j.Lat, j.Lon, data.Nodes[n].Lat, data.Nodes[n].Lon)) + 25);
            var legs = new List<Leg>();
            foreach (long start in j.NodeIds.Where(waysAt.ContainsKey))
            {
                foreach (OsmWay w in waysAt[start].Where(w => !w.IsRoundabout || !j.IsRoundabout))
                {
                    for (int i = 0; i < w.Nodes.Count; i++)
                    {
                        if (w.Nodes[i] != start)
                        {
                            continue;
                        }

                        foreach (int along in new[] { 1, -1 })
                        {
                            if (Walk(data, waysAt, nodeOwner, j, w, i, along, exit) is { } leg)
                            {
                                legs.Add(leg);
                            }
                        }
                    }
                }
            }

            // the same road leaving the same way twice (a two-way road walked from two points) is one leg
            legs = legs.GroupBy(l => (l.FirstWay.Id, l.Along, l.To?.Id, Math.Round(l.Bearing / 20))).Select(g => g.First()).ToList();

            // legs → arms: to the same chosen junction, or the same road in about the same direction
            var mine = new List<Arm>();
            foreach (Leg leg in legs.OrderBy(l => l.To is null).ThenByDescending(l => OsmQuery.Rank(l.FirstWay.Highway)))
            {
                Arm? arm = leg.To is not null
                    ? mine.FirstOrDefault(a => a.To == leg.To)
                    : mine.FirstOrDefault(a => a.To is null && a.Legs.Any(l => l.Label == leg.Label) && Geo.AngleBetween(a.Bearing, leg.Bearing) < 50);
                if (arm is null)
                {
                    arm = new Arm { At = j };
                    mine.Add(arm);
                }

                arm.Legs.Add(leg);
            }

            // ramps of a freeway interchange arrive as one-way pieces on either side of the bridge: one arm
            List<Arm> ramps = mine.Where(a => a.To is null && a.Label == "Ramp").ToList();
            for (int i = 0; i < ramps.Count; i++)
            {
                for (int k = i + 1; k < ramps.Count; k++)
                {
                    if (mine.Contains(ramps[k]) && mine.Contains(ramps[i]) && Geo.AngleBetween(ramps[i].Bearing, ramps[k].Bearing) < 60)
                    {
                        ramps[i].Legs.AddRange(ramps[k].Legs);
                        mine.Remove(ramps[k]);
                    }
                }
            }

            if (mine.Count > StudyValidator.MaxLegs)
            {
                List<Arm> keep = mine.OrderByDescending(a => a.To is not null).ThenByDescending(a => OsmQuery.Rank(a.Highway))
                    .ThenByDescending(a => a.Legs.Count).Take(StudyValidator.MaxLegs).ToList();
                foreach (Arm dropped in mine.Except(keep))
                {
                    notes.Add($"At {j.Label}: left out {dropped.Label} to the {Geo.Compass(dropped.Bearing)} — TrafficLab+ handles 3 or 4 roads at an intersection.");
                }

                mine = keep;
            }

            if (mine.Count < 3)
            {
                notes.Add($"{j.Label} has only {mine.Count} road{(mine.Count == 1 ? "" : "s")} TrafficLab+ could follow. An intersection needs 3 or 4: add a road end and a road in Network, or choose another intersection.");
            }

            arms.AddRange(mine);
        }

        // a road between two chosen junctions is counted from both ends; keep it only where both ends kept it
        foreach (Arm a in arms.Where(a => a.To is not null).ToList())
        {
            if (!arms.Any(b => b.At == a.To && b.To == a.At))
            {
                arms.Remove(a);
                notes.Add($"The road from {a.At.Label} toward {a.To!.Label} could not be followed all the way there, so it stops at a road end (an edge of the study, where cars come in and go out).");
                var copy = new Arm { At = a.At };
                foreach (Leg l in a.Legs)
                {
                    l.To = null;
                    copy.Legs.Add(l);
                }

                arms.Add(copy);
            }
        }

        // ------------------------------------------------ the study
        double lat0 = chosen.Average(j => j.Lat), lon0 = chosen.Average(j => j.Lon);
        (double X, double Y) Project(double lat, double lon) =>
            ((lon - lon0) * Geo.MetresPerDegreeLon * Math.Cos(lat0 * Math.PI / 180), (lat0 - lat) * Geo.MetresPerDegreeLat);

        var study = new Study
        {
            Title = string.IsNullOrWhiteSpace(request.Title) ? "Traffic study" : request.Title.Trim(),
            Place = Blank(request.Place),
            Author = Blank(request.Author),
            Course = Blank(request.Course),
            Budget = request.Budget > 0 ? request.Budget : StudyTemplates.SuggestedBudget,
            Costs = Costs.Lpga(),
            Demand = new Demand
            {
                Mode = Demand.Gravity,
                BaseTotal = 2400 + 300 * (chosen.Count - 1),
                Scenarios = [new Scenario { Name = "Today", Mult = 1 }, new Scenario { Name = "In 10 years", Mult = 1.25 }, new Scenario { Name = "Busy day", Mult = 1.5 }],
            },
            Sources = new Sources { Osm = true },
            From = new Dictionary<string, string>(StringComparer.Ordinal) { ["*"] = Origins.Default },
        };
        study.Sources.Extra = new() { ["network"] = System.Text.Json.JsonSerializer.SerializeToElement("OpenStreetMap, " + DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) };

        // the road most of the junctions share is the corridor; each junction's short name is its other road
        string? corridor = chosen.Count > 1 ? chosen.SelectMany(j => j.Roads).GroupBy(r => r).OrderByDescending(g => g.Count()).First().Key : null;
        var ids = new Dictionary<OsmJunction, string>();
        for (int i = 0; i < chosen.Count; i++)
        {
            OsmJunction j = chosen[i];
            string id = "I" + (i + 1).ToString(CultureInfo.InvariantCulture);
            ids[j] = id;
            (double x, double y) = Project(j.Lat, j.Lon);
            string type = j.IsRoundabout ? StudyNode.Roundabout : StudyNode.SignalType;
            string? shortName = j.Roads.FirstOrDefault(r => r != corridor && r is not ("Ramp" or "Unnamed road"))
                                ?? (j.Roads.Contains("Ramp") ? "Ramps" : null) ?? j.Roads.FirstOrDefault();
            // named after the roads it keeps: one left out (a fifth road) is not in its name
            var kept = arms.Where(a => a.At == j).Select(a => a.Label).ToHashSet(StringComparer.Ordinal);
            List<string> roadsKept = j.Roads.Where(kept.Contains).Take(3).ToList();
            string name = roadsKept.Count >= 2 ? string.Join(" & ", roadsKept) : j.Label.Replace(" (roundabout)", "", StringComparison.Ordinal);
            if (shortName is not null && !kept.Contains(shortName) && shortName != "Ramps")
            {
                shortName = roadsKept.FirstOrDefault(r => r != corridor) ?? shortName;
            }

            var node = new StudyNode { Id = id, Type = type, Name = name, Short = shortName, X = x, Y = y };
            if (type == StudyNode.SignalType)
            {
                node.Signal = new Signal { Cycle = 90, Split = 0.6 };
            }

            study.Nodes.Add(node);
            StudyEdits.Mark(study, $"node:{id}/name", Origins.Osm);
            if (j.IsSignal || j.IsRoundabout)
            {
                StudyEdits.Mark(study, $"node:{id}/type", Origins.Osm);
            }
            else
            {
                notes.Add($"OpenStreetMap shows no traffic signal at {j.Label}. TrafficLab+ starts it as a signal (stop signs are not in the model); check the real intersection.");
            }
        }

        int ends = 0, links = 0;
        foreach (Arm arm in arms)
        {
            string at = ids[arm.At];
            string other;
            if (arm.To is { } to)
            {
                if (string.CompareOrdinal(ids[arm.At], ids[to]) > 0)
                {
                    continue;   // made from the other end
                }

                other = ids[to];
            }
            else
            {
                List<(double Lat, double Lon)> path = arm.Legs.OrderByDescending(l => Length(l.Path)).First().Path;
                (double lat, double lon) = PointAlong(path, ArmMetres);
                if (Length(path) < 120)
                {
                    notes.Add($"{arm.Label} to the {Geo.Compass(arm.Bearing)} of {arm.At.Label} leaves the loaded area after {Math.Round(Units.Feet(Length(path)) / 10) * 10:0} ft, so its road end is close in. Drag the road end out in Network, or load a bigger area.");
                }
                (double x, double y) = Project(lat, lon);
                other = "E" + (++ends).ToString(CultureInfo.InvariantCulture);
                study.Nodes.Add(new StudyNode
                {
                    Id = other,
                    Type = StudyNode.End,
                    Name = $"{arm.Label} ({Geo.Compass(arm.Bearing)})",
                    X = x,
                    Y = y,
                    Weight = OsmQuery.Weight(arm.Highway),
                });
                StudyEdits.Mark(study, $"node:{other}/name", Origins.Osm);
            }

            Arm? back = arm.To is { } t2 ? arms.First(b => b.At == t2 && b.To == arm.At) : null;
            string id = "L" + (++links).ToString(CultureInfo.InvariantCulture);
            var link = new StudyLink { Id = id, A = at, B = other, Name = arm.Label };
            StudyEdits.Mark(study, $"link:{id}/name", Origins.Osm);

            // lanes: the approach to each end, read from the road right at the junction
            (int? intoAt, bool lAt, bool rAt) = Approach(arm.Legs.Where(l => l.Inbound));
            (int? intoOther, bool lOther, bool rOther) = back is null ? Approach(arm.Legs.Where(l => l.Outbound)) : Approach(back.Legs.Where(l => l.Inbound));
            int? lanes = new[] { intoAt, intoOther }.Max();
            link.Lanes = Math.Clamp(lanes ?? (OsmQuery.Rank(arm.Highway) >= 5 && !arm.Highway.EndsWith("_link", StringComparison.Ordinal) ? 2 : 1), 1, 3);
            if (lanes is not null)
            {
                StudyEdits.Mark(study, $"link:{id}/lanes", Origins.Osm);
            }

            if (lanes > 3)
            {
                notes.Add($"{arm.Label} has {lanes} through lanes each way in OpenStreetMap; TrafficLab+ simulates up to 3.");
            }

            double? mph = arm.Legs.Concat(back?.Legs ?? []).Select(l => OsmQuery.MaxSpeedMph(l.FirstWay.Tag("maxspeed"))).Max();
            link.Speed = Math.Round(Units.Mps(mph ?? OsmQuery.DefaultSpeedMph(arm.Highway)), 4);
            if (mph is not null)
            {
                StudyEdits.Mark(study, $"link:{id}/speed", Origins.Osm);
            }

            study.Links.Add(link);

            // turn lanes on the approaches into a signal: "ba" arrives at A (the junction this arm is at)
            if (study.Node(at)!.Type == StudyNode.SignalType && (lAt || rAt))
            {
                StudyEdits.SetPocket(study, id, "ba", lAt, rAt);
                StudyEdits.Mark(study, $"link:{id}/pockets:ba", Origins.Osm);
            }

            if (back is not null && study.Node(other)!.Type == StudyNode.SignalType && (lOther || rOther))
            {
                StudyEdits.SetPocket(study, id, "ab", lOther, rOther);
                StudyEdits.Mark(study, $"link:{id}/pockets:ab", Origins.Osm);
            }
        }

        // ------------------------------------------------ the backdrop: the streets around, never simulated
        double minX = study.Nodes.Min(n => n.X) - 120, maxX = study.Nodes.Max(n => n.X) + 120;
        double minY = study.Nodes.Min(n => n.Y) - 120, maxY = study.Nodes.Max(n => n.Y) + 120;
        var streets = new JsonArray();
        var freeways = new JsonArray();
        foreach (OsmWay w in data.Ways.Where(w => w.Highway is "residential" or "unclassified" or "living_street" or "motorway" or "trunk" or "tertiary"))
        {
            var pts = new JsonArray();
            bool inside = false;
            foreach (long n in w.Nodes)
            {
                if (!data.Nodes.TryGetValue(n, out OsmNode? p))
                {
                    continue;
                }

                (double x, double y) = Project(p.Lat, p.Lon);
                inside |= x >= minX && x <= maxX && y >= minY && y <= maxY;
                pts.Add(new JsonArray(Math.Round(x, 1), Math.Round(y, 1)));
            }

            if (!inside || pts.Count < 2)
            {
                continue;
            }

            if (w.Highway is "motorway" or "trunk")
            {
                freeways.Add(new JsonObject { ["points"] = pts, ["width"] = 18 });
            }
            else
            {
                streets.Add(pts);
            }
        }

        // move everything so the drawing starts near (40, 40)
        double dx = 40 - study.Nodes.Min(n => n.X), dy = 40 - study.Nodes.Min(n => n.Y);
        foreach (StudyNode n in study.Nodes)
        {
            n.X = Math.Round(n.X + dx, 1);
            n.Y = Math.Round(n.Y + dy, 1);
        }

        // the local frame's place on the earth, so counts can be looked up later
        study.Geo = new GeoAnchor { Lat = lat0, Lon = lon0, X = dx, Y = dy };

        foreach (JsonArray line in streets.OfType<JsonArray>().Concat(freeways.OfType<JsonObject>().Select(f => (JsonArray)f["points"]!)))
        {
            foreach (JsonArray p in line.OfType<JsonArray>())
            {
                p[0] = Math.Round(p[0]!.GetValue<double>() + dx, 1);
                p[1] = Math.Round(p[1]!.GetValue<double>() + dy, 1);
            }
        }

        if (streets.Count > 0 || freeways.Count > 0)
        {
            study.Backdrop = new JsonObject { ["streets"] = streets, ["freeways"] = freeways };
        }

        foreach (StudyNode n in study.Nodes.Where(n => n.Type == StudyNode.End))
        {
            StudyEdits.Mark(study, $"node:{n.Id}/position", Origins.Default);
        }

        return new OsmStudy(study, notes);
    }

    private static Leg? Walk(OsmData data, Dictionary<long, List<OsmWay>> waysAt, Dictionary<long, OsmJunction> owner,
                             OsmJunction from, OsmWay way, int index, int along, double exit)
    {
        var path = new List<(double Lat, double Lon)> { (from.Lat, from.Lon) };
        OsmWay w = way;
        int i = index, dir = along;
        double travelled = 0;
        bool outside = false;
        var seenWays = new HashSet<long> { w.Id };
        (double Lat, double Lon) last = (from.Lat, from.Lon);
        double? bearing = null;
        while (true)
        {
            int next = i + dir;
            if (next < 0 || next >= w.Nodes.Count)
            {
                // the way ends here: carry on along the one road that continues it, if there is one
                long endNode = w.Nodes[i];
                OsmWay current = w;
                List<OsmWay> onward = (waysAt.GetValueOrDefault(endNode) ?? [])
                    .Where(o => !seenWays.Contains(o.Id) && o.Label == current.Label && (o.Nodes[0] == endNode || o.Nodes[^1] == endNode)).ToList();
                if (onward.Count != 1)
                {
                    break;
                }

                w = onward[0];
                seenWays.Add(w.Id);
                i = w.Nodes[0] == endNode ? 0 : w.Nodes.Count - 1;
                dir = i == 0 ? 1 : -1;
                continue;
            }

            i = next;
            if (!data.Nodes.TryGetValue(w.Nodes[i], out OsmNode? n))
            {
                break;
            }

            travelled += Geo.Metres(last.Lat, last.Lon, n.Lat, n.Lon);
            last = (n.Lat, n.Lon);
            path.Add(last);
            double fromCentre = Geo.Metres(from.Lat, from.Lon, n.Lat, n.Lon);
            if (owner.TryGetValue(n.Id, out OsmJunction? hit))
            {
                if (hit == from)
                {
                    if (!outside)
                    {
                        return null;   // inside the junction itself: from one of its points to another
                    }

                    break;             // came back round
                }

                return new Leg { From = from, FirstWay = way, Along = along, Path = path, To = hit, Bearing = bearing ?? Geo.Bearing(from.Lat, from.Lon, n.Lat, n.Lon) };
            }

            if (!outside && fromCentre > exit)
            {
                outside = true;
                bearing = Geo.Bearing(from.Lat, from.Lon, n.Lat, n.Lon);
            }

            if (travelled > SearchMetres)
            {
                break;
            }
        }

        if (!outside || travelled < 40)
        {
            return null;   // a stub inside the junction, or a road that stops at once
        }

        return new Leg { From = from, FirstWay = way, Along = along, Path = path, Bearing = bearing ?? 0 };
    }

    /// <summary>Through lanes and turn lanes on the approach, from the road right at the junction.</summary>
    private static (int? Through, bool Left, bool Right) Approach(IEnumerable<Leg> legs)
    {
        int? through = null;
        bool left = false, right = false;
        foreach (Leg l in legs)
        {
            OsmWay w = l.FirstWay;
            bool twoWay = w.OneWay == 0;
            // walking out against the node order means the traffic coming in travels with it: "forward"
            string side = l.Along == -1 ? "forward" : "backward";
            string? turn = twoWay ? w.Tag("turn:lanes:" + side) : w.Tag("turn:lanes");
            int? count = twoWay
                ? Int(w.Tag("lanes:" + side)) ?? (Int(w.Tag("lanes")) is int all ? Math.Max(1, all / 2) : null)
                : Int(w.Tag("lanes"));
            if (OsmQuery.TurnLanes(turn) is { } t)
            {
                count = Math.Max(1, t.Through);
                left |= t.Left;
                right |= t.Right;
            }

            if (count is int c)
            {
                through = Math.Max(through ?? 0, c);
            }
        }

        return (through, left, right);
    }

    private static int? Int(string? s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v > 0 ? v : null;

    private static double Length(List<(double Lat, double Lon)> path) =>
        path.Zip(path.Skip(1), (a, b) => Geo.Metres(a.Lat, a.Lon, b.Lat, b.Lon)).Sum();

    private static (double Lat, double Lon) PointAlong(List<(double Lat, double Lon)> path, double metres)
    {
        double gone = 0;
        for (int i = 1; i < path.Count; i++)
        {
            double step = Geo.Metres(path[i - 1].Lat, path[i - 1].Lon, path[i].Lat, path[i].Lon);
            if (gone + step >= metres && step > 0)
            {
                double f = (metres - gone) / step;
                return (path[i - 1].Lat + f * (path[i].Lat - path[i - 1].Lat), path[i - 1].Lon + f * (path[i].Lon - path[i - 1].Lon));
            }

            gone += step;
        }

        return path[^1];
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
