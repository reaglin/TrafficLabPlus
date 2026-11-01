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
| 0.3 | ⚠️ `TrafficLabPlus.Core` (net10.0), `TrafficLabPlus.App` (net10.0-windows10.0.19041.0 since 9.3, WPF, WebView2), `TrafficLabPlus.Tests` (xUnit) | 2026-10-09: 14 tests green; `EngineTests` runs the 16 Node engine tests; `PagePlayTests` plays the page in headless Chrome |

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
| 3.6 | ✅ About: "Created by Dr. Ron Eaglin" card with the picture, the link to softwareplus.ai/trafficlab/, the credits (traffic model, OSM, Overpass font) | Ron, 2026-10-10: "Both are good." |
| 3.7 | ⚠️ **UX review** (cognitive walkthrough + Nielsen, `ux-reviewer`, plus the running window captured with PrintWindow), 2026-10-09: 1 critical, 3 major, 6 minor | Fixed: a value still being typed is now saved by Ctrl+S, Save, Close and Save the page (it was lost); Ctrl+Z after a change undoes the study, not just the box; **renaming a street renames it everywhere** (every road with that name, and the intersections and road ends named after it — one undo); the add-road message points to the form below; Esc and a second click leave an Add mode; Delete removes only from the drawing; the signal count in New study selects its layout; the budget box takes 3.5, $3.5M, 3.5 million or 3,500,000; the saved page is named after the title and its messages open over the window; road-end help fits typed volumes; "players" on the page, "students" for who hands it in; Help names the start screen. Also from looking: drawing labels no longer pile up or run off the edge; the page's instructions do not reopen on every rebuild. **Not done:** the engine's own words in the yellow bar ("road segment", ids) — they must change in the page's check and the window's together; a later round. Action: Ron, hand-tests 5–8 |
| 3.8 | ⚠️ **New study asks for the budget** (LPGA's $5M suggested, with a line on what it means), with a title, place, a starting layout — one signal, a T, or 2–10 signals along a main street — and author/course (defaults in Settings). Starting demand is moderately busy today and past capacity on "Busy day" | Test: the budget is in the built page; every starting layout passes the check and runs in the engine with every trip routed. Action: Ron, hand-test 6 |
| 3.9 | ⚠️ **A second built-in example: Four-Way Intersection** (Ron, 2026-10-09: "Add the 4 way intersection to the built in examples"). One signal, a four-lane main street across a two-lane side street, no turn lanes today, $2M budget (a roundabout is out of reach); today the side street is at LOS D (v/c 0.86), on Busy day it fails. Offered on the start screen and in File ▸ Open an example | `samples/four-way.json`, written by `tools/make-four-way.js` (a test keeps them in step); every built-in example passes the check, runs with every trip routed, and builds a page (tests). Action: Ron opens it (hand-test 5) |
| 3.10 | ⚠️ **← Previous / Next →** through Map ▸ Network ▸ Traffic ▸ Challenge ▸ Preview ▸ Publish (Ron, 2026-10-09: "The only thing needed is a "Next ->" and "<- Previous""). A bar at the foot of each step says which step it is (Step 2 of 6) and names the steps either side; with no study open, Next is held at Map and says why | Tried in the running window. Action: Ron, hand-test 12 |
| 3.11 | ⚠️ **Optional clearly stated, detailed help** on every step (Ron, same day): a box at the top — *What you need to do here* and *Optional* — and **Help for this step: every option explained**, opening to every setting described in plain words; each setting tagged **required** (the page needs a value; it starts with one) or **optional**; Preview has its own guide beside the page | Action: Ron, hand-test 12 |
| 3.12 | ⚠️ **UX review** of the steps and guides, 2026-10-09: 0 critical, 3 major, 6 minor | Fixed: the tag reads **needs a value** ("always has one; change it only if it is wrong"), not "required", so no student thinks they must change it; the demand buttons and the FDOT K, D and Use boxes are tagged optional as the guide says; Network's guide sits under its title, not over the chosen item's settings; the sign-letters help says empty shows TL+; the Map note names the button, not "step 4"; the Map tooltip no longer says "next version"; a screen reader hears where Previous and Next go; Preview says it can be skipped; title, lead, guide in the same order everywhere. Action: Ron, hand-test 12 |

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
| 5.1 | ⚠️ Traffic section: one total shared by how busy each road end is, or vehicles an hour typed per road end; trips that do not happen; the demand buttons (Today, a future year, an event) with their percentages; each road end shows the veh/h the engine will send in | Built early, with phase 3 (3.4). Action: Ron, hand-test 11 |
| 5.2 | ⚠️ **FDOT AADT lookup** from FDOT's own public layer (Transportation Data and Analytics, `Annual_Average_Daily_Traffic_TDA`): one query for the study's box; each counted stretch matched to the road end it leads to (runs along the road, within 40 m; ramps only to one-way counts); the site, year, AADT and FDOT's description shown before anything is used; the answer kept in the study file (`fdot.json`). A study made from the map knows where it is on the earth (`geo`) | **LPGA's roads return counts**: LPGA Blvd (site 797025, 29,000/day, 2025), Williamson north and south (797087, 797086), the I-95 off-ramp (792025); Outlet, Cornerstone and the short ramp beside LPGA have none, and say so. Tests on the saved answer; tried live in the window |
| 5.3 | ⚠️ AADT → design-hour volume: **AADT × K × D** in, AADT × K × (1 − D) out; an off-ramp brings AADT × K in, an on-ramp takes it out. **K and D start at FDOT's own factors for each count**, can be changed per road end, and are explained in plain words; each count can be left unticked. "Use the ticked counts" switches to typed volumes (one undo); uncounted road ends keep their estimates; the page and printout credit FDOT | Tests: 29,000 × 9% × 57.6% = 1,503 veh/h; the engine sends in exactly what the counts say; the study passes the check and runs. With LPGA's counts the design hour runs the corridor over capacity (LOS F) — a real busy hour |
| 5.4 | ⚠️ Outside Florida, or for a study not made from the map, the lookup says so and what to do instead (type a local count; the AI in phase 6) | Tests on both |
| 5.5 | ⚠️ **UX review** of the counts (cognitive walkthrough + Nielsen, `ux-reviewer`, plus the running window), 2026-10-09: 0 critical, 3 major, 5 minor | Fixed: the ticks and K/D a student chose are kept in the study file and come back on reopening; whether counts are in use is read from the study, so it stays true after an undo; figures changed after the counts are in use say "Not applied yet"; a **Stop using the counts** button; K and D commit on Enter, say what is wrong beside them, and show FDOT's own figure; the Use button counts the ticked ones; a line on how to tell a wrong count; "type them" says where; looking up counts asks for a Save; errors say which button to press and do not blame the internet for a bad answer; the credit year is the newest count used; counted road ends show "veh/h out". Action: Ron, hand-test 11 |

**Hand-test:** `docs/MANUAL-TESTING.md`, test 11.

---

## Phase 6 — the AI

Through `Eaglin.AiManager` and `Eaglin.AiManager.Wpf` (latest on the local feed: 1.4.1), app name
`TrafficLabPlus`. The AI proposes; the student sees each change and accepts or declines it.

| # | Task | Done when |
|---|---|---|
| 6.1 | ⚠️ `Eaglin.AiManager` + `.Wpf` 1.4.1, app name `TrafficLabPlus`: Settings ▸ AI settings… and What the AI has cost…; `TrafficAi.AskAsync` never throws (no key, refused, offline, too slow — each in words); every AI button says it uses AI, what it sends and what it costs | Tried live in the window (Claude, about $0.04 an ask). Action: Ron, hand-test 13 |
| 6.2 | ⚠️ **Build the network** — Network ▸ *Fill in with the AI…*: the student says what they know; the AI proposes changes (cycle, split, protected lefts, kind, names, lanes, speed, turn lanes), each shown as *road (ends) — setting: before → after* with its reason, all ticked; Apply makes the ticked ones as one undo, each marked "suggested by the AI"; anything the study cannot take is listed as left out, with why | Live: asked for LPGA & Williamson, the AI proposed 45 mph and said the cycle and protected lefts were already there. Tests on the reader |
| 6.3 | ⚠️ **Estimate demand** — Traffic ▸ *Estimate traffic with the AI…*: vehicles an hour coming in at each road end with no count, from the road, the place and the student's words; FDOT counts in use are kept; using an estimate switches to typed volumes | Tests: an FDOT-counted end is never changed; nonsense values left out with why. (Land use from OSM is left for later; the AI works from the roads and the student's words) |
| 6.4 | ⚠️ **Explain and coach** — Preview ▸ *Ask the coach about my last test…*: the window reads the page's last traffic test (`window.TL_lastTest()`, read-only, never sent by a published page) and the AI explains what failed and why and what to try, under 250 words; it changes nothing | Says what to do when there is no test yet. Putting the coach's words on the page as the student's notes: not done (D4 says optional) |
| 6.5 | ⚠️ **Write a challenge** — Challenge ▸ *Write the challenge with the AI…*: title, subtitle, introduction, budget, prices of the fixes, demand buttons, from the instructor's request; a fix to rule out is priced above the budget | Tests: field by field, an unknown fix left out |
| 6.6 | ⚠️ Readers that cope with what an AI actually answers: a code fence, a sentence before or after, trailing commas, a bare list, "120 s" for 120, an invented setting or id, a value out of range | 12 AI tests (no AI called); 92 tests in all |
| 6.7 | ⚠️ **UX review** of the AI (cognitive walkthrough + Nielsen, `ux-reviewer`, plus the live window), 2026-10-09: 0 critical, 4 major, 6 minor | Fixed: Settings no longer says the AI comes later; with no key, the window has **Set up an AI now…**; what an AI key is and where to get one is said in plain words; after Apply a message says how many changed and that one Ctrl+Z takes them back; an answer with nothing usable says so and to ask again; no parser jargon or internal ids in what is shown; setting names in the form's words; the coach says it only explains; the traffic window says applying switches to typed volumes; tooltips read the AI status when shown; closing the window stops a request still running. The guides on Network, Traffic and Challenge now describe their AI buttons. Action: Ron, hand-test 13 |

