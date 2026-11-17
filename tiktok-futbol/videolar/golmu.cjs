// "Gol mü, değil mi?" – ANA STİLİN örnek videosu (stil.cjs)
// Görseller: gorseller/golmu/MG1–MG5 · Anlatım: seslendirme/video-sesleri/M1–M10
// Çalıştır: node videolar/golmu.cjs [onizleme]   → videolar/cikti/gol-mu-degil-mi.mp4
const path = require('path');
const { HEAD, TAIL } = require('./common-head.cjs');
const { timeline, render, makeSfx } = require('./v2.cjs');
const S = require('./stil.cjs');
const { preview } = require('./onizleme.cjs');

const IMG = path.join(__dirname, 'gorseller', 'golmu');
const POS = [
  { img: 'MG2', title: 'TAÇ ATIŞI', sub: 'Top kimseye değmeden rakip kaleye giriyor.', yes: false,
    law: 'Kural 15 · Taç atışı', rule: 'Taçtan doğrudan gol olmaz. Oyun kale vuruşuyla devam eder.' },
  { img: 'MG3', title: 'KORNER', sub: 'Top kimseye değmeden ağlarda.', yes: true,
    law: 'Kural 17 · Köşe vuruşu', rule: 'Kornerden doğrudan gol geçerli. Adı: olimpik gol.' },
  { img: 'MG4', title: 'KALECİNİN VURUŞU', sub: 'Kale vuruşu bütün sahayı geçip rakip kaleye giriyor.', yes: true,
    law: 'Kural 16 · Kale vuruşu', rule: 'Kale vuruşundan doğrudan rakip kaleye atılan gol geçerli.' },
  { img: 'MG5', title: 'HAKEME ÇARPTI', sub: 'Şut hakeme çarpıyor, top ağlara gidiyor.', yes: false,
    law: 'Kural 9 · Top oyunda ve oyun dışında', rule: 'Top hakeme çarpıp kaleye girerse gol sayılmaz. Oyun hakem topuyla başlar.' },
];

const html = HEAD + S.CSS +
  S.photos(['MG1', 'MG2', 'MG3', 'MG4', 'MG5'], IMG) +
  S.hud('HAKEM SENSİN', POS.length) +
  S.intro('GOL MÜ,', 'DEĞİL Mİ?', 'Hakem sensin!', '4 pozisyon · her biri 3 saniye') +
  S.sit() + S.ask('GOL MÜ?', 'GOL', 'DEĞİL') + S.stamp() + S.rule() +
  S.end('Kaç tanesini', 'bildin?', POS.map((p) => ({ label: p.title, val: p.yes ? 'GOL' : 'DEĞİL', kind: p.yes ? 'g' : 'n' })), 'Skorunu yorumlara yaz 👇') +
  TAIL + S.JS + `<script>const P = ${JSON.stringify(POS)};</script>`;

const steps = [
  { say: 'M1', run: "photo('MG1'); unhide('intro')", min: 3200, pad: 300, sfx: [{ t: 0, k: 'whistle' }] },
  ...POS.flatMap((p, i) => [
    { say: 'soru', run: `step(${i}, P[${i}].img, P[${i}].title, P[${i}].sub)`, pad: 150 },
    { min: 3000, pad: 0, run: 'askNow()', sfx: [{ t: 0, k: 'tick' }, { t: 1000, k: 'tick' }, { t: 2000, k: 'tick' }] },
    { say: 'cevap', run: `answer(${i}, P[${i}].yes, P[${i}].yes ? 'GOL!' : 'GOL DEĞİL!', P[${i}].law, P[${i}].rule)`, pad: 900,
      sfx: p.yes ? [{ t: 0, k: 'boom' }, { t: 0, k: 'roar' }] : [{ t: 0, k: 'whistle' }] },
  ]),
  { say: 'son', run: "finale('MG1')", min: 3000 },
];

(async () => {
  const sfx = makeSfx();
  S.addSfx(sfx, path.join(__dirname, 'cikti', 'aud'));
  const tl = timeline(steps, ['M1', 'M2', 'M3', 'M4', 'M5', 'M6', 'M7', 'M8', 'M9', 'M10']);
  console.log('toplam', (tl.total / 1000).toFixed(1), 's');
  if (process.argv[2] === 'onizleme') {
    const s = tl.steps;
    return preview('golmu', html, tl, [2500, s[1].start + 2500, s[2].start + 1500, s[3].start + 2600, s[6].start + 2600, tl.total - 1000]);
  }
  await render('gol-mu-degil-mi', html, tl, sfx, 1500);
})();
