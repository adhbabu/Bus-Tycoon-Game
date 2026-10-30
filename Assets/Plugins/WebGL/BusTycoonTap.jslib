// Dokunuşla açılan tarayıcı işlemleri (WebTap.cs): iPad/telefonda yazı girişi (klavyeli tarayıcı penceresi) ve davet
// bağlantısını paylaşma/kopyalama. Tarayıcılar klavye, paylaşım menüsü ve panoya yalnız kullanıcının dokunuşu
// sırasında izin verir; Unity ise dokunuşu bir sonraki karede işler. Bu yüzden oyun her karede ekrandaki alanları
// (kimlik, dikdörtgen, işlem) bildirir, iş doğrudan dokunma/tıklama olayının içinde yapılır, sonuç kuyruktan okunur.
// Alanlar tuval pikseli cinsindendir (sol üst köşe başlangıç), Unity'nin Screen ve GUI koordinatlarıyla aynıdır.
mergeInto(LibraryManager.library, {
  BusTycoon_TapBegin: function () {
    var tap = Module.busTycoonTap;
    if (!tap) {
      tap = Module.busTycoonTap = { zones: [], building: [], results: [], down: null, lastTouchUp: 0 };

      function toCanvas(x, y) {
        var canvas = Module.canvas;
        if (!canvas) return null;
        var r = canvas.getBoundingClientRect();
        return { x: (x - r.left) * canvas.width / Math.max(1, r.width), y: (y - r.top) * canvas.height / Math.max(1, r.height) };
      }
      function find(p) {
        if (!p) return null;
        for (var i = tap.zones.length - 1; i >= 0; i--) {
          var z = tap.zones[i];
          if (p.x >= z.x && p.x <= z.x + z.w && p.y >= z.y && p.y <= z.y + z.h) return z;
        }
        return null;
      }
      function done(z, value) { tap.results.push(z.id + "\n" + value); }

      function copyText(text) {
        try {
          var area = document.createElement("textarea");
          area.value = text;
          area.setAttribute("readonly", "");
          area.style.position = "fixed"; area.style.top = "0"; area.style.left = "0"; area.style.opacity = "0";
          document.body.appendChild(area);
          area.focus(); area.select(); area.setSelectionRange(0, text.length);
          var ok = document.execCommand("copy");
          document.body.removeChild(area);
          return ok;
        } catch (e) { return false; }
      }
      function share(z, touch) {
        var url = z.a, text = z.b;
        // Telefon/tablette paylaşım menüsü (WhatsApp, Mesajlar…); masaüstünde panoya kopyala
        if (touch && navigator.share) {
          try {
            navigator.share({ title: document.title, text: text, url: url }).then(
              function () { done(z, "shared"); },
              function (err) { if (!err || err.name !== "AbortError") { window.prompt("Bu bağlantıyı kopyalayıp arkadaşına gönder:", url); done(z, "shown"); } });
            return;
          } catch (e) {}
        }
        if (navigator.clipboard && navigator.clipboard.writeText) {
          try {
            navigator.clipboard.writeText(url).then(
              function () { done(z, "copied"); },
              function () { done(z, copyText(url) ? "copied" : (window.prompt("Bu bağlantıyı kopyalayıp arkadaşına gönder:", url), "shown")); });
            return;
          } catch (e) {}
        }
        if (copyText(url)) done(z, "copied");
        else { window.prompt("Bu bağlantıyı kopyalayıp arkadaşına gönder:", url); done(z, "shown"); }
      }
      function run(z, touch) {
        if (z.kind === 1) {
          var value = window.prompt(z.a, z.b);
          if (value !== null) done(z, value);
        } else if (z.kind === 2) share(z, touch);
      }

      // Basış ve bırakış aynı alanda, kaydırmadan olmalı (paneli parmakla kaydırırken açılmasın)
      function press(x, y) { var p = toCanvas(x, y); tap.down = p ? { p: p, z: find(p) } : null; }
      function release(x, y, touch) {
        var down = tap.down; tap.down = null;
        if (!down || !down.z) return false;
        var p = toCanvas(x, y);
        var slop = 14 * (window.devicePixelRatio || 1);
        if (!p || Math.abs(p.x - down.p.x) > slop || Math.abs(p.y - down.p.y) > slop || find(p) !== down.z) return false;
        run(down.z, touch);
        return true;
      }
      window.addEventListener("touchstart", function (e) {
        var t = e.changedTouches && e.changedTouches[0];
        if (t && e.touches.length === 1) press(t.clientX, t.clientY); else tap.down = null;
      }, { capture: true, passive: true });
      window.addEventListener("touchend", function (e) {
        var t = e.changedTouches && e.changedTouches[0];
        if (t) { tap.lastTouchUp = Date.now(); release(t.clientX, t.clientY, true); }
      }, true);
      window.addEventListener("mousedown", function (e) {
        if (e.button === 0 && Date.now() - tap.lastTouchUp > 1000) press(e.clientX, e.clientY);
      }, true);
      window.addEventListener("mouseup", function (e) {
        if (e.button === 0 && Date.now() - tap.lastTouchUp > 1000) release(e.clientX, e.clientY, false);
      }, true);
    }
    tap.building = [];
  },

  // kind: 1 yazı girişi (a = başlık, b = mevcut değer) · 2 bağlantı paylaş/kopyala (a = adres, b = metin)
  BusTycoon_TapZone: function (idPtr, kind, x, y, w, h, aPtr, bPtr) {
    var tap = Module.busTycoonTap;
    if (!tap) return;
    tap.building.push({ id: UTF8ToString(idPtr), kind: kind, x: x, y: y, w: w, h: h, a: UTF8ToString(aPtr), b: UTF8ToString(bPtr) });
  },

  BusTycoon_TapEnd: function () {
    var tap = Module.busTycoonTap;
    if (!tap) return;
    tap.zones = tap.building;
    tap.building = [];
  },

  // "kimlik\ndeğer" ya da boş
  BusTycoon_TapPoll: function () {
    var tap = Module.busTycoonTap;
    if (!tap || tap.results.length === 0) return null;
    var text = tap.results.shift();
    var size = lengthBytesUTF8(text) + 1;
    var buffer = _malloc(size);
    stringToUTF8(text, buffer, size);
    return buffer;
  }
});
