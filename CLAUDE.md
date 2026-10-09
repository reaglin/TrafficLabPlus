# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

**TrafficLab+** (repo `TrafficLabPlus`) — a Windows 10/11 program that turns a real intersection,
or a corridor of up to about ten, into a **web page with a live traffic simulation**, like Ron's
*LPGA Traffic Lab* (`reference/lpga-traffic-lab.html`). A student finds the intersection on a map,
the roads come from **OpenStreetMap**, volumes are typed, looked up (FDOT AADT) or estimated by
the **AI** (through the shared `Eaglin.AiManager`), the program previews the page, publishes it to
**GitHub Pages**, and the student **hands in the link**. WPF on .NET 10, free on the Microsoft Store.

**The point of the software is the web page** (Ron, 2026-10-09). The engine is JavaScript because
it runs in the published page; C# does the map, the study, the AI, building and publishing.

**Status: phases 0–3 built, 2026-10-09** (the day it started). The LPGA engine is data-driven and
reproduces Ron's original page **exactly** (same cars, same score); the page builder makes one
self-contained HTML file. The page opens blank as a puzzle, the name is optional, and **Submit plan**
makes the printout. Phase 3: the `.trafficlab` study file, a start screen, the sections (Map ▸ Network ▸
Traffic ▸ Challenge ▸ Preview ▸ Publish), a network editor (drawing + forms) with the page rebuilt beside
it after every change, undo, New study with its budget, About. 40 tests green. Everything is ⚠️ waiting
for Ron (`docs/MANUAL-TESTING.md`, tests 1–8). Next: the map and OpenStreetMap (phase 4).

### Read these first

| File | What it is |
|---|---|
| `docs/DEVELOPMENT-PLAN.md` | **The work breakdown** — phases, tasks, marks, open questions. Start here |
| `docs/PLAN.md` | The strategy: decisions with Ron and why, the study, the architecture |
| `reference/lpga-traffic-lab.html` | Ron's original page: the engine to generalise and the yardstick for it |
| `docs/STUDY-FORMAT.md` | The study JSON, field by field |
| `docs/MANUAL-TESTING.md` | Ron's hand-tests |

## Layout

```
TrafficLabPlus.slnx
├── web/                 THE PAGE, embedded into Core (editing it changes nothing until `dotnet build`)
│   core/trafficlab-core.js   the engine: validate + load a study, then LPGA's model unchanged
│   ui/trafficlab-ui.js       canvas, inspector, plan, tests, Submit → printout
│   ui/trafficlab.css, fonts/ (Overpass, OFL), template.html
├── src/TrafficLabPlus.Core/  Build/PageBuilder (study → one HTML file), WebAssets
│                             Model/: Study (+ unknown fields kept), StudyJson, StudyValidator (the
│                             engine's check, same words), StudyFile (.trafficlab), StudyEdits,
│                             StudyTemplates (New study layouts), DemandShares, Units, RecentStudies
├── src/TrafficLabPlus.App/   WPF: MainWindow (sections, problems bar, preview), StudySession (undo,
│                             unsaved), NewStudyWindow; Views/: Form (field helper), NetworkCanvas,
│                             Network/Traffic/Challenge/Start/Info views, PreviewView (WebView2)
├── tests/TrafficLabPlus.Tests/  xUnit; EngineTests runs tests/js under Node; PagePlayTests plays the page
├── tests/js/            engine.test.js, reference.js (loads the ORIGINAL core out of reference/)
├── samples/lpga.json    the built-in example — generated, never hand-edited: tools/make-lpga-study.js
└── tools/               play-page.js (headless Chrome), engine-check.js (the engine for C# tests),
                         capture-window.ps1, tour-window.ps1 (UI Automation tour + PrintWindow shots;
                         sends keys only when the app is the window in front), make-lpga-study.js
```

## Build, test, run

```powershell
dotnet build TrafficLabPlus.slnx
dotnet test TrafficLabPlus.slnx          # needs node on PATH; plays the page if Chrome or Edge is installed
node --test tests/js/engine.test.js      # the engine alone
dotnet run --project src/TrafficLabPlus.App
.\src\TrafficLabPlus.App\bin\Debug\net10.0-windows\TrafficLabPlus.exe --build-example out.html
.\src\TrafficLabPlus.App\bin\Debug\net10.0-windows\TrafficLabPlus.exe --build-page study.json out.html
node tools/play-page.js out.html "Maria Gomez" shot.png   # TL_WIDTH=390 TL_DARK=1 TL_PRINT=p.pdf
.\src\TrafficLabPlus.App\bin\Debug\net10.0-windows\TrafficLabPlus.exe --new-study 3 study.json
```

**The engine is LPGA's.** Change its behaviour only on purpose: the yardstick tests compare it car
for car with the original page, so any change to the model shows up there first. A deliberate change
means the yardstick tests change with it, and the plan says why.

## Rules that carry the design

1. **The preview is the page.** The window shows the built HTML in WebView2; never a separate
   rendering of the simulation.
2. **A page needs nothing.** One self-contained HTML file: no CDN, no web fonts from a server, no
   map tiles. It opens from disk and from a GitHub Pages sub-path.
3. **No Google Maps.** Road geometry comes from OpenStreetMap only (ODbL; attribution on every page).
4. **The AI proposes; the student accepts.** Nothing from the AI enters a study unseen.
5. **Say where every number came from** — OSM tag, default, typed, FDOT, or AI.
6. **The program is TrafficLab+ in front of a person, `TrafficLabPlus` in the code.**

## Conventions

- Portfolio rules apply (`..\CLAUDE.md`): `docs/DEVELOPMENT-PLAN.md` marks (Claude never marks ✅),
  the development cycle with a UX review, the About card with "Created by Dr. Ron Eaglin" and the
  link to softwareplus.ai/trafficlab/, and SoftwarePlus.ai updated with every Store release.
- `Eaglin.AiManager` is a package from `C:\nuget-local`, never a project reference.
- Check the running window with PrintWindow + UI Automation; check pages with headless Chrome.

## Sibling repos worth knowing

- `..\GamifyPlus` — the closest model: self-contained HTML pages from templates, WebView2 play,
  GitHub Pages publishing, AI over AiManager, Store packaging.
- `..\LMS-2-Website` — the publishing rules (marked repositories, `PublishWords`, token handling)
  and the model `DEVELOPMENT-PLAN.md`.
- `..\AiManager` — the AI layer.
