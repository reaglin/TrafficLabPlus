<#
.SYNOPSIS
    Builds the .msixupload package that Partner Center accepts for a Store submission.

.DESCRIPTION
    A .msixupload is a zip holding an .msixbundle and, optionally, an .appxsym symbol archive
    for crash analysis. This script produces one per architecture bundle:

      pack.ps1 -SkipSigning   ->  one .msix per runtime
      makeappx bundle         ->  TrafficLabPlus_<version>_<arch>.msixbundle
      zip                     ->  TrafficLabPlus_<version>_<arch>_bundle.msixupload

    The package is deliberately UNSIGNED. The Store re-signs every submission with the
    publisher certificate it issues; a package signed with a local or development certificate
    is rejected.

    IDENTITY. Three values must match Partner Center exactly or the upload is rejected. All
    three are already in packaging/AppxManifest.xml, so no arguments are needed:

      Package/Identity/Name          DeanEaglin.TrafficLab
      Package/Identity/Publisher     CN=E88392BA-A722-4B3A-8372-04403A55AA63  (per account)
      PublisherDisplayName           Dean Eaglin

    The script checks the built package's family name equals DeanEaglin.TrafficLab_xs303fgqvwdg8
    and stops if it does not - a typo in either half of the identity breaks that match.

.EXAMPLE
    ./packaging/make-msixupload.ps1
    The submission package, with the identity from AppxManifest.xml.
#>
[CmdletBinding()]
param(
    [string]$Version,
    [string]$IdentityName,
    [string]$Publisher,
    [string]$PublisherDisplayName,
    [string]$Configuration = 'Release',
    [string[]]$Runtimes = @('win-x64'),
    [string]$OutputDirectory,
    [switch]$SkipSymbols
)

$ErrorActionPreference = 'Stop'

$repoRoot     = Split-Path -Parent $PSScriptRoot
$packagingDir = $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'artifacts' }

function Find-WindowsKitTool {
    param([Parameter(Mandatory)][string]$Name)
    $binRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    if (-not (Test-Path $binRoot)) { throw "Windows SDK not found at $binRoot." }
    $tool = Get-ChildItem -Path $binRoot -Filter $Name -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\' } |
        Sort-Object { [version]($_.Directory.Parent.Name) } -Descending |
        Select-Object -First 1
    if (-not $tool) { throw "$Name not found under $binRoot. Install the Windows 10/11 SDK." }
    return $tool.FullName
}

# --- version -------------------------------------------------------------------------------
if (-not $Version) {
    $props = Join-Path $repoRoot 'Directory.Build.props'
    $Version = ([xml](Get-Content $props)).Project.PropertyGroup.TrafficLabPlusVersion |
        Where-Object { $_ } | Select-Object -First 1
    if (-not $Version) { throw "TrafficLabPlusVersion not found in $props." }
}
$parts = @($Version.Split('.'))
while ($parts.Count -lt 4) { $parts += '0' }
# The Store requires the fourth part to be 0; it reserves it for its own use.
$parts[3] = '0'
$packageVersion = ($parts[0..3] -join '.')

Write-Host "TrafficLab+ $packageVersion -> .msixupload" -ForegroundColor Cyan

# --- one .msix per architecture --------------------------------------------------------------
$bundleDir = Join-Path $repoRoot 'artifacts/bundle'
Remove-Item $bundleDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $bundleDir | Out-Null

$packArgs = @{ Configuration = $Configuration; Version = $packageVersion; OutputDirectory = $bundleDir; SkipSigning = $true }
if ($IdentityName)         { $packArgs.IdentityName         = $IdentityName }
if ($Publisher)            { $packArgs.Publisher            = $Publisher }
if ($PublisherDisplayName) { $packArgs.PublisherDisplayName = $PublisherDisplayName }

foreach ($runtime in $Runtimes) {
    Write-Host "  packing $runtime" -ForegroundColor DarkGray
    & (Join-Path $packagingDir 'pack.ps1') @packArgs -Runtime $runtime | Out-Null
}

$msixFiles = Get-ChildItem $bundleDir -Filter *.msix
if (-not $msixFiles) { throw "pack.ps1 produced no .msix in $bundleDir." }

