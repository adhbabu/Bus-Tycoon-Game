// Çok oyunculu bağlantı (WebRoomTransport.cs okur): PeerJS ile tarayıcıdan tarayıcıya WebRTC veri kanalı. Odayı kuran
// "otobus-imparatoru-v1-KOD" kimliğiyle PeerJS'in ücretsiz eşleştirme sunucusuna kaydolur, katılanlar ona bağlanır.
// Olaylar JSON metni olarak kuyruğa alınır, oyun her karede BusTycoon_NetPoll ile okur (ağ geri çağrıları Unity'ye
// doğrudan dokunmaz). Kitaplık ilk kullanımda CDN'den yüklenir; tek oyunculu oyun etkilenmez.
mergeInto(LibraryManager.library, {
  BusTycoon_NetStart: function (asHost, codePtr) {
    var code = UTF8ToString(codePtr);
    var old = Module.busTycoonNet;
    if (old && old.peer) { try { old.peer.destroy(); } catch (e) {} }
    var net = Module.busTycoonNet = { queue: [], conns: {}, host: asHost === 1, peer: null, hostConn: null, closed: false };
    var prefix = "otobus-imparatoru-v1-";

    function push(type, peer, data) {
      if (net.closed) return;
      net.queue.push(JSON.stringify({ t: type, p: peer || "", d: data == null ? "" : String(data) }));
    }
    function errorType(e) { return String((e && e.type) || e || "network"); }
    function attach(conn, name) {
      conn.on("data", function (d) { push("message", name, typeof d === "string" ? d : JSON.stringify(d)); });
      conn.on("close", function () {
        if (net.host) { if (net.conns[name]) { delete net.conns[name]; push("left", name); } }
        else push("closed", "host");
      });
      conn.on("error", function (e) { push("error", name, errorType(e)); });
    }
    function begin() {
      if (net.closed) return;
      if (typeof Peer === "undefined") { push("error", "", "script"); return; }
      var peer;
      try { peer = net.host ? new Peer(prefix + code, { debug: 1 }) : new Peer({ debug: 1 }); }
      catch (e) { push("error", "", "browser-incompatible"); return; }
      net.peer = peer;
      peer.on("open", function (id) {
        if (net.host) { push("hosted", "", id); return; }
        var conn = peer.connect(prefix + code, { reliable: true });
        net.hostConn = conn;
        conn.on("open", function () { push("connected", "host"); });
        attach(conn, "host");
      });
      peer.on("connection", function (conn) {
        if (!net.host) { conn.close(); return; }
        var id = conn.peer;
        conn.on("open", function () { net.conns[id] = conn; push("joined", id); });
        attach(conn, id);
      });
      peer.on("error", function (e) { push("error", "", errorType(e)); });
      // Eşleştirme sunucusu bağlantısı koparsa (veri kanalları sürebilir) yeniden dene
      peer.on("disconnected", function () { if (!net.closed) { try { peer.reconnect(); } catch (e) {} } });
    }
    if (typeof Peer !== "undefined") { begin(); return; }
    if (!Module.busTycoonPeerLoading) {
      Module.busTycoonPeerLoading = [];
      var script = document.createElement("script");
      script.src = "https://cdn.jsdelivr.net/npm/peerjs@1.5.4/dist/peerjs.min.js";
      script.onload = function () { var list = Module.busTycoonPeerLoading; Module.busTycoonPeerLoading = null; list.forEach(function (f) { f(); }); };
      script.onerror = function () { var list = Module.busTycoonPeerLoading; Module.busTycoonPeerLoading = null; list.forEach(function (f) { f(true); }); };
      document.head.appendChild(script);
    }
    Module.busTycoonPeerLoading.push(function (failed) { if (failed) push("error", "", "script"); else begin(); });
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
    net.closed = true;
    try { for (var id in net.conns) net.conns[id].close(); } catch (e) {}
    try { if (net.hostConn) net.hostConn.close(); } catch (e) {}
    try { if (net.peer) net.peer.destroy(); } catch (e) {}
    Module.busTycoonNet = null;
  },

  // Davet bağlantısını panoya kopyala (tıklamadan hemen sonra çağrılır; izin yoksa sessizce vazgeçer)
  BusTycoon_CopyText: function (textPtr) {
    var text = UTF8ToString(textPtr);
    try {
      if (navigator.clipboard && navigator.clipboard.writeText) { navigator.clipboard.writeText(text).catch(function () {}); return; }
    } catch (e) {}
    try {
      var area = document.createElement("textarea");
      area.value = text; area.style.position = "fixed"; area.style.opacity = "0";
      document.body.appendChild(area); area.select(); document.execCommand("copy"); document.body.removeChild(area);
    } catch (e) {}
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
