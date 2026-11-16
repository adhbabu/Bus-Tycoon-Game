// TikTok Canlı Futbol — sunucu
// Çalıştır: npm start         (gerçek yayın)
//           npm run demo      (sahte hediyelerle deneme)
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { WebSocketServer } from 'ws';
import { Game } from './game.js';
import { TikTokSource } from './tiktok.js';
import { fakeAvatar } from './demo-avatar.js';

const ROOT = path.dirname(fileURLToPath(import.meta.url));
const PUBLIC = path.join(ROOT, 'public');
const DATA_DIR = process.env.DATA_DIR || path.join(ROOT, 'data');
const STATE_FILE = path.join(DATA_DIR, 'state.json');

const args = process.argv.slice(2);
const flag = (name) => args.find((a) => a === `--${name}` || a.startsWith(`--${name}=`));
const flagValue = (name) => flag(name)?.split('=')[1];

const config = JSON.parse(fs.readFileSync(flagValue('config') ?? path.join(ROOT, 'config.json'), 'utf8'));
// Bulutta (Render vb.) çalışırken ayarlar ortam değişkenlerinden gelir.
const env = process.env;
const PORT = Number(flagValue('port') ?? env.PORT ?? config.port ?? 3000);
const HOST = env.HOST ?? (env.PORT ? '0.0.0.0' : config.host ?? '127.0.0.1');
if (env.TIKTOK_USER) config.tiktokUser = env.TIKTOK_USER;
if (env.SIGN_API_KEY) config.signApiKey = env.SIGN_API_KEY;
const ADMIN_KEY = String(env.ADMIN_KEY ?? config.adminKey ?? '');
const PUBLIC_HOST = !['127.0.0.1', 'localhost', '::1'].includes(HOST);
if (PUBLIC_HOST && !ADMIN_KEY) {
  console.warn('⚠ Sunucu internete açık ama ADMIN_KEY yok: yönetim paneli kilitli. ADMIN_KEY ortam değişkenini ayarla.');
}

// ---------- oyun + kayıt ----------
let saved = null;
if (!flag('fresh')) {
  try {
    saved = JSON.parse(fs.readFileSync(STATE_FILE, 'utf8'));
  } catch {
    /* ilk çalıştırma */
  }
}
const game = new Game(config, saved);
let dirty = false;

setInterval(() => {
  if (!dirty) return;
  dirty = false;
  fs.mkdirSync(DATA_DIR, { recursive: true });
  fs.writeFile(STATE_FILE, JSON.stringify(game), (e) => e && console.error('Kayıt hatası:', e.message));
}, 3000);

// ---------- statik dosyalar ----------
const MIME = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8', '.png': 'image/png', '.svg': 'image/svg+xml', '.json': 'application/json' };
const server = http.createServer((req, res) => {
  const url = new URL(req.url, 'http://x');
  let p = decodeURIComponent(url.pathname);
  if (p === '/') p = '/overlay.html';
  if (p === '/admin') p = '/admin.html';
  if (p === '/health') {
    res.writeHead(200, { 'content-type': 'text/plain' }).end('ok');
    return;
  }
  const file = path.normalize(path.join(PUBLIC, p));
  if (!file.startsWith(PUBLIC)) {
    res.writeHead(403).end();
    return;
  }
  fs.readFile(file, (err, buf) => {
    if (err) {
      res.writeHead(404, { 'content-type': 'text/plain; charset=utf-8' }).end('Bulunamadı');
      return;
    }
    res.writeHead(200, { 'content-type': MIME[path.extname(file)] ?? 'application/octet-stream', 'cache-control': 'no-store' });
    res.end(buf);
  });
});

// ---------- WebSocket: ekran ve yönetim paneli ----------
const wss = new WebSocketServer({ server });
let tiktokStatus = { state: 'idle', message: 'Bağlı değil', username: config.tiktokUser };
let demoTimer = null;

function send(ws, msg) {
  if (ws.readyState === 1) ws.send(JSON.stringify(msg));
}

function stateMsg(admin) {
  return { type: 'state', state: game.snapshot({ admin }), tiktok: tiktokStatus, demo: !!demoTimer };
}

