# 3 · Otobüs Satın Alma ve Segment Progression Sistemi

| Dosya | Sorumluluk |
|---|---|
| `Data/BusModelData.cs` | Model tasarım verisi + `BusSegment` enum'u (kademe = sıra) |
| `Data/BusCatalog.cs` | Tüm modeller; kademe/fiyat sıralı liste, "sonraki kademe" sorgusu |
| `Data/ProgressionConfig.cs` | Başlangıç koşulları, seviye eğrisi, hat slotları, pazar araştırması |
| `Fleet/BusInstance.cs` | Oyuncunun sahip olduğu tek otobüs (save'e yazılır) |
| `Fleet/FleetSystem.cs` | Satın al (sıfır/2. el), sat, takasla yükselt, ata, yıpranma, arıza, revizyon |
| `Progression/ProgressionSystem.cs` | XP → seviye, memnuniyet → itibar, kilitler ve "YENİ" bildirimleri |
| `Editor/DefaultContentCreator.cs` | Aşağıdaki 8 modeli asset olarak üretir |

---

## 3.1 Veri modeli: model (SO) ↔ örnek (instance)

```mermaid
classDiagram
    class BusModelData {
        <<ScriptableObject>>
        +string Id
        +BusSegment Segment
        +float Price
        +int RequiredCompanyLevel
        +float RequiredReputation
        +int Seats
        +float Comfort
        +float FareMultiplier
        +float ElasticityMultiplier
        +float ComfortableRangeKm
        +float FuelLitersPer100Km ...
    }
    class BusInstance {
        <<Serializable · save>>
        +string busId
        +string modelId
        +float condition
        +double odometerKm
        +int purchaseDay
        +string assignedRouteId
        +int outOfServiceDays
        +BusModelData Model  [NonSerialized]
    }
    class BusCatalog {
        <<ScriptableObject>>
        +SortedByTier
        +GetById(id)
        +GetNextTierModel(current)
    }
    BusCatalog "1" o-- "*" BusModelData
    BusInstance --> BusModelData : modelId ile çözülür
```

* Aynı modelden 50 otobüs = 1 asset + 50 hafif `BusInstance`.
* Otobüs–hat ataması **yalnızca** `BusInstance.assignedRouteId` alanında tutulur (tek doğruluk kaynağı;
  rota ile otobüs listesi senkron dışı kalamaz).
* Kayıt yüklendiğinde `FleetSystem.ResolveModels()` her örneğin `Model` referansını katalogdan bağlar.

---

## 3.2 Segmentler ve modeller (varsayılan içerik)

| Kademe | Model | Fiyat | Seviye | İtibar | Koltuk | Konfor | Hız | Rahat menzil | Bilet× | Esneklik× | Yakıt L/100 | Sigorta/gün | İkram/yolcu |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 Minibüs | Kartal M16 | 22.000 | 1 | – | 16 | ★☆☆☆☆ | 80 | 300 km | 0,75 | 1,25 | 13 | 6 | 0,2 |
| 0 Minibüs | Kartal M19 Plus | 34.000 | 2 | – | 19 | ★★☆☆☆ | 82 | 350 km | 0,80 | 1,20 | 12 | 7 | 0,25 |
| 1 Midibüs | Anka 27 | 78.000 | 3 | – | 27 | ★★☆☆☆ | 84 | 600 km | 0,90 | 1,10 | 18 | 12 | 0,35 |
| 1 Midibüs | Anka 31 Line | 98.000 | 4 | – | 31 | ★★★☆☆ | 85 | 700 km | 0,92 | 1,05 | 19 | 14 | 0,4 |
| 2 Standart | Toros 46 | 185.000 | 5 | – | 46 | ★★★☆☆ | 88 | 1.200 km | 1,00 | 1,00 | 28 | 25 | 0,5 |
| 3 Konfor | Toros 42 Comfort | 280.000 | 8 | 40 | 42 | ★★★★☆ | 90 | 1.800 km | 1,20 | 0,85 | 30 | 35 | 1,0 |
| 4 Lüks VIP | Zirve VIP 2+1 | 420.000 | 12 | 55 | 33 | ★★★★★ | 90 | 2.500 km | 1,55 | 0,75 | 31 | 50 | 2,0 |
| 5 Çift katlı | Zirve Çift Katlı | 600.000 | 16 | 50 | 72 | ★★★★☆ | 85 | 2.000 km | 1,30 | 0,80 | 38 | 70 | 1,2 |
| 2 Standart (Avrupa Paketi) | Nordstar E12 Elektrik | 360.000 | 14 | 45 | 44 | ★★★★☆ | 90 | 800 km | 1,10 | 0,95 | 8 | 36 | 0,6 |
| 3 Konfor (Avrupa Paketi) | Alpina Reisestar 50 | 340.000 | 15 | 50 | 50 | ★★★★☆ | 95 | 2.200 km | 1,25 | 0,85 | 29 | 38 | 1,0 |

### Servis araçları (yalnız ihale işleri)

`BusSegment.Service`: okul, fabrika, şirket, otel ve hastane servis sözleşmelerinde çalışır; hatlara atanmaz, "sonraki
kademe" ve takasla yükseltme önerilerine girmez. Galeride ayrı "Servis" bölümünde (galeri üç bölüm: şehir içi,
şehirlerarası, servis). Özgün "Serçe" ailesi (marka yok); modeller `Tools/vehicles/bus_gen.py` → `servis_spec`.

