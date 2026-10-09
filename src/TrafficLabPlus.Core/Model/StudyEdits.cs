namespace TrafficLabPlus.Core.Model;

/// <summary>
/// The changes the editor makes to a study, kept here so they are tested and so a removal never
/// leaves a dangling name behind (a turn lane on a road that is gone, a main street that no longer
/// meets its signal, a no-trips pair naming a removed end).
/// </summary>
public static class StudyEdits
{
    public const double DefaultSpeed = 13.4112;   // 30 mph
    public const double DefaultEndWeight = 500;

    /// <summary>The next unused id with this prefix: I1, I2… for intersections, E1, E2… for road
    /// ends, L1, L2… for roads.</summary>
    public static string NextId(Study study, string prefix)
    {
        var used = new HashSet<string>(study.Nodes.Select(n => n.Id).Concat(study.Links.Select(l => l.Id)), StringComparer.Ordinal);
        for (int i = 1; ; i++)
        {
            if (!used.Contains(prefix + i))
            {
                return prefix + i;
            }
        }
    }

    public static StudyNode AddNode(Study study, string type, double x, double y)
    {
        bool end = type == StudyNode.End;
        string id = NextId(study, end ? "E" : "I");
        var node = new StudyNode
        {
            Id = id,
            Type = type,
            Name = end ? "Road end " + id[1..] : "Intersection " + id[1..],
            X = Math.Round(x, 1),
            Y = Math.Round(y, 1),
        };
        SetType(study, node, type);
        study.Nodes.Add(node);
        return node;
    }

    /// <summary>Joins two nodes with a road. Returns null, and why, when it cannot.</summary>
    public static (StudyLink? Link, string? Why) AddLink(Study study, string a, string b)
    {
        StudyNode? na = study.Node(a), nb = study.Node(b);
        if (na is null || nb is null)
        {
            return (null, "Both ends of a road must be in the study.");
        }

        if (a == b)
        {
            return (null, "A road joins two different places. Pick another one.");
        }

        if (study.Links.Any(l => (l.A == a && l.B == b) || (l.A == b && l.B == a)))
        {
            return (null, $"\"{na.Label}\" and \"{nb.Label}\" are already joined by a road.");
        }

        if (na.Type == StudyNode.End && study.LinksAt(a).Any())
        {
            return (null, $"\"{na.Label}\" is a road end, and a road end has only one road. Use an intersection to join more.");
        }

        if (nb.Type == StudyNode.End && study.LinksAt(b).Any())
        {
            return (null, $"\"{nb.Label}\" is a road end, and a road end has only one road. Use an intersection to join more.");
        }

        foreach (StudyNode n in new[] { na, nb }.Where(n => n.Type != StudyNode.End && study.LinksAt(n.Id).Count() >= StudyValidator.MaxLegs))
        {
            return (null, $"\"{n.Label}\" already has {StudyValidator.MaxLegs} roads, the most TrafficLab+ handles at one intersection.");
        }

        // a road named like the roads it continues: the other road at an end, or nothing
        string? name = study.LinksAt(a).Concat(study.LinksAt(b)).Select(l => l.Name).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
        var link = new StudyLink { Id = NextId(study, "L"), A = a, B = b, Name = name ?? "New road", Lanes = 1, Speed = DefaultSpeed };
        study.Links.Add(link);
        return (link, null);
    }