let pending = null;
function broadcastState() {
  if (pending) return;
  pending = setTimeout(() => {
    pending = null;
    const pub = JSON.stringify(stateMsg(false));
    const adm = JSON.stringify(stateMsg(true));
    for (const ws of wss.clients) if (ws.readyState === 1) ws.send(ws.isAdmin ? adm : pub);
  }, 80);
}

game.onChange = () => {
  dirty = true;
  broadcastState();
};
game.onEvent = (ev) => {
  const msg = JSON.stringify({ type: 'event', event: ev });
  for (const ws of wss.clients) if (ws.readyState === 1) ws.send(msg);
};

wss.on('connection', (ws, req) => {
  // Yönetim komutlarını yalnızca bu sunucunun kendi sayfasından kabul et
  const origin = req.headers.origin;
  let sameOrigin = !origin;
  try {
    sameOrigin ||= new URL(origin).host === req.headers.host;
  } catch {
    /* geçersiz origin */
  }
  const q = new URL(req.url, 'http://x').searchParams;
  const wantsAdmin = q.get('role') === 'admin';
  // Yerelde şifresiz; internete açıkken (veya şifre tanımlıysa) ADMIN_KEY zorunlu
  const keyOk = ADMIN_KEY ? q.get('key') === ADMIN_KEY : !PUBLIC_HOST;
  ws.isAdmin = sameOrigin && wantsAdmin && keyOk;
  if (wantsAdmin && !ws.isAdmin) send(ws, { type: 'auth', ok: false });
  send(ws, stateMsg(ws.isAdmin));
  ws.on('message', (raw) => {
    if (!ws.isAdmin) return;
    let msg;
    try {
      msg = JSON.parse(raw);
    } catch {
      return;
    }
    try {
      handleCommand(msg);
    } catch (e) {
      console.error('Komut hatası:', e);
    }
  });
});

// ---------- TikTok ----------
const tiktok = new TikTokSource({
  signApiKey: config.signApiKey,
  onGift: (g) => {
    console.log(`🎁 ${g.name} → ${g.giftName} x${g.count} (+${g.coins})`);
    game.handleGift(g);
  },
  onChat: (c) => game.handleChat(c),
  onMember: (u) => viewerJoined(u),
  onStatus: (s) => {
    tiktokStatus = s;
    if (s.state === 'connected' && demoTimer) setDemo(false);
    console.log(`[TikTok] ${s.message}`);
    broadcastState();
  },
});

// ---------- yeni izleyiciler (spiker adıyla karşılar) ----------
// Kalabalık yayında ekranı boğmasın diye 10 saniyede en fazla 5 isim iletilir.
let viewerWindow = [];
function viewerJoined(u) {
  const now = Date.now();
  viewerWindow = viewerWindow.filter((t) => now - t < 10000);
  if (!u?.name || viewerWindow.length >= 5) return;
  viewerWindow.push(now);
  game.onEvent({ type: 'viewer', name: String(u.name).slice(0, 30), avatar: u.avatar || '', ts: now });
}

// ---------- demo / test ----------
const FAKE_NAMES = ['Ahmet', 'Elif', 'Mert', 'Zeynep', 'Can', 'Ayşe', 'Burak', 'Selin', 'Emre', 'Deniz', 'Kerem', 'Ece', 'Onur', 'Merve', 'Arda', 'Buse', 'Yusuf', 'İrem', 'Kaan', 'Derya', 'Efe', 'Sude', 'Barış', 'Nehir', 'Umut', 'Ceren', 'Tolga', 'Melis', 'Serkan', 'Gizem'];
const FAKE_GIFTS = [
  { giftName: 'Gül', coins: 1, w: 50 },
  { giftName: 'TikTok', coins: 1, w: 20 },
  { giftName: 'Parmak Kalp', coins: 5, w: 15 },
  { giftName: 'Dondurma', coins: 1, w: 10 },
  { giftName: 'Parfüm', coins: 20, w: 6 },
  { giftName: 'Kalp Gözler', coins: 30, w: 4 },
  { giftName: 'Kaplan', coins: 99, w: 1.5 },
  { giftName: 'Aslan', coins: 299, w: 0.5 },
];

function fakeUser(i = Math.floor(Math.random() * FAKE_NAMES.length)) {
  const name = FAKE_NAMES[i % FAKE_NAMES.length];
  return { id: `demo_${i}`, name: `${name}${i >= FAKE_NAMES.length ? i : ''}`, avatar: fakeAvatar(i) };
}

