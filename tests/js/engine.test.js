// Engine tests — run with `node --test tests/js` (dotnet test runs them too, EngineTests.cs).
'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const { loadReferenceCore, loadCore, readStudy } = require('./reference');

// The original core runs in its own sandbox, so its objects have other prototypes: compare as JSON.
const same = (a, b, msg) => assert.equal(JSON.stringify(a ?? null), JSON.stringify(b ?? null), msg);

// ------------------------------------------------------------ small studies
// A study in metres: a node list and a link list are all most tests need.
function study(nodes, links, extra) {
  return Object.assign({ format: 1, title: 'Test', budget: 5, demand: { mode: 'gravity', baseTotal: 1200 }, nodes, links, pockets: {} }, extra || {});
}
const sig = (id, x, y, more) => Object.assign({ id, type: 'signal', name: id, x, y, signal: { cycle: 90, split: 0.6 } }, more || {});
const end = (id, x, y, weight) => ({ id, type: 'end', name: id, x, y, weight: weight || 1, volume: weight ? weight * 100 : 300 });
const link = (id, a, b, lanes) => ({ id, a, b, name: id, lanes: lanes || 1, speed: 13.4 });

// a plain 4-leg crossroads, 300 m arms
function crossroads(extra) {
  return study([sig('X', 0, 0), end('W', -300, 0), end('E', 300, 0), end('N', 0, -300), end('S', 0, 300)],
    [link('a', 'W', 'X', 2), link('b', 'X', 'E', 2), link('c', 'N', 'X'), link('d', 'X', 'S')], extra);
}

function run(T, seconds, seed, plan) {
  const sim = T.createSim(plan || T.emptyPlan(), { seed: seed || 1 });
  for (let i = 0; i < seconds / T.C.DT; i++) T.step(sim);
  return sim;
}

// ------------------------------------------- the yardstick: LPGA as data
test('LPGA as data puts every node and every road exactly where the original page does', () => {
  const R = loadReferenceCore(), T = loadCore().load(readStudy('lpga.json'));
  for (const id in R.NODES) {
    assert.equal(T.NODES[id].x, R.NODES[id].x, id + '.x');
    assert.equal(T.NODES[id].y, R.NODES[id].y, id + '.y');
    same(T.NODES[id].groupA, R.NODES[id].groupA, id + ' main street');
  }
  for (const L of R.LINKS) {
    const l = T.LINKS.find(x => x.id === L.id);
    assert.equal(l.speed, L.speed, L.id + ' speed'); assert.equal(l.lanes, L.lanes, L.id + ' lanes');
  }
  same(T.EXISTING_POCKETS, R.EXISTING_POCKETS);
  assert.equal(T.BUDGET, R.BUDGET);
  for (const len of [0, 120, 333.3, 800]) assert.equal(T.COSTS.addLane(len), R.COSTS.addLane(len), 'lane cost at ' + len);
});

test('LPGA as data gives the same demand and the same v/c on every approach', () => {
  const R = loadReferenceCore(), T = loadCore().load(readStudy('lpga.json'));
  for (const mult of [1, 1.5]) {
    same(T.odMatrix(mult, { BUC: 1.4 }), R.odMatrix(mult, { BUC: 1.4 }));
    const a = T.createSim(T.emptyPlan(), { seed: 1, demandMult: mult }), b = R.createSim(R.emptyPlan(), { seed: 1, demandMult: mult });
    for (const d of b.net.dirList) assert.equal(a.net.dirs[d.id].vc, d.vc, d.id);
  }
});

test('LPGA as data drives car for car like the original, today and with a plan', () => {
  const R = loadReferenceCore(), T = loadCore().load(readStudy('lpga.json'));
  const plan = { links: { L2: { addLane: true } }, pockets: { 'R1:ab': { R: true } }, signals: { I3: { C: 150, split: 0.6 } }, roundabouts: { I4: true } };
  for (const p of [null, plan]) {
    const a = run(T, 400, 99, p && T.clonePlan(p)), b = run(R, 400, 99, p && R.clonePlan(p));
    assert.equal(a.veh.length, b.veh.length);
    assert.equal(a.m.done, b.m.done);
    assert.equal(a.m.lost, b.m.lost);
    same(a.veh.map(v => [v.id, v.pos, v.v]), b.veh.map(v => [v.id, v.pos, v.v]));
  }
});

