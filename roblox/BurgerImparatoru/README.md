# 🍔 Burger İmparatoru 👑

Roblox oyunu. Burgerler açık bir burger kağıdının ortasında gelir; **KATLA**'ya her bastığında kağıdın
bir kanadı burgerin üstüne katlanır ve **"huss huss huss"** sesi gelir. **3 katlamada burger kağıda sarılıp
paketlenir**, paket tezgahının yanındaki kuleye uçar, sen de parayı kaparsın. Parayla dükkandan geliştirme al, unvanını yükselt: Çırak'tan
başla, **BURGER İMPARATORU** ol.

## Hızlı başlangıç (5 dakika)

1. **Roblox Studio'yu kur:** https://create.roblox.com adresine gir, "Start Creating" de, Studio'yu indir ve Roblox hesabınla giriş yap.
2. **Oyunu indir:** Bilgisayarda şu linke tıkla, dosya kendiliğinden iner:
   https://github.com/adhbabu/Bus-Tycoon-Game/raw/claude/sleepy-mccarthy-vqe8bx/roblox/BurgerImparatoru/BurgerImparatoru-indir.zip
3. **Zip'i aç:** **İndirilenler** (Downloads) klasöründe `BurgerImparatoru-indir.zip`'e sağ tıkla ▸ **Tümünü ayıkla** ▸ **Ayıkla**.
   Açılan klasörde `BurgerImparatoru.rbxlx` dosyasına **çift tıkla**; Roblox Studio oyunu açar.
   (Çift tıklayınca açılmazsa: Studio'yu aç ▸ sol üstte **File** ▸ **Open from File...** ▸ bu dosyayı seç.)
4. **Oyna:** Üstteki ▶️ **Play** düğmesine bas (ya da F5). Karakterin kendi tezgahına ışınlanır.
5. **KATLA!** butonuna bas (bilgisayarda **E** tuşu). 3 basışta burger kağıda sarılır, paketlenir.

> İlk açılışta ses yok, sadece sarı HUSS! yazıları var. Sesi eklemek için aşağıdaki adımları izle.

## 🔊 "Huss huss huss" sesini ekle

Ses dosyası hazır: `sesler/huss.mp3` (indirdiğin zip'in içinde de var). Bu, gönderdiğin videodan (Instagram: **turkish.huss**) kesilmiş
"huss huss huss" sesi. Oyun her tıklamada bu sesin tamamını çalar, ekrandaki üç HUSS! yazısı da sesteki
üç "huss"a denk gelir.

Roblox'ta bir sesi oyunda çalmak için önce Roblox'a yüklemen gerekiyor:

1. https://create.roblox.com/dashboard/creations adresine git ▸ **Development Items** ▸ **Audio** ▸ **Import Asset**.
2. `sesler/huss.mp3` dosyasını seç ve yükle (ücretsiz; Roblox kısa bir kontrol yapar).
3. Yüklenen sesin üstüne tıkla ▸ **Copy Asset ID**.
4. Studio'da soldaki **Explorer** penceresinde **ReplicatedStorage ▸ Ortak ▸ Config**'e çift tıkla.
5. Şu satırı bul ve numarayı yapıştır:
   ```lua
   Config.HussSesId = "rbxassetid://BURAYA_NUMARA"
   ```
6. Play'e bas. Artık her katlamada **huss huss huss**! 🎉

> ⚠️ Bu ses başka birinin videosundan. Roblox, sahibi olmadığın sesleri telif hakkı yüzünden reddedebilir
> ya da sonradan silebilir. Öyle olursa aynı "huss huss huss"u kendi sesinle kaydet (telefonun ses kaydedicisi
> yeter) ve onu yükle. Kendi sesin olunca sorun çıkmaz.
>
> Yükle düğmesi çıkmazsa hesabının ses yükleme hakkı yoktur (yaş/doğrulama sınırı); Creator Hub bunu yazar.
> Ses senin hesabına yüklendiği için oyun da aynı hesapta (ya da senin grubunda) olmalı, yoksa ses çalmaz.

## Nasıl oynanır

| Ne | Bilgisayar | Telefon/Tablet | Oyun kolu |
|---|---|---|---|
| Kağıdı katla | **E** ya da KATLA butonu | KATLA butonu | R2 |
| Dükkanı aç | **B** ya da DÜKKAN | DÜKKAN butonu | Y |

- Her burger açık bir burger kağıdının ortasında gelir. **1. katlama:** sol kanat, **2. katlama:** sağ kanat,
  **3. katlama:** ön ve arka kanatlar burgerin üstüne katlanır ve burger kağıda sarılıp **PAKETLENİR** 📦
- KATLA'nın üstündeki 3 nokta kaçıncı katlamada olduğunu gösterir.
- Paketlenen burgerler tezgahının yanında kule olur (en fazla 6 paket, sonra kule boşalır, yenisi başlar).
- İçindeki malzemeye göre her paket farklı para eder. Bazen **✨ ALTIN BURGER ✨** çıkar: altın kağıda sarılır, parası çok!
- **Hızlı katla, KOMBO yap:** Durmadan basarsan ses hızlanır, HUSS! yazıları büyür.
- **Dükkan:**
  - ✋ **Hızlı Eller**: Her tıkta daha çok katlama (en sonunda tek tıkta paket!)
  - 🧂 **Lezzet Ustası**: Her paket daha çok para eder
  - 👨‍🍳 **Çırak Aşçı**: Sen dururken o senin yerine katlar
  - ✨ **Altın Şans**: Altın burger daha sık çıkar
- **Unvanlar** (toplam paketlediğin burgere göre): Çırak ▸ Aşçı (20) ▸ Şef (100) ▸ Usta Şef (400) ▸
  Burger Lordu (1.500) ▸ Burger Sultanı (5.000) ▸ **BURGER İMPARATORU** (20.000)

## Oyunu yayınla (arkadaşların da oynasın)

1. Studio'da **File ▸ Publish to Roblox**, oyuna bir ad ver ("Burger İmparatoru").
2. **Home ▸ Game Settings ▸ Security** ▸ **Enable Studio Access to API Services**'ı aç. (İlerleme kaydı için gerekli. Kapalıyken de oyun çalışır, sadece kayıt olmaz.)
3. **Game Settings ▸ Places ▸ Max Players**'ı **12** yap (12 tezgah var).
4. Creator Hub'da oyunu **Public** yap, linki arkadaşlarına at.

## Kendin değiştir

Bütün ayarlar **ReplicatedStorage ▸ Ortak ▸ Config** içinde, Türkçe açıklamalı:
kaç katlamada paketleneceği, burger tarifi (hangi malzeme ne sıklıkla), kağıt renkleri, dükkan fiyatları,
unvanlar, tezgah sayısı, ses seviyesi. Mesela altın burgeri daha sık çıkarmak için
`Config.AltinTabanSansi = 0.03` satırını `0.10` yap.

## Dosyalar

```
BurgerImparatoru.rbxlx              ← Studio'da açılan hazır oyun
BurgerImparatoru-indir.zip          ← oyun + ses, tek tıkla indirmek için
sesler/huss.mp3                     ← huss huss huss sesi (Roblox'a yüklenecek)
src/shared/Config.luau              ← bütün ayarlar
src/shared/Ekonomi.luau             ← fiyat, para, tarif, unvan hesapları
src/server/BurgerSunucu.server.luau ← katlama, paketleme, dükkan, kayıt (hile korumalı)
src/server/Dunya.luau               ← mutfak, tezgahlar, burger kağıdı, paket kulesi, dev burger heykeli
src/client/BurgerEkran.client.luau  ← butonlar, HUSS! yazıları, ses, dükkan ekranı
default.project.json                ← Rojo proje dosyası
test/sim.luau                       ← Studio açmadan hızlı test
```

### Geliştiriciler için (Rojo)

Kodu VS Code'da yazıp Studio'ya canlı aktarmak istersen [Rojo](https://rojo.space) kullan:

```bash
rojo serve
```

Studio'da Rojo eklentisinden **Connect** de. `.rbxlx` dosyasını yeniden üretmek için:

```bash
rojo build -o BurgerImparatoru.rbxlx
```

İndirme zip'ini yenilemek için (oyun dosyası + ses):

```bash
rm -f BurgerImparatoru-indir.zip && zip -j BurgerImparatoru-indir.zip BurgerImparatoru.rbxlx sesler/huss.mp3
```

Studio açmadan hızlı test ([Lune](https://lune-org.github.io/docs) gerekir): sunucu ve ekran kodunu sahte bir
Roblox ortamında çalıştırır; katlama, paketleme, paket kulesi, hile koruması, dükkan, otomatik aşçı, kayıt
ve çıkışı dener.

```bash
lune run test/sim.luau BurgerImparatoru.rbxlx ok        # kayıt açık
lune run test/sim.luau BurgerImparatoru.rbxlx studio    # Studio'da kayıt kapalı
lune run test/sim.luau BurgerImparatoru.rbxlx ok ses    # ses numarası girilmiş gibi
```
