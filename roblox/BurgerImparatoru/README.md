# 🍔 Burger İmparatoru 👑

Roblox burger dükkanı oyunu. Herkesin **kendi dükkanı** var. İki köfteli, üç ekmekli
**İmparator Burger**'i karton kutusunda **KATLA**: her katlamada **"huss huss huss"** sesi gelir, 3 katlamada
kutu kapanır ve paket banttan satış penceresine gider; **müşteriler** sıraya girip paketini alır, kimi masada
yer, kimi alıp gider. Parayla dükkanını büyüt: **çalışanlar** (aşçı, kasiyer, teknisyen, temizlikçi),
paketleme makineleri, bantlar, duvarlar, neon tabela, masalar, çatıda dev burger. Bir süre sonra işi aşçın ve
makinelerin yapar. Sonra **Yeni Şube** aç, baştan başla, kalıcı çarpanla daha hızlı büyü.
Meydanda **Hüsnü Usta**'dan görev al, **Huss Yarışması**'nda rekor kır, **Hediyeci Hasan**'dan bedava hediye kap,
**Altın Saat**'te 2 kat kazan. Kasabada gezen, konuşan ve huss dansı yapan insanlar var.

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

**Neden ses çıkmıyor?** Roblox oyunun içine ses dosyası koymaya izin vermez; ses önce Roblox'a yüklenir,
Roblox ona bir numara verir, oyun da o numarayla sesi çalar. Oyundaki `Config.HussSesId` şu an boş,
bu yüzden şimdilik sadece HUSS! yazıları çıkıyor. Aşağıdaki adımları bir kere yapınca ses gelir.
Numarayı bana da yazarsan koda kalıcı olarak koyarım, her yeni sürümde ses hazır gelir.

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
| NPC ile konuş | **F** | NPC'nin yanındaki düğmeye dokun | X |
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
| 👷 Çalışanlar | 👨‍🍳 Aşçı (tezgahında senin yerine katlar, 5 seviye), 🧑‍💼 Kasiyer (ün +%20), 🧑‍🔧 Teknisyen (makineler hızlanır), 🧹 Temizlikçi (ün +%15). Hepsi dükkanında görünür |
| ⚙️ Makineler | 🤖 Paketleme Makinesi (6 taneye kadar, sen dururken paketler), ⚡ Makine Hızı |
| 🏪 Dükkan | 🧱 Duvarlar ve Camlar ▸ 💡 Çatı ve Neon Tabela ▸ 🪑 Oturma Alanı ▸ 🌸 Çiçek ve Işıklar ▸ 🍔 Çatıda Dev Burger. Her biri dükkanının **ününü** artırır, bütün paketler daha çok para eder |
| 🏙️ Yeni Şube | Paran ve geliştirmelerin sıfırlanır, dükkan baştan kurulur; ama paket sayın, unvanın, logon kalır ve her şube bütün paralarını **kalıcı olarak +%50** artırır |

DÜKKAN butonundaki kırmızı nokta "alabileceğin bir şey var" demek.

**Unvanlar** (toplam paket): Çırak ▸ Aşçı (50) ▸ Şef (300) ▸ Usta Şef (1.500) ▸ Burger Lordu (7.500) ▸
Burger Sultanı (30.000) ▸ **BURGER İMPARATORU** (150.000)

**Tahmini süreler** (saniyede 4 tıklayan biri için): ilk makine ~2 dk, 6 makine ~45 dk,
çatıda dev burger ~1,5 saat, ilk yeni şube ~2 saat.

**Meydandaki NPC'ler** (yanlarına gidip **F**'ye bas):

| NPC | Ne yapar |
|---|---|
| 🧔 Hüsnü Usta | Görev verir: "KATLA'ya 30 kez bas", "Elle 10 paket yap", "Altın burger bul"... Bitirince ödül. Görev sol tarafta da görünür, oradan da ödülünü alabilirsin |
| 😎 Hakem Hülya | **Huss Yarışması**: 3-2-1, sonra 15 saniye boyunca KATLA'ya basabildiğin kadar bas. Her tık para! Rekorun kaydedilir, kasaba rekorunu kırarsan herkes duyar |
| 🥳 Hediyeci Hasan | 10 dakikada bir **bedava hediye**: para ya da 60 saniye 2 kat para |

**Altın Saat ⏰:** Arada bir bütün kasabada 45 saniye boyunca paketler 2 kat para eder.

**Kasaba halkı:** Meydanda ve kaldırımlarda gezen, durup konuşan ("Altın burger gören var mı? ✨") insanlar ve
heykelin önünde huss dansı yapan ekip.

**Müşteriler:** Kaldırımdan gelir, satış penceresinde sıraya girer, paketini alıp kucağında taşır. Masaların
varsa bazıları oturup yer. Dükkanın ünü (duvar, çatı, kasiyer...) ve makinelerin arttıkça daha sık gelirler.

## Harita: Burger Kasabası

