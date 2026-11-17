// ANA VİDEO STİLİ: "Gol mü, değil mi?" görünüşü (kullanıcının seçimi, 2026-11-17).
// Yeni videoların hepsi bu parçalarla kurulur:
//   - tam ekran ElevenLabs görseli + yavaş zoom, üstte/altta koyu geçiş
//   - üstte sarı çerçeveli rozet (sarı kart simgesi) + ilerleme noktaları (✓ yeşil / ✗ kırmızı)
//   - büyük Oswald başlık (BÜYÜK HARF), altında gölgeli Inter cümle
//   - iki seçenek (yeşil / kırmızı) + ortada sarı 3-2-1 halkası
//   - cevapta eğik, kalın çerçeveli damga (GOL! / GOL DEĞİL!), golde beyaz flaş + sarsıntı
//   - altta kural/açıklama kartı (sarı küçük başlık + kalın beyaz metin)
//   - sonda özet listesi + "yorumlara yaz" çağrısı, en altta "Canlı Maç Oyna" el yazısı imza
//   - sesler: hakem düdüğü, gol patlaması + tribün, sayaçta tik
const path = require('path');
const { execSync } = require('child_process');

const CSS = `
<style>
  #bgb { display: none; }
  #v { background: #000; }
  .ph { position: absolute; inset: 0; opacity: 0; transition: opacity .45s; overflow: hidden; }
  .ph.on { opacity: 1; }
  .ph img { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: cover; }
  .ph.on img { animation: kb 9s ease-out both; }
  .ph.on.kb2 img { animation-name: kb2; }
  @keyframes kb { from { transform: scale(1.02); } to { transform: scale(1.13) translateY(-1.5%); } }
  @keyframes kb2 { from { transform: scale(1.14) translateY(-2%); } to { transform: scale(1.03); } }
  #shade { position: absolute; inset: 0; pointer-events: none;
    background: linear-gradient(to bottom, rgba(3,8,16,.82) 0%, rgba(3,8,16,.35) 26%, rgba(3,8,16,0) 40%, rgba(3,8,16,0) 52%, rgba(3,8,16,.55) 68%, rgba(3,8,16,.92) 100%); }
  #dim { position: absolute; inset: 0; background: rgba(3,8,16,.55); opacity: 0; transition: opacity .4s; }
  #dim.on { opacity: 1; }
  #flash { position: absolute; inset: 0; background: #fff; opacity: 0; pointer-events: none; }
  #flash.go { animation: flash .5s ease-out; }
  @keyframes flash { 0% { opacity: .75; } 100% { opacity: 0; } }

  #hud { position: absolute; top: 70px; left: 0; right: 0; display: flex; justify-content: center; align-items: center; gap: 22px; opacity: 0; transition: opacity .3s; }
  #hud.on { opacity: 1; }
  .pill { font: 700 40px/1 Oswald, sans-serif; letter-spacing: 3px; padding: 14px 26px; border-radius: 14px; background: #111; border: 4px solid #ffd400; color: #ffd400; }
  .pill .card { display: inline-block; width: 26px; height: 36px; background: #ffd400; border-radius: 4px; vertical-align: -6px; margin-right: 12px; transform: rotate(8deg); }
  .dots { display: flex; gap: 12px; }
  .dot { width: 54px; height: 54px; border-radius: 50%; border: 4px solid rgba(255,255,255,.55); display: grid; place-items: center; font: 800 28px Inter, sans-serif; color: rgba(255,255,255,.7); background: rgba(0,0,0,.35); }
  .dot.cur { border-color: #fff; color: #fff; background: rgba(255,255,255,.18); }
  .dot.g { background: var(--ok); border-color: var(--ok); color: #04210f; }
  .dot.n { background: var(--bad); border-color: var(--bad); color: #fff; }

  #intro { position: absolute; top: 230px; left: 50px; right: 50px; text-align: center; display: grid; gap: 26px; justify-items: center; }
  #intro .h1 { font-size: 160px; line-height: .98; }
  #intro .script { font-size: 96px; color: #ffd400; transform: rotate(-4deg); }
  #intro .s { color: #fff; font-size: 44px; letter-spacing: 1px; }

  #sit { position: absolute; top: 200px; left: 60px; right: 60px; text-align: center; display: grid; gap: 18px; justify-items: center; }
  #sit .h1 { font-size: 118px; }
  #sit .s { color: #fff; font-size: 46px; text-shadow: 0 4px 18px rgba(0,0,0,.8); max-width: 900px; }

  #ask { position: absolute; left: 50px; right: 50px; bottom: 210px; display: grid; gap: 30px; justify-items: center; }
  #ask .q { font: 700 96px/1 Oswald, sans-serif; letter-spacing: 3px; text-shadow: 0 6px 20px rgba(0,0,0,.7); }
  .btns { display: grid; grid-template-columns: 1fr 210px 1fr; gap: 26px; align-items: center; width: 100%; }
  .btn { height: 150px; border-radius: 28px; display: grid; place-items: center; font: 700 72px/1 Oswald, sans-serif; letter-spacing: 2px; background: rgba(5,12,22,.72); border: 7px solid; transition: transform .25s, opacity .25s, background .25s; }
  .btn.g { color: #4ade80; border-color: #22c55e; }
  .btn.n { color: #f87171; border-color: #ef4444; }
  .btn.right.g { background: #22c55e; color: #04210f; transform: scale(1.08); }
  .btn.right.n { background: #ef4444; color: #fff; transform: scale(1.08); }
  .btn.wrong { opacity: .28; transform: scale(.92); }
  .cd { width: 210px; height: 210px; border-radius: 50%; display: grid; place-items: center; position: relative; }
  .cd::before { content: ''; position: absolute; inset: 0; border-radius: 50%; background: conic-gradient(#ffd400 calc(var(--p) * 1%), rgba(255,255,255,.18) 0); -webkit-mask: radial-gradient(circle, transparent 78px, #000 80px); mask: radial-gradient(circle, transparent 78px, #000 80px); }
  .cd b { font: 700 120px/1 Oswald, sans-serif; color: #fff; }
  .cd b.pop { animation: pop .45s cubic-bezier(.2,1.6,.4,1) both; }
  .cd.run::before { animation: ring 3s linear both; }
  @property --p { syntax: '<number>'; inherits: false; initial-value: 100; }
  @keyframes ring { from { --p: 100; } to { --p: 0; } }

  #stamp { position: absolute; top: 640px; left: 0; right: 0; display: grid; place-items: center; pointer-events: none; }
  #stamp div { font: 700 170px/1 Oswald, sans-serif; letter-spacing: 4px; padding: 18px 46px; border: 12px solid; border-radius: 26px; transform: rotate(-7deg); background: rgba(3,8,16,.6); }
  #stamp .g { color: #4ade80; border-color: #22c55e; text-shadow: 0 0 40px rgba(34,197,94,.6); }
  #stamp .n { color: #f87171; border-color: #ef4444; text-shadow: 0 0 40px rgba(239,68,68,.6); }
  #stamp .long { font-size: 132px; }
  .slam { animation: slam .55s cubic-bezier(.2,1.5,.4,1) both; }
  @keyframes slam { from { opacity: 0; transform: rotate(-7deg) scale(2.6); } to { opacity: 1; transform: rotate(-7deg) scale(1); } }

  #rule { position: absolute; left: 60px; right: 60px; bottom: 200px; border-radius: 28px; padding: 30px 36px; background: rgba(5,12,22,.86); border: 5px solid; display: grid; gap: 12px; }
  #rule.g { border-color: #22c55e; } #rule.n { border-color: #ef4444; } #rule.y { border-color: #ffd400; }
  #rule .law { font: 800 32px Inter, sans-serif; letter-spacing: 2px; text-transform: uppercase; color: #ffd400; }
  #rule .txt { font: 800 50px/1.25 Inter, sans-serif; }

  #end { position: absolute; top: 300px; left: 60px; right: 60px; text-align: center; display: grid; gap: 34px; justify-items: center; }
  #end .h2 { font-size: 120px; line-height: 1.02; }
  .sum { display: grid; gap: 16px; width: 100%; max-width: 820px; }
  .sum div { display: flex; justify-content: space-between; align-items: center; gap: 20px; font: 800 46px Inter, sans-serif; padding: 16px 28px; border-radius: 18px; background: rgba(5,12,22,.75); border: 3px solid rgba(255,255,255,.15); text-align: left; }
  .sum .g span { color: #4ade80; } .sum .n span { color: #f87171; } .sum .y span { color: #ffd400; }
  #end .t { font-size: 60px; }

  .hide { display: none !important; }
  .shake { animation: shake .45s; }
  @keyframes shake { 20% { transform: translate(-14px, 6px); } 40% { transform: translate(12px, -8px); } 60% { transform: translate(-8px, 4px); } 80% { transform: translate(5px, -3px); } }
</style>`;

