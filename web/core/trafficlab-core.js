/* =====================================================================
   TRAFFICLAB+ — SIMULATION CORE
   ---------------------------------------------------------------------
   Generalised from Dr. Ron Eaglin's LPGA Traffic Lab. This file has NO
   drawing code. It holds:
     0. STUDY     – reads a study (JSON, metres) into the engine's tables
     1. DATA      – the road network (nodes, links, zones) as plain objects
     2. BUILD     – turns DATA + the player's plan into lanes, paths, signals
     3. ROUTING   – shortest paths for every origin/destination pair
     4. SIGNALS   – fixed-time signal controller (phases, yellow, all-red)
     5. VEHICLES  – Intelligent Driver Model (IDM) car-following
     6. SIM STEP  – spawning, moving, turning, gap acceptance, roundabouts
     7. ANALYSIS  – planning-level volume/capacity (v/c) and Level of Service
   Keeping the model separate from the picture is the #1 lesson for
   building an educational game: you can test the engineering without
   ever opening a browser.
   A study is written in metres and metres per second. Inside, the engine
   keeps LPGA's drawing unit, 1 u = 0.6 m (33 u/s ≈ 20 m/s ≈ 45 mph), so
   every constant below is exactly the one LPGA was tuned with.
   ===================================================================== */