# --- bundle ----------------------------------------------------------------------------------
$arch      = if ($Runtimes.Count -gt 1) { 'multi' } else { $Runtimes[0].Replace('win-', '') }
$stem      = "TrafficLabPlus_${packageVersion}_$arch"
$bundlePath = Join-Path $OutputDirectory "$stem.msixbundle"
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
Remove-Item $bundlePath -Force -ErrorAction SilentlyContinue

$makeappx = Find-WindowsKitTool 'makeappx.exe'
& $makeappx bundle /d $bundleDir /p $bundlePath /bv $packageVersion /o
if ($LASTEXITCODE -ne 0) { throw 'makeappx bundle failed.' }

# --- symbols ---------------------------------------------------------------------------------
# .appxsym is a zip of the build's .pdb files. pack.ps1 strips them from the package itself
# (they trip Store certification); Partner Center wants them separately for crash reports.
$uploadParts = @($bundlePath)
if (-not $SkipSymbols) {
    foreach ($runtime in $Runtimes) {
        $publishDir = Join-Path $repoRoot "artifacts/publish/$runtime"
        $pdbs = Get-ChildItem $publishDir -Filter *.pdb -Recurse -ErrorAction SilentlyContinue
        if (-not $pdbs) { continue }
        $sym = Join-Path $OutputDirectory "$stem.appxsym"
        Remove-Item $sym -Force -ErrorAction SilentlyContinue
        Compress-Archive -Path $pdbs.FullName -DestinationPath "$sym.zip" -Force
        Move-Item "$sym.zip" $sym -Force
        $uploadParts += $sym
    }
}

# --- .msixupload -------------------------------------------------------------------------------
$upload = Join-Path $OutputDirectory "${stem}_bundle.msixupload"
Remove-Item $upload -Force -ErrorAction SilentlyContinue
Compress-Archive -Path $uploadParts -DestinationPath "$upload.zip" -Force
Move-Item "$upload.zip" $upload -Force

Write-Host ''
foreach ($f in @($upload) + $uploadParts) {
    $item = Get-Item $f
    Write-Host ("  {0,-46} {1,8:N1} MB" -f $item.Name, ($item.Length / 1MB)) -ForegroundColor Green
}
# --- identity check ----------------------------------------------------------------------------
# The package family name is derived from Name and Publisher: SHA-256 of the UTF-16 publisher,
# first 8 bytes, base32 over Crockford's alphabet. Matching Partner Center's is a stronger check
# than reading the two values back - a typo or a case slip in either half breaks it.
$expectedFamily = 'DeanEaglin.TrafficLab_xs303fgqvwdg8'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$first = Get-ChildItem $bundleDir -Filter *.msix | Select-Object -First 1
$zip = [System.IO.Compression.ZipFile]::OpenRead($first.FullName)
try {
    $reader = New-Object System.IO.StreamReader($zip.GetEntry('AppxManifest.xml').Open())
    $built = [xml]$reader.ReadToEnd()
    $reader.Dispose()
}
finally { $zip.Dispose() }
$sha  = [System.Security.Cryptography.SHA256]::Create()
$hash = $sha.ComputeHash([System.Text.Encoding]::Unicode.GetBytes($built.Package.Identity.Publisher))
$alphabet = '0123456789abcdefghjkmnpqrstvwxyz'
$bits = (($hash[0..7] | ForEach-Object { [Convert]::ToString($_, 2).PadLeft(8, '0') }) -join '') + '0'
$suffix = -join (0..12 | ForEach-Object { $alphabet[[Convert]::ToInt32($bits.Substring($_ * 5, 5), 2)] })
$family = "$($built.Package.Identity.Name)_$suffix"
if ($family -ne $expectedFamily) {
    throw "Package family name is $family, not $expectedFamily - check Name and Publisher in AppxManifest.xml."
}
Write-Host "Package family name $family - matches Partner Center." -ForegroundColor Green

Write-Host ''
Write-Host 'Upload the .msixupload at Partner Center -> Submission -> Packages.' -ForegroundColor Cyan

Write-Output $upload
