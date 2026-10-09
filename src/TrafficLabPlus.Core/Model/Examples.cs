using TrafficLabPlus.Core.Build;

namespace TrafficLabPlus.Core.Model;

/// <summary>A finished study built into TrafficLab+, to play, change and save as one's own.</summary>
public sealed record BuiltInExample(string Id, string Title, string Description, string Resource)
{
    public string Json() => WebAssets.Text(Resource);

    /// <summary>The study, with every value marked as coming from the example.</summary>
    public Study Study()
    {
        Study s = StudyJson.Read(Json());
        s.From = new Dictionary<string, string>(StringComparer.Ordinal) { ["*"] = Origins.Example };
        return s;
    }
}

/// <summary>The built-in examples, in the order the start screen offers them.</summary>
public static class Examples
{
    public static IReadOnlyList<BuiltInExample> All { get; } =
    [
        new("lpga", "LPGA Traffic Lab",
            "Dr. Ron Eaglin's LPGA Traffic Lab: the LPGA Blvd corridor at I-95 in Daytona Beach — five signals, freeway ramps, a $5M budget.",
            WebAssets.LpgaExample),
        new("four-way", "Four-Way Intersection",
            "One signal where a four-lane main street crosses a two-lane side street, with no turn lanes yet — a $2M budget. The simplest place to start.",
            WebAssets.FourWayExample),
    ];
}