    /// <summary>Renames a street everywhere it appears: every road with that name, and the names of
    /// the intersections and road ends that carry it ("Main Street & Cross Street 2", "Cross Street 2
    /// (north)"). Returns how many roads and how many other names changed.</summary>
    public static (int Roads, int Names) RenameStreet(Study study, string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName) || oldName == newName)
        {
            return (0, 0);
        }

        int roads = 0, names = 0;
        foreach (StudyLink l in study.Links.Where(l => l.Name == oldName))
        {
            l.Name = newName;
            roads++;
        }

        foreach (StudyNode n in study.Nodes)
        {
            bool inName = n.Name is not null && ContainsWord(n.Name, oldName);
            bool inShort = n.Short is not null && ContainsWord(n.Short, oldName);
            if (inName)
            {
                n.Name = ReplaceWord(n.Name!, oldName, newName);
            }

            if (inShort)
            {
                n.Short = ReplaceWord(n.Short!, oldName, newName);
            }

            if (inName || inShort)
            {
                names++;
            }
        }

        return (roads, names);
    }

    // a whole-word match, so renaming "Cross Street 1" leaves "Cross Street 10" alone
    private static bool ContainsWord(string text, string word) =>
        System.Text.RegularExpressions.Regex.IsMatch(text, @"(?<![\w])" + System.Text.RegularExpressions.Regex.Escape(word) + @"(?![\w])");

    private static string ReplaceWord(string text, string word, string with) =>
        System.Text.RegularExpressions.Regex.Replace(text, @"(?<![\w])" + System.Text.RegularExpressions.Regex.Escape(word) + @"(?![\w])", with.Replace("$", "$$", StringComparison.Ordinal));

    public static void RemoveLink(Study study, string id)
    {
        study.Links.RemoveAll(l => l.Id == id);
        study.Pockets?.Remove(id + ":ab");
        study.Pockets?.Remove(id + ":ba");
        if (study.Pockets is { Count: 0 })
        {
            study.Pockets = null;
        }

        foreach (StudyNode n in study.Nodes.Where(n => n.Signal?.MainLinks is not null))
        {
            n.Signal!.MainLinks!.Remove(id);
            if (n.Signal.MainLinks.Count == 0)
            {
                n.Signal.MainLinks = null;
            }
        }

        Forget(study, "link:" + id + "/");
    }

    public static void RemoveNode(Study study, string id)
    {
        foreach (string link in study.LinksAt(id).Select(l => l.Id).ToList())
        {
            RemoveLink(study, link);
        }

        study.Nodes.RemoveAll(n => n.Id == id);
        study.Demand.NoTrips?.RemoveAll(pair => pair.Contains(id));
        study.Demand.Od?.RemoveAll(r => r.O == id || r.D == id);
        Forget(study, "node:" + id + "/");
    }

    /// <summary>Makes a node a road end, a signal or a roundabout, giving it what that kind needs
    /// (a signal plan; a weight) and dropping what it no longer has.</summary>
    public static void SetType(Study study, StudyNode node, string type)
    {
        node.Type = type;
        if (type == StudyNode.SignalType)
        {
            node.Signal ??= new Signal { Cycle = 90, Split = 0.6 };
        }
        else
        {
            node.Signal = null;
        }

        if (type == StudyNode.End)
        {
            node.Weight ??= DefaultEndWeight;
            if (study.Demand.IsVolumes)
            {
                node.Volume ??= 0;
            }
        }
        else
        {
            node.Weight = null;
            node.Volume = null;
        }
    }

    /// <summary>The turn lanes that exist today on one approach (<c>dir</c> is <c>ab</c> or <c>ba</c>).</summary>
    public static (bool Left, bool Right) GetPocket(Study study, string linkId, string dir) =>
        study.Pockets is not null && study.Pockets.TryGetValue(linkId + ":" + dir, out Pocket? p)
            ? ((p.L ?? 0) > 0, (p.R ?? 0) > 0)
            : (false, false);

    public static void SetPocket(Study study, string linkId, string dir, bool left, bool right)
    {
        string key = linkId + ":" + dir;
        study.Pockets ??= [];
        study.Pockets.TryGetValue(key, out Pocket? p);
        p ??= new Pocket();
        p.L = left ? 1 : null;
        p.R = right ? 1 : null;
        if (p.IsEmpty)
        {
            study.Pockets.Remove(key);
        }
        else
        {
            study.Pockets[key] = p;
        }

        if (study.Pockets.Count == 0)
        {
            study.Pockets = null;
        }
    }

    /// <summary>Where a value came from: its own mark, else the study's (<c>*</c>), else nothing.</summary>
    public static string? OriginOf(Study study, string key) =>
        study.From is null ? null
        : study.From.TryGetValue(key, out string? o) ? o
        : study.From.TryGetValue("*", out string? all) ? all
        : null;

    public static void Mark(Study study, string key, string origin)
    {
        study.From ??= new Dictionary<string, string>(StringComparer.Ordinal);
        study.From[key] = origin;
    }

    private static void Forget(Study study, string prefix)
    {
        if (study.From is null)
        {
            return;
        }

        foreach (string key in study.From.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            study.From.Remove(key);
        }
    }
}

/// <summary>What the engine will do with the study's demand, for the editor to show beside it.</summary>
public static class DemandShares
{
    /// <summary>Vehicles per hour entering at each road end at today's demand — the engine's own
    /// sums (<c>odMatrix</c> in trafficlab-core.js): gravity shares trips by weight × weight
    /// between ends, leaving out the no-trips pairs; typed volumes enter as typed.</summary>
    public static Dictionary<string, double> Entering(Study study)
    {
        List<StudyNode> ends = study.Nodes.Where(n => n.Type == StudyNode.End).ToList();
        var result = ends.ToDictionary(n => n.Id, _ => 0.0, StringComparer.Ordinal);
        Demand dm = study.Demand;
        if (dm.Od is { Count: > 0 })
        {
            foreach (OdRow r in dm.Od.Where(r => result.ContainsKey(r.O)))
            {
                result[r.O] += r.V;
            }

            return result;
        }

        bool Banned(string o, string d) => o == d || (dm.NoTrips ?? []).Any(p => p.Count == 2 && ((p[0] == o && p[1] == d) || (p[0] == d && p[1] == o)));
        if (dm.IsVolumes)
        {
            foreach (StudyNode o in ends)
            {
                bool anywhere = ends.Any(d => !Banned(o.Id, d.Id) && (d.Weight ?? d.Volume ?? 0) > 0);
                result[o.Id] = anywhere ? o.Volume ?? 0 : 0;
            }

            return result;
        }

        double sum = 0;
        foreach (StudyNode o in ends)
        {
            foreach (StudyNode d in ends.Where(d => !Banned(o.Id, d.Id)))
            {
                double w = (o.Weight ?? 1) * (d.Weight ?? 1);
                result[o.Id] += w;
                sum += w;
            }
        }

        foreach (string id in result.Keys.ToList())
        {
            result[id] = sum > 0 ? (dm.BaseTotal ?? 0) * result[id] / sum : 0;
        }

        return result;
    }
}
