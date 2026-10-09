// Loads the simulation core straight out of Ron's original LPGA Traffic Lab page
// (reference/lpga-traffic-lab.html), untouched, so the generalised engine can be
// measured against it.
'use strict';
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const ROOT = path.resolve(__dirname, '..', '..');

function loadReferenceCore() {
  const html = fs.readFileSync(path.join(ROOT, 'reference', 'lpga-traffic-lab.html'), 'utf8');
  const start = html.indexOf('<script>') + '<script>'.length;
  const end = html.indexOf('</script>', start);
  const code = html.slice(start, end);
  const module = { exports: {} };
  vm.runInNewContext(code, { module, globalThis: {}, Math, Date, JSON, Promise, setTimeout, Map, Set, Infinity, Object, Array, Number, String });
  return module.exports;
}

function loadCore() {
  const file = path.join(ROOT, 'web', 'core', 'trafficlab-core.js');
  delete require.cache[require.resolve(file)];
  return require(file);
}

function readStudy(name) {
  return JSON.parse(fs.readFileSync(path.join(ROOT, 'samples', name), 'utf8'));
}

module.exports = { ROOT, loadReferenceCore, loadCore, readStudy };
