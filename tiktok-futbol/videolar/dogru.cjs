// "Doğru mu, yanlış mı?" – ana stil (stil.cjs)
// Görseller: gorseller/dogru/DG1–DG5 · Anlatım: seslendirme/video-sesleri/D1–D10
// Çalıştır: node videolar/dogru.cjs [onizleme]   → videolar/cikti/dogru-mu-yanlis-mi.mp4
const path = require('path');
const { HEAD, TAIL } = require('./common-head.cjs');
const { timeline, render, makeSfx } = require('./v2.cjs');
const S = require('./stil.cjs');
const { preview } = require('./onizleme.cjs');

const IMG = process.env.GORSEL_DIR || path.join(__dirname, 'gorseller', 'dogru');
// bilgiler: IFAB Oyun Kuralları (2025/26) ve 2002 AS Adema 149-0 SOE maçı
const Q = [
  { img: 'DG2', title: '149-0 BİTEN MAÇ', sub: 'Bir futbol maçı 149-0 bitti.', yes: true,
    law: '2002 · Madagaskar', rule: 'Bir takım, hakem kararlarını protesto etmek için kendi kalesine 149 gol attı.' },
  { img: 'DG3', title: 'KALECİ VE TOP', sub: 'Kaleci topu elinde istediği kadar tutabilir.', yes: false,
    law: 'Kural 12 · 8 saniye', rule: 'En fazla 8 saniye. Fazla tutarsa rakip takım korner kullanır.' },
  { img: 'DG4', title: '7 OYUNCU SINIRI', sub: 'Yedi oyuncudan aza düşen takımla maç devam edemez.', yes: true,
    law: 'Kural 3 · Oyuncular', rule: 'Bir takım 7 oyuncunun altına düşerse maç oynanamaz.' },
  { img: 'DG5', title: 'TOPUN AĞIRLIĞI', sub: 'Futbol topu yarım kilodan ağırdır.', yes: false,
    law: 'Kural 2 · Top', rule: 'Resmi maç topu 410 ile 450 gram arasındadır.' },
];

const html = HEAD + S.CSS +
  S.photos(['DG1', 'DG2', 'DG3', 'DG4', 'DG5'], IMG) +
  S.hud('FUTBOL BİLGİSİ', Q.length) +
  S.intro('DOĞRU MU,', 'YANLIŞ MI?', 'Futbol özel!', '4 bilgi · her biri 3 saniye') +
  S.sit() + S.ask('SENCE?', 'DOĞRU', 'YANLIŞ') + S.stamp() + S.rule() +
  S.end('Kaç tanesini', 'bildin?', Q.map((q) => ({ label: q.title, val: q.yes ? 'DOĞRU' : 'YANLIŞ', kind: q.yes ? 'g' : 'n' })), 'Skorunu yorumlara yaz 👇') +
  TAIL + S.JS + `<script>const Q = ${JSON.stringify(Q)};</script>`;

const steps = [
  { say: 'D1', run: "photo('DG1'); unhide('intro')", min: 3200, pad: 300, sfx: [{ t: 0, k: 'whistle' }] },
  ...Q.flatMap((q, i) => [
    { say: 'soru', run: `step(${i}, Q[${i}].img, Q[${i}].title, Q[${i}].sub)`, pad: 150 },
    { min: 3000, pad: 0, run: 'askNow()', sfx: [{ t: 0, k: 'tick' }, { t: 1000, k: 'tick' }, { t: 2000, k: 'tick' }] },
    { say: 'cevap', run: `answer(${i}, Q[${i}].yes, Q[${i}].yes ? 'DOĞRU!' : 'YANLIŞ!', Q[${i}].law, Q[${i}].rule)`, pad: 900,
      sfx: q.yes ? [{ t: 0, k: 'boom' }, { t: 0, k: 'roar' }] : [{ t: 0, k: 'whistle' }] },
  ]),
  { say: 'son', run: "finale('DG1')", min: 3000 },
];

(async () => {
  const sfx = makeSfx();
  S.addSfx(sfx, path.join(__dirname, 'cikti', 'aud'));
  const tl = timeline(steps, ['D1', 'D2', 'D3', 'D4', 'D5', 'D6', 'D7', 'D8', 'D9', 'D10']);
  console.log('toplam', (tl.total / 1000).toFixed(1), 's');
  if (process.argv[2] === 'onizleme') {
    const s = tl.steps;
    return preview('dogru', html, tl, [2500, s[1].start + 2500, s[2].start + 1500, s[3].start + 2600, s[9].start + 2600, tl.total - 1000]);
  }
  await render('dogru-mu-yanlis-mi', html, tl, sfx, 1500);
})();
