using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using TrafficLabPlus.Core.Model;

namespace TrafficLabPlus.Core.Ai;

/// <summary>One change the AI proposes: what, from what to what, why — applied only if the student
/// ticks it (rule 4: the AI proposes; the student accepts).</summary>
public sealed class AiProposal
{
    public required string Target { get; init; }
    public required string Setting { get; init; }
    public required string Before { get; init; }
    public required string After { get; init; }
    public string Reason { get; init; } = "";

    /// <summary>The value's origin key (<c>link:L1/lanes</c>); it is marked "suggested by the AI".</summary>
    public string? Key { get; init; }
    public required Action<Study> Apply { get; init; }
    public bool Use { get; set; } = true;
}

/// <summary>What came back: the changes worth showing, and what was left out and why.</summary>
public sealed record AiProposals(List<AiProposal> Changes, List<string> LeftOut, string Notes);

/// <summary>
/// The four things TrafficLab+ asks an AI (plan D4): build the network, estimate the traffic,
/// write the challenge, and coach. Each prompt leads with who is asking and why (Gamify+'s lesson:
/// a prompt that might be questioned leads with its context), gives the study as compact JSON,
/// and asks for one JSON shape. Each reader checks every proposed value against the study and the
/// engine's limits, and leaves out — saying why — anything that does not fit.
/// </summary>
public static class AiPrompts
{
    public const string System = "You help university civil-engineering students build traffic simulation studies in a teaching program called TrafficLab+. " +
                                 "Answer with the JSON asked for and nothing else: no explanation before it, no commentary after it.";

    public const string CoachSystem = "You are a friendly, precise transportation-engineering tutor helping a university student with a traffic simulation lab. " +
                                      "Answer in plain English, in short paragraphs, with no headings and no tables.";

    // ------------------------------------------------------------ the study, as the AI sees it

