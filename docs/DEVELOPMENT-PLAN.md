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
| 0.1 | ⚠️ `docs/PLAN.md`, this file, `CLAUDE.md`; LPGA page kept in `reference/` | Written 2026-10-09; Ron's five answers folded in the same day. **Action: Ron confirms the plan reads right** |
| 0.2 | ⚠️ Solution `TrafficLabPlus.slnx`, `Directory.Build.props` (version, nullable, TreatWarningsAsErrors), `nuget.config` pointing at `C:\nuget-local` | `dotnet build` clean — 2026-10-09 |
| 0.3 | ⚠️ `TrafficLabPlus.Core` (net10.0), `TrafficLabPlus.App` (net10.0-windows, WPF, WebView2), `TrafficLabPlus.Tests` (xUnit) | 2026-10-09: 14 tests green; `EngineTests` runs the 16 Node engine tests; `PagePlayTests` plays the page in headless Chrome |

---

## Phase 1 — the engine, made data-driven

The LPGA core is good; this phase changes what it reads, not how it drives.

| # | Task | Done when |
|---|---|---|
| 1.1 | ⚠️ Split LPGA's page into `web/core/trafficlab-core.js` (no drawing) and `web/ui/` | 2026-10-09. Action: Ron compares (hand-test 1) |
| 1.2 | ⚠️ The **study JSON schema** (format 1): nodes, links, pockets, signals, zones, scenarios, costs, budget, backdrop, sources; metres and m/s; **no plan** (pages open blank) | `docs/STUDY-FORMAT.md` written; `validate()` in the engine says what is wrong in plain words, and a page that cannot run shows that text. The C# check with the same messages comes with the C# study model (3.1) |
| 1.3 | ⚠️ Engine reads a study (`TG.load`) instead of the hard-coded tables | `samples/lpga.json`, generated from the original page by `tools/make-lpga-study.js` (a test keeps them in step) |
| 1.4 | ⚠️ Metres in the study; inside, the engine keeps LPGA's unit (1 u = 0.6 m) so every tuned constant is untouched, converting once at load with a 10⁻⁹ snap | LPGA's positions and speeds come back exactly (test) |
| 1.5 | ⚠️ General intersections: T and skewed 4-leg run; main street named per signal or taken as the widest opposed pair; 5 legs refused with a reason; roundabouts that exist today | Tests on each |
| 1.6 | ⚠️ Typed volumes (`demand.mode: volumes`, veh/h entering per road end) and a typed OD table, besides the gravity model. Turning percentages per approach: not yet — routing decides the turns | Tests: each end sends out exactly what was typed |
| 1.7 | ⚠️ **The yardstick**: LPGA as data matches the original page | **Exactly**, not within a tolerance: same OD, same v/c on every approach, every car in the same place after 400 s (today and with a plan), same score. The original page's core is loaded straight out of `reference/` for the comparison |
| 1.8 | ⚠️ Engine tests under Node (`tests/js/engine.test.js`, 16): determinism, signal timing, no conflicting greens, LOS thresholds, intersection shapes, demand, scoring, validation | Green in `dotnet test` |

**Hand-test:** `docs/MANUAL-TESTING.md`, tests 1–4 (phases 0–2 together).

---

## Phase 2 — build the page

| # | Task | Done when |
|---|---|---|
| 2.1 | ⚠️ `template.html` + `PageBuilder`: study + engine + interface + CSS + Overpass fonts (OFL, inlined) → **one HTML file, ~220 KB** | Headless Chrome records **no requests** from the page; a study cannot break out of its script element (test). The sub-path check comes with publishing (7.5) |
| 2.2 | ⚠️ The page's title, author, course and intro come from the study. The name box says **optional**; Start works with it blank and the printout then says "Name not given". The name can be added or changed later (header button, or on the printout) | Played headless with and without a name |
| 2.3 | ⚠️ "Made with TrafficLab+" on every page; "© OpenStreetMap contributors" when `sources.osm` says the roads came from OSM |  |
| 2.4 | ⚠️ The page **opens blank, as a puzzle**: today's network, no plan (question 1) | Test: no plan in a built page |
| 2.5 | ⚠️ Light and dark, phone width | Played headless at 390 px dark and 1440 px light: no sideways scroll |
| 2.6 | ⚠️ Page tests: build LPGA, play it headless (`tools/play-page.js`), buy a fix, submit, read the printout and what reaches the printer | Green |
| 2.7 | ⚠️ **Submit plan → printout**: the plan on the map now (tested first unless it was tested exactly as it stands), its cost against the budget, the score and its parts, every approach's v/c, LOS and delay today and with the plan, the name if given, the study and date. **Print or save as PDF** — Ctrl+P prints the printout too | Chrome's PDF: one page plus two rows on a second. Action: Ron prints one (hand-test 2) |
| 2.8 | ⚠️ US customary units on the page: road lengths in ft/mi, speeds in mph |  |
| 2.9 | ⚠️ **UX review** (cognitive walkthrough + Nielsen) of the page and the window, 2026-10-09: 0 critical, 6 major, 8 minor | Fixed: a test is filed under the settings it started with, and says so if they changed while it ran; Submit says it hands in the plan on the map now, and how to hand in an earlier test; the name can be added or changed after Start and on the printout; Clear plan asks first, and leaving the page with a plan asks first; the window reloads a re-opened study; Ctrl+P prints the printout; how to save a PDF; the goal and LOS A–F explained; How it works reopens the instructions; over budget said when it happens; roundabouts offer no turn lanes; the retiming cost explained; the tests table explained; toolbar labels; window tooltips and a guarded Open in your browser. **Open:** which demand a submitted plan is tested at — question 6 |

