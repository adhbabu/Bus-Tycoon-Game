# 🍔 Burger İmparatoru 👑

Roblox burger dükkanı oyunu. Herkesin **kendi dükkanı** var. İki köfteli, üç ekmekli
**İmparator Burger**'i karton kutusunda **KATLA**: her katlamada **"huss huss huss"** sesi gelir, 3 katlamada
kutu kapanır ve paket banttan satış penceresine gider. Parayla dükkanını büyüt: paketleme makineleri,
bantlar, duvarlar, neon tabela, masalar, çatıda dev burger. Bir süre sonra işi makineler yapar.
Sonra **Yeni Şube** aç, baştan başla, kalıcı çarpanla daha hızlı büyü.

## Hızlı başlangıç

1. **Roblox Studio'yu kur:** https://create.roblox.com adresine gir, "Start Creating" de, Studio'yu indir ve Roblox hesabınla giriş yap.
2. **Oyunu indir:** Bilgisayarda şu linke tıkla, dosya kendiliğinden iner:
   https://github.com/adhbabu/Bus-Tycoon-Game/raw/claude/sleepy-mccarthy-vqe8bx/roblox/BurgerImparatoru/BurgerImparatoru-indir.zip
3. **Zip'i aç:** **İndirilenler** (Downloads) klasöründe `BurgerImparatoru-indir.zip`'e sağ tıkla ▸ **Tümünü ayıkla** ▸ **Ayıkla**.
   Açılan klasörde `BurgerImparatoru.rbxlx` dosyasına **çift tıkla**; Roblox Studio oyunu açar.
   (Çift tıklayınca açılmazsa: Studio'yu aç ▸ sol üstte **File** ▸ **Open from File...** ▸ bu dosyayı seç.)
4. Studio açılınca Burger Kasabası haritasını hemen görürsün. Üstteki ▶️ **Play**'e bas.
5. Önce **Dükkanını Kur** ekranı gelir: logonu, dükkan rengini ve dükkan adını seç, **DÜKKANI AÇ!**'a bas.
6. Dükkanına ışınlanırsın. **KATLA!**'ya bas (bilgisayarda **E**).

## 🔊 "Huss huss huss" sesini ekle

Ses dosyası hazır: `sesler/huss.mp3` (indirdiğin zip'in içinde de var). Bu, gönderdiğin videodan
(Instagram: **turkish.huss**) kesilmiş "huss huss huss" sesi. Oyun her tıklamada bu sesin tamamını çalar,
ekrandaki üç sarı HUSS! yazısı da sesteki üç "huss"a denk gelir. Makinelerin de kısık sesle huss'lar.

1. https://create.roblox.com/dashboard/creations adresine git ▸ **Development Items** ▸ **Audio** ▸ **Import Asset**.
2. `sesler/huss.mp3` dosyasını seç ve yükle.
3. Yüklenen sesin üstüne tıkla ▸ **Copy Asset ID**.
4. Studio'da soldaki **Explorer** penceresinde **ReplicatedStorage ▸ Ortak ▸ Config**'e çift tıkla.
5. Şu satırı bul ve numarayı yapıştır:
   ```lua
   Config.HussSesId = "rbxassetid://BURAYA_NUMARA"
   ```

> ⚠️ Bu ses başka birinin videosundan. Roblox, sahibi olmadığın sesleri telif hakkı yüzünden reddedebilir
> ya da sonradan silebilir. Öyle olursa aynı "huss huss huss"u kendi sesinle kaydet ve onu yükle.
> Ses senin hesabına yüklendiği için oyun da aynı hesapta olmalı, yoksa ses çalmaz.

## Nasıl oynanır

| Ne | Bilgisayar | Telefon/Tablet | Oyun kolu |
|---|---|---|---|
| Kutuyu katla | **E** ya da KATLA | KATLA butonu | R2 |
| Dükkan menüsü | **B** ya da DÜKKAN | DÜKKAN butonu | Y |
| Dükkanıma dön | 🏠 butonu | 🏠 butonu | |
| Logo/renk/ad değiştir | 🎨 butonu | 🎨 butonu | |
| Sesi kapat/aç | 🔊 butonu | 🔊 butonu | |

**Katlama:** İmparator Burger açık karton kutuda gelir. **1.** tepsinin kenarları kalkar, **2.** kapağın kenarları
kalkar, **3.** kapak kapanır: **PAKET!** 📦 Paket banttan satış penceresine gider, parayı alırsın.
KATLA'nın üstündeki 3 nokta kaçıncı katlamada olduğunu gösterir. Bazen **✨ ALTIN BURGER ✨** çıkar (8 kat para).

**Dükkan menüsü:**

| Sekme | Ne var |
|---|---|
| 🍳 Mutfak | ✋ Hızlı Eller (tıkta 2 katlama), 🧂 Gizli Sos (paket parası), ✨ Altın Şans |
| ⚙️ Makineler | 🤖 Paketleme Makinesi (6 taneye kadar, sen dururken paketler), ⚡ Makine Hızı |
| 🏪 Dükkan | 🧱 Duvarlar ve Camlar ▸ 💡 Çatı ve Neon Tabela ▸ 🪑 Oturma Alanı ▸ 🌸 Çiçek ve Işıklar ▸ 🍔 Çatıda Dev Burger. Her biri dükkanının **ününü** artırır, bütün paketler daha çok para eder |
| 🏙️ Yeni Şube | Paran ve geliştirmelerin sıfırlanır, dükkan baştan kurulur; ama paket sayın, unvanın, logon kalır ve her şube bütün paralarını **kalıcı olarak +%50** artırır |

DÜKKAN butonundaki kırmızı nokta "alabileceğin bir şey var" demek.

**Unvanlar** (toplam paket): Çırak ▸ Aşçı (50) ▸ Şef (300) ▸ Usta Şef (1.500) ▸ Burger Lordu (7.500) ▸
Burger Sultanı (30.000) ▸ **BURGER İMPARATORU** (150.000)

**Tahmini süreler** (saniyede 4 tıklayan biri için): ilk makine ~2 dk, 6 makine ~45 dk,
çatıda dev burger ~1,5 saat, ilk yeni şube ~2 saat.

## Harita: Burger Kasabası

Ortada dev burger heykelli meydan ve fıskiyeler, etrafında yaya geçitli yol ve lambalar, yolun dışında
**8 dükkan arsası** (her oyuncuya bir tane), köşelerde parklar (birinde göl), en dışta ağaçlar, şehir binaları ve
tepeler. Harita oyun dosyasının içinde: Studio'da açınca görünür, istediğin gibi değiştirebilirsin.

## Oyunu yayınla

1. Studio'da **File ▸ Publish to Roblox**, oyuna bir ad ver ("Burger İmparatoru").
2. **Home ▸ Game Settings ▸ Security** ▸ **Enable Studio Access to API Services**'ı aç (ilerleme kaydı için).
   Kapalıyken oyun yine çalışır, sadece kayıt olmaz ve her Play'de "Dükkanını Kur" ekranı yeniden gelir.
3. **Game Settings ▸ Places ▸ Max Players**'ı **8** yap (8 arsa var).
4. Creator Hub'da oyunu **Public** yap, linki arkadaşlarına at.

## Kendin değiştir

Bütün ayarlar **ReplicatedStorage ▸ Ortak ▸ Config** içinde, Türkçe açıklamalı: logolar, renkler, fiyatlar,
makine hızı, şube bonusu, unvanlar, dükkanın içindeki yerleşim. Örnek: altın burgeri daha sık çıkarmak için
`Config.AltinTabanSansi = 0.03` satırını `0.10` yap.

## Dosyalar

```
BurgerImparatoru.rbxlx               ← Studio'da açılan hazır oyun (harita dahil)
BurgerImparatoru-indir.zip           ← oyun + ses, tek tıkla indirmek için
sesler/huss.mp3                      ← huss huss huss sesi (Roblox'a yüklenecek)
harita/Harita.rbxm                   ← Burger Kasabası haritası
src/shared/Config.luau               ← bütün ayarlar
src/shared/Ekonomi.luau              ← fiyat, para, makine, şube, unvan hesapları
src/shared/Gorsel.luau               ← İmparator Burger, karton kutu, logolar
src/server/BurgerSunucu.server.luau  ← arsalar, katlama, makineler, dükkan, şube, stil, kayıt (hile korumalı)
src/server/Dukkan.luau               ← oyuncunun dükkanını arsasına kurar
src/server/HaritaUretici.luau        ← haritayı kodla kurar
src/client/BurgerEkran.client.luau   ← butonlar, menüler, HUSS! yazıları, ses
src/client/PaketGorsel.luau          ← kutunun katlanması, bantta giden paketler, makine presi
araclar/harita-uret.luau             ← haritayı harita/Harita.rbxm dosyasına yazar
test/sim.luau                        ← Studio açmadan hızlı test
```

### Geliştiriciler için

Gerekenler: [Rojo](https://rojo.space) ve [Lune](https://lune-org.github.io/docs). Komutlar bu klasörde çalışır.

Haritayı değiştirdiysen (`src/server/HaritaUretici.luau`) yeniden üret:

```bash
lune run araclar/harita-uret.luau
```

Oyun dosyasını yeniden üret:

```bash
rojo build -o BurgerImparatoru.rbxlx
```

İndirme zip'ini yenile:

```bash
rm -f BurgerImparatoru-indir.zip && zip -j BurgerImparatoru-indir.zip BurgerImparatoru.rbxlx sesler/huss.mp3
```

Studio açmadan test: sunucu ve ekran kodunu sahte bir Roblox ortamında çalıştırır; dükkan kurma ekranı,
katlama, hile koruması, bütün geliştirmeler, makineler, yeni şube, isim filtresi, ikinci oyuncu, kayıt ve
tekrar giriş denenir.

```bash
lune run test/sim.luau BurgerImparatoru.rbxlx ok        # kayıt açık
lune run test/sim.luau BurgerImparatoru.rbxlx studio    # Studio'da kayıt kapalı
lune run test/sim.luau BurgerImparatoru.rbxlx ok ses    # ses numarası girilmiş gibi
```

Kodu VS Code'da yazıp Studio'ya canlı aktarmak için `rojo serve` ve Studio'daki Rojo eklentisi.