    /// <summary>The study in a few lines of JSON, in the units people use (mph, ft, %, $M).</summary>
    public static string Describe(Study s)
    {
        string Where(StudyLink l, string dir) => dir == "ab" ? s.Node(l.B)?.Label ?? l.B : s.Node(l.A)?.Label ?? l.A;
        var o = new JsonObject
        {
            ["title"] = s.Title,
            ["place"] = s.Place,
            ["budgetMillions"] = s.Budget,
            ["roadsFromOpenStreetMap"] = s.Sources?.Osm == true,
            ["intersections"] = new JsonArray(s.Nodes.Where(n => n.Type != StudyNode.End).Select(n => (JsonNode)new JsonObject
            {
                ["id"] = n.Id,
                ["name"] = n.Label,
                ["kind"] = n.Type,
                ["cycleSeconds"] = n.Signal?.Cycle,
                ["mainStreetGreenPercent"] = n.Signal is { } g ? Math.Round(g.Split * 100) : null,
                ["protectedLeftMain"] = n.Signal?.ProtMain,
                ["protectedLeftSide"] = n.Signal?.ProtSide,
                ["roads"] = new JsonArray(s.LinksAt(n.Id).Select(l => (JsonNode)l.Id).ToArray()),
            }).ToArray()),
            ["roadEnds"] = new JsonArray(s.Nodes.Where(n => n.Type == StudyNode.End).Select(n => (JsonNode)new JsonObject
            {
                ["id"] = n.Id,
                ["name"] = n.Label,
                ["enteringPerHourNow"] = Math.Round(DemandShares.Entering(s).GetValueOrDefault(n.Id)),
                ["volumeFrom"] = Origins.Describe(StudyEdits.OriginOf(s, $"node:{n.Id}/volume")),
            }).ToArray()),
            ["roads"] = new JsonArray(s.Links.Select(l => (JsonNode)new JsonObject
            {
                ["id"] = l.Id,
                ["name"] = l.Title,
                ["from"] = s.Node(l.A)?.Label,
                ["to"] = s.Node(l.B)?.Label,
                ["throughLanesEachWay"] = l.Lanes,
                ["speedMph"] = Math.Round(Units.Mph(l.Speed)),
                ["lengthFt"] = s.Node(l.A) is { } a && s.Node(l.B) is { } b ? Math.Round(Units.Feet(Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2)))) : null,
                ["turnLanesToday"] = new JsonArray(new[] { "ab", "ba" }.SelectMany(d =>
                {
                    (bool left, bool right) = StudyEdits.GetPocket(s, l.Id, d);
                    var list = new List<JsonNode>();
                    if (left)
                    {
                        list.Add("left turn lane arriving at " + Where(l, d));
                    }

                    if (right)
                    {
                        list.Add("right turn lane arriving at " + Where(l, d));
                    }

                    return list;
                }).ToArray()),
            }).ToArray()),
        };
        // a prompt is not HTML: "LPGA & Williamson" must reach the AI as written, not escaped
        return o.ToJsonString(new JsonSerializerOptions { WriteIndented = false, Encoder = global::System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    // ------------------------------------------------------------ 1. build the network

    public static string Network(Study s, string words) => $$"""
        A student is building a traffic simulation of a real place for a class lab. The roads came from a map; OpenStreetMap often lacks
        lane counts, turn lanes, speed limits and signal timing, and the student knows things about the real intersections. Using what
        the student says below — and your general knowledge of typical US arterials only where the student says nothing — propose
        changes to the study so it matches the real place. Change only what there is a reason to change.

        The student says:
        [start of their words]
        {{words.Trim()}}
        [end of their words]

        The study now (ids are what you refer to):
        {{Describe(s)}}

        Answer with JSON only, in this shape:
        {"changes": [ {"id": "<an intersection or road id from the study>", "setting": "<one of the settings below>", "value": <the new value>, "reason": "<one sentence a student understands>"} ],
         "notes": "<one or two sentences: anything the student should check or could not be decided>"}

        Settings for an intersection: "cycleSeconds" (40 to 180), "mainStreetGreenPercent" (20 to 80), "protectedLeftMain" (true/false),
        "protectedLeftSide" (true/false), "kind" ("signal" or "roundabout", only one that exists today), "name" (text).
        Settings for a road: "throughLanesEachWay" (1 to 3; turn lanes are separate), "speedMph" (10 to 75), "name" (text),
        "leftTurnLaneAt" (the id of the intersection at one end of the road: a left-turn lane exists today on the road arriving there),
        "rightTurnLaneAt" (the same, for a right-turn lane).
        At most 25 changes.
        """;

    public static AiProposals ReadNetwork(Study s, string answer)
    {
        JsonObject o = AiReader.Object(answer, "changes");
        var changes = new List<AiProposal>();
        var leftOut = new List<string>();
        foreach (JsonNode? c in o["changes"]?.AsArray() ?? [])
        {
            string id = AiReader.Str(c?["id"]) ?? "";
            string setting = AiReader.Str(c?["setting"]) ?? "";
            JsonNode? value = c?["value"];
            string reason = AiReader.Str(c?["reason"]) ?? "";
            if (s.Node(id) is { } n && n.Type != StudyNode.End)
            {
                Add(NodeChange(s, n, setting, value, reason), $"{n.Label}: {Friendly(setting)}");
            }
            else if (s.Link(id) is { } l)
            {
                Add(LinkChange(s, l, setting, value, reason), $"{RoadName(s, l)}: {Friendly(setting)}");
            }
            else
            {
                leftOut.Add("A change to a place the AI named that is not in this study (no intersection or road with that id).");
            }

            void Add((AiProposal? P, string? Why) r, string what)
            {
                if (r.P is not null)
                {
                    changes.Add(r.P);
                }
                else
                {
                    leftOut.Add($"{what} — {r.Why}");
                }
            }
        }

        return new AiProposals(changes, leftOut, AiReader.Str(o["notes"]) ?? "");
    }

    private static (AiProposal?, string?) NodeChange(Study s, StudyNode n, string setting, JsonNode? value, string reason)
    {
        string k = $"node:{n.Id}/";
        Signal? sig = n.Signal;
        switch (setting)
        {
            case "cycleSeconds" when sig is not null:
                return Number(value, 40, 180, "seconds") is double cyc
                    ? (Make(n.Label, "Cycle length", $"{sig.Cycle:0} s", $"{cyc:0} s", reason, k + "cycle", st => st.Node(n.Id)!.Signal!.Cycle = Math.Round(cyc)), null)
                    : (null, "the cycle must be 40 to 180 seconds");
            case "mainStreetGreenPercent" when sig is not null:
                return Number(value, 20, 80, "%") is double pct
                    ? (Make(n.Label, "Main street's share of green", $"{sig.Split * 100:0}%", $"{pct:0}%", reason, k + "split", st => st.Node(n.Id)!.Signal!.Split = Math.Round(pct) / 100), null)
                    : (null, "the main street's share must be 20 to 80%");
            case "protectedLeftMain" or "protectedLeftSide" when sig is not null:
                bool main = setting == "protectedLeftMain";
                return AiReader.Bool(value) is bool on
                    ? (Make(n.Label, main ? "Protected left on the main street" : "Protected left on the side street", YesNo(main ? sig.ProtMain : sig.ProtSide), YesNo(on), reason, k + (main ? "protMain" : "protSide"),
                        st => { Signal g = st.Node(n.Id)!.Signal!; if (main) { g.ProtMain = on; } else { g.ProtSide = on; } }), null)
                    : (null, "it should be true or false");
            case "kind":
                string? kind = AiReader.Str(value);
                return kind is StudyNode.SignalType or StudyNode.Roundabout && kind != n.Type
                    ? (Make(n.Label, "Kind", Kind(n.Type), Kind(kind), reason, k + "type", st => StudyEdits.SetType(st, st.Node(n.Id)!, kind)), null)
                    : (null, "an intersection is a signal or a roundabout");
            case "name":
                return AiReader.Str(value) is { Length: > 0 and <= 80 } name
                    ? (Make(n.Label, "Name", n.Label, name, reason, k + "name", st => st.Node(n.Id)!.Name = name), null)
                    : (null, "a name must be some text");
            default:
                return (null, sig is null && setting is "cycleSeconds" or "mainStreetGreenPercent" or "protectedLeftMain" or "protectedLeftSide"
                    ? "this intersection is not a signal"
                    : "TrafficLab+ has no such setting for an intersection");
        }
    }

    private static (AiProposal?, string?) LinkChange(Study s, StudyLink l, string setting, JsonNode? value, string reason)
    {
        string k = $"link:{l.Id}/";
        switch (setting)
        {
            case "throughLanesEachWay":
                return Number(value, 1, 3, "lanes") is double lanes
                    ? (Make(RoadName(s, l), "Through lanes each way", $"{l.Lanes}", $"{lanes:0}", reason, k + "lanes", st => st.Link(l.Id)!.Lanes = (int)Math.Round(lanes)), null)
                    : (null, "TrafficLab+ simulates 1 to 3 through lanes each way");
            case "speedMph":
                return Number(value, 10, 75, "mph") is double mph
                    ? (Make(RoadName(s, l), "Speed limit", $"{Units.Mph(l.Speed):0} mph", $"{mph:0} mph", reason, k + "speed", st => st.Link(l.Id)!.Speed = Math.Round(Units.Mps(Math.Round(mph)), 4)), null)
                    : (null, "the speed must be 10 to 75 mph");
            case "name":
                return AiReader.Str(value) is { Length: > 0 and <= 80 } name
                    ? (Make(RoadName(s, l), "Street name", l.Title, name, reason, k + "name", st => st.Link(l.Id)!.Name = name), null)
                    : (null, "a name must be some text");
            case "leftTurnLaneAt" or "rightTurnLaneAt":
                bool left = setting == "leftTurnLaneAt";
                string at = AiReader.Str(value) ?? "";
                string? dir = at == l.B ? "ab" : at == l.A ? "ba" : null;
                if (dir is null || s.Node(at)?.Type != StudyNode.SignalType)
                {
                    return (null, "a turn lane is on a road arriving at a signal at one of its ends");
                }

                (bool hasL, bool hasR) = StudyEdits.GetPocket(s, l.Id, dir);
                if (left ? hasL : hasR)
                {
                    return (null, "that turn lane is already there");
                }

                return (Make(RoadName(s, l), (left ? "Left" : "Right") + "-turn lane arriving at " + s.Node(at)!.Label, "none", "a turn lane today", reason, k + "pockets:" + dir,
                    st =>
                    {
                        (bool l0, bool r0) = StudyEdits.GetPocket(st, l.Id, dir);
                        StudyEdits.SetPocket(st, l.Id, dir, l0 || left, r0 || !left);
                    }), null);
            default:
                return (null, "TrafficLab+ has no such setting for a road");
        }
    }

    // ------------------------------------------------------------ 2. estimate the traffic

    public static string Traffic(Study s, string words) => $$"""
        A student is setting the traffic for a simulation of a real place for a class lab, and some roads have no traffic count.
        Estimate, for each road end listed, the vehicles per hour that come INTO the study there in the busy (design) hour, from
        what the student says, the kind of road (its name, lanes and speed), the place, and any counts already in use. Keep any
        road end whose volume came from traffic counts as it is: do not list it. Be realistic for a US city; say what you assumed.

        The student says:
        [start of their words]
        {{(string.IsNullOrWhiteSpace(words) ? "(nothing more)" : words.Trim())}}
        [end of their words]

        The study now:
        {{Describe(s)}}

        Answer with JSON only, in this shape:
        {"ends": [ {"id": "<a road end id>", "enteringPerHour": <a whole number, 0 to 5000>, "reason": "<one sentence>"} ],
         "notes": "<one or two sentences on what you assumed>"}
        """;

    public static AiProposals ReadTraffic(Study s, string answer)
    {
        JsonObject o = AiReader.Object(answer, "ends");
        var changes = new List<AiProposal>();
        var leftOut = new List<string>();
        Dictionary<string, double> now = DemandShares.Entering(s);
        foreach (JsonNode? e in o["ends"]?.AsArray() ?? [])
        {
            string id = AiReader.Str(e?["id"]) ?? "";
            if (s.Node(id) is not { Type: StudyNode.End } end)
            {
                leftOut.Add("An estimate for a place the AI named that is not one of this study's road ends.");
                continue;
            }

            if (StudyEdits.OriginOf(s, $"node:{id}/volume") == Origins.Fdot)
            {
                leftOut.Add($"{end.Label} — its volume comes from an FDOT count, which is kept.");
                continue;
            }

            if (Number(e?["enteringPerHour"], 0, 5000, "veh/h") is not double v)
            {
                leftOut.Add($"{end.Label} — the estimate must be 0 to 5,000 vehicles an hour.");
                continue;
            }

            double vol = Math.Round(v);
            changes.Add(Make(end.Label, "Vehicles an hour coming in", $"{now.GetValueOrDefault(id):#,0}", $"{vol:#,0}", AiReader.Str(e?["reason"]) ?? "",
                $"node:{id}/volume", st => AiApply.Volume(st, id, vol)));
        }

        return new AiProposals(changes, leftOut, AiReader.Str(o["notes"]) ?? "");
    }

    // ------------------------------------------------------------ 3. write the challenge

    public static string Challenge(Study s, string words) => $$"""
        An instructor is setting a traffic-engineering challenge for a class on a simulated network. Players get a budget to spend on
        fixes (turn lanes, an extra through lane, retiming a signal, protected left turns, converting a signal to a roundabout), test
        their plan, and get a score out of 100: 50 for cutting delay, 25 for approaches at level of service D or better, 15 for budget
        left, 10 for trips completed; a plan over budget scores half. Write the challenge the instructor asks for below.

        The instructor says:
        [start of their words]
        {{words.Trim()}}
        [end of their words]

        The study now:
        {{Describe(s)}}

        Answer with JSON only, in this shape (leave out anything that should not change):
        {"title": "<the page's title>", "subtitle": "<one line>", "intro": "<two or three sentences for the players: where, why it matters, the goal>",
         "budgetMillions": <number, 0.1 to 1000>,
         "costsMillions": {"pocketL": <left-turn lane>, "pocketR": <right-turn lane>, "retime": <retiming a signal>, "protL": <protected left phase>, "roundabout": <signal to roundabout>, "addLaneBase": <adding a lane, fixed part>, "addLanePerMile": <adding a lane, per mile>, "oneWay": <making a road one-way>},
         "demandButtons": [ {"name": "<what the button says>", "percent": <of today's demand, 10 to 300>} ],
         "reasons": "<two or three sentences: why these choices make the challenge the instructor asked for>"}
        A fix that should not be used can be priced above the budget.
        """;

    public static AiProposals ReadChallenge(Study s, string answer)
    {
        JsonObject o = AiReader.Object(answer, "changes");
        var changes = new List<AiProposal>();
        var leftOut = new List<string>();
        string why = AiReader.Str(o["reasons"]) ?? "";
        void Text(string field, string label, Func<Study, string?> get, Action<Study, string> set, int max)
        {
            if (AiReader.Str(o[field]) is not { Length: > 0 } v)
            {
                return;
            }

            if (v.Length > max)
            {
                leftOut.Add($"{label} — longer than {max} characters.");
                return;
            }

            if (v != get(s))
            {
                changes.Add(Make("The page", label, get(s) ?? "(empty)", v, why, null, st => set(st, v)));
            }
        }

        Text("title", "Title", st => st.Title, (st, v) => st.Title = v, 80);
        Text("subtitle", "Subtitle", st => st.Subtitle, (st, v) => st.Subtitle = v, 140);
        Text("intro", "Introduction", st => st.Intro, (st, v) => st.Intro = v, 700);
        if (o["budgetMillions"] is JsonNode b)
        {
            if (Number(b, 0.1, 1000, "$M") is double budget)
            {
                changes.Add(Make("The challenge", "Budget", Money(s.Budget), Money(budget), why, "budget", st => st.Budget = Math.Round(budget, 2)));
            }
            else
            {
                leftOut.Add("Budget — it must be $0.1M to $1,000M.");
            }
        }

        if (o["costsMillions"] is JsonObject costs)
        {
            Costs lpga = Costs.Lpga();
            foreach ((string name, JsonNode? v) in costs)
            {
                (string Label, Func<Costs, double?> Get, Action<Costs, double> Set, double Scale)? f = name switch
                {
                    "pocketL" => ("Price: a left-turn lane", c => c.PocketL, (c, x) => c.PocketL = x, 1),
                    "pocketR" => ("Price: a right-turn lane", c => c.PocketR, (c, x) => c.PocketR = x, 1),
                    "retime" => ("Price: retiming a signal", c => c.Retime, (c, x) => c.Retime = x, 1),
                    "protL" => ("Price: a protected left phase", c => c.ProtL, (c, x) => c.ProtL = x, 1),
                    "roundabout" => ("Price: a signal into a roundabout", c => c.Roundabout, (c, x) => c.Roundabout = x, 1),
                    "addLaneBase" => ("Price: adding a lane (fixed part)", c => c.AddLaneBase, (c, x) => c.AddLaneBase = x, 1),
                    "addLanePerMile" => ("Price: adding a lane (per mile)", c => c.AddLanePerKm * 1.609344, (c, x) => c.AddLanePerKm = x / 1.609344, 1),
                    "oneWay" => ("Price: making a road one-way", c => c.OneWay, (c, x) => c.OneWay = x, 1),
                    _ => null,
                };
                if (f is not { } fx)
                {
                    leftOut.Add($"A price for \"{name}\" — TrafficLab+ has no such fix.");
                    continue;
                }

                if (Number(v, 0, 100, "$M") is not double price)
                {
                    leftOut.Add($"{fx.Label} — it must be $0 to $100M.");
                    continue;
                }

                double before = fx.Get(s.Costs ?? lpga) ?? fx.Get(lpga) ?? 0;
                if (Math.Abs(before - price) < 0.0005)
                {
                    continue;
                }

                changes.Add(Make("The challenge", fx.Label, Money(before), Money(price), why, "costs/" + name, st => fx.Set(st.Costs ??= Costs.Lpga(), price)));
            }
        }

        if (o["demandButtons"] is JsonArray buttons)
        {
            var list = new List<Scenario>();
            foreach (JsonNode? b2 in buttons.Take(5))
            {
                if (AiReader.Str(b2?["name"]) is { Length: > 0 and <= 20 } name && Number(b2?["percent"], 10, 300, "%") is double pct)
                {
                    list.Add(new Scenario { Name = name, Mult = Math.Round(pct) / 100 });
                }
            }

            if (list.Count > 0)
            {
                string Show(IEnumerable<Scenario> sc) => string.Join(", ", sc.Select(x => $"{x.Name} {x.Mult * 100:0}%"));
                changes.Add(Make("The page", "Demand buttons", Show(s.Demand.Scenarios ?? []), Show(list), why, "demand/scenarios", st => st.Demand.Scenarios = list.Select(x => new Scenario { Name = x.Name, Mult = x.Mult }).ToList()));
            }
        }

        return new AiProposals(changes, leftOut, why);
    }

    // ------------------------------------------------------------ 4. coach

    public static string Coach(Study s, string lastTestJson, string question) => $$"""
        A student is working on a traffic simulation lab. They built a plan of fixes within a budget and ran a traffic test; the
        results are below, with every approach's volume-to-capacity ratio (v/c), level of service (A best to F worst) and delay,
        today and with their plan. Explain what the results show, which approaches still fail and why, and suggest two or three
        specific things to try next, with what each costs against the budget. Encourage; do not hand them a complete answer.
        Keep it under 250 words.

        {{(string.IsNullOrWhiteSpace(question) ? "" : "The student asks: \"" + question.Trim() + "\"\n")}}
        The study:
        {{Describe(s)}}

        The last traffic test:
        {{lastTestJson}}
        """;

    // ------------------------------------------------------------ helpers

    private static AiProposal Make(string target, string setting, string before, string after, string reason, string? key, Action<Study> apply) =>
        new() { Target = target, Setting = setting, Before = before, After = after, Reason = reason, Key = key, Apply = apply };

    private static double? Number(JsonNode? v, double min, double max, string unit) =>
        AiReader.Num(v) is double d && d >= min && d <= max ? d : null;

    // several pieces of one street share its name: say which piece
    private static string RoadName(Study s, StudyLink l) => $"{l.Title} ({s.Node(l.A)?.Label} – {s.Node(l.B)?.Label})";

    // the AI's setting names, in the words of the form
    private static string Friendly(string setting) => setting switch
    {
        "cycleSeconds" => "cycle length",
        "mainStreetGreenPercent" => "main street's share of green",
        "protectedLeftMain" => "protected left on the main street",
        "protectedLeftSide" => "protected left on the side street",
        "throughLanesEachWay" => "through lanes each way",
        "speedMph" => "speed limit",
        "leftTurnLaneAt" => "left-turn lane",
        "rightTurnLaneAt" => "right-turn lane",
        "kind" => "kind",
        "name" => "name",
        _ => "a setting TrafficLab+ does not have",
    };

    private static string YesNo(bool b) => b ? "yes" : "no";

    private static string Kind(string t) => t == StudyNode.Roundabout ? "roundabout" : t == StudyNode.SignalType ? "traffic signal" : "road end";

    private static string Money(double m) => "$" + m.ToString("0.###", CultureInfo.InvariantCulture) + "M";
}

/// <summary>Applying what the student accepted.</summary>
public static class AiApply
{
    /// <summary>Applies the ticked changes and marks each value "suggested by the AI".</summary>
    public static int Apply(Study study, IEnumerable<AiProposal> proposals)
    {
        int n = 0;
        foreach (AiProposal p in proposals.Where(p => p.Use))
        {
            p.Apply(study);
            if (p.Key is not null)
            {
                StudyEdits.Mark(study, p.Key, Origins.Ai);
            }

            n++;
        }

        return n;
    }

    /// <summary>An estimated volume for one road end: the study moves to typed volumes (the other
    /// ends keep what they have now), and the end's weight follows its volume.</summary>
    public static void Volume(Study study, string endId, double volume)
    {
        if (!study.Demand.IsVolumes)
        {
            Dictionary<string, double> now = DemandShares.Entering(study);
            study.Demand.Mode = Demand.Volumes;
            foreach (StudyNode e in study.Nodes.Where(n => n.Type == StudyNode.End))
            {
                e.Volume = Math.Round(now.GetValueOrDefault(e.Id));
                e.Weight = Math.Max(1, e.Volume.Value);
            }
        }

        StudyNode end = study.Node(endId)!;
        end.Volume = volume;
        if (StudyEdits.OriginOf(study, $"node:{endId}/weight") != Origins.Fdot)
        {
            end.Weight = Math.Max(1, volume);
        }
    }

    /// <summary>The text of a coach's answer, tidied: no markdown headings or bold markers.</summary>
    public static string Tidy(string text)
    {
        IEnumerable<string> lines = text.Replace("\r", "", StringComparison.Ordinal).Split('\n')
            .Select(line => line.TrimStart('#', ' ').Replace("**", "", StringComparison.Ordinal));
        return string.Join("\n", lines).Trim();
    }
}
