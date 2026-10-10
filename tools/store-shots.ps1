# The Store screenshots (plan 8.6): the window at 1920x1080, driven through UI Automation and captured
# with PrintWindow (tools/tour-window.ps1, which never types into another window), plus the published
# website and a player at work on a page, from headless Chrome. Writes resources/images/screenshots/*.png.
#
#   pwsh tools/store-shots.ps1            (the AI shot asks the AI once: a few cents on the key set up)
#   pwsh tools/store-shots.ps1 -NoAi
param([switch]$NoAi)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $root "src\TrafficLabPlus.App\bin\Debug\net10.0-windows\TrafficLabPlus.exe"
$out = Join-Path $root "resources\images\screenshots"
$tour = Join-Path $PSScriptRoot "tour-window.ps1"
New-Item -ItemType Directory -Force $out | Out-Null

# the LPGA corridor as the Map and Traffic steps make it, from the saved OpenStreetMap and FDOT answers
$work = Join-Path ([IO.Path]::GetTempPath()) ("tl-shots-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force $work | Out-Null
$demo = Join-Path $work "LPGA Corridor Traffic Lab.trafficlab"
& $exe --demo-study (Join-Path $root "tests\fixtures\osm-lpga.json") (Join-Path $root "tests\fixtures\fdot-lpga.json") $demo `
    j101035693 j3269748465 j4750842748 j3269758871 j3269758870 | Out-Null
Start-Sleep -Milliseconds 1500

$env:TRAFFICLAB_WINDOW = "1920x1080"
# the start screen without this machine's recent studies: their folder paths are not for a public listing
$recent = Join-Path $env:LOCALAPPDATA "TrafficLabPlus\recent.txt"
if (Test-Path $recent) { Move-Item $recent "$recent.store-shot" -Force }
try {
    & $tour -Exe $exe -Steps "wait:500", "shot:$out\01-start.png"
}
finally {
    if (Test-Path "$recent.store-shot") { Move-Item "$recent.store-shot" $recent -Force }
}
try {
    & $tour -Exe $exe -Args "`"$demo`"" -WaitMs 4000 -Steps "select:1  Map", "wait:7000", "shot:$out\02-map-from-openstreetmap.png"
    & $tour -Exe $exe -Steps "invoke:Open the LPGA Traffic Lab example", "wait:4000", "invoke:Start", "wait:600", "toggle:Race Week",
        "expand:Choose an intersection, road end or road to change", "select:Intersection: LPGA & Williamson", "keys:{ESC}", "wait:8000",
        "shot:$out\03-network-and-live-page.png"
    & $tour -Exe $exe -Args "`"$demo`"" -WaitMs 4000 -Steps "select:3  Traffic", "wait:3000", "invoke:Start", "wait:6000", "shot:$out\04-traffic-fdot-counts.png"
    & $tour -Exe $exe -Steps "invoke:Open the Four-Way Intersection example", "wait:4000", "invoke:Start", "wait:600", "select:4  Challenge",
        "wait:1000", "toggle:Busy day", "wait:7000", "shot:$out\05-challenge.png"
    & $tour -Exe $exe -Steps "invoke:Open the LPGA Traffic Lab example", "wait:4000", "select:5  Preview", "wait:3000", "invoke:Start", "wait:600",
        "toggle:2035", "wait:9000", "shot:$out\06-preview.png"
    if (-not $NoAi) {
        & $tour -Exe $exe -Steps "invoke:Open the LPGA Traffic Lab example", "wait:4000", "invoke:Fill in with the AI…", "wait:1500",
            "type:What you know about the real roads=Williamson Boulevard has dual left turn lanes onto LPGA in both directions and is posted 45 mph. The ramp signal runs a 110 second cycle.",
            "invoke:Ask the AI", "wait:50000", "shot:$out\07-ai-proposes.png"
    }
}
finally {
    Remove-Item Env:TRAFFICLAB_WINDOW -ErrorAction SilentlyContinue
}

# the published website (the summary page) and a player building a plan, in Chrome as a visitor sees them
$site = Join-Path $work "site"
& $exe --build-site $site "Maria Gomez — CEN 3722 traffic studies" example:lpga example:four-way $demo | Out-Null
Start-Sleep -Milliseconds 1500
$chrome = @("C:\Program Files\Google\Chrome\Application\chrome.exe", "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
& $chrome --headless=new --disable-gpu --hide-scrollbars --window-size=1920,1080 "--screenshot=$out\08-published-summary-page.png" ("file:///" + ($site -replace '\\', '/') + "/index.html") 2>$null
Start-Sleep -Seconds 3
$env:TL_WIDTH = "1920"
node (Join-Path $PSScriptRoot "play-page.js") (Join-Path $site "lpga\index.html") "Maria Gomez" "$out\09-player-builds-a-plan.png" | Out-Null
Remove-Item Env:TL_WIDTH

Remove-Item -Recurse -Force $work
Get-ChildItem $out | Select-Object Name, Length
