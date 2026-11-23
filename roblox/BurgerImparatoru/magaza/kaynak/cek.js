// Kullanım: node cek.js [çekim adı]  -> çekimler.json'daki çekimleri PNG olarak kaydeder
const { chromium } = require('playwright');
const fs = require('fs');
const http = require('http');
const path = require('path');
const kok = __dirname;
const sunucu = http.createServer((req, res) => {
  const p = path.join(kok, decodeURIComponent(req.url.split('?')[0]));
  if (!p.startsWith(kok) || !fs.existsSync(p) || fs.statSync(p).isDirectory()) { res.writeHead(404); return res.end(); }
  const tur = { '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json', '.css': 'text/css', '.woff2': 'font/woff2', '.woff': 'font/woff' }[path.extname(p)] || 'application/octet-stream';
  res.writeHead(200, { 'Content-Type': tur }); fs.createReadStream(p).pipe(res);
});
(async () => {
  await new Promise(r => sunucu.listen(0, '127.0.0.1', r));
  const port = sunucu.address().port;
  const cekimler = JSON.parse(fs.readFileSync(path.join(kok, 'cekimler.json'), 'utf8'));
  const sadece = process.argv[2];
  const browser = await chromium.launch({ args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
  for (const [ad, c] of Object.entries(cekimler)) {
    if (sadece && sadece !== ad) continue;
    const t0 = Date.now();
    const page = await browser.newPage({ viewport: { width: c.w, height: c.h }, deviceScaleFactor: c.dsf || 2 });
    page.on('pageerror', e => { console.log('HATA', ad, e.message); process.exit(1); });
    await page.goto(`http://127.0.0.1:${port}/cizici.html?c=${encodeURIComponent(JSON.stringify(c))}`);
    await page.waitForFunction(() => document.title === 'HAZIR', null, { timeout: 600000 });
    await page.waitForTimeout(200);
    await page.screenshot({ path: path.join(kok, ad + '_ham.png') });
    await page.close();
    console.log(ad, 'bitti', ((Date.now() - t0) / 1000).toFixed(1) + ' sn');
  }
  await browser.close(); sunucu.close();
})();
