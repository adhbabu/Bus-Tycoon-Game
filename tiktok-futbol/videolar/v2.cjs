// Seslendirmeli futbol videoları: anlatım sesi → süreye göre zamanlama → 30 fps kare kare kayıt → ses + görüntü
// Anlatım CLIPS_DIR içindeki ElevenLabs kayıtlarından alınır (varsayılan: ../seslendirme/video-sesleri)
const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');
const { chromium } = require('playwright');
const { HEAD, TAIL } = require('./common-head.cjs');

// çıktılar (html, kareler, mp4, efekt sesleri) cikti/ altına yazılır; git'e girmez
const DIR = path.join(__dirname, 'cikti');
const AUD = path.join(DIR, 'aud');
fs.mkdirSync(AUD, { recursive: true });
const VOICE = process.env.VOICE || 'en-US-AndrewMultilingualNeural';
const SERVER = 'https://bus-tycoon-game-1.onrender.com';
const FPS = 30;

const dur = (f) => Number(execSync(`ffprobe -v error -show_entries format=duration -of csv=p=0 "${f}"`).toString().trim());
let clipNo = 0;
function voice(text, style = 'info') {
  const f = path.join(AUD, `${VOICE.split('-')[2]}-${Buffer.from(style + text).toString('base64url').slice(0, 40)}-${text.length}.mp3`);
  if (!fs.existsSync(f) || fs.statSync(f).size < 1000) {
    execSync(`curl -sS -m 90 -G -o "${f}" --data-urlencode "t=${text.replace(/"/g, '\\"')}" --data-urlencode "v=${VOICE}" --data-urlencode "s=${style}" ${SERVER}/tts`);
  }
  if (fs.statSync(f).size < 1000) throw new Error('ses üretilemedi: ' + text);
  clipNo++;
  return { file: f, ms: dur(f) * 1000 };
}
// basit efektler
function makeSfx() {
  const tick = path.join(AUD, 'tick.wav');
  const ding = path.join(AUD, 'ding.wav');
  const buzz = path.join(AUD, 'buzz.wav');
  execSync(`ffmpeg -y -loglevel error -f lavfi -i "sine=f=1000:d=0.07" -af "afade=t=out:st=0.03:d=0.04,volume=0.5" -ar 24000 -ac 1 "${tick}"`);
  execSync(`ffmpeg -y -loglevel error -f lavfi -i "aevalsrc='0.4*sin(2*PI*1320*t)*exp(-6*t)+0.3*sin(2*PI*1760*t)*exp(-5*(t-0.09))*gt(t,0.09)':s=24000:d=0.7" -ac 1 "${ding}"`);
  execSync(`ffmpeg -y -loglevel error -f lavfi -i "aevalsrc=0.35*sin(2*PI*150*t)*exp(-3*t):s=24000:d=0.5" -ac 1 "${buzz}"`);
  return { tick, ding, buzz };
}

// steps: { say?, style?, min?, pad?, run? (JS), sfx?: [{t, k}] }  → zaman çizelgesi
// codes verilirse anlatım, CLIPS_DIR içindeki kayıtlı seslerden (P1.mp3 ...) sırayla alınır
function timeline(steps, codes) {
  let t = 0;
  let n = 0;
  const out = [];
  const clipsDir = process.env.CLIPS_DIR || path.join(__dirname, '..', 'seslendirme', 'video-sesleri');
  for (const s of steps) {
    let v = null;
    if (s.say && codes && clipsDir) {
      const f = path.join(clipsDir, codes[n++] + '.mp3');
      v = { file: f, ms: dur(f) * 1000 };
    } else if (s.say) v = voice(s.say, s.style);
    const len = Math.max(s.min || 0, v ? v.ms + (s.pad ?? 350) : 0);
    out.push({ ...s, start: Math.round(t), audio: v });
    t += len;
  }
  return { steps: out, total: Math.round(t) };
}

async function render(name, html, tl, sfx, tail = 1200) {
  const file = path.join(DIR, name + '.html');
  const script = `<script>${tl.steps.filter((s) => s.run).map((s) => `at(${s.start}, () => { ${s.run} });`).join('\n')}</script></body></html>`;
  fs.writeFileSync(file, html + script);
  const fdir = path.join(DIR, 'f-' + name);
  const totalMs0 = tl.total + tail;
  const frames0 = Math.round((totalMs0 / 1000) * FPS);
  const cached = fs.existsSync(fdir) && fs.readdirSync(fdir).length === frames0;
  if (!cached) {
  fs.rmSync(fdir, { recursive: true, force: true });
  fs.mkdirSync(fdir);
  const b = await chromium.launch();
  const ctx = await b.newContext({ viewport: { width: 1080, height: 1920 }, deviceScaleFactor: 1 });
  const p = await ctx.newPage();
  const errs = [];
  p.on('pageerror', (e) => errs.push(e.message));
  const t0 = new Date('2026-11-16T18:00:00Z').getTime();
  await p.clock.install({ time: t0 });
  await p.clock.pauseAt(t0 + 1000);
  await p.goto('file://' + file);
  await p.evaluate(() => document.fonts.ready);
  await p.evaluate(() => {
    const seen = new WeakMap();
    window.__sync = (vt) => {
      for (const a of document.getAnimations()) {
        if (!seen.has(a)) { seen.set(a, vt); a.pause(); }
        a.currentTime = Math.max(0, vt - seen.get(a));
      }
    };
  });
  const frames = frames0;
  const step = 1000 / FPS;
  for (let i = 0; i < frames; i++) {
    await p.clock.runFor(step);
    await p.evaluate((vt) => window.__sync(vt), (i + 1) * step);
    await p.screenshot({ path: path.join(fdir, String(i).padStart(5, '0') + '.jpg'), type: 'jpeg', quality: 90 });
  }
  await b.close();
  if (errs.length) console.log('sayfa hataları:', errs);
  }
  const totalMs = totalMs0;
  // ses izi: anlatım + efektler, zamanında
  const clips = [];
  for (const s of tl.steps) {
    if (s.audio) clips.push({ f: s.audio.file, t: s.start, vol: 1.6 });
    for (const x of s.sfx || []) clips.push({ f: sfx[x.k], t: s.start + x.t, vol: 1 });
  }
  const inputs = clips.map((c) => `-i "${c.f}"`).join(' ');
  const filt = clips.map((c, i) => `[${i + 1}:a]aresample=24000,adelay=${c.t}:all=1,volume=${c.vol}[a${i}]`).join(';') +
    `;${clips.map((_, i) => `[a${i}]`).join('')}amix=inputs=${clips.length}:duration=longest:normalize=0,alimiter=limit=0.95,apad[aout]`;
  const mp4 = path.join(DIR, name + '.mp4');
  try {
  execSync(`ffmpeg -y -loglevel error -framerate ${FPS} -i "${fdir}/%05d.jpg" ${inputs} -filter_complex "${filt}" -map 0:v -map "[aout]" -c:v libx264 -preset medium -crf 20 -pix_fmt yuv420p -r ${FPS} -c:a aac -b:a 128k -shortest -movflags +faststart "${mp4}"`, { stdio: ['ignore', 'pipe', 'pipe'] });
  } catch (e) {
    console.log('FFMPEG HATASI:', e.stderr?.toString().slice(0, 1500));
    throw new Error('ffmpeg');
  }
  fs.rmSync(fdir, { recursive: true, force: true });
  console.log(name, '| süre:', (totalMs / 1000).toFixed(1) + 's', '| ses:', clips.length);
}

module.exports = { timeline, render, makeSfx, voice };
