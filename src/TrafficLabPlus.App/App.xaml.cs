using System.IO;
using System.Windows;
using TrafficLabPlus.Core.Build;
using TrafficLabPlus.Core.Model;

namespace TrafficLabPlus.App;

/// <summary>
/// Starts the window, or — with a command line — builds a page with no window:
/// <code>
/// TrafficLabPlus.exe --build-page &lt;study.json&gt; &lt;page.html&gt;
/// TrafficLabPlus.exe --build-example &lt;page.html&gt;      (the built-in LPGA example)
/// TrafficLabPlus.exe --new-study &lt;signals&gt; &lt;study.json&gt;  (a starting network, as New study makes it)
/// TrafficLabPlus.exe --osm-junctions &lt;overpass.json&gt; &lt;out.txt&gt;   (the junctions found in an Overpass answer)
/// TrafficLabPlus.exe --osm-study &lt;overpass.json&gt; &lt;study.json&gt; &lt;junction id&gt;…
/// TrafficLabPlus.exe --fdot-match &lt;study.json&gt; &lt;fdot.json&gt; &lt;out.txt&gt;  (FDOT counts matched to road ends)
/// TrafficLabPlus.exe &lt;study.trafficlab&gt;                (opens it)
/// </code>
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string[] args = e.Args;
        if (args.Length > 0 && args[0].StartsWith("--", StringComparison.Ordinal))
        {
            Shutdown(RunCommand(args));
            return;
        }

        new MainWindow(args.Length > 0 ? args[0] : null).Show();
    }

    private static int RunCommand(string[] args)
    {
        try
        {
            switch (args[0])
            {
                case "--build-page" when args.Length == 3:
                    File.WriteAllText(args[2], PageBuilder.Build(File.ReadAllText(args[1])));
                    return 0;
                case "--build-example" when args.Length == 2:
                    File.WriteAllText(args[1], PageBuilder.Build(PageBuilder.LpgaExampleJson()));
                    return 0;
                case "--new-study" when args.Length == 3 && int.TryParse(args[1], out int signals):
                    File.WriteAllText(args[2], StudyJson.Write(StudyTemplates.Create(new NewStudyRequest { Title = "New study", Signals = signals })));
                    return 0;
                case "--osm-junctions" when args.Length == 3:
                    File.WriteAllLines(args[2], Core.Osm.OsmJunctions.Find(Core.Osm.OsmData.Parse(File.ReadAllText(args[1])))
                        .Select(j => $"{j.Id}	{j.Lat:0.000000},{j.Lon:0.000000}	{(j.IsSignal ? "signal" : j.IsRoundabout ? "roundabout" : "-")}	{j.Label}"));
                    return 0;
                case "--osm-study" when args.Length >= 4:
                    Core.Osm.OsmStudy made = Core.Osm.NetworkFromOsm.Build(Core.Osm.OsmData.Parse(File.ReadAllText(args[1])),
                        new Core.Osm.OsmStudyRequest { Junctions = args[3..], Title = "From OpenStreetMap" });
                    File.WriteAllText(args[2], StudyJson.Write(made.Study));
                    File.WriteAllLines(args[2] + ".notes.txt", made.Notes);
                    return 0;
                case "--fdot-match" when args.Length == 4:
                    Study st = StudyJson.Read(File.ReadAllText(args[1]));
                    File.WriteAllLines(args[3], Core.Counts.FdotCounts.Match(st, Core.Counts.FdotCounts.Parse(File.ReadAllText(args[2])))
                        .Select(m => $"{m.EndId}	{m.EndName}	in {m.Entering:0}	out {m.Exiting:0}	K {m.K:0.###} D {m.D:0.###}	"
                                     + string.Join(" + ", m.Counts.Select(c => (c.Inbound ? "IN " : "OUT ") + c.Count.Describe()))));
                    return 0;
                default:
                    return 2;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or StudyFormatException)
        {
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "TrafficLabPlus-command.txt"), ex.Message);
            return 1;
        }
    }
}
