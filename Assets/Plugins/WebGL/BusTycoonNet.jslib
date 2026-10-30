// Çok oyunculu bağlantı (WebRoomTransport.cs okur): PeerJS ile tarayıcıdan tarayıcıya WebRTC veri kanalı. Odayı kuran
// "otobus-imparatoru-v1-KOD" kimliğiyle PeerJS'in ücretsiz eşleştirme sunucusuna kaydolur, katılanlar ona bağlanır.
// Olaylar JSON metni olarak kuyruğa alınır, oyun her karede BusTycoon_NetPoll ile okur (ağ geri çağrıları Unity'ye
// doğrudan dokunmaz). Kitaplık ilk kullanımda CDN'den yüklenir; tek oyunculu oyun etkilenmez.
//
// Dayanıklılık:
// - Eşleştirme sunucusu bağlantısı koparsa (telefonda başka uygulamaya geçip kodu paylaşmak gibi) oda kapanmaz:
//   aynı kimlikle artan aralıklarla ve sayfa yeniden görününce hemen yeniden bağlanılır. Oyuncular arası veri
//   kanalları bu sırada sürer.
// - Hazır olduktan sonraki (kurucu: oda açıldı, katılımcı: bağlandı) sunucu hataları oyuna bildirilmez.
// - Kurucuda tek bir katılımcının bağlantı hatası odayı kapatmaz; o oyuncu "left" olur.
// - Katılımcı 25 sn içinde bağlanamazsa "timeout" (farklı ağlarda NAT engeli gibi durumlar için açık mesaj).
// - STUN'a ek olarak PeerJS'in TURN aktarıcıları kullanılır (doğrudan bağlantı kurulamayan ağlar).
mergeInto(LibraryManager.library, {
  BusTycoon_NetStart: function (asHost, codePtr) {
    var code = UTF8ToString(codePtr);
    var old = Module.busTycoonNet;
    if (old) old.stop(0);
    var net = Module.busTycoonNet = { queue: [], conns: {}, host: asHost === 1, peer: null, hostConn: null, closed: false, ready: false, failed: false, timers: [] };
    var prefix = "otobus-imparatoru-v1-";
    var iceServers = [
      { urls: ["stun:stun.l.google.com:19302", "stun:stun1.l.google.com:19302"] },
      { urls: "stun:stun.cloudflare.com:3478" },
      { urls: ["turn:eu-0.turn.peerjs.com:3478", "turn:us-0.turn.peerjs.com:3478"], username: "peerjs", credential: "peerjsp" }
    ];

    function push(type, peer, data) {
      if (net.closed) return;
      net.queue.push(JSON.stringify({ t: type, p: peer || "", d: data == null ? "" : String(data) }));
    }
    // Ölümcül hata yalnız bir kez bildirilir (bağlantı hatası ardından kapanma olayı da gelir)
    function fail(type) { if (net.failed) return; net.failed = true; push("error", "", type); }
    function later(fn, ms) { var id = setTimeout(function () { if (!net.closed) fn(); }, ms); net.timers.push(id); return id; }
    function errorType(e) { return String((e && e.type) || e || "network"); }
    function log(text) { try { console.log("[Multiplayer] ağ: " + text); } catch (e) {} }

    net.stop = function (delay) {
      if (net.closed) return;
      net.closed = true;
      net.timers.forEach(clearTimeout);
      document.removeEventListener("visibilitychange", onVisible);
      window.removeEventListener("online", onVisible);
      var peer = net.peer, conns = net.conns, hostConn = net.hostConn;
      // Kısa gecikme: son iletiler (BYE) gönderilebilsin
      setTimeout(function () {
        try { for (var id in conns) conns[id].close(); } catch (e) {}
        try { if (hostConn) hostConn.close(); } catch (e) {}
        try { if (peer) peer.destroy(); } catch (e) {}
      }, delay || 0);
    };

    // Eşleştirme sunucusuna yeniden bağlan (aynı kimlik ve anahtar korunur)
    var retryDelay = 1000, retryTimer = 0;
    function reconnect(now) {
      var peer = net.peer;
      if (net.closed || !peer || peer.destroyed || !peer.disconnected) return;
      if (retryTimer) { clearTimeout(retryTimer); retryTimer = 0; }
      if (!now) {
        retryTimer = later(function () { retryTimer = 0; reconnect(true); }, retryDelay);
        retryDelay = Math.min(retryDelay * 2, 15000);
        return;
      }
      log("eşleştirme sunucusuna yeniden bağlanılıyor");
      try { peer.reconnect(); } catch (e) { reconnect(false); }
    }
    function onVisible() { if (document.visibilityState !== "hidden") { retryDelay = 1000; reconnect(true); } }
    document.addEventListener("visibilitychange", onVisible);
    window.addEventListener("online", onVisible);

    function attach(conn, name) {
      conn.on("data", function (d) { push("message", name, typeof d === "string" ? d : JSON.stringify(d)); });
      conn.on("close", function () {
        if (net.host) { if (net.conns[name]) { delete net.conns[name]; push("left", name); } }
        else if (net.ready) push("closed", "host");
        else fail("negotiation-failed");
      });
      conn.on("error", function (e) {
        log((net.host ? name : "kurucu") + " bağlantı hatası: " + errorType(e));
        // Kurucuda bir oyuncunun hatası odayı etkilemez (ardından "close" gelir); katılımcıda bağlanmadan önceki hata ölümcül
        if (!net.host && !net.ready) fail(errorType(e));
      });
    }

    function begin() {
      if (net.closed) return;
      if (typeof Peer === "undefined") { fail("script"); return; }
      var peer, options = { debug: 1, config: { iceServers: iceServers } };
      try { peer = net.host ? new Peer(prefix + code, options) : new Peer(options); }
      catch (e) { fail("browser-incompatible"); return; }
      net.peer = peer;
      later(function () {
        if (!net.ready) { log("zaman aşımı"); fail(net.host ? "server-timeout" : "timeout"); }
      }, net.host ? 20000 : 25000);

      peer.on("open", function (id) {
        retryDelay = 1000;
        if (net.host) {
          if (!net.ready) { net.ready = true; push("hosted", "", id); }
          return;
        }
        if (net.hostConn) return; // yeniden bağlanma: veri kanalı zaten var
        var conn = peer.connect(prefix + code, { reliable: true });
        net.hostConn = conn;
        conn.on("open", function () { net.ready = true; push("connected", "host"); });
        attach(conn, "host");
      });
      peer.on("connection", function (conn) {
        if (!net.host) { conn.close(); return; }
        var id = conn.peer;
        conn.on("open", function () {
          var previous = net.conns[id];
          net.conns[id] = conn;
          if (previous && previous !== conn) { try { previous.close(); } catch (e) {} }
          else push("joined", id);
        });
        attach(conn, id);
      });
      peer.on("error", function (e) {
        var type = errorType(e);
        log("hata: " + type);
        if (net.ready) {
          // Hazır olduktan sonra sunucu hataları geçicidir; bağlantı kopmuşsa yeniden denenir
          if (peer.disconnected) reconnect(false);
          return;
        }
        if (type === "network" || type === "server-error" || type === "socket-error" || type === "socket-closed" || type === "disconnected") {
          if (peer.disconnected && !peer.destroyed) { reconnect(false); return; } // zaman aşımına kadar dene
        }
        fail(type);
      });
      peer.on("disconnected", function () { if (!net.closed) { log("eşleştirme sunucusu bağlantısı koptu"); reconnect(false); } });
    }

    if (typeof Peer !== "undefined") { begin(); return; }
    if (!Module.busTycoonPeerLoading) {
      Module.busTycoonPeerLoading = [];
      var script = document.createElement("script");
      script.src = "https://cdn.jsdelivr.net/npm/peerjs@1.5.4/dist/peerjs.min.js";
      script.onload = function () { var list = Module.busTycoonPeerLoading; Module.busTycoonPeerLoading = null; list.forEach(function (f) { f(); }); };
      script.onerror = function () { var list = Module.busTycoonPeerLoading; Module.busTycoonPeerLoading = null; script.remove(); list.forEach(function (f) { f(true); }); };
      document.head.appendChild(script);
    }
    Module.busTycoonPeerLoading.push(function (failed) { if (failed) fail("script"); else begin(); });
  },

  BusTycoon_NetSend: function (peerPtr, dataPtr) {
    var net = Module.busTycoonNet;
    if (!net || net.closed) return;
    var conn = net.host ? net.conns[UTF8ToString(peerPtr)] : net.hostConn;
    if (conn && conn.open) { try { conn.send(UTF8ToString(dataPtr)); } catch (e) {} }
  },

  BusTycoon_NetBroadcast: function (dataPtr) {
    var net = Module.busTycoonNet;
    if (!net || net.closed || !net.host) return;
    var data = UTF8ToString(dataPtr);
    for (var id in net.conns) {
      var conn = net.conns[id];
      if (conn && conn.open) { try { conn.send(data); } catch (e) {} }
    }
  },

  BusTycoon_NetStop: function () {
    var net = Module.busTycoonNet;
    if (!net) return;
    net.stop(400);
    Module.busTycoonNet = null;
  },

  BusTycoon_NetPoll: function () {
    var net = Module.busTycoonNet;
    if (!net || net.queue.length === 0) return null;
    var text = net.queue.shift();
    var size = lengthBytesUTF8(text) + 1;
    var buffer = _malloc(size);
    stringToUTF8(text, buffer, size);
    return buffer;
  }
});
