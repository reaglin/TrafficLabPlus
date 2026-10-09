# TrafficLab+ — development plan

Numbered tasks per phase, each with what "done" means. Started **2026-10-09**; the strategy and
the decisions behind it are in `PLAN.md`.

---

## How an item is marked

| Mark | Means |
|---|---|
| ⬜ | to do — not started or still being built |
| ⚠️ | **action needed** — built and tested, waiting for Ron to look; or Ron returned it with a comment; or blocked on an answer from Ron. The row says what the action is |
| ✅ | done — built, tested, **and verified by Ron** |

Dropped items are struck through with ❌ and the reason.

Every item goes: specification → questions → code + tests → UX review (cognitive walkthrough +
Nielsen heuristics, `ux-reviewer` agent, and a look at the running window) → Ron's hand-test →
✅, or back with his comment. Each phase ends with something Ron can open and try.

---

## Phase 0 — the plan and the repository

| # | Task | Done when |
|---|---|---|
| 0.1 | ⚠️ `docs/PLAN.md`, this file, `CLAUDE.md`; LPGA page kept in `reference/` | Written 2026-10-09. **Action: Ron reads the plan and answers the open questions below** |
| 0.2 | Solution `TrafficLabPlus.slnx`, `Directory.Build.props` (version, nullable, TreatWarningsAsErrors), `nuget.config` pointing at `C:\nuget-local` | `dotnet build` clean |
| 0.3 | `TrafficLabPlus.Core` (net10.0), `TrafficLabPlus.App` (net10.0-windows, WPF, WebView2), `TrafficLabPlus.Tests` (xUnit) | Builds; one test runs the JS engine under Node |

---

## Phase 1 — the engine, made data-driven

The LPGA core is good; this phase changes what it reads, not how it drives.

| # | Task | Done when |
|---|---|---|
| 1.1 | Split LPGA's page into `web/core/trafficlab-core.js` (no drawing) and `web/ui/` | The split page behaves as the original |
| 1.2 | The **study JSON schema**: nodes, links, pockets, signals, zones, scenarios, challenge, plan; metres and m/s | Documented in `docs/STUDY-FORMAT.md`; validated in C# and in JS with the same messages |
| 1.3 | Engine reads a study instead of the hard-coded `NODES`/`LINKS`/`ZONES`/`COSTS`/`BUDGET` | `samples/lpga.json` runs |
| 1.4 | Metres instead of LPGA's 1 px = 0.6 m; drawing scales to the canvas | LPGA's constants converted once, by a test-checked table |
| 1.5 | General intersections: 3-leg (T) and 4-leg at any angle; main-street group chosen per signal; ≥5 legs refused with a reason | Tests on a T, a skewed 4-leg and a 5-leg refusal |
| 1.6 | Typed volumes as well as gravity weights: entering volume per zone, optional OD or turning percentages | Both drive the same OD matrix |
| 1.7 | **The yardstick**: LPGA as data matches the original page | Same seeds, same demand → today's average delay and LOS per approach within a stated tolerance of the original |
| 1.8 | Engine tests under Node: determinism by seed, signal phase timing, v/c and LOS thresholds, roundabout capacity, scoring | Green in `dotnet test` |

**Hand-test:** open `samples/lpga.html` (built from data) beside the original and compare.

---

## Phase 2 — build the page

| # | Task | Done when |
|---|---|---|
| 2.1 | `template.html` + `PageBuilder`: study + engine + UI + CSS + font → **one self-contained HTML file** | Opens from disk and from a sub-path; no network requests (checked in headless Chrome) |
| 2.2 | The page's title, author, course, and intro built from the study (LPGA's intro dialog, generalised) | |
| 2.3 | "© OpenStreetMap contributors" and "Made with TrafficLab+" on every page | |
| 2.4 | The student's plan shown as their answer (see question 1) | |
| 2.5 | Light and dark, phone width (16 px gutter, no sideways scroll) | Checked in headless Chrome at 390 px and 1440 px |
| 2.6 | Page tests: build LPGA, load it headless, run a traffic test, read the score | Green |

---

## Phase 3 — the window and the study file

| # | Task | Done when |
|---|---|---|
| 3.1 | `.trafficlab` study file (zip: `study.json`, OSM extract, notes); New / Open / Save / Recent | Round-trips; a newer-format file is refused, not half-read |
| 3.2 | Start screen: what TrafficLab+ is, the steps, Open the LPGA example / New study / Continue | |
| 3.3 | Sections in the order the work happens: **Map ▸ Network ▸ Traffic ▸ Challenge ▸ Preview ▸ Publish**, plus Settings and About | |
| 3.4 | Network editor: nodes, links, lanes, pockets, signal timing — every field explained in plain words, with where its value came from (OSM tag / default / typed / AI) | |
| 3.5 | **Preview**: the built page in WebView2, rebuilt on every change | What the window shows is the file that will be published |
| 3.6 | About: "Created by Dr. Ron Eaglin" card and the link to softwareplus.ai/trafficlab/ | |
| 3.7 | UX review of every screen | Recorded here with the fixes |

**Hand-test:** open the LPGA example, change a signal's cycle, watch the preview change.

---

## Phase 4 — the map and OpenStreetMap

