namespace TrafficLabPlus.Core.Osm;

/// <summary>
/// A place where roads meet, as a person would point at it. OpenStreetMap draws a divided road as
/// two one-way roads, so one real intersection is often four points 15 m apart; a roundabout is a
/// ring of points. A junction gathers them into one.
/// </summary>
public sealed record OsmJunction(
    string Id,
    IReadOnlyList<long> NodeIds,
    double Lat,
    double Lon,
    string Label,
    IReadOnlyList<string> Roads,
    bool IsSignal,
    bool IsRoundabout,
    int Rank);

/// <summary>Finds the junctions in what Overpass returned.</summary>
public static class OsmJunctions
{
    /// <summary>Points of one junction lie within this many metres of the next.</summary>
    public const double ClusterMetres = 45;

    /// <summary>A traffic signal tagged within this distance of a junction belongs to it (OSM often
    /// puts the signal on the stop line of each approach rather than at the crossing point).</summary>
    public const double SignalMetres = 60;

    public static List<OsmJunction> Find(OsmData data)
    {
        List<OsmWay> roads = data.Ways.Where(OsmQuery.IsNetworkRoad).ToList();
        var waysAt = new Dictionary<long, List<OsmWay>>();
        foreach (OsmWay w in roads)
        {
            foreach (long n in w.Nodes.Distinct())
            {
                (waysAt.TryGetValue(n, out List<OsmWay>? list) ? list : waysAt[n] = []).Add(w);
            }
        }

        // a junction point: two different roads meet (a road split into pieces is not a junction)
        var points = waysAt
            .Where(kv => data.Nodes.ContainsKey(kv.Key) && kv.Value.Select(w => w.Label).Distinct().Count() >= 2)
            .Select(kv => kv.Key)
            .ToList();

        // roundabouts: every point of the ring is one junction
        var parent = points.ToDictionary(p => p, p => p);
        long Root(long x)
        {
            while (parent[x] != x)
            {
                x = parent[x] = parent[parent[x]];
            }

            return x;
        }

        void Union(long a, long b)
        {
            parent.TryAdd(a, a);
            parent.TryAdd(b, b);
            parent[Root(a)] = Root(b);
        }

        foreach (OsmWay ring in roads.Where(w => w.IsRoundabout))
        {
            long first = ring.Nodes[0];
            foreach (long n in ring.Nodes.Where(data.Nodes.ContainsKey))
            {
                Union(first, n);
            }
        }

        // nearby points of the same crossing (a grid, so this stays quick on a big box)
        var grid = new Dictionary<(int, int), List<long>>();
        const double cell = ClusterMetres / 111_000.0;
        foreach (long p in points)
        {
            OsmNode n = data.Nodes[p];
            (int, int) key = ((int)Math.Floor(n.Lat / cell), (int)Math.Floor(n.Lon / cell));
            (grid.TryGetValue(key, out List<long>? list) ? list : grid[key] = []).Add(p);
        }

        foreach (long p in points)
        {
            OsmNode a = data.Nodes[p];
            int ci = (int)Math.Floor(a.Lat / cell), cj = (int)Math.Floor(a.Lon / cell);
            for (int di = -1; di <= 1; di++)
            {
                for (int dj = -1; dj <= 1; dj++)
                {
                    if (!grid.TryGetValue((ci + di, cj + dj), out List<long>? near))
                    {
                        continue;
                    }

                    foreach (long q in near.Where(q => q < p && Geo.Metres(a, data.Nodes[q]) <= ClusterMetres))
                    {
                        Union(p, q);
                    }
                }
            }
        }

        List<OsmNode> signals = data.Nodes.Values.Where(n => n.Tag("highway") == "traffic_signals" && waysAt.ContainsKey(n.Id)).ToList();
        var junctions = new List<OsmJunction>();
        foreach (IGrouping<long, long> group in parent.Keys.GroupBy(Root))
        {
            List<long> ids = group.OrderBy(x => x).ToList();
            List<OsmNode> nodes = ids.Select(i => data.Nodes[i]).ToList();
            double lat = nodes.Average(n => n.Lat), lon = nodes.Average(n => n.Lon);
            List<OsmWay> here = ids.Where(waysAt.ContainsKey).SelectMany(i => waysAt[i]).Distinct().ToList();
            // named streets first (a person says "LPGA & Williamson", not "Ramp & LPGA"), busiest first
            List<string> names = here.Where(w => !w.IsRoundabout).GroupBy(w => w.Label)
                .OrderBy(g => g.Key is "Ramp" or "Unnamed road" or "Slip lane")
                .ThenByDescending(g => g.Max(w => OsmQuery.Rank(w.Highway))).ThenBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => g.Key).ToList();
            if (names.Count < 2 && !here.Any(w => w.IsRoundabout))
            {
                continue;
            }

            bool roundabout = here.Any(w => w.IsRoundabout);
            var centre = new OsmNode(0, lat, lon, new Dictionary<string, string>());
            bool signal = !roundabout && (nodes.Any(n => n.Tag("highway") == "traffic_signals")
                                          || signals.Any(s => Geo.Metres(s, centre) <= SignalMetres));
            string label = string.Join(" & ", names.Take(3)) + (roundabout ? " (roundabout)" : "");
            int rank = here.Count == 0 ? 0 : here.Select(w => OsmQuery.Rank(w.Highway)).OrderByDescending(r => r).Skip(1).FirstOrDefault();
            junctions.Add(new OsmJunction("j" + ids[0], ids, lat, lon, label, names, signal, roundabout, rank));
        }

        return junctions.OrderByDescending(j => j.Rank).ThenBy(j => j.Label, StringComparer.Ordinal).ToList();
    }
}

/// <summary>Small distances on the earth, flat enough for a few kilometres.</summary>
public static class Geo
{
    public const double MetresPerDegreeLat = 110_574;
    public const double MetresPerDegreeLon = 111_320;

    public static double Metres(OsmNode a, OsmNode b) => Metres(a.Lat, a.Lon, b.Lat, b.Lon);

    public static double Metres(double lat1, double lon1, double lat2, double lon2)
    {
        double dy = (lat2 - lat1) * MetresPerDegreeLat;
        double dx = (lon2 - lon1) * MetresPerDegreeLon * Math.Cos((lat1 + lat2) / 2 * Math.PI / 180);
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Compass bearing from one point to another, degrees clockwise from north.</summary>
    public static double Bearing(double lat1, double lon1, double lat2, double lon2)
    {
        double dy = (lat2 - lat1) * MetresPerDegreeLat;
        double dx = (lon2 - lon1) * MetresPerDegreeLon * Math.Cos((lat1 + lat2) / 2 * Math.PI / 180);
        double deg = Math.Atan2(dx, dy) * 180 / Math.PI;
        return deg < 0 ? deg + 360 : deg;
    }

    public static double AngleBetween(double a, double b)
    {
        double d = Math.Abs(a - b) % 360;
        return d > 180 ? 360 - d : d;
    }

    public static string Compass(double bearing) =>
        new[] { "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest" }[(int)Math.Round(bearing / 45) % 8];
}
