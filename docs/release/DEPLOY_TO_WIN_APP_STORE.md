# Deploying TrafficLab+ to the Microsoft Store

Step by step: building the Store package, and publishing the first version and every update after
it through Partner Center. Modelled on Gamify+'s `docs/release/DEPLOY_TO_WIN_APP_STORE.md`; the
packaging is ported from Gamify+ (2026-10-10), which passed WACK and certification with it.

The other documents in this folder:

| File | What it is for |
|---|---|
| `RELEASE_NOTES_<version>.md` | What each version added, one file per release — read in order, they are the timeline of the program |
| `STORE_LISTING.md` | The listing copy to paste into Partner Center (name, descriptions, features, screenshots list) |
| `STORE_SUBMISSION.md` | Everything else Partner Center asks: age rating, declarations, the `runFullTrust` justification, certification notes, WACK results |

The privacy policy is live at **https://softwareplus.ai/trafficlab/privacy/**; its source is
`SoftwarePlus\site\trafficlab\privacy\index.html` (edit it there, then `.\deploy.ps1` in SoftwarePlus).

---

## 1. Tools used

| Tool | Purpose | Where it comes from |
|---|---|---|
| .NET 10 SDK | Builds and publishes the app (`dotnet publish`, self-contained) | dot.net — already installed |
| Windows 10/11 SDK | `makeappx.exe`, `makepri.exe`, `signtool.exe` — found automatically under `C:\Program Files (x86)\Windows Kits\10\bin` | Visual Studio Installer, or the standalone SDK |
| Windows App Certification Kit | `appcert.exe` — local pre-submission test (optional but recommended) | Part of the Windows SDK: `C:\Program Files (x86)\Windows Kits\10\App Certification Kit\` |
| Python 3 with Pillow | Draws the icon (`packaging/make-icon.py`) | Only needed to re-draw the icon |
| ImageMagick 7 (`magick.exe` on PATH) | Makes every tile and listing image from the icon | imagemagick.org — only needed to re-make the artwork |
| PowerShell 7 (`pwsh`) | Runs the scripts in `packaging/` | Already installed |
| Node and Chrome or Edge | The tests play the page; `tools/store-shots.ps1` photographs it | Already installed |
| Partner Center | The Store account and submissions | partner.microsoft.com — sign in as **ron.eaglin@gmail.com** |

**No Visual Studio packaging project** (`.wapproj`) is used. The scripts call the SDK tools
directly, so the same steps work on this machine and on a bare build server.

---

## 2. Store identity

Reserved in Partner Center by Ron (TrafficLab+ → Product management → Product identity), recorded
2026-10-10. **These cannot be changed after the product is created.** They are already written
into `packaging/AppxManifest.xml`.

| Field | Value |
|---|---|
| Package/Identity/Name | `DeanEaglin.TrafficLab` |
| Package/Identity/Publisher | `CN=E88392BA-A722-4B3A-8372-04403A55AA63` |
| Package/Properties/PublisherDisplayName | `Dean Eaglin` |
| Package Family Name (PFN) | `DeanEaglin.TrafficLab_xs303fgqvwdg8` |
| Store ID | `9P5NGQF4HRPD` |
| Store display name | **TrafficLab+** |

The publisher and the `_xs303fgqvwdg8` part are the same for every app on the account (Gamify+,
SMADA, Statistle, CIATLE); only the Name differs. `make-msixupload.ps1` recomputes the family name
from the built package and **stops if it does not equal `DeanEaglin.TrafficLab_xs303fgqvwdg8`**.

---

## 3. One-time setup (done — kept for a new machine)

1. **Partner Center:** the developer account (publisher **Dean Eaglin**) and the name **TrafficLab+**
   reserved — done by Ron.
2. **Privacy page** on SoftwarePlus.ai — done and live 2026-10-09.
3. **The packaging folder** is in the repository: `packaging/AppxManifest.xml` and the scripts in §9.
4. **The artwork** — the icon is drawn by `packaging/make-icon.py` (the signal on a "+", chosen by Ron
   2026-10-09); the tiles and listing images are made from it by `packaging/make-store-assets.ps1`.
   Both already run and committed; re-run them only to change the art (icon first).
5. **Developer Mode** (Windows Settings → System → For developers) — only needed for the local
   install test in step 5 below. It is on on this machine.

---

## 4. Version numbers

- The version lives in **one place**: `TrafficLabPlusVersion` in `Directory.Build.props`
  (three parts, e.g. `1.0.0`). The program and the package both read it.
- The package version is that plus `.0` (e.g. `1.0.0.0`). The Store reserves the fourth part.
- **Every submission needs a higher version than the last one uploaded** — even a resubmission
  after a failed certification.
- Semantic meaning: `1.0.0` first release; `1.x.0` new features; `1.0.x` fixes only.

---

## 5. First release — step by step (1.0.0)

**Step 1 — Tests green, hand tests done.**
```powershell
dotnet test TrafficLabPlus.slnx
```
Ron's hand tests are in `docs/MANUAL-TESTING.md`; the plan (`docs/DEVELOPMENT-PLAN.md`) shows what
is still waiting.

**Step 2 — Set the version.** `Directory.Build.props` →
`<TrafficLabPlusVersion>1.0.0</TrafficLabPlusVersion>` (done for 1.0.0).

**Step 3 — Write the release notes.** `docs/release/RELEASE_NOTES_1.0.0.md` (done). The
"What's new" box in Partner Center takes the short version from its top.

**Step 4 — Build the Store package.** Close TrafficLab+ first, then from the repository root:
```powershell
pwsh ./packaging/make-msixupload.ps1
```
It publishes the app self-contained, stages it with the manifest and the tiles, builds the resource
index, packs an **unsigned** `.msix`, bundles it, adds the symbols, and checks the package family
name. Output (in `artifacts\`):

| File | What it is |
|---|---|
| `TrafficLabPlus_1.0.0.0_x64_bundle.msixupload` | **The file to upload to Partner Center** (about 63 MB) |
| `TrafficLabPlus_1.0.0.0_x64.msixbundle` | Inside the upload; the file WACK tests |
| `TrafficLabPlus_1.0.0.0_x64.appxsym` | Inside the upload; symbols for crash reports |

Do **not** sign it: the Store signs every package with its own certificate and rejects one signed
locally. (Built 2026-10-10.)

**Step 5 — Try the package on this machine** (optional, needs Developer Mode):
```powershell
Add-AppxPackage -Register .\artifacts\stage\AppxManifest.xml
Start-Process "shell:AppsFolder\DeanEaglin.TrafficLab_xs303fgqvwdg8!TrafficLabPlus"
# ...try it: the start screen, open the LPGA example, the Map step (tiles load), Preview, Publish...
Get-AppxPackage -Name DeanEaglin.TrafficLab | Remove-AppxPackage   # remove it again afterwards
```
Remove it before installing from the Store, or the two will clash. (Done 2026-10-10 on 1.0.0: it
started from the package, opened the LPGA example and played the page, and was removed.)

**Step 6 — Run WACK** (Windows App Certification Kit):
```powershell
pwsh ./packaging/run-wack.ps1
```
Approve the UAC prompt. WACK drives the app on screen for several minutes — leave the machine
alone. The window stays open with the summary; the full report is `artifacts\wack-report.xml`.
Record the result in `STORE_SUBMISSION.md`. **Do not run `appcert.exe` by hand** — see §8.

**Step 7 — Partner Center: create the submission.** Partner Center → Apps and games →
**TrafficLab+** → **Start your submission**, then each section in order:

1. **Pricing and availability** — Free, all markets, public (values in `STORE_SUBMISSION.md`).
2. **Properties** — category (Education → Instructional tools) and the declarations
   (`STORE_SUBMISSION.md`).
3. **Age ratings** — the questionnaire answers are in `STORE_SUBMISSION.md` (expected: 3+).
4. **Packages** — upload `artifacts\TrafficLabPlus_1.0.0.0_x64_bundle.msixupload`. Partner Center
   reads the identity from it; if it complains about the identity, the manifest is wrong (§8).
5. **Store listings → English (United States)** — paste from `STORE_LISTING.md`: description, short
   description, "What's new", features, search terms; upload the screenshots
   (`resources/images/screenshots/`, in the order the listing gives) and the listing art
   (`resources/images/store/`). Privacy policy URL: `https://softwareplus.ai/trafficlab/privacy/`.
   Website: `https://softwareplus.ai/trafficlab/`.
