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
| 7.5 | Real publish with Ron's token | **Action: Ron** — save a GitHub token in Publish ▸ GitHub token…, publish, and open the link on his phone (hand-test 15) |
| 7.6 | ⚠️ The Publish step lists every study the program knows (the studies folder, recent studies, the open one), each with a tick (Publish / leave out, remembered by its file) and, once published, its link with Copy | Seen in the running window: it found Ron's own East New York Avenue study |
| 7.7 | ⚠️ **UX review** of Publish (cognitive walkthrough + Nielsen, `ux-reviewer`), 2026-10-09: 1 critical, 4 major, 4 minor | Fixed: replacing a repository TrafficLab+ did not make asks with **No as the default** and says Yes deletes everything in it; a study's address stays fixed once published, even if its file is renamed; the links can be copied any day, not only right after publishing; no internet is said in words; the steps to switch the website on come before the links, which say to check they are up first; what comes off the website is named by title; typed boxes survive a tick; one set of words ("ticked"); long introductions cut on the summary cards. Not done: a "← All studies" link on each study page (a page is built the same for the site and for a file). Action: Ron, hand-test 15 |

**Hand-test:** `docs/MANUAL-TESTING.md`, test 15.

---

## Phase 8 — the Microsoft Store and SoftwarePlus.ai

| # | Task | Done when |
|---|---|---|
| 8.1 | ⚠️ Name **TrafficLab+** reserved in Partner Center — done by Ron 2026-10-09. **Action when packaging starts: Ron reads Package/Identity Name, Publisher and Publisher display name from Product identity** | Identity in the manifest |
| 8.2 | ⚠️ Icon: a traffic signal — the head with three lamps — on a backplate whose sides make the **"+"**, white on the guide-sign green, the middle lamp amber (Ron, 2026-10-09: "A streetlight with sides making a plus"). `packaging/make-icon.py` draws it, the .ico (16–256) and two variants (`resources/images/icon-candidates.png`); the program and its window use it | Action: **Ron approves** (or picks B or C from the candidates) |
| 8.3 | `packaging/` ported from GamifyPlus; WACK | WACK passes |
| 8.4 | ⚠️ Privacy policy **live at https://softwareplus.ai/trafficlab/privacy/** (2026-10-09): nothing comes to us; OpenStreetMap (tiles, Nominatim, Overpass), FDOT on ArcGIS, the AI provider and GitHub, each only when used; the pages collect nothing | Action: Ron reads it; it goes in Partner Center as the privacy URL |
| 8.5 | ⚠️ `SoftwarePlus\site\trafficlab\`: the summary page (with the icon, "Coming soon") and the privacy policy are live; feature pages and live example pages come with the release | Started 2026-10-09 |
| 8.6 | ⚠️ Listing, **screenshots**, submit. Nine screenshots at 1920×1080 in `resources/images/screenshots/` (start; the map from OpenStreetMap; the network and the live page; FDOT counts; the challenge; the preview; the AI proposing; the published summary page; a player building a plan), made by `tools/store-shots.ps1` from a demo study built from the saved LPGA answers (`--demo-study`) and the website (`--build-site`) | Action: Ron picks the ones for the listing; the listing text and submission are his, 2026-10-10 |

---

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
| `../CLAUDE.md` | How to work in this repo |
