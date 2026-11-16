import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import { Game, norm } from '../game.js';
import { GiftTracker } from '../tiktok.js';

const config = JSON.parse(fs.readFileSync(new URL('../config.json', import.meta.url), 'utf8'));

function seeded(seed = 1) {
  return () => {
    seed = (seed * 16807) % 2147483647;
    return (seed - 1) / 2147483646;
  };
}

test('en çok jeton atan kaptan ve santrfor olur, geçilince yer değişir', () => {
  const g = new Game(config);
  const events = [];
  g.onEvent = (e) => events.push(e);
  g.handleChat({ id: 'ali', text: '1' });
  g.handleChat({ id: 'veli', text: 'Fener!' });
  g.handleGift({ id: 'ali', name: 'Ali', coins: 10 });
  g.handleGift({ id: 'veli', name: 'Veli', coins: 5 });

  let lu = g.lineup('A');
  assert.equal(lu.starters[0].pos.key, 'ST');
  assert.equal(lu.starters[0].player.id, 'ali');
  assert.equal(lu.captainId, 'ali');
  assert.equal(lu.starters[1].player.id, 'veli');

  g.handleGift({ id: 'veli', name: 'Veli', coins: 100 });
  lu = g.lineup('A');
  assert.equal(lu.captainId, 'veli');
  assert.equal(lu.starters[1].player.id, 'ali');
  assert.ok(events.some((e) => e.type === 'captain' && e.id === 'veli'));
});

test('12. oyuncu yedeğe düşer, takım seçmeyen az kişili takıma gider', () => {
  const g = new Game(config);
  for (let i = 0; i < 12; i++) {
    g.handleChat({ id: 'p' + i, text: '2' });
    g.handleGift({ id: 'p' + i, name: 'P' + i, coins: 100 - i });
  }
  const lu = g.lineup('B');
  assert.equal(lu.starters.filter((s) => s.player).length, 11);
  assert.deepEqual(lu.bench.map((p) => p.id), ['p11']);

  g.handleGift({ id: 'yeni', name: 'Yeni', coins: 1 });
  assert.equal(g.players.get('yeni').team, 'A');
});

test('minCoins şartı karşılanmazsa mevki boş kalır', () => {
  const cfg = structuredClone(config);
  cfg.positions[0].minCoins = 50;
  const g = new Game(cfg);
  g.handleGift({ id: 'a', name: 'A', coins: 10 });
  const lu = g.lineup('A');
  assert.ok(!lu.starters[0].player || lu.starters[0].player.bot);
  assert.equal(lu.starters[1].player.id, 'a');
});

test('seri hediyeler iki kez sayılmaz', () => {
  const t = new GiftTracker();
  const base = { user: { displayId: 'ali', nickname: 'Ali' }, giftId: '5655', groupId: 'g1', gift: { name: 'Rose', diamondCount: 1, type: 1 } };
  const got = [];
  for (const [repeatCount, repeatEnd] of [[1, 0], [2, 0], [5, 0], [5, 1]]) {
    const r = t.process({ ...base, repeatCount, repeatEnd });
    if (r) got.push(r.coins);
  }
  assert.deepEqual(got, [1, 1, 3]);

  // seri olmayan pahalı hediye
  const r = t.process({ user: { uniqueId: 'veli' }, giftId: '1', repeatCount: 1, gift: { name: 'Lion', diamondCount: 29999, type: 2 } });
  assert.equal(r.coins, 29999);
  assert.equal(r.id, 'veli');
});

test('Türkçe anahtar kelime normalize edilir', () => {
  assert.equal(norm('KIRMIZI'), 'kirmizi');
  assert.equal(norm('Kırmızı'), 'kirmizi');
  assert.equal(norm('MAVİ'), 'mavi');
  assert.equal(norm('FENERBAHÇE'), 'fenerbahce');
});

