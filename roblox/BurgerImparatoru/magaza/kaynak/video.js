// Kullanım: node video.js   (önce: lune run ../../test/sim.luau ../../BurgerImparatoru.rbxlx foto)
// TikTok / Shorts / Reels için 9:16 tanıtım videosu: oyunun haritası + huss sesi + yazılar (EN ve TR)
const { chromium } = require('playwright');
const fs = require('fs'), http = require('http'), path = require('path'), { execFileSync } = require('child_process');
const kok = __dirname;
const FPS = 24, SURE = 12, W = 540, H = 960, DSF = 2;
const ORNEK = process.argv.includes('--ornek');
const KARE = FPS * SURE;
const KARELER = ORNEK ? [24, 130, 200, 275] : [...Array(KARE).keys()];
const klasor = path.join(kok, 'video_kareler');
fs.mkdirSync(klasor, { recursive: true });

// çekimler: [başlangıç sn, bitiş sn, göz başı, göz sonu, bakış başı, bakış sonu, fov]
const CEKIMLER = [
  [0, 3.6, [-45.5, 9.5, -102], [-42, 7, -106], [-38.6, 4.6, -111.3], [-38.6, 4.6, -111.3], 50],
  [3.6, 7.2, [-10, 30, -56], [-100, 34, -68], [-56, 8, -121], [-56, 8, -121], 56],
  [7.2, 12, [34, 16, 64], [66, 72, 112], [0, 9, 0], [0, 6, 0], 62],
];
const yumusak = (t) => t * t * (3 - 2 * t);
const ara = (a, b, t) => a.map((x, i) => x + (b[i] - x) * t);
function kamera(t) {
  for (const c of CEKIMLER) {
    if (t >= c[0] && t < c[1] + 1e-9) {
      const u = yumusak((t - c[0]) / (c[1] - c[0]));
      return { goz: ara(c[2], c[3], u), bak: ara(c[4], c[5], u), fov: c[6] };
    }
  }
}
// huss sesleri (tam "huss huss huss" klibi) bu anlarda çalar; HUSS! yazıları da sesteki 3 huss'a denk gelir
const VURUSLAR = [0.3, 1.3, 2.3, 4.0, 5.2, 7.6, 8.8, 10.2];
const HUSS_ANLARI = [0.04, 0.24, 0.44];

const YAZI = {
  en: { katla: 'FOLD THE BURGER 🍔', kur: 'BUILD YOUR<br>BURGER EMPIRE 👑', kasaba: 'QUESTS • CONTESTS<br>GOLDEN HOUR ⏰', oyna: 'PLAY NOW ON ROBLOX 🎮', kod: 'CODE: HUSS 🎟️' },
  tr: { katla: 'BURGERİ KATLA 🍔', kur: 'BURGER<br>İMPARATORLUĞUNU KUR 👑', kasaba: 'GÖREVLER • YARIŞMALAR<br>ALTIN SAAT ⏰', oyna: "ROBLOX'TA HEMEN OYNA 🎮", kod: 'KOD: HUSS 🎟️' },
};

const sunucu = http.createServer((req, res) => {
  const p = path.join(kok, decodeURIComponent(req.url.split('?')[0]));
  if (!p.startsWith(kok) || !fs.existsSync(p) || fs.statSync(p).isDirectory()) { res.writeHead(404); return res.end(); }
  const tur = { '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json', '.css': 'text/css', '.woff2': 'font/woff2', '.woff': 'font/woff' }[path.extname(p)] || 'application/octet-stream';
  res.writeHead(200, { 'Content-Type': tur }); fs.createReadStream(p).pipe(res);
});