Ortada kırmızı-beyaz damalı meydan, dev burger heykeli, dev patates ve kola, fıskiyeler, dört girişte
"BURGER KASABASI" kemerleri ve bayrak süsleri; etrafında yaya geçitli yol ve lambalar; yolun dışında **8 dükkan
arsası** (her oyuncuya bir tane); köşelerde parklar (dev ketçap ve hardal şişeleri, yemek kamyonları, göl,
gökyüzünde burger balon); en dışta ağaçlar, şehir binaları ve tepeler. Harita oyun dosyasının içinde: Studio'da
açınca görünür. Oyun başlayınca tepeler ve göl gerçek Roblox arazisine dönüşür (yumuşak tepeler, dalgalı su).

**Grafik:** Roblox'un en güzel ışıklandırması (Future), bulutlar, hafif sis, güneş ışınları, parlama.
Gün ve gece döngüsü var (10 dakikada bir gün): gece neon tabelalar, lambalar ve ışık zincirleri parlar.

## 🚀 Roblox'a yayınlama (ilk kez)

1. `BurgerImparatoru.rbxlx`'i Studio'da aç.
2. Sol üstte **File ▸ Publish to Roblox** (ya da **Alt + P**).
3. **Create a new experience** (yeni oyun) seç. **Name:** `Burger İmparatoru`, istersen bir açıklama yaz,
   **Creator:** kendi hesabın. **Create**'e bas. Artık oyun Roblox'ta ama sadece sen görüyorsun.
4. **Home ▸ Game Settings ▸ Security** ▸ **Enable Studio Access to API Services**'ı aç ▸ **Save**.
   (Bu, ilerlemenin kaydedilmesi için. Kapalıyken de oyun çalışır, sadece kayıt olmaz.)
5. https://create.roblox.com/dashboard/creations adresinde oyununa tıkla:
   - **Places ▸ Burger İmparatoru ▸ Max Players** (en fazla oyuncu) = **8** (8 arsa var).
   - Sol menüde **Audience ▸ Maturity & Compliance** anketini doldur (Roblox herkese açmadan önce ister).
   - Oyun sayfasında **Privacy / Access** kısmını **Public** yap.
6. Oyunun linkini (roblox.com/games/...) arkadaşlarına at. 🎉

## 🖼️ Roblox sayfası: logo, açıklama, tanıtım resimleri