**Hand-test:** `docs/MANUAL-TESTING.md`, test 13.

---

## Phase 7 — publish and hand in

| # | Task | Done when |
|---|---|---|
| 7.1 | ⚠️ `Publisher`, `GitHubApi`, `GitCli`, `GitHubAddress` (Core/Publish) and `TokenStore`, `TokenWindow` (App/Publish) ported from Gamify+: with a token, TrafficLab+ creates the repository, pushes (Git, or the API without Git) and switches Pages on; without one, Git pushes to a repository the student made and the steps to switch Pages on are given | Tests: Git pushes the whole site to a local repository; the API creates the repository and switches Pages on (a fake GitHub) |
| 7.2 | ⚠️ **One repository** (`TrafficLab`), GitHub Pages: **every saved study goes in, except those unticked** (remembered); one folder per study, its address fixed once published; a **summary page as the index** — a card per study (sign letters, title, place, intersections, budget) linking to its page; each publish rebuilds the whole site, and says first which studies will come off it (D15, Ron 2026-10-09) | Tests: each study has its page and the index links to each; the index needs nothing from the internet; a study left out is gone and .git is kept; folder names are safe and unique. Store screenshot 08 is the summary page |
| 7.3 | ⚠️ Marked as made by TrafficLab+: the `trafficlab-plus` topic, the description, `trafficlab-site.json`, the generator meta. A repository that holds something, is not where TrafficLab+ last published and lacks the topic is **never replaced without a yes — and No is the default** | Tests: a repository TrafficLab+ did not make is not replaced without a yes; a marked one is recognised |
| 7.4 | ⚠️ **Copy the link to hand in** — for the summary page and for each study — after a publish, and any day after (the links stay in the Publish step); a **New to GitHub?** guide in four steps; the token window says how to make a token | Action: Ron, hand-test 15 |
| 7.5 | ✅ Real publish with Ron's token | Ron, 2026-10-10: "The app publish worked." |
| 7.6 | ⚠️ The Publish step lists every study the program knows (the studies folder, recent studies, the open one), each with a tick (Publish / leave out, remembered by its file) and, once published, its link with Copy | Seen in the running window: it found Ron's own East New York Avenue study |
| 7.7 | ⚠️ **UX review** of Publish (cognitive walkthrough + Nielsen, `ux-reviewer`), 2026-10-09: 1 critical, 4 major, 4 minor | Fixed: replacing a repository TrafficLab+ did not make asks with **No as the default** and says Yes deletes everything in it; a study's address stays fixed once published, even if its file is renamed; the links can be copied any day, not only right after publishing; no internet is said in words; the steps to switch the website on come before the links, which say to check they are up first; what comes off the website is named by title; typed boxes survive a tick; one set of words ("ticked"); long introductions cut on the summary cards. Not done: a "← All studies" link on each study page (a page is built the same for the site and for a file). Action: Ron, hand-test 15 |
| 7.8 | ✅ **Fixed 2026-10-10 (Ron, hand-test 15):** the program closed after **Check and save** in the token window — two colours it asks for (`OkBrush`, `WarnBrush`) came over from Gamify+ without being defined. The token had been checked and saved before the crash. Added to App.xaml; a test now fails if the app asks for any colour or style App.xaml does not define | Ron, 2026-10-10: the token saved and the publish worked |

