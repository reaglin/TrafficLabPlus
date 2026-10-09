using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace TrafficLabPlus.App.Views;

/// <summary>
/// The guide at the top of each step: what the page is for, what must be done on it and what is
/// optional, and every option explained (Ron, 2026-10-09: "very detailed help and information about
/// all options", optional items "clearly stated"). Never assume the reader knows traffic
/// engineering or this program.
/// </summary>
public static class Guides
{
    public sealed record Part(string Heading, string Body);

    /// <summary>A guide: a short summary that is always shown (what to do, what is optional), and the
    /// details in an expander that opens on a click.</summary>
    // whether each guide was left open, so choosing another road does not close it again
    private static readonly Dictionary<string, bool> Open = [];

    public static StackPanel Build(string required, string optional, params Part[] parts)
    {
        string key = parts.Length > 0 ? parts[0].Body[..Math.Min(40, parts[0].Body.Length)] : required;
        var box = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        var summary = new Border
        {
            Background = Form.Res("ChipBrush"),
            BorderBrush = Form.Res("LineBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 8, 10, 8),
        };
        var lines = new StackPanel();
        lines.Children.Add(Line("What you need to do here: ", required));
        lines.Children.Add(Line("Optional: ", optional));
        summary.Child = lines;
        box.Children.Add(summary);

        var details = new StackPanel { Margin = new Thickness(4, 6, 0, 4) };
        foreach (Part p in parts)
        {
            details.Children.Add(new TextBlock { Text = p.Heading, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 2), TextWrapping = TextWrapping.Wrap });
            foreach (string para in p.Body.Split("\n\n"))
            {
                details.Children.Add(new TextBlock { Text = para, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) });
            }
        }