Hepsi `magaza/` klasöründe (indirdiğin zip'te de var):

1. https://create.roblox.com/dashboard/creations ▸ oyununa tıkla ▸ **Configure** (ya da **Basic Info**).
2. **Name:** `🍔 Burger İmparatoru 👑`
3. **Description:** `magaza/aciklama.md` içindeki Türkçe açıklamayı kopyala yapıştır.
4. **Icon:** `magaza/ikon-512.png` yükle.
5. **Thumbnails:** `magaza/tanitim-1.png`, `tanitim-2.png`, `tanitim-3.png` yükle.
6. **Genre:** `Simulation`. **Save Changes**.

## 👶 Yaş sınırı: herkes oynayabilsin

Roblox'ta yaş sınırını sen elle koymazsın; **olgunluk anketi** (Maturity & Compliance) belirler. Anket
doldurulmamış oyunları Roblox küçük yaştaki oyunculara göstermez, yaş sınırı gibi görünen şey budur.

1. https://create.roblox.com/dashboard/creations ▸ oyununa tıkla ▸ sol menüde **Audience ▸ Maturity & Compliance**.
2. **Start Questionnaire**. Oyunumuzda şiddet, kan, korku, kötü söz, romantik içerik, alkol ve kumar (Robux ile
   rastgele ödül) **yok**; bu sorulara dürüstçe **No** de.
3. Sonuç **Minimal** çıkar = **her yaş** oynayabilir. **Submit**.
4. Sonra oyunu **Public** yap (sol menü ▸ Access / Privacy).

## 🔄 Yayınladıktan sonra güncelleme

Oyunu bir kere yayınladın; sonraki her değişiklikte aynı oyunun **üstüne** yayınlarsın, link ve oyuncuların
kayıtları aynı kalır.

1. Benden yeni bir şey iste. Ben kodu değiştiririm ve indirme linkindeki zip'i yenilerim (link hep aynı).
2. Yeni zip'i indir, ayıkla, `BurgerImparatoru.rbxlx`'i Studio'da aç.
3. **File ▸ Publish to Roblox As...** ▸ listeden kendi **Burger İmparatoru** oyununu seç ▸ **Publish**
   (üstüne yazmayı onayla).
4. Açık sunuculardaki oyuncular eski sürümde kalır. Hemen güncellemek için Creator Hub'da oyununun **⋯** menüsünden
   **Restart Servers for Updates**'e bas (yeni girenler zaten yeni sürümü görür).

⚠️ **Önemli:** Studio'da kendin bir şey değiştirirsen (ör. ses numarası, haritaya eklediğin bir şey), yeni sürümü
yayınlamadan önce bana söyle; ben de koda eklerim. Yoksa yeni sürüm senin değişikliğinin üstüne yazar.
En kolayı: ses numarasını bana yaz, koda kalıcı koyayım.

**İleri seviye (isteğe bağlı):** Roblox'un Open Cloud API anahtarıyla sesi yükleme ve güncellemeleri
yayınlama işini doğrudan ben yapabilirim; sen hiçbir şey indirmeden oyun güncellenir. Anahtarın nasıl
oluşturulacağı hemen aşağıda.

## 🔑 Roblox API anahtarı (Open Cloud) nasıl oluşturulur

Bu anahtar, benim senin yerine Roblox'a ses yüklememi ve oyunu yayınlamamı sağlar.
Roblox menü adlarını arada değiştiriyor; düğme adları biraz farklı olabilir.

1. https://create.roblox.com/dashboard/credentials adresine git
   (Creator Hub ▸ soldaki menü ▸ **Open Cloud** ▸ **API Keys**).
2. **Create API Key**'e bas. **Name:** `burger-imparatoru`.
3. **Access Permissions** ▸ **Select API System** listesinden **Assets**'i seç ▸ **Add API System**.
   Altında **Read** ve **Write** kutularını işaretle (ses yüklemek için).
4. Oyunu da benim yayınlamamı istiyorsan: yine **Select API System** ▸ **universe-places**
   (eski adı *Place Management*) ▸ **Add API System** ▸ oyun olarak **Burger İmparatoru**'nu seç ▸ **Write**.
5. **Security** ▸ **Accepted IP Addresses** kutusuna `0.0.0.0/0` yaz ▸ **Add**
   (benim bilgisayarımın adresi sabit değil; anahtarı istediğin zaman silebilirsin).
6. **Expiration** varsa bir tarih seç (ör. 30 gün sonra), daha güvenli olur.
7. **Save & Generate Key** ▸ **Copy Key to Clipboard**. Anahtar sadece bir kere gösterilir.

> 🔒 Anahtarı **sohbete yapıştırma**, kimseyle paylaşma. Bana sadece "anahtar hazır" yaz; güvenli bir form
> açarım, anahtarı oraya yapıştırırsın, ben anahtarı hiç görmem. İstersen kendin de ekleyebilirsin:
> oturumun başlığındaki bulut ortamı menüsü ▸ **Edit** ▸ **Network secrets** ▸ host `apis.roblox.com`,
> header `x-api-key`.

Bir de bana şunları yaz (gizli değiller):
- Profil linkin (`roblox.com/users/123456789/profile`): ses senin hesabına yüklensin diye.
- Oyunun linki (`roblox.com/games/...`): oyunu yayınlamamı istiyorsan.

## Kendin değiştir

Bütün ayarlar **ReplicatedStorage ▸ Ortak ▸ Config** içinde, Türkçe açıklamalı: logolar, renkler, fiyatlar,
makine hızı, çalışanlar, müşteri sıklığı, burgerin boyu (`BurgerOlcegi`), gün uzunluğu (`GunSuresi`),
şube bonusu, unvanlar, dükkanın içindeki yerleşim. Örnek: altın burgeri daha sık çıkarmak için
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
src/client/Musteriler.luau           ← müşteriler (gelir, sıra, paket, masa, gider) ve çalışanların hareketi
src/client/KasabaHalki.luau          ← meydanda gezen/konuşan/dans eden insanlar, NPC'lerin el sallaması
magaza/                              ← Roblox sayfası: ikon, tanıtım resimleri, açıklama metni
magaza/kaynak/                       ← ikon ve tanıtım resimlerini oyunun kendi haritasından 3D çeken araç (three.js)
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
rm -f BurgerImparatoru-indir.zip && zip -j BurgerImparatoru-indir.zip BurgerImparatoru.rbxlx sesler/huss.mp3 magaza/*.png magaza/aciklama.md
```

Studio açmadan test: sunucu ve ekran kodunu sahte bir Roblox ortamında çalıştırır; dükkan kurma ekranı,
katlama, hile koruması, NPC'ler (görev, yarışma, hediye), Altın Saat, kasaba halkı, bütün geliştirmeler ve
çalışanlar, aşçı ve makineler, müşteriler, arazi, yeni şube, isim filtresi, ikinci oyuncu, kayıt ve tekrar giriş
denenir.

```bash
lune run test/sim.luau BurgerImparatoru.rbxlx ok        # kayıt açık
lune run test/sim.luau BurgerImparatoru.rbxlx studio    # Studio'da kayıt kapalı
lune run test/sim.luau BurgerImparatoru.rbxlx ok ses    # ses numarası girilmiş gibi
```

Kodu VS Code'da yazıp Studio'ya canlı aktarmak için `rojo serve` ve Studio'daki Rojo eklentisi.

İkon ve tanıtım resimlerini yeniden çek (Node.js, Playwright ve Python Pillow gerekir). `foto` modu 4 oyunculu
dolu bir kasaba kurup bütün sahneyi `sahne.json`'a yazar; `cek.js` onu three.js ile çizer
(çekim açıları `cekimler.json`'da), `kucult.py` de Roblox boyutlarına küçültür:

```bash
cd magaza/kaynak
npm install
lune run ../../test/sim.luau ../../BurgerImparatoru.rbxlx foto
node cek.js
python3 kucult.py
```
