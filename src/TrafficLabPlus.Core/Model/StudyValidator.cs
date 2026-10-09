namespace TrafficLabPlus.Core.Model;

/// <summary>One thing wrong with a study: where (<c>nodes[3]</c>, <c>node I2</c>, <c>budget</c>) and,
/// in plain words, what.</summary>
public sealed record StudyProblem(string Path, string Message);

/// <summary>
/// The engine's check, in C#: the same rules and <b>the same words</b> as <c>validate</c> in
/// <c>web/core/trafficlab-core.js</c>, so the window says what the page would say. A test runs both
/// over the same broken studies and compares them; change one, change the other.
/// </summary>
public static class StudyValidator
{
    /// <summary>The most roads an intersection may have (the engine's MAX_LEGS).</summary>
    public const int MaxLegs = 4;

    public static List<StudyProblem> Check(Study study)
    {
        var errs = new List<StudyProblem>();
        void Say(string path, string message) => errs.Add(new StudyProblem(path, message));

        if (study.Format > Study.CurrentFormat)
        {
            Say("format", StudyJson.NewerFormat(study.Format));
        }

        List<StudyNode> nodes = study.Nodes;
        List<StudyLink> links = study.Links;
        if (nodes.Count == 0)
        {
            Say("nodes", "The study has no intersections or road ends.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < nodes.Count; i++)
        {
            StudyNode n = nodes[i];
            string p = $"nodes[{i}]", nm = Or(n.Name, n.Id);
            if (string.IsNullOrEmpty(n.Id))
            {
                Say(p, "A node has no id.");
            }
            else if (!ids.Add(n.Id))
            {
                Say(p, $"Two nodes are both called \"{n.Id}\". Each needs its own id.");
            }

            if (n.Type is not (StudyNode.End or StudyNode.SignalType or StudyNode.Roundabout))
            {
                Say(p, $"\"{nm}\" must be a road end, a signal or a roundabout.");
            }

            if (!double.IsFinite(n.X) || !double.IsFinite(n.Y))
            {
                Say(p, $"\"{nm}\" has no position.");
            }

            if (n.Type == StudyNode.SignalType)
            {
                Signal? s = n.Signal;
                if (s is null || !(s.Cycle >= 40 && s.Cycle <= 180))
                {
                    Say(p, $"The signal at \"{nm}\" needs a cycle between 40 and 180 seconds.");
                }

                if (s is null || !(s.Split >= 0.2 && s.Split <= 0.8))
                {
                    Say(p, $"The main street's share of green at \"{nm}\" must be between 20% and 80%.");
                }
            }
        }

        var linkIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < links.Count; i++)
        {
            StudyLink l = links[i];
            string p = $"links[{i}]", nm = Or(l.Name, l.Id);
            if (string.IsNullOrEmpty(l.Id))
            {
                Say(p, "A road segment has no id.");
            }
            else if (!linkIds.Add(l.Id))
            {
                Say(p, $"Two road segments are both called \"{l.Id}\".");
            }

            if (!ids.Contains(l.A) || !ids.Contains(l.B))
            {
                Say(p, $"Road segment \"{nm}\" joins a node that is not in the study.");
            }

            if (l.A == l.B)
            {
                Say(p, $"Road segment \"{nm}\" starts and ends at the same place.");
            }

            if (l.Lanes is < 1 or > 3)
            {
                Say(p, $"\"{nm}\" needs 1, 2 or 3 through lanes in each direction.");
            }

            if (!(l.Speed > 0))
            {
                Say(p, $"\"{nm}\" needs a speed.");
            }
        }

        foreach (StudyNode n in nodes)
        {
            // as the engine counts: a road that comes back to the same node meets it twice
            List<StudyLink> legs = links.Where(l => l.A == n.Id).Concat(links.Where(l => l.B == n.Id)).ToList();
            int k = legs.Count;
            string nm = Or(n.Name, n.Id);
            if (n.Type == StudyNode.End && k != 1)
            {
                Say($"node {n.Id}", $"The road end \"{nm}\" must have exactly one road; it has {k}.");
            }

            if (n.Type != StudyNode.End && k < 3)
            {
                Say($"node {n.Id}", $"\"{nm}\" has {k} road{(k == 1 ? "" : "s")}. An intersection needs 3 or 4.");
            }

            if (n.Type != StudyNode.End && k > MaxLegs)
            {
                Say($"node {n.Id}", $"\"{nm}\" has {k} roads. TrafficLab+ handles intersections with 3 or 4 roads; leave a road out of the study, or split the intersection in two.");
            }

            if (n.Type == StudyNode.SignalType && n.Signal?.MainLinks is { } main)
            {
                foreach (string id in main.Where(id => !legs.Any(l => l.Id == id)))
                {
                    Say($"node {n.Id}", $"The signal at \"{nm}\" names \"{id}\" as its main street, but that road does not meet it.");
                }
            }
        }

        if (nodes.Count(n => n.Type == StudyNode.End) < 2)
        {
            Say("nodes", "A study needs at least two road ends, where traffic comes in and goes out.");
        }

        foreach (string key in (study.Pockets ?? []).Keys)
        {
            string[] parts = key.Split(':');
            string tag = parts.Length > 1 ? parts[1] : "";
            if (!linkIds.Contains(parts[0]) || (tag != "ab" && tag != "ba"))
            {
                Say($"pockets.{key}", $"The turn lane \"{key}\" names an approach that is not in the study.");
            }
        }

        Demand dm = study.Demand;
        if (dm.IsVolumes)
        {
            foreach (StudyNode n in nodes.Where(n => n.Type == StudyNode.End && !(n.Volume >= 0)))
            {
                Say($"node {n.Id}", $"Type how many vehicles per hour enter at \"{Or(n.Name, n.Id)}\" (0 if none).");
            }
        }
        else if (!(dm.Od is { Count: > 0 }) && !(dm.BaseTotal > 0))
        {
            Say("demand.baseTotal", "Say how many vehicles per hour enter the whole study at today's demand.");
        }

        if (!(study.Budget > 0))
        {
            Say("budget", "The study needs a budget, in millions of dollars.");
        }

        return errs;
    }

    private static string Or(string? name, string id) => string.IsNullOrEmpty(name) ? id : name;
}
