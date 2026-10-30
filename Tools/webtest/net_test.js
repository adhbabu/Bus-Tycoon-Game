// Çok oyunculu bağlantı eklentisinin (BusTycoonNet.jslib) Unity olmadan tarayıcı testi: gerçek PeerJS sunucusuyla
// kurucu + iki katılımcı, kurucunun sunucu bağlantısının kopması, yanlış kod, ayrılma ve kapanış.
// Kullanım: node Tools/webtest/net_test.js [--kopar]   (Playwright gerekir)
const { chromium } = require('playwright');
const fs = require('fs');
const libArg = process.argv.slice(2).find(a => !a.startsWith('--')); // başka bir sürümü denemek için dosya yolu
const lib = fs.readFileSync(libArg || require('path').join(__dirname, '../../Assets/Plugins/WebGL/BusTycoonNet.jslib'), 'utf8');
// --kopar: sunucu soketini öldür (telefonun arka planda bağlantıyı kesmesi gibi); yoksa düzgün disconnect()
const kill = process.argv.includes('--kopar');
(async () => {
  const b = await chromium.launch();
  let ok = true;
  const check = (c, m) => { console.log((c ? 'OK  ' : 'FAIL') + ' ' + m); if (!c) ok = false; };
  async function open() {
    const ctx = await b.newContext();
    const p = await ctx.newPage();
    await p.goto('file://' + __dirname + '/page.html');
    await p.addScriptTag({ content: lib });
    await p.evaluate(() => { window.events = []; setInterval(() => { let e; while ((e = poll('BusTycoon_NetPoll'))) window.events.push(JSON.parse(e)); }, 50); });
    return p;
  }
  const events = p => p.evaluate(() => window.events);
  async function waitFor(p, pred, ms = 20000) {
    const end = Date.now() + ms;
    while (Date.now() < end) { const ev = await events(p); const hit = ev.find(pred); if (hit) return hit; await p.waitForTimeout(200); }
    return null;
  }
  const code = 'T' + Math.random().toString(36).slice(2, 6).toUpperCase();
  const host = await open();
  await host.evaluate(c => BusTycoon_NetStart(1, c), code);
  check(await waitFor(host, e => e.t === 'hosted'), 'kurucu odayı açtı ' + code);

  const c1 = await open();
  await c1.evaluate(c => BusTycoon_NetStart(0, c), code);
  check(await waitFor(c1, e => e.t === 'connected'), 'katılımcı 1 bağlandı');
  const j1 = await waitFor(host, e => e.t === 'joined');
  check(j1, 'kurucu katılımcı 1\'i gördü');
  await c1.evaluate(() => BusTycoon_NetSend('', 'HELLO|Ali|1'));
  check(await waitFor(host, e => e.t === 'message' && e.d === 'HELLO|Ali|1'), 'katılımcı → kurucu ileti');

  // Kurucunun eşleştirme sunucusu bağlantısı kopar (telefonda başka uygulamaya geçmek gibi)
  await host.evaluate(k => { const peer = Module.busTycoonNet.peer; if (!k) return peer.disconnect(); const s = peer.socket; (s._socket || s.socket).close(); }, kill);
  await host.waitForTimeout(500);
  check(await host.evaluate(() => Module.busTycoonNet.peer.disconnected), 'sunucu bağlantısı koptu (benzetim)');
  await host.waitForTimeout(5000);
  check(!(await host.evaluate(() => Module.busTycoonNet.peer.disconnected)), 'kurucu aynı kimlikle yeniden bağlandı');
  check(!(await events(host)).some(e => e.t === 'error'), 'kurucuya hata bildirilmedi (oda açık kaldı)');
  await host.evaluate(() => BusTycoon_NetBroadcast('ROOM|x'));
  check(await waitFor(c1, e => e.t === 'message' && e.d === 'ROOM|x'), 'kopma sırasında veri kanalı sürdü');

  const c2 = await open();
  await c2.evaluate(c => BusTycoon_NetStart(0, c), code);
  check(await waitFor(c2, e => e.t === 'connected'), 'yeniden bağlanmadan sonra katılımcı 2 de katılabildi');

  const c3 = await open();
  await c3.evaluate(() => BusTycoon_NetStart(0, 'ZZZZZ9'));
  const err = await waitFor(c3, e => e.t === 'error');
  await c3.waitForTimeout(1500);
  const errs = (await events(c3)).filter(e => e.t === 'error');
  check(err && err.d === 'peer-unavailable' && errs.length === 1, 'yanlış kod: tek hata "' + (err && err.d) + '" (' + errs.length + ')');

  // Katılımcı 2 ayrılır → kurucuda "left"; kurucu kapatır → katılımcı 1'de "closed"
  await c2.evaluate(() => BusTycoon_NetStop());
  check(await waitFor(host, e => e.t === 'left'), 'katılımcı 2 ayrılınca kurucu gördü');
  await host.evaluate(() => { BusTycoon_NetBroadcast('BYE'); BusTycoon_NetStop(); });
  check(await waitFor(c1, e => e.t === 'message' && e.d === 'BYE'), 'kapanmadan önce BYE ulaştı');
  check(await waitFor(c1, e => e.t === 'closed'), 'kurucu kapatınca katılımcı 1 "closed" aldı');
  check(!(await events(c1)).some(e => e.t === 'error'), 'katılımcı 1\'e hata bildirilmedi');
  await b.close();
  console.log(ok ? 'TÜMÜ GEÇTİ' : 'HATA VAR');
  process.exit(ok ? 0 : 1);
})().catch(e => { console.error('FAIL', e); process.exit(1); });
