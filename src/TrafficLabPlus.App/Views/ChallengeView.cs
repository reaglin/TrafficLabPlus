using System.Windows.Controls;
using TrafficLabPlus.Core.Model;

namespace TrafficLabPlus.App.Views;

/// <summary>Challenge: the words on the page, the budget, the prices of the fixes, and the
/// student's own notes.</summary>
public sealed class ChallengeView : SectionView
{
    public override void Rebuild()
    {
        (ScrollViewer scroll, StackPanel panel) = Column();
        Content = scroll;
        if (Session is not { } s)
        {
            return;
        }

        Study st = s.Study;
        panel.Children.Add(Title("Challenge"));
        panel.Children.Add(Lead("What a player is asked to do: the words at the top of the page, the money they have, and what each fix costs. A player builds a plan within the budget, tests it, and submits it for a score out of 100."));
        var form = new Form(panel, s);

        form.Heading("The page");
        form.Text("Title", "The page's heading and the printout's, like \"LPGA Traffic Lab\".", () => st.Title, v => st.Title = v ?? "Traffic study", "study/title");
        form.Text("Sign letters", "Up to 5 letters for the green sign in the page's header, like LPGA. Leave it empty for none.", () => st.Short, v => st.Short = v is { Length: > 5 } ? v[..5] : v, "study/short");
        form.Text("Subtitle", $"One line under the title. Left empty, the page says \"Fix this network on a {Money(st.Budget)} budget\".", () => st.Subtitle, v => st.Subtitle = v, "study/subtitle");
        form.Text("Introduction", "A sentence or two at the top of the page's instructions: where this is and why it matters.", () => st.Intro, v => st.Intro = v, "study/intro", multiLine: true);
        form.Text("Place", "Where the network is, like \"Daytona Beach, Florida\". On the printout.", () => st.Place, v => st.Place = v, "study/place");
        form.Text("Author", "Who made this study. On the page's credit line and the printout.", () => st.Author, v => st.Author = v, "study/author");
        form.Text("Course", "The class it is for, like \"CEN 3722\". On the printout.", () => st.Course, v => st.Course = v, "study/course");

        form.Heading("Budget");
        form.Number("Budget for a plan", "$ million", "The money a player's plan may spend. Spending less scores better; going over it halves the score.",
            () => st.Budget, v => st.Budget = v, 0.1, 1000, "budget", 2);

        form.Heading("What each fix costs", "In millions of dollars. The page adds these up as a player builds a plan.");
        Costs c = st.Costs ??= new Costs();
        Costs lpga = Costs.Lpga();
        Cost(form, "Adding a through lane each way — fixed part", "Paid once per road widened, whatever its length.", () => c.AddLaneBase ?? lpga.AddLaneBase, v => c.AddLaneBase = v, "addLaneBase");
        Cost(form, "Adding a through lane each way — per mile", "Paid for every mile of road widened (the study keeps it per kilometre).", () => (c.AddLanePerKm ?? lpga.AddLanePerKm) * 1.609344, v => c.AddLanePerKm = v / 1.609344, "addLanePerKm");
        Cost(form, "Making a road one-way", "Signs and markings.", () => c.OneWay ?? lpga.OneWay, v => c.OneWay = v, "oneWay");
        Cost(form, "A left-turn lane", "One approach.", () => c.PocketL ?? lpga.PocketL, v => c.PocketL = v, "pocketL");
        Cost(form, "A right-turn lane", "One approach.", () => c.PocketR ?? lpga.PocketR, v => c.PocketR = v, "pocketR");
        Cost(form, "Retiming a signal", "A new cycle length or split at one signal: an engineer's time, no construction.", () => c.Retime ?? lpga.Retime, v => c.Retime = v, "retime");
        Cost(form, "A protected left-turn phase", "A green arrow at one signal.", () => c.ProtL ?? lpga.ProtL, v => c.ProtL = v, "protL");
        Cost(form, "Turning a signal into a roundabout", "The whole intersection rebuilt.", () => c.Roundabout ?? lpga.Roundabout, v => c.Roundabout = v, "roundabout");
        form.Button("Use the LPGA example's prices", "Puts every price back to the LPGA Traffic Lab's.", () =>
        {
            s.Edit("study/costs", () => st.Costs = Costs.Lpga());
            RebuildSoon();
        });

        form.Heading("Your notes", "Anything you want to remember about this study. Kept in the study file; never put on the page.");
        form.Text("Notes", "", () => s.Document.Notes, v => s.Document.Notes = v ?? "", null, multiLine: true);
    }

    private static void Cost(Form form, string label, string help, Func<double?> get, Action<double> set, string name) =>
        form.Number(label, "$ million", help, get, set, 0, 100, "costs/" + name, 3);

    private static string Money(double millions) => millions >= 1 ? "$" + millions.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture) + "M" : "$" + (millions * 1000).ToString("0", System.Globalization.CultureInfo.CurrentCulture) + "K";
}
