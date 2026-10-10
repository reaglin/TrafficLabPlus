# TrafficLab+ — Microsoft Store submission

Everything Partner Center asks for that is not listing copy. Listing text is in
`STORE_LISTING.md`; the procedure, the identity and where every file lives are in
`DEPLOY_TO_WIN_APP_STORE.md`. Modelled on Gamify+'s (same account, same packaging, passed
certification twice).

## Where each item stands

| Item | Status |
|---|---|
| Name reserved | ✅ **TrafficLab+** — `DeanEaglin.TrafficLab`, Store ID 9P5NGQF4HRPD (Ron, identity sent 2026-10-10) |
| Privacy page | ✅ Live at `https://softwareplus.ai/trafficlab/privacy/` (2026-10-09) |
| `runFullTrust` justification | ✅ Written — below |
| Version | ✅ **1.0.0** |
| Logo and Store assets | ✅ Made by `packaging/make-store-assets.ps1` from the icon Ron chose 2026-10-09; 68 package images, all under WACK's size cap |
| `.msixupload` | ✅ Built 2026-10-10, package family name verified. The package was installed on this machine (`Add-AppxPackage -Register`), started from the Start menu entry, opened the LPGA example and played the page, and was removed again |
| Listing copy, category, age rating | ✅ `STORE_LISTING.md` and below |
| Screenshots | ✅ Nine at 1920 × 1080 in `resources/images/screenshots/` (`tools/store-shots.ps1`); no folder path or GitHub account in any of them — **Ron to look before uploading** |
| WACK | ✅ **PASS**, 23 of 24, on 1.0.0.0 (Ron, 2026-10-10) — see Result below |
| Submit | ✅ Submitted by Ron 2026-10-10 — **live on the Store 2026-10-10** (tagged `v1.0.0`) |

## The Partner Center account

| | |
|---|---|
| Sign-in | ron.eaglin@gmail.com |
| Publisher display name | **Dean Eaglin** |
| Publisher | `CN=E88392BA-A722-4B3A-8372-04403A55AA63` — per account, the same as Gamify+, SMADA, Statistle, CIATLE |
| Package family name | `DeanEaglin.TrafficLab_xs303fgqvwdg8` |
| Store ID | `9P5NGQF4HRPD` — the listing will be https://apps.microsoft.com/detail/9P5NGQF4HRPD |
| Other apps | https://apps.microsoft.com/search/publisher?name=Dean+Eaglin |

## `runFullTrust` justification

Paste into *Submission → Product declarations → notes*, and into the certification notes:

> TrafficLab+ is a Win32 desktop application (WPF on .NET 10) distributed as an MSIX package. The
> `runFullTrust` restricted capability is required for any desktop application packaged this
> way. TrafficLab+ uses it to run as an ordinary desktop process; to read and write the user's own
> files through standard Windows file dialogs — `.trafficlab` study files and the web pages it
> builds, saved by default under the user's `Documents\TrafficLabPlus` folder; to open a built page
> or a help link in the user's default browser and show a folder in Explorer; and, only when the
> user publishes to GitHub Pages without saving a GitHub token, to run the user's own installed
> `git.exe`. It does not access other applications' data, does not require administrator rights,
> installs no drivers or services, and collects no user data. `internetClient` is used only for
> what the user starts: the map and its roads (OpenStreetMap: tile.openstreetmap.org,
> nominatim.openstreetmap.org, overpass-api.de), traffic counts for Florida roads (the Florida
> Department of Transportation's public map on services1.arcgis.com), the optional AI features (the
> AI provider whose key the user entered), and publishing to GitHub Pages (api.github.com).

## Age rating (IARC questionnaire)

TrafficLab+ is a tool for making traffic-simulation web pages for a class. Expected result:
**3+ / Everyone**.

| Question area | Answer |
|---|---|
| Violence, fear, sexual content, profanity, drugs, gambling | None |
| In-app purchases | None |
| User-to-user interaction, chat | None |
| User-generated content shared with others | **Consider carefully**, as for Gamify+: studies are made by the user and published by the user to their own GitHub Pages site. There is no sharing *through* the app or any service of ours — answer as Gamify+ was answered |
| Collects or shares personal information | No |
| Shares the user's location | No — the map shows places the user searches for; the app never reads the device's location |
| Displays advertising | No |
| Unrestricted internet access | No — connections only to OpenStreetMap, FDOT's map, the AI provider the user chose and GitHub, each when the user asks |

## Product declarations

| Declaration | Answer |
|---|---|
| Allows purchases outside the Microsoft commerce engine | No |
| Tested to meet accessibility guidelines | **No** — leave unchecked; no formal accessibility pass has been done |
| Depends on non-Microsoft drivers or NT services | No |
| Can run on Windows in S mode | No — a `runFullTrust` desktop app cannot |
| Contains cryptography | Yes, standard only: HTTPS and Windows DPAPI for the stored GitHub token and AI keys — all from Windows and .NET. Answer as for Gamify+ |

## Notes for certification

Paste into *Submission options → Notes for certification*:

> TrafficLab+ requires no account, sign-in or payment, and has no trial or paid tier. To exercise
> it: on the start screen press **Open the LPGA Traffic Lab example** (or **Open the Four-Way
> Intersection example**). The study opens on its steps, listed on the left: Map, Network, Traffic,
> Challenge, Preview, Publish; **Next** and **Previous** at the bottom move between them. The right
> half of the window is the web page the program builds (Microsoft Edge WebView2), rebuilt after
> every change: press **Start** on it, then **Run traffic test** and **Submit plan** to play it as a
> student would. **Open in your browser** opens the same page in the default browser.
>
> The internet is used only for features the tester starts: the **Map** step shows OpenStreetMap
> and loads roads when **Load the roads shown on the map** is pressed; the **Traffic** step looks
> up Florida's FDOT counts on a button press; the buttons that say "with the AI" need the tester's own
> key for an AI provider (Settings) and say so when none is set; **Publish** needs a GitHub
> account. None is needed to test the app: both examples work offline.
>
> User files are written to the signed-in user's `Documents\TrafficLabPlus` folder. ShellExecute is
> used to open a built page or a help link in the default browser and to show a folder in
> Explorer; `git.exe` (the user's own) is run only when publishing without a GitHub token. Nothing
> else is launched.

## Pricing and availability

| Field | Value |
|---|---|
| Base price | **Free** |
| Free trial | None |
| Markets | All |
| Visibility | Public — available and discoverable |
| Device families | Windows 10/11 desktop, x64 |
| Publish date | As soon as it passes certification |

## System requirements

| | |
|---|---|
| OS | Windows 10 version 2004 (build 19041) or later — the manifest's `MinVersion`. Windows 11 supported |
| Architecture | x64 |
| Disk | ~200 MB (self-contained; carries the .NET runtime, so nothing else to install) |
| Memory | 4 GB recommended |
| Other | Microsoft Edge WebView2 runtime for the preview and the map (present on Windows 11 and current Windows 10). Git for Windows only if publishing without a token |

## WACK

```powershell
pwsh ./packaging/run-wack.ps1
```

Picks the newest `.msixbundle` in `artifacts\`, prompts for elevation, prints pass/fail; the full
report is `artifacts\wack-report.xml`. Do not run `appcert.exe` by hand (see
`DEPLOY_TO_WIN_APP_STORE.md` §8).

**Expected, from Gamify+'s runs (same packaging):**

| Earlier finding | TrafficLab+ |
|---|---|
| `App resources` — images over 204,800 bytes | `make-store-assets.ps1` keeps all 68 assets under the cap |
| `DPIAwarenessValidation` | `src/TrafficLabPlus.App/app.manifest` declares `PerMonitorV2` |
| `Blocked executables` (optional) | Expect the same optional FAIL Gamify+ passed overall with (23 of 24): most in the .NET runtime, the rest `ShellExecute`/`Process.Start` for the browser and Explorer and `git.exe` for publishing — all described in the certification notes |

### Result

**2026-10-10, on 1.0.0.0 — OVERALL PASS, 23 of 24** (run by Ron; report `artifacts\wack-report.xml`).

The one FAIL is `Blocked executables`, **optional** — the same finding Gamify+, SMADA and Statistle
passed certification with. 42 of its 46 messages are inside Microsoft's own runtime, which a
self-contained package must carry (`System.Diagnostics.Process.dll`, `mscordbi.dll`,
`System.CodeDom.dll`…). Four name TrafficLab+'s own files:

| Message | What it is |
|---|---|
| `TrafficLabPlus.exe` … `shell32.dll!ShellExecuteW` | Opening a built page or a help link in the browser, and a folder in Explorer |
| `TrafficLabPlus.dll` … `Process.Start` | The same, from the views |
| `TrafficLabPlus.Core.dll` … `Process.Start` | Running the user's own `git.exe` when publishing without a GitHub token |
| `TrafficLabPlus.dll` … blocked executable reference to `"cmd"` | **Not a program launch.** `cmd` is the name of a field in the messages the Map step sends to its map page (`new { cmd = "goto", … }` in `Views/MapView.cs`, read by `Map/map.html`); WACK matches the text. TrafficLab+ never runs `cmd.exe`. It could be renamed in a later version to quiet the message |

The first three are described in the certification notes. `App resources` and
`DPIAwarenessValidation` passed.

## Submission order

1. ~~Reserve the name~~ — done: TrafficLab+, `DeanEaglin.TrafficLab`.
2. ~~Publish the privacy page~~ — done, 2026-10-09.
3. ~~Set the release version~~ — 1.0.0.
4. ~~Generate the Store assets~~ — done.
5. ~~Build the `.msixupload`~~ — done, PFN-verified, installed and run locally.
6. ~~Run WACK~~ — PASS, 23/24, 2026-10-10.
7. ~~Partner Center~~ — submitted 2026-10-10: pricing → properties → age rating → packages → listing (copy, images,
   screenshots) → certification notes → submit (`DEPLOY_TO_WIN_APP_STORE.md` §5 step 7).
8. ~~Once live: tag, record, website~~ — live 2026-10-10; tagged `v1.0.0`; softwareplus.ai/trafficlab/ has the Store link.
   `softwareplus.ai/trafficlab/` (Store link, version, screenshots — the portfolio rule).

## Releases

| Version | Built | Submitted | Live | Store link |
|---|---|---|---|---|
| 1.0.0.0 | 2026-10-10 | 2026-10-10 | 2026-10-10 | https://apps.microsoft.com/detail/9P5NGQF4HRPD |
