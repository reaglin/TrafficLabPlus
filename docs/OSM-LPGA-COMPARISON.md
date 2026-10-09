# LPGA from OpenStreetMap, against Ron's hand-made LPGA

Plan task 4.4: "LPGA from OSM comes out close to Ron's hand-made LPGA; the differences listed."
Built 2026-10-09 from the saved Overpass answer `tests/fixtures/osm-lpga.json` (fetched that day),
choosing the five junctions that are Ron's five signals. A test (`OsmTests`) builds it, checks it,
and runs it in the engine.

```
TrafficLabPlus.exe --osm-study tests/fixtures/osm-lpga.json lpga-osm.json j101035693 j3269748465 j4750842748 j3269758871 j3269758870
```

## The same

| | Ron's LPGA | From OSM |
|---|---|---|
| Intersections | 5 signals | 5 (signals or starting as signals) |
| Road ends | 8 | 8 |
| Road segments | 13 | 13 |
| Shape | LPGA Blvd as the spine, Williamson crossing it, a loop south of LPGA | the same |
| Runs in the engine | yes | yes, every trip routed |

## Different, and why

| | Ron's LPGA | From OSM | Why |
|---|---|---|---|
| Street names | LPGA Blvd, N Williamson Blvd, Market Center Dr, Shoppes Way, Gateway N Dr | LPGA Boulevard, Williamson Boulevard, Outlet Boulevard, Cornerstone Boulevard | OSM has today's names; Market Center / Shoppes Way are now Outlet / Cornerstone |
| Size | 592 × 499 m | 1,252 × 844 m | Ron drew the corridor compressed to fit the screen; OSM is to scale |
| LPGA lanes | 2 each way | 2–3 each way | OSM counts the real through lanes from `turn:lanes` |
| Speeds | 44 mph arterials, 30 mph locals | 40 mph arterials (no tag: the default), 15 mph on Outlet and Cornerstone (tagged) | OSM's tags; 15 mph is what OSM says for those mall roads |
| I-95 ramps | one intersection with north and south ramp ends | the east ramp terminal; the west terminal is a separate junction a player may add | OSM maps each ramp terminal where it is |
| Turn lanes today | 8 left-turn lanes | 6 approaches, with left and right turn lanes | read from `turn:lanes`; OSM has right-turn lanes Ron left out |
| Signals | all five | three tagged in OSM; two (the Cornerstone pair) start as signals and say so | OSM does not show signals there; the student checks the real intersection |
| Roads left out | — | Technology Boulevard at Outlet (a fifth road) | TrafficLab+ handles 3 or 4 roads at an intersection; the notes say so |
| Signal timing | tuned by Ron (cycles 60–120 s, splits, protected lefts) | 90 s and 60% everywhere, marked as starting values | OSM has no timing |
| Traffic | gravity weights tuned by Ron, 3,800 veh/h | weights by road class, 3,600 veh/h, marked as starting values | OSM has no counts (FDOT counts: phase 5) |
| Backdrop | hand-drawn blocks, places, I-95 | the streets around and I-95 from OSM | to scale, no land-use blocks or place labels |

## What this means

A study from the map gets the **geometry, the lanes, the turn lanes, the names and some speeds**
right without typing, and says what it could not know. The **signal timing and the traffic** are
what the student (or phase 5's counts, or phase 6's AI) must still supply — the same split as Ron's
plan made in the first place.
