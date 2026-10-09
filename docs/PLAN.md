# TrafficLab+ — plan

The strategy: what TrafficLab+ is, who it is for, the decisions settled with Ron and why, and the
architecture. The numbered work is in `DEVELOPMENT-PLAN.md`.

Started **2026-10-09**.

---

## 1. What it is

A Windows 10/11 program that turns **a real intersection, or a short corridor of them**, into a
**web page with a live traffic simulation** — the kind of page Ron built by hand as the
*LPGA Traffic Lab* (`reference/lpga-traffic-lab.html`, from
https://claude.ai/artifact/UgD49FCedy4smDdEABRfud).

A student:

1. **finds the intersection on a map** and picks the signals or roundabouts that make up the study;
2. lets the program **read the roads from OpenStreetMap** — lanes, turn lanes, one-ways, signals —
   and fills in what the map does not know (signal timing, traffic volumes) by typing, by an
   **FDOT AADT lookup**, or by asking the **AI**;
3. **watches it run** in the program, exactly as the page will run, and adjusts it;
4. **publishes it to GitHub Pages** and **hands in the link**.

The published page is the **full lab**, like LPGA: the simulation runs live, roads are shaded by
Level of Service, a visitor raises demand, clicks a road or intersection, builds a plan of fixes
under a budget, and runs a scored traffic test.

**The point of the software is the web page.** The program is the authoring tool; the page is what
a student hands in and what anyone opens on a phone.

## 2. Who it is for

- **Students** in a transportation or civil engineering class (Ron's courses first), who build and
  publish a study and hand in the link. Assume no GitHub knowledge, no traffic-software
  knowledge, and say everything (the portfolio's UX rule).
- **Teachers**, who can write a challenge (budget, costs, demand scenarios, goals — with the AI's
  help) and hand it out as a study file for students to start from.
- **Anyone who opens a published link**: it must make sense with no program installed.

**Classroom game and lab** first (Ron, 2026-10-09): budget, plan, score, the student's name on
the page. Not a replacement for Synchro/VISSIM; it is a teaching instrument that is honest about
its model.

## 3. Decisions settled with Ron (2026-10-09)

| # | Decision | Why |
|---|---|---|
| D1 | **Roads come from OpenStreetMap**, found on a map in the program | Google's terms forbid deriving road geometry from Google Maps, and its API needs a billed key. OSM data is open (ODbL), free, and carries `lanes`, `turn:lanes`, `oneway`, `maxspeed` and `highway=traffic_signals`. Published pages carry "© OpenStreetMap contributors" |
| D2 | **Classroom game + lab** | As LPGA: budget, plan, score, name on the report, plus free play |
| D3 | **AI does four jobs**, through `Eaglin.AiManager`: build the network (fill what OSM lacks), estimate demand, explain and coach, write challenges | Ron chose all four |
| D4 | **Free** on the Microsoft Store, named **TrafficLab+** (repo `TrafficLabPlus`) | Like Gamify+, Statistle, SMADA; supports the courses and the paid products. The "+" naming and the "+" in the icon are the suite's theme |
| D5 | **A study covers a corridor of up to about 10 nodes** | LPGA is five signals; a single intersection is the smallest corridor. Spillback between signals is part of the lesson |
| D6 | **The output is a web page; students hand in the link** | Ron: "The point of the software is to create Web versions of the intersections. The students will hand in their simulated intersection as a link." |
| D7 | **Publish to GitHub Pages** with the student's own account and token | The route Gamify+ and LMS-2-Website already use; the link is stable and free |
| D8 | **The published page is the full lab** | Visitors run it, inspect, build plans, run scored tests — as LPGA does |
| D9 | **Volumes are typed in, or looked up from FDOT AADT**, besides the AI's estimate | A count sheet from class, or published counts converted to the peak hour with K and D factors. Florida first |

### Consequences of D6–D8 (decided in this plan, not asked)

- **The simulation engine is JavaScript**, because it has to run in the published page. It is the
  LPGA core (IDM car-following, fixed-time signals with protected lefts, roundabouts, gravity OD,
  Dijkstra routing, v/c and LOS, seeded multi-run tests and scoring), made **data-driven**: the
  hard-coded `NODES`, `LINKS`, `ZONES`, `EXISTING_POCKETS`, `COSTS` and `BUDGET` become a
  **study** object read from JSON.
- **The program previews the real page.** The window hosts the built page in WebView2, so what the
  student watches is byte for byte what will be published ("the preview cannot lie", LMS-2-Website).
- **C# does everything around the page**: the map and OSM import, the study file and its editor,
  the volumes, the AI, building the page and publishing it.
- **One page is one self-contained HTML file**: engine, interface, study data and font inlined. No
  CDN, no server; it opens from disk and from a GitHub Pages project path (Gamify+'s rule).
- **The same JS is tested twice**: under Node in the test suite (as Gamify+'s
  `CompletionCodeTests` does), and in the page in headless Chrome.

## 4. The study

A **study** is everything one page needs. Saved as `<name>.trafficlab` (a zip: `study.json`, the
OSM extract it came from, notes) under `Documents\TrafficLabPlus\Studies`.

| Part | What it holds | Where it comes from |
|---|---|---|
| Title, author, course | shown on the page and in the report | the student |
| **Nodes** | ends (zones at the edge), signals, roundabouts; position in metres from a local origin | OSM, edited |
| **Links** | from/to, name, through lanes each way, speed, one-way | OSM (`lanes`, `lanes:forward/backward`, `oneway`, `maxspeed`, `name`), edited |
| **Existing turn pockets** | per approach, left/right, storage length | OSM `turn:lanes`, edited, or the AI |
| **Signals** | cycle, split, main-street group, protected lefts, yellow, all-red | typed, or the AI (OSM does not carry timing) |
| **Zones / demand** | entering volume per edge zone, trip pattern, scenarios (Today, 2035, Event) | typed, FDOT AADT × K × D, or the AI |
| **Challenge** | budget, cost of each fix, which fixes are allowed, scoring weights, goals text | defaults from LPGA; the teacher; the AI |
| **Plan** | the student's own fixes, shown on the page as their answer | the student, in the preview |
| Background | an optional faint outline of nearby streets for context | OSM |

Positions are stored in **metres**; the page scales them. (LPGA used 1 px = 0.6 m with speeds in
px/s; the generalised engine works in metres and m/s and draws at any zoom.)

## 5. Architecture

```
TrafficLabPlus.slnx
├── web/                        THE PAGE — plain JS/CSS/HTML, embedded into Core as resources
│   ├── core/trafficlab-core.js   the engine (from LPGA's core): build, route, signals, IDM, step,
│   │                             analyse, evaluate, score — no drawing, runs under Node
│   ├── ui/trafficlab-ui.js       the canvas, inspector, plan, test and report (from LPGA's UI)
│   ├── ui/trafficlab.css
│   └── template.html             one placeholder for the study JSON, one for each inlined asset
├── src/TrafficLabPlus.Core/    net10.0, no UI
│   ├── Study/       Study, Node, Link, Pocket, SignalPlan, Zone, Scenario, Challenge (+ JSON)
│   ├── Osm/         OverpassClient, OsmExtract, IntersectionFinder, NetworkFromOsm,
│   │                LocalProjection (lat/lon → metres)
│   ├── Volumes/     TypedVolumes, FdotAadtClient, PeakHour (AADT × K × D)
│   ├── Build/       PageBuilder (study + web assets → one HTML file)
│   ├── Ai/          prompts and readers for the four AI jobs; never calls a provider itself
│   ├── Publish/     Publisher, GitHubApi, GitCli — ported from GamifyPlus
│   └── Document/    StudyDocument (.trafficlab), paths, recent studies, samples (LPGA)
├── src/TrafficLabPlus.App/     net10.0-windows, WPF
│   ├── MainWindow   Map ▸ Network ▸ Traffic ▸ Challenge ▸ Preview ▸ Publish, plus Settings, About
│   ├── Views/MapView    WebView2 + Leaflet (bundled, not CDN) for finding and picking intersections
│   ├── Views/PreviewView WebView2 hosting the built page
│   └── Ai/          over Eaglin.AiManager + Eaglin.AiManager.Wpf (settings, usage)
├── tests/TrafficLabPlus.Tests/  xUnit; runs the JS engine under Node for the engine tests
├── samples/lpga.trafficlab     LPGA rebuilt as data — the first test of the generalisation
└── reference/lpga-traffic-lab.html  Ron's original page, kept as the yardstick
```

### OpenStreetMap, used politely

- **Finding**: Nominatim search (the map's search box) and clicking on the map.
- **Reading**: one Overpass API query for the box around the chosen nodes: the `highway=*` ways and
  their nodes, `highway=traffic_signals`, `junction=roundabout`.
- **Usage policies apply**: a real User-Agent naming the program, no bulk downloads, results cached
  in the study so a study never re-queries. The map tiles in the program follow the OSM tile policy
  (attribution, identified client, light use) — **watch this**: if Store use grows, switch the
  program's tiles to a provider with a free tier for apps. Published pages draw their own
  schematic and **load no tiles**.
- **What OSM is missing** is filled in and shown as such: an approach whose lanes came from a
  default rather than a tag says so in the editor, and the AI can be asked to propose them.

### The AI, through AI Manager

Every call goes through `Eaglin.AiManager` tagged `"TrafficLabPlus"`, using the app's own model
setting. The AI **proposes**; the student **sees what changes and accepts it**. Nothing it says
lands in a study unread.

| Job | In | Out |
|---|---|---|
| Build the network | OSM extract + the student's words ("120 s cycle, protected lefts on LPGA") | missing lanes, pockets, signal plans, each with a reason |
| Estimate demand | the network, land use around it (from OSM: a Buc-ee's, an I-95 ramp), any AADT | zone volumes and the trip pattern, with reasoning; editable |
| Explain and coach | a test result, the study | why an approach is LOS F, which fixes might help, a critique of the plan — **in the program** |
| Write a challenge | a teacher's request ("race week, $3M, fix the ramps") | budget, costs, allowed fixes, scenarios, goals text |

The published page has **no AI** (it carries no keys). What the coach wrote can be put on the page
as the student's notes, if they choose.

### Publishing

One repository per student, `TrafficLab` by default, with **one folder per study**:
`https://<user>.github.io/TrafficLab/<study-slug>/` — the link they hand in. A study's folder is
the only thing a publish replaces, so publishing one study never removes another. The repository
is marked as made by TrafficLab+ (topic `trafficlab-plus`, description, a `trafficlab-site.json`
at the root, a generator meta on every page) — the LMS-2-Website rule, so a publish can never land
on a hand-made repository. An index page lists the student's studies.

## 6. What is deliberately absent (for 1.0)

- Pedestrians, bicycles, transit, actuated or coordinated signal timing (fixed-time only, as LPGA).
- Curved roads (links are straight between nodes; OSM ways are simplified to their end nodes).
- Google Maps, in any form.
- AI on the published page.
- Anything that claims HCM-grade results. The page says it is a teaching model, as LPGA does
  ("planning-level v/c").

## 7. Portfolio rules this program follows

- `docs/DEVELOPMENT-PLAN.md` with the three marks; Claude never marks ✅.
- Development cycle: operational + tests → UX review (cognitive walkthrough + Nielsen) → Ron's
  hand-test → back to code.
- **About**: "Created by Dr. Ron Eaglin" card, and a link to **softwareplus.ai/trafficlab/**.
- **Store release** updates SoftwarePlus.ai in the same release; privacy policy at
  `softwareplus.ai/trafficlab/privacy/`.
- Icon: one white mark on the program's colour, with a "+" in it (guide-sign green, as LPGA).
