// Asks the engine about studies, for the C# tests (StudyModelTests).
//   node tools/engine-check.js validate <studies.json>   → for each study, the messages validate() gives
//   node tools/engine-check.js run <study.json>          → loads it, drives 10 simulated minutes, and
//                                                          reports trips done, stranded demand and the worst v/c
//   node tools/engine-check.js entering <study.json>     → veh/h entering at each road end at today's demand
'use strict';
const fs = require('fs');
const path = require('path');
const TG = require(path.join(__dirname, '..', 'web', 'core', 'trafficlab-core.js'));

const [, , what, file] = process.argv;
const input = JSON.parse(fs.readFileSync(file, 'utf8'));

if (what === 'validate') {
  process.stdout.write(JSON.stringify(input.map(s => TG.validate(s).map(e => e.message))));
} else if (what === 'run') {
  const T = TG.load(input);
  const sim = T.createSim(T.emptyPlan(), { seed: 1 });
  for (let i = 0; i < 600 / T.C.DT; i++) T.step(sim);
  const res = T.evaluateSync(T.emptyPlan(), { seeds: [1], warmup: 60, measure: 240 });
  process.stdout.write(JSON.stringify({ done: sim.m.done, unserved: sim.analysis.unserved, worstVC: res.worstVC, avgDelay: res.avgDelay }));
} else if (what === 'entering') {
  const T = TG.load(input), out = {};
  for (const r of T.odMatrix(1, {})) out[r.o] = (out[r.o] || 0) + r.v;
  process.stdout.write(JSON.stringify(out));
} else {
  process.stderr.write('usage: engine-check.js validate|run|entering <file>');
  process.exit(2);
}