        var expander = new Expander
        {
            Header = new TextBlock { Text = "Help for this step: every option explained (click to open or close)", FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap },
            Content = details,
            Margin = new Thickness(0, 6, 0, 0),
            IsExpanded = Open.GetValueOrDefault(key),
        };
        expander.Expanded += (_, _) => Open[key] = true;
        expander.Collapsed += (_, _) => Open[key] = false;
        box.Children.Add(expander);
        return box;
    }

    private static TextBlock Line(string lead, string text)
    {
        var t = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 1, 0, 1) };
        t.Inlines.Add(new Run(lead) { FontWeight = FontWeights.SemiBold });
        t.Inlines.Add(new Run(text));
        return t;
    }

    public static StackPanel Map() => Build(
        "find your intersection (step 1), load its roads (step 2), choose one intersection or up to ten along a corridor (step 3), and make the study (step 4).",
        "this whole step, if your study already exists — made from a layout or from an example. Searching is optional too: you can simply drag and zoom the map.",
        new("What this step does",
            "It turns real roads into a traffic study. The roads come from OpenStreetMap, a free map of the world made by volunteers, which records each road's lanes, turn lanes, speed limit and name, and where the traffic signals are. TrafficLab+ reads those records for the area you choose and builds the study from them, so you start with the real intersection instead of drawing it."),
        new("1  Find the place",
            "Type a street and a town, like \"LPGA Boulevard, Daytona Beach\", and press Search (or Enter). The results list shows every match; click one to go there. A town alone works too.\n\nOpenStreetMap's search cannot find a crossing by its two street names (\"LPGA & Williamson\" finds nothing). Find one of the two streets, then pick the crossing in step 3 — its list can be filtered by the other street's name.\n\nYou can also skip the search and move the map yourself: drag it with the mouse, and zoom with the wheel or the + and − buttons."),
        new("2  Bring in the roads",
            "Press Load the roads shown on the map. TrafficLab+ asks OpenStreetMap once for every road inside the area the map shows. The area must be less than 4 km across (about the length of a ten-signal corridor): if it is bigger, the button is greyed out and the reason is written under it — zoom in.\n\nTip: zoom so the intersections you want fill the map, with a little room around them. Roads that leave the loaded area stop at its edge, so a road end may come out close in.\n\nThe answer is kept in your study file: a saved study never asks OpenStreetMap again. Loading again replaces the roads and clears what you chose (you are asked first)."),
        new("3  Choose the intersections",
            "Every place two or more named roads meet is a dot. A green dot has a traffic signal on OpenStreetMap; a green ring is a roundabout; a grey dot has no signal on OpenStreetMap (it may be a stop sign, or the signal may simply not be mapped). Click a dot to choose it — it turns yellow with a number — and click again to un-choose it. Or tick it in the list; type part of a street name in Find by street name to shorten the list.\n\nOne intersection makes a single-intersection study. For a corridor, choose up to ten, in order along the main road: the numbers are the order. Use the ↑ and ↓ beside a chosen one to change its place, and Remove to drop it.\n\nA divided road's crossing is drawn by OpenStreetMap as four points close together; TrafficLab+ treats them as one intersection, so there is one dot."),
        new("4  Make the study",
            "Press Make the study. You give it a title (one is suggested), the place, and the budget players get; then TrafficLab+ builds it and opens it in Network. A message lists anything to check: an intersection with more than four roads (TrafficLab+ keeps the four busiest), a junction with no signal in OpenStreetMap (it starts as a signal), a road that ran off the loaded area.\n\nWhat comes from OpenStreetMap is marked \"from OpenStreetMap\" in Network; what it could not know — the signal timing, the traffic — starts at a plain value marked \"a starting value\". Those are for you to set in the next steps."));

    public static StackPanel Network() => Build(
        "check that the drawing matches the real intersection, and fix anything the yellow bar at the top lists (the page cannot run until it is fixed).",
        "everything else. Every name, lane count, speed, signal setting and turn lane already has a value; change any that are not what is on the street.",
        new("What this step does",
            "It is the network the page simulates: intersections (signals or roundabouts), the roads between them, and road ends — the edges of the study, where cars come in and go out. The drawing shows it from above; the form under the drawing shows the settings of whatever you choose. The page on the right is rebuilt about half a second after every change, so you can watch what each change does."),
        new("Choosing and moving",
            "Click an intersection, a road end or a road on the drawing to choose it (it is ringed in yellow), or choose it from the Change list, which also works with the keyboard. Drag an intersection or a road end to move it; the roads follow. Moving changes only the drawing and the road lengths, not the names or settings.\n\nGreen dots are traffic signals, green rings are roundabouts, black squares are road ends. Roads are drawn wider when they have more lanes. Grid squares are 500 ft."),
        new("Adding and removing",
            "Add intersection: click the button, then click on the drawing where it goes. It starts as a signal and needs 3 or 4 roads.\nAdd road end: click where traffic should come in and leave.\nAdd road: click one intersection or road end, then another. A road end has exactly one road; an intersection at most four.\nRemove (or the Delete key, on the drawing) removes what is chosen; an intersection's roads go with it.\n\nPress Esc, or click the active button again, to stop adding. Every change can be undone with Ctrl+Z (Edit ▸ Undo) and redone with Ctrl+Y."),
        new("An intersection's settings",
            "Kind: a traffic signal, a roundabout that exists today, or a road end. (Players can turn a signal into a roundabout as part of their plan; they cannot turn a roundabout back.)\nName and short name: what players read on the page and the printout. The short name is used in tables.\nCycle length: how many seconds the lights take to go all the way round, 40 to 180. Longer cycles move more cars each cycle but make each driver wait longer.\nMain street's share of green: of the green time, the percentage the main street gets (20 to 80%); the side street gets the rest.\nWhich roads are the main street: tick the two roads that form the main street. If none are ticked, TrafficLab+ takes the two roads most opposite each other, widest first.\nProtected left turns: a green-arrow phase for left turns, for the main street or the side street. Without one, left turns wait for a gap in oncoming traffic.\nWhere it is: distances east and south, in feet, from a corner of the drawing — easier to drag than to type."),
        new("A road's settings",
            "Street name: as people know it. Renaming a street renames it everywhere it appears — every road with that name, and the intersections and road ends named after it — in one undo.\nThrough lanes in each direction: lanes that carry traffic straight on, counted one way, 1 to 3. Turn lanes are separate.\nSpeed limit: the posted speed in mph; cars drive at about this speed on a clear road.\nShow the street name on the page's map: untick it for short pieces where the name would crowd the map.\nTurn lanes that exist today: a short extra lane before a signal so turning cars wait out of the way of through traffic, for each direction arriving at a signal. These are free and part of today's network; players can buy more."),
        new("Where each value came from",
            "Under each value a line says where it came from: from OpenStreetMap, from FDOT traffic counts, from the built-in example, typed by you, or a starting value. A starting value is a guess to begin with — change it to match the real road if you know better."));

    public static StackPanel Traffic() => Build(
        "nothing, if the starting traffic is good enough to begin with. To match the real intersection, set how many vehicles come in at each road end — from FDOT's counts (Florida, studies made from the map) or typed from a count.",
        "the FDOT counts, the trips that do not happen, and the demand buttons. Each already has a value or can be left empty.",
        new("What this step does",
            "It sets how much traffic uses the network at today's demand and where it comes in and goes out. The cars on the page pick their own routes between the road ends. Traffic is in vehicles per hour (veh/h) in the busiest hour."),
        new("Traffic counts from FDOT",
            "FDOT (the Florida Department of Transportation) counts traffic on state and most county roads and publishes each count as AADT, the Annual Average Daily Traffic: vehicles a day in both directions, averaged over a year. Press Look up FDOT counts for these roads: TrafficLab+ asks FDOT's public count map once, matches each count to the road end it belongs to, and shows each one — the count site, year, AADT and FDOT's description of the stretch counted — before anything is used.\n\nK turns a day into the busy hour: it is the share of a day's traffic in the design hour (FDOT's busy hour), usually about 9%. D is the share going the busier way in that hour, usually 55 to 58%. TrafficLab+ takes AADT × K × D as the vehicles an hour coming in at a road end. A ramp runs one way: an off-ramp brings AADT × K in; an on-ramp only takes traffic out. Each K and D starts at FDOT's own figure for that count; change one if your instructor gives you another.\n\nUntick a count that looks wrong (its from–to roads are not this road, or it is many years old). Then press Use the ticked counts. Roads with no count keep their estimates. Stop using the counts goes back. Counts need a study made from the map, in Florida; otherwise type the volumes."),
        new("How traffic is given",
            "One total, shared by how busy each road end is: type the vehicles an hour entering the whole study, and a number for each road end saying how busy it is compared with the others (900 sends and receives about twice the trips of 450). Each road end then shows the vehicles an hour that come in there.\nVehicles per hour typed for each road end: type the vehicles an hour coming in at each road end (from a count sheet, for example), and how busy each is as a destination."),
        new("Trips that do not happen",
            "Pairs of road ends with no trips between them. At LPGA, traffic from the I-95 north ramps does not get off and back on to go south, so that pair is listed. Leave this empty unless you know of one. The sentence under it is shown on the page."),
        new("Demand buttons on the page",
            "Each is a button on the page's toolbar that sets demand to a percentage of today's: Today at 100%, a future year or an event day above it. Players use them to test their plans in harder conditions. Up to five; the name is what the button says."));

    public static StackPanel Challenge() => Build(
        "the title and the budget (both start with a value).",
        "everything else: the sign letters, subtitle, introduction, place, author, course, the prices of the fixes, and your notes.",
        new("What this step does",
            "It sets what a player is asked to do: the words at the top of the page, the money they may spend on fixes, and what each fix costs. A player builds a plan within the budget, runs a traffic test, and submits it for a score out of 100."),
        new("The page's words",
            "Title: the heading of the page and the printout.\nSign letters: up to five letters in the green sign at the top left of the page, like LPGA. Empty shows TL+.\nSubtitle: one line under the title; empty says \"Fix this network on a $X budget\".\nIntroduction: a sentence or two at the top of the page's instructions — where this is and why it matters.\nPlace, author, course: printed on the printout (\"Study by …\"); the author is also in the page's credit line."),
        new("Budget",
            "The money, in millions of dollars, a player's plan may spend. The score rewards money left over, and a plan over budget has its score halved. LPGA's is $5 million: enough for a few real fixes, not everything."),
        new("What each fix costs",
            "Adding a through lane each way: a fixed part per road, plus a part per mile of road widened.\nMaking a road one-way: signs and markings.\nA left-turn lane, a right-turn lane: per approach.\nRetiming a signal: a new cycle or split at one signal.\nA protected left-turn phase: a green arrow at one signal.\nTurning a signal into a roundabout: the whole intersection rebuilt.\nUse the LPGA example's prices puts them all back."),
        new("Your notes",
            "Anything you want to remember. Kept in the study file, never put on the page. A study made from the map starts with the list of things to check."));

    public static StackPanel Preview() => Build(
        "try the page the way a player will, and make sure it reads well.",
        "this whole step — nothing here changes your study, so it can be skipped.",
        new("What this step does",
            "It shows the page full size, exactly as it will be published — the same file. Try it as a player: read the instructions, press Start (the name is optional), raise the demand, click a red road or intersection, buy a fix, press Run traffic test, then Submit plan to see the printout."),
        new("Things to check",
            "Is the page's goal clear from its title and introduction? Is the budget enough for a few fixes but not everything? At Today's demand, is there something worth fixing — orange or red roads at least on the busier demand buttons? Are the names the ones people use?\n\nOpen in your browser shows the same page in Edge or Chrome, as a visitor sees it."));

    public static StackPanel Publish() => Build(
        "nothing yet — publishing to the web arrives in a later version.",
        "saving the page as a file now.",
        new("What this step does",
            "In a later version: every study goes into one GitHub Pages website of yours, with a summary page linking to each one, and you hand in the link. You will be able to leave any study out. For now you can save the page as one file."),
        new("Save the page as a web page file",
            "Saves the page as one .html file. It needs nothing else: it opens in any browser with no internet, can be emailed or put on any web site, and is exactly the page shown in Preview."));
}

/// <summary>Preview: the guide beside the full page.</summary>
public sealed class PreviewHelpView : SectionView
{
    public override void Rebuild()
    {
        (ScrollViewer scroll, StackPanel panel) = Column();
        Content = scroll;
        panel.Children.Add(Title("Preview"));
        panel.Children.Add(Lead("The page on the right is exactly the page that will be published. Try it as a player would."));
        panel.Children.Add(Guides.Preview());
    }
}