test('LPGA as data scores a plan exactly as the original does', () => {
  const R = loadReferenceCore(), T = loadCore().load(readStudy('lpga.json'));
  const plan = { links: {}, pockets: { 'W1:ab': { R: true } }, signals: { I1: { split: 0.66 } }, roundabouts: {} };
  const opts = { warmup: 60, measure: 240, seeds: [777], demandMult: 1.25 };
  const ra = T.evaluateSync(T.emptyPlan(), opts), rb = R.evaluateSync(R.emptyPlan(), opts);
  const pa = T.evaluateSync(T.clonePlan(plan), opts), pb = R.evaluateSync(R.clonePlan(plan), opts);
  assert.equal(pa.avgDelay, pb.avgDelay);
  same(T.score(pa, ra, T.planCost(plan)), R.score(pb, rb, R.planCost(plan)));
  same(T.planItems(plan), R.planItems(plan));
});

// ---------------------------------------------------------- determinism
test('the same seed always gives the same run; another seed gives another', () => {
  const T = loadCore().load(readStudy('lpga.json'));
  const a = run(T, 300, 5), b = run(T, 300, 5), c = run(T, 300, 6);
  assert.equal(a.m.lost, b.m.lost);
  assert.notEqual(a.m.lost, c.m.lost);
});

// ------------------------------------------------------------- signals
test('a signal plan fills its cycle exactly, with yellow and all-red after every phase', () => {
  const T = loadCore().load(crossroads());
  for (const s of [{ C: 60, split: 0.5 }, { C: 90, split: 0.62, protA: true }, { C: 150, split: 0.4, protA: true, protB: true }]) {
    const sg = T.buildNetwork({ links: {}, pockets: {}, signals: { X: s }, roundabouts: {} }).nodes.X.sig;
    assert.equal(sg.cycle, s.C);
    assert.equal(sg.phases.length, 2 + (s.protA ? 1 : 0) + (s.protB ? 1 : 0));
  }
});

test('the main street and the side street are never green together', () => {
  const T = loadCore().load(crossroads());
  const sim = T.createSim(T.emptyPlan(), { seed: 1 }), X = sim.net.nodes.X;
  const main = X.inDirs.find(d => d.group === 'A'), side = X.inDirs.find(d => d.group === 'B');
  assert.ok(main && side);
  for (let t = 0; t < X.sig.cycle; t += 0.5) {
    const m = T.sigState(X, t, main, 'T'), s = T.sigState(X, t, side, 'T');
    assert.ok(!(m !== 'R' && s !== 'R'), 'both moving at t=' + t);
  }
});

test('with no main street named, the signal takes the widest pair of opposed roads', () => {
  const T = loadCore().load(crossroads());
  assert.deepEqual(T.NODES.X.groupA.slice().sort(), ['a', 'b']);
});

// ------------------------------------------------------- level of service
test('LOS from v/c and from delay use the published thresholds', () => {
  const T = loadCore();
  assert.deepEqual([0.6, 0.61, 0.7, 0.8, 0.9, 1.0, 1.01].map(T.losFromVC), ['A', 'B', 'B', 'C', 'D', 'E', 'F']);
  assert.deepEqual([10, 11, 20, 35, 55, 80, 81].map(T.losFromDelay), ['A', 'B', 'B', 'C', 'D', 'E', 'F']);
});

// ------------------------------------------------- other intersection shapes
test('a T intersection runs and every trip has a route', () => {
  const T = loadCore().load(study([sig('X', 0, 0), end('W', -300, 0), end('E', 300, 0), end('S', 0, 300)],
    [link('a', 'W', 'X', 2), link('b', 'X', 'E', 2), link('c', 'X', 'S')]));
  const sim = run(T, 600, 3);
  assert.equal(sim.analysis.unserved, 0);
  assert.ok(sim.m.done > 50, 'trips completed: ' + sim.m.done);
});

