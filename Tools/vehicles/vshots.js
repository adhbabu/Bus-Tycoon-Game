// node vshots.js outdir [ids...]  -> outdir/ID.png (1600x800, saydam)
const { chromium } = require('playwright');
(async () => {
  const [out, ...ids] = process.argv.slice(2);
  const b = await chromium.launch({ args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
  const p = await b.newPage({ viewport: { width: 1600, height: 800 } });
  p.on('pageerror', e => console.log('pageerror', e.message));
  p.on('console', m => { if (m.type() === 'error') console.log('console', m.text()); });
  await p.goto('http://127.0.0.1:8806/threejs/vehicles.html' + (process.env.SRC ? '?src=' + process.env.SRC : ''));
  await p.waitForFunction(() => window.READY === true, null, { timeout: 120000 });
  const all = ids.length ? ids : await p.evaluate(() => window.IDS);
  for (const id of all) {
    await p.evaluate(([i]) => window.show(i), [id]);
    await p.screenshot({ path: `${out}/${id}.png`, omitBackground: true });
    console.log('ok', id);
  }
  await b.close();
})();
