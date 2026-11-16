// Ses ve görsel efektler.
// Tüm sesler Web Audio ile anında üretilir (ses dosyası gerekmez).
// Adres çubuğu ayarları:
//   ?ses=0   tüm sesler kapalı        ?ses=az  sadece önemli anlar (gol, düdük, kaptan) + spiker
//   ?tts=0   sesli anons/spiker kapalı ?vol=0.4 ses düzeyi (0-1.5)   ?sesadi=Emel  Türkçe ses seçimi
window.FX = (() => {
  const params = new URLSearchParams(location.search);
  const hasTTS = 'speechSynthesis' in window && typeof SpeechSynthesisUtterance !== 'undefined';
  const ttsWanted = params.get('tts') !== '0';
  const ttsOn = ttsWanted && hasTTS;
  // Sunucuda yapay zeka sesi (OpenAI / ElevenLabs) tanımlıysa spiker onu kullanır; yoksa cihazın sesi.
  let serverVoice = false;
  const volume = Math.min(1.5, Math.max(0, Number(params.get('vol') ?? 0.55)));
  // 2 = tam, 1 = az (yalnızca önemli sesler), 0 = kapalı
  let level = params.get('ses') === '0' ? 0 : params.get('ses') === 'az' ? 1 : 2;

  let ctx = null;
  let master = null;
  let noiseBuf = null;
  let crowd = null;
  let crowdLevel = 0;
  let voiceGain = null;

  function init() {
    if (ctx) return;
    const AC = window.AudioContext || window.webkitAudioContext;
    if (!AC) return;
    ctx = new AC();
    master = ctx.createGain();
    master.gain.value = level ? volume : 0;
    const comp = ctx.createDynamicsCompressor();
    comp.threshold.value = -18;
    comp.ratio.value = 4;
    master.connect(comp);
    comp.connect(ctx.destination);
    voiceGain = ctx.createGain();
    voiceGain.gain.value = 1.8; // spiker efektlerden önde duyulsun
    voiceGain.connect(master);
    noiseBuf = ctx.createBuffer(1, ctx.sampleRate * 2, ctx.sampleRate);
    const d = noiseBuf.getChannelData(0);
    for (let i = 0; i < d.length; i++) d[i] = Math.random() * 2 - 1;
  }

  // kind: 'minor' (jeton, tribün, ooh...) ya da 'major' (gol, düdük, kaptan)
  function ok(kind = 'minor') {
    if (level === 0 || (level === 1 && kind === 'minor')) return false;
    init();
    if (!ctx) return false;
    if (ctx.state === 'suspended') ctx.resume().catch(() => {});
    return ctx.state === 'running';
  }

  // Aynı ses art arda çalıp kulak tırmalamasın
  const lastPlay = {};
  function gate(name, ms) {
    const now = performance.now();
    if (now - (lastPlay[name] ?? -1e9) < ms) return false;
    lastPlay[name] = now;
    return true;
  }

  // ---------- temel sentez parçaları ----------
  function tone({ type = 'sine', f = 440, f2 = null, t = 0, dur = 0.2, gain = 0.3, attack = 0.008, lp = null }) {
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
    g.connect(master);
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

  // ---------- oyun sesleri (yumuşak tonlar) ----------
  function coin(coins = 1) {
    // küçük hediyeler en fazla 1,5 sn'de bir, büyükler daha sık duyulur
    if (!gate('coin', coins >= 30 ? 400 : 1500) || !ok('minor')) return;
    const notes = coins >= 100 ? [1047, 1319, 1568, 2093] : coins >= 20 ? [1047, 1319, 1568] : coins >= 5 ? [1175, 1568] : [1319];
    notes.forEach((f, i) => {
      tone({ type: 'sine', f, t: i * 0.08, dur: 0.35, gain: 0.07 });
      tone({ type: 'triangle', f: f / 2, t: i * 0.08, dur: 0.25, gain: 0.025 });
    });
  }

  function whistle(blasts = 1, long = false) {
    if (!ok('major')) return;
    let t = 0;
    for (let i = 0; i < blasts; i++) {
      const dur = long && i === blasts - 1 ? 0.9 : 0.28;
      const start = ctx.currentTime + t;
      const o = ctx.createOscillator();
      const lfo = ctx.createOscillator();
      const lfoG = ctx.createGain();
      const g = ctx.createGain();
      o.type = 'sine';
      o.frequency.value = 2600;
      lfo.frequency.value = 30;
      lfoG.gain.value = 120;
      lfo.connect(lfoG);
      lfoG.connect(o.frequency);
      g.gain.setValueAtTime(0.0001, start);
      g.gain.exponentialRampToValueAtTime(0.12, start + 0.03);
      g.gain.setValueAtTime(0.12, start + dur - 0.06);
      g.gain.exponentialRampToValueAtTime(0.0001, start + dur);
      o.connect(g);
      g.connect(master);
      o.start(start);
      lfo.start(start);
      o.stop(start + dur + 0.05);
      lfo.stop(start + dur + 0.05);
      t += dur + 0.14;
    }
  }

  // tribün coşkusu: alçak, sıcak bir uğultu (tıslama yok)
  function roar(dur = 3, gain = 0.4, force = false) {
    if (!force && !gate('roar', 9000)) return;
    if (!ok(force ? 'major' : 'minor')) return;
    noise({ dur, gain, type: 'lowpass', f: 520, q: 0.3, attack: 0.35 });
    noise({ dur: dur * 0.85, gain: gain * 0.45, f: 750, q: 0.6, attack: 0.4 });
  }

  function goal() {
    if (!ok('major')) return;
    const chord = [262, 330, 392];
    for (const [t, d] of [[0, 0.6], [0.7, 1.3]]) {
      for (const f of chord) tone({ type: 'triangle', f, t, dur: d, gain: 0.08, attack: 0.04, lp: 1200 });
    }
    roar(3.8, 0.55, true);
  }

  function fanfare() {
    if (!gate('fanfare', 2500) || !ok('major')) return;
    const notes = [[523, 0, 0.16], [659, 0.15, 0.16], [784, 0.3, 0.16], [1047, 0.45, 0.6]];
    for (const [f, t, d] of notes) {
      tone({ type: 'triangle', f, t, dur: d, gain: 0.09 });
      tone({ type: 'sine', f: f * 2, t, dur: d * 0.8, gain: 0.025 });
    }
  }

  function mega() {
    if (!gate('mega', 3000) || !ok('major')) return;
    tone({ type: 'triangle', f: 330, f2: 990, dur: 0.6, gain: 0.07, attack: 0.05, lp: 2000 });
    for (const f of [523, 659, 784, 1047]) tone({ type: 'sine', f, t: 0.6, dur: 1.2, gain: 0.06, attack: 0.02 });
    roar(2.4, 0.35, true);
  }

  function ooh() {
    if (!gate('ooh', 7000) || !ok('minor')) return;
    noise({ dur: 1.2, gain: 0.22, f: 520, f2: 260, q: 2, attack: 0.18 });
  }

  function charge() {
    if (!gate('charge', 3000) || !ok('minor')) return;
    tone({ type: 'sine', f: 330, f2: 990, dur: 0.45, gain: 0.06 });
  }

  function promote() {
    if (!gate('promote', 5000) || !ok('minor')) return;
    tone({ type: 'sine', f: 784, dur: 0.18, gain: 0.07 });
    tone({ type: 'sine', f: 1175, t: 0.12, dur: 0.3, gain: 0.07 });
  }

  function sad() {
    if (!gate('sad', 20000) || !ok('minor')) return;
    [392, 370, 349].forEach((f, i) => tone({ type: 'sine', f, t: i * 0.26, dur: i === 2 ? 0.55 : 0.24, gain: 0.06 }));
  }

  function tick(final = false) {
    if (!ok('minor')) return;
    tone({ type: 'sine', f: final ? 1320 : 990, dur: final ? 0.3 : 0.07, gain: 0.06 });
  }

  // ---------- tribün uğultusu (yalnızca maç sırasında, kısık) ----------
  function setCrowd(lvl) {
    crowdLevel = lvl;
    if (!crowd && !ok('minor')) return;
    if (!crowd) {
      const s = ctx.createBufferSource();
      s.buffer = noiseBuf;
      s.loop = true;
      const lp = ctx.createBiquadFilter();
      lp.type = 'lowpass';
      lp.frequency.value = 480;
      lp.Q.value = 0.2;
      const g = ctx.createGain();
      g.gain.value = 0;
      s.connect(lp);
      lp.connect(g);
      g.connect(master);
      s.start();
      crowd = { g, lp };
      setInterval(() => {
        const target = (level < 2 ? 0 : crowdLevel) * (0.85 + Math.random() * 0.3);
        crowd.g.gain.linearRampToValueAtTime(target, ctx.currentTime + 2);
        crowd.lp.frequency.linearRampToValueAtTime(420 + Math.random() * 160, ctx.currentTime + 2);
      }, 2000);
    }
    crowd.g.gain.linearRampToValueAtTime(level < 2 ? 0 : lvl, ctx.currentTime + 1);
  }

  // ---------- Türkçe sesli anons ----------
  // En doğal Türkçe sesi seç: Edge'deki "Emel Online (Natural)", iPhone'daki "Gelişmiş/Premium" sesler önce gelir.
  let voice = null;
  const wantedVoice = (params.get('sesadi') || '').toLocaleLowerCase('tr');
  function pickVoice() {
    const vs = speechSynthesis.getVoices().filter((v) => /^tr/i.test(v.lang));
    const score = (v) =>
      (wantedVoice && v.name.toLocaleLowerCase('tr').includes(wantedVoice) ? 100 : 0) +
      (/natural|neural|online/i.test(v.name) ? 8 : 0) +
      (/premium|enhanced|geli[sş]mi[sş]/i.test(v.name) ? 6 : 0) +
      (/google/i.test(v.name) ? 3 : 0) +
      (v.localService ? 0 : 1);
    voice = vs.sort((a, b) => score(b) - score(a))[0] || null;
  }
  if (hasTTS) {
    pickVoice();
    speechSynthesis.onvoiceschanged = pickVoice;
  }

  const cleanName = (n) =>
    String(n || '')
      .replace(/[^\p{L}\p{N} ]+/gu, ' ')
      .replace(/\s+/g, ' ')
      .trim()
      .slice(0, 22) || 'biri';

  // 'unknown' | 'ok' | 'broken': cihaz gerçekten konuşabiliyor mu?
  let ttsState = ttsOn ? 'unknown' : 'broken';
  let current = 0; // sırada/çalan konuşma sayısı
  const listeners = [];
  const setTtsState = (s) => {
    if (ttsState === s) return;
    ttsState = s;
    listeners.forEach((f) => f(s));
  };

  // Konuşmayı başlatır; başladıysa true döner. onend konuşma bitince (ya da kesilince/takılınca) çağrılır.
  // iPhone'da konuşma bazen "bitti" sinyali vermeden takılır; bu yüzden her cümlenin bir süre sınırı var.
  // ---------- sunucudaki yapay zeka sesi ----------
  let chain = Promise.resolve();
  let voiceGen = 0;
  let currentSrc = null;
  function duck(on) {
    if (crowd && ctx) crowd.g.gain.linearRampToValueAtTime((level < 2 ? 0 : crowdLevel) * (on ? 0.35 : 1), ctx.currentTime + 0.3);
  }
  function stopVoice() {
    voiceGen++;
    chain = Promise.resolve();
    if (currentSrc) {
      try {
        currentSrc.stop();
      } catch {
        /* zaten durdu */
      }
      currentSrc = null;
    }
    current = 0;
  }
  function speakServer(text, { urgent = false, onend = null, style = 'info' } = {}) {
    if (urgent) stopVoice();
    else if (current >= 2) return false;
    current++;
    const gen = voiceGen;
    let done = false;
    const finish = () => {
      if (done) return;
      done = true;
      current = Math.max(0, current - 1);
      duck(false);
      if (onend) onend();
    };
    // ses hemen istenir, sıra gelince çalınır
    const clip = fetch('tts?s=' + style + '&t=' + encodeURIComponent(text.slice(0, 260)))
      .then((r) => (r.ok ? r.arrayBuffer() : Promise.reject(new Error('tts ' + r.status))))
      .then((buf) => ctx.decodeAudioData(buf));
    chain = chain
      .then(() => clip)
      .then(
        (buf) =>
          new Promise((res) => {
            if (gen !== voiceGen || level === 0) {
              finish();
              return res();
            }
            const s = ctx.createBufferSource();
            s.buffer = buf;
            s.connect(voiceGain);
            currentSrc = s;
            duck(true);
            setTtsState('ok');
            s.onended = () => {
              if (currentSrc === s) currentSrc = null;
              finish();
              res();
            };
            s.start();
          }),
      )
      .catch(() => finish());
    setTimeout(finish, 25000 + text.length * 80);
    return true;
  }
  fetch('tts/status')
    .then((r) => (r.ok ? r.json() : null))
    .then((j) => {
      if (!j?.provider || !ttsWanted) return;
      serverVoice = true;
      if (ctx && ctx.state === 'running') setTtsState('ok');
      else if (ttsState === 'broken') setTtsState('unknown');
    })
    .catch(() => {});

  const alive = new Set(); // Chrome konuşma nesnesini erken silerse "bitti" sinyali gelmez; referans tutulur
  function speak(text, { urgent = false, onend = null, rate = 1.02, style = 'info' } = {}) {
    if (serverVoice && ttsWanted && level > 0 && ctx && text) return speakServer(text, { urgent, onend, style });
    if (!ttsOn || level === 0 || !text) return false;
    if (!speechSynthesis.speaking && !speechSynthesis.pending) current = 0; // takılı sayaç temizlenir
    let cancelled = false;
    if (urgent) {
      if (speechSynthesis.speaking || speechSynthesis.pending) {
        speechSynthesis.cancel();
        cancelled = true;
      }
      current = 0;
    } else if (current >= 2) return false;
    try {
      speechSynthesis.resume(); // iOS bazen "duraklatılmış" durumda kalır
    } catch {
      /* yok say */
    }
    const u = new SpeechSynthesisUtterance(text);
    u.lang = 'tr-TR';
    if (voice) u.voice = voice;
    u.rate = rate;
    u.pitch = 1;
    current++;
    let done = false;
    let watchdog = null;
    const finish = () => {
      if (done) return;
      done = true;
      current = Math.max(0, current - 1);
      clearTimeout(watchdog);
      alive.delete(u);
      if (onend) onend();
    };
    u.onstart = () => setTtsState('ok');
    u.onend = finish;
    u.onerror = finish;
    alive.add(u);
    watchdog = setTimeout(finish, 2500 + text.length * 110);
    // iPhone: cancel()'dan hemen sonra gelen konuşma bazen hiç okunmaz; kısa bir ara verilir
    if (cancelled) setTimeout(() => speechSynthesis.speak(u), 80);
    else speechSynthesis.speak(u);
    return true;
  }
  const say = (text, urgent = false) => speak(text, { urgent });
  const isSpeaking = () => level > 0 && (current > 0 || (ttsOn && speechSynthesis.speaking));

  // Tarayıcılar sese ancak ilk dokunuştan sonra izin verir (OBS'te sorun yok).
  // iPhone'da sesli anons da ilk dokunuşta, dokunuşun içinde başlatılmalı.
  // Spiker gerçekten konuşmaya başlayana kadar her dokunuşta yeniden denenir.
  // iPhone yalnızca parmak kalkınca (touchend/click) gelen dokunuşu sayar ve konuşma
  // o dokunuşun içinde, öncesinde cancel() çağrılmadan başlatılmalıdır.
  let unlockTries = 0;
  let unlockTimer = null;
  let greeted = false;
  function unlock() {
    init();
    const p = ctx ? ctx.resume().catch(() => {}) : Promise.resolve();
    if (serverVoice && ttsWanted && level > 0) {
      return p.then(() => {
        if (ctx.state !== 'running') return;
        setTtsState('ok');
        if (!greeted) {
          greeted = true;
          speak('Merhaba! Canlı Maç Oyna yayınına hoş geldiniz. Takımını seç, maça hazır ol!');
        }
      });
    }
    if (ttsOn && level > 0 && ttsState !== 'ok') {
      unlockTries++;
      const u = new SpeechSynthesisUtterance(unlockTries === 1 ? 'Merhaba! Canlı Maç Oyna yayınına hoş geldiniz.' : 'Spiker hazır.');
      u.lang = 'tr-TR';
      if (voice) u.voice = voice;
      u.rate = 1.02;
      u.onstart = () => setTtsState('ok');
      u.onend = u.onerror = () => alive.delete(u);
      alive.add(u);
      try {
        speechSynthesis.resume();
      } catch {
        /* yok say */
      }
      speechSynthesis.speak(u);
      clearTimeout(unlockTimer);
      unlockTimer = setTimeout(() => {
        if (ttsState !== 'ok') setTtsState('broken');
      }, 3000);
    }
    return p;
  }

  function setupUnlock() {
    if (level === 0) return;
    init();
    const btn = document.createElement('button');
    btn.type = 'button';
    btn.style.cssText =
      'position:fixed;left:50%;top:50%;transform:translate(-50%,-50%);z-index:99;font:800 20px Inter,sans-serif;' +
      'max-width:86vw;padding:16px 24px;border-radius:16px;border:3px solid #fff;background:#ffc531;color:#2a1e00;cursor:pointer;display:none';
    document.body.appendChild(btn);
    let gaveUp = false;
    const needAudio = () => ctx && ctx.state !== 'running';
    const needTts = () => ttsWanted && level > 0 && (serverVoice || ttsOn) && ttsState !== 'ok';
    const check = () => {
      if (level === 0 || (!needAudio() && !needTts())) {
        btn.style.display = 'none';
        return;
      }
      if (gaveUp && !needAudio()) {
        btn.style.display = 'none';
        return;
      }
      if (ttsState === 'broken' && unlockTries >= 2 && !needAudio()) {
        btn.textContent = "🎙 Spiker sesi bu tarayıcıda çalışmıyor. Safari'de aç, sessiz modu kapat. (Altyazılar görünür.)";
        btn.style.display = 'block';
        setTimeout(() => {
          gaveUp = true;
          check();
        }, 7000);
        return;
      }
      btn.textContent = unlockTries ? '🔊 Spikeri açmak için tekrar dokun' : '🔊 Sesi ve spikeri açmak için dokun';
      btn.style.display = 'block';
    };
    // tek dokunuş hem touchend hem click üretir; ikisi bir deneme sayılır
    let lastTap = 0;
    const onTap = () => {
      const now = Date.now();
      if (now - lastTap < 800) return;
      lastTap = now;
      if (needAudio() || needTts()) unlock().then(() => setTimeout(check, 300));
    };
    document.addEventListener('click', onTap, true);
    document.addEventListener('touchend', onTap, { capture: true, passive: true });
    listeners.push(() => setTimeout(check, 50));
    if (ctx) ctx.onstatechange = check;
    setTimeout(check, 600);
  }

  function setLevel(n) {
    level = n;
    if (n === 0) {
      stopVoice();
      if (hasTTS) speechSynthesis.cancel();
      current = 0;
    }
    if (n > 0) unlock();
    if (master) master.gain.value = n ? volume : 0;
    if (crowd) crowd.g.gain.value = n < 2 ? 0 : crowdLevel;
  }

  // ---------- konfeti ----------
  let canvas = null;
  let c2d = null;
  let parts = [];
  function confetti(colors = ['#ffc531', '#ffffff', '#e53935', '#1e88e5'], n = 170, x = 540, y = 1040) {
    if (matchMedia('(prefers-reduced-motion: reduce)').matches) return;
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

  return {
    ttsAvailable: () => ttsWanted && level > 0 && (serverVoice || ttsOn),
    voiceSource: () => (serverVoice ? 'sunucu' : ttsOn ? 'cihaz' : 'yok'),
    ttsStatus: () => ttsState,
    onTtsStatus: (f) => listeners.push(f),
    getLevel: () => level,
    setLevel,
    speak,
    say,
    isSpeaking,
    setupUnlock,
    coin, whistle, roar, goal, fanfare, mega, ooh, charge, promote, sad, tick, setCrowd,
    cleanName, confetti, shake,
  };
})();
