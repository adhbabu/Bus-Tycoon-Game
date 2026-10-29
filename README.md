# 🚌 Bus Tycoon — Otobüs İşletmeciliği Strateji Oyunu (Unity)

Otobüsü sürmediğiniz, **tamamen şirketi yönettiğiniz** 3D bir tycoon oyunu. Gerçek harita üzerinde
81 il + 12 komşu ülke şehri arasında hat açar, bilet fiyatını talep esnekliğine göre ayarlar, eski bir
minibüsle başlayıp VIP ve çift katlı otobüslere kadar filonuzu büyütürsünüz.

## ▶ Hemen oyna (tarayıcıda)

**https://adhbabu.github.io/Bus-Tycoon-Game/oyna/** — bilgisayarda ve telefonun tarayıcısında, kurulum gerekmez.
(Site, "Web sitesi ve web oyunu" iş akışıyla yayınlanır; bkz. [store/BULUT-DERLEME.md](store/BULUT-DERLEME.md).)

## ▶ Unity'de açmak için (3 adım)

1. **Unity Hub**'ı kurun ve bu depoyu bilgisayarınıza indirin (GitHub ▸ **Code ▸ Download ZIP** ya da `git clone`).
2. Unity Hub → **Add ▸ Add project from disk** → depo klasörünü seçin. Hub, projenin sürümünü
   (**Unity 6.3 LTS — 6000.3.27f1**) kurmayı önerir; kabul edin (daha yeni bir Unity 6 sürümü de olur).
   İlk açılışta Unity paketleri indirip scriptleri derler (birkaç dakika).
3. Açılan sahnede **Play ▶** tuşuna basın.

> Kendi projenize eklemek isterseniz yalnızca **`Assets/_Project`** klasörünü projenin `Assets` klasörüne
> kopyalamanız da yeterlidir (Unity 6 veya 2022.3 LTS, Built-in veya URP).

Başka hiçbir şey gerekmez: sahne, prefab, asset bağlama, TextMeshPro kaynakları veya Input System ayarı yok.
Oyun Play'e basınca kendini kodla kurar (`GameBootstrap`): kamera, ışık, gerçek harita dokusu, şehirler,
hatlar, hareket eden otobüsler ve tüm arayüz.

> **Not:** Bu depo Unity editörü olmayan bir ortamda geliştirildi. Kod, Unity API'lerini taklit eden bir test
> katmanıyla derlendi; arayüz, 749 otomatik IMGUI tutarlılık kontrolü ve tam bir oyun akışı (menü → hat açma →
> fiyat → filo → kaydet/yükle → zafer/iflas) ile sınandı. Yine de gerçek Unity'de ilk açılışta konsolda bir
> uyarı/hata görürseniz mesajı paylaşın.

## Nasıl oynanır?

| Adım | Ne yapılır |
|---|---|
| **Şirketi kur** | Ana menü → Yeni Oyun → şirket adı → haritadan merkez şehri seçin (İstanbul: kolay başlangıç). 40.000 $ ve 2. el bir minibüsle başlarsınız. |
| **Hat aç** | Haritada bir şehre, sonra varış şehrine tıklayın. Planlayıcı en kârlı hedefleri de önerir. Teklif kartında mesafe, talep, önerilen bilet ve otobüs sayısı görünür → **HATTI AÇ**. |
| **Fiyatı ayarla** | Hat panelinin **Fiyat** sekmesinde kaydırıcıyı oynatın: kâr eğrisi, yeşil "ideal bölge" ve canlı yolcu/doluluk/kâr tahmini. Çok ucuz → otobüs dolar ama kâr kaçar; çok pahalı → yolcu kaçar. **UYGULA**. |
| **Büyü** | Galeri'den otobüs alın, hatlara atayın. Yolcu taşıdıkça seviye atlarsınız: yeni otobüs modelleri, daha fazla hat ve yeni ülkeler (Bölgeler) açılır. |
| **Kazan** | Görevleri takip edin (sol alt). Kasada **10.000.000 $**'a ulaşınca oyunu kazanırsınız; sonra serbest devam. Kasa 30 gün eksi kalırsa iflas. |

**Kontroller (dokunmatik):** dokun = seç · tek parmakla sürükle = kaydır · iki parmak = yakınlaştır ·
ekrandaki +/- · panelde parmakla kaydır · Android geri tuşu = geri / menü.

**Kontroller (klavye/fare):** Sol tık seç · sürükle / WASD / ok tuşları kaydır · tekerlek / Q-E yakınlaştır · sağ tık iptal ·
Boşluk duraklat · 1/2/3 hız · N yeni hat · R hatlar · F filo · G galeri · K finans · B bölgeler · J görevler ·
L günlük · H yardım · F5 kaydet · Esc geri / menü. Oyun her 7 günde ve çıkışta otomatik kaydeder.

