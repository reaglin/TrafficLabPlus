// Writes samples/lpga.json — Ron's LPGA Traffic Lab as a TrafficLab+ study.
// The network, zones, turn lanes, costs and budget are read from the original page's
// own core, so nothing is retyped; positions and speeds are converted from LPGA's
// drawing unit (1 u = 0.6 m) to metres. The backdrop (local streets, blocks, places,
// I-95's mainline) was drawn in the original page's interface code and is copied here.
//   node tools/make-lpga-study.js
'use strict';
const fs = require('fs');
const path = require('path');
const { ROOT, loadReferenceCore } = require('../tests/js/reference');

const R = loadReferenceCore();
const M = R.C.M_PER_PX;
const m = (u) => Math.round(u * M * 1e6) / 1e6;
const pts = (list) => list.map(([x, y]) => [m(x), m(y)]);

const nodes = Object.entries(R.NODES).map(([id, n]) => {
  const out = { id, type: n.type, name: n.name, x: m(n.x), y: m(n.y) };
  if (n.type === 'end') out.weight = R.ZONES[id];
  if (n.type === 'signal') {
    out.short = n.name.replace('LPGA & ', '');
    out.signal = { cycle: n.C, split: n.split, mainLinks: n.groupA, protMain: !!n.protA, protSide: !!n.protB };
  }
  return out;
});

// LPGA labelled every road but the south end of Market Center, Williamson's north end a
// little lower and Gateway a little higher.
const links = R.LINKS.map(L => {
  const out = { id: L.id, a: L.a, b: L.b, name: L.name, lanes: L.lanes, speed: m(L.speed) };
  if (L.id === 'O3') out.label = false;
  if (L.id === 'W1') out.labelAt = 0.55;
  if (L.id === 'O1') out.labelAt = 0.4;
  return out;
});

const LOCAL = [
  [[150, 250], [235, 250], [250, 300], [300, 300]], [[180, 200], [180, 290], [260, 300]], [[240, 230], [300, 210], [340, 240]],
  [[470, 120], [560, 140], [590, 200]], [[640, 90], [700, 120], [760, 100], [800, 150]], [[700, 120], [720, 200], [790, 210]],
  [[760, 40], [770, 110]], [[820, 150], [880, 160], [880, 220]], [[740, 300], [850, 300], [860, 380]], [[730, 260], [740, 300]],
  [[520, 400], [600, 360], [640, 420]], [[540, 380], [560, 470]], [[600, 300], [660, 330]], [[430, 600], [520, 610], [540, 700]],
  [[440, 650], [440, 780], [560, 790]], [[540, 610], [610, 600], [640, 700], [700, 720]], [[600, 700], [600, 800]], [[300, 470], [380, 520], [420, 600]]
];
const BLOCKS = [[[420, 380], [560, 360], [600, 470], [470, 500]], [[430, 590], [640, 590], [690, 800], [450, 810]], [[700, 130], [880, 120], [900, 240], [740, 260]]];
const POIS = [
  { x: 190, y: 238, t: "Buc-ee's" }, { x: 528, y: 470, t: 'Twin Peaks' }, { x: 520, y: 560, t: "Ford's Garage" },
  { x: 560, y: 690, t: 'Academy Sports' }, { x: 92, y: 150, t: 'I-95' }
];

// LPGA's costs, written the way a study writes them: a lane is a base price plus a price per km.
const c0 = R.COSTS.addLane(0);
const study = {
  format: 1,
  title: 'LPGA Traffic Lab',
  short: 'LPGA',
  subtitle: 'Fix the LPGA Blvd / I-95 / Williamson corridor on a $5M budget',
  place: 'Daytona Beach, Florida',
  author: 'Dr. Ron Eaglin',
  course: '',
  intro: 'This is the LPGA Blvd corridor at I-95 in Daytona Beach.',
  world: { x: 0, y: 0, w: m(1000), h: m(840) },
  budget: R.BUDGET,
  costs: { addLaneBase: c0, addLanePerKm: 0.004 / M * 1000, oneWay: R.COSTS.oneWay, pocketL: R.COSTS.pocketL, pocketR: R.COSTS.pocketR,
    retime: R.COSTS.retime, protL: R.COSTS.protL, roundabout: R.COSTS.roundabout },
  demand: {
    mode: 'gravity',
    baseTotal: 3800,
    noTrips: [['I95N', 'I95S']],
    noTripsWhy: 'Freeway-to-freeway trips stay on I-95.',
    scenarios: [{ name: 'Today', mult: 1.0 }, { name: '2035', mult: 1.25 }, { name: 'Race Week', mult: 1.5 }]
  },
  nodes, links,
  pockets: R.EXISTING_POCKETS,
  backdrop: {
    streets: LOCAL.map(pts),
    blocks: BLOCKS.map(pts),
    places: POIS.map(p => ({ x: m(p.x), y: m(p.y), text: p.t })),
    freeways: [{ points: pts([[26, -10], [66, 850]]), width: m(30) }]
  },
  sources: { network: 'Drawn by hand by Dr. Ron Eaglin for the original LPGA Traffic Lab', demand: 'Gravity model, LPGA Traffic Lab' }
};

const out = path.join(ROOT, 'samples', 'lpga.json');
fs.writeFileSync(out, JSON.stringify(study, null, 2) + '\n');
console.log('wrote ' + out);
