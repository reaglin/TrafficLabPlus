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
| 2.9 | ⚠️ **UX review** (cognitive walkthrough + Nielsen) of the page and the window, 2026-10-09: 0 critical, 6 major, 8 minor | Fixed: a test is filed under the settings it started with, and says so if they changed while it ran; Submit says it hands in the plan on the map now, and how to hand in an earlier test; the name can be added or changed after Start and on the printout; Clear plan asks first, and leaving the page with a plan asks first; the window reloads a re-opened study; Ctrl+P prints the printout; how to save a PDF; the goal and LOS A–F explained; How it works reopens the instructions; over budget said when it happens; roundabouts offer no turn lanes; the retiming cost explained; the tests table explained; toolbar labels; window tooltips and a guarded Open in your browser. Question 6 answered (b): the printout says the demand it was tested at, and names the button when it matches one ("150% demand (Race Week)") |

---

## Phase 3 — the window and the study file

| # | Task | Done when |
|---|---|---|
| 3.1 | ⚠️ `.trafficlab` study file (zip: `study.json`, `notes.md`, later `osm.json`; unknown entries and fields kept); New / Open / Save / Save as / Recent; a plain `.json` study opens too. C# study model with the engine's check in the same words (`StudyValidator`) | Tests: LPGA read and written again is identical; unknown fields kept; a newer format refused before anything is read; damaged file refused in words; save swaps in a whole file; the C# check gives **exactly** the page's messages over 23 studies (run through the engine under Node). Action: Ron, hand-test 6 |
| 3.2 | ⚠️ Start screen: what TrafficLab+ is, the four steps, Open the LPGA example / New study / Open a study file, and **Continue where you left off** (recent studies). **LPGA is built in** as the example | Action: Ron, hand-test 5 |
| 3.3 | ⚠️ Sections in the order the work happens: **Start, 1 Map ▸ 2 Network ▸ 3 Traffic ▸ 4 Challenge ▸ 5 Preview ▸ 6 Publish**, plus Settings and About. Map says it arrives in phase 4 and what to do meanwhile; Publish saves the page as a file until phase 7. A yellow bar lists what stops the page running, in the engine's words; each line goes to the thing to fix. Undo/redo of every change (Ctrl+Z also from a box with nothing of its own to undo); unsaved changes asked about before they are lost | Looked at in the running window (PrintWindow). Action: Ron, hand-tests 5–8 |
| 3.4 | ✅ Network editor: a drawing (click to choose, drag to move; add intersection, road end, road; remove) and a form for what is chosen — kind, names, signal cycle and split, main-street roads, protected lefts, lanes, speed in mph, position in feet, turn lanes that exist today — every field explained, out-of-range values refused beside the box, and **where each value came from** (LPGA example / starting value / typed; OSM and AI from phases 4 and 6). Traffic section (one total or typed volumes per road end, showing the engine's own veh/h per end; trips that do not happen; the demand buttons) and Challenge section (the page's words, budget, prices of the fixes, notes) | Tests on every edit (removing leaves nothing dangling, roads that cannot be built say why, kinds get what they need, turn lanes set and cleared) and on the veh/h shown against the engine's. Verified by Ron 2026-10-09 (hand-test 5): "The test was good, great option for building an intersection" |
| 3.5 | ✅ **Preview**: the built page in WebView2, rebuilt half a second after every change; the first showing opens with the page's instructions, a rebuild goes straight to the map (`?nointro`) | What the window shows is the file that will be published (Documents\TrafficLabPlus\Preview). Verified by Ron 2026-10-09 (hand-test 5): "The test was good, great option for building an intersection" |
| 3.6 | ⚠️ About: "Created by Dr. Ron Eaglin" card with the picture, the link to softwareplus.ai/trafficlab/, the credits (traffic model, OSM, Overpass font) | Action: Ron looks (hand-test 8) |
| 3.7 | ⚠️ **UX review** (cognitive walkthrough + Nielsen, `ux-reviewer`, plus the running window captured with PrintWindow), 2026-10-09: 1 critical, 3 major, 6 minor | Fixed: a value still being typed is now saved by Ctrl+S, Save, Close and Save the page (it was lost); Ctrl+Z after a change undoes the study, not just the box; **renaming a street renames it everywhere** (every road with that name, and the intersections and road ends named after it — one undo); the add-road message points to the form below; Esc and a second click leave an Add mode; Delete removes only from the drawing; the signal count in New study selects its layout; the budget box takes 3.5, $3.5M, 3.5 million or 3,500,000; the saved page is named after the title and its messages open over the window; road-end help fits typed volumes; "players" on the page, "students" for who hands it in; Help names the start screen. Also from looking: drawing labels no longer pile up or run off the edge; the page's instructions do not reopen on every rebuild. **Not done:** the engine's own words in the yellow bar ("road segment", ids) — they must change in the page's check and the window's together; a later round. Action: Ron, hand-tests 5–8 |
| 3.8 | ⚠️ **New study asks for the budget** (LPGA's $5M suggested, with a line on what it means), with a title, place, a starting layout — one signal, a T, or 2–10 signals along a main street — and author/course (defaults in Settings). Starting demand is moderately busy today and past capacity on "Busy day" | Test: the budget is in the built page; every starting layout passes the check and runs in the engine with every trip routed. Action: Ron, hand-test 6 |
| 3.9 | ⚠️ **A second built-in example: Four-Way Intersection** (Ron, 2026-10-09: "Add the 4 way intersection to the built in examples"). One signal, a four-lane main street across a two-lane side street, no turn lanes today, $2M budget (a roundabout is out of reach); today the side street is at LOS D (v/c 0.86), on Busy day it fails. Offered on the start screen and in File ▸ Open an example | `samples/four-way.json`, written by `tools/make-four-way.js` (a test keeps them in step); every built-in example passes the check, runs with every trip routed, and builds a page (tests). Action: Ron opens it (hand-test 5) |

**Hand-test:** `docs/MANUAL-TESTING.md`, tests 5–8.

---

## Phase 4 — the map and OpenStreetMap

| # | Task | Done when |
|---|---|---|
| 4.1 | ⚠️ Map section: WebView2 + Leaflet 1.9.4 (bundled, BSD), OpenStreetMap tiles with attribution and an identified User-Agent; search by place name (Nominatim, one search a second at most). Works with no study open; the start screen offers **New study from the map** | Nominatim finds streets and towns, **not crossings by two names** ("LPGA Blvd & Williamson Blvd" finds nothing). So the search takes you to "LPGA Boulevard, Daytona Beach", and the crossing is chosen in step 3, where the list can be filtered by street name ("Williamson"). Tried in the running window. Action: Ron, hand-test 9 |
| 4.2 | ⚠️ Choose intersections by clicking dots on the map or ticking them in a list (filter by street name); up to 10, numbered, reorderable. A divided road's crossing (four OSM points) and a roundabout ring are each **one** intersection; signals recognised within 60 m; slip lanes are not roads | Tests on the LPGA fixture (LPGA & Williamson is one junction of four points, with its signal) and on made-up maps (crossroads, roundabout ring). Action: Ron, hand-test 9 |
| 4.3 | ⚠️ `OsmClient`: one Overpass query for the box (refused before asking if over 4 km across), a second server if the first is busy, identified User-Agent, plain-words errors. The answer is kept in the study file as `osm.json` with the box, the date and the chosen junctions | An opened study's Map section shows its own saved roads and never asks again. Tests with a pretend network: User-Agent, too-big box, busy server, error page, Nominatim's answer |
| 4.4 | ⚠️ `NetworkFromOsm`: chosen junctions → signals (or roundabouts); roads between them → road segments; roads leaving → road ends 250 m out (closer, and said, where the loaded area ends); through lanes and turn lanes from `turn:lanes` / `lanes` / `lanes:forward|backward`, `oneway`, `maxspeed` (mph or km/h), `name`/`ref`; a fifth road left out and said; a junction with no signal in OSM starts as one and says so; the freeway and the streets around become backdrop | **LPGA from OSM matches Ron's LPGA in shape: 5 intersections, 8 road ends, 13 roads**; the differences are listed in `OSM-LPGA-COMPARISON.md` (today's street names, to scale, 2–3 lanes, right-turn lanes, two signals OSM lacks, timing and traffic as starting values). Test: it passes the check and runs with every trip routed |
| 4.5 | ⚠️ Every value says where it came from: names, lanes, speeds, turn lanes and signal/roundabout **from OpenStreetMap** when tagged; everything else **a starting value** | In Network's "Where this came from" lines (tests on the origins) |
| 4.6 | ⚠️ OSM tests against saved Overpass answers and made-up maps (no network in tests) | 24 OSM and client tests, green with the rest |
| 4.7 | ⚠️ **UX review** of the Map section (cognitive walkthrough + Nielsen, `ux-reviewer`, plus the running window), 2026-10-09: 1 critical, 4 major, 7 minor | Fixed: the map no longer shows another study's saved roads as the open study's; chosen intersections can be reordered (↑ ↓); ticking one no longer jumps the list to the top or zooms the map; why Load is greyed out is always written, not only in a tooltip; the made-study message shows five notes and says where the rest are, and explains "road end"; remaking from saved roads says edits are not carried over; unsaved changes are asked about only once the new study is really coming; the ten-limit says the click did nothing; one set of words for signal / roundabout / no signal in the list and on the map; the legend explains the yellow numbered dots and the dashed box; the title starts from the first intersection; plain-words errors with the details after. Also from looking: slip lanes are not roads; intersection names list only the roads kept. Action: Ron, hand-tests 9–10 |

**Hand-test:** `docs/MANUAL-TESTING.md`, tests 9–10.

---

## Phase 5 — traffic volumes

| # | Task | Done when |
|---|---|---|
| 5.1 | Traffic section: entering volume per zone, scenarios (Today, a future year, an event) with multipliers, typed in a table that explains veh/hr | Built early, with phase 3 (3.4); what phase 5 adds is FDOT and the AADT conversion |
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

## Open questions for Ron — all 6 answered (2026-10-09)

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

6. ~~Which demand is a submitted plan tested at?~~ **(b), answered by Ron 2026-10-09:** a plan is tested at
   whatever the player has set, the printout says what that was (and names the demand button when it
   matches one), and the teacher tells the class which demand to use. Task 2.9. The question as asked: today a plan is
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
| `OSM-LPGA-COMPARISON.md` | LPGA built from OpenStreetMap against Ron's hand-made LPGA: what matches, what differs and why (task 4.4) |
| `../CLAUDE.md` | How to work in this repo |