(function (root) {
'use strict';

// ---------- tunable constants (students: try changing these!) ----------
const C = {
  M_PER_PX: 0.6,
  CAR_LEN: 8,        // u   (~4.8 m)
  S0: 3.5,           // u   minimum jam gap
  T_HEAD: 1.2,       // s   desired time headway
  A_MAX: 2.6,        // u/s² comfortable acceleration
  B_COMF: 3.4,       // u/s² comfortable braking
  B_MAX: 7.5,        // u/s² hardest braking a driver will use for a light
  LANE_W: 6,         // u   drawn lane width
  MED: 5,            // u   half-median (room for a left-turn pocket)
  POCKET_LEN: 84,    // u   turn-lane storage (~7 cars)
  TURN_SPEED: 13,    // u/s (~17 mph)
  RING_SPEED: 12,    // u/s
  RAB_R: 17,         // u   roundabout circulating radius
  YELLOW: 3, ALLRED: 1,
  SAT: 1800,         // veh/hr/lane saturation flow
  DT: 0.1            // s simulation step
};

// =====================================================================
// 0. STUDY — validate and load
// =====================================================================
const FORMAT = 1;
const MAX_LEGS = 4;
// $ millions. Adding a lane costs a base plus a rate per km of road (LPGA: 0.6 + 0.004 per 0.6 m).
const DEFAULT_COSTS = { addLaneBase: 0.6, addLanePerKm: 0.004 / 0.6 * 1000, oneWay: 0.15, pocketL: 0.45, pocketR: 0.35,
  retime: 0.02, protL: 0.08, roundabout: 2.8 };

// metres → engine units, snapped so a value written as u × 0.6 comes back exactly
const toU = (m) => Math.round(m / C.M_PER_PX * 1e9) / 1e9;

// Every problem with a study, in words a student can act on. [] means it loads.
function validate(study) {
  const errs = [];
  const say = (path, message) => errs.push({ path, message });
  if (!study || typeof study !== 'object') { say('', 'The study is empty or is not a study file.'); return errs; }
  if (study.format != null && study.format > FORMAT) say('format', `This study was made by a newer TrafficLab+ (format ${study.format}). Update TrafficLab+ to open it.`);
  const nodes = Array.isArray(study.nodes) ? study.nodes : [], links = Array.isArray(study.links) ? study.links : [];
  if (!nodes.length) say('nodes', 'The study has no intersections or road ends.');
  const ids = new Set();
  nodes.forEach((n, i) => {
    const p = `nodes[${i}]`, nm = n.name || n.id;
    if (!n.id || typeof n.id !== 'string') say(p, 'A node has no id.');
    else if (ids.has(n.id)) say(p, `Two nodes are both called "${n.id}". Each needs its own id.`); else ids.add(n.id);
    if (!['end', 'signal', 'roundabout'].includes(n.type)) say(p, `"${nm}" must be a road end, a signal or a roundabout.`);
    if (!Number.isFinite(n.x) || !Number.isFinite(n.y)) say(p, `"${nm}" has no position.`);
    if (n.type === 'signal') {
      const s = n.signal || {};
      if (!(s.cycle >= 40 && s.cycle <= 180)) say(p, `The signal at "${nm}" needs a cycle between 40 and 180 seconds.`);
      if (!(s.split >= 0.2 && s.split <= 0.8)) say(p, `The main street's share of green at "${nm}" must be between 20% and 80%.`);
    }
  });
  const linkIds = new Set();
  links.forEach((L, i) => {
    const p = `links[${i}]`, nm = L.name || L.id;
    if (!L.id) say(p, 'A road segment has no id.'); else if (linkIds.has(L.id)) say(p, `Two road segments are both called "${L.id}".`); else linkIds.add(L.id);
    if (!ids.has(L.a) || !ids.has(L.b)) say(p, `Road segment "${nm}" joins a node that is not in the study.`);
    if (L.a === L.b) say(p, `Road segment "${nm}" starts and ends at the same place.`);
    if (!(Number.isInteger(L.lanes) && L.lanes >= 1 && L.lanes <= 3)) say(p, `"${nm}" needs 1, 2 or 3 through lanes in each direction.`);
    if (!(L.speed > 0)) say(p, `"${nm}" needs a speed.`);
  });
  const legs = {}; for (const L of links) { (legs[L.a] = legs[L.a] || []).push(L); (legs[L.b] = legs[L.b] || []).push(L); }
  for (const n of nodes) {
    const k = (legs[n.id] || []).length, nm = n.name || n.id;
    if (n.type === 'end' && k !== 1) say(`node ${n.id}`, `The road end "${nm}" must have exactly one road; it has ${k}.`);
    if (n.type !== 'end' && k < 3) say(`node ${n.id}`, `"${nm}" has ${k} road${k === 1 ? '' : 's'}. An intersection needs 3 or 4.`);
    if (n.type !== 'end' && k > MAX_LEGS) say(`node ${n.id}`, `"${nm}" has ${k} roads. TrafficLab+ handles intersections with 3 or 4 roads; leave a road out of the study, or split the intersection in two.`);
    if (n.type === 'signal' && n.signal && n.signal.mainLinks)
      for (const id of n.signal.mainLinks) if (!(legs[n.id] || []).some(L => L.id === id)) say(`node ${n.id}`, `The signal at "${nm}" names "${id}" as its main street, but that road does not meet it.`);
  }
  if (nodes.filter(n => n.type === 'end').length < 2) say('nodes', 'A study needs at least two road ends, where traffic comes in and goes out.');
  for (const key in (study.pockets || {})) {
    const [lid, tag] = key.split(':');
    if (!linkIds.has(lid) || (tag !== 'ab' && tag !== 'ba')) say(`pockets.${key}`, `The turn lane "${key}" names an approach that is not in the study.`);
  }
  const dm = study.demand || {};
  if (dm.mode === 'volumes') { for (const n of nodes) if (n.type === 'end' && !(n.volume >= 0)) say(`node ${n.id}`, `Type how many vehicles per hour enter at "${n.name || n.id}" (0 if none).`); }
  else if (!(dm.od && dm.od.length) && !(dm.baseTotal > 0)) say('demand.baseTotal', "Say how many vehicles per hour enter the whole study at today's demand.");
  if (!(study.budget > 0)) say('budget', 'The study needs a budget, in millions of dollars.');
  return errs;
}

// The engine's tables. load() fills them; everything below reads them.
let STUDY = null, NODES = {}, LINKS = [], EXISTING_POCKETS = {}, ZONES = {}, ZONE_VOL = {}, NO_TRIPS = [], BASE_TOTAL = 0,
  DEMAND_MODE = 'gravity', OD_FIXED = null, COSTS = null, BUDGET = 0;

// A signal with no main street named takes the most opposed pair of roads, widest first.
function defaultMainLinks(id, legs) {
  const n = STUDY.nodes.find(x => x.id === id); let best = null, bs = -Infinity;
  const away = (L) => { const o = STUDY.nodes.find(x => x.id === (L.a === id ? L.b : L.a)); const dx = o.x - n.x, dy = o.y - n.y, d = Math.hypot(dx, dy) || 1; return [dx / d, dy / d]; };
  for (let i = 0; i < legs.length; i++) for (let j = i + 1; j < legs.length; j++) {
    const a = away(legs[i]), b = away(legs[j]), dot = a[0] * b[0] + a[1] * b[1];
    if (dot > -0.5) continue;
    const s = (legs[i].lanes + legs[j].lanes) * 10 - dot;
    if (s > bs) { bs = s; best = [legs[i].id, legs[j].id]; }
  }
  return best || [legs[0].id];
}

function load(study) {
  const errs = validate(study);
  if (errs.length) { const e = new Error(errs.map(x => x.message).join('\n')); e.problems = errs; throw e; }
  STUDY = study;
  const legs = {}; for (const L of study.links) { (legs[L.a] = legs[L.a] || []).push(L); (legs[L.b] = legs[L.b] || []).push(L); }
  NODES = {};
  for (const n of study.nodes) {
    const N = { type: n.type, x: toU(n.x), y: toU(n.y), name: n.name || n.id, short: n.short || n.name || n.id };
    if (n.type === 'signal') {
      const s = n.signal;
      N.groupA = s.mainLinks && s.mainLinks.length ? s.mainLinks.slice() : defaultMainLinks(n.id, legs[n.id]);
      N.C = s.cycle; N.split = s.split; N.protA = !!s.protMain; N.protB = !!s.protSide;
    }
    NODES[n.id] = N;
  }
  LINKS = study.links.map(L => ({ id: L.id, a: L.a, b: L.b, name: L.name || L.id, lanes: L.lanes, speed: toU(L.speed),
    label: L.label !== false, labelAt: L.labelAt ?? 0.5 }));
  EXISTING_POCKETS = JSON.parse(JSON.stringify(study.pockets || {}));
  const dm = study.demand || {};
  DEMAND_MODE = dm.mode === 'volumes' ? 'volumes' : 'gravity';
  ZONES = {}; ZONE_VOL = {};
  for (const n of study.nodes) if (n.type === 'end') {
    ZONES[n.id] = DEMAND_MODE === 'volumes' ? (n.weight ?? n.volume) : (n.weight ?? 1);
    ZONE_VOL[n.id] = n.volume || 0;
  }
  NO_TRIPS = (dm.noTrips || []).map(p => p.slice());
  BASE_TOTAL = dm.baseTotal || 0;
  OD_FIXED = Array.isArray(dm.od) && dm.od.length ? dm.od.map(p => ({ o: p.o, d: p.d, v: p.v })) : null;
  const k = { ...DEFAULT_COSTS, ...(study.costs || {}) };
  COSTS = { addLane: (len) => +(k.addLaneBase + len * C.M_PER_PX / 1000 * k.addLanePerKm).toFixed(2),
    oneWay: k.oneWay, pocketL: k.pocketL, pocketR: k.pocketR, retime: k.retime, protL: k.protL, roundabout: k.roundabout };
  BUDGET = study.budget;
  Object.assign(API, { STUDY, NODES, LINKS, EXISTING_POCKETS, ZONES, COSTS, BUDGET, DEMAND_MODE });
  return API;
}

// =====================================================================
// helpers
// =====================================================================
function mulberry32(a) { return function () { a |= 0; a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }; }
const mod = (a, n) => ((a % n) + n) % n;
function compass(ux, uy) { return Math.abs(ux) >= Math.abs(uy) ? (ux > 0 ? 'EB' : 'WB') : (uy > 0 ? 'SB' : 'NB'); }
function emptyPlan() { return { links: {}, pockets: {}, signals: {}, roundabouts: {} }; }
function clonePlan(p) { return JSON.parse(JSON.stringify(p)); }

function losFromVC(vc) { return vc <= 0.6 ? 'A' : vc <= 0.7 ? 'B' : vc <= 0.8 ? 'C' : vc <= 0.9 ? 'D' : vc <= 1.0 ? 'E' : 'F'; }
function losFromDelay(d) { return d <= 10 ? 'A' : d <= 20 ? 'B' : d <= 35 ? 'C' : d <= 55 ? 'D' : d <= 80 ? 'E' : 'F'; }

// =====================================================================
// 2. BUILD — plan → network
// =====================================================================
function signalSettings(plan, id) {
  const n = NODES[id]; const s = plan.signals[id] || {};
  return { C: s.C || n.C, split: s.split != null ? s.split : n.split,
    protA: s.protA != null ? s.protA : !!n.protA, protB: s.protB != null ? s.protB : !!n.protB };
}

function buildNetwork(plan) {
  const net = { nodes: {}, dirs: {}, dirList: [], plan, lanesAll: [] };
  for (const id in NODES) {
    const N = NODES[id];
    net.nodes[id] = { id, ...N, inDirs: [], outDirs: [], paths: [], moves: {},
      ctrl: N.type === 'end' ? 'end' : (N.type === 'roundabout' || plan.roundabouts[id] ? 'rab' : 'sig'), ringVeh: [] };
  }
  const trim = (n) => n.ctrl === 'end' ? 0 : n.ctrl === 'rab' ? C.RAB_R + 13 : 19;

  for (const L of LINKS) {
    const ov = plan.links[L.id] || {};
    let nAB = L.lanes + (ov.addLane ? 1 : 0), nBA = nAB;
    let oneWay = false;
    if (ov.oneWay === 'ab') { nAB += nBA; nBA = 0; oneWay = true; }
    if (ov.oneWay === 'ba') { nBA += nAB; nAB = 0; oneWay = true; }
    makeDir(L, L.a, L.b, nAB, 'ab', oneWay);
    makeDir(L, L.b, L.a, nBA, 'ba', oneWay);
  }

  function makeDir(L, from, to, n, tag, oneWay) {
    if (n <= 0) return;
    const A = net.nodes[from], B = net.nodes[to];
    const dx = B.x - A.x, dy = B.y - A.y, D = Math.hypot(dx, dy);
    const ux = dx / D, uy = dy / D, t0 = trim(A), t1 = trim(B);
    const d = { id: L.id + ':' + tag, link: L, from, to, n, ux, uy, nx: -uy, ny: ux,
      x0: A.x + ux * t0, y0: A.y + uy * t0, len: D - t0 - t1, speed: L.speed,
      med: oneWay ? -n * C.LANE_W / 2 : C.MED, lanes: [], pocketL: null, pocketR: null,
      heading: compass(ux, uy), delays: [], vol: 0, vc: 0 };
    d.label = `${L.name} ${d.heading}`;
    for (let i = 0; i < n; i++) d.lanes.push(makeLane(d, i, 0, 'main'));
    const pk = { ...(EXISTING_POCKETS[d.id] || {}), ...(plan.pockets[d.id] || {}) };
    if (B.ctrl !== 'end') {
      if (pk.L) d.pocketL = makeLane(d, -1, Math.max(0, d.len - C.POCKET_LEN), 'L');
      if (pk.R) d.pocketR = makeLane(d, n, Math.max(0, d.len - C.POCKET_LEN), 'R');
    }
    net.dirs[d.id] = d; net.dirList.push(d);
    A.outDirs.push(d); B.inDirs.push(d);
  }
  function makeLane(d, idx, start, kind) {
    const lane = { id: d.id + '#' + idx, dir: d, idx, kind, start, len: d.len,
      off: d.med + (idx + 0.5) * C.LANE_W, veh: [], incoming: 0 };
    net.lanesAll.push(lane); return lane;
  }

  // movements, groups, opposing approaches, signal plans, ring geometry
  for (const id in net.nodes) {
    const node = net.nodes[id];
    if (node.ctrl === 'end') continue;
    for (const d of node.inDirs) {
      node.moves[d.id] = {};
      for (const e of node.outDirs) {
        if (e.link === d.link) continue;                      // no U-turns
        const cross = d.ux * e.uy - d.uy * e.ux, dot = d.ux * e.ux + d.uy * e.uy;
        node.moves[d.id][e.id] = dot > 0.82 ? 'T' : (cross > 0 ? 'R' : 'L');
      }
      d.group = (NODES[id].groupA || []).includes(d.link.id) ? 'A' : 'B';
      let best = null, bd = -0.55;
      for (const o of node.inDirs) { if (o === d) continue; const dot = d.ux * o.ux + d.uy * o.uy; if (dot < bd) { bd = dot; best = o; } }
      d.opp = best;
    }
    if (node.ctrl === 'sig') node.sig = makeSignal(signalSettings(plan, id));
    if (node.ctrl === 'rab') {
      const R = C.RAB_R, L = 2 * Math.PI * R;
      node.ringLen = L; node.entryS = {}; node.exitS = {};
      for (const d of node.inDirs) node.entryS[d.id] = mod(-(Math.atan2(-d.uy, -d.ux) - 0.38) * R, L);
      for (const e of node.outDirs) node.exitS[e.id] = mod(-(Math.atan2(e.uy, e.ux) + 0.38) * R, L);
    }
  }
  return net;
}

// =====================================================================
// 4. SIGNALS — fixed-time, 2 or 3 phases
// =====================================================================
function makeSignal(s) {
  // phase order: [A-left] A [B-left] B   (leading protected lefts)
  const nPh = 2 + (s.protA ? 1 : 0) + (s.protB ? 1 : 0);
  const G = s.C - nPh * (C.YELLOW + C.ALLRED);
  const gLA = s.protA ? Math.max(7, Math.round(0.14 * G)) : 0;
  const gLB = s.protB ? Math.max(7, Math.round(0.12 * G)) : 0;
  const rem = G - gLA - gLB;
  const gA = Math.max(8, Math.round(rem * s.split)), gB = Math.max(8, rem - gA);
  const phases = [];
  if (s.protA) phases.push({ grp: 'A', leftOnly: true, g: gLA });
  phases.push({ grp: 'A', g: gA });
  if (s.protB) phases.push({ grp: 'B', leftOnly: true, g: gLB });
  phases.push({ grp: 'B', g: gB });
  const cycle = phases.reduce((t, p) => t + p.g + C.YELLOW + C.ALLRED, 0);
  return { phases, cycle, settings: s };
}
// returns 'G' protected green, 'P' permitted (must yield), 'Y' yellow, 'R' red
function phaseAllows(p, d, m) {
  if (d.group !== p.grp) return 'R';
  if (p.leftOnly) return m === 'L' ? 'G' : 'R';
  return m === 'L' ? 'P' : 'G';
}
function sigState(node, t, d, m) {
  const S = node.sig; let tt = mod(t, S.cycle);
  for (const p of S.phases) {
    if (tt < p.g) return phaseAllows(p, d, m); tt -= p.g;
    if (tt < C.YELLOW) return phaseAllows(p, d, m) === 'R' ? 'R' : 'Y'; tt -= C.YELLOW;
    if (tt < C.ALLRED) return 'R'; tt -= C.ALLRED;
  }
  return 'R';
}
// effective green ratios for analysis
function greenRatios(sig) {
  const r = { A: 0, B: 0, AL: 0, BL: 0 };
  for (const p of sig.phases) { const g = (p.g + C.YELLOW - 2) / sig.cycle; if (p.leftOnly) r[p.grp + 'L'] += g; else r[p.grp] += g; }
  return r;
}

// =====================================================================
// 3. ROUTING — Dijkstra over directed links (states = dirs)
// =====================================================================
function computeRoutes(net) {
  const routes = {};
  const ends = Object.keys(NODES).filter(k => NODES[k].type === 'end');
  const PEN = { L: 12, R: 5, T: 0 };
  for (const o of ends) {
    routes[o] = {};
    const dist = new Map(), prev = new Map(), open = [];
    for (const d of net.nodes[o].outDirs) { dist.set(d, d.len / d.speed); prev.set(d, null); open.push(d); }
    while (open.length) {
      open.sort((a, b) => dist.get(a) - dist.get(b));
      const d = open.shift(); const node = net.nodes[d.to];
      if (node.ctrl === 'end') continue;
      for (const e of node.outDirs) {
        const m = node.moves[d.id][e.id]; if (!m) continue;
        const c = dist.get(d) + e.len / e.speed + PEN[m] + 8;
        if (!dist.has(e) || c < dist.get(e)) { if (!dist.has(e)) open.push(e); dist.set(e, c); prev.set(e, d); }
      }
    }
    for (const t of ends) {
      if (t === o) continue;
      let best = null, bc = Infinity;
      for (const d of net.nodes[t].inDirs) if (dist.has(d) && dist.get(d) < bc) { bc = dist.get(d); best = d; }
      if (!best) { routes[o][t] = null; continue; }
      const r = []; for (let d = best; d; d = prev.get(d)) r.unshift(d);
      routes[o][t] = r;
    }
  }
  return routes;
}

function odMatrix(demandMult, zoneMult) {
  zoneMult = zoneMult || {};
  // a typed origin-destination table, when the study has one, is used as it stands
  if (OD_FIXED) return OD_FIXED.map(p => ({ o: p.o, d: p.d, v: p.v * demandMult * (zoneMult[p.o] ?? 1) }));
  const ids = Object.keys(ZONES), P = {}; let pairs = [], sum = 0;
  for (const z of ids) P[z] = ZONES[z];
  const banned = (o, d) => o === d || NO_TRIPS.some(([a, b]) => (a === o && b === d) || (a === d && b === o));
  if (DEMAND_MODE === 'volumes') {
    // typed volumes: each zone's entering traffic is shared among the others by their weight
    const out = [];
    for (const o of ids) {
      const dests = ids.filter(d => !banned(o, d)), tot = dests.reduce((s, d) => s + P[d], 0);
      if (!(tot > 0)) continue;
      for (const d of dests) out.push({ o, d, v: ZONE_VOL[o] * P[d] / tot * demandMult * (zoneMult[o] ?? 1) });
    }
    return out;
  }
  for (const o of ids) for (const d of ids) {
    if (banned(o, d)) continue;
    const w = P[o] * P[d]; pairs.push({ o, d, w }); sum += w;
  }
  return pairs.map(p => ({ o: p.o, d: p.d,
    v: BASE_TOTAL * p.w / sum * demandMult * (zoneMult[p.o] ?? 1) }));   // zone multiplier = upstream change to INFLOW only
}

// =====================================================================
// 7. ANALYSIS — planning-level v/c, the "macroscopic" half of the hybrid
// =====================================================================
function analyze(net, routes, od) {
  for (const d of net.dirList) { d.vol = 0; d.mv = { L: 0, T: 0, R: 0 }; d.vc = 0; }
  let unserved = 0;
  const movVol = {}; // node -> "in>out" -> vol
  for (const p of od) {
    const r = routes[p.o][p.d];
    if (!r) { unserved += p.v; continue; }
    for (let i = 0; i < r.length; i++) {
      const d = r[i]; d.vol += p.v;
      if (i < r.length - 1) {
        const node = net.nodes[d.to], m = node.moves[d.id][r[i + 1].id];
        d.mv[m] += p.v;
        (movVol[node.id] = movVol[node.id] || {})[d.id + '>' + r[i + 1].id] = p.v + ((movVol[node.id] || {})[d.id + '>' + r[i + 1].id] || 0);
      }
    }
  }
  for (const d of net.dirList) {
    const node = net.nodes[d.to];
    if (node.ctrl === 'end') { d.vc = d.vol / (d.n * 1900); d.capInfo = 'free-flow exit'; continue; }
    if (node.ctrl === 'sig') {
      const g = greenRatios(node.sig), gr = g[d.group];
      const vo = d.opp ? d.opp.mv.T + d.opp.mv.R : 0, no = d.opp ? d.opp.n : 1;
      let capL = d.opp ? Math.max(0, C.SAT * gr * (1 - vo / (C.SAT * no * gr))) * 0.9 + 2 * 3600 / node.sig.cycle : C.SAT * gr * 0.95;
      capL += C.SAT * 0.95 * g[d.group + 'L'];
      let main = d.mv.T, vcs = [];
      if (d.pocketL) vcs.push(d.mv.L / Math.max(capL, 1)); else main += d.mv.L * Math.min(8, (C.SAT * gr) / Math.max(capL, 60));
      if (d.pocketR) vcs.push(d.mv.R / (C.SAT * 0.85 * gr + 150)); else main += d.mv.R * 1.15;
      vcs.push(main / (d.n * C.SAT * gr));
      d.vc = Math.max(...vcs); d.gr = gr;
    } else { // roundabout: HCM 6th ed. single-lane entry capacity
      const L = node.ringLen, circ = {}; for (const x of node.inDirs) circ[x.id] = 0;
      const mv = movVol[node.id] || {};
      for (const key in mv) {
        const [i, o] = key.split('>'); const s0 = node.entryS[i], len = mod(node.exitS[o] - s0, L);
        for (const k of node.inDirs) { if (k.id === i) continue; const pos = mod(node.entryS[k.id] - s0, L); if (pos > 0 && pos < len) circ[k.id] += mv[key]; }
      }
      const cap = 1130 * Math.exp(-0.001 * circ[d.id]);
      d.vc = (d.mv.L + d.mv.T + d.mv.R) / cap; d.circ = circ[d.id];
    }
  }
  return { unserved, movVol };
}

// =====================================================================
// 5. VEHICLES — Intelligent Driver Model
// =====================================================================
function idm(v, v0, gap, dv) {
  if (gap === Infinity) return C.A_MAX * (1 - Math.pow(v / v0, 4));
  const sStar = C.S0 + Math.max(0, v * C.T_HEAD + v * dv / (2 * Math.sqrt(C.A_MAX * C.B_COMF)));
  const g = Math.max(gap, 0.05);
  return C.A_MAX * (1 - Math.pow(v / v0, 4) - (sStar / g) * (sStar / g));
}

// =====================================================================
// 6. SIMULATION
// =====================================================================
function createSim(plan, opts) {
  opts = opts || {};
  const net = buildNetwork(plan);
  const routes = computeRoutes(net);
  const sim = { net, routes, plan, t: 0, rng: mulberry32(opts.seed || 12345), veh: [], nextId: 1,
    demandMult: opts.demandMult || 1, zoneMult: opts.zoneMult || {}, backlog: {}, arrivals: [],
    warmup: opts.warmup || 0, m: { lost: 0, done: 0, spawned: 0, delaySum: 0, lostTrips: 0 } };
  for (const z in ZONES) sim.backlog[z] = [];
  sim.setDemand = (mult, zoneMult) => { sim.demandMult = mult; if (zoneMult) sim.zoneMult = zoneMult; refreshDemand(sim); };
  refreshDemand(sim);
  return sim;
}
function refreshDemand(sim) {
  sim.od = odMatrix(sim.demandMult, sim.zoneMult);
  sim.analysis = analyze(sim.net, sim.routes, sim.od);
  sim.arrivals = sim.od.map(p => ({ ...p, lam: p.v / 3600, next: sim.t + (-Math.log(1 - sim.rng()) * 3600 / Math.max(p.v, 1e-6)) }));
}

function laneHasRoomAtStart(lane) {
  const last = lane.veh[lane.veh.length - 1];
  return !last || last.pos - C.CAR_LEN > C.S0 + 1 + lane.incoming * (C.CAR_LEN + C.S0);
}
function insertSorted(lane, v) {
  let i = lane.veh.length; while (i > 0 && lane.veh[i - 1].pos < v.pos) i--;
  lane.veh.splice(i, 0, v); return i;
}
function removeFrom(arr, v) { const i = arr.indexOf(v); if (i >= 0) arr.splice(i, 1); }

// movement the vehicle will make at the end of route[ri]
function moveAt(sim, v, ri) {
  const r = v.route; if (ri >= r.length - 1) return 'X';
  return sim.net.nodes[r[ri].to].moves[r[ri].id][r[ri + 1].id];
}
// pick a lane when entering dir (route index ri) — turners go to their side
function chooseLane(dir, m) {
  if (m === 'L') return dir.lanes[0];
  if (m === 'R') return dir.lanes[dir.n - 1];
  let best = dir.lanes[0], bc = Infinity;
  for (const l of dir.lanes) { const c = l.veh.length + l.incoming; if (c < bc) { bc = c; best = l; } }
  return best;
}
function desiredLaneIdx(dir, m) { return m === 'L' ? 0 : m === 'R' ? dir.n - 1 : -1; }

function getPath(node, fromLane, toLane, m) {
  const key = fromLane.id + '>' + toLane.id;
  let p = node.paths.find(q => q.key === key); if (p) return p;
  const a = fromLane.dir, b = toLane.dir;
  const P0 = { x: a.x0 + a.ux * a.len + a.nx * fromLane.off, y: a.y0 + a.uy * a.len + a.ny * fromLane.off };
  const P3 = { x: b.x0 + b.nx * toLane.off, y: b.y0 + b.ny * toLane.off };
  const dist = Math.hypot(P3.x - P0.x, P3.y - P0.y), k = dist * 0.42;
  const P1 = { x: P0.x + a.ux * k, y: P0.y + a.uy * k }, P2 = { x: P3.x - b.ux * k, y: P3.y - b.uy * k };
  const pts = [], cum = [0];
  for (let i = 0; i <= 16; i++) {
    const t = i / 16, u = 1 - t;
    pts.push({ x: u*u*u*P0.x + 3*u*u*t*P1.x + 3*u*t*t*P2.x + t*t*t*P3.x, y: u*u*u*P0.y + 3*u*u*t*P1.y + 3*u*t*t*P2.y + t*t*t*P3.y });
    if (i) cum.push(cum[i - 1] + Math.hypot(pts[i].x - pts[i - 1].x, pts[i].y - pts[i - 1].y));
  }
  p = { key, node, from: a, fromLane, to: b, toLane, m, pts, cum, len: cum[16], veh: [] };
  node.paths.push(p); return p;
}

// --- conflict checks for permitted movements -------------------------
function canStop(v, dist) { return dist > (v.v * v.v) / (2 * C.B_MAX) || v.v < 3; }
function oppClear(sim, node, d, myTarget) {
  const o = d.opp; if (!o) return true;
  // an opposing RIGHT turn only conflicts if we both squeeze into a 1-lane exit
  const rightOK = (w) => w.m === 'R' && w.route[w.ri + 1] === myTarget && myTarget.n >= 2;
  const st = sigState(node, sim.t, o, 'T');
  if (st !== 'R') {
    for (const lane of o.lanes.concat(o.pocketR ? [o.pocketR] : [])) {
      for (const w of lane.veh) {
        if (w.m === 'L' || rightOK(w)) continue;
        const dist = o.len - w.pos;
        if (dist > w.v * 4.5 + 14) break;
        if (st === 'G' || !canStop(w, dist)) return false;
      }
    }
  }
  for (const p of node.paths) if (p.from === o && p.m !== 'L' && p.veh.length && !(p.m === 'R' && p.to === myTarget && myTarget.n >= 2)) return false;
  return true;
}
function rtorClear(sim, node, d, target) {
  for (const l of target.lanes) if (l.incoming > 0) return false;
  for (const x of node.inDirs) {
    if (x === d) continue;
    for (const lane of x.lanes) for (const w of lane.veh) {
      const dist = x.len - w.pos; if (dist > w.v * 4 + 16) break;
      if (w.route[w.ri + 1] === target && sigState(node, sim.t, x, w.m) !== 'R') return false;
    }
  }
  return true;
}
function ringClear(node, s) {
  const L = node.ringLen;
  for (const r of node.ringVeh) {
    const up = mod(s - r.s, L), ahead = mod(r.s - s, L);
    if (up < 22 + r.v * 0.8 || ahead < C.CAR_LEN + 3) return false;
  }
  return node.ringVeh.length < Math.floor(L / (C.CAR_LEN + C.S0)) - 1;
}

// may the front vehicle on `lane` enter the node now?
function mayEnter(sim, v, lane) {
  const d = lane.dir, node = sim.net.nodes[d.to], next = v.route[v.ri + 1];
  const dist = d.len - v.pos;
  if (node.ctrl === 'rab') return ringClear(node, node.entryS[d.id]) && dist < 6;
  const st = sigState(node, sim.t, d, v.m);
  if (st === 'G') return true;
  if (st === 'P') return oppClear(sim, node, d, next);
  if (st === 'Y') {
    if (v.m === 'L') return dist < 4 && oppClear(sim, node, d, next);      // "sneaker" on yellow
    return !canStop(v, dist);                                         // dilemma zone: can't stop, go
  }
  if (v.m === 'R' && v.v < 0.5 && dist < 3) return rtorClear(sim, node, d, next); // right on red
  return false;
}

function trySpawn(sim) {
  for (const a of sim.arrivals) {
    while (a.next <= sim.t) {
      a.next += -Math.log(1 - sim.rng()) / Math.max(a.lam, 1e-9);
      const route = sim.routes[a.o][a.d];
      if (!route) { if (sim.t >= sim.warmup) sim.m.lostTrips++; continue; }
      sim.backlog[a.o].push({ route, tSpawn: sim.t, wait: 0 });
      if (sim.t >= sim.warmup) sim.m.spawned++;
    }
  }
  for (const z in sim.backlog) {
    const q = sim.backlog[z];
    let tries = 0;
    while (q.length && tries++ < 3) {
      const b = q[0], dir = b.route[0];
      const v = { id: sim.nextId++, route: b.route, ri: 0, pos: 0, v: dir.speed * 0.8, tSpawn: b.tSpawn,
        tripLost: b.wait, dirLost: 0, lat: null, hue: (sim.nextId * 47) % 360, stopped: 0 };
      v.m = moveAt(sim, v, 0);
      const lane = chooseLane(dir, v.m);
      if (!laneHasRoomAtStart(lane)) break;
      const last = lane.veh[lane.veh.length - 1];
      if (last) v.v = Math.min(v.v, last.v + 2);
      v.lane = lane; v.lat = lane.off; lane.veh.push(v); sim.veh.push(v); q.shift();
    }
    for (const b of q) b.wait += C.DT;
  }
}

function enterDirFromNode(sim, v, lane, posOver) {
  v.ri++; v.m = moveAt(sim, v, v.ri);
  v.pos = Math.max(0, posOver); v.lane = lane; v.path = null; v.ring = null;
  const i = insertSorted(lane, v);
  const lead = lane.veh[i - 1]; if (lead && v.pos > lead.pos - C.CAR_LEN - 0.5) v.pos = Math.max(0, lead.pos - C.CAR_LEN - 0.5);
}
function recordDirDelay(sim, v, d) {
  d.delays.push({ t: sim.t, d: v.dirLost }); v.dirLost = 0;
  while (d.delays.length && d.delays[0].t < sim.t - 300) d.delays.shift();
}

function step(sim) {
  const dt = C.DT, net = sim.net;
  trySpawn(sim);
  const measuring = sim.t >= sim.warmup;

  // ----- lanes (front to back) -----
  for (const lane of net.lanesAll) {
    const d = lane.dir, node = net.nodes[d.to];
    for (let i = 0; i < lane.veh.length; i++) {
      const v = lane.veh[i], lead = lane.veh[i - 1];
      const dist = d.len - v.pos;
      let v0 = d.speed;
      if ((v.m === 'L' || v.m === 'R') && dist < 50) v0 = Math.min(v0, C.TURN_SPEED + dist * 0.25);
      if (node.ctrl === 'rab' && dist < 60) v0 = Math.min(v0, 9 + dist * 0.3);
      let gap = Infinity, dv = 0;
      if (lead) { gap = lead.pos - C.CAR_LEN - v.pos; dv = v.v - lead.v; }
      // must stop at pocket entrance if pocket is full (spillback blocks the through lane)
      let wantPocket = null;
      if (lane.kind === 'main') {
        if (v.m === 'L' && d.pocketL && lane.idx === 0) wantPocket = d.pocketL;
        if (v.m === 'R' && d.pocketR && lane.idx === d.n - 1) wantPocket = d.pocketR;
      }
      if (wantPocket) {
        const ps = wantPocket.start - v.pos;
        if (ps <= 0.5) {
          const pl = wantPocket.veh[wantPocket.veh.length - 1];
          if (!pl || pl.pos - C.CAR_LEN - C.S0 > v.pos) {   // move into pocket
            lane.veh.splice(i, 1); i--; v.lane = wantPocket; insertSorted(wantPocket, v); continue;
          }
          gap = Math.min(gap, C.S0 * 0.6); dv = v.v;
        } else if (ps < 80) {
          const pl = wantPocket.veh[wantPocket.veh.length - 1];
          if (pl && pl.pos - C.CAR_LEN - C.S0 < wantPocket.start + 1 && ps + C.S0 < gap) { gap = ps + C.S0; dv = v.v; }
        }
      }
      // node entry: only the front vehicle of the lane decides
      let go = true;
      if (!lead && node.ctrl !== 'end') {
        if (dist < 120) {
          go = mayEnter(sim, v, lane);
          // also need room in the exit lane (don't block the box)
          const nd = v.route[v.ri + 1];
          if (go) { const m2 = moveAt(sim, v, v.ri + 1); v._tgt = pickOutLane(nd, v.m, m2, lane.kind === 'main' ? lane.idx : (lane.kind === 'L' ? 0 : 99)); go = laneHasRoomAtStart(v._tgt); }
          if (!go && dist + C.S0 - 1 < gap) { gap = Math.max(0, dist + C.S0 - 1); dv = v.v; }
        }
      }
      let a = idm(v.v, v0, gap, dv);
      a = Math.max(a, -C.B_MAX * 1.6);
      v.v = Math.max(0, v.v + a * dt);
      let np = v.pos + v.v * dt;
      if (lead) np = Math.min(np, lead.pos - C.CAR_LEN - 0.3);
      v.pos = Math.max(v.pos, np);
      const lost = dt * Math.max(0, 1 - v.v / d.speed);
      v.dirLost += lost; v.tripLost += lost; if (measuring) sim.m.lost += lost;

      // optional lane change to reach the turning side
      if (lane.kind === 'main' && d.n > 1 && v.pos > 12 && dist > C.POCKET_LEN * 0.6) {
        const want = desiredLaneIdx(d, v.m);
        if (want >= 0 && want !== lane.idx) {
          const tl = d.lanes[lane.idx + Math.sign(want - lane.idx)];
          if (tryLaneChange(v, lane, tl)) { i--; continue; }
        }
      }

      // reached the stop line?
      if (v.pos >= d.len - 0.5 && !lead) {
        if (node.ctrl === 'end') {
          lane.veh.splice(i, 1); i--; finishTrip(sim, v); continue;
        }
        if (go) {
          lane.veh.splice(i, 1); i--;
          recordDirDelay(sim, v, d);
          const nd = v.route[v.ri + 1];
          const tgt = v._tgt || pickOutLane(nd, v.m, moveAt(sim, v, v.ri + 1), lane.idx);
          tgt.incoming++;
          if (node.ctrl === 'rab') {
            v.lane = null; v.ring = node; v.s = node.entryS[d.id]; v.tgt = tgt;
            v.ringLeft = mod(node.exitS[nd.id] - v.s, node.ringLen); v.v = Math.min(v.v, C.RING_SPEED);
            node.ringVeh.push(v);
          } else {
            const p = getPath(node, lane, tgt, v.m);
            v.lane = null; v.path = p; v.pos = 0; v.tgt = tgt; p.veh.push(v);
          }
        } else v.pos = Math.min(v.pos, d.len - 0.5);
      }
    }
  }

  // ----- intersection paths -----
  for (const id in net.nodes) {
    const node = net.nodes[id];
    for (const p of node.paths) {
      for (let i = 0; i < p.veh.length; i++) {
        const v = p.veh[i], lead = p.veh[i - 1];
        let gap = Infinity, dv = 0;
        if (lead) { gap = lead.pos - C.CAR_LEN - v.pos; dv = v.v - lead.v; }
        else { const last = p.toLane.veh[p.toLane.veh.length - 1]; if (last) { gap = (p.len - v.pos) + last.pos - C.CAR_LEN; dv = v.v - last.v; } }
        const v0 = p.m === 'T' ? p.to.speed : C.TURN_SPEED + 4;
        v.v = Math.max(0, v.v + Math.max(idm(v.v, v0, gap, dv), -C.B_MAX * 1.6) * dt);
        v.pos += v.v * dt;
        const lost = dt * Math.max(0, 1 - v.v / p.from.speed);
        v.dirLost += lost; v.tripLost += lost; if (measuring) sim.m.lost += lost;
        if (v.pos >= p.len) {
          p.veh.splice(i, 1); i--; p.toLane.incoming--;
          addLostToPrev(sim, v, p.from);
          enterDirFromNode(sim, v, p.toLane, v.pos - p.len);
        }
      }
    }
    // ----- roundabout ring -----
    if (node.ctrl === 'rab') {
      const L = node.ringLen;
      for (const v of node.ringVeh) {
        let gap = Infinity, dv = 0;
        for (const w of node.ringVeh) { if (w === v) continue; const g = mod(w.s - v.s, L) - C.CAR_LEN; if (g > -C.CAR_LEN / 2 && g < gap) { gap = Math.max(g, 0.05); dv = v.v - w.v; } }
        if (v.ringLeft < 30 && !laneHasRoomAtStartIgnoring(v.tgt, 1)) { const g = Math.max(0, v.ringLeft - 1); if (g < gap) { gap = g; dv = v.v; } }
        v._a = idm(v.v, C.RING_SPEED, gap, dv);
      }
      for (let i = 0; i < node.ringVeh.length; i++) {
        const v = node.ringVeh[i];
        v.v = Math.max(0, v.v + Math.max(v._a, -C.B_MAX * 1.6) * dt);
        const ds = Math.min(v.v * dt, Math.max(0, v.ringLeft));
        v.s = mod(v.s + ds, L); v.ringLeft -= ds;
        const ref = v.route[v.ri].speed, lost = dt * Math.max(0, 1 - v.v / ref);
        v.dirLost += lost; v.tripLost += lost; if (measuring) sim.m.lost += lost;
        if (v.ringLeft <= 0.2) {
          node.ringVeh.splice(i, 1); i--; v.tgt.incoming--;
          addLostToPrev(sim, v, v.route[v.ri]);
          enterDirFromNode(sim, v, v.tgt, 0);
        }
      }
    }
  }
  // backlog delay
  if (measuring) for (const z in sim.backlog) sim.m.lost += sim.backlog[z].length * dt;
  sim.t += dt;
}
function addLostToPrev(sim, v, d) { if (v.dirLost > 0) { const last = d.delays[d.delays.length - 1]; if (last) last.d += v.dirLost; v.dirLost = 0; } }
function laneHasRoomAtStartIgnoring(lane, own) {
  const last = lane.veh[lane.veh.length - 1];
  return !last || last.pos - C.CAR_LEN > C.S0 + 1 + Math.max(0, lane.incoming - own) * (C.CAR_LEN + C.S0);
}
function pickOutLane(dir, m, m2, fromIdx) {
  // preference order: lane nearest the turn (or best for the NEXT turn), then the others
  const idx = dir.lanes.map(l => l.idx);
  let pref;
  if (m === 'R') pref = dir.n - 1; else if (m === 'L') pref = 0;
  else if (m2 === 'L') pref = 0; else if (m2 === 'R') pref = dir.n - 1;
  else pref = Math.min(dir.n - 1, Math.max(0, fromIdx ?? 0));
  idx.sort((a, b) => Math.abs(a - pref) - Math.abs(b - pref));
  for (const i of idx) if (laneHasRoomAtStart(dir.lanes[i])) return dir.lanes[i];
  return dir.lanes[pref];
}
function tryLaneChange(v, from, to) {
  // need gaps ahead and behind in the target lane
  let ahead = null, behind = null;
  for (const w of to.veh) { if (w.pos >= v.pos) ahead = w; else { behind = w; break; } }
  if (ahead && ahead.pos - C.CAR_LEN - v.pos < C.S0 + v.v * 0.6) return false;
  if (behind && v.pos - C.CAR_LEN - behind.pos < C.S0 + behind.v * 0.9) return false;
  removeFrom(from.veh, v); v.lane = to; insertSorted(to, v); return true;
}
function finishTrip(sim, v) {
  removeFrom(sim.veh, v);
  if (sim.t >= sim.warmup) { sim.m.done++; sim.m.delaySum += v.tripLost; }
}
function cleanup(sim) { // keep sim.veh in sync (vehicles finished are removed in finishTrip)
}

// average measured delay per approach (last 5 min)
function dirDelay(d) { if (!d.delays.length) return null; let s = 0; for (const x of d.delays) s += x.d; return s / d.delays.length; }

// count of vehicles currently stuck in entry backlogs
function backlogCount(sim) { let n = 0; for (const z in sim.backlog) n += sim.backlog[z].length; return n; }

// =====================================================================
// TEST RUN — deterministic (seeded) evaluation used for scoring
// =====================================================================
// Runs the same plan with several random seeds and averages — one seed is
// too noisy to compare plans fairly (a great lesson in its own right).
const SEEDS = [777, 1234, 4242];
function evaluate(plan, opts, onProgress) {
  const warm = opts.warmup ?? 180, meas = opts.measure ?? 720, seeds = opts.seeds || SEEDS;
  const per = (warm + meas) / C.DT, total = per * seeds.length;
  let si = 0, k = 0, sim = null; const results = [];
  return new Promise(resolve => {
    function chunk() {
      const t0 = Date.now();
      while (Date.now() - t0 < 30 && si < seeds.length) {
        if (!sim) sim = createSim(plan, { seed: seeds[si], demandMult: opts.demandMult, zoneMult: opts.zoneMult, warmup: warm });
        for (let j = 0; j < 200 && k < per; j++, k++) step(sim);
        if (k >= per) { results.push(summarize(sim, meas)); sim = null; k = 0; si++; }
      }
      if (onProgress) onProgress((si * per + k) / total);
      if (si < seeds.length) setTimeout(chunk, 0); else resolve(average(results));
    }
    chunk();
  });
}
function evaluateSync(plan, opts) {
  const warm = opts.warmup ?? 180, meas = opts.measure ?? 720, seeds = opts.seeds || SEEDS;
  const results = seeds.map(seed => {
    const sim = createSim(plan, { seed, demandMult: opts.demandMult, zoneMult: opts.zoneMult, warmup: warm });
    const total = (warm + meas) / C.DT; for (let k = 0; k < total; k++) step(sim);
    return summarize(sim, meas);
  });
  return average(results);
}
function average(rs) {
  const out = { ...rs[0] };
  for (const k in out) if (typeof out[k] === 'number') out[k] = rs.reduce((s, r) => s + r[k], 0) / rs.length;
  out.runs = rs.map(r => r.avgDelay);
  // per approach: v/c is the same in every run; measured delay is averaged over the runs that have one
  out.approaches = rs[0].approaches.map((a, i) => {
    const ds = rs.map(r => r.approaches[i].delay).filter(x => x != null);
    return { ...a, delay: ds.length ? ds.reduce((s, x) => s + x, 0) / ds.length : null };
  });
  return out;
}
function summarize(sim, meas) {
  const m = sim.m;
  const demand = sim.od.reduce((s, p) => s + p.v, 0);
  const expected = demand * meas / 3600;
  const vcs = sim.net.dirList.filter(d => sim.net.nodes[d.to].ctrl !== 'end').map(d => d.vc);
  const worstVC = Math.max(...vcs);
  return {
    avgDelay: m.lost / Math.max(1, m.done),
    done: m.done, expected, throughput: Math.min(1.2, m.done / Math.max(1, expected)),
    vehHrsLost: m.lost / 3600,
    worstVC, worstLOS: losFromVC(worstVC),
    shareOK: vcs.filter(v => v <= 0.9).length / vcs.length,
    unserved: sim.analysis.unserved, backlog: backlogCount(sim), inNetwork: sim.veh.length,
    approaches: sim.net.dirList.filter(d => sim.net.nodes[d.to].ctrl !== 'end').map(d => ({
      id: d.id, label: d.label, at: NODES[d.to].short, vc: d.vc, los: losFromVC(d.vc), delay: dirDelay(d) }))
  };
}

// =====================================================================
// PLAN COSTING & SCORING
// =====================================================================
function planItems(plan) {
  const items = [];
  for (const id in plan.links) {
    const L = LINKS.find(l => l.id === id), o = plan.links[id];
    const net0 = null;
    const len = Math.hypot(NODES[L.b].x - NODES[L.a].x, NODES[L.b].y - NODES[L.a].y);
    if (o.addLane) items.push({ key: `lane:${id}`, label: `Add through lane — ${L.name} (${NODES[L.a].name.split(' (')[0]} ↔ ${NODES[L.b].name.split(' (')[0]})`, cost: COSTS.addLane(len) });
    if (o.oneWay) items.push({ key: `ow:${id}`, label: `One-way ${L.name} toward ${NODES[o.oneWay === 'ab' ? L.b : L.a].name}`, cost: COSTS.oneWay });
  }
  for (const did in plan.pockets) {
    const p = plan.pockets[did], [lid, tag] = did.split(':'); const L = LINKS.find(l => l.id === lid);
    const to = NODES[tag === 'ab' ? L.b : L.a];
    const A = NODES[tag === 'ab' ? L.a : L.b];
    const hd = compass(to.x - A.x, to.y - A.y);
    const ex = EXISTING_POCKETS[did] || {};
    if (p.L && !ex.L) items.push({ key: `pl:${did}`, label: `Left-turn lane — ${L.name} ${hd} at ${to.name}`, cost: COSTS.pocketL });
    if (p.R && !ex.R) items.push({ key: `pr:${did}`, label: `Right-turn lane — ${L.name} ${hd} at ${to.name}`, cost: COSTS.pocketR });
  }
  for (const id in plan.signals) {
    const s = plan.signals[id], n = NODES[id];
    if ((s.C && s.C !== n.C) || (s.split != null && Math.abs(s.split - n.split) > 1e-6)) items.push({ key: `rt:${id}`, label: `Retime signal — ${n.name}`, cost: COSTS.retime });
    if (s.protA && !n.protA) items.push({ key: `pa:${id}`, label: `Protected left (main street) — ${n.name}`, cost: COSTS.protL });
    if (s.protB && !n.protB) items.push({ key: `pb:${id}`, label: `Protected left (side street) — ${n.name}`, cost: COSTS.protL });
    if ((s.protA === false && n.protA) || (s.protB === false && n.protB)) items.push({ key: `pr0:${id}`, label: `Remove protected left phase — ${n.name}`, cost: COSTS.retime });
  }
  for (const id in plan.roundabouts) if (plan.roundabouts[id]) items.push({ key: `rb:${id}`, label: `Convert to roundabout — ${NODES[id].name}`, cost: COSTS.roundabout });
  return items;
}
function planCost(plan) { return +planItems(plan).reduce((s, i) => s + i.cost, 0).toFixed(2); }

function score(result, baseline, cost) {
  const red = (baseline.avgDelay - result.avgDelay) / Math.max(1, baseline.avgDelay);
  const pDelay = 50 * Math.max(0, Math.min(1, red / 0.4));
  const pLOS = 25 * result.shareOK;
  const pBudget = cost <= BUDGET ? 15 * (1 - cost / BUDGET) : 0;
  const pThru = 10 * Math.min(1, result.throughput);
  let s = pDelay + pLOS + pBudget + pThru - result.unserved / 8;   // stranded trips are heavily penalized
  if (cost > BUDGET) s *= 0.5;
  return { total: Math.max(0, Math.round(s)), red, parts: { pDelay, pLOS, pBudget, pThru } };
}

const API = { FORMAT, load, validate, toU, C, STUDY, EXISTING_POCKETS, NODES, LINKS, ZONES, COSTS, BUDGET, DEMAND_MODE, emptyPlan, clonePlan, buildNetwork, computeRoutes,
  createSim, step, sigState, mayEnter, analyze, odMatrix, evaluate, evaluateSync, planItems, planCost, score,
  losFromVC, losFromDelay, dirDelay, backlogCount, signalSettings, refreshDemand, mod };
if (typeof module !== 'undefined' && module.exports) module.exports = API; else root.TG = API;
})(typeof window !== 'undefined' ? window : globalThis);
