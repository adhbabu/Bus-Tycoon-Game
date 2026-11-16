// Oyun mantığı: takımlar, mevkiler, kaptanlık ve maç motoru.
// TikTok'tan bağımsızdır; sunucu hediye/yorum olaylarını buraya iletir.

export const norm = (s) =>
  String(s ?? '')
    .toLocaleLowerCase('tr')
    .replace(/ı/g, 'i')
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .trim();

const log2p = (n) => Math.log2(1 + Math.max(0, n));
const MAX_FEED = 30;
const MAX_EVENTS = 40;

function freshMatch() {
  return {
    status: 'idle', // idle | running | paused | finished
    minute: 0,
    extra: 0,
    score: { A: 0, B: 0 },
    meter: { A: 0, B: 0 }, // atak barı (0-100)
    boost: { A: 0, B: 0 }, // son ataktan beri gelen hediye jetonu
    lastAttack: { A: -99, B: -99 },
    scorers: {},
    events: [],
    result: null,
  };
}

export class Game {
  constructor(config, saved = null, rng = Math.random) {
    this.cfg = config;
    this.rng = rng;
    this.positions = config.positions;
    this.teams = config.teams.map((t) => ({
      key: t.key,
      name: t.name,
      color: t.color,
      keywords: t.keywords.map(norm),
    }));
    this.players = new Map();
    this.pendingChoice = new Map();
    this.banned = new Set();
    this.feed = [];
    this.match = freshMatch();
    this.seq = 0;
    this.auto = { enabled: !!config.auto?.enabled, phase: 'lobby', endsAt: 0, warned: {} };
    this.onChange = () => {};
    this.onEvent = () => {};
    if (saved) this.load(saved);
    this.prevSlots = this.slotMap();
  }

  // ---------- kalıcılık ----------
  toJSON() {
    return {
      players: [...this.players.values()],
      banned: [...this.banned],
      teamNames: Object.fromEntries(this.teams.map((t) => [t.key, t.name])),
      feed: this.feed,
      match: this.match,
      seq: this.seq,
    };
  }

  load(s) {
    for (const p of s.players ?? []) this.players.set(p.id, p);
    for (const id of s.banned ?? []) this.banned.add(id);
    for (const t of this.teams) if (s.teamNames?.[t.key]) t.name = s.teamNames[t.key];
    this.feed = s.feed ?? [];
    if (s.match) this.match = { ...freshMatch(), ...s.match };
    if (this.match.status === 'running') this.match.status = 'paused';
    this.seq = s.seq ?? this.players.size;
  }

  // ---------- yardımcılar ----------
  team(key) {
    return this.teams.find((t) => t.key === key);
  }

  other(key) {
    return key === 'A' ? 'B' : 'A';
  }

  teamPlayers(key) {
    return [...this.players.values()]
      .filter((p) => p.team === key)
      .sort((a, b) => b.coins - a.coins || a.seq - b.seq);
  }

  // Mevkiler önem sırasına göre dizilidir. En çok jeton atan, karşıladığı
  // en önemli mevkiye yerleşir; kalanlar yedek kulübesine gider.
  lineup(key) {
    const players = this.teamPlayers(key);
    const used = new Set();
    const starters = this.positions.map((pos) => {
      const player = players.find((p) => !used.has(p.id) && p.coins >= (pos.minCoins ?? 0)) ?? null;
      if (player) used.add(player.id);
      return { pos, player };
    });
    let captainId = null;
    let best = -1;
    for (const s of starters) {
      if (s.player && s.player.coins > best) {
        best = s.player.coins;
        captainId = s.player.id;
      }
    }
    for (const s of starters) s.captain = !!s.player && s.player.id === captainId;
    return { starters, bench: players.filter((p) => !used.has(p.id)), captainId };
  }

  slotMap() {
    const map = new Map();
    for (const t of this.teams) {
      const lu = this.lineup(t.key);
      lu.starters.forEach((s, idx) => {
        if (s.player) map.set(s.player.id, { team: t.key, idx, captain: s.captain });
      });
    }
    return map;
  }

  autoTeam() {
    const stats = this.teams.map((t) => {
      const ps = this.teamPlayers(t.key);
      return { key: t.key, n: ps.length, coins: ps.reduce((a, p) => a + p.coins, 0) };
    });
    stats.sort((a, b) => a.n - b.n || a.coins - b.coins);
    return stats[0].key;
  }

  emit(ev) {
    this.onEvent({ ts: Date.now(), ...ev });
  }

  pushFeed(item) {
    this.feed.push({ ts: Date.now(), ...item });
    if (this.feed.length > MAX_FEED) this.feed.splice(0, this.feed.length - MAX_FEED);
  }

