// Writes samples/four-way.json — the built-in "Four-Way Intersection" example (Ron, 2026-10-09:
// "Add the 4 way intersection to the built in examples"). One signal where a four-lane main street
// crosses a two-lane side street, no turn lanes today. Run: node tools/make-four-way.js
//
// Tuned with tools/engine-check.js: today the worst approach is near capacity (v/c ≈ 0.86, LOS D);
// on "Busy day" it fails (≈ 1.5). The $2M budget buys turn lanes, retiming and protected lefts, but
// not a roundabout ($2.8M) — the puzzle is which small fixes matter most.
'use strict';
const fs = require('fs');
const path = require('path');

const mph = (v) => Math.round(v * 0.44704 * 10000) / 10000;   // the study keeps m/s
const study = {
  format: 1,
  title: 'Four-Way Intersection',
  short: '4-WAY',
  subtitle: 'Fix one busy crossroads on a $2M budget',
  intro: 'A four-lane main street crosses a two-lane side street at one traffic signal. There are no turn lanes yet, so every left turn waits in a through lane.',
  author: 'Dr. Ron Eaglin',
  budget: 2,
  costs: { addLaneBase: 0.6, addLanePerKm: 0.004 / 0.6 * 1000, oneWay: 0.15, pocketL: 0.45, pocketR: 0.35, retime: 0.02, protL: 0.08, roundabout: 2.8 },
  demand: {
    mode: 'gravity',
    baseTotal: 2400,
    scenarios: [{ name: 'Today', mult: 1 }, { name: 'In 10 years', mult: 1.25 }, { name: 'Busy day', mult: 1.5 }],
  },
  nodes: [
    { id: 'W', type: 'end', name: 'Main Street (west)', x: 0, y: 300, weight: 900 },
    { id: 'E', type: 'end', name: 'Main Street (east)', x: 600, y: 300, weight: 900 },
    { id: 'N', type: 'end', name: 'Oak Avenue (north)', x: 300, y: 0, weight: 450 },
    { id: 'S', type: 'end', name: 'Oak Avenue (south)', x: 300, y: 600, weight: 450 },
    { id: 'X', type: 'signal', name: 'Main Street & Oak Avenue', short: 'Oak Avenue', x: 300, y: 300,
      signal: { cycle: 90, split: 0.6, mainLinks: ['MW', 'ME'], protMain: false, protSide: false } },
  ],
  links: [
    { id: 'MW', a: 'W', b: 'X', name: 'Main Street', lanes: 2, speed: mph(40) },
    { id: 'ME', a: 'X', b: 'E', name: 'Main Street', lanes: 2, speed: mph(40) },
    { id: 'ON', a: 'N', b: 'X', name: 'Oak Avenue', lanes: 1, speed: mph(30) },
    { id: 'OS', a: 'X', b: 'S', name: 'Oak Avenue', lanes: 1, speed: mph(30) },
  ],
  sources: { network: 'Made up for TrafficLab+ as a first, single-intersection example' },
};

const out = path.join(__dirname, '..', 'samples', 'four-way.json');
fs.writeFileSync(out, JSON.stringify(study, null, 2) + '\n');
console.log('wrote ' + out);