---

## Phase 3 — the window and the study file

| # | Task | Done when |
|---|---|---|
| 3.1 | `.trafficlab` study file (zip: `study.json`, OSM extract, notes); New / Open / Save / Recent | Round-trips; a newer-format file is refused, not half-read |
| 3.2 | Start screen: what TrafficLab+ is, the steps, Open the LPGA example / New study / Continue. **LPGA is built in** as the example | |
| 3.3 | Sections in the order the work happens: **Map ▸ Network ▸ Traffic ▸ Challenge ▸ Preview ▸ Publish**, plus Settings and About | |
| 3.4 | Network editor: nodes, links, lanes, pockets, signal timing — every field explained in plain words, with where its value came from (OSM tag / default / typed / AI) | |
| 3.5 | **Preview**: the built page in WebView2, rebuilt on every change | What the window shows is the file that will be published |
| 3.6 | About: "Created by Dr. Ron Eaglin" card and the link to softwareplus.ai/trafficlab/ | |
| 3.7 | UX review of every screen | Recorded here with the fixes |
| 3.8 | **New study asks for the budget** the simulation will use (with LPGA's $5M as the suggestion and a line on what it means) | The budget is on the page and in the score |

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
| 8.1 | ⚠️ Name **TrafficLab+** reserved in Partner Center — done by Ron 2026-10-09. **Action when packaging starts: Ron reads Package/Identity Name, Publisher and Publisher display name from Product identity** | Identity in the manifest |
| 8.2 | Icon: one white mark on guide-sign green with a "+" | Ron approves |
| 8.3 | `packaging/` ported from GamifyPlus; WACK | WACK passes |
| 8.4 | Privacy policy at `softwareplus.ai/trafficlab/privacy/` (says: OSM and FDOT queries, AI calls go to the provider the user chose, GitHub publish) | Live |
| 8.5 | `SoftwarePlus\site\trafficlab\`: summary, features, **live example pages** (LPGA and others) | Deployed with the release |
| 8.6 | Listing, screenshots, submit | Live on the Store |

---

## Open questions for Ron — 1–5 answered 2026-10-09; 6 open

1. ~~What does a published page open with?~~ **Blank, as a puzzle.** The page opens with today's
   network and no plan. The player builds a plan; when they **submit** it (run the scored test and
   finish), that plan and its score become the **printout**. Tasks 2.4, 2.7.
2. ~~Teacher hand-outs?~~ **Yes, and LPGA is built in as the example.** When a user creates a new
   study they set the **budget** the generated simulation uses. Tasks 3.2, 3.8.
3. ~~Units?~~ **US customary on the page** (mph, feet, veh/hr); metres inside. Task 2.8.
4. ~~Partner Center?~~ **TrafficLab+ is reserved** (Ron, 2026-10-09). Task 8.1 needs the identity
   values from Partner Center ▸ Product identity when packaging starts.
5. ~~A list of the class's links?~~ **No.** Students entering their names is enough. **The name is
   optional:** a player may leave it blank and still play, and the page says the name is optional.
   Task 2.2.

6. **Which demand is a submitted plan tested at?** (asked 2026-10-09, from the UX review) Today a plan is
   tested at whatever demand and inflows the player has set, and the score is not adjusted for them: at 50%
   demand a plan with no changes scores about 50, so two students' scores compare only if they tested at
   the same demand. Options:
   (a) **proposed:** the study names a **test demand** (e.g. "Race Week"), chosen by the author, and Submit
   always tests at it and says so; the toolbar stays free for exploring;
   (b) Submit tests at whatever is set, the printout says what that was (it does now), and the teacher
   tells the class which demand to use.

---

## References

| Document | What it is for |
|---|---|
| `PLAN.md` | The strategy, settled decisions, architecture, what is left out |
| `../reference/lpga-traffic-lab.html` | Ron's original LPGA Traffic Lab — the engine's source and the yardstick for task 1.7 |
| `STUDY-FORMAT.md` | The study JSON, field by field |
| `MANUAL-TESTING.md` | Ron's hand-tests, phase by phase |
| `../CLAUDE.md` | How to work in this repo |
