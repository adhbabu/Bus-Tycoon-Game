// TikTok LIVE bağlantısı (resmî olmayan tiktok-live-connector kütüphanesi ile).
// Kütüphanenin farklı sürümlerindeki veri biçimlerini tek biçime çevirir.

export function normalizeUser(d) {
  const u = d?.user ?? d ?? {};
  const uniqueId = u.uniqueId || u.displayId || d?.uniqueId || '';
  const id = String(uniqueId || u.userId || d?.userId || u.id || u.nickname || '');
  const name = u.nickname || d?.nickname || uniqueId || id;
  const avatar =
    u.profilePictureUrl ||
    d?.profilePictureUrl ||
    u.avatarThumb?.urlList?.[0] ||
    u.avatarMedium?.urlList?.[0] ||
    u.profilePicture?.urls?.[0] ||
    u.profilePicture?.url?.[0] ||
    '';
  return { id, uniqueId, name, avatar };
}

// Seri (combo) hediyelerde TikTok aynı hediyeyi artan repeatCount ile tekrar tekrar
// gönderir. Her kullanıcı+hediye için son sayıyı tutup yalnızca farkı sayarız;
// böylece hediye anında ekrana yansır ama iki kez sayılmaz.
export class GiftTracker {
  constructor() {
    this.streaks = new Map();
  }

  process(d) {
    const user = normalizeUser(d);
    if (!user.id) return null;
    const gift = d.gift || d.giftDetails || {};
    const ext = d.extendedGiftInfo || {};
    const diamonds =
      Number(d.diamondCount ?? gift.diamondCount ?? gift.diamond_count ?? ext.diamond_count ?? ext.diamondCount ?? 0) || 1;
    const giftType = Number(d.giftType ?? gift.type ?? gift.giftType ?? ext.type ?? 0);
    const repeat = Math.max(1, Number(d.repeatCount) || 1);
    const ended = d.repeatEnd === true || Number(d.repeatEnd) > 0;
    const giftName = d.giftName || gift.name || gift.giftName || ext.name || 'Hediye';
    const icon =
      gift.image?.urlList?.[0] || gift.icon?.urlList?.[0] || d.giftPictureUrl || ext.image?.url_list?.[0] || '';

    let count = repeat;
    if (giftType === 1) {
      const key = `${user.id}|${d.giftId ?? giftName}|${d.groupId ?? ''}`;
      const last = this.streaks.get(key)?.count ?? 0;
      count = repeat >= last ? repeat - last : repeat;
      if (ended) this.streaks.delete(key);
      else this.streaks.set(key, { count: repeat, t: Date.now() });
      this.cleanup();
    }
    if (count <= 0) return null;
    return { ...user, giftName, icon, count, coins: count * diamonds };
  }

  cleanup() {
    if (this.streaks.size < 200) return;
    const old = Date.now() - 5 * 60 * 1000;
    for (const [k, v] of this.streaks) if (v.t < old) this.streaks.delete(k);
  }
}

export class TikTokSource {
  constructor({ signApiKey, onGift, onChat, onMember, onStatus }) {
    this.signApiKey = signApiKey;
    this.onGift = onGift;
    this.onChat = onChat;
    this.onMember = onMember;
    this.onStatus = onStatus;
    this.conn = null;
    this.username = '';
    this.wanted = false;
    this.retryTimer = null;
    this.tracker = new GiftTracker();
  }

  status(state, message) {
    this.onStatus({ state, message, username: this.username });
  }

  async connect(username) {
    await this.disconnect();
    this.username = String(username || '').replace(/^@/, '').trim();
    if (!this.username || this.username === 'kullanici_adin') {
      this.status('error', 'config.json içindeki tiktokUser alanına kendi TikTok kullanıcı adını yaz.');
      return;
    }
    this.wanted = true;
    this.tryConnect();
  }

  async tryConnect() {
    clearTimeout(this.retryTimer);
    if (!this.wanted) return;
    let lib;
    try {
      lib = await import('tiktok-live-connector');
    } catch {
      this.status('error', 'tiktok-live-connector bulunamadı. Klasörde "npm install" çalıştır.');
      return;
    }
    const { TikTokLiveConnection, WebcastEvent, ControlEvent } = lib;
    this.status('connecting', `@${this.username} yayınına bağlanılıyor...`);

    const conn = new TikTokLiveConnection(this.username, {
      processInitialData: false, // yeniden bağlanınca eski hediyeler iki kez sayılmasın
      enableExtendedGiftInfo: false,
      signApiKey: this.signApiKey || undefined,
    });
    this.conn = conn;

    conn.on(WebcastEvent.GIFT, (d) => {
      try {
        const g = this.tracker.process(d);
        if (g) this.onGift(g);
      } catch (e) {
        console.error('Hediye işlenemedi:', e);
      }
    });
    conn.on(WebcastEvent.CHAT, (d) => {
      const u = normalizeUser(d);
      if (u.id) this.onChat({ ...u, text: d.comment ?? '' });
    });
    conn.on(WebcastEvent.MEMBER, (d) => {
      const u = normalizeUser(d);
      if (u.id && this.onMember) this.onMember(u);
    });
    conn.on(WebcastEvent.STREAM_END, () => {
      this.status('offline', 'Yayın sona erdi. Yayın tekrar başlayınca otomatik bağlanılacak.');
      this.scheduleRetry(30);
    });
    conn.on(ControlEvent.DISCONNECTED, () => {
      if (this.conn !== conn || !this.wanted) return;
      this.status('connecting', 'Bağlantı koptu, 10 sn içinde yeniden denenecek...');
      this.scheduleRetry(10);
    });
    conn.on(ControlEvent.ERROR, (e) => console.error('TikTok hata:', e?.message ?? e));

    try {
      const state = await conn.connect();
      if (this.conn !== conn) return;
      this.status('connected', `@${this.username} yayınına bağlandı (oda ${state.roomId})`);
    } catch (e) {
      if (this.conn !== conn) return;
      const msg = String(e?.message ?? e);
      const offline = /offline|not live|UserOffline|room ?id/i.test(msg) || e?.name === 'UserOfflineError';
      this.status(
        offline ? 'offline' : 'error',
        offline
          ? `@${this.username} yayını bulunamadı: ya şu an canlı yayında değilsin ya da kullanıcı adı yanlış. 30 sn sonra tekrar denenecek.`
          : `Bağlanılamadı: ${msg}`,
      );
      this.scheduleRetry(offline ? 30 : 20);
    }
  }

  scheduleRetry(sec) {
    clearTimeout(this.retryTimer);
    if (this.wanted) this.retryTimer = setTimeout(() => this.tryConnect(), sec * 1000);
  }

  async disconnect() {
    this.wanted = false;
    clearTimeout(this.retryTimer);
    const c = this.conn;
    this.conn = null;
    if (c) {
      try {
        await c.disconnect();
      } catch {
        /* zaten kapalı */
      }
    }
    if (this.username) this.status('idle', 'Bağlantı kapalı');
  }
}