test('a skewed four-leg intersection runs and every trip has a route', () => {
  const T = loadCore().load(study([sig('X', 0, 0), end('W', -300, 40), end('E', 290, -60), end('N', 80, -300), end('S', -120, 280)],
    [link('a', 'W', 'X', 2), link('b', 'X', 'E', 2), link('c', 'N', 'X'), link('d', 'X', 'S')]));
  const sim = run(T, 600, 3);
  assert.equal(sim.analysis.unserved, 0);
  assert.ok(sim.m.done > 50, 'trips completed: ' + sim.m.done);
});

test('a roundabout that exists today runs as a roundabout and cannot be bought again', () => {
  const s = crossroads(); s.nodes[0] = { id: 'X', type: 'roundabout', name: 'X', x: 0, y: 0 };
  const T = loadCore().load(s);
  const sim = run(T, 600, 3);
  assert.equal(sim.net.nodes.X.ctrl, 'rab');
  assert.ok(sim.m.done > 50);
  assert.deepEqual(T.planItems(T.emptyPlan()), []);
});

// ------------------------------------------------------------- demand
test('typed volumes: each road end sends out exactly what was typed', () => {
  const s = crossroads({ demand: { mode: 'volumes' } });
  s.nodes.find(n => n.id === 'W').volume = 700; s.nodes.find(n => n.id === 'N').volume = 150;
  const T = loadCore().load(s);
  const od = T.odMatrix(1, {}), out = (z) => od.filter(p => p.o === z).reduce((t, p) => t + p.v, 0);
  assert.ok(Math.abs(out('W') - 700) < 1e-9);
  assert.ok(Math.abs(out('N') - 150) < 1e-9);
  assert.ok(Math.abs(T.odMatrix(1.5, { W: 2 }).filter(p => p.o === 'W').reduce((t, p) => t + p.v, 0) - 2100) < 1e-9);
});

test('a typed origin-destination table is used as it stands', () => {
  const T = loadCore().load(crossroads({ demand: { od: [{ o: 'W', d: 'E', v: 500 }, { o: 'N', d: 'S', v: 80 }] } }));
  assert.deepEqual(T.odMatrix(1, {}), [{ o: 'W', d: 'E', v: 500 }, { o: 'N', d: 'S', v: 80 }]);
});

// ------------------------------------------------------------ scoring
test('a plan over budget has its score halved', () => {
  const T = loadCore().load(crossroads({ budget: 1 }));
  const r = { avgDelay: 20, shareOK: 1, throughput: 1, unserved: 0 }, base = { avgDelay: 40 };
  // 50% less delay → 50 points (the full 50 come at 40%), LOS 25, throughput 10; no budget points over budget
  assert.equal(T.score(r, base, 1).total, 85);
  assert.equal(T.score(r, base, 2).total, Math.round(85 / 2));
});

// ------------------------------------------------- what a bad study says
test('a study that cannot run says why, in plain words', () => {
  const T = loadCore();
  const say = (s) => T.validate(s).map(e => e.message).join(' | ');
  const five = crossroads(); five.nodes.push(end('NE', 250, -250)); five.links.push(link('e', 'X', 'NE'));
  assert.match(say(five), /has 5 roads\. TrafficLab\+ handles intersections with 3 or 4 roads/);
  const twoRoads = crossroads(); twoRoads.links.push(link('f', 'W', 'N'));
  assert.match(say(twoRoads), /road end "W" must have exactly one road; it has 2/);
  assert.match(say(crossroads({ format: 99 })), /newer TrafficLab\+/);
  assert.match(say(crossroads({ pockets: { 'zz:ab': { L: 1 } } })), /turn lane "zz:ab"/);
  assert.match(say(crossroads({ budget: 0 })), /needs a budget/);
  const vol = crossroads({ demand: { mode: 'volumes' } }); delete vol.nodes[1].volume;
  assert.match(say(vol), /vehicles per hour enter at "W"/);
  assert.deepEqual(T.validate(crossroads()), []);
  assert.deepEqual(T.validate(readStudy('lpga.json')), []);
  assert.throws(() => T.load(five), /3 or 4 roads/);
});
