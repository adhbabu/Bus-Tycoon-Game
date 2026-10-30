// Tarayıcı girdisi: tekerlek olaylarının türü (WheelInput.cs) ve dokunmatik algılama (TouchInput.cs).
// Tekerlek olayları şöyle sınıflandırılır:
//   1 fare tekerleği → yakınlaştırma · 2 touchpad iki parmak kaydırma → harita kaydırma · 3 sıkıştırma → yakınlaştırma
// Unity'nin kendi olay işleyişi bozulmaz; değerler kare başında okunup sıfırlanır.
mergeInto(LibraryManager.library, {
  BusTycoon_InitWheel: function () {
    if (Module.busTycoonWheel) return;
    var state = Module.busTycoonWheel = { kind: 0, dx: 0, dy: 0, gestureScale: 1 };
    var canvas = Module.canvas;
    if (!canvas) return;

    function pixelScale() { return canvas.width / Math.max(1, canvas.clientWidth); }

    canvas.addEventListener("wheel", function (e) {
      var kind;
      if (e.ctrlKey) kind = 3;                                    // touchpad sıkıştırma (Chrome, Edge, Firefox)
      else if (e.deltaMode !== 0) kind = 1;                       // satır/sayfa birimi: fare tekerleği (Firefox)
      else if (e.deltaX !== 0) kind = 2;                          // yatay bileşen: touchpad
      else if (typeof e.wheelDeltaY === "number" && e.wheelDeltaY !== 0) kind = Math.abs(e.wheelDeltaY) % 120 === 0 ? 1 : 2;
      else kind = Math.abs(e.deltaY) >= 50 && e.deltaY === Math.round(e.deltaY) ? 1 : 2;

      var unit = e.deltaMode === 1 ? 16 : e.deltaMode === 2 ? 400 : 1;
      var scale = pixelScale() * unit;
      state.kind = kind;
      state.dx += e.deltaX * scale;
      state.dy += e.deltaY * scale;
      if (e.ctrlKey) e.preventDefault();                          // sayfanın kendisi yakınlaşmasın
    }, { passive: false, capture: true });

    // Safari (macOS) sıkıştırmayı tekerlek yerine jest olayıyla bildirir.
    canvas.addEventListener("gesturestart", function (e) { state.gestureScale = 1; e.preventDefault(); });
    canvas.addEventListener("gesturechange", function (e) {
      state.kind = 3;
      state.dy += (state.gestureScale - e.scale) * 400 * pixelScale();
      state.gestureScale = e.scale;
      e.preventDefault();
    });
  },

  // Birincil girdi dokunmatik mi? iPadOS Safari kendini masaüstü Mac olarak tanıttığından Unity onu mobil
  // saymaz; dokunma noktası olan "Macintosh" iPad'dir.
  BusTycoon_IsTouchPrimary: function () {
    var coarse = typeof window.matchMedia === "function" && window.matchMedia("(pointer: coarse)").matches;
    var ipad = (navigator.maxTouchPoints || 0) > 0 && /Macintosh/.test(navigator.userAgent);
    return coarse || ipad ? 1 : 0;
  },

  // Son basış parmakla mı yapıldı (dokunmatik ekranlı bilgisayarda da paneller parmakla kaysın).
  BusTycoon_InitPointer: function () {
    if (Module.busTycoonPointer) return;
    var state = Module.busTycoonPointer = { touch: 0 };
    var canvas = Module.canvas;
    if (!canvas) return;
    canvas.addEventListener("pointerdown", function (e) { state.touch = e.pointerType === "mouse" ? 0 : 1; }, { capture: true, passive: true });
    canvas.addEventListener("touchstart", function () { state.touch = 1; }, { capture: true, passive: true }); // işaretçi olayı olmayan eski tarayıcılar
  },

  BusTycoon_LastPointerTouch: function () {
    var state = Module.busTycoonPointer;
    return state ? state.touch : 0;
  },

  // Tuval pikseli / CSS pikseli. iOS'ta 1 CSS pikseli 1 noktadır: telefonda arayüz ölçeği fiziksel boyuta göre ayarlanır.
  BusTycoon_CanvasScale: function () {
    var canvas = Module.canvas;
    if (!canvas) return 0;
    var width = canvas.getBoundingClientRect().width;
    return width > 0 ? canvas.width / width : 0;
  },

  // Çentik/ada ve ana ekran çubuğu için güvenli alan payı (CSS env(safe-area-inset-*)), tuval pikseli.
  // side: 0 sol, 1 sağ, 2 üst, 3 alt. Ölçüm için görünmez bir kutu bir kez eklenir.
  BusTycoon_SafeInset: function (side) {
    var canvas = Module.canvas;
    if (!canvas || !document.body) return 0;
    var probe = Module.busTycoonSafeProbe;
    if (!probe) {
      probe = Module.busTycoonSafeProbe = document.createElement("div");
      probe.style.cssText = "position:fixed;left:0;top:0;width:0;height:0;visibility:hidden;pointer-events:none;" +
        "padding:env(safe-area-inset-top,0px) env(safe-area-inset-right,0px) env(safe-area-inset-bottom,0px) env(safe-area-inset-left,0px)";
      document.body.appendChild(probe);
    }
    var style = getComputedStyle(probe);
    var value = parseFloat([style.paddingLeft, style.paddingRight, style.paddingTop, style.paddingBottom][side]) || 0;
    var width = canvas.getBoundingClientRect().width;
    return width > 0 ? value * canvas.width / width : 0;
  },

  BusTycoon_WheelKind: function () {
    var state = Module.busTycoonWheel;
    return state ? state.kind : 0;
  },

  BusTycoon_TakeWheelDeltaX: function () {
    var state = Module.busTycoonWheel;
    if (!state) return 0;
    var value = state.dx;
    state.dx = 0;
    return value;
  },

  BusTycoon_TakeWheelDeltaY: function () {
    var state = Module.busTycoonWheel;
    if (!state) return 0;
    var value = state.dy;
    state.dy = 0;
    return value;
  }
});