  // ---------- TikTok olayları ----------
  // g = { id, name, avatar, coins, count, giftName, icon }
  handleGift(g) {
    if (!g?.id || this.banned.has(g.id)) return;
    const coins = Math.max(0, Math.round(Number(g.coins) || 0));
    if (coins <= 0) return;

    let p = this.players.get(g.id);
    let joined = false;
    if (!p) {
      const team = this.pendingChoice.get(g.id) ?? this.autoTeam();
      this.pendingChoice.delete(g.id);
      p = { id: g.id, name: g.name || g.id, avatar: g.avatar || '', team, coins: 0, gifts: 0, seq: this.seq++ };
      this.players.set(g.id, p);
      joined = true;
    }
    if (g.name) p.name = g.name;
    if (g.avatar) p.avatar = g.avatar;
    p.coins += coins;
    p.gifts += g.count || 1;

    this.pushFeed({
      kind: 'gift',
      id: p.id,
      name: p.name,
      avatar: p.avatar,
      team: p.team,
      giftName: g.giftName || 'Hediye',
      icon: g.icon || '',
      count: g.count || 1,
      coins,
      total: p.coins,
    });
    this.emit({ type: 'gift', id: p.id, team: p.team, coins, name: p.name, giftName: g.giftName || 'Hediye' });
    if (coins >= (this.cfg.bigGift ?? 99)) {
      this.emit({
        type: 'mega',
        big: true,
        team: p.team,
        id: p.id,
        name: p.name,
        avatar: p.avatar,
        coins,
        giftName: g.giftName || 'Hediye',
        text: `💥 ${p.name} → ${g.giftName || 'Hediye'}!  +${coins} jeton`,
      });
    }
    if (joined) {
      this.emit({ type: 'join', team: p.team, id: p.id, text: `${p.name} ${this.team(p.team).name} takımına katıldı!` });
    }
    if (this.match.status === 'running') {
      const m = this.match;
      m.meter[p.team] = Math.min(100, m.meter[p.team] + (coins / (this.cfg.match?.attackCost ?? 50)) * 100);
      m.boost[p.team] += coins;
    }
    this.afterChange();
  }

  // Yorumla takım seçimi: "1", "2", "kırmızı", "mavi" (config'deki anahtar kelimeler)
  handleChat(c) {
    if (!c?.id || this.banned.has(c.id)) return;
    const word = norm(c.text)
      .replace(/[^\p{L}\p{N}]+/gu, ' ')
      .trim()
      .split(' ')[0];
    if (!word) return;
    const team = this.teams.find((t) => t.keywords.includes(word));
    if (!team) return;

    const p = this.players.get(c.id);
    if (!p) {
      if (this.pendingChoice.get(c.id) === team.key) return;
      this.pendingChoice.set(c.id, team.key);
      if (this.pendingChoice.size > 5000) this.pendingChoice.delete(this.pendingChoice.keys().next().value);
      this.pushFeed({ kind: 'choice', id: c.id, name: c.name || c.id, avatar: c.avatar || '', team: team.key });
      this.onChange();
      return;
    }
    if (p.team === team.key) return;
    if (!this.cfg.allowTeamSwitch || this.match.status === 'running') return;
    p.team = team.key;
    this.emit({ type: 'switch', team: team.key, id: p.id, text: `${p.name} ${team.name} takımına geçti!` });
    this.afterChange();
  }

  // Mevki değişikliklerini bul, duyur
  afterChange() {
    const next = this.slotMap();
    const topN = this.cfg.announceTopN ?? 5;
    const prevCaptain = {};
    for (const [id, s] of this.prevSlots) if (s.captain) prevCaptain[s.team] = id;
    for (const [id, s] of next) {
      const prev = this.prevSlots.get(id);
      const p = this.players.get(id);
      const pos = this.positions[s.idx];
      const improved = !prev || prev.team !== s.team || s.idx < prev.idx;
      if (s.captain && !(prev?.captain && prev.team === s.team)) {
        const oldId = prevCaptain[s.team];
        const old = oldId && oldId !== id ? this.players.get(oldId) : null;
        this.emit({
          type: 'captain',
          big: true,
          team: s.team,
          id,
          name: p.name,
          avatar: p.avatar,
          prevName: old?.name ?? null,
          text: old ? `👑 TAHT DEĞİŞTİ! Yeni kaptan: ${p.name}` : `👑 ${p.name} artık KAPTAN!`,
        });
      } else if (improved && s.idx < topN) {
        this.emit({ type: 'promote', big: true, team: s.team, id, name: p.name, avatar: p.avatar, text: `⚡ ${p.name} artık ${pos.name.toLocaleUpperCase('tr')}!` });
      } else if (improved && !prev && this.match.status === 'running') {
        this.emit({ type: 'sub', team: s.team, id, text: `🔄 ${p.name} oyuna girdi (${pos.name})` });
      }
    }
    for (const [id, prev] of this.prevSlots) {
      const p = this.players.get(id);
      if (!next.has(id) && p && p.team === prev.team) {
        this.emit({ type: 'bench', team: prev.team, id, text: `🪑 ${p.name} yedeğe düştü!` });
      }
    }
    this.prevSlots = next;
    this.onChange();
  }

