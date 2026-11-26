# Araç modelleri

Oyundaki 11 araç (dolmuş, midibüs, şehirlerarası, konfor, VIP, çift katlı, şehir otobüsü, elektrikli, körüklü) özgün
tasarımdır; marka, logo ya da yazı yoktur. Hepsi tek bir parametrik üreticiden çıkar: `bus_gen.py`.

| Dosya | Ne yapar |
| --- | --- |
| `bus_gen.py` | Araç ölçüleri (`vehicle_specs`) ve geometri (gövde kesitleri, camlar, kapılar, tekerlekler, ışıklar). İçe aktarma yok; aynı kod SketchUp'ta da çalışır. `Q["low"]` az çokgenli harita sürümünü verir. |
| `otobus_imparatoru_araclar.skp` | 11 aracın SketchUp modeli (aynı kodla SketchUp'ta kuruldu). |
| `export_json.py` | Modelleri `vehicles.json` olarak döker (`vehicles.html` için). |
| `vehicles.html`, `vshots.js` | three.js ile önden-yandan görünüm, saydam PNG (`shots/<KİMLİK>.png`). |
| `atlas.py` | Görüntülerden galeri atlası: `Resources/BusTycoon/UI/buses.png` (2 sütun × 6 satır, 512×256 hücre). |
| `export_unity.py` | Haritadaki 3B araçlar: `Resources/BusTycoon/Models/vehicles.bytes` (oyunda `VehicleMeshes`). |

Yeniden üretmek:

```sh
python3 Tools/vehicles/export_unity.py                       # harita örgüleri
mkdir -p /tmp/arac && cd /tmp/arac
npm i three@0.170.0 playwright                              # bir kez
python3 ~/Bus-Tycoon-Game/Tools/vehicles/export_json.py vehicles/vehicles.json
mkdir -p threejs shots && cp ~/Bus-Tycoon-Game/Tools/vehicles/vehicles.html ~/Bus-Tycoon-Game/Tools/vehicles/vshots.js threejs/
ln -s ../node_modules threejs/node_modules
python3 -m http.server 8806 --bind 127.0.0.1 &               # /tmp/arac kökünden
(cd threejs && node vshots.js ../shots)
python3 ~/Bus-Tycoon-Game/Tools/vehicles/atlas.py ~/Bus-Tycoon-Game/Assets/_Project/Resources/BusTycoon/UI/buses.png
```

Hücre sırası `UIBusArt.ModelIds` ile aynıdır. Gövde renkleri haritadaki segment renkleriyle (`BusModels.SegmentColors`)
aynı tonlardadır.
