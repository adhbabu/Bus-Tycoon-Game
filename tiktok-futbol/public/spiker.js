// Spiker: yayında sürekli konuşan sanal anlatıcı.
// Oyunun o anki gerçek durumundan cümle kurar (boş mevkiler, kaptanın jetonu, skor, atak barı,
// geri sayım) ve yeni gelen izleyicileri adıyla karşılayıp oyuna davet eder.
// Söylediği her şey ekranda altyazı olarak da görünür (sesi kapalı izleyenler için).
// Adres ayarları: ?spiker=0 (kapalı)  ?sik=9 (iki cümle arası sessizlik, saniye)
window.Spiker = (() => {
  const params = new URLSearchParams(location.search);
  const enabled = params.get('spiker') !== '0';
  const gapMs = Math.max(3, Number(params.get('sik') ?? 10)) * 1000;

  let getState = () => null;
  let getCountdown = () => 0;
  let lastBig = 0;
  let lastLineEnd = Date.now() - gapMs + 4000;
  let busyToken = 0;
  let lastGreet = 0;
  const recent = [];
  const viewers = [];

  const fmt = (n) => Number(n || 0).toLocaleString('tr-TR');
  const sn = (t) => t.name; // takım adı
  const toSay = (s) =>
    s
      .replace(/\p{Extended_Pictographic}|️|‍/gu, '')
      .replace(/[_.@#*|~^=+<>[\]{}]+/g, ' ')
      .replace(/\s+/g, ' ')
      .trim();
  const time = (sec) => {
    const m = Math.floor(sec / 60);
    const s = sec % 60;
    if (m && s) return `${m} dakika ${s} saniye`;
    if (m) return `${m} dakika`;
    return `${s} saniye`;
  };

  // ---------- cümle havuzu ----------
  function candidates(st) {
    const [A, B] = st.teams;
    const m = st.match;
    const out = [];
    const add = (key, w, text, team = null) => out.push({ key, w, text, team });
    const cap = (t) => t.starters.find((s) => s.captain)?.player ?? null;
    const empty = (t) => t.starters.filter((s) => !s.player || s.player.bot).length;
    const bots = (t) => t.starters.filter((s) => s.player?.bot).length;
    const phase =
      m.status === 'running' || m.status === 'paused'
        ? 'match'
        : m.status === 'finished' || st.auto?.phase === 'break'
          ? 'break'
          : 'lobby';

    if (phase === 'lobby') {
      for (const t of [A, B]) {
        const e = empty(t);
        const c = cap(t);
        if (bots(t) > 0) {
          add('bos' + t.key, 3, `🤖 ${sn(t)} takımında ${bots(t)} mevkide bot oynuyor. Bir hediye at, botun yerini sen al!`, t.key);
          add('eksik' + t.key, 1.5, `${sn(t)} botlarla mı sahaya çıkacak? Gerçek oyuncular nerede?`, t.key);
        } else if (e > 0) {
          add('bos' + t.key, 3, `${sn(t)} takımında ${e} forma boş! Tek bir hediyeyle sahaya çıkabilirsin.`, t.key);
          add('eksik' + t.key, 1.5, `${sn(t)} eksik kadroyla mı çıkacak? ${e} oyuncu daha lazım!`, t.key);
        }
        if (c) {
          add('kaptan' + t.key, 3, `👑 ${sn(t)} kaptanı ${c.name}, ${fmt(c.coins)} jetonla tahtta. ${fmt(c.coins + 1)} jeton atan kaptanlığı alır!`, t.key);
        } else {
          add('kaptanyok' + t.key, 4, `👑 ${sn(t)} takımının henüz kaptanı yok! İlk hediyeyi atan kaptan ve santrfor olur!`, t.key);
        }
        if (e === 0) {
          const min = Math.min(...t.starters.map((s) => s.player.coins));
          add('sonsira' + t.key, 2, `${sn(t)} ilk on biri dolu! Son sıradaki oyuncunun ${fmt(min)} jetonu var, onu geçen sahaya çıkar.`, t.key);
        }
      }
      if (A.totalCoins || B.totalCoins) {
        const [lead, trail] = A.totalCoins >= B.totalCoins ? [A, B] : [B, A];
        const diff = lead.totalCoins - trail.totalCoins;
        if (diff > 0) add('fark', 2, `${sn(lead)} ${fmt(diff)} jeton önde. ${sn(trail)} taraftarları, sesinizi duyurun!`, trail.key);
        else add('denk', 2, 'İki takım başa baş! Dengeyi bozacak kişi sen olabilirsin.');
      }
      const sec = getCountdown();
      if (sec > 0) add('sayac', sec <= 60 ? 5 : 2.5, `⏳ Maçın başlamasına ${time(sec)} var! Kadroda yerini şimdi al.`);
      add('yorum', 3, `💬 Yorumlara ${A.keyword} yaz ${sn(A)}, ${B.keyword} yaz ${sn(B)}. Takımını seç!`);
      add('kural', 1.5, 'Kural basit: hediye at, takıma gir. En çok destek veren kaptan olur, sahada en önemli yeri alır!');
    }

    if (phase === 'match') {
      const min = m.minute;
      for (const t of [A, B]) {
        const v = m.meter?.[t.key] ?? 0;
        if (v >= 50 && v < 100) add('bar' + t.key, v / 20, `⚡ ${sn(t)} atak barı yüzde ${v}! Biraz daha destek, atak geliyor!`, t.key);
        const st9 = t.starters.find((s) => s.pos.key === 'ST')?.player;
        if (st9 && !st9.bot) add('forvet' + t.key, 1.5, `${sn(t)} santrforu ${st9.name} gol kokluyor! Atak barını doldurun!`, t.key);
        if (bots(t) > 0) add('macbot' + t.key, 1.5, `🤖 ${sn(t)} sahada ${bots(t)} botla oynuyor. Hediye at, botu kenara al, oyuna sen gir!`, t.key);
        const c = cap(t);
        if (c) add('mackaptan' + t.key, 1, `👑 ${sn(t)} kaptanı ${c.name} sahada. Tahtı almak isteyen var mı?`, t.key);
      }
      const { A: a, B: b } = m.score;
      if (a !== b) {
        const [lead, trail] = a > b ? [A, B] : [B, A];
        add('skor', 3, `Dakika ${min}. ${sn(lead)} ${Math.max(a, b)}-${Math.min(a, b)} önde! ${sn(trail)} taraftarları, takımınızı geri getirin!`, trail.key);
      } else {
        add('esit', 3, `Dakika ${min}, skor ${a}-${b}. Bu maçı taraftarlar kazanacak!`);
      }
      if (min >= 75) add('son', 4, 'Son dakikalara giriyoruz! Her hediye bir gol şansı!');
    }

    if (phase === 'break') {
      const r = m.result;
      if (r) {
        const w = r.winner ? st.teams.find((t) => t.key === r.winner) : null;
        add('sonuc', 3, `${w ? `🏆 ${w.name} kazandı!` : '🤝 Maç berabere bitti!'}${r.mvp ? ` Maçın yıldızı ${r.mvp.name}!` : ''}`, r.winner);
      }
      add('yenimac', 3, 'Yeni maç birazdan başlıyor. Kaptanlık yarışına şimdiden katıl!');
    }

    // her zaman: ücretsiz katılım çağrıları ve teşekkür
    add('paylas', 1.2, '📣 Arkadaşlarını çağır, yayını paylaş! Kalabalık ne kadar büyükse maç o kadar büyük!');
    add('begeni', 1.2, '❤️ Ekrana dokun, beğenileri yağdır, stadyum ısınsın!');
    const top = [...A.starters, ...B.starters, ...A.bench, ...B.bench]
      .map((s) => s.player ?? s)
      .filter((p) => p && p.coins)
      .sort((x, y) => y.coins - x.coins)[0];
    if (top) add('tesekkur', 1.2, `🙏 ${top.name}, ${fmt(top.coins)} jetonla yayının en büyük destekçisi. Teşekkürler!`);
    return out;
  }

  function pick(list) {
    const fresh = list.filter((c) => !recent.includes(c.key));
    const pool = fresh.length ? fresh : list;
    const total = pool.reduce((a, c) => a + c.w, 0);
    let r = Math.random() * total;
    for (const c of pool) if ((r -= c.w) <= 0) return c;
    return pool[0];
  }

  function nextLine(st, now) {
    if (viewers.length && now - lastGreet > 14000) {
      const people = viewers.splice(0);
      lastGreet = now;
      const shown = people.slice(-3);
      const more = people.length - shown.length;
      return {
        key: 'selam',
        faces: shown,
        text: `👋 Hoş geldin ${shown.map((p) => p.name).join(', ')}${more > 0 ? ` ve ${more} kişi daha` : ''}! Yorumlara 1 ya da 2 yaz, takımını seç!`,
      };
    }
    const list = candidates(st);
    if (!list.length) return null;
    const c = pick(list);
    recent.push(c.key);
    if (recent.length > 6) recent.shift();
    return c;
  }

  // ---------- altyazı ----------
  const box = document.getElementById('spiker');
  const bars = '<i></i><i></i><i></i><i></i>';
  function show(text, team, faces) {
    if (!box) return;
    box.querySelector('.sp-t').textContent = text;
    // karşılamada konuşan dalga yerine gelenlerin profil fotoğrafları görünür
    const ic = box.querySelector('.sp-ic');
    if (faces?.length) {
      ic.className = 'sp-ic faces';
      ic.innerHTML = '';
      for (const p of faces) {
        const s = document.createElement('span');
        if (p.avatar) s.style.backgroundImage = `url("${p.avatar}")`;
        ic.appendChild(s);
      }
    } else if (ic.classList.contains('faces')) {
      ic.className = 'sp-ic';
      ic.innerHTML = bars;
    }
    box.style.setProperty('--c', team === 'A' ? 'var(--A)' : team === 'B' ? 'var(--B)' : 'var(--gold)');
    box.classList.add('on');
  }
  function hide() {
    box?.classList.remove('on');
  }

  function say(line) {
    const token = ++busyToken;
    show(line.text, line.team, line.faces);
    const done = () => {
      if (token !== busyToken) return;
      busyToken++;
      lastLineEnd = Date.now();
      setTimeout(hide, 600);
    };
    const started = FX.speak(toSay(line.text), { onend: done, rate: 1.04 });
    // ses yoksa (OBS, ?tts=0) altyazı okunabilecek kadar ekranda kalır
    setTimeout(done, started ? 15000 : Math.min(9000, 1800 + line.text.length * 60));
  }

  function loop() {
    if (!enabled) return;
    setInterval(() => {
      const now = Date.now();
      if (busyToken % 2 === 1) return; // konuşuyor
      if (now - lastLineEnd < gapMs) return;
      if (now - lastBig < 4000) return; // gol/kaptan duyurusunun üstüne konuşma
      if (FX.isSpeaking()) return;
      const st = getState();
      if (!st) return;
      const line = nextLine(st, now);
      if (line) say(line);
    }, 400);
  }

  return {
    start(opts) {
      getState = opts.getState;
      getCountdown = opts.getCountdown ?? (() => 0);
      loop();
    },
    onEvent(ev) {
      // önemli anların üstüne konuşma (sıradan mevki değişimleri spikeri susturmaz)
      if (ev.big && ['goal', 'captain', 'end', 'kickoff', 'mega', 'countdown', 'lobby', 'half'].includes(ev.type)) lastBig = Date.now();
      if (ev.type === 'viewer' && ev.name) {
        viewers.push({ name: ev.name, avatar: ev.avatar || '' });
        if (viewers.length > 10) viewers.shift();
      }
    },
  };
})();