  // ---------- yönetim ----------
  addManual({ id, name, coins, team }) {
    const key = id || norm(name).replace(/\s+/g, '_');
    if (!key) return;
    if (team && !this.players.has(key)) this.pendingChoice.set(key, team);
    this.handleGift({ id: key, name: name || key, coins, giftName: 'Elle eklendi' });
    const p = this.players.get(key);
    if (p && team && p.team !== team) {
      p.team = team;
      this.afterChange();
    }
  }

  movePlayer(id, team) {
    const p = this.players.get(id);
    if (!p || !this.team(team)) return;
    p.team = team;
    this.afterChange();
  }

  kick(id, ban = true) {
    this.players.delete(id);
    this.pendingChoice.delete(id);
    if (ban) this.banned.add(id);
    this.afterChange();
  }

  unbanAll() {
    this.banned.clear();
    this.onChange();
  }

  setTeamName(key, name) {
    const t = this.team(key);
    if (t && name?.trim()) {
      t.name = name.trim().slice(0, 28);
      this.onChange();
    }
  }

  resetAll({ silent = false } = {}) {
    this.players.clear();
    this.pendingChoice.clear();
    this.feed = [];
    this.match = freshMatch();
    this.prevSlots = new Map();
    if (this.auto.enabled) this.restartLobby();
    if (!silent) this.emit({ type: 'reset', big: true, text: 'Yeni kadro kuruluyor! Hediye at, takıma gir 🎁' });
    this.onChange();
  }

  // ---------- otomatik pilot: kadro kurma → maç → ara → yeni maç ----------
  restartLobby(now = Date.now()) {
    const a = this.auto;
    a.phase = 'lobby';
    a.endsAt = now + (this.cfg.auto?.lobbySeconds ?? 180) * 1000;
    a.warned = {};
  }

  setAuto(on) {
    this.auto.enabled = !!on;
    const st = this.match.status;
    if (st === 'running' || st === 'paused') this.auto.phase = 'match';
    else this.restartLobby();
    this.onChange();
  }

  autoTick(now = Date.now()) {
    const a = this.auto;
    const ac = this.cfg.auto ?? {};
    if (!a.enabled) return;
    const st = this.match.status;
    if (st === 'running' || st === 'paused') {
      if (a.phase !== 'match') {
        a.phase = 'match';
        this.onChange();
      }
      return;
    }
    if (a.phase === 'match') {
      a.phase = 'break';
      a.endsAt = now + (ac.breakSeconds ?? 30) * 1000;
      this.onChange();
      return;
    }
    if (!a.endsAt) {
      this.restartLobby(now);
      this.onChange();
    }
    const left = Math.ceil((a.endsAt - now) / 1000);
    if (a.phase === 'break') {
      if (left > 0) return;
      if (ac.resetSquadEachRound) this.resetAll({ silent: true });
      this.match = freshMatch();
      this.restartLobby(now);
      this.emit({ type: 'lobby', big: true, text: "🆕 YENİ MAÇ! Kadro kuruluyor — hediye at, ilk 11'e gir!" });
      this.onChange();
      return;
    }
    for (const w of [60, 10]) {
      if (left <= w && left > 0 && !a.warned[w]) {
        a.warned[w] = true;
        this.emit({ type: 'countdown', seconds: w, big: true, text: `⏳ Maça ${w} saniye kaldı!` });
      }
    }
    if (left > 0) return;
    const ready = this.teams.every((t) => this.teamPlayers(t.key).length > 0);
    if (!ready) {
      a.endsAt = now + 30000;
      a.warned = { 60: true };
      this.emit({ type: 'info', big: true, text: 'Maç için iki takımda da en az 1 oyuncu lazım! Hediye at 🎁' });
      this.onChange();
      return;
    }
    a.phase = 'match';
    this.startMatch();
  }

  // ---------- maç ----------
  strength(key) {
    const lu = this.lineup(key);
    let s = 0.5;
    for (const st of lu.starters) if (st.player) s += 1 + log2p(st.player.coins);
    return s;
  }

