<#
.SYNOPSIS
    Makes every MSIX tile and Store listing image for TrafficLab+ from the icon Ron approved.

.DESCRIPTION
    Ported from Gamify+'s make-store-assets.ps1 (2026-10-10). The artwork is not drawn here: it
    comes from packaging/make-icon.py (plan 8.2, Ron chose it 2026-10-09), which writes

      resources/images/trafficlab-mark-2048.png    the MARK: the signal on its "+" backplate, on green.
                                                   It carries everything small: the 44, 71 and 150
                                                   tiles, the taskbar sizes, the .trafficlab file icon.
      resources/images/trafficlab-glyph-2048.png   the signal alone on transparency, set beside or
                                                   above the "TrafficLab+" name for the LOGO, the wide
                                                   tile and the hero art.

    Run make-icon.py first if those are missing. Every package asset is written under WACK's
    204800-byte cap for logo images (256 colours, then 128, 64, 32 until it fits). The Partner
    Center uploads are not capped.

    Outputs:
      packaging/Assets/              MSIX tiles - unqualified (scale-100) plus scale-* variants
      resources/images/store/        Partner Center upload images, and a contact sheet
      resources/images/TrafficLabPlus_logo2048.png   the logo (mark above the name), for reuse

    Requires ImageMagick 7 (magick.exe) on PATH.

.EXAMPLE
    ./packaging/make-store-assets.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repoRoot  = Split-Path -Parent $PSScriptRoot
$assetsDir = Join-Path $repoRoot 'packaging/Assets'
$storeDir  = Join-Path $repoRoot 'resources/images/store'
$imagesDir = Join-Path $repoRoot 'resources/images'
$workDir   = Join-Path ([System.IO.Path]::GetTempPath()) 'trafficlabplus-store-assets'

foreach ($d in @($assetsDir, $storeDir, $imagesDir, $workDir)) { New-Item -ItemType Directory -Force $d | Out-Null }

function Invoke-Magick {
    param([Parameter(ValueFromRemainingArguments)][string[]]$Arguments)
    & magick @Arguments
    if ($LASTEXITCODE -ne 0) { throw "magick failed: $($Arguments -join ' ')" }
}

# --- palette: the app's own ---------------------------------------------------------------------
$green  = '#0F6B4A'   # AccentBrush in App.xaml, the guide-sign green of every page
$white  = '#FFFFFF'
$pale   = '#BEE2D0'   # the hero's second line
# magick strips backslashes out of -font paths, so keep it forward-slashed.
$font   = $env:WINDIR.Replace('\', '/') + '/Fonts/segoeuib.ttf'

$MaxAssetBytes = 204800

$mark  = Join-Path $imagesDir 'trafficlab-mark-2048.png'
$glyph = Join-Path $imagesDir 'trafficlab-glyph-2048.png'
foreach ($f in $mark, $glyph) {
    if (-not (Test-Path $f)) { throw "$f is missing - run: python packaging/make-icon.py" }
}

# --- the logo: the signal above the name, on flat green -------------------------------------------
$artFull = Join-Path $imagesDir 'TrafficLabPlus_logo2048.png'
Invoke-Magick -size 2048x2048 "xc:$green" `
    '(' $glyph -filter Lanczos -resize 1400x1400 ')' -gravity north -geometry '+0+60' -compose over -composite `
    -gravity south -font $font -pointsize 300 -fill $white -annotate '+0+230' 'TrafficLab+' `
    -strip $artFull

# --- emitters -------------------------------------------------------------------------------------
function Write-Capped {
    param([string]$Src, [int]$Width, [int]$Height, [string]$Dest, [string]$Mode = 'Fit')
    foreach ($colors in 256, 128, 64, 32) {
        $cmd = if ($Mode -eq 'Pad') {
            @('-size', "${Width}x${Height}", "xc:$green",
              '(', $Src, '-filter', 'Lanczos', '-resize', "${Width}x${Height}", ')',
              '-gravity', 'center', '-composite')
        } else {
            @($Src, '-filter', 'Lanczos', '-resize', "${Width}x${Height}^",
              '-gravity', 'center', '-extent', "${Width}x${Height}")
        }
        Invoke-Magick @cmd -colors $colors -strip $Dest
        if ((Get-Item $Dest).Length -le $MaxAssetBytes) { return }
    }
    Write-Warning ("{0} is {1} bytes, over WACK's {2} limit even at 32 colours." -f `
        (Split-Path -Leaf $Dest), (Get-Item $Dest).Length, $MaxAssetBytes)
}

