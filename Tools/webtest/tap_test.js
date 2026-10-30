// Dokunuş eklentisinin (BusTycoonTap.jslib) Unity olmadan tarayıcı testi: dokunmatik ve fareyle yazı alanı
// (klavyeli pencere), kaydırırken açılmama, davet bağlantısının panoya kopyalanması.
// Kullanım: node Tools/webtest/tap_test.js   (Playwright gerekir)
const { chromium } = require('playwright');
const fs = require('fs');
const lib = fs.readFileSync(require('path').join(__dirname, '../../Assets/Plugins/WebGL/BusTycoonTap.jslib'), 'utf8');
(async () => {
  const b = await chromium.launch();
  let ok = true;
  const check = (c, m) => { console.log((c ? 'OK  ' : 'FAIL') + ' ' + m); if (!c) ok = false; };
  for (const touch of [true, false]) {
    const ctx = await b.newContext({ viewport: { width: 800, height: 600 }, hasTouch: touch, isMobile: touch, permissions: ['clipboard-read', 'clipboard-write'] });
    const p = await ctx.newPage();
    const dialogs = [];
    p.on('dialog', d => { dialogs.push(d.message()); d.accept('Ayşe Yılmaz'); });
    await p.goto('file://' + __dirname + '/page.html');
    await p.addScriptTag({ content: lib });
    // Oyun gibi her karede alanları yeniden bildir (yeni nesneler)
    await p.evaluate(() => {
      setInterval(() => {
        BusTycoon_TapBegin();
        BusTycoon_TapZone('mp_name', 1, 100, 100, 300, 40, 'Adın', 'Oyuncu');
        BusTycoon_TapZone('mp_invite', 2, 500, 100, 170, 36, 'https://example.org/oyna/?oda=ABCDE', 'Oda kodu ABCDE');
        BusTycoon_TapEnd();
      }, 16);
    });
    await p.waitForTimeout(100);
    // Gerçek dokunuş gibi: basılı tutma süresince oyun alanları birkaç kez yeniden bildirir
    const cdp = touch ? await ctx.newCDPSession(p) : null;
    const tap = async (x, y) => {
      if (touch) {
        await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x, y }] });
        await p.waitForTimeout(80);
        await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
      } else {
        await p.mouse.move(x, y); await p.mouse.down(); await p.waitForTimeout(80); await p.mouse.up();
      }
    };
    const tag = touch ? '[dokunmatik]' : '[fare]';
    await tap(200, 120); await p.waitForTimeout(200);
    check(dialogs.length === 1 && dialogs[0] === 'Adın', `${tag} yazı alanına dokununca klavyeli pencere açıldı (${dialogs.join('|')})`);
    let r = await p.evaluate(() => poll('BusTycoon_TapPoll'));
    check(r === 'mp_name\nAyşe Yılmaz', `${tag} yazılan değer oyuna döndü: ${JSON.stringify(r)}`);
    await tap(50, 300); await p.waitForTimeout(150);
    check(dialogs.length === 1, `${tag} alan dışına dokunmak bir şey açmaz`);
    if (touch) {
      // kaydırma: basış ve bırakış farklı yerde → açılmamalı
      dialogs.length = 0;
      await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x: 200, y: 120 }] });
      await cdp.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [{ x: 200, y: 180 }] });
      await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
      await p.waitForTimeout(150);
      check(dialogs.length === 0, `${tag} parmakla kaydırırken pencere açılmaz`);
    }
    await tap(580, 118); await p.waitForTimeout(400);
    r = await p.evaluate(() => poll('BusTycoon_TapPoll'));
    const clip = await p.evaluate(() => navigator.clipboard.readText().catch(e => 'ERR ' + e.message));
    check(r === 'mp_invite\ncopied' && clip.endsWith('?oda=ABCDE'), `${tag} davet bağlantısı: ${JSON.stringify(r)} pano="${clip}"`);
    await ctx.close();
  }
  await b.close();
  console.log(ok ? 'TÜMÜ GEÇTİ' : 'HATA VAR');
  process.exit(ok ? 0 : 1);
})();