  startMatch() {
    const m = this.match;
    if (m.status === 'running') return;
    if (m.status === 'paused') {
      m.status = 'running';
      this.emit({ type: 'resume', big: true, text: 'Maç devam ediyor!' });
    } else {
      this.match = freshMatch();
      this.match.status = 'running';
      this.emit({ type: 'kickoff', big: true, text: '⚽ MAÇ BAŞLADI! Hediye at, takımın atağa kalksın!' });
    }
    this.onChange();
  }

  pauseMatch() {
    if (this.match.status !== 'running') return;
    this.match.status = 'paused';
    this.emit({ type: 'pause', big: true, text: '⏸ Maç durduruldu' });
    this.onChange();
  }

  resetMatch() {
    this.match = freshMatch();
    this.onChange();
  }

  logEvent(ev) {
    const m = this.match;
    m.events.push({ minute: m.minute, ...ev });
    if (m.events.length > MAX_EVENTS) m.events.splice(0, m.events.length - MAX_EVENTS);
  }

  pickWeighted(items, weightFn) {
    const total = items.reduce((a, it) => a + weightFn(it), 0);
    if (total <= 0) return items[0];
    let r = this.rng() * total;
    for (const it of items) {
      r -= weightFn(it);
      if (r <= 0) return it;
    }
    return items[items.length - 1];
  }

  // strong = atak barı dolunca yapılan büyük atak; değilse oyun içi rastgele pozisyon
  attack(key, bonus, strong) {
    const m = this.match;
    const opp = this.other(key);
    const att = this.lineup(key).starters.filter((s) => s.player);
    if (!att.length) return;
    const sa = this.strength(key);
    const sd = this.strength(opp);
    const gk = this.lineup(opp).starters.find((s) => s.pos.key === 'GK')?.player;

    const share = sa / (sa + sd);
    let p = strong ? 0.14 + 0.16 * share + Math.min(0.22, 0.035 * log2p(bonus)) : 0.06 + 0.1 * share;
    if (gk) p -= Math.min(0.08, 0.012 * log2p(gk.coins));
    else p += 0.1;
    p = Math.min(0.7, Math.max(0.04, p));

    const weight = (s) => (s.pos.attack ?? 1) * (1 + log2p(s.player.coins));
    const shooter = this.pickWeighted(att, weight);
    const sp = shooter.player;
    const teamName = this.team(key).name;

    if (this.rng() < p) {
      m.score[key] += 1;
      m.scorers[sp.id] = (m.scorers[sp.id] ?? 0) + 1;
      const others = att.filter((s) => s !== shooter);
      const assist = others.length && this.rng() < 0.6 ? this.pickWeighted(others, weight).player : null;
      const text = `GOOOL! ${sp.name}${assist ? ` (asist: ${assist.name})` : ''}`;
      this.logEvent({ type: 'goal', team: key, text });
      this.emit({
        type: 'goal',
        big: true,
        team: key,
        id: sp.id,
        name: sp.name,
        avatar: sp.avatar,
        assist: assist?.name ?? null,
        minute: m.minute,
        score: { ...m.score },
        text: `${m.minute}' GOOOL! ${sp.name} — ${teamName}`,
      });
    } else if (strong || this.rng() < 0.5) {
      const r = this.rng();
      let text;
      if (gk && r < 0.4) text = `🧤 ${gk.name} kurtardı! (${sp.name} şutu)`;
      else if (r < 0.6) text = `😱 ${sp.name} direkten döndü!`;
      else if (r < 0.8) text = `${sp.name} şutunu auta gönderdi`;
      else text = `${sp.name} ofsayta yakalandı`;
      this.logEvent({ type: 'chance', team: key, text });
      this.emit({ type: 'chance', team: key, text: `${m.minute}' ${text}` });
    }
  }