function Write-Plain {
    param([string]$Src, [int]$Width, [int]$Height, [string]$Dest, [string]$Mode = 'Fit')
    if ($Mode -eq 'Pad') {
        Invoke-Magick -size "${Width}x${Height}" "xc:$green" `
            '(' $Src -filter Lanczos -resize "${Width}x${Height}" ')' `
            -gravity center -composite -strip $Dest
    } else {
        Invoke-Magick $Src -filter Lanczos -resize "${Width}x${Height}^" `
            -gravity center -extent "${Width}x${Height}" -strip $Dest
    }
}

$scales = @(
    @{ Suffix = '.scale-100'; Factor = 1.00 },
    @{ Suffix = '.scale-125'; Factor = 1.25 },
    @{ Suffix = '.scale-150'; Factor = 1.50 },
    @{ Suffix = '.scale-200'; Factor = 2.00 },
    @{ Suffix = '.scale-400'; Factor = 4.00 }
)

function Write-TileSet {
    param([string]$Name, [int]$Width, [int]$Height, [string]$Src, [string]$Mode = 'Fit')
    foreach ($s in $scales) {
        $w = [int][math]::Round($Width  * $s.Factor)
        $h = [int][math]::Round($Height * $s.Factor)
        $dest = Join-Path $assetsDir "$Name$($s.Suffix).png"
        Write-Capped $Src $w $h $dest $Mode
        if ($s.Suffix -eq '.scale-100') { Copy-Item $dest (Join-Path $assetsDir "$Name.png") -Force }
    }
    Write-Host "  $Name" -ForegroundColor DarkGray
}

function Write-TargetSizes {
    param([string]$Name, [string]$Src)
    foreach ($t in 16, 24, 32, 48, 256) {
        Write-Capped $Src $t $t (Join-Path $assetsDir "$Name.targetsize-$t.png")
        Write-Capped $Src $t $t (Join-Path $assetsDir ("$Name.targetsize-$t" + "_altform-unplated.png"))
    }
}

function Write-WideTile {
    # The signal beside the name, on flat green.
    param([int]$Width, [int]$Height, [string]$Dest)
    $margin = [int]($Width * 0.04)
    $artW   = [int]($Height * 0.80)
    $textX  = $margin + $artW + [int]($Width * 0.03)
    Invoke-Magick -size "${Width}x${Height}" "xc:$green" `
        '(' $glyph -filter Lanczos -resize "${artW}x${artW}" ')' `
        -gravity west -geometry "+$margin+0" -compose over -composite `
        -gravity west -font $font -pointsize ([int]($Height * 0.20)) -fill $white `
        -annotate "+$textX+0" 'TrafficLab+' `
        -colors 256 -strip $Dest
}

function Write-Hero {
    # The logo left, two lines of copy right; caption: fits the point size to the column.
    param([int]$Width, [int]$Height, [string]$Dest)
    $margin = [int]($Width * 0.05)
    $artW   = [int]($Height * 0.70)
    $textX  = $margin + $artW + [int]($Width * 0.05)
    $textW  = $Width - $textX - $margin

    $sub = Join-Path $workDir "hero-sub-$Width.png"
    $tag = Join-Path $workDir "hero-tag-$Width.png"
    Invoke-Magick -background none -fill $white -font $font `
        -size "${textW}x$([int]($Height * 0.12))" -gravity west `
        'caption:A real intersection, a live simulation' -strip $sub
    Invoke-Magick -background none -fill $pale -font $font `
        -size "${textW}x$([int]($Height * 0.06))" -gravity west `
        'caption:Roads from OpenStreetMap - published as a web page students play and hand in' -strip $tag

    Invoke-Magick -size "${Width}x${Height}" "xc:$green" `
        '(' $artFull -filter Lanczos -resize "${artW}x${artW}" ')' `
        -gravity west -geometry "+$margin+0" -compose over -composite `
        -gravity northwest `
        $sub -geometry "+$textX+$([int]($Height * 0.33))" -composite `
        $tag -geometry "+$textX+$([int]($Height * 0.53))" -composite `
        -strip $Dest
    Write-Host "  $(Split-Path -Leaf $Dest)" -ForegroundColor DarkGray
}

# --- MSIX tiles -----------------------------------------------------------------------------------
Write-Host 'MSIX tiles -> packaging/Assets' -ForegroundColor Cyan
Write-TileSet 'Square44x44Logo'   44  44  $mark
Write-TileSet 'Square71x71Logo'   71  71  $mark
Write-TileSet 'Square150x150Logo' 150 150 $mark
Write-TileSet 'StoreLogo'         50  50  $mark
Write-TileSet 'TrafficLabFile'    44  44  $mark
Write-TileSet 'Square310x310Logo' 310 310 $artFull
# The manifest declares the splash background as this green, so the logo sits on its own colour.
Write-TileSet 'SplashScreen'      620 300 $artFull 'Pad'
Write-TargetSizes 'Square44x44Logo' $mark
Write-TargetSizes 'TrafficLabFile'  $mark

foreach ($s in $scales) {
    Write-WideTile ([int][math]::Round(310 * $s.Factor)) ([int][math]::Round(150 * $s.Factor)) `
        (Join-Path $assetsDir "Wide310x150Logo$($s.Suffix).png")
}
Copy-Item (Join-Path $assetsDir 'Wide310x150Logo.scale-100.png') (Join-Path $assetsDir 'Wide310x150Logo.png') -Force
Write-Host '  Wide310x150Logo' -ForegroundColor DarkGray

# --- Partner Center listing images ------------------------------------------------------------------
Write-Host 'Store listing -> resources/images/store' -ForegroundColor Cyan
Write-Plain $mark    300  300  (Join-Path $storeDir 'StoreLogo-300x300.png')
Write-Plain $artFull 1080 1080 (Join-Path $storeDir 'BoxArt-1080x1080.png')
Write-Plain $artFull 720  1080 (Join-Path $storeDir 'PosterArt-720x1080.png') 'Pad'
Write-Hero 2400 1200 (Join-Path $storeDir 'SuperHeroArt-2400x1200.png')
Write-Hero 1920 1080 (Join-Path $storeDir 'HeroImage-1920x1080.png')

# --- contact sheet ----------------------------------------------------------------------------------
$sheet = Join-Path $storeDir 'asset-contact-sheet.png'
Push-Location $assetsDir
try {
    & magick montage `
        'Square44x44Logo.targetsize-16.png' 'Square44x44Logo.targetsize-32.png' `
        'Square44x44Logo.png' 'Square71x71Logo.png' 'Square150x150Logo.png' `
        'Square310x310Logo.png' 'Wide310x150Logo.png' 'SplashScreen.png' `
        -tile 4x2 -geometry '+10+10<' -background '#909090' $sheet
    if ($LASTEXITCODE -ne 0) { throw 'montage failed.' }
}
finally { Pop-Location }

$pngs = Get-ChildItem $assetsDir -Filter *.png
$over = @($pngs | Where-Object Length -gt $MaxAssetBytes)
Write-Host ''
Write-Host ("Assets: {0} files, {1:N1} MB, {2} over the {3}-byte cap" -f `
    $pngs.Count, (($pngs | Measure-Object Length -Sum).Sum / 1MB), $over.Count, $MaxAssetBytes) `
    -ForegroundColor Green
Write-Host "Contact sheet: $sheet" -ForegroundColor Green