function fakeGift(coins) {
  let g;
  if (coins) g = { giftName: coins >= 100 ? 'Aslan' : coins >= 10 ? 'Parfüm' : 'Gül', coins };
  else {
    const total = FAKE_GIFTS.reduce((a, x) => a + x.w, 0);
    let r = Math.random() * total;
    g = FAKE_GIFTS.find((x) => (r -= x.w) <= 0) ?? FAKE_GIFTS[0];
  }
  const u = fakeUser(Math.floor(Math.random() * 30));
  const count = coins ? 1 : 1 + Math.floor(Math.random() * 3);
  game.handleGift({ ...u, giftName: g.giftName, count, coins: g.coins * count });
}

let demoViewerTimer = null;
function setDemo(on) {
  clearInterval(demoTimer);
  clearInterval(demoViewerTimer);
  demoTimer = on ? setInterval(() => fakeGift(), 900) : null;
  demoViewerTimer = on ? setInterval(() => viewerJoined(fakeUser(30 + Math.floor(Math.random() * 40))), 7000) : null;
  broadcastState();
}

// ---------- maç saati ----------
let tickTimer = null;
function restartTicker() {
  clearInterval(tickTimer);
  const ms = Math.max(250, (config.match?.secondsPerMinute ?? 2) * 1000);
  tickTimer = setInterval(() => game.tick(), ms);
}
restartTicker();
setInterval(() => game.autoTick(), 1000);

// Gerçek yayına bağlıyken sahte hediye üretmek izleyicileri yanıltır; engelle.
const isLive = () => tiktokStatus.state === 'connected';

// ---------- yönetim komutları ----------
function handleCommand(m) {
  switch (m.action) {
    case 'connect':
      if (m.username) config.tiktokUser = m.username;
      tiktok.connect(config.tiktokUser);
      break;
    case 'disconnect':
      tiktok.disconnect();
      break;
    case 'startMatch':
      game.startMatch();
      break;
    case 'pauseMatch':
      game.pauseMatch();
      break;
    case 'resetMatch':
      game.resetMatch();
      break;
    case 'resetAll':
      game.resetAll();
      break;
    case 'demo':
      setDemo(!!m.on && !isLive());
      break;
    case 'fakeGift':
      if (!isLive()) fakeGift(Number(m.coins) || 0);
      break;
    case 'auto':
      game.setAuto(!!m.on);
      break;
    case 'fakeChat': {
      if (isLive()) break;
      const u = fakeUser(Math.floor(Math.random() * 30));
      game.handleChat({ ...u, text: m.text });
      break;
    }
    case 'addManual':
      game.addManual({ name: String(m.name ?? '').trim(), coins: Number(m.coins) || 0, team: m.team || null });
      break;
    case 'move':
      game.movePlayer(m.id, m.team);
      break;
    case 'kick':
      game.kick(m.id, m.ban !== false);
      break;
    case 'unbanAll':
      game.unbanAll();
      break;
    case 'teamName':
      game.setTeamName(m.team, m.name);
      break;
    case 'speed':
      config.match.secondsPerMinute = Math.min(10, Math.max(0.25, Number(m.value) || 2));
      restartTicker();
setInterval(() => game.autoTick(), 1000);

// Gerçek yayına bağlıyken sahte hediye üretmek izleyicileri yanıltır; engelle.
const isLive = () => tiktokStatus.state === 'connected';
      break;
    default:
      break;
  }
}

// ---------- başlat ----------
server.listen(PORT, HOST, () => {
  const base = `http://localhost:${PORT}`;
  console.log('');
  console.log('⚽  TikTok Canlı Futbol hazır');
  console.log(`   Yayın ekranı   : ${base}/`);
  console.log(`   Yönetim paneli : ${base}/admin`);
  console.log('');
  if (flag('demo')) {
    console.log('   DEMO modu: sahte hediyeler geliyor (TikTok bağlantısı yok).');
    setDemo(true);
  } else {
    tiktok.connect(flagValue('user') ?? config.tiktokUser);
  }
});

function shutdown() {
  fs.mkdirSync(DATA_DIR, { recursive: true });
  fs.writeFileSync(STATE_FILE, JSON.stringify(game));
  process.exit(0);
}
process.on('SIGINT', shutdown);
process.on('SIGTERM', shutdown);