## 📱 Mobil (Android / iOS) ve mağaza yayını

Oyun telefon ve tablette oynanacak şekilde uyarlandı: dokunmatik seçim ve sürükleme, iki parmakla yakınlaştırma,
ekrandaki +/- düğmeleri, telefona özel düzen (alt menü çubuğu, büyük düğmeler), çentik/güvenli alan desteği,
keskin yazılar (ekran yoğunluğuna göre gerçek piksel ölçekleme), Android geri tuşu, yatay ekran ve uygulama
arka plana alınınca otomatik kayıt.

Mağazaya çıkarmak için: **Bus Tycoon ▸ Mağaza** menüsü ayarları uygular ve Android `.aab` / iOS Xcode projesi
derler. İkonlar, Play tanıtım görseli, TR/EN mağaza metinleri ve gizlilik politikası `store/` klasöründe;
hesap açmaktan incelemeye kadar tüm adımlar **[store/YAYIN-REHBERI.md](store/YAYIN-REHBERI.md)** içinde.
Mağaza hesapları (Google Play 25 $ bir kez, Apple 99 $/yıl), imza anahtarı ve yükleme sizin tarafınızdan yapılmalıdır.

**Mac'iniz yoksa:** GitHub Actions iş akışları (`.github/workflows/`) oyunu GitHub'ın sunucularında derler —
Android `.aab`/`.apk` Linux'ta, iOS ise kiralık bir Mac'te imzalanıp doğrudan **TestFlight**'a yüklenir.
Kurulum (bir kerelik sırlar) ve kullanım: **[store/BULUT-DERLEME.md](store/BULUT-DERLEME.md)**.

## İçerik

* **Harita:** Natural Earth 1:10m verisinden 4096 px Mercator dokusu: gölgeli dağlar, göller, nehirler, il ve ülke sınırları (Türkiye, Balkanlar, Kafkasya).
* **Karayolları:** Natural Earth otoyol, devlet yolu ve feribot ağı (+ Osmangazi ve 1915 Çanakkale köprüleri); hatlar ve otobüsler bu yolları izler.
* **93 şehir:** 81 il merkezi (GeoNames koordinatları ve il nüfusları) + Bulgaristan, Yunanistan, Gürcistan, Romanya'dan 12 şehir.
* **5 ülke / bölge:** Türkiye başlangıçta açık; diğerleri seviye + lisans ücretiyle açılır.
* **8 otobüs modeli, 6 segment:** minibüs → midibüs → şehirlerarası → konfor → VIP 2+1 → çift katlı.
* **Ekonomi:** çekim (gravity) talep modeli, yarı-logaritmik fiyat esnekliği, otomatik sefer planı, yakıt/şoför/bakım/otoyol/otogar/ikram/sigorta giderleri, arıza ve yıpranma, itibar döngüsü, pazar araştırması belirsizliği.
* **14 görev** (öğretici zincir + ödüller), zafer ve iflas sonu, kayıt/yükleme.

## Dokümanlar

| # | Doküman | İçerik |
|---|---|---|
| 1 | [Mimari ve Dünya Haritası](docs/01-Mimari-ve-Dunya-Haritasi.md) | Katmanlı mimari, ScriptableObject ↔ save ayrımı, şehir/bölge verisi, projeksiyonlar, veri hattı |
| 2 | [Hat ve Talep Esnekliği](docs/02-Hat-ve-Talep-Esnekligi.md) | Çekim modeli, esneklik formülü, kapasite, maliyetler, **sayısal örnek**, denge rehberi |
| 3 | [Filo ve Progression](docs/03-Filo-ve-Progression.md) | Segmentler, kilitler, XP eğrisi, satın al / 2. el / takas / revizyon |
| 4 | [UI Akış Planı](docs/04-UI-Akis-Plani.md) | Tasarım ilkeleri, ekran haritası, wireframe'ler, 3 tıkla hat açma, fiyat paneli |

**Talep formülü (özet):**

```
Q(P) = Q_ref × e^(−ε_ref × (P/P_ref − 1))      →  kârı maksimize eden fiyat:  P* = c + P_ref / ε_ref
Q_ref = K × √(Nüfus_A × Nüfus_B) × f(mesafe) × turizm × itibar × olgunluk × konfor
```

## Proje yapısı