const esc = (t) => String(t).replace(/&/g, '&amp;').replace(/</g, '&lt;');

// görseller: kodlar → tam ekran katmanlar (dir: gorseller/<set> klasörü)
const photos = (codes, dir) => `<div id="stage" style="position:absolute;inset:0">
  ${codes.map((c, i) => `<div class="ph${i % 2 ? ' kb2' : ''}" id="ph-${c}"><img src="file://${path.join(dir, c + '.jpg')}" alt=""></div>`).join('')}
  <div id="shade"></div><div id="dim"></div>
</div><div id="flash"></div>`;

// üst rozet + ilerleme noktaları
const hud = (label, n) => `<div id="hud"><div class="pill"><span class="card"></span>${esc(label)}</div><div class="dots">${Array.from({ length: n }, (_, i) => `<div class="dot" id="dot${i}">${i + 1}</div>`).join('')}</div></div>`;

// açılış: büyük başlık (ikinci satır sarı) + el yazısı + küçük bilgi
const intro = (l1, l2, script, small) => `<div id="intro" class="hide">
  <div class="h1 pop">${esc(l1)}<br><span class="gold">${esc(l2)}</span></div>
  ${script ? `<div class="script rise d2">${esc(script)}</div>` : ''}
  ${small ? `<div class="s rise d3">${esc(small)}</div>` : ''}
</div>`;

