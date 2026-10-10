<#
.SYNOPSIS
    Builds an MSIX for TrafficLab+ (ported from Gamify+ 2026-10-10).

.DESCRIPTION
    Publishes TrafficLabPlus.App self-contained, stages it with the manifest and assets, packs it with
    makeappx and signs it with signtool. No Visual Studio and no .wapproj — the same steps run
    on this machine and on a bare windows-latest CI runner.

    For local use the script creates a self-signed development certificate on demand and
    rewrites the manifest Publisher to match it. Store builds pass -IdentityName and -Publisher
    with the identity Partner Center issued, and -CertificateThumbprint for the real signing
    certificate. See packaging/make-msixupload.ps1 for the Store submission package, and
    docs/release/DEPLOY_TO_WIN_APP_STORE.md for the values and the whole procedure.

.EXAMPLE
    ./packaging/pack.ps1
    Development build: self-signed, output in artifacts/.

.EXAMPLE
    ./packaging/pack.ps1 -SkipSigning
    Unsigned package, for CI where a certificate is not available.
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [string]$Version,
    [string]$IdentityName,
    [string]$Publisher,
    [string]$PublisherDisplayName,
    [string]$CertificateThumbprint,
    [string]$DevCertSubject = 'CN=TrafficLab Plus Development',
    [string]$OutputDirectory,
    [switch]$SkipSigning
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$packagingDir = $PSScriptRoot
$stageDir = Join-Path $repoRoot 'artifacts/stage'
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

# MSIX versions are four-part and the revision must be 0 for Store submission.
$parts = @($Version.Split('.'))
while ($parts.Count -lt 4) { $parts += '0' }
$packageVersion = ($parts[0..3] -join '.')

Write-Host "TrafficLab+ $packageVersion ($Configuration / $Runtime)" -ForegroundColor Cyan

# --- publish -------------------------------------------------------------------------------
$publishDir = Join-Path $repoRoot "artifacts/publish/$Runtime"
Remove-Item $publishDir -Recurse -Force -ErrorAction SilentlyContinue

# Self-contained: the MSIX carries the .NET runtime, so a clean machine needs no prerequisite.
dotnet publish (Join-Path $repoRoot 'src/TrafficLabPlus.App/TrafficLabPlus.App.csproj') `
    -c $Configuration -r $Runtime --self-contained true `
    -p:PublishSingleFile=false `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

# --- stage ---------------------------------------------------------------------------------
Remove-Item $stageDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $stageDir | Out-Null

Copy-Item "$publishDir/*" $stageDir -Recurse -Force
Copy-Item (Join-Path $packagingDir 'Assets') $stageDir -Recurse -Force

# .pdb files are not shipped and trip Store certification.
Get-ChildItem $stageDir -Filter *.pdb -Recurse | Remove-Item -Force

# --- certificate ---------------------------------------------------------------------------
# The manifest Publisher must equal the signing certificate subject exactly, or Windows
# refuses the package at install time with a signature/identity mismatch.
$certificate = $null
if (-not $SkipSigning) {
    if ($CertificateThumbprint) {
        $certificate = Get-Item "Cert:\CurrentUser\My\$CertificateThumbprint" -ErrorAction Stop
    }
    else {
        $certificate = Get-ChildItem Cert:\CurrentUser\My |
            Where-Object { $_.Subject -eq $DevCertSubject -and $_.NotAfter -gt (Get-Date) } |
            Select-Object -First 1

        if (-not $certificate) {
            Write-Host "Creating development certificate $DevCertSubject" -ForegroundColor Yellow
            $certificate = New-SelfSignedCertificate `
                -Type Custom -Subject $DevCertSubject `
                -KeyUsage DigitalSignature -FriendlyName 'TrafficLab+ development signing' `
                -CertStoreLocation 'Cert:\CurrentUser\My' `
                -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
            Write-Host "  Sideloading requires this certificate trusted under Local Machine\\TrustedPeople." -ForegroundColor Yellow
        }
    }
}

# The manifest carries the Store publisher. An unsigned (Store) build keeps it; a signed local
# build must carry the signing certificate's subject instead, or Windows refuses to install it.
if (-not $Publisher) {
    $Publisher = if ($certificate) { $certificate.Subject } else { $null }
}

# --- manifest ------------------------------------------------------------------------------
$manifest = [xml](Get-Content (Join-Path $packagingDir 'AppxManifest.xml'))
$manifest.Package.Identity.Version = $packageVersion
if ($Publisher) { $manifest.Package.Identity.Publisher = $Publisher }
# Store builds pass the Name Partner Center reserved; local builds keep the development one.
if ($IdentityName) { $manifest.Package.Identity.Name = $IdentityName }
# The manifest already carries the account's publisher display name ("Dean Eaglin"); this is
# here so a rename in Partner Center does not need a manifest edit.
if ($PublisherDisplayName) { $manifest.Package.Properties.PublisherDisplayName = $PublisherDisplayName }
$manifest.Package.Identity.ProcessorArchitecture = $Runtime.Replace('win-', '')
$manifest.Save((Join-Path $stageDir 'AppxManifest.xml'))

# --- resource index --------------------------------------------------------------------------
# Assets/ ships every tile at scale-100/125/150/200/400 plus the taskbar target sizes. Windows
# resolves those qualified names only through a resources.pri; without one it ignores them and
# upscales the 100% tile, so high-DPI tiles and the taskbar icon come out soft.
$makepri   = Find-WindowsKitTool 'makepri.exe'
$priConfig = Join-Path $repoRoot 'artifacts/priconfig.xml'
Remove-Item $priConfig -Force -ErrorAction SilentlyContinue
& $makepri createconfig /cf $priConfig /dq en-US /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'makepri createconfig failed.' }

# createconfig's default <packaging> splits every Scale-qualified candidate out into a separate
# resource package. TrafficLab+ ships one package, so without removing that the index keeps only
# scale-100 and the other four scales are dead weight in the .msix.
$priXml = [xml](Get-Content $priConfig)
$packagingNode = $priXml.SelectSingleNode('//packaging')
if ($packagingNode) { $packagingNode.ParentNode.RemoveChild($packagingNode) | Out-Null }
$priXml.Save($priConfig)

& $makepri new /pr $stageDir /cf $priConfig /of (Join-Path $stageDir 'resources.pri') /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'makepri new failed.' }
Remove-Item $priConfig -Force -ErrorAction SilentlyContinue

# --- pack ----------------------------------------------------------------------------------
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$msix = Join-Path $OutputDirectory "TrafficLabPlus-$packageVersion-$Runtime.msix"
Remove-Item $msix -Force -ErrorAction SilentlyContinue

$makeappx = Find-WindowsKitTool 'makeappx.exe'
& $makeappx pack /d $stageDir /p $msix /o
if ($LASTEXITCODE -ne 0) { throw 'makeappx failed.' }

# --- sign ----------------------------------------------------------------------------------
if ($SkipSigning) {
    Write-Host "Unsigned package: $msix" -ForegroundColor Yellow
    Write-Host 'It will not install until signed.' -ForegroundColor Yellow
}
else {
    $signtool = Find-WindowsKitTool 'signtool.exe'
    & $signtool sign /fd SHA256 /sha1 $certificate.Thumbprint $msix
    if ($LASTEXITCODE -ne 0) { throw 'signtool failed.' }
    Write-Host "Signed package: $msix" -ForegroundColor Green
}

Write-Output $msix
