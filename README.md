# 🚌 Bus Tycoon — Otobüs İşletmeciliği Strateji Oyunu (Unity)

Oyuncunun otobüs sürmediği, **tamamen şirket yönettiği** 3D bir tycoon oyununun iskeleti.
Gerçek dünya koordinatlarıyla şehirler, şehirlerarası hat açma, **fiyat esnekliğine dayalı** talep/gelir
modeli, minibüsten VIP'ye segment progression'ı ve oyuncuyu boğmayan bir UI akışı.

## Dokümanlar

| # | Doküman | İçerik |
|---|---|---|
| 1 | [Mimari ve Dünya Haritası](docs/01-Mimari-ve-Dunya-Haritasi.md) | Katmanlı mimari, ScriptableObject ↔ save ayrımı, `CityData`/`RegionData`/`WorldMapDatabase`, projeksiyonlar, CSV ile gerçek dünya verisi |
| 2 | [Hat ve Talep Esnekliği](docs/02-Hat-ve-Talep-Esnekligi.md) | Çekim modeli, esneklik formülü, kapasite, maliyetler, **sayısal örnek**, itibar döngüsü, denge rehberi |
| 3 | [Filo ve Progression](docs/03-Filo-ve-Progression.md) | 6 segment / 8 model, kilitler, XP eğrisi, satın al / 2. el / takas / revizyon formülleri |
| 4 | [UI Akış Planı](docs/04-UI-Akis-Plani.md) | Tasarım ilkeleri, ekran haritası, wireframe'ler, 3 tıkla hat açma, fiyat paneli, onboarding |

## Oyunun çekirdek döngüsü

```
Haritadan iki şehir seç → hattı aç → bilet fiyatını ayarla (ideal bölgeyi bul)
      ↑                                                       ↓
Üst segment otobüse geç ← seviye / itibar ↑ ← yolcu taşı, kâr et
```

**Talep formülü (özet):**

```
Q(P) = Q_ref × e^(−ε_ref × (P/P_ref − 1))      →  kârı maksimize eden fiyat:  P* = c + P_ref / ε_ref
Q_ref = K × √(Nüfus_A × Nüfus_B) × f(mesafe) × turizm × itibar × olgunluk × konfor
```

Fiyat çok yüksekse yolcu kaçar, çok düşükse otobüsler dolar ama kâr kaçar; filo büyüdükçe ideal fiyat düşer.
Ayrıntı ve gerçek sayılarla örnek: [doküman 2](docs/02-Hat-ve-Talep-Esnekligi.md).

## Gereksinimler

* **Unity 6 (6000.x LTS)** veya 2022.3 LTS — "Universal 3D (URP)" şablonu önerilir
* TextMeshPro (Unity 6'da uGUI paketine dahil)
* Opsiyonel: Input System paketi (yoksa eski Input Manager ile çalışır)

## Hızlı başlangıç

1. Unity Hub → **New project → Universal 3D** → konum olarak bu deponun kök klasörünü seçin
   (veya mevcut bir projeye `Assets/_Project` klasörünü kopyalayın). Unity `ProjectSettings/` ve `Packages/` klasörlerini üretir.
2. Menüden **Bus Tycoon ▸ Create Default Content** → ayarlar, 8 otobüs modeli, Mercator projeksiyonu ve
   örnek CSV'den 40 şehir (`Assets/_Project/Data/`) oluşturulur.
3. Sahneye boş bir GameObject ekleyip **`GameSession`** bileşenini verin; `WorldMapDatabase`, `EconomyConfig`,
   `ProgressionConfig`, `BusCatalog` alanlarını `Assets/_Project/Data/` altındaki asset'lerle doldurun.
4. Harita: `WorldMapView` + bir şehir prefab'ı (küre + `SphereCollider` + `CityNodeView` + TMP etiket) +
   bir hat prefab'ı (`LineRenderer` + `RouteLineView`). Kameraya **`PhysicsRaycaster`** ekleyin.
5. UI: [doküman 4 · 4.12](docs/04-UI-Akis-Plani.md#412-unity-sahne-kurulumu)'deki hiyerarşiyi kurun.
6. Testler: **Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All**.

Gerçek dünya verisi için: simplemaps `worldcities.csv` (veya benzeri) → **Bus Tycoon ▸ Import Cities From CSV**
(nüfus ve ülke filtresiyle).

## Klasör yapısı

```
Assets/_Project/
├─ Data/World/cities_sample.csv        örnek şehir verisi (Türkiye + komşular)
├─ Scripts/
│  ├─ Runtime/        (BusTycoon.Runtime — yalnız UnityEngine'e bağımlı, test edilebilir)
│  │  ├─ Core/        GameSession (kompozisyon kökü), SimulationClock, OpResult
│  │  ├─ Data/        CityData, RegionData, WorldMapDatabase, BusModelData, BusCatalog, EconomyConfig, ProgressionConfig
│  │  ├─ World/       GeoCoordinate (Haversine), MapProjection, Mercator/Globe, WorldGraph
│  │  ├─ Economy/     DemandModel, RouteEconomics, PriceOptimizer, PricingAdvisor, FinanceSystem
│  │  ├─ Routes/      RouteState, RouteSystem, RouteAdvisor
│  │  ├─ Fleet/       BusInstance, FleetSystem
│  │  ├─ Progression/ ProgressionSystem
│  │  └─ Save/        CompanyState, SaveSystem
│  ├─ Presentation/   (BusTycoon.Presentation — uGUI + TextMeshPro)
│  │  ├─ Map/         WorldMapView, CityNodeView, RouteLineView
│  │  └─ UI/          UIFlowController, HudView, RoutePlannerController, TicketPricePanel, UILineGraph,
│  │                  RouteListPanel, RouteCardView, DealershipPanel, BusModelCardView, StartCityScreen, UIFormat
│  └─ Editor/         CityCsvImporter, DefaultContentCreator
└─ Tests/EditMode/    DemandModelTests, EconomySystemsTests (18 test)
```

## Durum

| Alan | Durum |
|---|---|
| Ekonomi çekirdeği (talep, esneklik, maliyet, kapasite, optimizasyon) | ✅ Kod + test |
| Hat aç/kapat, teklif kartı, otomatik sefer planı, günlük simülasyon | ✅ |
| Filo: sıfır/2. el alım, satış, takas, atama, yıpranma, arıza, revizyon | ✅ |
| Progression: XP/seviye, itibar, model/bölge/hat slotu kilitleri, pazar araştırması | ✅ |
| Kayıt/yükleme (JSON, atomik, sürüm göçü) | ✅ |
| Harita görünümü (şehir düğümleri, hat yayları, renk durumu) | ✅ Script (prefab/sahne sizde) |
| UI: HUD, başlangıç şehri, hat planlayıcı, fiyat paneli, hat listesi, galeri | ✅ Script (prefab/sahne sizde) |
| Filo/garaj ekranı, finans raporu, onboarding, kamera kontrolü | 📝 Tasarımı dokümanda |
| Rakip firmalar, kredi, sezonluk talep, çok duraklı hat | 💡 Genişletme önerisi (doküman 3.6) |