// durum başlığı + iki seçenek + sayaç + damga + kart
const sit = () => `<div id="sit" class="hide"><div class="h1 pop" id="sitT"></div><div class="s rise d1" id="sitS"></div></div>`;
const ask = (q, yes, no) => `<div id="ask" class="hide">
  <div class="q rise">${esc(q)}</div>
  <div class="btns rise d1"><div class="btn g" id="bG">${esc(yes)}</div><div class="cd" id="cd"><b id="cdn">3</b></div><div class="btn n" id="bN">${esc(no)}</div></div>
</div>`;
const stamp = () => `<div id="stamp" class="hide"><div id="stampT"></div></div>`;
const rule = () => `<div id="rule" class="hide"><div class="law" id="ruleL"></div><div class="txt" id="ruleT"></div></div>`;

// kapanış: rows = [{ label, val, kind: 'g' | 'n' | 'y' }]
const end = (l1, l2, rows, cta) => `<div id="end" class="hide">
  <div class="h2 pop">${esc(l1)}<br><span class="gold">${esc(l2)}</span></div>
  <div class="sum rise d1">${rows.map((r) => `<div class="${r.kind || 'y'}">${esc(r.label)}<span>${esc(r.val)}</span></div>`).join('')}</div>
  <div class="t rise d3">${esc(cta)}</div>
</div>`;