| Model | Fiyat | Seviye | İtibar | Koltuk | Konfor | Hız | Yakıt L/100 | Sigorta/gün | Not |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| Serçe 9 | 15.000 | 1 | – | 9 | 0,35 | 90 | 9,5 | 4 | Kısa, alçak tavan |
| Serçe 14 | 21.000 | 2 | – | 14 | 0,40 | 88 | 10,5 | 5 | Yüksek tavan |
| Serçe 17 Uzun | 26.000 | 3 | – | 17 | 0,45 | 86 | 11,5 | 5,5 | Klimalı, uzun şasi |
| Serçe 19 Yüksek Tavan | 30.000 | 4 | 10 | 19 | 0,50 | 85 | 12 | 6 | Büyük fabrika servisleri |
| Serçe Kompakt VIP 7 | 32.000 | 4 | 25 | 7 | 0,92 | 100 | 8,5 | 7 | Siyah, kısa burunlu yolcu vanı (VIP) |
| Serçe 14 Elektrik | 36.000 | 5 | 20 | 14 | 0,50 | 86 | 3 | 6 | Elektrikli |
| Serçe VIP 12 | 42.000 | 6 | 30 | 12 | 0,85 | 90 | 11,5 | 8 | Lacivert, deri koltuk (VIP) |

### İhaleler (taşımacılık sözleşmeleri)

`Contracts/ContractSystem.cs` (şehir içi başlangıçlı tek oyunculu oyunda açık):

* **Teklifler:** lisanslı şehirlerdeki okul, fabrika, şirket, otel ve hastaneler (uydurma adlar, `ContractNames`) her gün
  %60 olasılıkla yeni teklif gönderir (en çok 3–5). Teklif: araç sayısı, en az koltuk, VIP (konfor ≥ 0,8), süre
  (10–60 gün), günlük ödeme, son cevap günü. Müşterinin büyüklüğü (kademe 0–3) şirket seviyesi ve **müşteri memnuniyeti
  puanıyla** büyür: büyük fabrikalar 6–9 büyük araç, oteller ve holdingler VIP araç ister.
* **Pazarlık:** kabul et / reddet / **daha fazla iste** (en çok 2 kez). Firma ödemeyi %10–18 artırır ya da teklifini
  çeker; şans %62'den başlar, her istekte azalır, memnuniyet puanıyla artar.
