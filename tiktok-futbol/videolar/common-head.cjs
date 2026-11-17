const fs = require('fs');
const path = require('path');
const DIR = __dirname;
const BALL = fs.readFileSync(path.join(DIR, 'ball.js'), 'utf8');
const HEAD = `<!doctype html><html lang="tr"><head><meta charset="utf-8">
<link href="https://fonts.googleapis.com/css2?family=Kaushan+Script&family=Oswald:wght@500;700&family=Inter:wght@600;800;900&display=swap" rel="stylesheet">
<style>
  :root { --gold:#ffc531; --fb:#0b2a6f; --fb2:#ffe600; --gs:#b0102e; --gs2:#fdb913; --ok:#22c55e; --bad:#ef4444; }
  * { box-sizing: border-box; margin: 0; padding: 0; }
  html, body { width: 1080px; height: 1920px; overflow: hidden; background: #070d17; color: #fff; font-family: Inter, sans-serif; }
  #v { position: relative; width: 1080px; height: 1920px; overflow: hidden;
       background: radial-gradient(circle at 50% 35%, #1d4a6e 0%, #0d2034 50%, #060b14 100%); }
  #bgb { position: absolute; left: 50%; top: 50%; width: 1700px; height: 1700px; margin: -850px 0 0 -850px; opacity: .06; animation: spin 60s linear infinite; }
  .scene { position: absolute; inset: 0; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 40px; padding: 0 70px; text-align: center; opacity: 0; transition: opacity .35s; }
  .scene.on { opacity: 1; }
  .h1 { font: 700 132px/1.02 Oswald, sans-serif; letter-spacing: 2px; text-shadow: 0 10px 0 rgba(0,0,0,.35); }
  .h2 { font: 700 84px/1.08 Oswald, sans-serif; }
  .t { font: 800 56px/1.25 Inter, sans-serif; }
  .s { font: 700 40px/1.35 Inter, sans-serif; color: #c9d4e3; }
  .gold { color: var(--gold); }
  .script { font-family: 'Kaushan Script', cursive; font-weight: 400; }
  .pop { animation: pop .5s cubic-bezier(.2,1.6,.4,1) both; }
  .rise { animation: rise .5s ease both; }
  .d1 { animation-delay: .15s; } .d2 { animation-delay: .3s; } .d3 { animation-delay: .45s; } .d4 { animation-delay: .6s; }
  .chip { display: inline-block; padding: 10px 28px; border-radius: 18px; border: 6px solid; font: 700 60px/1 Oswald, sans-serif; }
  .chip.fb { background: var(--fb); color: var(--fb2); border-color: var(--fb2); }
  .chip.gs { background: var(--gs); color: var(--gs2); border-color: var(--gs2); }
  #sig { position: absolute; left: 0; right: 0; bottom: 40px; text-align: center; font: 400 44px 'Kaushan Script', cursive; color: rgba(255,255,255,.55); }
  @keyframes pop { from { opacity: 0; transform: scale(1.9); } to { opacity: 1; transform: none; } }
  @keyframes rise { from { opacity: 0; transform: translateY(60px); } to { opacity: 1; transform: none; } }
  @keyframes spin { to { transform: rotate(360deg); } }
  @keyframes pulse { 50% { transform: scale(1.08); } }
</style></head><body><div id="v"><div id="bgb"></div>`;

const TAIL = `<div id="sig">Canlı Maç Oyna</div></div>
<script>${BALL}
document.getElementById('bgb').innerHTML = classicBall('bg');
const at = (ms, fn) => setTimeout(fn, ms);
const show = (id) => { document.querySelectorAll('.scene').forEach((s) => s.classList.toggle('on', s.id === id)); };
</script>`;


module.exports = { HEAD, TAIL };
