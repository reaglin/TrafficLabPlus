# Launch the app, walk through it with UI Automation, and capture its own window with PrintWindow
# (never a screen grab) at each "shot". Steps, in order:
#   invoke:<name>   press the button or menu item with this automation name
#   select:<name>   select the radio button / list item with this name (a section on the left)
#   type:<name>=<text>   set a text box's value (then Tab is sent so it commits)
#   toggle:<name>   tick or untick a check box
#   expand:<name>   open a drop-down list, so its items can be selected
#   wait:<ms>       wait
#   shot:<file.png> capture the window
# Example:
#   .\tools\tour-window.ps1 -Exe manual-test\TrafficLabPlus.exe -Steps 'invoke:Open the LPGA example','wait:3000','shot:shots\a.png'
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [Parameter(Mandatory = $true)][string[]]$Steps,
    [string]$Args = "",
    [int]$WaitMs = 3000
)

Add-Type -AssemblyName System.Drawing, UIAutomationClient, UIAutomationTypes, System.Windows.Forms

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class Win2 {
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

$proc = if ($Args) { Start-Process -FilePath $Exe -ArgumentList $Args -PassThru } else { Start-Process -FilePath $Exe -PassThru }
Start-Sleep -Milliseconds $WaitMs
$proc.Refresh()
$h = $proc.MainWindowHandle
if ($h -eq [IntPtr]::Zero) { Write-Output "NO WINDOW"; if (-not $proc.HasExited) { $proc.Kill() }; exit 1 }
[Win2]::SetForegroundWindow($h) | Out-Null
$root = [System.Windows.Automation.AutomationElement]::FromHandle($h)

function Find($name, $pattern = $null) {
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)
    if ($pattern) {
        # the element that can do it, not a text inside it with the same name
        $can = New-Object System.Windows.Automation.PropertyCondition($pattern, $true)
        $cond = New-Object System.Windows.Automation.AndCondition($cond, $can)
    }
    for ($i = 0; $i -lt 20; $i++) {
        # dialogs are other top-level windows of the process
        $e = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
        if ($e) { return $e }
        Start-Sleep -Milliseconds 250
    }
    throw "not found: $name"
}

# Keystrokes go to whatever window is in front. They are sent only when that window belongs to the
# app being toured; otherwise the tour stops, so nothing is ever typed into the person's own windows.
function Keys($text) {
    $fg = [Win2]::GetForegroundWindow(); $fgPid = 0
    [Win2]::GetWindowThreadProcessId($fg, [ref]$fgPid) | Out-Null
    if ($fgPid -ne $proc.Id) { throw "the app is not the window in front; no keys sent" }
    [System.Windows.Forms.SendKeys]::SendWait($text)
}

function Shot($out) {
    $proc.Refresh()
    $hw = [System.Windows.Automation.AutomationElement]::FocusedElement
    $r = New-Object Win2+RECT
    [Win2]::GetWindowRect($h, [ref]$r) | Out-Null
    $w = $r.Right - $r.Left; $ht = $r.Bottom - $r.Top
    $bmp = New-Object System.Drawing.Bitmap $w, $ht
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    [Win2]::PrintWindow($h, $hdc, 2) | Out-Null
    $g.ReleaseHdc($hdc); $g.Dispose()
    $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    Write-Output "saved $out"
}

try {
    foreach ($step in $Steps) {
        $kind, $rest = $step -split ':', 2
        switch ($kind) {
            'invoke' { (Find $rest ([System.Windows.Automation.AutomationElement]::IsInvokePatternAvailableProperty)).GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
            'select' { (Find $rest ([System.Windows.Automation.AutomationElement]::IsSelectionItemPatternAvailableProperty)).GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select() }
            'type' {
                $name, $text = $rest -split '=', 2
                $e = Find $name ([System.Windows.Automation.AutomationElement]::IsValuePatternAvailableProperty)
                try { $e.SetFocus() } catch { }   # a combo box's edit part takes the value without focus
                $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($text)
                Keys "{TAB}"
            }
            'toggle' { (Find $rest ([System.Windows.Automation.AutomationElement]::IsTogglePatternAvailableProperty)).GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle() }
            'expand' { (Find $rest ([System.Windows.Automation.AutomationElement]::IsExpandCollapsePatternAvailableProperty)).GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand() }
            'keys' { Keys $rest }
            'wait' { Start-Sleep -Milliseconds ([int]$rest) }
            'shot' { Shot $rest }
        }
        Start-Sleep -Milliseconds 300
    }
}
catch { Write-Output "STEP FAILED: $step - $_" }
finally { if (-not $proc.HasExited) { $proc.Kill() } }
