// Sunucusuz, tek dosyalık demo üretir: oyun mantığı tarayıcıda çalışır, sahte izleyiciler hediye atar.
// Kullanım: node build-demo.mjs [çıktı.html]   (varsayılan: dist/demo.html)
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = path.dirname(fileURLToPath(import.meta.url));
const read = (p) => fs.readFileSync(path.join(ROOT, p), 'utf8');
const out = process.argv[2] ?? path.join(ROOT, 'dist', 'demo.html');

const overlay = read('public/overlay.html');
const css = overlay.match(/<style>([\s\S]*?)<\/style>/)[1];
const fonts = overlay.match(/<link href="https:\/\/fonts\.googleapis\.com[^>]*>/)[0];
const body = overlay.match(/<body>([\s\S]*?)<script src="fx\.js"><\/script>/)[1];
const mainScript = overlay.match(/<script src="spiker\.js"><\/script>\s*<script>([\s\S]*?)<\/script>\s*<\/body>/)[1];
const gameJs = read('game.js').replace(/^export /gm, '');

const config = JSON.parse(read('config.json'));
config.auto = { ...config.auto, enabled: true, lobbySeconds: 35, breakSeconds: 14 };
config.match = { ...config.match, secondsPerMinute: 1 };

const demoCss = `
  /* demo: sayfa Claude'un çerçevesinde açılır, kök dolgusu korunur */
  html, body { height: 100%; }
  body { background: #070d17; }
  #wrap { position: relative; inset: auto; width: 100%; height: 100%; }
  #demobar {
    position: fixed; left: 50%; transform: translateX(-50%); z-index: 50;
    bottom: calc(10px + env(safe-area-inset-bottom, 0px));
    display: flex; gap: 6px; flex-wrap: wrap; justify-content: center;
    width: max-content; max-width: calc(100% - 24px);
    padding: 6px; border-radius: 14px; background: rgba(6,10,18,.88); border: 1px solid rgba(255,255,255,.18);
  }
  #demobar button {
    font: 800 13px Inter, system-ui, sans-serif; color: #fff; background: #22324a;
    border: 1px solid #3a5174; border-radius: 10px; padding: 8px 10px; cursor: pointer;
  }
  #demobar button:focus-visible { outline: 2px solid #ffc531; outline-offset: 2px; }
  #demobar .a { background: #e53935; border-color: #e53935; }
  #demobar .b { background: #1e88e5; border-color: #1e88e5; }
  #demobar .tag { font: 800 11px Inter, sans-serif; color: #ffc531; align-self: center; padding: 0 4px; letter-spacing: 1px; }
  @media (prefers-reduced-motion: reduce) { * { animation-duration: .01ms !important; transition-duration: .01ms !important; } }
`;

const demoBar = `
<div id="demobar" role="group" aria-label="Demo kontrolleri">
  <span class="tag">DEMO · SEN:</span>
  <button id="dA" class="a" type="button">1 yaz</button>
  <button id="dB" class="b" type="button">2 yaz</button>
  <button id="dG1" type="button">🌹 Gül 1</button>
  <button id="dG20" type="button">💐 Parfüm 20</button>
  <button id="dG299" type="button">🦁 Aslan 299</button>
</div>`;

const engine = `
window.FUTBOL_LOCAL = {
  start({ render, onEvent }) {
    const CONFIG = ${JSON.stringify(config)};
    const game = new Game(CONFIG);
    let pending = null;
    game.onChange = () => {
      if (pending) return;
      pending = setTimeout(() => { pending = null; render(game.snapshot()); }, 80);
    };
    game.onEvent = (ev) => onEvent(ev);
    setInterval(() => game.tick(), CONFIG.match.secondsPerMinute * 1000);
    setInterval(() => game.autoTick(), 1000);

    const NAMES = ['Ahmet','Elif','Mert','Zeynep','Can','Ayşe','Burak','Selin','Emre','Deniz','Kerem','Ece','Onur','Merve','Arda','Buse','Yusuf','İrem','Kaan','Derya','Efe','Sude','Barış','Nehir','Umut','Ceren','Tolga','Melis','Serkan','Gizem'];
    const GIFTS = [['Gül',1,50],['TikTok',1,20],['Parmak Kalp',5,15],['Dondurma',1,10],['Parfüm',20,6],['Kalp Gözler',30,4],['Kaplan',99,1.5],['Aslan',299,0.5]];
    const total = GIFTS.reduce((a, g) => a + g[2], 0);
    function fake() {
      let r = Math.random() * total;
      const g = GIFTS.find((x) => (r -= x[2]) <= 0) || GIFTS[0];
      const i = Math.floor(Math.random() * 30);
      const n = 1 + Math.floor(Math.random() * 3);
      game.handleGift({ id: 'demo_' + i, name: NAMES[i], giftName: g[0], count: n, coins: g[1] * n });
    }
    setInterval(fake, 1300);
    setInterval(() => {
      const i = 30 + Math.floor(Math.random() * 40);
      onEvent({ type: 'viewer', name: NAMES[i % 30] + i, ts: Date.now() });
    }, 8000);

    const me = { id: 'sen', name: 'Sen' };
    const gift = (name, coins) => game.handleGift({ ...me, giftName: name, count: 1, coins });
    document.getElementById('dA').onclick = () => game.handleChat({ ...me, text: '1' });
    document.getElementById('dB').onclick = () => game.handleChat({ ...me, text: '2' });
    document.getElementById('dG1').onclick = () => gift('Gül', 1);
    document.getElementById('dG20').onclick = () => gift('Parfüm', 20);
    document.getElementById('dG299').onclick = () => gift('Aslan', 299);

    for (let k = 0; k < 9; k++) fake();
    render(game.snapshot());
  },
};`;

const html = `<title>Canlı Futbol Demo</title>
${fonts}
<style>${css}${demoCss}</style>
${body.trim()}
${demoBar}
<script>${read('public/fx.js')}</script>
<script>${read('public/spiker.js')}</script>
<script>${gameJs}</script>
<script>${engine}</script>
<script>${mainScript}</script>
`;

fs.mkdirSync(path.dirname(out), { recursive: true });
fs.writeFileSync(out, html);
console.log(`Demo yazıldı: ${out} (${(html.length / 1024).toFixed(0)} KB)`);