  tick() {
    const m = this.match;
    if (m.status !== 'running') return;
    m.minute += 1;

    const mc = this.cfg.match ?? {};
    const sa = this.strength('A');
    const sb = this.strength('B');
    const share = { A: sa / (sa + sb), B: sb / (sa + sb) };
    const order = this.rng() < 0.5 ? ['A', 'B'] : ['B', 'A'];
    let attacked = false;
    for (const k of order) {
      // takım kendi kendine de yavaşça baskı kurar; hediyeler barı çok daha hızlı doldurur
      m.meter[k] = Math.min(100, m.meter[k] + (mc.passiveFill ?? 1.5) * (1 + 2 * share[k]));
      if (m.meter[k] >= 100 && m.minute - m.lastAttack[k] >= (mc.attackCooldown ?? 8)) {
        this.emit({ type: 'attack', team: k, text: `${m.minute}' ⚡ ${this.team(k).name} atağa kalktı!` });
        this.attack(k, m.boost[k], true);
        m.meter[k] = 0;
        m.boost[k] = 0;
        m.lastAttack[k] = m.minute;
        attacked = true;
      }
    }
    if (!attacked && this.rng() < (mc.randomAttackChance ?? 0.12)) {
      this.attack(this.rng() < share.A ? 'A' : 'B', 0, false);
    }

    if (m.minute === 45) {
      this.logEvent({ type: 'info', text: 'İlk yarı sona erdi' });
      this.emit({ type: 'half', big: true, text: `İLK YARI BİTTİ  ${m.score.A} - ${m.score.B}` });
    }
    if (m.minute === 90) {
      m.extra = 1 + Math.floor(this.rng() * 4);
      this.logEvent({ type: 'info', text: `Uzatma: +${m.extra}` });
      this.emit({ type: 'info', big: true, text: `⏱ Uzatma: +${m.extra} dakika` });
    }
    if (m.minute >= 90 && m.minute >= 90 + m.extra) this.finish();
    this.onChange();
  }

  finish() {
    const m = this.match;
    m.status = 'finished';
    const { A, B } = m.score;
    const winner = A === B ? null : A > B ? 'A' : 'B';
    let mvp = null;
    const scorerIds = Object.keys(m.scorers).filter((id) => this.players.has(id));
    if (scorerIds.length) {
      scorerIds.sort((x, y) => m.scorers[y] - m.scorers[x] || this.players.get(y).coins - this.players.get(x).coins);
      mvp = this.players.get(scorerIds[0]);
    } else {
      const pool = winner ? this.teamPlayers(winner) : [...this.players.values()].sort((a, b) => b.coins - a.coins);
      mvp = pool[0] ?? null;
    }
    m.result = { winner, mvp: mvp ? { id: mvp.id, name: mvp.name, avatar: mvp.avatar, team: mvp.team } : null };
    const head = winner ? `🏆 ${this.team(winner).name} KAZANDI!` : '🤝 BERABERE!';
    this.logEvent({ type: 'end', text: `Maç bitti ${A} - ${B}` });
    this.emit({
      type: 'end',
      big: true,
      team: winner,
      text: `${head}  ${A} - ${B}${mvp ? `  ·  MVP: ${mvp.name}` : ''}`,
      name: mvp?.name,
      avatar: mvp?.avatar,
    });
  }

  // ---------- ekrana gönderilen durum ----------
  pub(p) {
    return { id: p.id, name: p.name, avatar: p.avatar, coins: p.coins, goals: this.match.scorers[p.id] ?? 0 };
  }

  snapshot({ admin = false } = {}) {
    const teams = this.teams.map((t) => {
      const lu = this.lineup(t.key);
      const all = this.teamPlayers(t.key);
      const captain = lu.starters.find((s) => s.captain)?.player ?? null;
      const empty = lu.starters.filter((s) => !s.player).length;
      return {
        key: t.key,
        name: t.name,
        color: t.color,
        keyword: t.keywords[0],
        totalCoins: all.reduce((a, p) => a + p.coins, 0),
        playerCount: all.length,
        starterCount: lu.starters.length - empty,
        captainTarget: captain ? captain.coins + 1 : null,
        starters: lu.starters.map((s) => ({
          pos: { key: s.pos.key, name: s.pos.name, short: s.pos.short, x: s.pos.x, y: s.pos.y },
          player: s.player ? this.pub(s.player) : null,
          captain: s.captain,
        })),
        bench: lu.bench.slice(0, 10).map((p) => this.pub(p)),
        benchCount: lu.bench.length,
      };
    });
    const m = this.match;
    const snap = {
      teams,
      match: {
        status: m.status,
        minute: m.minute,
        extra: m.extra,
        score: m.score,
        meter: { A: Math.round(m.meter.A), B: Math.round(m.meter.B) },
        events: m.events.slice(-8),
        result: m.result,
      },
      feed: this.feed.slice(-8),
      auto: {
        enabled: this.auto.enabled,
        phase: this.auto.phase,
        endsIn: this.auto.endsAt ? Math.max(0, this.auto.endsAt - Date.now()) : 0,
      },
    };
    if (admin) {
      snap.players = [...this.players.values()]
        .sort((a, b) => b.coins - a.coins)
        .map((p) => ({ ...this.pub(p), team: p.team, gifts: p.gifts }));
      snap.bannedCount = this.banned.size;
    }
    return snap;
  }
}
