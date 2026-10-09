# Launch the app, capture its own window with PrintWindow (never a screen grab), close it.
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [Parameter(Mandatory = $true)][string]$Out,
    [string]$Args = "",
    [int]$WaitMs = 3000
)

Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class Win {
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

    /// The biggest visible top-level window belonging to the process.
    public static IntPtr MainWindow(uint pid, out string title) {
        IntPtr best = IntPtr.Zero; int bestArea = 0; string bestTitle = "";
        EnumWindows((h, l) => {
            uint p; GetWindowThreadProcessId(h, out p);
            if (p != pid || !IsWindowVisible(h)) return true;
            RECT r; if (!GetWindowRect(h, out r)) return true;
            int area = (r.Right - r.Left) * (r.Bottom - r.Top);
            if (area <= bestArea) return true;
            var sb = new StringBuilder(500); GetWindowText(h, sb, 500);
            best = h; bestArea = area; bestTitle = sb.ToString();
            return true;
        }, IntPtr.Zero);
        title = bestTitle; return best;
    }
}
"@

$proc = if ($Args) { Start-Process -FilePath $Exe -ArgumentList $Args -PassThru } else { Start-Process -FilePath $Exe -PassThru }
Start-Sleep -Milliseconds $WaitMs

$title = ""
$h = [Win]::MainWindow([uint]$proc.Id, [ref]$title)
if ($h -eq [IntPtr]::Zero) {
    Write-Output "NO WINDOW (exited: $($proc.HasExited))"
    if (-not $proc.HasExited) { $proc.Kill() }
    exit 1
}

[Win]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 500

$r = New-Object Win+RECT
[Win]::GetWindowRect($h, [ref]$r) | Out-Null
$w = $r.Right - $r.Left
$ht = $r.Bottom - $r.Top
Write-Output "window '$title' ${w}x${ht}"

$bmp = New-Object System.Drawing.Bitmap $w, $ht
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
[Win]::PrintWindow($h, $hdc, 2) | Out-Null   # 2 = PW_RENDERFULLCONTENT, needed for WPF
$g.ReleaseHdc($hdc)
$g.Dispose()
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

Write-Output "saved $Out"
if (-not $proc.HasExited) { $proc.Kill() }
