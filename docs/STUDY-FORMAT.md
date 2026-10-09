# The study format (format 1)

A **study** is everything one TrafficLab+ page needs. It is JSON. The program writes it; the page reads
it; the engine checks it (`validate` in `web/core/trafficlab-core.js`) and says what is wrong in plain
words. `samples/lpga.json` is a complete example — Ron's LPGA Traffic Lab, generated from the original
page by `tools/make-lpga-study.js`.

**Units:** distances and positions in **metres**, speeds in **metres per second**, money in **millions
of dollars**, volumes in **vehicles per hour**. The page shows US customary units (feet, miles, mph).
Positions are a flat local frame: **x grows east, y grows south** (screen order), origin anywhere.

Inside, the engine keeps LPGA's drawing unit, 1 u = 0.6 m, so every constant it was tuned with is
unchanged; `toU()` converts once, at load, and snaps to 10⁻⁹ u so LPGA's values come back exactly.

## Top level

| Field | Required | What it is |
|---|---|---|
| `format` | yes | `1`. A study with a higher number is refused with "made by a newer TrafficLab+" |
| `title` | yes | The page's title and the printout's heading |
| `short` | | Up to ~5 letters for the green shield in the header (LPGA) |
| `subtitle` | | One line under the title; default "Fix this network on a $X budget" |
| `intro` | | A sentence or two at the top of the intro dialog |
| `place`, `author`, `course` | | Shown on the printout ("Study by …"); `author` also in the page's credit line |
| `world` | | `{x, y, w, h}` in metres: the frame the map fits to. Default: every node with a margin |
| `budget` | yes | The plan budget, $M |
| `costs` | | Prices of the fixes, $M (below). Missing ones take LPGA's |
| `demand` | yes | How much traffic, and where it goes (below) |
| `nodes` | yes | Road ends, signals and roundabouts (below) |
| `links` | yes | Road segments between nodes (below) |
| `pockets` | | Turn lanes that exist today (below) |
| `backdrop` | | Decoration only — never simulated (below) |
| `sources` | | Where things came from. `osm: true` puts "© OpenStreetMap contributors" on the page and printout |
| `from` | | For the editor: where each value came from — a key like `node:I1/cycle` or `link:L1/lanes`, and `example`, `default`, `typed`, `osm`, `fdot` or `ai`; `*` covers every value not listed. Left out of the built page |

**No `plan`.** A published page always opens blank, as a puzzle (Ron, 2026-10-09).

## nodes

| Field | What it is |
|---|---|
| `id` | Unique, short (`I1`, `W`) |
| `type` | `end` (traffic enters and leaves here), `signal`, or `roundabout` (one that exists today) |
| `name` | What a person reads ("LPGA & Market Center") |
| `short` | A shorter name for tables ("Market Center"); default `name` |
| `x`, `y` | Position, metres |
| `weight` | `end` only. How busy this end is, relative to the others (gravity model; destination share for typed volumes) |
| `volume` | `end` only, typed-volumes mode. Vehicles per hour **entering** here |
| `signal` | `signal` only: `{ cycle, split, mainLinks, protMain, protSide }` |

`signal.cycle` is seconds (40–180). `split` is the main street's share of the green (0.2–0.8).
`mainLinks` names the links that form the main street (phase group A); left out, the two most opposed
roads, widest first, are taken. `protMain` / `protSide` are protected left-turn phases that exist today.

**Rules:** an `end` has exactly one road; a `signal` or `roundabout` has 3 or 4. A fifth road is refused:
"TrafficLab+ handles intersections with 3 or 4 roads; leave a road out of the study, or split the
intersection in two." At least two ends.

## links

| Field | What it is |
|---|---|
| `id`, `a`, `b` | Unique id, and the two nodes it joins (direction `a→b` is "ab") |
| `name` | Street name |
| `lanes` | Through lanes **in each direction**, 1–3 |
| `speed` | Metres per second (13.4 ≈ 30 mph, 17.9 ≈ 40 mph, 19.8 ≈ 44 mph) |
| `label` | `false` leaves the segment unlabelled on the map |
| `labelAt` | Where along the segment the label sits, 0–1 (default 0.5) |

## pockets

Turn lanes that exist today, free and not removable: `{ "L2:ab": { "L": 1 }, "W1:ab": { "L": 1, "R": 1 } }`
— the key is `link:direction`, the approach arriving at the far node.

## demand

| Field | What it is |
|---|---|
| `mode` | `gravity` (default) or `volumes` |
| `baseTotal` | `gravity`: vehicles per hour entering the whole study at 100% demand. Trips between ends ∝ weight × weight |
| `od` | Optional typed origin–destination table `[{o, d, v}]` (veh/h). When present it is used as it stands |
| `noTrips` | Pairs of ends that exchange no trips (`[["I95N","I95S"]]`: freeway-to-freeway stays on I-95) |
| `scenarios` | Buttons on the toolbar: `[{ name, mult }]`, e.g. Today 1.0, 2035 1.25, Race Week 1.5 |

In `volumes` mode each end sends out exactly its `volume`, shared among the other ends by their
`weight` (or, without one, their own volume).

## costs ($M)

`addLaneBase` + `addLanePerKm` × length (adding a through lane each way), `oneWay`, `pocketL`, `pocketR`,
`retime` (cycle or split), `protL` (a protected left phase), `roundabout` (converting a signal).
LPGA's: 0.6 + 6.67/km, 0.15, 0.45, 0.35, 0.02, 0.08, 2.8.

## backdrop

Drawn under the roads, never simulated: `streets` (polylines), `blocks` (polygons of land use), `places`
(`{x, y, text}` labels), `freeways` (`{points, width}` mainlines passing through, like I-95).

## The `.trafficlab` file

What the program saves: a zip holding `study.json` (the study above), `notes.md` (the author's notes,
never on the page) and, from phase 4, `osm.json` (the roads as OpenStreetMap gave them, so an opened
study never asks OpenStreetMap again). Entries the program does not know are kept and written back.
A study with a newer `format` is refused before anything is read. A plain `.json` study opens too, and
is saved as a `.trafficlab` file.

Every field the program does not know — at any level — is kept as it came and written back.