const YAZI_SAYFASI = `<!doctype html><html><head><meta charset="utf-8">
<link rel="stylesheet" href="node_modules/@fontsource/fredoka/700.css">
<style>html,body{margin:0;background:transparent;overflow:hidden;width:${W}px;height:${H}px}
#k{position:absolute;inset:0;font-family:Fredoka,"Noto Color Emoji";font-weight:700;color:#fff;text-align:center}
.s{position:absolute;left:0;right:0;text-shadow:0 4px 0 #5a200a,3px 3px 0 #5a200a,-3px 3px 0 #5a200a,3px -3px 0 #5a200a,-3px -3px 0 #5a200a,0 8px 14px rgba(0,0,0,.4)}
.h{position:absolute;font-size:74px;color:#ffd614;text-shadow:0 5px 0 #5a200a,4px 4px 0 #5a200a,-4px 4px 0 #5a200a,4px -4px 0 #5a200a,-4px -4px 0 #5a200a}
.e{position:absolute;left:50%;transform:translateX(-50%);background:rgba(255,255,255,.95);color:#c8341f;border:5px solid #3b1d0c;border-radius:22px;padding:6px 20px;white-space:nowrap;box-shadow:0 6px 0 rgba(0,0,0,.3)}
</style></head><body><div id="k"></div><script>
window.ciz = (d) => { document.getElementById('k').innerHTML = d; };
document.fonts.ready.then(() => { document.title = 'HAZIR'; });
</script></body></html>`;

function yazilar(t, dil) {
  const y = YAZI[dil];
  let h = '';
  // en üstte küçük oyun adı
  h += `<div class="s" style="top:22px;font-size:34px">🍔 BURGER TYCOON</div>`;
  if (t < 3.6) h += `<div class="s" style="top:96px;font-size:54px">${y.katla}</div>`;
  else if (t < 7.2) h += `<div class="s" style="top:96px;font-size:54px;line-height:1.05">${y.kur}</div>`;
  else if (t < 9.8) h += `<div class="s" style="top:96px;font-size:50px;line-height:1.1">${y.kasaba}</div>`;
  else {
    const u = Math.min(1, (t - 9.8) / 0.35), olc = 0.6 + 0.4 * (1 - Math.pow(1 - u, 3));
    h += `<div class="s" style="top:250px;font-size:96px;line-height:0.95;transform:scale(${olc})">BURGER<br><span style="color:#ffd614">TYCOON</span></div>`;
    h += `<div class="e" style="bottom:210px;font-size:40px">${y.oyna}</div>`;
    h += `<div class="e" style="bottom:120px;font-size:40px;color:#1f7a3a">${y.kod}</div>`;
  }
  // HUSS! yazıları: her vuruşta sesteki 3 huss'a denk
  const yerler = [[60, 560, -10], [250, 640, 8], [110, 730, -6]];
  VURUSLAR.forEach((v, i) => {
    if (t > 9.8) return;
    HUSS_ANLARI.forEach((a, j) => {
      const dt = t - (v + a);
      if (dt >= 0 && dt < 0.55) {
        const pop = dt < 0.12 ? 0.4 + 0.6 * (dt / 0.12) * 1.25 : 1.15 - Math.min(0.15, (dt - 0.12) * 0.6);
        const op = dt > 0.4 ? 1 - (dt - 0.4) / 0.15 : 1;
        const [x, yy, r] = yerler[(i + j) % 3];
        const renk = ['#ffd614', '#ffffff', '#ff8a3d'][j];
        h += `<div class="h" style="left:${x}px;top:${yy - dt * 60}px;color:${renk};opacity:${op};transform:rotate(${r}deg) scale(${pop})">HUSS!</div>`;
      }
    });
  });
  return h;
}

