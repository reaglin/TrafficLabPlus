using System.Diagnostics;

namespace TrafficLabPlus.Tests;

/// <summary>Finding the repository, and running Node and the browser tools the tests lean on.</summary>
internal static class Tools
{
    public static string RepoRoot { get; } = FindRoot();

    private static string FindRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "TrafficLabPlus.slnx")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new InvalidOperationException("The tests must run inside the TrafficLabPlus repository.");
    }

    /// <summary>Runs node with the given arguments in the repository; returns the exit code and all output.</summary>
    public static (int Exit, string Output) Node(params string[] args)
    {
        var start = new ProcessStartInfo("node")
        {
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
            UseShellExecute = false,
        };
        foreach (string a in args)
        {
            start.ArgumentList.Add(a);
        }

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("node did not start.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(300_000))
        {
            process.Kill(entireProcessTree: true);
            return (-1, "node did not finish in five minutes");
        }

        return (process.ExitCode, stdout.Result + stderr.Result);
    }

    public static bool HasBrowser() =>
        new[]
        {
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\Application\chrome.exe"),
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
        }.Any(File.Exists);
}
