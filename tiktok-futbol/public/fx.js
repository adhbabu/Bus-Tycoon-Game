// Ses ve görsel efektler.
// Tüm sesler Web Audio ile anında üretilir (ses dosyası gerekmez).
// Adres çubuğu ayarları:  ?ses=0 (sessiz)  ?tts=0 (sesli anons kapalı)  ?vol=0.5 (ses düzeyi)
window.FX = (() => {
  const params = new URLSearchParams(location.search);
  const soundOn = params.get('ses') !== '0';
  const ttsOn = params.get('tts') !== '0' && 'speechSynthesis' in window;
  const volume = Math.min(1.5, Math.max(0, Number(params.get('vol') ?? 0.8)));

  let ctx = null;
  let master = null;
  let noiseBuf = null;
  let crowd = null;
  let crowdLevel = 0;

  function init() {
    if (ctx || !soundOn) return;
    const AC = window.AudioContext || window.webkitAudioContext;
    if (!AC) return;
    ctx = new AC();
    master = ctx.createGain();
    master.gain.value = volume;
    const comp = ctx.createDynamicsCompressor();
    comp.threshold.value = -14;
    comp.ratio.value = 6;
    master.connect(comp);
    comp.connect(ctx.destination);
    noiseBuf = ctx.createBuffer(1, ctx.sampleRate * 2, ctx.sampleRate);
    const d = noiseBuf.getChannelData(0);
    for (let i = 0; i < d.length; i++) d[i] = Math.random() * 2 - 1;
  }

  function ok() {
    init();
    if (!ctx) return false;
    if (ctx.state === 'suspended') ctx.resume().catch(() => {});
    return ctx.state === 'running';
  }

  // Tarayıcı ilk tıklamadan önce sese izin vermeyebilir (OBS'te sorun yok).
  function setupUnlock() {
    if (!soundOn) return;
    init();
    if (!ctx) return;
    const btn = document.createElement('button');
    btn.textContent = '🔊 Sesi açmak için tıkla';
    btn.style.cssText =
      'position:fixed;left:50%;top:50%;transform:translate(-50%,-50%);z-index:99;font:800 22px Inter,sans-serif;' +
      'padding:18px 28px;border-radius:16px;border:3px solid #fff;background:#ffc531;color:#2a1e00;cursor:pointer;display:none';
    document.body.appendChild(btn);
    const check = () => {
      btn.style.display = ctx.state === 'running' ? 'none' : 'block';
    };
    // iPhone'da sesli anons da ilk dokunuşta açılır
    let spoke = false;
    const unlock = () => {
      if (ttsOn && !spoke) {
        spoke = true;
        const u = new SpeechSynthesisUtterance('Ses açık');
        u.lang = 'tr-TR';
        if (voice) u.voice = voice;
        speechSynthesis.speak(u);
      }
      return ctx.resume().then(check);
    };
    btn.onclick = unlock;
    document.addEventListener('pointerdown', unlock, { once: true });
    ctx.onstatechange = check;
    setTimeout(check, 600);
  }

  // ---------- temel sentez parçaları ----------
  function tone({ type = 'sine', f = 440, f2 = null, t = 0, dur = 0.2, gain = 0.3, attack = 0.006, dest = master, lp = null }) {
    const start = ctx.currentTime + t;
    const o = ctx.createOscillator();
    const g = ctx.createGain();
    o.type = type;
    o.frequency.setValueAtTime(f, start);
    if (f2) o.frequency.exponentialRampToValueAtTime(f2, start + dur);
    g.gain.setValueAtTime(0.0001, start);
    g.gain.exponentialRampToValueAtTime(gain, start + attack);
    g.gain.exponentialRampToValueAtTime(0.0001, start + dur);
    let node = o;
    if (lp) {
      const fl = ctx.createBiquadFilter();
      fl.type = 'lowpass';
      fl.frequency.value = lp;
      o.connect(fl);
      node = fl;
    }
    node.connect(g);
    g.connect(dest);
    o.start(start);
    o.stop(start + dur + 0.05);
    return o;
  }

  function noise({ t = 0, dur = 1, gain = 0.3, type = 'bandpass', f = 800, f2 = null, q = 1, attack = 0.05 }) {
    const start = ctx.currentTime + t;
    const s = ctx.createBufferSource();
    s.buffer = noiseBuf;
    s.loop = true;
    const fl = ctx.createBiquadFilter();
    fl.type = type;
    fl.frequency.setValueAtTime(f, start);
    if (f2) fl.frequency.exponentialRampToValueAtTime(f2, start + dur);
    fl.Q.value = q;
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, start);
    g.gain.exponentialRampToValueAtTime(gain, start + attack);
    g.gain.exponentialRampToValueAtTime(0.0001, start + dur);
    s.connect(fl);
    fl.connect(g);
    g.connect(master);
    s.start(start, Math.random());
    s.stop(start + dur + 0.1);
  }

  // ---------- oyun sesleri ----------
  let lastCoin = 0;
  function coin(coins = 1) {
    if (!ok()) return;
    const now = performance.now();
    if (now - lastCoin < 70) return; // hediye yağmurunda kulak tırmalamasın
    lastCoin = now;
    const tier = coins >= 100 ? 3 : coins >= 30 ? 2 : coins >= 5 ? 1 : 0;
    const scale = [988, 1319, 1568, 1976, 2349, 2637];
    const n = tier + 2;
    for (let i = 0; i < n; i++) {
      tone({ type: 'square', f: scale[i], t: i * 0.065, dur: 0.16 + (i === n - 1 ? 0.25 : 0), gain: 0.07, lp: 4000 });
      tone({ type: 'sine', f: scale[i] * 2, t: i * 0.065, dur: 0.12, gain: 0.03 });
    }
  }

  function whistle(blasts = 1, long = false) {
    if (!ok()) return;
    let t = 0;
    for (let i = 0; i < blasts; i++) {
      const dur = long && i === blasts - 1 ? 1.1 : 0.32;
      const start = ctx.currentTime + t;
      const o = ctx.createOscillator();
      const lfo = ctx.createOscillator();
      const lfoG = ctx.createGain();
      const g = ctx.createGain();
      o.type = 'sine';
      o.frequency.value = 2850;
      lfo.frequency.value = 38;
      lfoG.gain.value = 170;
      lfo.connect(lfoG);
      lfoG.connect(o.frequency);
      g.gain.setValueAtTime(0.0001, start);
      g.gain.exponentialRampToValueAtTime(0.22, start + 0.02);
      g.gain.setValueAtTime(0.22, start + dur - 0.05);
      g.gain.exponentialRampToValueAtTime(0.0001, start + dur);
      o.connect(g);
      g.connect(master);
      o.start(start);
      lfo.start(start);
      o.stop(start + dur + 0.05);
      lfo.stop(start + dur + 0.05);
      noise({ t, dur, gain: 0.05, type: 'highpass', f: 3500, attack: 0.02 });
      t += dur + 0.12;
    }
  }

  function roar(dur = 3.5, gain = 0.55) {
    if (!ok()) return;
    noise({ dur, gain, f: 900, q: 0.5, attack: 0.25 });
    noise({ dur: dur * 0.9, gain: gain * 0.6, f: 2200, q: 0.7, attack: 0.3 });
    noise({ dur: dur * 0.8, gain: gain * 0.5, type: 'lowpass', f: 400, attack: 0.2 });
  }

  function goal() {
    if (!ok()) return;
    const chord = [233, 294, 349, 466];
    for (const [t, d] of [[0, 0.75], [0.85, 1.6]]) {
      for (const f of chord) tone({ type: 'sawtooth', f, t, dur: d, gain: 0.09, attack: 0.03, lp: 1400 });
    }
    roar(4.2, 0.7);
  }

  function fanfare() {
    if (!ok()) return;
    const notes = [[523, 0, 0.14], [659, 0.13, 0.14], [784, 0.26, 0.14], [1047, 0.39, 0.6]];
    for (const [f, t, d] of notes) {
      tone({ type: 'square', f, t, dur: d, gain: 0.08, lp: 2600 });
      tone({ type: 'triangle', f: f / 2, t, dur: d, gain: 0.12 });
    }
    noise({ t: 0.39, dur: 1.2, gain: 0.18, f: 1200, q: 0.6, attack: 0.15 });
  }

  function mega() {
    if (!ok()) return;
    tone({ type: 'sawtooth', f: 180, f2: 1400, dur: 0.7, gain: 0.08, attack: 0.05, lp: 3000 });
    for (const f of [523, 659, 784, 1047]) tone({ type: 'triangle', f, t: 0.7, dur: 1.3, gain: 0.1, attack: 0.02 });
    for (let i = 0; i < 6; i++) tone({ type: 'sine', f: 1568 + i * 220, t: 0.75 + i * 0.05, dur: 0.25, gain: 0.04 });
    roar(2.8, 0.45);
  }

  function ooh() {
    if (!ok()) return;
    noise({ dur: 1.3, gain: 0.35, f: 650, f2: 280, q: 2.5, attack: 0.15 });
  }

  function charge() {
    if (!ok()) return;
    tone({ type: 'sawtooth', f: 220, f2: 880, dur: 0.45, gain: 0.07, lp: 2000 });
    tone({ type: 'square', f: 880, t: 0.45, dur: 0.18, gain: 0.06, lp: 3000 });
  }

  function promote() {
    if (!ok()) return;
    tone({ type: 'triangle', f: 784, dur: 0.12, gain: 0.12 });
    tone({ type: 'triangle', f: 1175, t: 0.1, dur: 0.25, gain: 0.12 });
  }

  function sad() {
    if (!ok()) return;
    [392, 370, 349, 311].forEach((f, i) =>
      tone({ type: 'triangle', f, t: i * 0.28, dur: i === 3 ? 0.7 : 0.26, gain: 0.1, lp: 1500 }),
    );
  }

  function tick(final = false) {
    if (!ok()) return;
    tone({ type: 'square', f: final ? 1760 : 1100, dur: final ? 0.4 : 0.06, gain: 0.08, lp: 3500 });
  }

  // ---------- tribün uğultusu (maç boyunca) ----------
  function setCrowd(level) {
    crowdLevel = level;
    if (!ok()) return;
    if (!crowd) {
      const s = ctx.createBufferSource();
      s.buffer = noiseBuf;
      s.loop = true;
      const bp = ctx.createBiquadFilter();
      bp.type = 'bandpass';
      bp.frequency.value = 700;
      bp.Q.value = 0.6;
      const lp = ctx.createBiquadFilter();
      lp.type = 'lowpass';
      lp.frequency.value = 1800;
      const g = ctx.createGain();
      g.gain.value = 0;
      s.connect(bp);
      bp.connect(lp);
      lp.connect(g);
      g.connect(master);
      s.start();
      crowd = { g, bp };
      setInterval(() => {
        if (!crowd) return;
        const target = crowdLevel * (0.8 + Math.random() * 0.4);
        crowd.g.gain.linearRampToValueAtTime(target, ctx.currentTime + 1.5);
        crowd.bp.frequency.linearRampToValueAtTime(600 + Math.random() * 300, ctx.currentTime + 1.5);
      }, 1500);
    }
    crowd.g.gain.linearRampToValueAtTime(level, ctx.currentTime + 1);
  }

  // ---------- Türkçe sesli anons ----------
  let voice = null;
  function pickVoice() {
    const vs = speechSynthesis.getVoices();
    voice = vs.find((v) => /^tr/i.test(v.lang)) || null;
  }
  if (ttsOn) {
    pickVoice();
    speechSynthesis.onvoiceschanged = pickVoice;
  }
  const cleanName = (n) =>
    String(n || '')
      .replace(/[^\p{L}\p{N} ]+/gu, ' ')
      .replace(/\s+/g, ' ')
      .trim()
      .slice(0, 22) || 'biri';
  let queued = 0;
  function say(text, urgent = false) {
    if (!ttsOn) return;
    if (urgent) {
      speechSynthesis.cancel();
      queued = 0;
    } else if (queued >= 2) return;
    const u = new SpeechSynthesisUtterance(text);
    u.lang = 'tr-TR';
    if (voice) u.voice = voice;
    u.rate = 1.08;
    u.pitch = 1.05;
    queued++;
    u.onend = u.onerror = () => {
      queued = Math.max(0, queued - 1);
    };
    speechSynthesis.speak(u);
  }

  // ---------- konfeti ----------
  let canvas = null;
  let c2d = null;
  let parts = [];
  function confetti(colors = ['#ffc531', '#ffffff', '#e53935', '#1e88e5'], n = 170, x = 540, y = 900) {
    if (!canvas) {
      canvas = document.createElement('canvas');
      canvas.width = 1080;
      canvas.height = 1920;
      canvas.style.cssText = 'position:absolute;inset:0;pointer-events:none;z-index:30';
      document.getElementById('stage').appendChild(canvas);
      c2d = canvas.getContext('2d');
    }
    const wasIdle = parts.length === 0;
    for (let i = 0; i < n; i++) {
      const a = Math.random() * Math.PI * 2;
      const sp = 12 + Math.random() * 26;
      parts.push({
        x, y,
        vx: Math.cos(a) * sp,
        vy: Math.sin(a) * sp - 14,
        w: 10 + Math.random() * 10,
        h: 6 + Math.random() * 6,
        r: Math.random() * 6,
        vr: (Math.random() - 0.5) * 0.4,
        c: colors[i % colors.length],
        life: 110 + Math.random() * 60,
      });
    }
    if (wasIdle) requestAnimationFrame(frame);
  }
  function frame() {
    c2d.clearRect(0, 0, 1080, 1920);
    for (const p of parts) {
      p.vy += 0.75;
      p.vx *= 0.985;
      p.x += p.vx;
      p.y += p.vy;
      p.r += p.vr;
      p.life--;
      c2d.save();
      c2d.globalAlpha = Math.min(1, p.life / 30);
      c2d.translate(p.x, p.y);
      c2d.rotate(p.r);
      c2d.fillStyle = p.c;
      c2d.fillRect(-p.w / 2, -p.h / 2, p.w, p.h);
      c2d.restore();
    }
    parts = parts.filter((p) => p.life > 0 && p.y < 2000);
    if (parts.length) requestAnimationFrame(frame);
    else c2d.clearRect(0, 0, 1080, 1920);
  }

  function shake() {
    const el = document.getElementById('pitch');
    el.classList.remove('shake');
    void el.offsetWidth;
    el.classList.add('shake');
  }

  return { setupUnlock, coin, whistle, roar, goal, fanfare, mega, ooh, charge, promote, sad, tick, setCrowd, say, cleanName, confetti, shake };
})();
