// Tarayıcı tekerlek olaylarını sınıflandırır (WheelInput.cs okur):
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
