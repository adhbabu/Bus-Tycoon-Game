# 🍔 Burger İmparatoru 👑

Roblox oyunu. Burgerini kat kat katla, her katlamada **"huss huss huss"** sesi gelsin.
Burgeri ne kadar yükseğe çıkarırsan servis edince o kadar çok para kazanırsın. Parayla dükkandan
geliştirme al, unvanını yükselt: Çırak'tan başla, **BURGER İMPARATORU** ol.

## Hızlı başlangıç (5 dakika)

1. **Roblox Studio'yu kur:** https://create.roblox.com adresine gir, "Start Creating" de, Studio'yu indir ve Roblox hesabınla giriş yap.
2. **Oyun dosyasını indir:** Bu klasördeki `BurgerImparatoru.rbxlx` dosyası (GitHub'da dosyayı aç, sağ üstteki ⬇️ "Download raw file").
3. **Studio'da aç:** Dosyaya çift tıkla, ya da Studio'da **File ▸ Open from File** ile seç.
4. **Oyna:** Üstteki ▶️ **Play** düğmesine bas (ya da F5). Karakterin kendi tezgahına ışınlanır.
5. **KATLA!** butonuna bas (bilgisayarda **E** tuşu). Burger yükselir, ekranda HUSS! yazıları fırlar.

> İlk açılışta ses yok, sadece HUSS! yazıları var. Sesi eklemek için aşağıdaki adımları izle.

## 🔊 "Huss huss huss" sesini ekle

Roblox'ta bir sesi oyunda çalmak için önce Roblox'a yüklemen gerekiyor. Ses dosyaları hazır:

| Dosya | Ses |
|---|---|
| `sesler/huss.mp3` | Türkçe erkek sesi, "Huss! Huss! Huss!" (oyun buna göre ayarlı) |
| `sesler/huss-2.mp3` | Coşkulu, enerjik ses |
| `sesler/huss-3.mp3` | Daha hızlı ve heyecanlı |

1. https://create.roblox.com/dashboard/creations adresine git ▸ **Development Items** ▸ **Audio** ▸ **Import Asset**.
2. `sesler/huss.mp3` dosyasını seç ve yükle (ücretsiz; Roblox kısa bir kontrol yapar).
3. Yüklenen sesin üstüne tıkla ▸ **Copy Asset ID**.
4. Studio'da soldaki **Explorer** penceresinde **ReplicatedStorage ▸ Ortak ▸ Config**'e çift tıkla.
5. Şu satırı bul ve numarayı yapıştır:
   ```lua
   Config.HussSesId = "rbxassetid://BURAYA_NUMARA"
   ```
6. Play'e bas. Artık her katlamada **huss huss huss**! 🎉

> Yükle düğmesi çıkmazsa hesabının ses yükleme hakkı yoktur (yaş/doğrulama sınırı); Creator Hub bunu yazar.
> Ses senin hesabına yüklendiği için oyun da aynı hesapta (ya da senin grubunda) olmalı, yoksa ses çalmaz.
> Başka bir "huss" sesi kullanmak istersen onu yükleyip numarasını yazman yeterli. İnternetteki
> şarkıları veya başkasının sesini yükleme: Roblox telif hakkı yüzünden siler.

## Nasıl oynanır

| Ne | Bilgisayar | Telefon/Tablet | Oyun kolu |
|---|---|---|---|
| Burger katla | **E** ya da KATLA butonu | KATLA butonu | R2 |
| Servis et (parayı al) | **Q** ya da SERVİS ET | SERVİS ET butonu | L2 |
| Dükkanı aç | **B** ya da DÜKKAN | DÜKKAN butonu | Y |

- Her tıklama burgere bir kat ekler: köfte, peynir, marul, domates, soğan, turşu… Arada bir **✨ ALTIN KÖFTE ✨** çıkar, tam 30 para değerinde!
- **Hızlı katla, KOMBO yap:** Durmadan katlarsan ses inceliyor ve yazılar büyüyor.
- **Servis et:** Burger ne kadar yüksekse **boy bonusu** o kadar büyük. 25 kat = paranın 2 katı, 50 kat = 3 katı…
- **Tabak dolunca** KATLA çalışmaz, önce servis etmen gerekir.
- **Dükkan:**
  - ✋ **Hızlı Eller**: Her tıkta daha çok kat
  - 🧂 **Lezzet Ustası**: Her kat daha çok para eder
  - 👨‍🍳 **Çırak Aşçı**: Sen dururken o senin yerine katlar
  - 🍽️ **Dev Tabak**: Burger daha yükseğe çıkabilir
- **Unvanlar** (toplam katladığın kata göre): Çırak ▸ Aşçı ▸ Şef ▸ Usta Şef ▸ Burger Lordu ▸ Burger Sultanı ▸ **BURGER İMPARATORU**

## Oyunu yayınla (arkadaşların da oynasın)

1. Studio'da **File ▸ Publish to Roblox**, oyuna bir ad ver ("Burger İmparatoru").
2. **Home ▸ Game Settings ▸ Security** ▸ **Enable Studio Access to API Services**'ı aç. (İlerleme kaydı için gerekli. Kapalıyken de oyun çalışır, sadece kayıt olmaz.)
3. **Game Settings ▸ Places ▸ Max Players**'ı **12** yap (12 tezgah var).
4. Creator Hub'da oyunu **Public** yap, linki arkadaşlarına at.

## Kendin değiştir

Bütün ayarlar **ReplicatedStorage ▸ Ortak ▸ Config** içinde, Türkçe açıklamalı:
malzemeler (renk, değer, ne sıklıkla çıktığı), dükkan fiyatları, unvanlar, tezgah sayısı, ses seviyesi.
Mesela altın köfteyi daha sık çıkarmak için `Agirlik = 2` yazan yeri `Agirlik = 10` yap.

## Dosyalar

```
BurgerImparatoru.rbxlx            ← Studio'da açılan hazır oyun
sesler/                           ← huss huss huss sesleri (Roblox'a yüklenecek)
src/shared/Config.luau            ← bütün ayarlar
src/shared/Ekonomi.luau           ← fiyat, para, unvan hesapları
src/server/BurgerSunucu.server.luau ← katlama, servis, dükkan, kayıt (hile korumalı)
src/server/Dunya.luau             ← mutfak, tezgahlar, dev burger heykeli, burger katları
src/client/BurgerEkran.client.luau  ← butonlar, HUSS! yazıları, ses, dükkan ekranı
default.project.json              ← Rojo proje dosyası
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

Studio açmadan hızlı test ([Lune](https://lune-org.github.io/docs) gerekir): sunucu ve ekran kodunu sahte bir
Roblox ortamında çalıştırır; katlama, hile koruması, servis, dükkan, otomatik aşçı, kayıt ve çıkışı dener.

```bash
lune run test/sim.luau BurgerImparatoru.rbxlx ok        # kayıt açık
lune run test/sim.luau BurgerImparatoru.rbxlx studio    # Studio'da kayıt kapalı
lune run test/sim.luau BurgerImparatoru.rbxlx ok ses    # ses numarası girilmiş gibi
```
