/* =====================================================================
   TRAFFICLAB+ — USER INTERFACE & RENDERING (from the LPGA Traffic Lab)
   ---------------------------------------------------------------------
   Everything here READS the simulation in trafficlab-core.js (window.TG,
   already loaded with the study) and draws it, or EDITS the player's plan
   and asks the core to rebuild. The page opens blank, as a puzzle: today's
   network and no plan. Submitting a plan makes the printout.
   Sections:  A. state   B. canvas view & theme   C. drawing
              D. hit-testing & input   E. inspector panels
              F. plan / budget   G. traffic test & scoring   H. main loop
   ===================================================================== */
(function () {
'use strict';
const C = TG.C;
const $ = (id) => document.getElementById(id);
const fmt$ = (m) => '$' + m.toFixed(2) + 'M';
const esc = (s) => String(s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

// ---------------------------------------------------------------- A. state
const S = {
  plan: TG.emptyPlan(),
  demand: 1.0,
  zoneMult: {},
  colorMode: 'vc',          // 'vc' (planning) | 'delay' (simulated)
  speed: 2, paused: false,
  sel: null,                // {type:'link'|'node', id}
  hover: null,
  name: '',
  runs: [], baseCache: {}, testing: false,
  sim: null
};
const ST = TG.STUDY;
// one intersection is an intersection, not a corridor (Ron, 2026-10-09)
const SINGLE = ST.nodes.filter(n => n.type !== 'end').length === 1;
const SCEN = ((ST.demand && ST.demand.scenarios) || [{ name: 'Today', mult: 1.0 }]).map(s => ({ k: s.name, v: s.mult }));
// US customary on the page (Ron, 2026-10-09); the study is in metres
const FT_PER_M = 3.28084;
// the page's words come from the study
document.title = ST.title;
$('brandShield').textContent = ST.short || 'TL+';
$('brandTitle').textContent = ST.title;
$('brandSub').textContent = ST.subtitle || `Fix this network on a ${fmt$(TG.BUDGET)} budget`;
$('introTitle').textContent = ST.title;
$('introText').textContent = (ST.intro ? ST.intro + ' ' : '') +
  'Cars follow a real car-following model, signals cycle through their phases, and each road is shaded by its Level of Service (LOS A–F).';
$('introBudget').textContent = fmt$(TG.BUDGET) + ' budget';
$('credit').textContent = ['Made with TrafficLab+', ST.author ? 'Study by ' + ST.author : '',
  ST.sources && ST.sources.osm ? 'Road data © OpenStreetMap contributors' : '',
  ST.sources && typeof ST.sources.counts === 'string' ? 'Traffic counts: ' + ST.sources.counts : ''].filter(Boolean).join(' · ');
function distText(m) { const ft = m * FT_PER_M; return ft < 1000 ? Math.round(ft / 10) * 10 + ' ft' : (ft / 5280).toFixed(2) + ' mi'; }

function newLiveSim() {
  S.sim = TG.createSim(S.plan, { seed: (Math.random() * 1e9) | 0, demandMult: S.demand, zoneMult: S.zoneMult });
  for (let i = 0; i < 900; i++) TG.step(S.sim);    // 90 s warm-up so the map never starts empty
}

// ---------------------------------------------------- B. canvas view & theme
const cv = $('map'), ctx = cv.getContext('2d');
// the drawing's extent in engine units: the study's own frame, or every node with a margin
const WORLD = (() => {
  if (ST.world) return { x: TG.toU(ST.world.x), y: TG.toU(ST.world.y), w: TG.toU(ST.world.w), h: TG.toU(ST.world.h) };
  const xs = Object.values(TG.NODES).map(n => n.x), ys = Object.values(TG.NODES).map(n => n.y), pad = 40;
  const x = Math.min(...xs) - pad, y = Math.min(...ys) - pad;
  return { x, y, w: Math.max(...xs) + pad - x, h: Math.max(...ys) + pad - y };
})();
const view = { s: 1, ox: 0, oy: 0, fit: true };
let theme = {};
function readTheme() {
  const cs = getComputedStyle(document.documentElement);
  for (const k of ['map-bg', 'map-block', 'map-local', 'pave', 'pave-edge', 'mark', 'label', 'halo', 'sign', 'amber', 'ink', 'muted',
    'los-a', 'los-b', 'los-c', 'los-d', 'los-e', 'los-f', 'font-d', 'font-m']) theme[k] = cs.getPropertyValue('--' + k).trim();
}
readTheme();
matchMedia('(prefers-color-scheme: dark)').addEventListener('change', readTheme);
new MutationObserver(readTheme).observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });

function resize() {
  const r = cv.getBoundingClientRect(), dpr = window.devicePixelRatio || 1;
  cv.width = Math.max(1, Math.round(r.width * dpr)); cv.height = Math.max(1, Math.round(r.height * dpr));
  if (view.fit) fitView();
}
function fitView() {
  const r = cv.getBoundingClientRect();
  view.s = Math.min(r.width / WORLD.w, r.height / WORLD.h) * 0.98;
  view.ox = (r.width - WORLD.w * view.s) / 2 - WORLD.x * view.s; view.oy = (r.height - WORLD.h * view.s) / 2 - WORLD.y * view.s; view.fit = true;
}
new ResizeObserver(resize).observe(cv);
const toWorld = (sx, sy) => ({ x: (sx - view.ox) / view.s, y: (sy - view.oy) / view.s });

// ---------------------------------------------------------------- C. drawing
const LOS_KEYS = { A: 'los-a', B: 'los-b', C: 'los-c', D: 'los-d', E: 'los-e', F: 'los-f' };
function dirLOS(d) {
  const net = S.sim.net;
  if (S.colorMode === 'vc') return TG.losFromVC(d.vc);
  if (net.nodes[d.to].ctrl === 'end') return null;
  const dd = TG.dirDelay(d); return dd == null ? null : TG.losFromDelay(dd);
}
function losColor(l) { return l ? theme[LOS_KEYS[l]] : theme['pave']; }

// the backdrop: decorative local streets (the blue "secondary" roads), blocks of land use,
// place names and freeways passing through — all from the study, in metres
const BD = ST.backdrop || {};
const U = (p) => [TG.toU(p[0]), TG.toU(p[1])];
const LOCAL = (BD.streets || []).map(l => l.map(U));
const BLOCKS = (BD.blocks || []).map(b => b.map(U));
const POIS = (BD.places || []).map(p => ({ x: TG.toU(p.x), y: TG.toU(p.y), t: p.text }));
const FREEWAYS = (BD.freeways || []).map(f => ({ pts: f.points.map(U), w: TG.toU(f.width || 18) }));

function draw(dtReal) {
  const dpr = window.devicePixelRatio || 1, W = cv.width / dpr, H = cv.height / dpr;
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  ctx.fillStyle = theme['map-bg']; ctx.fillRect(0, 0, W, H);
  ctx.setTransform(dpr * view.s, 0, 0, dpr * view.s, dpr * view.ox, dpr * view.oy);
  const net = S.sim.net, t = S.sim.t;

  // land use + local streets
  ctx.fillStyle = theme['map-block']; ctx.globalAlpha = 0.55;
  for (const b of BLOCKS) { ctx.beginPath(); b.forEach((p, i) => i ? ctx.lineTo(p[0], p[1]) : ctx.moveTo(p[0], p[1])); ctx.closePath(); ctx.fill(); }
  ctx.globalAlpha = 1; ctx.strokeStyle = theme['map-local']; ctx.lineWidth = 3; ctx.lineCap = 'round'; ctx.lineJoin = 'round';
  for (const l of LOCAL) { ctx.beginPath(); l.forEach((p, i) => i ? ctx.lineTo(p[0], p[1]) : ctx.moveTo(p[0], p[1])); ctx.stroke(); }

  // freeway mainlines (pass under the study's roads; not simulated)
  for (const f of FREEWAYS) {
    ctx.strokeStyle = theme['pave-edge']; ctx.lineWidth = f.w; ctx.lineCap = 'butt';
    ctx.beginPath(); f.pts.forEach((p, i) => i ? ctx.lineTo(p[0], p[1]) : ctx.moveTo(p[0], p[1])); ctx.stroke();
    ctx.strokeStyle = theme['pave']; ctx.lineWidth = f.w - 4; ctx.stroke();
    ctx.strokeStyle = theme['mark']; ctx.lineWidth = 1; ctx.setLineDash([8, 10]); ctx.stroke(); ctx.setLineDash([]);
  }

  // pavement: each direction from node center to node center
  for (const d of net.dirList) {
    const A = net.nodes[d.from], B = net.nodes[d.to];
    const o = d.med + d.n * C.LANE_W / 2, w = d.n * C.LANE_W + 2;
    ctx.strokeStyle = theme['pave-edge']; ctx.lineWidth = w + 2; ctx.lineCap = 'butt';
    seg(A.x + d.nx * o, A.y + d.ny * o, B.x + d.nx * o, B.y + d.ny * o);
  }
  for (const d of net.dirList) {
    const A = net.nodes[d.from], B = net.nodes[d.to];
    const o = d.med + d.n * C.LANE_W / 2, w = d.n * C.LANE_W + 2;
    ctx.strokeStyle = theme['pave']; ctx.lineWidth = w;
    seg(A.x + d.nx * o, A.y + d.ny * o, B.x + d.nx * o, B.y + d.ny * o);
    // median strip for two-way roads
    if (d.med > 0) { ctx.strokeStyle = theme['map-bg']; ctx.globalAlpha = 0.5; ctx.lineWidth = d.med * 2 - 2; seg(d.x0, d.y0, d.x0 + d.ux * d.len, d.y0 + d.uy * d.len); ctx.globalAlpha = 1; }
  }
  // intersections
  for (const id in net.nodes) {
    const n = net.nodes[id]; if (n.ctrl === 'end') continue;
    ctx.fillStyle = theme['pave'];
    ctx.beginPath(); ctx.arc(n.x, n.y, n.ctrl === 'rab' ? C.RAB_R + 7 : 22, 0, 7); ctx.fill();
    if (n.ctrl === 'rab') {
      ctx.fillStyle = theme['map-bg']; ctx.beginPath(); ctx.arc(n.x, n.y, C.RAB_R - 5, 0, 7); ctx.fill();
      ctx.fillStyle = theme['los-a']; ctx.globalAlpha = 0.35; ctx.beginPath(); ctx.arc(n.x, n.y, C.RAB_R - 7, 0, 7); ctx.fill(); ctx.globalAlpha = 1;
    }
  }
  // LOS shading over the lanes + pockets + markings
  const pulse = 0.55 + 0.25 * Math.sin(performance.now() / 260);
  for (const d of net.dirList) {
    const los = dirLOS(d), col = losColor(los);
    const ex = d.x0 + d.ux * d.len, ey = d.y0 + d.uy * d.len;
    ctx.strokeStyle = col; ctx.globalAlpha = los === 'F' ? pulse : 0.62;
    const o = d.med + d.n * C.LANE_W / 2;
    ctx.lineWidth = d.n * C.LANE_W - 0.6; seg(d.x0 + d.nx * o, d.y0 + d.ny * o, ex + d.nx * o, ey + d.ny * o);
    for (const p of [d.pocketL, d.pocketR]) if (p) {
      ctx.lineWidth = C.LANE_W - 0.6; ctx.globalAlpha = 1; ctx.strokeStyle = theme['pave'];
      lseg(p, p.start, p.len); ctx.globalAlpha = 0.62; ctx.strokeStyle = col; lseg(p, p.start, p.len);
    }
    ctx.globalAlpha = 1;
    // lane lines
    ctx.strokeStyle = theme['mark']; ctx.lineWidth = 0.6; ctx.setLineDash([4, 5]);
    for (let i = 1; i < d.n; i++) { const off = d.med + i * C.LANE_W; seg(d.x0 + d.nx * off, d.y0 + d.ny * off, ex + d.nx * off, ey + d.ny * off); }
    ctx.setLineDash([]);
    if (d.pocketL) { ctx.strokeStyle = theme['mark']; ctx.lineWidth = 0.7; const off = d.med; seg(d.x0 + d.ux * d.pocketL.start + d.nx * off, d.y0 + d.uy * d.pocketL.start + d.ny * off, ex + d.nx * off, ey + d.ny * off); }
    if (d.pocketR) { ctx.strokeStyle = theme['mark']; ctx.lineWidth = 0.7; const off = d.med + d.n * C.LANE_W; seg(d.x0 + d.ux * d.pocketR.start + d.nx * off, d.y0 + d.uy * d.pocketR.start + d.ny * off, ex + d.nx * off, ey + d.ny * off); }
    // stop bar
    if (net.nodes[d.to].ctrl !== 'end') {
      const a = d.med - (d.pocketL ? C.LANE_W : 0), b = d.med + (d.n + (d.pocketR ? 1 : 0)) * C.LANE_W;
      ctx.strokeStyle = theme['mark']; ctx.lineWidth = net.nodes[d.to].ctrl === 'rab' ? 0.8 : 1.4;
      if (net.nodes[d.to].ctrl === 'rab') ctx.setLineDash([1.5, 1.5]);
      seg(ex + d.nx * a, ey + d.ny * a, ex + d.nx * b, ey + d.ny * b); ctx.setLineDash([]);
    }
  }

  // signal heads: through/right ball on the right edge, left arrow on the left edge
  const blink = Math.floor(performance.now() / 450) % 2 === 0;
  for (const id in net.nodes) {
    const n = net.nodes[id]; if (n.ctrl !== 'sig') continue;
    for (const d of n.inDirs) {
      const ex = d.x0 + d.ux * (d.len + 2.5), ey = d.y0 + d.uy * (d.len + 2.5);
      const st = TG.sigState(n, t, d, 'T');
      const r = d.med + d.n * C.LANE_W + (d.pocketR ? C.LANE_W : 0) + 2.5;
      dot(ex + d.nx * r, ey + d.ny * r, 2.3, st === 'G' ? theme['los-a'] : st === 'Y' ? theme['amber'] : theme['los-f']);
      const sl = TG.sigState(n, t, d, 'L'), l = d.med - (d.pocketL ? C.LANE_W : 0) - 0.5;
      if (sl === 'P') { if (blink) arrow(ex + d.nx * l, ey + d.ny * l, d, theme['amber']); }   // flashing yellow arrow = permitted
      else arrow(ex + d.nx * l, ey + d.ny * l, d, sl === 'G' ? theme['los-a'] : sl === 'Y' ? theme['amber'] : theme['los-f']);
    }
  }

  // vehicles
  const k = Math.min(1, dtReal * 6);
  for (const v of S.sim.veh) drawCar(v, k);

  // entry backlogs (cars that cannot get onto the network)
  ctx.font = `700 10px ${theme['font-m']}`; ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
  for (const z in S.sim.backlog) {
    const q = S.sim.backlog[z].length; if (q < 3) continue;
    const n = net.nodes[z];
    const tx = Math.min(WORLD.x + WORLD.w - 15, Math.max(WORLD.x + 15, n.x)), ty = Math.min(WORLD.y + WORLD.h - 12, Math.max(WORLD.y + 12, n.y));
    ctx.fillStyle = theme['los-f']; roundRect(tx - 16, ty - 7, 32, 14, 7); ctx.fill();
    ctx.fillStyle = '#fff'; ctx.fillText('+' + q, tx, ty + 0.5);
  }

  // labels
  drawLabels(net);

  // selection outline
  if (S.sel || S.hover) for (const it of [S.hover, S.sel]) {
    if (!it) continue;
    ctx.strokeStyle = it === S.sel ? theme['amber'] : theme['ink']; ctx.globalAlpha = it === S.sel ? 0.9 : 0.35; ctx.lineWidth = 2.2; ctx.setLineDash(it === S.sel ? [] : [3, 3]);
    if (it.type === 'node') { const n = net.nodes[it.id]; ctx.beginPath(); ctx.arc(n.x, n.y, n.ctrl === 'end' ? 12 : 30, 0, 7); ctx.stroke(); }
    else { const L = TG.LINKS.find(l => l.id === it.id), A = net.nodes[L.a], B = net.nodes[L.b]; ctx.lineWidth = 34; ctx.globalAlpha *= 0.35; ctx.lineCap = 'round'; seg(A.x, A.y, B.x, B.y); }
    ctx.globalAlpha = 1; ctx.setLineDash([]);
  }
  // compass last: it switches the canvas to screen coordinates
  compassRose(dpr, W, H);
}
function compassRose(dpr, W, H) {
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  const cx = W - 44, cy = H - 168, r = 26;
  ctx.fillStyle = theme['halo']; ctx.strokeStyle = theme['pave-edge']; ctx.lineWidth = 1;
  ctx.beginPath(); ctx.arc(cx, cy, r + 9, 0, 7); ctx.fill(); ctx.stroke();
  // 8 thin ticks, 4 long points
  for (let i = 0; i < 8; i++) { const a = i * Math.PI / 4; ctx.beginPath(); ctx.moveTo(cx + Math.cos(a) * (r - 4), cy + Math.sin(a) * (r - 4)); ctx.lineTo(cx + Math.cos(a) * (r + 2), cy + Math.sin(a) * (r + 2)); ctx.stroke(); }
  const point = (a, len, fill) => { ctx.fillStyle = fill; ctx.beginPath(); ctx.moveTo(cx + Math.cos(a) * len, cy + Math.sin(a) * len);
    ctx.lineTo(cx + Math.cos(a + Math.PI / 2) * 4.5, cy + Math.sin(a + Math.PI / 2) * 4.5); ctx.lineTo(cx + Math.cos(a - Math.PI / 2) * 4.5, cy + Math.sin(a - Math.PI / 2) * 4.5); ctx.closePath(); ctx.fill(); };
  point(Math.PI / 2, r - 8, theme['muted']); point(0, r - 8, theme['muted']); point(Math.PI, r - 8, theme['muted']);
  point(-Math.PI / 2, r - 2, theme['los-f']);                                  // north needle
  ctx.font = `800 11px ${theme['font-d']}`; ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
  ctx.fillStyle = theme['ink'];
  ctx.fillText('N', cx, cy - r - 1); ctx.fillText('S', cx, cy + r + 2); ctx.fillText('E', cx + r + 2, cy + 1); ctx.fillText('W', cx - r - 2, cy + 1);
}
function seg(x0, y0, x1, y1) { ctx.beginPath(); ctx.moveTo(x0, y0); ctx.lineTo(x1, y1); ctx.stroke(); }
function lseg(lane, a, b) { const d = lane.dir; seg(d.x0 + d.ux * a + d.nx * lane.off, d.y0 + d.uy * a + d.ny * lane.off, d.x0 + d.ux * b + d.nx * lane.off, d.y0 + d.uy * b + d.ny * lane.off); }
function dot(x, y, r, c) { ctx.fillStyle = c; ctx.beginPath(); ctx.arc(x, y, r, 0, 7); ctx.fill(); }
function arrow(x, y, d, c) {
  // small left-pointing arrow (relative to travel direction)
  ctx.fillStyle = c; ctx.beginPath();
  const lx = -d.nx, ly = -d.ny;                // left of travel
  ctx.moveTo(x + lx * 2.6, y + ly * 2.6); ctx.lineTo(x - d.ux * 1.8 - lx * 1.2, y - d.uy * 1.8 - ly * 1.2); ctx.lineTo(x + d.ux * 1.8 - lx * 1.2, y + d.uy * 1.8 - ly * 1.2);
  ctx.closePath(); ctx.fill();
}
function roundRect(x, y, w, h, r) { ctx.beginPath(); ctx.moveTo(x + r, y); ctx.arcTo(x + w, y, x + w, y + h, r); ctx.arcTo(x + w, y + h, x, y + h, r); ctx.arcTo(x, y + h, x, y, r); ctx.arcTo(x, y, x + w, y, r); ctx.closePath(); }
const CAR_COLORS = ['#f4f5f2', '#2b3a4a', '#9aa3a8', '#7a2a2a', '#1f5f8b', '#c9c2b0', '#3d5a40'];
function carPose(v, k) {
  if (v.lane) {
    const d = v.lane.dir; if (v.lat == null) v.lat = v.lane.off; v.lat += (v.lane.off - v.lat) * k;
    return { x: d.x0 + d.ux * v.pos + d.nx * v.lat, y: d.y0 + d.uy * v.pos + d.ny * v.lat, hx: d.ux, hy: d.uy };
  }
  if (v.path) {
    const p = v.path; let i = 1; while (i < p.cum.length - 1 && p.cum[i] < v.pos) i++;
    const a = p.pts[i - 1], b = p.pts[i], f = Math.min(1, Math.max(0, (v.pos - p.cum[i - 1]) / Math.max(0.01, p.cum[i] - p.cum[i - 1])));
    const hx = b.x - a.x, hy = b.y - a.y, hl = Math.hypot(hx, hy) || 1;
    v.lat = null; return { x: a.x + (b.x - a.x) * f, y: a.y + (b.y - a.y) * f, hx: hx / hl, hy: hy / hl };
  }
  if (v.ring) {
    const n = v.ring, th = -v.s / C.RAB_R; v.lat = null;
    return { x: n.x + C.RAB_R * Math.cos(th), y: n.y + C.RAB_R * Math.sin(th), hx: Math.sin(th), hy: -Math.cos(th) };
  }
  return null;
}
function drawCar(v, k) {
  const p = carPose(v, k); if (!p) return;
  ctx.save(); ctx.translate(p.x, p.y); ctx.transform(p.hx, p.hy, -p.hy, p.hx, 0, 0);
  ctx.fillStyle = CAR_COLORS[v.id % CAR_COLORS.length];
  roundRect(-C.CAR_LEN + 0.6, -2.1, C.CAR_LEN - 1.2, 4.2, 1.2); ctx.fill();
  ctx.strokeStyle = 'rgba(0,0,0,.45)'; ctx.lineWidth = 0.4; ctx.stroke();
  if (v.v < 2) { ctx.fillStyle = '#ff2a2a'; ctx.fillRect(-C.CAR_LEN + 0.6, -2, 0.9, 1.1); ctx.fillRect(-C.CAR_LEN + 0.6, 0.9, 0.9, 1.1); }
  ctx.restore();
}
function drawLabels(net) {
  ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
  for (const L of TG.LINKS) {
    if (!L.label) continue;                                   // the study can leave a segment unlabelled
    const A = net.nodes[L.a], B = net.nodes[L.b];
    let ang = Math.atan2(B.y - A.y, B.x - A.x); if (ang > Math.PI / 2) ang -= Math.PI; if (ang < -Math.PI / 2) ang += Math.PI;
    const t = L.labelAt;
    const mx = A.x + (B.x - A.x) * t, my = A.y + (B.y - A.y) * t;
    const nx = -Math.sin(ang), ny = Math.cos(ang), off = -26;
    const big = L.lanes >= 2;                                 // arterials in bold, local roads lighter
    ctx.save(); ctx.translate(mx + nx * off, my + ny * off); ctx.rotate(ang);
    ctx.font = `${big ? 800 : 600} ${big ? 12 : 10}px ${theme['font-d']}`;
    ctx.lineWidth = 3.5; ctx.strokeStyle = theme['halo']; ctx.strokeText(L.name, 0, 0);
    ctx.fillStyle = theme['label']; ctx.fillText(L.name, 0, 0); ctx.restore();
  }
  ctx.font = `600 10px ${theme['font-d']}`;
  for (const p of POIS) {
    ctx.lineWidth = 3; ctx.strokeStyle = theme['halo']; ctx.strokeText(p.t, p.x, p.y);
    ctx.fillStyle = theme['muted']; ctx.fillText(p.t, p.x, p.y);
  }
}

// ---------------------------------------------- inflow controls on the map
const zoneBox = document.createElement('div'); zoneBox.className = 'zonectl-layer';
document.querySelector('.mapwrap').appendChild(zoneBox);
const zoneEls = {};
for (const z in TG.ZONES) {
  const el = document.createElement('div'); el.className = 'zonectl';
  el.innerHTML = `<button data-z="${z}" data-d="1" aria-label="More traffic entering from ${esc(TG.NODES[z].name)}">▲</button><output></output><button data-z="${z}" data-d="-1" aria-label="Less traffic entering from ${esc(TG.NODES[z].name)}">▼</button>`;
  el.querySelectorAll('button').forEach(b => b.onclick = (e) => { e.stopPropagation(); bumpZone(z, +b.dataset.d * 0.1); });
  zoneBox.appendChild(el); zoneEls[z] = el;
}
function bumpZone(z, delta) {
  const m = Math.round(Math.min(3, Math.max(0, (S.zoneMult[z] ?? 1) + delta)) * 10) / 10;
  if (m === 1) delete S.zoneMult[z]; else S.zoneMult[z] = m;
  S.sim.setDemand(S.demand, S.zoneMult); renderPlan();
  if (!S.sel || (S.sel.type === 'node' && S.sel.id === z)) renderInspector();
}
function placeZoneControls() {
  const net = S.sim.net, top = cv.offsetTop, cw = cv.clientWidth, ch = cv.clientHeight;
  for (const z in zoneEls) {
    const n = net.nodes[z], d = n.outDirs[0] || n.inDirs[0], el = zoneEls[z];
    const ux = d.from === z ? d.ux : -d.ux, uy = d.from === z ? d.uy : -d.uy;
    const wx = n.x + ux * 46 + (-uy) * 30, wy = n.y + uy * 46 + ux * 30;        // a little inside, beside the inbound lanes
    const sx = Math.min(cw - 22, Math.max(22, wx * view.s + view.ox)), sy = Math.min(ch - 34, Math.max(34, wy * view.s + view.oy));
    el.style.transform = `translate(${(sx - 17).toFixed(0)}px, ${(top + sy - 30).toFixed(0)}px)`;
    const m = S.zoneMult[z] ?? 1, out = el.querySelector('output');
    const vph = S.sim.od.filter(p => p.o === z).reduce((s, p) => s + p.v, 0);
    const txt = Math.round(m * 100) + '%'; if (out.textContent !== txt) out.textContent = txt;
    el.title = `${TG.NODES[z].name}: ${Math.round(vph)} veh/h entering`;
    el.classList.toggle('changed', m !== 1);
  }
}

// ------------------------------------------------- D. hit-testing & input
function hit(w) {
  const net = S.sim.net; let best = null, bd = 26;
  for (const id in net.nodes) { const n = net.nodes[id]; const d = Math.hypot(w.x - n.x, w.y - n.y); const lim = n.ctrl === 'end' ? 14 : 28; if (d < lim && d < bd) { bd = d; best = { type: 'node', id }; } }
  if (best) return best;
  bd = 14;
  for (const L of TG.LINKS) {
    const A = net.nodes[L.a], B = net.nodes[L.b];
    const dx = B.x - A.x, dy = B.y - A.y, t = Math.max(0, Math.min(1, ((w.x - A.x) * dx + (w.y - A.y) * dy) / (dx * dx + dy * dy)));
    const d = Math.hypot(w.x - (A.x + dx * t), w.y - (A.y + dy * t)); if (d < bd) { bd = d; best = { type: 'link', id: L.id }; }
  }
  return best;
}
let drag = null;
cv.addEventListener('pointerdown', e => { drag = { x: e.clientX, y: e.clientY, ox: view.ox, oy: view.oy, moved: false }; cv.setPointerCapture(e.pointerId); });
cv.addEventListener('pointermove', e => {
  const r = cv.getBoundingClientRect();
  if (drag) {
    const dx = e.clientX - drag.x, dy = e.clientY - drag.y;
    if (Math.abs(dx) + Math.abs(dy) > 4) drag.moved = true;
    if (drag.moved) { view.ox = drag.ox + dx; view.oy = drag.oy + dy; view.fit = false; cv.style.cursor = 'grabbing'; }
    return;
  }
  const w = toWorld(e.clientX - r.left, e.clientY - r.top), h = hit(w);
  S.hover = h; cv.style.cursor = h ? 'pointer' : 'grab';
  showTip(h, e.clientX - r.left, e.clientY - r.top);
});
cv.addEventListener('pointerup', e => {
  const r = cv.getBoundingClientRect();
  if (drag && !drag.moved) { const h = hit(toWorld(e.clientX - r.left, e.clientY - r.top)); S.sel = h; renderInspector(); }
  drag = null; cv.style.cursor = 'grab';
});
cv.addEventListener('pointerleave', () => { S.hover = null; $('tip').hidden = true; });
cv.addEventListener('wheel', e => { e.preventDefault(); const r = cv.getBoundingClientRect(); zoomAt(e.clientX - r.left, e.clientY - r.top, e.deltaY < 0 ? 1.15 : 1 / 1.15); }, { passive: false });
function zoomAt(sx, sy, f) {
  const w = toWorld(sx, sy); view.s = Math.min(6, Math.max(0.3, view.s * f));
  view.ox = sx - w.x * view.s; view.oy = sy - w.y * view.s; view.fit = false;
}
$('zin').onclick = () => { const r = cv.getBoundingClientRect(); zoomAt(r.width / 2, r.height / 2, 1.35); };
$('zout').onclick = () => { const r = cv.getBoundingClientRect(); zoomAt(r.width / 2, r.height / 2, 1 / 1.35); };
$('zfit').onclick = fitView;

function showTip(h, x, y) {
  const tip = $('tip');
  if (!h) { tip.hidden = true; return; }
  const net = S.sim.net; let html = '';
  if (h.type === 'node') {
    const n = net.nodes[h.id];
    html = `<b>${esc(n.name)}</b><br>` + (n.ctrl === 'end' ? 'Trips start and end here' : n.ctrl === 'rab' ? 'Roundabout' : `Signal · ${n.sig.cycle}s cycle`);
  } else {
    const L = TG.LINKS.find(l => l.id === h.id);
    html = `<b>${esc(L.name)}</b>`;
    for (const d of net.dirList.filter(d => d.link === L)) {
      const los = TG.losFromVC(d.vc), dd = TG.dirDelay(d);
      html += `<br>${d.heading} · ${Math.round(d.vol)} veh/h · v/c ${d.vc.toFixed(2)} <span class="los ${los}">${los}</span>` + (dd != null && net.nodes[d.to].ctrl !== 'end' ? ` · ${dd.toFixed(0)}s delay` : '');
    }
  }
  tip.innerHTML = html; tip.hidden = false;
  const r = cv.getBoundingClientRect();
  tip.style.left = Math.min(x + 14, r.width - 250) + 'px'; tip.style.top = (y + 60) + 'px';
}

// ------------------------------------------------------ E. inspector panels
function turnHeading(d, k) { const h = { EB: ['NB', 'SB'], WB: ['SB', 'NB'], NB: ['WB', 'EB'], SB: ['EB', 'WB'] }[d.heading]; return k === 'L' ? h[0] : h[1]; }
function approachName(d) { return `${d.link.name} ${d.heading}`; }
function losChip(l) { return l ? `<span class="los ${l}">${l}</span>` : '–'; }
function renderInspector() {
  const box = $('inspector'), net = S.sim.net;
  if (!S.sel) { box.innerHTML = overviewHTML(); bindOverview(); return; }
  if (S.sel.type === 'link') box.innerHTML = linkHTML(S.sel.id);
  else if (net.nodes[S.sel.id].ctrl === 'end') box.innerHTML = zoneHTML(S.sel.id);
  else box.innerHTML = nodeHTML(S.sel.id);
  box.querySelectorAll('[data-act]').forEach(el => el.addEventListener(el.type === 'range' ? 'input' : 'change', onAct));
  box.querySelectorAll('button[data-act]').forEach(el => el.addEventListener('click', onAct));
  const back = box.querySelector('#backBtn'); if (back) back.onclick = () => { S.sel = null; renderInspector(); };
}
function overviewHTML() {
  const net = S.sim.net;
  const rows = net.dirList.filter(d => net.nodes[d.to].ctrl !== 'end').map(d => ({ d, vc: d.vc, dl: TG.dirDelay(d) }))
    .sort((a, b) => S.colorMode === 'vc' ? b.vc - a.vc : (b.dl || 0) - (a.dl || 0)).slice(0, 6);
  const un = S.sim.analysis.unserved;
  return `<h2>${SINGLE ? 'Intersection' : 'Corridor'} overview</h2>
    <p class="hint">Click any road or intersection on the map to inspect it and buy improvements. Worst approaches right now:</p>
    ${un > 0 ? `<p class="warn">${Math.round(un)} veh/h cannot reach their destination with this plan.</p>` : ''}
    <table class="t"><tr><th>Approach</th><th>At</th><th style="text-align:right">v/c</th><th style="text-align:right">Delay</th></tr>
    ${rows.map(r => `<tr class="click" data-node="${r.d.to}"><td>${esc(approachName(r.d))}</td><td>${esc(TG.NODES[r.d.to].short.replace(' & ', '/'))}</td>
      <td class="n">${r.vc.toFixed(2)} ${losChip(TG.losFromVC(r.vc))}</td><td class="n">${r.dl == null ? '–' : r.dl.toFixed(0) + 's'}</td></tr>`).join('')}
    </table>
    <p class="sub">v/c is the planning estimate (volume ÷ capacity). Delay is measured from the simulated cars over the last 5 minutes.</p>`;
}
function bindOverview() { $('inspector').querySelectorAll('tr[data-node]').forEach(tr => tr.onclick = () => { S.sel = { type: 'node', id: tr.dataset.node }; renderInspector(); }); }

function linkHTML(id) {
  const L = TG.LINKS.find(l => l.id === id), net = S.sim.net, ov = S.plan.links[id] || {};
  const A = TG.NODES[L.a], B = TG.NODES[L.b];
  const len = Math.hypot(B.x - A.x, B.y - A.y), cost = TG.COSTS.addLane(len);
  const dirs = net.dirList.filter(d => d.link === L);
  const short = (n) => n.name.replace(/ \(.*\)/, '');
  return `<h2>Road segment <button class="btn small" id="backBtn">Overview</button></h2>
   <p class="insp-title">${esc(L.name)}</p>
   <p class="sub">${esc(short(A))} ↔ ${esc(short(B))} · ${distText(len * C.M_PER_PX)} · ${Math.round(L.speed * C.M_PER_PX * 2.237)} mph</p>
   <table class="t"><tr><th>Direction</th><th style="text-align:right">Lanes</th><th style="text-align:right">veh/h</th><th style="text-align:right">v/c</th><th style="text-align:right">Delay</th></tr>
   ${dirs.map(d => { const dd = TG.dirDelay(d); return `<tr><td>${d.heading} toward ${esc(short(TG.NODES[d.to]))}</td><td class="n">${d.n}</td><td class="n">${Math.round(d.vol)}</td><td class="n">${d.vc.toFixed(2)} ${losChip(TG.losFromVC(d.vc))}</td><td class="n">${dd == null || net.nodes[d.to].ctrl === 'end' ? '–' : dd.toFixed(0) + 's'}</td></tr>`; }).join('')}
   </table>
   <div class="opt"><input type="checkbox" id="o-lane" data-act="addLane" data-id="${id}" ${ov.addLane ? 'checked' : ''} ${L.lanes >= 3 ? 'disabled' : ''}>
     <label for="o-lane"><b>Add a through lane</b> in each direction<br><span class="sub">More capacity along the segment. Intersections still meter the flow.</span></label><span class="cost">${fmt$(cost)}</span></div>
   <div class="row"><label for="o-ow">Traffic flow</label>
     <select id="o-ow" data-act="oneWay" data-id="${id}">
       <option value="" ${!ov.oneWay ? 'selected' : ''}>Two-way (today)</option>
       <option value="ab" ${ov.oneWay === 'ab' ? 'selected' : ''}>One-way toward ${esc(short(B))}</option>
       <option value="ba" ${ov.oneWay === 'ba' ? 'selected' : ''}>One-way toward ${esc(short(A))}</option>
     </select><span class="cost sub">${fmt$(TG.COSTS.oneWay)}</span></div>
   <p class="sub">One-way conversion puts every lane in one direction. Trips that relied on the other direction must reroute, or they get stranded.</p>`;
}
function zoneHTML(id) {
  const n = TG.NODES[id], net = S.sim.net, m = S.zoneMult[id] ?? 1;
  const gen = S.sim.od.filter(p => p.o === id).reduce((s, p) => s + p.v, 0), att = S.sim.od.filter(p => p.d === id).reduce((s, p) => s + p.v, 0);
  return `<h2>Trip zone <button class="btn small" id="backBtn">Overview</button></h2>
   <p class="insp-title">${esc(n.name)}</p>
   <p class="sub">Cars enter and leave the model here. ${TG.DEMAND_MODE === 'volumes'
     ? 'The traffic entering here was counted or estimated for this study, and it is shared among the other road ends by how busy each one is.'
     : 'Trips between zones follow a gravity model: bigger zones exchange more trips.'}</p>
   <table class="t"><tr><td>Entering here</td><td class="n">${Math.round(gen)} veh/h</td></tr><tr><td>Leaving here</td><td class="n">${Math.round(att)} veh/h</td></tr>
   <tr><td>Cars waiting to enter</td><td class="n">${S.sim.backlog[id].length}</td></tr></table>
   <div class="row"><label for="z-m">Inflow</label><input type="range" id="z-m" min="0" max="300" step="10" value="${Math.round(m * 100)}" data-act="zone" data-id="${id}"><output id="z-mo">${Math.round(m * 100)}%</output></div>
   <p class="sub">Inflow is traffic entering the map here. Raise it to model an upstream change, such as a new subdivision or a holiday surge at a busy store. You can also use the ▲ ▼ controls beside each road edge on the map. Inflow changes are free: they change the problem, not your plan.</p>`;
}
// Outlined arrow like a pavement-marking sketch: travel in the approach direction,
// then turn. Drawn heading east, then rotated to the approach's real heading.
function turnGlyph(d, k) {
  const R = 'M4 12 L38 12 L38 32 L44 32 L34 42 L24 32 L30 32 L30 20 L4 20 Z';           // east, then right (south)
  const L = 'M4 36 L38 36 L38 16 L44 16 L34 6 L24 16 L30 16 L30 28 L4 28 Z';             // east, then left (north)
  const deg = Math.atan2(d.uy, d.ux) * 180 / Math.PI;
  return `<svg class="glyph" viewBox="0 0 48 48" width="34" height="34" aria-hidden="true"><g transform="rotate(${deg.toFixed(1)} 24 24)">
    <path d="${k === 'R' ? R : L}" fill="var(--panel)" stroke="var(--ink)" stroke-width="3.2" stroke-linejoin="miter"/></g></svg>`;
}
function nodeHTML(id) {
  const net = S.sim.net, node = net.nodes[id], N = TG.NODES[id];
  const s = TG.signalSettings(S.plan, id), built = N.type === 'roundabout', rab = built || !!S.plan.roundabouts[id];
  const ex = TG.EXISTING_POCKETS;
  const appr = node.inDirs.map(d => {
    const pk = S.plan.pockets[d.id] || {}, e = ex[d.id] || {}, dd = TG.dirDelay(d);
    const opt = (k, label, cost) => {
      const existing = !!e[k], on = existing || !!pk[k];
      return `<div class="opt ${existing ? 'disabled' : ''}"><input type="checkbox" id="pk-${d.id}-${k}" data-act="pocket" data-id="${d.id}" data-k="${k}" ${on ? 'checked' : ''} ${existing || rab ? 'disabled' : ''}>
        ${turnGlyph(d, k)}<label for="pk-${d.id}-${k}">${label}${existing ? ' <span class="sub">(exists today)</span>' : ''}</label><span class="cost">${existing ? '' : fmt$(cost)}</span></div>`;
    };
    return `<tr><td colspan="4" style="border-bottom:0;padding-bottom:0"><b>${esc(approachName(d))}</b> <span class="sub">${d.group === 'A' ? 'main street' : 'side street'}</span></td></tr>
      <tr><td class="sub">L ${Math.round(d.mv.L)} · T ${Math.round(d.mv.T)} · R ${Math.round(d.mv.R)}</td><td class="n">${d.vc.toFixed(2)} ${losChip(TG.losFromVC(d.vc))}</td><td class="n">${dd == null ? '–' : dd.toFixed(0) + 's'}</td></tr>
      ${rab ? '' : `<tr><td colspan="4"><div style="display:grid;gap:4px">${opt('L', `Add left-turn lane<br><span class="sub">${d.heading} → ${turnHeading(d, 'L')}</span>`, TG.COSTS.pocketL)}${opt('R', `Add right-turn lane<br><span class="sub">${d.heading} → ${turnHeading(d, 'R')}</span>`, TG.COSTS.pocketR)}</div></td></tr>`}`;
  }).join('');
  const sig = node.sig;
  return `<h2>Intersection <button class="btn small" id="backBtn">Overview</button></h2>
   <p class="insp-title">${esc(N.name)}</p>
   <p class="sub">${rab ? 'Single-lane roundabout (yield on entry)' : `Traffic signal · ${sig.cycle}s cycle · phases: ${sig.phases.map(p => p.grp === 'A' ? (p.leftOnly ? 'main lefts' : 'main') : (p.leftOnly ? 'side lefts' : 'side')).join(' → ')}`}</p>
   ${built ? '<p class="sub">This roundabout exists today. Change the roads that meet it, or the intersections around it.</p>' : `<div class="opt"><input type="checkbox" id="o-rab" data-act="rab" data-id="${id}" ${rab ? 'checked' : ''}>
     <label for="o-rab"><b>Convert to roundabout</b><br><span class="sub">No signal and no stops when gaps are available. Entry capacity drops as circulating traffic rises.</span></label><span class="cost">${fmt$(TG.COSTS.roundabout)}</span></div>`}
   ${rab ? '' : `
   <div class="row"><label for="s-c">Cycle length</label><select id="s-c" data-act="cycle" data-id="${id}">${[...new Set([60, 80, 90, 100, 120, 150, N.C])].sort((a, b) => a - b).map(c => `<option ${c === s.C ? 'selected' : ''} value="${c}">${c} s</option>`).join('')}</select></div>
   <p class="sub">Changing the cycle or the main street's share of green is a retiming: ${fmt$(TG.COSTS.retime)}, once.</p>
   <div class="row"><label for="s-s">Main-street green</label><input type="range" id="s-s" min="35" max="80" step="1" value="${Math.round(s.split * 100)}" data-act="split" data-id="${id}"><output id="s-so">${Math.round(s.split * 100)}%</output></div>
   <div class="opt"><input type="checkbox" id="s-pa" data-act="protA" data-id="${id}" ${s.protA ? 'checked' : ''}><label for="s-pa">Protected left phase, main street${N.protA ? ' <span class="sub">(today)</span>' : ''}</label><span class="cost">${N.protA ? '' : fmt$(TG.COSTS.protL)}</span></div>
   <div class="opt"><input type="checkbox" id="s-pb" data-act="protB" data-id="${id}" ${s.protB ? 'checked' : ''}><label for="s-pb">Protected left phase, side street${N.protB ? ' <span class="sub">(today)</span>' : ''}</label><span class="cost">${N.protB ? '' : fmt$(TG.COSTS.protL)}</span></div>`}
   <h2 style="margin-top:4px">Approaches <span>v/c · delay</span></h2>
   <table class="t">${appr}</table>
   ${built && !Object.keys(S.plan.pockets).some(k => node.inDirs.some(d => d.id === k)) ? '' : `<div class="row"><button class="btn small" data-act="resetNode" data-id="${id}">Reset this intersection</button></div>`}`;
}

let rebuildTimer = null;
function onAct(e) {
  const el = e.currentTarget, a = el.dataset.act, id = el.dataset.id, P = S.plan;
  const sigEntry = () => (P.signals[id] = P.signals[id] || {});
  switch (a) {
    case 'addLane': (P.links[id] = P.links[id] || {}).addLane = el.checked; tidy(); break;
    case 'oneWay': (P.links[id] = P.links[id] || {}).oneWay = el.value || undefined; tidy(); break;
    case 'pocket': { const p = (P.pockets[id] = P.pockets[id] || {}); p[el.dataset.k] = el.checked; tidy(); break; }
    case 'rab': P.roundabouts[id] = el.checked; if (el.checked) { delete P.signals[id]; for (const d of S.sim.net.nodes[id].inDirs) delete P.pockets[d.id]; } tidy(); break;
    case 'cycle': sigEntry().C = +el.value; tidy(); break;
    case 'split': sigEntry().split = +el.value / 100; $('s-so').textContent = el.value + '%'; tidy(); scheduleRebuild(true); return;
    case 'protA': sigEntry().protA = el.checked; tidy(); break;
    case 'protB': sigEntry().protB = el.checked; tidy(); break;
    case 'zone': S.zoneMult[id] = +el.value / 100; $('z-mo').textContent = el.value + '%'; S.sim.setDemand(S.demand, S.zoneMult); renderPlan(); return;
    case 'resetNode': delete P.signals[id]; delete P.roundabouts[id];
      for (const d of S.sim.net.nodes[id].inDirs) delete P.pockets[d.id]; tidy(); break;
  }
  scheduleRebuild(false);
}
// drop plan entries that equal "today" so the cost list stays honest
function tidy() {
  const P = S.plan;
  for (const id in P.links) { const o = P.links[id]; if (!o.addLane) delete o.addLane; if (!o.oneWay) delete o.oneWay; if (!Object.keys(o).length) delete P.links[id]; }
  for (const id in P.pockets) { const o = P.pockets[id]; for (const k of ['L', 'R']) if (!o[k]) delete o[k]; if (!Object.keys(o).length) delete P.pockets[id]; }
  for (const id in P.roundabouts) if (!P.roundabouts[id]) delete P.roundabouts[id];
  for (const id in P.signals) {
    const o = P.signals[id], N = TG.NODES[id];
    if (o.C === N.C) delete o.C; if (o.split != null && Math.abs(o.split - N.split) < 1e-6) delete o.split;
    if (o.protA != null && o.protA === !!N.protA) delete o.protA; if (o.protB != null && o.protB === !!N.protB) delete o.protB;
    if (!Object.keys(o).length) delete P.signals[id];
  }
}
function scheduleRebuild(slow) {
  clearTimeout(rebuildTimer);
  rebuildTimer = setTimeout(() => { newLiveSim(); renderInspector(); renderPlan(); toast(TG.planCost(S.plan) > TG.BUDGET ? `Over budget by ${fmt$(TG.planCost(S.plan) - TG.BUDGET)}: a plan over budget scores half.` : 'Plan applied. Traffic restarted on the new network.'); }, slow ? 350 : 30);
  renderPlan();
}
let toastT = null;
function toast(msg) { const t = $('toast'); t.textContent = msg; t.classList.add('on'); clearTimeout(toastT); toastT = setTimeout(() => t.classList.remove('on'), 1800); }

// ---------------------------------------------------- F. plan / budget
function renderPlan() {
  const items = TG.planItems(S.plan), cost = TG.planCost(S.plan);
  $('planList').innerHTML = items.length ? items.map(i => `<li><span>${esc(i.label)}</span><span class="cost">${fmt$(i.cost)}</span><button aria-label="Remove ${esc(i.label)}" data-key="${i.key}">×</button></li>`).join('')
    : '<li><span class="hint">No changes yet. Today\'s network is your baseline.</span></li>';
  $('planList').querySelectorAll('button[data-key]').forEach(b => b.onclick = () => removeItem(b.dataset.key));
  $('planCount').textContent = items.length ? items.length + (items.length === 1 ? ' change' : ' changes') : '';
  const pct = Math.min(100, cost / TG.BUDGET * 100);
  $('meter').querySelector('i').style.width = pct + '%'; $('meter').classList.toggle('over', cost > TG.BUDGET);
  $('spent').textContent = fmt$(cost) + ' spent';
  $('left').textContent = cost > TG.BUDGET ? fmt$(cost - TG.BUDGET) + ' over budget — scores half' : fmt$(TG.BUDGET - cost) + ' left';
  $('testDemand').textContent = 'at ' + Math.round(S.demand * 100) + '% demand';
}
function removeItem(key) {
  const [k, ...rest] = key.split(':'), id = rest.join(':'), P = S.plan;
  if (k === 'lane') delete (P.links[id] || {}).addLane;
  if (k === 'ow') delete (P.links[id] || {}).oneWay;
  if (k === 'pl') delete (P.pockets[id] || {}).L;
  if (k === 'pr') delete (P.pockets[id] || {}).R;
  if (k === 'rt') { delete (P.signals[id] || {}).C; delete (P.signals[id] || {}).split; }
  if (k === 'pa' || k === 'pr0') delete (P.signals[id] || {}).protA;
  if (k === 'pb' || k === 'pr0') delete (P.signals[id] || {}).protB;
  if (k === 'rb') delete P.roundabouts[id];
  tidy(); scheduleRebuild(false);
}
$('btnClear').onclick = () => {
  const n = TG.planItems(S.plan).length; if (!n) return;
  if (!confirm(`Remove all ${n} change${n === 1 ? '' : 's'} from your plan? Tests you have already run can still be loaded.`)) return;
  S.plan = TG.emptyPlan(); scheduleRebuild(false);
};
// leaving or reloading the page loses the plan: the browser asks first
window.addEventListener('beforeunload', e => { if (TG.planItems(S.plan).length) { e.preventDefault(); e.returnValue = ''; } });

// ------------------------------------------- G. traffic test & scoring
// Runs today's network (cached per demand) and the current plan, records the run, returns it.
async function runTest() {
  if (S.testing) return null; S.testing = true;
  const btn = $('btnTest'), sub = $('btnSubmit'), prog = $('prog'), bar = prog.querySelector('i');
  btn.disabled = true; sub.disabled = true; prog.hidden = false;
  // everything the run is filed under is taken now, at the start: the page stays usable while it runs
  const startDemand = S.demand, startZone = { ...S.zoneMult }, startKey = runKey();
  const opts = { demandMult: startDemand, zoneMult: { ...startZone } };
  const key = JSON.stringify(opts);
  const plan = TG.clonePlan(S.plan), cost = TG.planCost(plan);
  let base = S.baseCache[key];
  const both = !base;
  if (!base) { btn.textContent = 'Testing today\'s network…'; base = await TG.evaluate(TG.emptyPlan(), opts, f => bar.style.width = (f * 50) + '%'); S.baseCache[key] = base; }
  btn.textContent = 'Testing your plan…';
  const res = await TG.evaluate(plan, opts, f => bar.style.width = ((both ? 50 : 0) + f * (both ? 50 : 100)) + '%');
  const sc = TG.score(res, base, cost);
  const run = { n: S.runs.length + 1, demand: startDemand, zone: startZone, plan, cost, res, base, sc, items: TG.planItems(plan), key: startKey };
  S.runs.push(run);
  btn.textContent = 'Run traffic test'; btn.disabled = false; sub.disabled = false; prog.hidden = true; bar.style.width = '0';
  S.testing = false;
  renderRuns();
  if (runKey() !== startKey) toast('You changed the plan or the demand during the test. Those changes are not in this test.');
  return run;
}
// what makes two tests the same test: the plan, the demand and the inflow changes
function runKey() { return JSON.stringify([S.plan, S.demand, S.zoneMult]); }
$('btnTest').onclick = runTest;

function renderRuns() {
  const last = S.runs[S.runs.length - 1];
  if (!last) { $('scoreBox').innerHTML = ''; $('runs').innerHTML = ''; return; }
  const r = last.res, b = last.base, p = last.sc.parts;
  $('scoreBox').innerHTML = `<div class="scorebig"><b>${last.sc.total}</b><span>/ 100 · Test ${last.n}</span></div>
    <div class="parts">
      <span>Delay per trip: ${r.avgDelay.toFixed(0)}s vs ${b.avgDelay.toFixed(0)}s today (${last.sc.red >= 0 ? '−' : '+'}${Math.abs(last.sc.red * 100).toFixed(0)}%)</span><span>${p.pDelay.toFixed(1)} / 50</span>
      <span>Approaches at LOS D or better: ${(r.shareOK * 100).toFixed(0)}%</span><span>${p.pLOS.toFixed(1)} / 25</span>
      <span>Budget left: ${fmt$(Math.max(0, TG.BUDGET - last.cost))}</span><span>${p.pBudget.toFixed(1)} / 15</span>
      <span>Trips completed vs demanded: ${(r.throughput * 100).toFixed(0)}%</span><span>${p.pThru.toFixed(1)} / 10</span>
    </div>
    ${r.unserved > 0 ? `<p class="warn">${Math.round(r.unserved)} veh/h are stranded with no route. Large penalty applied.</p>` : ''}
    ${last.cost > TG.BUDGET ? '<p class="warn">Over budget: score halved.</p>' : ''}`;
  const best = S.runs.reduce((a, x) => x.sc.total > a.sc.total ? x : a, S.runs[0]);
  $('runs').innerHTML = `<caption class="sub" style="text-align:left;caption-side:top;padding-bottom:4px">Your tests — the best so far is highlighted. <b>Load</b> puts that test's plan, demand and inflows back on the map.</caption><tr><th>#</th><th style="text-align:right">Demand</th><th style="text-align:right">Delay</th><th style="text-align:right">Cost</th><th style="text-align:right">Score</th><th></th></tr>` +
    S.runs.slice().reverse().map(x => `<tr class="${x === best ? 'best' : ''}"><td>${x.n}</td><td class="n">${Math.round(x.demand * 100)}%</td><td class="n">${x.res.avgDelay.toFixed(0)}s</td><td class="n">${fmt$(x.cost)}</td><td class="n"><b>${x.sc.total}</b></td>
      <td><button class="btn small" data-load="${x.n}">Load</button></td></tr>`).join('');
  $('runs').querySelectorAll('button[data-load]').forEach(b => b.onclick = () => {
    const x = S.runs.find(r => r.n === +b.dataset.load); S.plan = TG.clonePlan(x.plan); S.zoneMult = { ...x.zone }; setDemand(x.demand); scheduleRebuild(false);
  });
}

// Submit: the plan as it stands now becomes the printout. If it has not been tested
// exactly as it stands (plan, demand, inflows), it is tested first.
async function submitPlan() {
  let run = S.runs.length && S.runs[S.runs.length - 1].key === runKey() ? S.runs[S.runs.length - 1] : null;
  if (!run) { toast('Testing your plan before it is submitted…'); run = await runTest(); }
  if (run) showPrintout(run);
}
$('btnSubmit').onclick = submitPlan;

function printoutHTML(run) {
  const now = new Date(), r = run.res, b = run.base, p = run.sc.parts;
  const byId = {}; for (const a of r.approaches) byId[a.id] = a;
  const delayTxt = (d) => d == null ? '–' : d.toFixed(0) + ' s';
  const appr = b.approaches.map(a => {
    const x = byId[a.id];
    return `<tr><td>${esc(a.label)}</td><td>${esc(a.at)}</td><td class="n">${a.vc.toFixed(2)} ${losChip(a.los)}</td>
      <td class="n">${x ? x.vc.toFixed(2) + ' ' + losChip(x.los) : '<span class="sub">removed</span>'}</td><td class="n">${delayTxt(a.delay)}</td><td class="n">${x ? delayTxt(x.delay) : '–'}</td></tr>`;
  }).join('');
  const by = [ST.author ? 'Study by ' + ST.author : '', ST.course || '', ST.place || ''].filter(Boolean).join(' · ');
  return `<div class="hdr"><b>${esc(ST.title)} · Submitted plan</b><span>${S.name ? esc(S.name) : 'Name not given'}</span></div>
    <p class="sub">${by ? esc(by) + '<br>' : ''}${now.toLocaleDateString()} ${now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })} ·
      tested at ${Math.round(run.demand * 100)}% demand${(s => s ? ' (' + esc(s.k) + ')' : '')(SCEN.find(s => Math.abs(s.v - run.demand) < 0.001))}${Object.keys(run.zone).length ? ', with inflow changes' : ''} · 3 seeded 15-minute runs, each against today's network</p>
    <div class="scorebig"><b>${run.sc.total}</b><span>/ 100</span></div>
    <div class="parts">
      <span>Delay per trip (${run.sc.red >= 0 ? '−' : '+'}${Math.abs(run.sc.red * 100).toFixed(0)}% against today)</span><span>${p.pDelay.toFixed(1)} / 50</span>
      <span>Approaches at LOS D or better</span><span>${p.pLOS.toFixed(1)} / 25</span>
      <span>Budget left</span><span>${p.pBudget.toFixed(1)} / 15</span>
      <span>Trips completed vs demanded</span><span>${p.pThru.toFixed(1)} / 10</span>
    </div>
    ${r.unserved > 0 ? `<p class="warn">${Math.round(r.unserved)} veh/h are stranded with no route. Large penalty applied.</p>` : ''}
    ${run.cost > TG.BUDGET ? '<p class="warn">Over budget: score halved.</p>' : ''}
    <div><b>Plan — ${fmt$(run.cost)} of the ${fmt$(TG.BUDGET)} budget</b>
      <ul class="plan">${run.items.length ? run.items.map(i => `<li><span>${esc(i.label)}</span><span class="cost">${fmt$(i.cost)}</span></li>`).join('') : '<li><span>No changes: today\'s network as it is</span></li>'}</ul></div>
    <table class="t">
      <tr><th></th><th style="text-align:right">Today</th><th style="text-align:right">With the plan</th></tr>
      <tr><td>Average delay per trip</td><td class="n">${b.avgDelay.toFixed(0)} s</td><td class="n">${r.avgDelay.toFixed(0)} s</td></tr>
      <tr><td>Trips completed vs demanded</td><td class="n">${(b.throughput * 100).toFixed(0)}%</td><td class="n">${(r.throughput * 100).toFixed(0)}%</td></tr>
      <tr><td>Approaches at LOS D or better</td><td class="n">${(b.shareOK * 100).toFixed(0)}%</td><td class="n">${(r.shareOK * 100).toFixed(0)}%</td></tr>
      <tr><td>Worst approach (v/c)</td><td class="n">${b.worstVC.toFixed(2)} ${losChip(b.worstLOS)}</td><td class="n">${r.worstVC.toFixed(2)} ${losChip(r.worstLOS)}</td></tr>
      <tr><td>Vehicle-hours of delay (15 min)</td><td class="n">${b.vehHrsLost.toFixed(1)}</td><td class="n">${r.vehHrsLost.toFixed(1)}</td></tr>
    </table>
    <div><b>Every approach</b> <span class="sub">v/c with its LOS, and the delay measured from the simulated cars</span>
    <table class="t"><tr><th>Approach</th><th>At</th><th style="text-align:right">v/c today</th><th style="text-align:right">v/c plan</th><th style="text-align:right">Delay today</th><th style="text-align:right">Delay plan</th></tr>${appr}</table></div>
    <p class="sub">Made with TrafficLab+. A teaching model: planning-level v/c and a simulated ${SINGLE ? 'intersection' : 'corridor'}, not an engineering analysis.${ST.sources && ST.sources.osm ? ' Road data © OpenStreetMap contributors.' : ''}${ST.sources && typeof ST.sources.counts === 'string' ? ' Traffic counts: ' + esc(ST.sources.counts) + '.' : ''}</p>`;
}
let printed = null;   // the run on the printout now
function showPrintout(run) {
  printed = run;
  $('printName').value = S.name;
  $('reportBody').innerHTML = printoutHTML(run);
  $('reportDlg').showModal();
}
// Printing copies the printout out of the dialog, so only it reaches the paper.
$('printName').addEventListener('input', () => { S.name = $('printName').value.trim(); renderWho(); if (printed) $('reportBody').innerHTML = printoutHTML(printed); });
$('btnPrint').onclick = () => window.print();
// whatever starts the printing (the button, Ctrl+P, the browser's menu), the paper gets the printout
window.addEventListener('beforeprint', () => {
  $('printArea').innerHTML = printed ? printoutHTML(printed)
    : '<p>Nothing to print yet. Press <b>Submit plan</b> on the page to make your printout.</p>';
});
$('btnCloseReport').onclick = () => $('reportDlg').close();

// ------------------------------------------------------ toolbar & intro
function setDemand(v) {
  S.demand = v; $('demand').value = Math.round(v * 100); $('demandOut').textContent = Math.round(v * 100) + '%';
  S.sim.setDemand(S.demand, S.zoneMult); renderScen(); renderPlan(); if (!S.sel) renderInspector();
}
$('demand').addEventListener('input', e => setDemand(+e.target.value / 100));
function renderScen() {
  $('scenseg').innerHTML = SCEN.map(s => `<button aria-pressed="${Math.abs(S.demand - s.v) < 0.001}" data-v="${s.v}" title="${Math.round(s.v * 100)}% demand">${s.k}</button>`).join('');
  $('scenseg').querySelectorAll('button').forEach(b => b.onclick = () => setDemand(+b.dataset.v));
}
function renderSpeed() {
  $('speedseg').innerHTML = [1, 2, 4, 8, 16].map(s => `<button aria-pressed="${S.speed === s}" data-s="${s}">${s}×</button>`).join('');
  $('speedseg').querySelectorAll('button').forEach(b => b.onclick = () => { S.speed = +b.dataset.s; renderSpeed(); });
}
function renderColor() {
  $('colorseg').innerHTML = `<button aria-pressed="${S.colorMode === 'vc'}" data-m="vc" title="Planning estimate: volume ÷ capacity">v/c</button><button aria-pressed="${S.colorMode === 'delay'}" data-m="delay" title="Measured from simulated cars">Delay</button>`;
  $('colorseg').querySelectorAll('button').forEach(b => b.onclick = () => { S.colorMode = b.dataset.m; renderColor(); renderLegend(); if (!S.sel) renderInspector(); });
}
function renderLegend() {
  const lab = S.colorMode === 'vc' ? ['≤.60', '.70', '.80', '.90', '1.0', '>1'] : ['≤10s', '20', '35', '55', '80', '>80'];
  $('legend').innerHTML = `<span class="lt" title="Level of Service: A flows freely, F is jammed">LOS · ${S.colorMode === 'vc' ? 'v/c' : 'delay'}<br><span style="text-transform:none;letter-spacing:0">A free · F jammed</span></span>` + 'ABCDEF'.split('').map((l, i) =>
    `<span style="display:inline-flex;flex-direction:column;align-items:center;gap:1px"><span class="sw" style="background:var(--los-${l.toLowerCase()})"></span><span class="ln">${l}</span><span class="ln" style="color:var(--muted)">${lab[i]}</span></span>`).join('');
}
$('btnPlay').onclick = () => { S.paused = !S.paused; $('btnPlay').textContent = S.paused ? '▶' : '❚❚'; $('btnPlay').setAttribute('aria-label', S.paused ? 'Play' : 'Pause'); };
function renderWho() {
  $('who').innerHTML = `<button class="btn small" id="btnName" title="Add or change the name printed on your printout">${S.name ? 'Name: <b>' + esc(S.name) + '</b>' : 'Add your name (optional)'}</button>
    <button class="btn small" id="btnHow" title="Show the instructions again">How it works</button>`;
  $('btnName').onclick = () => { $('nameIn').value = S.name; $('btnStart').textContent = 'Back to the map'; $('intro').showModal(); $('nameIn').focus(); };
  $('btnHow').onclick = () => { $('nameIn').value = S.name; $('btnStart').textContent = 'Back to the map'; $('intro').showModal(); };
}
$('btnStart').onclick = () => { S.name = $('nameIn').value.trim(); renderWho(); $('intro').close(); };   // the name is optional: blank still plays
$('intro').addEventListener('cancel', () => { S.name = $('nameIn').value.trim(); renderWho(); });
$('nameIn').addEventListener('keydown', e => { if (e.key === 'Enter') $('btnStart').click(); });

// ------------------------------------------------------------ H. main loop
newLiveSim();
renderSpeed(); renderScen(); renderColor(); renderLegend(); renderPlan(); renderInspector(); renderWho();
// TrafficLab+ rebuilds its preview after every edit and opens it with ?nointro, so the author is
// not shown the instructions again each time; a visitor never has it, and How it works opens them.
if (!/[?&]nointro(&|$)/.test(location.search)) try { $('intro').showModal(); } catch (e) { /* dialogs unsupported: skip intro */ }
let lastT = performance.now(), acc = 0, uiT = 0;
function frame(now) {
  const dt = Math.min(0.1, (now - lastT) / 1000); lastT = now;
  if (!S.paused) {
    acc += dt * S.speed; let n = 0;
    while (acc >= C.DT && n < 200) { TG.step(S.sim); acc -= C.DT; n++; }
    if (n >= 200) acc = 0;
  }
  draw(dt);
  placeZoneControls();
  const tt = Math.floor(S.sim.t); $('clock').textContent = `sim ${String(Math.floor(tt / 60)).padStart(2, '0')}:${String(tt % 60).padStart(2, '0')} · ${S.sim.veh.length} cars`;
  uiT += dt; if (uiT > 1.5) { uiT = 0; refreshInspectorNumbers(); }
  requestAnimationFrame(frame);
}
// re-render the inspector occasionally so measured delays stay live (skip while the user is dragging a slider)
function refreshInspectorNumbers() {
  const a = document.activeElement;
  if (a && $('inspector').contains(a) && (a.tagName === 'SELECT' || a.type === 'range')) return;
  const id = a && $('inspector').contains(a) ? a.id : null;
  renderInspector();
  if (id && $(id)) $(id).focus({ preventScroll: true });
}
requestAnimationFrame(frame);
})();