test('maç 90+ dakikada biter ve sonuç üretir', () => {
  const g = new Game(config, null, seeded(7));
  for (let i = 0; i < 22; i++) g.handleGift({ id: 'p' + i, name: 'P' + i, coins: 1 + i * 3 });
  g.startMatch();
  for (let i = 0; i < 200 && g.match.status === 'running'; i++) {
    if (i % 10 === 0) g.handleGift({ id: 'p1', name: 'P1', coins: 50 });
    g.tick();
  }
  assert.equal(g.match.status, 'finished');
  assert.ok(g.match.minute >= 91 && g.match.minute <= 94);
  assert.ok(g.match.result);
  const goals = Object.values(g.match.scorers).reduce((a, b) => a + b, 0);
  assert.equal(goals, g.match.score.A + g.match.score.B);
});

test('kayıt / yükleme', () => {
  const g = new Game(config);
  g.handleGift({ id: 'a', name: 'A', coins: 7 });
  g.setTeamName('A', 'Yeni İsim');
  const g2 = new Game(config, JSON.parse(JSON.stringify(g)));
  assert.equal(g2.players.get('a').coins, 7);
  assert.equal(g2.team('A').name, 'Yeni İsim');
});

test('otomatik pilot: geri sayım → maç → ara → yeni kadro', () => {
  const cfg = structuredClone(config);
  cfg.auto = { enabled: true, lobbySeconds: 60, breakSeconds: 10, resetSquadEachRound: true };
  cfg.bots = { enabled: false };
  const g = new Game(cfg, null, seeded(3));
  const events = [];
  g.onEvent = (e) => events.push(e.type);
  let now = 1_000_000;
  g.autoTick(now);
  assert.equal(g.auto.phase, 'lobby');

  // tek takım varken maç başlamaz, süre uzar
  g.handleGift({ id: 'a', name: 'A', coins: 5 });
  now += 61_000;
  g.autoTick(now);
  assert.equal(g.match.status, 'idle');

  g.handleGift({ id: 'b', name: 'B', coins: 5 });
  now += 31_000;
  g.autoTick(now);
  assert.equal(g.match.status, 'running');
  assert.ok(events.includes('countdown'));

  while (g.match.status === 'running') g.tick();
  g.autoTick(now);
  assert.equal(g.auto.phase, 'break');
  now += 11_000;
  g.autoTick(now);
  assert.equal(g.auto.phase, 'lobby');
  assert.equal(g.match.status, 'idle');
  assert.equal(g.players.size, 0);
  assert.ok(events.includes('lobby'));
});

test('kaptan tahtı el değiştirince önceki kaptanın adı duyurulur', () => {
  const g = new Game(config);
  const caps = [];
  g.onEvent = (e) => e.type === 'captain' && caps.push(e);
  g.handleGift({ id: 'x', name: 'X', coins: 10 });
  g.handleChat({ id: 'y', text: '1' });
  g.handleGift({ id: 'y', name: 'Y', coins: 50 });
  assert.equal(caps.at(-1).name, 'Y');
  assert.equal(caps.at(-1).prevName, 'X');
});

test('botlar boş mevkileri doldurur, kaptan olmaz, gerçek oyuncuya yer açar', () => {
  const cfg = structuredClone(config);
  cfg.bots = { enabled: true };
  cfg.auto = { enabled: true, lobbySeconds: 10, breakSeconds: 5 };
  const g = new Game(cfg, null, seeded(5));
  let lu = g.lineup('B');
  assert.equal(lu.starters.filter((s) => s.player?.bot).length, 11);
  assert.equal(lu.captainId, null);

  g.handleChat({ id: 'tek', text: '2' });
  g.handleGift({ id: 'tek', name: 'Tek', coins: 1 });
  lu = g.lineup('B');
  assert.equal(lu.starters[0].player.id, 'tek');
  assert.equal(lu.captainId, 'tek');
  assert.equal(lu.starters.filter((s) => s.player?.bot).length, 10);
  assert.equal(g.snapshot().teams[1].starterCount, 1);

  // tek gerçek oyuncu varken otomatik pilot maçı başlatır
  let now = 1_000_000;
  g.autoTick(now);
  now += 11_000;
  g.autoTick(now);
  assert.equal(g.match.status, 'running');
  while (g.match.status === 'running') g.tick();
  assert.equal(g.match.status, 'finished');
  if (g.match.result.mvp) assert.ok(!g.match.result.mvp.id.startsWith('bot-'));
});
