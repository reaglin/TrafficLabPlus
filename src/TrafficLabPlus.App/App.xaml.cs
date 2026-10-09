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
/// TrafficLabPlus.exe --demo-study &lt;overpass.json&gt; &lt;fdot.json&gt; &lt;out.trafficlab&gt; &lt;junction id&gt;…  (a study as Map and Traffic make it)
/// TrafficLabPlus.exe --build-site &lt;folder&gt; &lt;title&gt; &lt;study file | example:id&gt;…  (the website Publish sends)
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
                case "--demo-study" when args.Length >= 5:
                    {
                        // a study made as the Map and Traffic steps make it, from saved answers (Store screenshots, demos)
                        string osm = File.ReadAllText(args[1]), fdot = File.ReadAllText(args[2]);
                        var cache = new Core.Osm.OsmCache { Box = [29.2185, -81.103, 29.229, -81.087], Fetched = "2026-10-09", Junctions = [.. args[4..]], Overpass = osm };
                        Core.Osm.OsmStudy demo = Core.Osm.NetworkFromOsm.Build(Core.Osm.OsmData.Parse(osm), new Core.Osm.OsmStudyRequest
                        {
                            Junctions = args[4..], Title = "LPGA Corridor Traffic Lab", Place = "Daytona Beach, Florida", Author = "Dr. Ron Eaglin", Budget = 5,
                        });
                        List<Core.Counts.CountMatch> matches = Core.Counts.FdotCounts.Match(demo.Study, Core.Counts.FdotCounts.Parse(fdot));
                        Core.Counts.FdotCounts.Apply(demo.Study, matches, 2025);
                        var doc = new StudyDocument { Study = demo.Study, Notes = string.Join(Environment.NewLine, demo.Notes) };
                        doc.Attachments[Core.Osm.OsmCache.Entry] = cache.ToBytes();
                        doc.Attachments[Core.Counts.FdotCache.Entry] = Core.Counts.FdotCache.ToBytes(fdot, "2026-10-09",
                            matches.Where(m => m.Found).ToDictionary(m => m.EndId, m => (m.Use, m.K, m.D)));
                        StudyFile.Save(doc, args[3]);
                        return 0;
                    }

                case "--build-site" when args.Length >= 4:
                    {
                        // the website Publish builds, without publishing: --build-site <folder> <title> <study file | example:id>…
                        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        var studies = args[3..].Select(a => a.StartsWith("example:", StringComparison.Ordinal)
                                ? new Core.Site.SiteStudy(Examples.All.Single(e => e.Id == a[8..]).Study(), Core.Site.SiteBuilder.Slug(a[8..], taken))
                                : new Core.Site.SiteStudy(StudyFile.Load(a).Study, Core.Site.SiteBuilder.Slug(Path.GetFileNameWithoutExtension(a), taken)))
                            .ToList();
                        Core.Site.SiteBuilder.Build(args[1], args[2], studies, DateTime.Now);
                        return 0;
                    }

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
