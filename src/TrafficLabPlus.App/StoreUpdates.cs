using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Windows.Services.Store;

namespace TrafficLabPlus.App;

/// <summary>
/// Help ▸ Check for a new version (Ron, 2026-10-30, for every Store program). Asks the Microsoft Store whether a newer version
/// is published; if so, asks the person, then lets the Store download and install it (Windows shows its own progress). A copy
/// not installed from the Store (a hand-test build) cannot ask, and says so, offering the Store page instead.
/// </summary>
public static class StoreUpdates
{
    public const string ProgramName = "TrafficLab+";
    public const string StorePage = "https://apps.microsoft.com/detail/9P5NGQF4HRPD";

    /// <summary>The version running now, as people read it (1.0.0).</summary>
    public static string CurrentVersion
    {
        get
        {
            var v = typeof(StoreUpdates).Assembly.GetName().Version;
            return v == null ? "" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    /// <param name="owner">The window the Store's dialogs belong to.</param>
    /// <param name="confirmSave">Asked before installing, because the Store closes TrafficLab+ to install: offers to
    /// save the open study (Yes / No / Cancel). False (Cancel) stops the update.</param>
    /// <param name="busy">Told true while the Store is being asked (the window shows "Asking the Microsoft Store…"), false after.</param>
    public static async Task CheckAsync(Window owner, Func<bool> confirmSave, Action<bool>? busy = null)
    {
        const string title = ProgramName + " — Check for a new version";
        if (!IsFromStore())
        {
            if (MessageBox.Show(owner,
                    $"This copy of {ProgramName} ({CurrentVersion}) was not installed from the Microsoft Store, so it cannot check for a " +
                    $"new version itself.\n\nOpen {ProgramName}'s page on the Microsoft Store to see the latest version?",
                    title, MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                OpenStorePage(owner);
            return;
        }

        IReadOnlyList<StorePackageUpdate> updates;
        StoreContext store;
        try
        {
            store = StoreContext.GetDefault();
            // A desktop program must tell the Store which window its own dialogs belong to.
            WinRT.Interop.InitializeWithWindow.Initialize(store, new WindowInteropHelper(owner).Handle);
            busy?.Invoke(true);
            try
            {
                updates = await store.GetAppAndOptionalStorePackageUpdatesAsync();
            }
            finally
            {
                busy?.Invoke(false);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            if (MessageBox.Show(owner,
                    "The Microsoft Store could not be asked about a new version. Check that this computer is connected to the " +
                    $"internet and try again.\n\nOpen {ProgramName}'s page on the Microsoft Store instead?\n\nDetails for support: {ex.Message}",
                    title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                OpenStorePage(owner);
            return;
        }

        if (updates.Count == 0)
        {
            MessageBox.Show(owner, $"You have the latest version of {ProgramName} ({CurrentVersion}).", title,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show(owner,
                $"A new version of {ProgramName} is on the Microsoft Store (you have {CurrentVersion}).\n\n" +
                $"Download and install it now? Windows closes {ProgramName} to install it; open it again from the Start menu to use " +
                "the new version. If the study has unsaved changes, you are asked to save them first. (No: the Store will install it " +
                "later if automatic updates are on.)",
                title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        // the Store closes the program to install it: unsaved changes are offered a save first (Cancel stops here)
        if (!confirmSave())
            return;

        try
        {
            var result = await store.RequestDownloadAndInstallStorePackageUpdatesAsync(updates);
            string message = result.OverallState switch
            {
                StorePackageUpdateState.Completed => $"The new version is installed. If {ProgramName} is still open, close it and open it again from the Start menu.",
                StorePackageUpdateState.Canceled => "The update was cancelled. You can check again at any time, or the Store will install it later if automatic updates are on.",
                _ => "The update did not finish. The Microsoft Store will try again later, or open " +
                     $"{ProgramName}'s page on the Store and press Update.",
            };
            MessageBox.Show(owner, message, title, MessageBoxButton.OK,
                result.OverallState == StorePackageUpdateState.Completed ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            MessageBox.Show(owner, $"The update could not be started. Open {ProgramName}'s page on the Microsoft Store " +
                "and press Update instead.\n\nDetails for support: " + ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static void OpenStorePage(Window owner)
    {
        try { Process.Start(new ProcessStartInfo(StorePage) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(owner, $"Windows could not open the page.\n\n{StorePage}", ProgramName + " — Check for a new version",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>True when the program runs from its Store package (it has a package identity).</summary>
    public static bool IsFromStore()
    {
        int length = 0;
        // APPMODEL_ERROR_NO_PACKAGE (15700) means it was not installed as a package.
        return GetCurrentPackageFullName(ref length, null) != 15700;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);
}
