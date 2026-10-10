/*
 * Plays a built TrafficLab+ page in headless Chrome, the way a visitor would, and prints what
 * happened as one line of JSON.
 *
 *   node tools/play-page.js <page.html> [name|-] [shot.png]
 *
 * It opens the page, types the name into the intro (or leaves it blank for "-"), presses Start,
 * opens the worst intersection from the overview, buys the first turn lane offered, presses
 * Submit plan, waits for the test to finish, and reads the printout. Along the way it records
 * every request the page makes to anything but itself (there must be none: a page needs nothing)
 * and every error the page throws.
 *
 * Environment: TL_WIDTH=390 for a phone, TL_DARK=1 for dark mode, TL_PRINT=<file.pdf> to print
 * the printout to a PDF as the browser would.
 *
 * Needs Chrome or Edge. A developer and test tool, not part of the program. Modelled on
 * ..\GamifyPlus\tools\play-game.js.
 */
'use strict';

const { spawn } = require('node:child_process');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');

const BROWSERS = [
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe',
  process.env.LOCALAPPDATA + '/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
  'C:/Program Files/Microsoft/Edge/Application/msedge.exe',
];

function findBrowser() {
  for (const candidate of BROWSERS) if (candidate && fs.existsSync(candidate)) return candidate;
  throw new Error('No Chrome or Edge found. This tool needs one of them.');
}

async function waitFor(check, whatFor, timeoutMs = 20000) {
  const until = Date.now() + timeoutMs;
  while (Date.now() < until) {
    const result = await check();
    if (result) return result;
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  throw new Error('Timed out waiting for ' + whatFor);
}

// The visit, as one expression evaluated inside the page.
function visitScript(name) {
  return `(async () => {
    const sleep = ms => new Promise(r => setTimeout(r, ms));
    const $ = id => document.getElementById(id);
    const out = { title: document.title, introOpen: $('intro').open };
    $('nameIn').value = ${JSON.stringify(name)};
    $('btnStart').click();
    await sleep(300);
    out.introClosed = !$('intro').open;
    out.who = $('who').textContent;
    out.footer = $('credit').innerText;
    out.footerLinks = [...$('credit').querySelectorAll('a')].map(a => a.href);
    const row = document.querySelector('#inspector tr[data-node]');
    out.overviewRows = document.querySelectorAll('#inspector tr[data-node]').length;
    if (row) { row.click(); await sleep(200); }
    const buy = [...document.querySelectorAll('#inspector input[data-act="pocket"]')].find(b => !b.checked && !b.disabled);
    if (buy) { buy.checked = true; buy.dispatchEvent(new Event('change')); await sleep(400); }
    out.planItems = document.querySelectorAll('#planList button[data-key]').length;
    $('btnSubmit').click();
    const until = Date.now() + 120000;
    while (!$('reportDlg').open && Date.now() < until) await sleep(200);
    out.printoutOpen = $('reportDlg').open;
    out.printout = $('reportBody').innerText;
    out.score = +(document.querySelector('#reportBody .scorebig b') || {}).textContent;
    out.approachRows = document.querySelectorAll('#reportBody table')[1] ? document.querySelectorAll('#reportBody table')[1].rows.length - 1 : 0;
    window.dispatchEvent(new Event('beforeprint'));   // what printing does first, without a print dialog
    out.printArea = $('printArea').innerText.slice(0, 80);
    return out;
  })()`;
}

async function main() {
  const [file, nameArg = '-', shot] = process.argv.slice(2);
  if (!file) throw new Error('Give the path of a built page.');
  const name = nameArg === '-' ? '' : nameArg;
  const pageUrl = 'file:///' + path.resolve(file).replace(/\\/g, '/');

  const port = 9222 + Math.floor(Math.random() * 500);
  const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'tl-chrome-'));
  const chrome = spawn(findBrowser(), [
    '--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check',
    '--remote-debugging-port=' + port, '--user-data-dir=' + profile, 'about:blank',
  ], { stdio: 'ignore' });

  let socket;
  try {
    const target = await waitFor(async () => {
      try {
        const list = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json();
        return list.find(t => t.type === 'page' && t.webSocketDebuggerUrl);
      } catch { return null; }
    }, 'the browser to start');

    socket = new WebSocket(target.webSocketDebuggerUrl);
    await new Promise((resolve, reject) => { socket.onopen = resolve; socket.onerror = () => reject(new Error('Could not talk to the browser.')); });

    let nextId = 1;
    const waiting = new Map(), requests = [], errors = [];
    socket.onmessage = event => {
      const m = JSON.parse(event.data);
      if (waiting.has(m.id)) { waiting.get(m.id)(m); waiting.delete(m.id); return; }
      if (m.method === 'Network.requestWillBeSent') {
        const u = m.params.request.url;
        if (!u.startsWith('data:') && u !== pageUrl && !u.startsWith('about:')) requests.push(u);
      }
      if (m.method === 'Runtime.exceptionThrown') errors.push(m.params.exceptionDetails.exception?.description || m.params.exceptionDetails.text);
      if (m.method === 'Log.entryAdded' && m.params.entry.level === 'error') errors.push(m.params.entry.text);
    };
    const send = (method, params) => new Promise((resolve, reject) => {
      const id = nextId++;
      waiting.set(id, m => m.error ? reject(new Error(m.error.message)) : resolve(m.result));
      socket.send(JSON.stringify({ id, method, params }));
    });
    const evaluate = async expression => {
      const r = await send('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true });
      if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description ?? 'page error');
      return r.result.value;
    };

    await send('Network.enable', {});
    await send('Runtime.enable', {});
    await send('Log.enable', {});
    await send('Page.enable', {});
    const width = Number(process.env.TL_WIDTH || 1440);
    await send('Emulation.setDeviceMetricsOverride', { width, height: width < 600 ? 1600 : 900, deviceScaleFactor: 1, mobile: width < 600 });
    if (process.env.TL_DARK) await send('Emulation.setEmulatedMedia', { features: [{ name: 'prefers-color-scheme', value: 'dark' }] });
    await send('Page.navigate', { url: pageUrl });

    await waitFor(() => evaluate('!!(window.TG && document.getElementById("intro"))'), 'the page to load');
    await new Promise(r => setTimeout(r, 500));
    const result = await evaluate(visitScript(name));
    result.sideways = await evaluate('document.documentElement.scrollWidth > window.innerWidth');

    if (shot) {
      await evaluate("document.getElementById('reportDlg').close()");
      await new Promise(r => setTimeout(r, 600));
      const png = await send('Page.captureScreenshot', {});
      fs.writeFileSync(shot, Buffer.from(png.data, 'base64'));
    }
    if (process.env.TL_PRINT) {
      const pdf = await send('Page.printToPDF', { printBackground: true });
      fs.writeFileSync(process.env.TL_PRINT, Buffer.from(pdf.data, 'base64'));
    }

    result.requests = requests;
    result.errors = errors;
    console.log(JSON.stringify(result));
  } finally {
    if (socket) socket.close();
    chrome.kill();
    await new Promise(resolve => setTimeout(resolve, 400));
    try { fs.rmSync(profile, { recursive: true, force: true }); } catch { /* Windows still had a handle open. */ }
  }
}

main().catch(error => { console.error(error.message); process.exitCode = 1; });
