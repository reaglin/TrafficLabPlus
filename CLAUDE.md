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

**Status: plan stage, 2026-10-09** — no code yet. Open questions for Ron are in the plan.

### Read these first

| File | What it is |
|---|---|
| `docs/DEVELOPMENT-PLAN.md` | **The work breakdown** — phases, tasks, marks, open questions. Start here |
| `docs/PLAN.md` | The strategy: decisions with Ron and why, the study, the architecture |
| `reference/lpga-traffic-lab.html` | Ron's original page: the engine to generalise and the yardstick for it |

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