| # | Task | Done when |
|---|---|---|
| 4.1 | Map view: WebView2 + Leaflet (bundled), OSM tiles with attribution, search by place name (Nominatim) | Finds "LPGA Blvd & Williamson Blvd, Daytona Beach" |
| 4.2 | Pick intersections by clicking; up to ~10; signals and roundabouts recognised from OSM | |
| 4.3 | `OverpassClient`: one query for the box; identified User-Agent; cached in the study | Never re-queries an opened study |
| 4.4 | `NetworkFromOsm`: ways → links between the chosen nodes; `lanes`, `lanes:forward/backward`, `turn:lanes`, `oneway`, `maxspeed`, `name`; edge zones where roads leave the study | LPGA from OSM comes out close to Ron's hand-made LPGA; the differences listed |
| 4.5 | Everything that came from a default rather than a tag is marked as such in the editor | |
| 4.6 | OSM tests against saved Overpass responses (no network in tests) | Green |

**Hand-test:** build LPGA from the map and compare it with the example.

---

## Phase 5 — traffic volumes

| # | Task | Done when |
|---|---|---|
| 5.1 | Traffic section: entering volume per zone, scenarios (Today, a future year, an event) with multipliers, typed in a table that explains veh/hr | |
| 5.2 | **FDOT AADT lookup** from FDOT's open data for each road in a Florida study, with the count year and station | LPGA's roads return counts |
| 5.3 | AADT → peak-hour volume with **K** and **D** factors (FDOT defaults, editable, explained) | |
| 5.4 | Outside Florida, the lookup says so and offers typing or the AI | |

---

## Phase 6 — the AI

Through `Eaglin.AiManager` and `Eaglin.AiManager.Wpf` (latest on the local feed: 1.4.1), app name
`TrafficLabPlus`. The AI proposes; the student sees each change and accepts or declines it.

| # | Task | Done when |
|---|---|---|
| 6.1 | AI settings and usage windows from `Eaglin.AiManager.Wpf`; one `AskAsync` that never throws | |
| 6.2 | **Build the network**: fill in lanes, pockets and signal plans OSM lacks, from the student's words | Changes shown with reasons; nothing applied unseen |
| 6.3 | **Estimate demand** from land use (OSM) and any AADT | |
| 6.4 | **Explain and coach** after a traffic test: why an approach fails, what might help, a critique of the plan; optionally put on the page as the student's notes | |
| 6.5 | **Write a challenge** from a teacher's request: budget, costs, allowed fixes, scenarios, goals | |
| 6.6 | Readers that cope with what an AI actually answers (Gamify+'s `DraftReader` lesson) | Tests on real answers |

---

## Phase 7 — publish and hand in

| # | Task | Done when |
|---|---|---|
| 7.1 | Port `Publisher`, `GitHubApi`, `GitCli`, `TokenStore` from GamifyPlus | |
| 7.2 | One repository per student (`TrafficLab`), one folder per study; publishing one study never removes another; an index page of the student's studies | |
| 7.3 | Marked as made by TrafficLab+ (topic, description, `trafficlab-site.json`, generator meta) | A publish refuses a repository it did not make |
| 7.4 | **"Copy the link to hand in"** after a publish, and a plain "New to GitHub?" guide | |
| 7.5 | Real publish with Ron's token | Ron opens the link on his phone |

---

## Phase 8 — the Microsoft Store and SoftwarePlus.ai

| # | Task | Done when |
|---|---|---|
| 8.1 | Name **TrafficLab+** reserved in Partner Center (question 4) | Identity known |
| 8.2 | Icon: one white mark on guide-sign green with a "+" | Ron approves |
| 8.3 | `packaging/` ported from GamifyPlus; WACK | WACK passes |
| 8.4 | Privacy policy at `softwareplus.ai/trafficlab/privacy/` (says: OSM and FDOT queries, AI calls go to the provider the user chose, GitHub publish) | Live |
| 8.5 | `SoftwarePlus\site\trafficlab\`: summary, features, **live example pages** (LPGA and others) | Deployed with the release |
| 8.6 | Listing, screenshots, submit | Live on the Store |

---

## Open questions for Ron

Asked 2026-10-09.

1. **What does a published page open with?** Proposed: the study as it is today, with the
   student's plan loaded as "the engineer's plan" and its score shown; a visitor can clear it and
   try their own. (Alternative: no plan shown, so the page is a puzzle for the next person.)
2. **Teacher hand-outs:** may a teacher give students a starting study file (e.g. LPGA with a $5M
   budget) to open, change and publish? Proposed: yes — it is just a `.trafficlab` file.
3. **Units:** US customary on the page (mph, feet, veh/hr), as LPGA? Proposed: yes; metres inside.
4. **Partner Center:** please reserve **TrafficLab+** (new product) when convenient; phase 8 needs
   the identity.
5. **Does the class need a list of everyone's links?** E.g. the teacher pastes the links into
   TrafficLab+ and gets one page of all the class's studies. Not planned unless you want it.

---

## References

| Document | What it is for |
|---|---|
| `PLAN.md` | The strategy, settled decisions, architecture, what is left out |
| `../reference/lpga-traffic-lab.html` | Ron's original LPGA Traffic Lab — the engine's source and the yardstick for task 1.7 |
| `../CLAUDE.md` | How to work in this repo |
