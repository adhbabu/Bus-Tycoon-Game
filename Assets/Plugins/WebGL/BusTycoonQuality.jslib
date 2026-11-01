// Görüntü kalitesi (Game/GraphicsQuality.cs): 0 düşük, 1 orta, 2 yüksek.
// Seçim tarayıcıda saklanır ("bt_quality"); sayfa şablonu (index.html) açılışta okuyup ilk çözünürlüğü buna göre
// seçer. Düşük'te tuval 1 CSS pikseli çözünürlükte çizilir (daha az piksel = daha akıcı, pil dostu); diğerlerinde
// ekranın yoğunluğu (en çok 2). Adres satırında ?dpr= verilmişse (testler) çözünürlüğe dokunulmaz.
mergeInto(LibraryManager.library, {
  BusTycoon_ApplyQuality: function (level, stored) {
    if (stored !== -2) {
      try {
        if (stored >= 0) window.localStorage.setItem("bt_quality", String(stored));
        else window.localStorage.removeItem("bt_quality");
      } catch (e) { /* gizli sekme vb.: kayıt olmadan da çalışır */ }
    }
    if (/[?&]dpr=/.test(window.location.search)) return;
    var ratio = level === 0 ? 1 : Math.min(window.devicePixelRatio || 1, 2);
    if (Module.devicePixelRatio !== ratio) Module.devicePixelRatio = ratio;
  }
});
