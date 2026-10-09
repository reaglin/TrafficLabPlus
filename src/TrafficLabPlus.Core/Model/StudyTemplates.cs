namespace TrafficLabPlus.Core.Model;

/// <summary>What the New study window asks.</summary>
public sealed record NewStudyRequest
{
    public string Title { get; init; } = "";
    public string? Place { get; init; }
    public string? Author { get; init; }
    public string? Course { get; init; }

    /// <summary>How many signals along the main street, 1 to <see cref="StudyTemplates.MaxSignals"/>.</summary>
    public int Signals { get; init; } = 1;

    /// <summary>One signal with no road to the south: a T intersection.</summary>
    public bool Tee { get; init; }

    /// <summary>The plan budget, $M (Ron, 2026-10-09: a new study asks for it).</summary>
    public double Budget { get; init; } = StudyTemplates.SuggestedBudget;

    public string MainStreet { get; init; } = "Main Street";
    public string CrossStreet { get; init; } = "Cross Street";
}

/// <summary>
/// Starting networks for a new study, until the map (phase 4) can bring in the real roads: one
/// signal (four roads or a T) or a corridor of signals along a main street, laid out on a plain
/// grid. Every value is marked as a starting value, for the student to change to the real road.
/// </summary>
public static class StudyTemplates
{
    public const int MaxSignals = 10;
    public const double SuggestedBudget = 5;   // LPGA's
    public const double Spacing = 400;         // m between signals, about a quarter mile
    public const double SideLength = 300;      // m from the main street to a side road's end

    public static Study Create(NewStudyRequest r)
    {
        int n = Math.Clamp(r.Signals, 1, MaxSignals);
        bool tee = r.Tee && n == 1;
        string main = Or(r.MainStreet, "Main Street"), cross = Or(r.CrossStreet, "Cross Street");
        var s = new Study
        {
            Title = Or(r.Title, "Traffic study"),
            Place = Blank(r.Place),
            Author = Blank(r.Author),
            Course = Blank(r.Course),
            Budget = r.Budget > 0 ? r.Budget : SuggestedBudget,
            Costs = Costs.Lpga(),
            Demand = new Demand
            {
                Mode = Demand.Gravity,
                BaseTotal = 2400 + 300 * (n - 1),   // Today moderately busy (worst v/c about 0.75); Busy day past capacity
                Scenarios =
                [
                    new Scenario { Name = "Today", Mult = 1 },
                    new Scenario { Name = "In 10 years", Mult = 1.25 },
                    new Scenario { Name = "Busy day", Mult = 1.5 },
                ],
            },
            From = new Dictionary<string, string>(StringComparer.Ordinal) { ["*"] = Origins.Default },
        };

        double y = SideLength, x0 = SideLength;
        s.Nodes.Add(new StudyNode { Id = "W", Type = StudyNode.End, Name = main + " (west)", X = 0, Y = y, Weight = 900 });
        string prev = "W";
        for (int i = 1; i <= n; i++)
        {
            double x = x0 + (i - 1) * Spacing;
            string id = "I" + i, crossName = n == 1 ? cross : cross + " " + i;
            s.Nodes.Add(new StudyNode
            {
                Id = id,
                Type = StudyNode.SignalType,
                Name = main + " & " + crossName,
                Short = crossName,
                X = x,
                Y = y,
                Signal = new Signal { Cycle = 90, Split = 0.6 },
            });
            s.Links.Add(MainLink(s, prev, id, main));
            s.Nodes.Add(new StudyNode { Id = "N" + i, Type = StudyNode.End, Name = crossName + " (north)", X = x, Y = 0, Weight = 350 });
            s.Links.Add(new StudyLink { Id = "C" + i + "N", A = "N" + i, B = id, Name = crossName, Lanes = 1, Speed = StudyEdits.DefaultSpeed });
            if (!tee)
            {
                s.Nodes.Add(new StudyNode { Id = "S" + i, Type = StudyNode.End, Name = crossName + " (south)", X = x, Y = 2 * y, Weight = 350 });
                s.Links.Add(new StudyLink { Id = "C" + i + "S", A = id, B = "S" + i, Name = crossName, Lanes = 1, Speed = StudyEdits.DefaultSpeed });
            }

            prev = id;
        }

        s.Nodes.Add(new StudyNode { Id = "E", Type = StudyNode.End, Name = main + " (east)", X = x0 + (n - 1) * Spacing + SideLength, Y = y, Weight = 900 });
        s.Links.Add(MainLink(s, prev, "E", main));

        // the main street is each signal's main street, named rather than guessed
        for (int i = 1; i <= n; i++)
        {
            string id = "I" + i;
            s.Node(id)!.Signal!.MainLinks = s.Links.Where(l => (l.A == id || l.B == id) && l.Id.StartsWith('M')).Select(l => l.Id).ToList();
        }

        return s;
    }

    private static StudyLink MainLink(Study s, string a, string b, string name) =>
        new() { Id = "M" + (s.Links.Count(l => l.Id.StartsWith('M')) + 1), A = a, B = b, Name = name, Lanes = 2, Speed = 17.8816 };   // 40 mph

    private static string Or(string? text, string fallback) => string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