(async () => {
  await new Promise(r => sunucu.listen(0, '127.0.0.1', r));
  const port = sunucu.address().port;
  fs.writeFileSync(path.join(kok, 'video_yazi.html'), YAZI_SAYFASI);
  const browser = await chromium.launch({ args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
  const atla = process.argv.includes('--sadece-yazi');
  if (!atla) {
    // 1) 3 boyutlu kareler (bir kere)
    const temel = { w: W, h: H, goz: CEKIMLER[0][2], bak: CEKIMLER[0][4], fov: 50, golgeAlani: 170, gunesKamera: 35, gunesYukseklik: 50, ao: 0, parlama: 0.35, panolar: false };
    // gölgeler meydanla dükkan arasında bir merkeze göre hesaplansın
    temel.bak = [-30, 4, -60];
    const page = await browser.newPage({ viewport: { width: W, height: H }, deviceScaleFactor: DSF });
    page.on('pageerror', e => { console.log('HATA', e.message); process.exit(1); });
    await page.goto(`http://127.0.0.1:${port}/cizici.html?c=${encodeURIComponent(JSON.stringify(temel))}`);
    await page.waitForFunction(() => document.title === 'HAZIR', null, { timeout: 600000 });
    const t0 = Date.now();
    for (const i of KARELER) {
      const k = kamera(i / FPS);
      await page.evaluate((k) => window.kareCiz(k), k);
      await page.screenshot({ path: path.join(klasor, `k${String(i).padStart(4, '0')}.png`) });
      if (i % 24 === 0) console.log('3D kare', i, '/', KARE, ((Date.now() - t0) / 1000).toFixed(0) + ' sn');
    }
    await page.close();
  }
  // 2) yazı kareleri (iki dil)
  const yp = await browser.newPage({ viewport: { width: W, height: H }, deviceScaleFactor: DSF });
  await yp.goto(`http://127.0.0.1:${port}/video_yazi.html`);
  await yp.waitForFunction(() => document.title === 'HAZIR');
  for (const dil of ['en', 'tr']) {
    for (const i of KARELER) {
      await yp.evaluate((h) => window.ciz(h), yazilar(i / FPS, dil));
      await yp.screenshot({ path: path.join(klasor, `${dil}${String(i).padStart(4, '0')}.png`), omitBackground: true });
    }
    console.log('yazılar bitti', dil);
  }
  await browser.close(); sunucu.close();
  if (ORNEK) {
    for (const i of KARELER) {
      const n = String(i).padStart(4, '0');
      execFileSync('ffmpeg', ['-y', '-i', path.join(klasor, `k${n}.png`), '-i', path.join(klasor, `en${n}.png`), '-filter_complex', 'overlay=0:0,scale=540:960', path.join(klasor, `ornek${n}.jpg`)], { stdio: 'ignore' });
    }
    console.log('örnekler hazır'); return;
  }
  // 3) ses: huss klibi her vuruşta
  const ses = path.join(kok, '..', '..', 'sesler', 'huss.mp3');
  const args = ['-y'];
  VURUSLAR.forEach(() => args.push('-i', ses));
  const filtre = VURUSLAR.map((v, i) => `[${i}:a]adelay=${Math.round(v * 1000)}|${Math.round(v * 1000)}[a${i}]`).join(';')
    + ';' + VURUSLAR.map((_, i) => `[a${i}]`).join('') + `amix=inputs=${VURUSLAR.length}:normalize=0,apad=whole_dur=${SURE}[s]`;
  const sesDosyasi = path.join(klasor, 'ses.m4a');
  execFileSync('ffmpeg', [...args, '-filter_complex', filtre, '-map', '[s]', '-t', String(SURE), '-c:a', 'aac', '-b:a', '160k', sesDosyasi], { stdio: 'ignore' });
  // 4) birleştir
  for (const dil of ['en', 'tr']) {
    const cikti = path.join(kok, '..', `tanitim-video-${dil}.mp4`);
    execFileSync('ffmpeg', ['-y', '-framerate', String(FPS), '-i', path.join(klasor, 'k%04d.png'), '-framerate', String(FPS), '-i', path.join(klasor, `${dil}%04d.png`), '-i', sesDosyasi,
      '-filter_complex', '[0:v][1:v]overlay=0:0,format=yuv420p[v]', '-map', '[v]', '-map', '2:a', '-c:v', 'libx264', '-preset', 'medium', '-crf', '20', '-c:a', 'copy', '-shortest', '-movflags', '+faststart', cikti], { stdio: 'ignore' });
    console.log('video hazır:', cikti);
  }
})();