6. **Submission options → Notes for certification** — paste from `STORE_SUBMISSION.md`. The
   `runFullTrust` justification goes where Partner Center asks for it.

**Step 8 — Submit**, and wait for certification (usually a day or two; Partner Center e-mails).

**Step 9 — When it is live:** tag the release, record it, and update the website.
```powershell
git tag v1.0.0
git -c credential.helper= -c credential.helper='!gh auth git-credential' push origin v1.0.0
```
Record the Store link and the date in `STORE_SUBMISSION.md` and in the plan (phase 8). Then update
**softwareplus.ai/trafficlab/** — "Coming soon" becomes the Store link, with the version and the
screenshots — and run `.\deploy.ps1` in SoftwarePlus (every Store release updates the site).

---

## 6. Updates — step by step (1.0.1, 1.1.0, …)

1. `dotnet test TrafficLabPlus.slnx`; the hand tests for whatever changed.
2. **Raise the version** in `Directory.Build.props` — higher than anything uploaded before.
3. **Write `docs/release/RELEASE_NOTES_<version>.md`** — copy the shape of the last one: the short
   "What's new" first (it is pasted into Partner Center), then each change in words a student
   uses, then **Notes for people upgrading** (anything that changes their files, their folders,
   their published pages or the privacy policy).
4. If anything the app sends or stores changed, **update the privacy page** on SoftwarePlus.ai and
   deploy it **before** submitting.
5. `pwsh ./packaging/make-msixupload.ps1` → `artifacts\TrafficLabPlus_<version>_x64_bundle.msixupload`.
6. Optional but recommended: the local install test (§5 step 5) and `run-wack.ps1`. If the screens
   changed, retake the screenshots: `pwsh tools/store-shots.ps1` (`-NoAi` skips the one that asks
   the AI, a few cents on the key).
7. Partner Center → TrafficLab+ → **Update** (creates a new submission from the last one). Replace
   the package under **Packages** (remove the old one, upload the new), paste the new "What's new"
   into the listing, update screenshots if the screens changed.
8. Submit. When live: `git tag v<version>`, record it in `STORE_SUBMISSION.md`, and update
   softwareplus.ai/trafficlab/.

Users get the update automatically through the Store. Their studies and settings are in their own
folders (§7) and are never touched by an update. **Pages already published keep working** — each is
a self-contained file — and the next publish rebuilds them with the new version.

---

## 7. Where the files are

### 7.1 On a user's computer (the installed Store app)

| What | Where | Notes |
|---|---|---|
| The program itself | `C:\Program Files\WindowsApps\DeanEaglin.TrafficLab_1.0.0.0_x64__xs303fgqvwdg8\` | Read-only, managed by Windows; replaced on each update |
| **Studies** (`.trafficlab`) | `Documents\TrafficLabPlus\Studies\` (default for Save) | The user's work. Each is a zip: the study, notes, and the OpenStreetMap and FDOT answers it was made from. Files saved elsewhere stay where they were saved |
| The page built for the preview | `Documents\TrafficLabPlus\Preview\` | Rebuilt as the user works; safe to delete |
| The website built for publishing | `Documents\TrafficLabPlus\Site\` | Rebuilt whole on each publish; a `.git` folder appears inside once published |
| Settings, recent studies | `%LOCALAPPDATA%\TrafficLabPlus\settings.json`, `recent.txt` | Settings: author, course, GitHub account and repository, website title, which studies are left out, the published addresses |
| GitHub token and its account name | `%LOCALAPPDATA%\TrafficLabPlus\github-token.dat`, `github-account.txt` | Token encrypted with DPAPI for the Windows user |
| The web view's working files (WebView2) | `%LOCALAPPDATA%\TrafficLabPlus\WebView2\` | Cache only; safe to delete |
| AI keys, usage ledger, prompts | `Documents\AiManager\` | Shared with the other apps that use AiManager (Gamify+, Author+, CIATLE…); keys encrypted with DPAPI |
| Anything else Windows keeps for the app | `%LOCALAPPDATA%\Packages\DeanEaglin.TrafficLab_xs303fgqvwdg8\` | Removed by Windows when the app is uninstalled. Documents folders are **not** removed |

**Store app and `%LOCALAPPDATA%`:** on a machine that has never run TrafficLab+ outside the Store,
Windows redirects the `%LOCALAPPDATA%\TrafficLabPlus` rows to
`%LOCALAPPDATA%\Packages\DeanEaglin.TrafficLab_xs303fgqvwdg8\LocalCache\Local\TrafficLabPlus\`. A
folder that already existed before install is used in place (seen with Gamify+, 2026-09-24) — so on
this machine the Store build will find the hand-test build's settings and saved GitHub token.

Documents is where the user's own work goes on purpose: every version of the app, packaged or
not, sees the same folder, and uninstalling never deletes a student's studies. On a machine where
Documents is redirected to OneDrive (as Ron's is), the Documents rows are under
`OneDrive - …\Documents\`.

### 7.2 In the repository (building and releasing)

| What | Path |
|---|---|
| Version | `Directory.Build.props` → `TrafficLabPlusVersion` |
| Package manifest (identity, file type, capabilities) | `packaging/AppxManifest.xml` |
| Tiles and icons in the package | `packaging/Assets/` (68 PNGs, generated) |
| Build scripts | `packaging/pack.ps1`, `make-msixupload.ps1`, `make-store-assets.ps1`, `run-wack.ps1`, `make-icon.py` |
| The program's icon | `src/TrafficLabPlus.App/TrafficLabPlus.ico` (generated by `make-icon.py`) |
| The icon's sources, and the logo (2048×2048) | `resources/images/trafficlab-mark-2048.png`, `trafficlab-glyph-2048.png`, `TrafficLabPlus_logo2048.png` (generated) |
| Partner Center listing art | `resources/images/store/` (box art, poster, hero, store logo, contact sheet) |
| Store screenshots | `resources/images/screenshots/` |
| Release documents | `docs/release/` (this folder) |
| Hand tests | `docs/MANUAL-TESTING.md` |
| The plan (phase 8 is the Store) | `docs/DEVELOPMENT-PLAN.md` |

### 7.3 Build output (not committed — `artifacts/` is git-ignored)

| What | Path |
|---|---|
| **The upload for Partner Center** | `artifacts\TrafficLabPlus_<version>_x64_bundle.msixupload` |
| The bundle (for WACK) | `artifacts\TrafficLabPlus_<version>_x64.msixbundle` |
| Symbols | `artifacts\TrafficLabPlus_<version>_x64.appxsym` |
| The unpacked package (for the local install test) | `artifacts\stage\` |
| The self-contained publish | `artifacts\publish\win-x64\` |
| WACK report | `artifacts\wack-report.xml` |
| Hand-test copy (single file, not the Store build) | `manual-test\TrafficLabPlus.exe` |

### 7.4 Outside the repository

| What | Where |
|---|---|
| Privacy page source | `C:\Users\ronal\source\repos\SoftwarePlus\site\trafficlab\privacy\index.html` (deploy: `.\deploy.ps1` there) |
| The program's page | `SoftwarePlus\site\trafficlab\index.html` → https://softwareplus.ai/trafficlab/ |
| AiManager package feed | `C:\nuget-local\` (`Eaglin.AiManager` 1.4.1) |
| Partner Center | https://partner.microsoft.com → Apps and games → TrafficLab+ |

---

## 8. Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `make-msixupload.ps1` stops: "Package family name is …, not DeanEaglin.TrafficLab_xs303fgqvwdg8" | Name or Publisher in `packaging/AppxManifest.xml` differs from §2 — a typo or a case change. Copy them from Partner Center again |
| Partner Center rejects the upload's identity | Same cause. Also check the package was **not** signed |
| Partner Center: "version must be higher" | Raise `TrafficLabPlusVersion` (§4) and rebuild |
| `dotnet publish` cannot overwrite `TrafficLabPlus.exe` | TrafficLab+ (or a WACK run) is still open. Close it and rebuild |
| `appcert.exe` window flashes and closes, no report | It needs elevation and absolute paths. Use `packaging/run-wack.ps1`, which does both |
| WACK: "App resources" — an image over 204,800 bytes | Re-run `make-store-assets.ps1`; it steps colours down until every asset fits |
| WACK: "Blocked executables" (optional test) | Expected, as for Gamify+: TrafficLab+ opens pages in the browser (ShellExecute) and runs `git.exe` when publishing without a token. Explained in the certification notes |
| The installed app's tiles look soft | `resources.pri` missing; `pack.ps1` builds it with `makepri` — check it ran |
| Local install test: "package conflicts" | A Store-installed TrafficLab+ is present, or a previous test registration. `Get-AppxPackage -Name DeanEaglin.TrafficLab \| Remove-AppxPackage` |
| The Map step is blank in the installed app | The `Map\` folder (map.html and Leaflet) must be in the package; `pack.ps1` copies the publish output, which includes it. Check `artifacts\stage\Map\map.html` exists |
| The preview says WebView2 is not installed | That machine lacks the Edge WebView2 runtime (Windows 11 and current Windows 10 have it). **Open in your browser** still works |

---

## 9. The scripts

| Script | What it does |
|---|---|
| `packaging/make-icon.py` | Draws the icon — the signal on a "+" on the guide-sign green — and writes `TrafficLabPlus.ico` and the 2048 sources the tiles are made from |
| `packaging/make-store-assets.ps1` | Makes the logo (the signal above the name) and every tile at five scales plus taskbar sizes, the `.trafficlab` file icon, the listing images and a contact sheet |
| `packaging/pack.ps1` | One `.msix`: self-contained publish → stage → `makepri` → `makeappx` → sign. Signs with a local development certificate unless `-SkipSigning`; a signed local build carries that certificate's publisher, the Store build keeps the real one |
| `packaging/make-msixupload.ps1` | The Store upload: `pack.ps1 -SkipSigning` → `makeappx bundle` → zip with the symbols → **checks the package family name** |
| `packaging/run-wack.ps1` | Elevates, runs the certification kit against the newest bundle, prints pass/fail |
| `tools/store-shots.ps1` | Photographs the nine listing screenshots at 1920 × 1080 into `resources/images/screenshots/` with PrintWindow, so only TrafficLab+ is ever captured; sets this machine's recent studies aside for the start screen so no folder path shows |
