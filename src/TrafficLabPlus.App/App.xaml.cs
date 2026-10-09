using System.IO;
using System.Windows;
using TrafficLabPlus.Core.Build;

namespace TrafficLabPlus.App;

/// <summary>
/// Starts the window, or — with a command line — builds a page with no window:
/// <code>
/// TrafficLabPlus.exe --build-page &lt;study.json&gt; &lt;page.html&gt;
/// TrafficLabPlus.exe --build-example &lt;page.html&gt;      (the built-in LPGA example)
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

        new MainWindow().Show();
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
