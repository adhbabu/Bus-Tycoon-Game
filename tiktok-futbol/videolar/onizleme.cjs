// Hızlı kontrol: videonun birkaç anını tek kare olarak çeker ve yan yana bir kontrol görseli yapar.
const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');
const { chromium } = require('playwright');

async function preview(name, html, tl, marks) {
  const dir = path.join(__dirname, 'cikti');
  fs.mkdirSync(dir, { recursive: true });
  const file = path.join(dir, name + '-onizleme.html');
  fs.writeFileSync(file, html + `<script>${tl.steps.filter((s) => s.run).map((s) => `at(${s.start}, () => { ${s.run} });`).join('\n')}</script></body></html>`);
  const b = await chromium.launch();
  const p = await (await b.newContext({ viewport: { width: 1080, height: 1920 } })).newPage();
  const errs = [];
  p.on('pageerror', (e) => errs.push(e.message));
  await p.clock.install({ time: 0 });
  await p.clock.pauseAt(1000);
  await p.goto('file://' + file);
  await p.evaluate(() => document.fonts.ready);
  let now = 0;
  const shots = [];
  for (const [k, m] of marks.entries()) {
    await p.clock.runFor(m - now);
    now = m;
    await p.waitForTimeout(700);
    const f = path.join(dir, `${name}-on-${k}.jpg`);
    await p.screenshot({ path: f, type: 'jpeg', quality: 70 });
    shots.push(f);
  }
  await b.close();
  const sheet = path.join(dir, `${name}-onizleme.jpg`);
  const cols = 3;
  const rows = Math.ceil(shots.length / cols);
  const inputs = shots.map((f) => `-i "${f}"`).join(' ');
  const scaled = shots.map((_, i) => `[${i}]scale=360:640[s${i}]`).join(';');
  const pads = Array.from({ length: rows * cols - shots.length }, (_, i) => `color=black:s=360x640:d=1[e${i}]`).join(';');
  const all = [...shots.map((_, i) => `[s${i}]`), ...Array.from({ length: rows * cols - shots.length }, (_, i) => `[e${i}]`)];
  const rowsF = Array.from({ length: rows }, (_, r) => `${all.slice(r * cols, r * cols + cols).join('')}hstack=${cols}[r${r}]`).join(';');
  const filt = [scaled, pads, rowsF, rows > 1 ? `${Array.from({ length: rows }, (_, r) => `[r${r}]`).join('')}vstack=${rows}` : '[r0]null'].filter(Boolean).join(';');
  execSync(`ffmpeg -y -loglevel error ${inputs} -filter_complex "${filt}" -frames:v 1 "${sheet}"`);
  shots.forEach((f) => fs.rmSync(f));
  console.log('önizleme:', sheet, errs.length ? '| sayfa hataları: ' + errs.join(' ; ') : '');
  return sheet;
}

module.exports = { preview };