**Hand-test:** `docs/MANUAL-TESTING.md`, test 15.

---

## Phase 8 — the Microsoft Store and SoftwarePlus.ai

| # | Task | Done when |
|---|---|---|
| 8.1 | ✅ Name **TrafficLab+** reserved in Partner Center by Ron; identity sent 2026-10-10: `DeanEaglin.TrafficLab`, publisher `CN=E88392BA-A722-4B3A-8372-04403A55AA63`, display name Dean Eaglin, Store ID 9P5NGQF4HRPD — in `packaging/AppxManifest.xml`; the package family name `DeanEaglin.TrafficLab_xs303fgqvwdg8` is checked on every build | Ron, 2026-10-10: Partner Center took the package and it is live |
| 8.2 | ⚠️ Icon: a traffic signal — the head with three lamps — on a backplate whose sides make the **"+"**, white on the guide-sign green, the middle lamp amber (Ron, 2026-10-09: "A streetlight with sides making a plus"). `packaging/make-icon.py` draws it, the .ico (16–256) and two variants (`resources/images/icon-candidates.png`); the program and its window use it | Action: **Ron approves** (or picks B or C from the candidates) |
| 8.3 | ✅ `packaging/` ported from Gamify+ (2026-10-10): `AppxManifest.xml` (with the `.trafficlab` file type), `pack.ps1`, `make-msixupload.ps1`, `run-wack.ps1`, `make-store-assets.ps1` (68 tiles under WACK's cap, listing art, from the approved icon). Version 1.0.0. `artifacts\TrafficLabPlus_1.0.0.0_x64_bundle.msixupload` built; the package was installed here, ran from the Start menu entry (example, page, map tiles) and was removed. Release documents in `docs/release/`: `STORE_LISTING.md`, `STORE_SUBMISSION.md`, `DEPLOY_TO_WIN_APP_STORE.md`, `RELEASE_NOTES_1.0.0.md`. Two stale messages found and fixed on the way: the start screen said publishing comes "in a later version", and FDOT's outside-Florida message said the same of the AI | WACK PASS 23 of 24 (Ron, 2026-10-10); Partner Center took the upload and it is live |
| 8.4 | ✅ Privacy policy **live at https://softwareplus.ai/trafficlab/privacy/** (2026-10-09): nothing comes to us; OpenStreetMap (tiles, Nominatim, Overpass), FDOT on ArcGIS, the AI provider and GitHub, each only when used; the pages collect nothing | Ron, 2026-10-10: "Both are good." It is the privacy URL in Partner Center |
| 8.5 | ✅ `SoftwarePlus\site\trafficlab\`: the summary page (with the icon, "Coming soon") and the privacy policy are live; feature pages and live example pages come with the release | Ron, 2026-10-10: "All good now" — the page is at 1.0 with the Store link, screenshots and two playable examples; "coming soon" gone |
| 8.6 | ✅ Listing, **screenshots**, submit. Nine screenshots at 1920×1080 in `resources/images/screenshots/`, made by `tools/store-shots.ps1`; `01-start.png` retaken 2026-10-10 without this machine's recent studies (it showed a OneDrive path) and with the corrected start text — the script now always does that. Listing copy, upload order and captions in `docs/release/STORE_LISTING.md` | Ron, 2026-10-10: submitted and **live the same day**: https://apps.microsoft.com/detail/9p5ngqf4hrpd. Tagged `v1.0.0` |

---

## Phase 9 — the next version (1.1)

| # | Task | Done when |
|---|---|---|
| 9.1 | ✅ **A footer on everything TrafficLab+ makes** (Ron, 2026-10-10: "all generated artifacts (studies) will have a footer (created with..) and a link to SoftwarePlus.ai"). The study page has a footer across its foot: "Created with **TrafficLab+** · **SoftwarePlus.ai**", then the study's author, OSM and count credits; TrafficLab+ links to softwareplus.ai/trafficlab/, SoftwarePlus.ai to the home page (`web/template.html`, `web/ui/trafficlab-ui.js`; it replaces 2.3's "Made with" line in the side panel). The summary page's footer says the same, with the same links (`Site/SiteBuilder.cs`). The printout reads "Created with TrafficLab+ (softwareplus.ai/trafficlab)", since paper cannot be clicked. In the preview, a link opens in the browser, not a window inside the program (`Views/PreviewView.cs`). Tests: the played page's footer text and links (headless Chrome) and the summary page's footer; "needs nothing from the internet" now means nothing *loaded* (`src`, a `<link>`), since a link to click is not a load. Seen rendered: wide, phone width and dark | Ron, 2026-10-10: "Everything looks good." Ships with 1.1; then rebuild softwareplus.ai/trafficlab/examples/ so they show it |
| 9.2 | ⚠️ **About links to the Store page and all programs** (Ron, 2026-10-30: *"The Help->About on all the apps should have a link to the location of the app on the Windows App Store. They should also have a link to their subdirectory on SoftwarePlus.ai and a link to all programs."*) About keeps "More about TrafficLab+, with help and examples: softwareplus.ai/trafficlab" and adds "TrafficLab+ on the Microsoft Store (updates, ratings and reviews): apps.microsoft.com/detail/9P5NGQF4HRPD" and "All of Dr. Ron Eaglin's programs: softwareplus.ai" (softwareplus.ai/#programs), beside the Created-by card; each opens in the browser (`Views/InfoViews.cs`) | ⚠️ Built 2026-10-30; builds, tests green. Ron to look; ships with the next Store release |
| 9.3 | ⚠️ **Help ▸ Check for a new version…** (Ron, 2026-10-30, for Accessibility+: *"Is it possible on the Help menu to add a 'check for new version?'"*, then *"Add this to all the Windows Store Apps."*) Copied from Accessibility+ (`StoreUpdates.cs`): asks the Microsoft Store (`StoreContext`, tied to the window) whether a newer TrafficLab+ is out; says "You have the latest version of TrafficLab+ (1.0.0)", or asks before letting the Store download and install it, with plain words for every outcome and error. A copy not installed from the Store (the hand-test build) says it cannot check and offers the Store page. The App project now targets `net10.0-windows10.0.19041.0` (was `net10.0-windows`) for the Store API; `tools/store-shots.ps1` and `CLAUDE.md` follow the new bin folder | ⚠️ Built 2026-10-30; builds, 103 tests green, hand-test exe in `manual-test\`. Ron: try it in the Store-installed copy once the next version is out |
| 9.4 | ⚠️ **UX review 2026-10-31 (pre-release), M1: the preview no longer asks "Leave site?"** (`docs/ux-reviews/2026-10-31-pre-release.md`). The page asks before it is closed with a plan on it; the preview rebuilds it after every edit, so the question popped up mid-edit. In the preview only, the page's leave-guard is never registered (`Views/PreviewView.cs`, a script run before the page's own); a published page and Open in your browser keep it. The line above the preview now says "Rebuilt at …, after your change — a plan you were trying on the page starts again." (the first showing: "This is the page as a player sees it.") | ⚠️ Built 2026-10-31; builds, 103 C# + 16 engine tests green (the engine is untouched). **Ron: hand-test 17, steps 1–2** |
| 9.5 | ⚠️ **UX review 2026-10-31, M2: Check for a new version tells the truth about closing.** It said "Your work is kept", but the Store closes TrafficLab+ to install. Now: "Download and install it now? Windows closes TrafficLab+ to install it; open it again from the Start menu to use the new version. If the study has unsaved changes, you are asked to save them first. (No: the Store will install it later if automatic updates are on.)" (the review's wording, aligned with the checklist's 6.5 and its own UX review); Yes then asks to save unsaved changes (Yes / No / Cancel; Cancel stops the update); the menu item is greyed and the line under the study's name reads "Asking the Microsoft Store…", with a wait cursor, while it asks; no raw error text or Store state names in the messages (`StoreUpdates.cs`, `MainWindow`) | ⚠️ Built 2026-10-31. **Ron: hand-test 16 (the hand-test copy) now; the save question can only be seen in the Store-installed copy once 1.1 is live** |
| 9.6 | ⚠️ **UX review 2026-10-31, minors.** Publish's tooltip: "Put your studies on your free GitHub Pages website and get the link to hand in." (it said publishing comes later). Map ▸ Make the study asks about unsaved changes before the name-and-budget window, not after it is filled in. Settings ▸ Author / Course are saved as they are typed. An unexpected error no longer closes the program: "Something went wrong in TrafficLab+. If a study is open, save it under a new name (File ▸ Save as…), then close TrafficLab+ and open it again." (then "Details for support:") About adds "Privacy policy: softwareplus.ai/trafficlab/privacy". GitHub token window, step 5: "Check that GitHub shows your expiry date (if it says the token never expires, cancel and choose a date again)."; its foot: "never put in a study file or on your website." New to GitHub? adds "Players of your page hand in their printout; you hand in the link." A recent study that cannot be opened comes off the start screen's list at once ("That file is gone; it has been taken off the list." when it was moved or deleted). Traffic ▸ Trips that do not happen refuses a pair already listed ("That pair is already listed.") | ⚠️ Built 2026-10-31; builds, tests green. **Ron: hand-test 17, steps 3–9** |
| 9.7 | ⚠️ **Version 1.1.0** (`Directory.Build.props`, `packaging/AppxManifest.xml`; was 1.0.0) | ⚠️ About and `manual-test\TrafficLabPlus.exe` say 1.1.0. Ron: confirm before the package is built |
| 9.8 | ⚠️ **Privacy policy and Check for a new version** — the policy lists every feature that uses the internet (OpenStreetMap, Nominatim, FDOT, the AI provider, GitHub) and says "All of this is described in full below"; Check for a new version asks the Microsoft Store and is not listed. The text is not changed here (`SoftwarePlus\site\trafficlab\privacy\index.html`). **Proposed wording for Ron**, added to the Summary's list: "… and publishing your pages (GitHub), **and checking for a new version (the Microsoft Store, only when you choose Help ▸ Check for a new version)**, each only when you use it." and a short section: "**Checking for a new version.** Help ▸ Check for a new version asks the Microsoft Store, run by Microsoft, whether a newer TrafficLab+ is published, and if you say yes lets the Store download and install it. Nothing about your studies is sent. Microsoft's privacy statement covers the Store: privacy.microsoft.com." | ⚠️ Wording changed 2026-10-31 per Ron (reflect the reality): effective October 31, 2026, version 1.1.0; the Summary and Network use list Check for a new version and help in the browser, new sections "Help, and links that open your browser" and "Checking for a new version", the page footer's links, and publishing without a token through Git for Windows; the Store listing's "goes online only for" line adds the Store check and help. ⚠️ Ron to read the page (`SoftwarePlus\site\trafficlab\privacy\index.html`, deployed with 1.1) |
| 9.9 | ⚠️ **UX review 2026-10-31: the usability pass** (Ron, 2026-10-31: localized help, tooltips, descriptive labels, the same in every program — `..\USABILITY-CHECKLIST.md`). **Help menu** in the shared order and words: *Help for this screen* (F1) · *Help on the web (softwareplus.ai/trafficlab)* · *Check for a new version…* · *About TrafficLab+…*; F1 opens the help page about the step shown (`Guides.HelpPage`: Start → studies, Map, Network, Traffic, Challenge, Preview → challenge#preview, Publish, Settings → about#settings, About); "Start screen: what TrafficLab+ is for" left the menu (Start stays at the top of the steps). **Local help links:** under each step's guide, "More help for this step, with examples (F1): softwareplus.ai/trafficlab/help/….html". **Tooltips** on every menu item, section, step button, and the buttons, ticks and boxes of Map, Network, Traffic, Challenge, Publish, Settings, the New study window, the AI window and the token window; a field's help is also its tooltip (`Form`). **Greyed-out controls say why**: the steps with no study open, Next, Save, Save as, Undo, Redo, Close the study, Make the study…, Publish (now greyed when nothing is ticked), Ask the AI, Apply, Check and save, Remove the saved token. **Errors:** plain words first, the system's text only after "Details for support:" (opening, saving, making a study from the map, the preview folder, the browser, the map and preview viewers, a folder, the page file, the Store, GitHub and the AI offline); message titles "TrafficLab+" or "TrafficLab+ — subject". Check for a new version shows "Asking the Microsoft Store…" under the study's name with a wait cursor. Test: every help page F1 and the guides name exists in the SoftwarePlus help (`HelpLinkTests`) | ⚠️ Built 2026-10-31; builds, 105 C# + 16 engine tests green. UX: walkthrough + heuristics on the changes (`ux-reviewer`), 0 critical, 0 major, 9 minor — all 9 fixed (install question no longer says "save first" before the program asks; installed message; visible busy state; double full stop; disabled-step wording; crash message says Save as and restart; make-study error; Check and save greyed reason; Store errors' details). Not seen rendered (no driving the desktop): Ron to look at the step bar's tooltips and the 12 pt help link under each guide. **Ron: hand-test round 2026-11 (top of `MANUAL-TESTING.md`), B11–B14, C, D, E** |
| 9.10 | ⚠️ **Help pages on SoftwarePlus.ai follow the new words** (`SoftwarePlus\site\trafficlab\help\`, being written in parallel; not committed or deployed from here): `about.html` (the Help menu, Help for this screen, tooltips, the privacy link in About, Check for a new version's save question and busy line), `studies.html` (Start, "That file is gone…"), `publish.html` (token step 5, "never put in a study file or on your website"), `messages.html` ("That file is gone…", "Something went wrong…", what "Details for support:" means) | ⚠️ Ron (or the help session): commit and deploy them with `deploy.ps1` when 1.1 ships |
| 9.11 | ⚠️ **UX review 2026-10-31: what the help screen captures showed.** (1) Publish: "On the website now" puts the address on its own full-width line under the label and the Copy the link / Open buttons, wrapping, with the whole address as its tooltip (it was cut off at 1440 px). (2) Network: the settings under the drawing get the larger share (1 : 1.4) and at least 260 px, the drawing at least 170 px — at 1500 × 1100 the settings now show about 300 px (were ~200). (3) Settings' AI line: "To see what the AI has cost so far, choose What the AI has cost… in Settings." (4) Check for a new version, not from the Store: "Open TrafficLab+'s page on the Microsoft Store…" — and the line was missing its `$`, so it read "{ProgramName}'s"; fixed. (5) Summary page: a card's facts line starts with a capital ("One intersection · $2M budget"). (6) The page: a ▲▼ inflow control that would sit under the legend or the zoom buttons moves up just above it (`trafficlab-ui.js`, UI only; the engine is untouched) — checked in headless Chrome at 1280 and 390 px. (7) The map-made demo study: the view already fits every node with a margin; its roads look thin because it is drawn to true scale (1.28 × 0.85 km, against the LPGA example's hand-made 600 × 504 m frame) over a sparse OpenStreetMap backdrop (18 local streets, no land use or place names) — no change made. `tools/tour-window.ps1` gains a `size:<w>x<h>` step (UI Automation, no keys). Help captures retaken: network-editor, settings, check-version, published, player (SoftwarePlus, not committed or deployed from here) | ⚠️ Built 2026-10-31; builds, 105 C# + 16 engine tests green; each fix seen in a fresh capture (PrintWindow, headless Chrome); not re-run through `ux-reviewer` (the fixes are the review's own findings). `manual-testTrafficLabPlus.exe` rebuilt. **Ron: round 2026-11, B15** |

**Hand-test:** `docs/MANUAL-TESTING.md`, round 2026-11 — usability (at the top), A–E; B1–B15 are items 9.4–9.11 (9.10: the help pages).

---

## Every release — AI Manager and the manual-test build (Ron, 2026-10-11)

Ron: *"the new releases will need to update to the latest version and this will be something that
must occur with every version that we release of all the programs that use the AI Manager."*
These two items are done **again for every release**; a release is not ready for the Store until both are.

| # | Task | Done when |
|---|---|---|
| R.1 | **Move to the latest AI Manager** (`Eaglin.AiManager` and its UI package in the csproj). **For 1.1: moved 1.4.1 → 1.7.0 (2026-10-31)**, the latest in `C:
uget-local`. Read AI Manager's `docs/DEVELOPMENT-PLAN.md`: 1.5.0 speech to text, 1.6.0 How? beside Get a key and the dated key help page, 1.7.0 pictures — all additions, nothing TrafficLab+ calls changed; no code change needed | ⚠️ the csproj names 1.7.0; builds, 103 tests green. Ron: R.2 |
| R.2 | **Hand-test build** in `manual-test\TrafficLabPlus.exe` (`docs/MANUAL-TESTING.md`); Ron opens AI Settings there and checks How? beside his provider | ⚠️ Rebuilt 2026-10-31 (1.1.0, AI Manager 1.7.0). Ron runs the exe |

## Open questions for Ron — all 7 answered (2026-10-09)

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

7. ~~Which hour do FDOT counts give the page?~~ **(a) keep AADT × K × D, answered by Ron 2026-10-09: "keep
   it, that is how it is today."** Task 5.3. The question as asked (2026-10-09, phase 5): TrafficLab+ uses **AADT × K × D**
   coming in at every counted road end — the busier direction of the design hour, as if every road's
   busier direction were toward the intersection. With LPGA's counts that runs the corridor over capacity
   (LOS F) at "Today". Options: (a) keep it — a deliberately busy hour; (b) **AADT × K ÷ 2** each way (the
   design hour, both directions averaged) — gentler, and the scenario buttons make it busier; (c) let the
   author choose per study. Proposed: (c), with (b) as the starting choice.

---

## References

| Document | What it is for |
|---|---|
| `PLAN.md` | The strategy, settled decisions, architecture, what is left out |
| `../reference/lpga-traffic-lab.html` | Ron's original LPGA Traffic Lab — the engine's source and the yardstick for task 1.7 |
| `STUDY-FORMAT.md` | The study JSON, field by field |
| `MANUAL-TESTING.md` | Ron's hand-tests, phase by phase |
| `OSM-LPGA-COMPARISON.md` | LPGA built from OpenStreetMap against Ron's hand-made LPGA: what matches, what differs and why (task 4.4) |
| `release/DEPLOY_TO_WIN_APP_STORE.md` | The Store procedure step by step, the identity, where every file lives (phase 8) |
| `release/STORE_LISTING.md` | The listing copy, images and screenshot order to paste into Partner Center |
| `release/STORE_SUBMISSION.md` | Age rating, declarations, `runFullTrust`, certification notes, WACK results, releases |
| `release/RELEASE_NOTES_1.0.0.md` | What 1.0.0 contains |
| `ux-reviews/2026-10-31-pre-release.md` | The pre-release UX review for 1.1 (items 9.4–9.6, 9.8) |
| `../CLAUDE.md` | How to work in this repo |
