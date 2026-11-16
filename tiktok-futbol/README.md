# ⚽ TikTok Canlı Futbol — Hediyelerle 11'e 11

Canlı yayındaki izleyiciler hediye göndererek iki takımdan birine katılır. **Daha çok veya daha değerli hediye** atan, takımında **daha önemli mevkiye** yükselir. Takımın en büyük destekçisi **KAPTAN + SANTRFOR** olur. Kadrolar dolunca maç oynanır: maç sırasında atılan hediyeler takımın **atak barını** doldurur, bar dolunca takım atağa kalkar ve gol arar.

Sesler, Türkçe sesli anons, konfeti ve otomatik maç döngüsü dahildir. Program kendi kendine çalışır, sen yalnızca yayını açarsın.

![akış](https://img.shields.io/badge/hediye-%E2%86%92%20takım%20%E2%86%92%20mevki%20%E2%86%92%20maç-ffc531)

---

## Kurallar (izleyicilere)

| | |
|---|---|
| 🎁 | Herhangi bir hediye at, takıma gir |
| 💬 | Yorumda **1** yaz → Kırmızı, **2** yaz → Mavi (yazmazsan az kişili takıma girersin) |
| 👑 | Takımında en çok jeton atan **KAPTAN** olur |
| ⚡ | Mevki önemi: Santrfor › 10 Numara › Kaleci › Kanatlar › Orta saha › Stoper › Bekler |
| 🪑 | İlk 11'e giremeyen yedekte bekler; biri seni geçerse yerini kaptırırsın |
| 🔥 | Maçta hediye at → atak barı dolar → takımın atağa kalkar. Büyük hediye = büyük gol şansı |

Yönetim panelinde yayına sabitleyebileceğin hazır kural metni var.

## Öne çıkanlar

- **Kaptanlık savaşı:** Kaptan geçildiği an *"👑 TAHT DEĞİŞTİ!"* duyurusu, fanfar ve sesli anons.
- **Canlı mevki değişimi:** Oyuncular sahada gerçekten yer değiştirir (animasyonlu). Boş mevkilerde *"SENİN YERİN!"* yazar.
- **Atak barı:** Maç sırasında her hediye barı doldurur; bar dolunca atak, gol, konfeti ve kalabalık sesi gelir.
- **Hype göstergesi:** 10 saniyede gelen hediyeler arttıkça 🔥 HYPE → x2 → ÇILGINLIK x3 olur; tribün sesi yükselir, saha parlar.
- **Büyük hediye efekti:** 99 jeton ve üstü hediyede ekran patlaması ve isimle teşekkür.
- **Sesli anons (Türkçe):** "Ahmet kaptan oldu!", "Gol! Gol! Gol! Elif!", "Maça on saniye!"
- **Otomatik pilot:** Kadro kurma (3 dk geri sayım) → maç (3 dk) → maç sonu → yeni maç. Sürekli döner.
- **Sesler dosyasız:** Düdük, gol kornası, tribün uğultusu, jeton sesi, fanfar tarayıcıda üretilir.
- **Kayıt:** Kadro ve jetonlar `data/state.json` dosyasına kaydedilir. Program kapanıp açılsa da kaybolmaz.
- **Dürüstlük:** Gerçek yayına bağlıyken test (sahte) hediyeleri otomatik kapanır.

---

## Kurulum (bir kez)

1. **Node.js** kur (20 veya üstü): <https://nodejs.org> → "LTS" sürümünü indir, kur.
2. Bu `tiktok-futbol` klasörünü bilgisayarına indir.
3. `config.json` dosyasını Not Defteri ile aç, `"tiktokUser"` alanına **kendi TikTok kullanıcı adını** yaz (@ olmadan):
   ```json
   "tiktokUser": "senin_kullanici_adin",
   ```

## Çalıştırma

- **Windows:** `baslat.bat` dosyasına çift tıkla. İlk seferde gerekli paketleri kendisi kurar.
- **Mac / Linux:** klasörde terminal aç → `npm install` (ilk sefer) → `npm start`

Açılınca iki adres verir:

| Adres | Ne işe yarar |
|---|---|
| `http://localhost:3000/` | **Yayın ekranı** (1080×1920 dikey). Bunu yayına eklersin. |
| `http://localhost:3000/admin` | **Yönetim paneli.** Bağlantı, maç, otomatik pilot, test, oyuncu çıkarma. Yalnızca sen görürsün. |

### Yayın açmadan dene

`npm run demo` (ya da yönetim panelinde **Demo: otomatik hediye AÇ**). Sahte izleyiciler hediye atar, her şeyi görür ve duyarsın.

---

## TikTok yayınına ekleme

> Program, yayının **sen canlıya geçtikten sonra** gelen hediyelerini okur. TikTok'ta canlı yayın izni (LIVE erişimi) hesabında açık olmalı.

### Yol A: TikTok LIVE Studio (bilgisayardan yayın, en kolayı)

1. TikTok LIVE Studio'yu indir, hesabınla giriş yap, yayın yönünü **Dikey** seç.
2. Chrome'da `http://localhost:3000/` adresini aç ve sayfaya **bir kez tıkla** (tarayıcı sese ancak tıklamadan sonra izin verir).
3. LIVE Studio → **Kaynak ekle → Pencere yakalama (Window Capture)** → Chrome penceresini seç, sahneye sığdır.
4. Sesin yayına gitmesi için LIVE Studio'da **Masaüstü sesi / Sistem sesi** kaynağının açık olduğundan emin ol.
5. **Canlı yayını başlat.** Yönetim panelinde durum **"Yayına bağlı"** olur. Program, yayın başladıktan sonra en geç 30 saniye içinde kendiliğinden bağlanır.

### Yol B: OBS (yayın anahtarın varsa)

1. OBS → Kaynak ekle → **Tarayıcı (Browser)** → URL: `http://localhost:3000/`, Genişlik **1080**, Yükseklik **1920**.
2. **"Sesi OBS üzerinden kontrol et"** kutusunu işaretle (sesler doğrudan yayına gider, tıklama gerekmez).
3. Not: OBS'in tarayıcısında Türkçe sesli anons çalışmayabilir. Anons istiyorsan Yol A'daki gibi Chrome penceresini yakala.

### Yol C: Sadece iPhone ile (bilgisayarsız)

Oyun sunucusu iPhone'da çalışamaz. Bu yüzden onu ücretsiz bir bulut sunucusuna koyarsın, iPhone'da da yalnızca oyun sayfasını açıp ekranını TikTok'ta yayınlarsın.

**1. Sunucuyu internete koy (bir kez, iPhone'dan yapılır):**
1. Safari'de <https://render.com> → **GitHub ile giriş yap**.
2. **New → Blueprint** → `Bus-Tycoon-Game` deposunu seç. Dal olarak bu oyunun bulunduğu dalı seç.
3. İki değer sorar:
   - `TIKTOK_USER`: TikTok kullanıcı adın (@ olmadan)
   - `ADMIN_KEY`: yönetim paneli için kendi belirleyeceğin bir şifre
4. **Deploy** de. Birkaç dakika sonra sana `https://tiktok-futbol-xxxx.onrender.com` gibi bir adres verir.

**2. Oyun ekranını hazırla:**
1. Safari'de bu adresi aç → Paylaş → **Ana Ekrana Ekle**. Ana ekrandaki simgeden açınca adres çubuğu olmadan tam ekran açılır.
2. Ekrana **bir kez dokun** (ses ve sesli anons açılır).
3. Yönetim paneli: `https://...onrender.com/admin?key=ŞİFREN`. Yayından **önce** bak, yayın sırasında açma (ekranın yayında görünür). Otomatik pilot açık olduğu için yayında panele gerek yok.

**3. Canlıya geç:**
1. TikTok → **+** → **CANLI** → **Mobil Oyun** → Canlı yayına başla → ekran kaydı iznini ver.
2. Ana ekrandan **Canlı Futbol** simgesine geç. Artık izleyiciler oyunu görür, hediyeler sahaya yansır.
3. Bitirmek için TikTok'a dönüp yayını kapat.

**Bilmen gerekenler:**
- **Mobil Oyun seçeneği:** TikTok bunu her hesapta açmıyor. LIVE menünde göremiyorsan hesabında yok demektir; o zaman Yol A ile bilgisayardan yayın yapman gerekir.
- **Ücretsiz planda uyku:** Sunucu, kimse bağlı değilken bir süre sonra uyur ve ilk açılışta uyanması yaklaşık 1 dakika sürer. Yayından birkaç dakika önce oyun sayfasını aç.
- **Kadro kaydı:** Ücretsiz planda sunucu yeniden başlarsa kadro sıfırlanır.
- **TikTok'a bağlanamazsa:** Bulut sunucularından bağlantı bazen sınırlanır. <https://www.eulerstream.com> adresinden ücretsiz anahtar al, Render'da `SIGN_API_KEY` ortam değişkeni olarak ekle.

### Ekran ayarları (adresin sonuna eklenir)

| Ayar | Örnek | Ne yapar |
|---|---|---|
| `?ses=0` | `localhost:3000/?ses=0` | Tüm sesleri kapatır |
| `?tts=0` | `localhost:3000/?tts=0` | Yalnızca sesli anonsu kapatır |
| `?vol=0.5` | | Ses düzeyi (0–1.5) |
| `?bg=green` | | Yeşil arka plan (chroma key için) |
| `?bg=transparent` | | Şeffaf arka plan (OBS) |

Birden fazlası: `localhost:3000/?vol=0.6&bg=green`

---

## Ayarlar (`config.json`)

| Alan | Varsayılan | Açıklama |
|---|---|---|
| `tiktokUser` | | Senin TikTok kullanıcı adın |
| `teams[].name / color` | Kırmızı Şimşekler / Mavi Kartallar | Takım adı ve rengi (yönetim panelinden de değişir) |
| `teams[].keywords` | `1`, `kırmızı` / `2`, `mavi` | Takım seçtiren yorumlar |
| `positions[]` | 4-2-3-1 | Mevkiler **önem sırasına göre**. Sırayı değiştirerek hangi mevkinin daha değerli olduğunu belirlersin |
| `positions[].minCoins` | 0 | O mevki için gereken en az jeton (ör. Santrfor için 100 yaparsan 100 jeton atmayan santrfor olamaz) |
| `bigGift` | 99 | Bu ve üstü hediyede büyük efekt + teşekkür |
| `auto.enabled` | true | Otomatik pilot |
| `auto.lobbySeconds` | 180 | Maç öncesi kadro kurma süresi (sn) |
| `auto.breakSeconds` | 30 | Maç sonu ekranı süresi (sn) |
| `auto.resetSquadEachRound` | false | `true`: her maçta kadro sıfırlanır, herkes kaptanlık için yeniden yarışır |
| `match.secondsPerMinute` | 2 | 1 maç dakikası kaç saniye (2 → maç 3 dk sürer) |
| `match.attackCost` | 50 | Atak barını sıfırdan doldurmak için gereken jeton |
| `match.attackCooldown` | 8 | İki büyük atak arası en az kaç maç dakikası |
| `allowTeamSwitch` | true | Maç dışında yorumla takım değiştirmeye izin ver |
| `signApiKey` | | İsteğe bağlı Euler Stream anahtarı (bağlantı sınırına takılırsan) |

Ayarı değiştirdikten sonra programı kapatıp aç.

---

## Sorun giderme

| Sorun | Çözüm |
|---|---|
| "şu an canlı yayında değil" | Normal. Yayını başlatınca 30 sn içinde bağlanır. |
| Ses yok | Yayın sayfasına bir kez tıkla. LIVE Studio'da masaüstü sesinin açık olduğunu kontrol et. |
| Sesli anons yok | Chrome kullan (OBS tarayıcısında çalışmayabilir). Windows'ta Türkçe ses paketi yüklü olmalı: Ayarlar → Saat ve dil → Konuşma. |
| Hediyeler gelmiyor | `config.json`'daki kullanıcı adını kontrol et. Yönetim panelinde durum "Yayına bağlı" olmalı. |
| Bağlantı sık kopuyor / limit hatası | <https://www.eulerstream.com> adresinden ücretsiz anahtar al, `signApiKey` alanına yaz. |
| Bir kullanıcı uygunsuz isimle girdi | Yönetim paneli → Oyuncular → **Çıkar + engelle**. |

> **Not:** TikTok canlı yayın verileri, resmî olmayan `tiktok-live-connector` kütüphanesiyle okunur. TikTok altyapısını değiştirirse geçici olarak bağlanamayabilir; o durumda `npm update` ile kütüphaneyi güncelle.

## Geliştirici

```bash
npm test        # oyun mantığı testleri
npm run demo    # sahte hediyelerle çalıştır
node server.js --user=kullanici --port=3001 --config=baska.json
```

Dosyalar: `game.js` (kurallar, mevkiler, maç motoru), `tiktok.js` (TikTok bağlantısı), `server.js` (sunucu), `public/overlay.html` (yayın ekranı), `public/fx.js` (ses ve efektler), `public/admin.html` (yönetim).