* **Ödeme:** hatlar gibi saniye saniye kasaya akar (`İhale geliri`), çalışan araç sayısıyla orantılı. Giderler yakıt,
  şoför, bakım ve sigorta olarak işlenir (günde 4 yön + garaj yolu). Ödeme, işe uyan en ucuz aracın giderinin
  ~1,25 katı + koltuk başı pay (VIP'te 2,2 kat) × kademe.
* **Memnuniyet:** her gün kapsama (çalışan / istenen araç), araç durumu ve konfordan; eksik araç hızla düşürür
  (ilk 2 gün tolerans). %33'ün altına düşerse müşteri feshder (puan −8). İş bitince puan güncellenir; memnuniyet
  %85 üstündeyse 3 günlük ödeme ikramiye ve itibar artışı.
* **Harita:** müşteri binası ve mahalle (durak) şehir haritasından seçilir; servis araçları ikisi arasında gidip gelir
  (altın çizgi), müşteri adı haritada yazar.

Her segmentin **farklı bir oyun rolü** vardır; "üst segment her zaman daha iyi" değildir:

| Segment | Güçlü yanı | Zayıf yanı | Nerede kullanılır |
|---|---|---|---|
| Minibüs | Ucuz, küçük talepte bile dolar | Uzun hatta talep çöker (konfor), düşük bilet | 30–250 km, küçük şehir çiftleri |
| Midibüs | Orta talepte verimli | Ölçek ekonomisi yok | Bölgesel hatlar |
| Standart | **Koltuk-km başı en düşük maliyet**, uzun menzil | Küçük talepte boş gider | Ana arterler |
| Konfor / VIP | Yüksek bilet, düşük esneklik (fiyata dayanıklı), itibar artırır | Pahalı, az koltuk, yüksek ikram | Zengin/turistik, uzun hatlar |
| Çift katlı | Dev kapasite | Çok pahalı; ancak çok yoğun hatta kâr eder | İstanbul–Ankara gibi omurgalar |

### Yükseltmenin ekonomisi (İstanbul ⇄ Ankara, 437 km, itibar 30, önerilen fiyat ve otobüs sayısıyla)

| Model | Önerilen bilet | Otobüs | Hat kârı/gün | **Kâr/otobüs/gün** | Geri ödeme |
|---|---:|---:|---:|---:|---:|
| Kartal M16 | 21,6 $ | 4 | 1.295 $ | 324 $ | 68 gün |
| Anka 27 | 21,6 $ | 7 | 5.301 $ | 757 $ | 103 gün |
| Toros 46 | 22,7 $ | 4 | 7.125 $ | 1.781 $ | 104 gün |
| Zirve VIP 2+1 | 44,7 $ | 4 | 10.839 $ | 2.710 $ | 155 gün |

Okuma: Minibüs bu uzun hatta talebin yarısını konfor yüzünden kaybeder (hat toplamı yalnızca 1.295 $).
Standart otobüs aynı hattan **5,5 kat** fazla kazanır. VIP, otobüs başına en yüksek kârı verir ama
pahalı olduğundan geri ödemesi uzundur ve 55 itibar ister (gerçekte o itibarda talep %28 daha yüksek olur).
Küçük hatlarda (ör. Eskişehir–Afyon, ~37 yolcu/gün) ise standart otobüs yarı boş gider; orada midibüs en iyisidir.
**Doğru aracı doğru hatta koymak** progression'ın stratejik katmanıdır.

---

## 3.3 Kilitler: neyle ne açılır?

```mermaid
flowchart LR
    P[Yolcu taşı] -->|1 yolcu = 1 XP| XP[Şirket XP]
    XP --> L[Seviye ↑]
    L --> M1[Yeni otobüs modeli]
    L --> S[+1 hat slotu]
    L --> R[Bölge lisansı hakkı]
    L --> I[Pazar araştırması ↑<br/>daha isabetli fiyat tahmini]
    Q[Adil fiyat + konforlu araç] --> SAT[Memnuniyet] --> REP[İtibar ↑]
    REP --> D[Tüm hatlarda talep ↑]
    REP --> M2[Konfor / VIP / Çift katlı kilidi]
    D --> P
    M1 --> CAP[Daha fazla kapasite + daha yüksek bilet] --> P
```

**Seviye eğrisi:** `XP(L) = 1.500 × (L − 1)^2,2` · **Hat slotu:** `2 + (L − 1)`

| Seviye | Toplam XP (yolcu) | Hat slotu | Açılanlar |
|---:|---:|---:|---|
| 1 | 0 | 2 | Kartal M16, başlangıç bölgesi |
| 2 | 1.500 | 3 | Kartal M19 Plus |
| 3 | 6.892 | 4 | Anka 27 |
| 4 | 16.817 | 5 | Anka 31 Line · Pazar araştırması Sv.1 (±%12) |
| 5 | 31.668 | 6 | **Toros 46 (standart şehirlerarası)** |
| 6 | 51.740 | 7 | Bulgaristan lisansı |
| 7 | 77.272 | 8 | Gürcistan lisansı |
| 8 | 108.469 | 9 | Toros 42 Comfort (+ itibar 40) · Pazar araştırması Sv.2 (±%6) |
| 9 | 145.509 | 10 | Yunanistan lisansı |
| 12 | 293.194 | 13 | Zirve VIP (+ itibar 55) · Pazar araştırması Sv.3 (kesin) |
| 16 | 580.086 | 17 | Zirve Çift Katlı (+ itibar 50) |
| 30 | 2.473.813 | 31 | Maksimum seviye |

Simülasyonda basit bir bot, İstanbul'dan başlayarak standart otobüse ~100. günde (1 gün = 4 sn → ~7 dk),
VIP'ye ~460. günde ulaşıyor. İyi oynayan bir oyuncu daha hızlı ilerler.

`ProgressionSystem.GetLockReason(model)` kilitli kartlarda her zaman **somut bir hedef** gösterir:
*"Şirket seviyesi 5 gerekli (14.851 yolcu daha taşıyın)."* · *"İtibar 55 gerekli (şu an 41)."*

Yeni açılan modeller `CompanyState.announcedModelIds` ile **bir kez** duyurulur (toast + galeride "YENİ" rozeti).
İtibar düşerse itibar kilitli modeller tekrar satın alınamaz ama sahip olunanlar elde kalır.

---

## 3.4 Filo eylemleri ve formüller (`FleetSystem`)

| Eylem | Formül / kural |
|---|---|
| **Sıfır alım** | `fiyat = liste` |
| **2. el alım** (galeride anahtar) | `fiyat = liste × (0,35 + 0,65 × durum)` → durum 0,6 ⇒ %74 fiyat. Ucuz başlangıç, ama bakım ve arıza yüksek |
| **Satış** | `liste × 0,88^yaş(yıl) × (0,35 + 0,65 × durum) × 0,85 (galerici marjı)` |
| **Takasla yükselt** | Eski otobüs satılır + yenisi alınır + **aynı hatta otomatik atanır**; bakiye net fark üzerinden kontrol edilir. Progression'ın tek tık eylemi |
| **Atama** | `RouteSystem.TryAssignBus(busId, routeId)` (null = garaja çek) |
| **Yıpranma** | Her gün `durum −= km × 0,3 / ömür_km` (minibüs 400 B km, standart 1 M km; `EconomyConfig.wearRate` = 0,3: oyun günü kısa olduğu için otobüs gerçek ömründen yavaş yıpranır — yoğun çalışan minibüs ~1 yılda bir revizyon ister) |
| **Bakım maliyeti** | `bakım/km × (1 + (1 − durum) × 1,5)` → durum 0,4'te bakım %90 pahalı |
| **Arıza** | Günlük olasılık `0,08 × (1 − durum)²`; 1–3 gün servis dışı + `fiyat × %1` tamir |
| **Revizyon** | Durumu 0,9'a çıkarır, bedel `fiyat × 0,3 × (0,9 − durum)` |
| **Bakım asistanı** | `MaintenanceAssistant` (Filo panelinde AÇIK/KAPALI; yeni oyunda açık). Gün başında durumu 0,6'nın altındaki otobüsler (en yıpranmış önce) revizyona, arızalılar **acil tamire** (`fiyat × %1`, otobüs o gün sefere çıkar) girer. **Hepsini onar ve hatlara gönder**: 0,9'un altındaki her otobüs revizyona, garajdakiler son hatlarına (`BusInstance.lastRouteId`; yoksa en çok yolcunun bilet bulamadığı hatta). Kasada dünkü giderin 2 katı yedek bırakılır, parası yetmeyen iş ertesi güne kalır |
| **Sigorta** | Garajdaki otobüs de sigorta öder → atıl filo para kaybettirir |

Başlangıçta oyuncuya **hediye 2. el Kartal M16** (durum 0,6) ve 40.000 $ verilir
(`ProgressionConfig.startingBus / startingCash`). "Eski minibüsle başla" fantezisi buradan gelir.

---

## 3.5 Yeni model eklemek (tasarımcı kontrol listesi)

1. `Create ▸ Bus Tycoon ▸ Fleet ▸ Bus Model` → kalıcı `modelId` verin (yayından sonra değiştirmeyin).
2. Segment, fiyat, `requiredCompanyLevel`, gerekirse `requiredReputation` girin.
3. Ekonomi değerlerini aynı kademedeki modellerle kıyaslayın: koltuk-km başı maliyet
   `(yakıt × 1,3 + bakım × 100 + 2 + 100 × 8 / hız) / koltuk` (kuruş/km) üst kademede **düşmeli** veya
   bilet çarpanı **yükselmeli** — aksi halde model anlamsız kalır.
4. `BusCatalog` listesine ekleyin. Kod değişikliği gerekmez; galeri, kilitler ve bildirimler otomatik çalışır.

---

## 3.6 Genişletme fikirleri (iskelet bunlara hazır)

* **Kredi / leasing:** `FinanceSystem`'e `LedgerCategory.LoanInterest` + `CompanyState.loans` listesi.
* **Rakip firmalar:** `RouteEconomics.Inputs`'a `CompetitorPrice` ekleyip talebi pazar payıyla bölmek
  (`pay = e^(−β·P_ben) / Σ e^(−β·P_i)` logit modeli). Mevcut esneklik modeliyle doğal uyumludur.
* **Personel / şoför kalitesi**, **otogar yatırımı** (şehirde terminal açınca otogar ücreti düşer),
  **sezonluk talep** (yaz aylarında turizm çarpanı ×1,4).
* **Çok duraklı hatlar** (A–B–C): `RouteState`'e durak listesi; talep her durak çifti için toplanır.