// sayfada çalışan yardımcılar (at() ve show() common-head'den gelir)
const JS = `<script>
  const $ = (id) => document.getElementById(id);
  const hide = (...ids) => ids.forEach((i) => $(i) && $(i).classList.add('hide'));
  const unhide = (id) => { const e = $(id); e.classList.add('hide'); void e.offsetWidth; e.classList.remove('hide'); };
  const restart = (el, cls) => { el.classList.remove(cls); void el.offsetWidth; el.classList.add(cls); };
  function photo(code) {
    document.querySelectorAll('.ph').forEach((p) => { if (p.id !== 'ph-' + code) p.classList.remove('on'); });
    restart($('ph-' + code), 'on');
  }
  // i. adım: rozet görünür, nokta işaretlenir, başlık + cümle
  function step(i, code, title, sub) {
    hide('intro', 'stamp', 'rule', 'ask');
    $('dim').classList.remove('on');
    photo(code);
    $('hud').classList.add('on');
    document.querySelectorAll('.dot').forEach((d, k) => d.classList.toggle('cur', k === i && !d.classList.contains('g') && !d.classList.contains('n')));
    $('sitT').textContent = title; $('sitS').textContent = sub || '';
    unhide('sit');
  }
  // iki seçenek + 3 saniyelik sayaç
  function askNow() {
    $('bG').className = 'btn g'; $('bN').className = 'btn n';
    unhide('ask');
    restart($('cd'), 'run');
    [3, 2, 1].forEach((n, k) => setTimeout(() => { const b = $('cdn'); b.textContent = n; restart(b, 'pop'); }, k * 1000));
  }
  // cevap: yes = soldaki (yeşil) seçenek doğru mu; text = damga; law/txt = alttaki kart
  function answer(i, yes, text, law, txt) {
    $('bG').className = 'btn g ' + (yes ? 'right' : 'wrong');
    $('bN').className = 'btn n ' + (yes ? 'wrong' : 'right');
    $('cd').classList.remove('run'); $('cdn').textContent = yes ? '✓' : '✗';
    hide('sit');
    const st = $('stampT'); st.className = (yes ? 'g' : 'n') + (text.length > 7 ? ' long' : '') + ' slam'; st.textContent = text;
    unhide('stamp');
    const d = $('dot' + i); if (d) { d.classList.remove('cur'); d.classList.add(yes ? 'g' : 'n'); d.textContent = yes ? '✓' : '✗'; }
    if (yes) { restart($('flash'), 'go'); restart($('stage'), 'shake'); }
    if (law || txt) setTimeout(() => {
      hide('ask');
      $('rule').className = yes ? 'g' : 'n';
      $('ruleL').textContent = law || ''; $('ruleT').textContent = txt || '';
      unhide('rule');
    }, 1300);
  }
  function finale(code) {
    hide('stamp', 'rule', 'ask', 'sit', 'intro');
    $('hud').classList.remove('on');
    photo(code); $('dim').classList.add('on');
    unhide('end');
  }
</script>`;

// stilin efekt sesleri: düdük, gol patlaması, tribün (tick/ding/buzz makeSfx'ten)
function addSfx(sfx, AUD) {
  const make = (name, src) => {
    sfx[name] = path.join(AUD, name + '.wav');
    execSync(`ffmpeg -y -loglevel error ${src} -ac 1 "${sfx[name]}"`);
  };
  make('boom', `-f lavfi -i "aevalsrc='0.9*sin(2*PI*(36+120*exp(-3*t))*t)*exp(-2.2*t)':s=24000:d=1.4"`);
  make('whistle', `-f lavfi -i "aevalsrc='0.32*sin(2*PI*2900*t+2.5*sin(2*PI*32*t))*(0.65+0.35*sin(2*PI*32*t))*min(1,t*40)*if(lt(t,0.55),1,exp(-25*(t-0.55)))':s=24000:d=0.75"`);
  make('roar', `-f lavfi -i "anoisesrc=d=2.4:c=pink:a=0.5:r=24000" -af "lowpass=f=1800,highpass=f=150,afade=t=in:d=0.25,afade=t=out:st=1.2:d=1.2,volume=0.55"`);
  return sfx;
}

module.exports = { CSS, photos, hud, intro, sit, ask, stamp, rule, end, JS, addSfx };