```
Assets/_Project/
├─ Resources/BusTycoon/   cities.csv (93 şehir), map.jpg (harita dokusu), roads.txt (karayolu ağı)
├─ Scripts/
│  ├─ Runtime/            BusTycoon.Runtime — oyun kuralları (Unity'den bağımsız test edilebilir)
│  │  ├─ Core/            GameSession, SimulationClock, CsvUtility, OpResult
│  │  ├─ Data/            CityData, RegionData, BusModelData, EconomyConfig, ProgressionConfig, DefaultContent ...
│  │  ├─ World/           GeoCoordinate (Haversine), Mercator/Globe projeksiyon, WorldGraph
│  │  ├─ Economy/         DemandModel, RouteEconomics, PriceOptimizer, PricingAdvisor, FinanceSystem
│  │  ├─ Routes/          RouteSystem, RouteState, RouteAdvisor
│  │  ├─ Fleet/           FleetSystem, BusInstance
│  │  ├─ Progression/     ProgressionSystem
│  │  └─ Save/            CompanyState, SaveSystem
│  ├─ Game/               BusTycoon.Game — OYNANABİLİR KATMAN (kurulumsuz)
│  │  ├─ GameBootstrap    Play'de her şeyi kurar
│  │  ├─ WorldRenderer    3D harita, şehirler, hat yayları, hareket eden otobüsler
│  │  ├─ MapCamera        kaydırma / yakınlaştırma / odaklanma
│  │  ├─ GoalTracker      görev zinciri
│  │  └─ UI/              GameUI (IMGUI arayüzü: menü, HUD, planlayıcı, fiyat, filo, galeri, finans ...)
│  ├─ Presentation/       BusTycoon.Presentation — opsiyonel uGUI/TextMeshPro + prefab tabanlı arayüz iskeleti
│  └─ Editor/             CSV şehir içe aktarıcı, "Create Default Content", mağaza/bulut derleme (StoreBuild)
└─ Tests/EditMode/        24 NUnit testi (talep modeli, ekonomi sistemleri, içerik)
Packages/manifest.json     Unity paketleri (Input System, Test Framework, gerekli modüller)
ProjectSettings/           Unity sürümü (6000.3.27f1); diğer ayarları Unity ilk açılışta üretir
.github/workflows/         Bulutta Android, iOS (TestFlight), web sitesi + web oyunu, mağaza ekran görüntüleri
Assets/WebGLTemplates/     Web sürümünün sayfa şablonu (tam ekran, yükleme çubuğu, telefonu yan çevir uyarısı)
site/                      Web sitesinin giriş sayfası (Tools/build_site.py ile üretilir)
Tools/store-screenshots.mjs Web sürümünden mağaza ekran görüntüleri (Playwright)
Tools/build_world_data.py  GeoNames → cities.csv üretir
Tools/build_map.py         Natural Earth 1:10m → map.jpg (harita) ve roads.txt (yol ağı) üretir
Tools/make_store_art.py    Uygulama ikonu, mağaza tanıtım görseli ve ana menüdeki otobüs çizimi
Tools/make_fonts.py        Nunito yazı tipinden arayüzün iki kalınlığını üretir (Resources/BusTycoon/Fonts)
store/                     Mağaza metinleri, gizlilik politikası, görseller, YAYIN-REHBERI.md, BULUT-DERLEME.md
```

**İki arayüz yolu:** Oynanabilir sürüm `BusTycoon.Game` içindeki IMGUI arayüzünü kullanır (sıfır kurulum).
`BusTycoon.Presentation` ise sahne/prefab ile kurulacak daha cilalı bir uGUI arayüzünün scriptleridir; sahneye
kendi `GameSession`'ınızı koyarsanız `GameBootstrap` devre dışı kalır ve o yolu kullanabilirsiniz.
uGUI/TextMeshPro paketleri yoksa bu assembly derlenmez, oyunu etkilemez.

**Dengeyi değiştirmek:** Tüm katsayılar `DefaultContent.cs` (otobüsler, bölgeler) ve `EconomyConfig` /
`ProgressionConfig` varsayılanlarındadır. Inspector'da düzenlemek için **Bus Tycoon ▸ Create Default Content**
asset'leri üretir. **Testler:** Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All.

## Veri kaynakları

* Harita: [Natural Earth](https://www.naturalearthdata.com/) (kamu malı)
* Şehirler: [GeoNames](https://www.geonames.org/) (CC BY 4.0). Komşu ülke şehirlerinin nüfusları yaklaşık bölge/metro değerleridir.
* Yazı tipi: [Nunito](https://github.com/googlefonts/nunito) (SIL Open Font License 1.1; lisans metni `Assets/_Project/Resources/BusTycoon/Fonts/OFL.txt`)
