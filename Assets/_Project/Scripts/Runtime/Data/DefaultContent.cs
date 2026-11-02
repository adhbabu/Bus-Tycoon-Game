using System;
using System.Collections.Generic;
using BusTycoon.Core;
using BusTycoon.World;
using UnityEngine;

namespace BusTycoon.Data
{
    /// <summary>Bellekte oluşturulmuş tam oyun içeriği (asset gerektirmez).</summary>
    public sealed class GameContent
    {
        public WorldMapDatabase World;
        public MercatorMapProjection Projection;
        public EconomyConfig Economy;
        public ProgressionConfig Progression;
        public BusCatalog Catalog;
    }

    /// <summary>
    /// Varsayılan oyun içeriğinin TEK kaynağı: otobüs modelleri, bölge ayarları, harita sınırları.
    /// Hem oyunun çalışma anında kendini kurması (<see cref="Build"/>) hem de editördeki
    /// "Create Default Content" komutu bu tabloları kullanır.
    /// </summary>
    public static class DefaultContent
    {
        // Harita sınırları — Tools/build_map.py ile AYNI olmalı (harita dokusu bu sınırlarla üretilir). Kıta Avrupası,
        // Britanya, Türkiye ve Kafkasya. Genişlik boylam başına ~3,57 birim: eski (yalnız Türkiye ve çevresi) haritayla
        // aynı ölçek; kamera, otobüs ve durak boyları değişmez.
        public const double MinLongitude = -11.0;
        public const double MaxLongitude = 47.0;
        public const double MinLatitude = 34.0;
        public const double MaxLatitude = 61.0;
        public const float MapWidth = 100f / 28f * 58f;

        public const string CitiesResourcePath = "BusTycoon/cities";
        /// <summary>
        /// Harita dokuları StreamingAssets'te (Tools/build_map.py): açılıştan sonra indirilir, tarayıcıda arka planda çözülür
        /// (Game/StreamingImage.cs). Unity veri paketine girmez: açılış hızlanır, açılışta 4096 px çözme takılması olmaz.
        /// Genel harita: 4096 px Mercator, sınırlar <see cref="MapBounds"/> ile AYNI.
        /// </summary>
        public const string MapTextureFile = "Map/map.jpg";
        /// <summary>
        /// Haritanın çevresi: düşük çözünürlüklü, uzaklaştıkça bulanıklaşan ve denize karışan devam; harita "tak" diye
        /// bitmez. Sınırlar build_map.py'deki OUTER_LON/OUTER_LAT ile AYNI.
        /// </summary>
        public const string OuterMapTextureFile = "Map/map_outer.jpg";
        /// <summary>Deniz maskesi (R su, G kıyı, B açık deniz): kıyı halkaları (MapSurface).</summary>
        public const string WaterMaskFile = "Map/map_water.png";
        public const double OuterMinLongitude = -40.0;
        public const double OuterMaxLongitude = 76.0;
        public const double OuterMinLatitude = 22.0;
        public const double OuterMaxLatitude = 72.0;
        /// <summary>Karayolu ağı (Tools/build_map.py üretir): hat çizgileri ve otobüsler yolları izler.</summary>
        public const string RoadsResourcePath = "BusTycoon/roads";
        /// <summary>
        /// Kodla oluşturulan malzemelerin şablonları (Built-in pipeline). Mağaza/bulut derlemesi bunları
        /// Resources'a üretir; böylece Standard ve Unlit/Texture shader'ları derlemeden atılmaz (pembe nesne olmaz).
        /// </summary>
        public const string LitMaterialResourcePath = "BusTycoon/Materials/Lit";
        public const string MapMaterialResourcePath = "BusTycoon/Materials/MapUnlit";
        /// <summary>
        /// Arayüz yazı tipleri (Inter, SIL OFL; Tools/make_fonts.py), ikon atlası (Twemoji, CC BY 4.0;
        /// Tools/make_icons.py) ve ana menüdeki otobüs çizimi.
        /// </summary>
        public const string RegularFontResourcePath = "BusTycoon/Fonts/Inter-Medium";
        public const string HeavyFontResourcePath = "BusTycoon/Fonts/Inter-Bold";
        public const string DisplayFontResourcePath = "BusTycoon/Fonts/Inter-ExtraBold";
        public const string IconsResourcePath = "BusTycoon/UI/icons";
        public const string MascotResourcePath = "BusTycoon/UI/mascot";
        /// <summary>Otobüs modellerinin yandan çizimleri (Tools/make_bus_art.py): galeri, filo, planlayıcı, mini harita.</summary>
        public const string BusArtResourcePath = "BusTycoon/UI/buses";
        /// <summary>Her şehrin kendine özgü ikonu ve "nesiyle ünlü" açıklaması (Tools/make_city_icons.py).</summary>
        public const string CityIconsResourcePath = "BusTycoon/UI/cities";
        public const string CityIconIndexResourcePath = "BusTycoon/UI/city_icons";
        /// <summary>Durak tabelaları için derinlik testli yazı shader'ı (Resources'ta: derlemeye girer).</summary>
        public const string SignTextShaderResourcePath = "BusTycoon/Shaders/SignText";
        public const string MapRoadsShaderResourcePath = "BusTycoon/Shaders/MapRoads";
        /// <summary>Harita yüzeyi: canlı deniz, yakın arazi dokusu, bulut gölgeleri, gölge ve pus (GraphicsQuality).</summary>
        public const string MapSurfaceShaderResourcePath = "BusTycoon/Shaders/MapSurface";
        /// <summary>Haritadaki düz işaretler: şehir rozetleri ve otobüs kapsülleri (Game/MapSprites.cs).</summary>
        public const string MapSpriteShaderResourcePath = "BusTycoon/Shaders/MapSprite";

        public readonly struct ModelSpec
        {
            public readonly string Id, Name, Maker, Description;
            public readonly BusSegment Segment;
            public readonly float Price, Reputation, Comfort, Speed, RangeKm, LifetimeKm, Fare, Elasticity, Fuel, Maintenance, Insurance, Service;
            public readonly int Level, Seats;

            public ModelSpec(string id, string name, string maker, BusSegment segment, string description,
                float price, int level, float reputation, int seats, float comfort, float speed, float rangeKm, float lifetimeKm,
                float fare, float elasticity, float fuel, float maintenance, float insurance, float service)
            {
                Id = id; Name = name; Maker = maker; Segment = segment; Description = description;
                Price = price; Level = level; Reputation = reputation; Seats = seats; Comfort = comfort; Speed = speed;
                RangeKm = rangeKm; LifetimeKm = lifetimeKm; Fare = fare; Elasticity = elasticity; Fuel = fuel;
                Maintenance = maintenance; Insurance = insurance; Service = service;
            }

            public void ApplyTo(BusModelData model)
            {
                model.Initialize(Id, Name, Maker, Segment, Description);
                model.SetStats(Price, Level, Reputation, Seats, Comfort, Speed, RangeKm, LifetimeKm, Fare, Elasticity,
                    Fuel, Maintenance, Insurance, Service);
            }
        }

        public readonly struct RegionSpec
        {
            public readonly string Id;
            public readonly float Fare, Fuel, Wage, License;
            public readonly int Level;

            public RegionSpec(string id, float fare, float fuel, float wage, int level, float license)
            {
                Id = id; Fare = fare; Fuel = fuel; Wage = wage; Level = level; License = license;
            }
        }

        /// <summary>docs/03-Filo-ve-Progression.md tablosuyla aynı tutun.</summary>
        public static readonly ModelSpec[] Models =
        {
            //            id            ad                   üretici         segment                   açıklama
            //            fiyat  sv itibar koltuk konfor hız menzil  ömür(km)  bilet× esnek× yakıt bakım sigorta ikram
            new ModelSpec("KARTAL_M16", "Kartal M16", "Kartal Oto", BusSegment.Minibus,
                "Eski ama dayanıklı minibüs. Kısa hatlar için ideal başlangıç aracı.",
                22000, 1, 0, 16, 0.25f, 80, 300, 400000, 0.75f, 1.25f, 13, 0.06f, 6, 0.2f),
            new ModelSpec("KARTAL_M19", "Kartal M19 Plus", "Kartal Oto", BusSegment.Minibus,
                "Klimalı, 19 koltuklu yeni nesil minibüs.",
                34000, 2, 0, 19, 0.35f, 82, 350, 450000, 0.8f, 1.2f, 12, 0.055f, 7, 0.25f),
            new ModelSpec("ANKA_27", "Anka 27", "Anka Motor", BusSegment.Midibus,
                "Bölgesel hatların midibüsü: 27 koltuk, bagaj bölmesi.",
                78000, 3, 0, 27, 0.45f, 84, 600, 600000, 0.9f, 1.1f, 18, 0.08f, 12, 0.35f),
            new ModelSpec("ANKA_31", "Anka 31 Line", "Anka Motor", BusSegment.Midibus,
                "Uzatılmış şasi, 31 koltuk, daha konforlu koltuklar.",
                98000, 4, 0, 31, 0.5f, 85, 700, 650000, 0.92f, 1.05f, 19, 0.085f, 14, 0.4f),
            new ModelSpec("TOROS_46", "Toros 46", "Toros Otobüs", BusSegment.StandardCoach,
                "Şehirlerarası standart: 2+2, 46 koltuk. Ölçek ekonomisinin başladığı yer.",
                185000, 5, 0, 46, 0.6f, 88, 1200, 1000000, 1f, 1f, 28, 0.12f, 25, 0.5f),
            new ModelSpec("TOROS_42C", "Toros 42 Comfort", "Toros Otobüs", BusSegment.ComfortCoach,
                "Koltuk ekranı, ikram servisi, geniş diz mesafesi.",
                280000, 8, 40, 42, 0.75f, 90, 1800, 1100000, 1.2f, 0.85f, 30, 0.14f, 35, 1f),
            new ModelSpec("ZIRVE_VIP", "Zirve VIP 2+1", "Zirve Coach", BusSegment.LuxuryCoach,
                "2+1 deri koltuk, host/hostes, sıcak ikram. Yolcu fiyata en az duyarlı segment.",
                420000, 12, 55, 33, 0.92f, 90, 2500, 1200000, 1.55f, 0.75f, 31, 0.18f, 50, 2f),
            new ModelSpec("ZIRVE_DD", "Zirve Çift Katlı", "Zirve Coach", BusSegment.DoubleDecker,
                "72 koltuk. Yoğun ana arterlerde koltuk başı en düşük maliyet.",
                600000, 16, 50, 72, 0.8f, 85, 2000, 1200000, 1.3f, 0.8f, 38, 0.22f, 70, 1.2f),
        };

        /// <summary>Avrupa Paketi'nin otobüsleri (paket sahibiyse katalogda; uydurma markalar). docs/03 tablosuyla aynı.</summary>
        public static readonly ModelSpec[] EuropeModels =
        {
            new ModelSpec("NORDSTAR_E12", "Nordstar E12 Elektrik", "Nordstar Mobility", BusSegment.StandardCoach,
                "Sessiz elektrikli şehirlerarası: yakıt gideri çok düşük, bakımı ucuz. Rahat menzili 800 km.",
                360000, 14, 45, 44, 0.7f, 90, 800, 1400000, 1.1f, 0.95f, 8, 0.09f, 36, 0.6f),
            new ModelSpec("ALPINA_C50", "Alpina Reisestar 50", "Alpina Reisen", BusSegment.ComfortCoach,
                "Alp geçitlerinin hâkimi: 50 koltuk, tuvalet ve güçlü motor. Uzun Avrupa hatlarının konfor yıldızı.",
                340000, 15, 50, 50, 0.8f, 95, 2200, 1300000, 1.25f, 0.85f, 29, 0.14f, 38, 1f),
        };

        /// <summary>Bölge ekonomisi ve genişleme kilitleri. Listede olmayan bölgeler varsayılan (×1, seviye 1, lisans yok) alır.</summary>
        public static readonly RegionSpec[] Regions =
        {
            //             id    bilet× yakıt× maaş×  seviye  lisans
            new RegionSpec("TR", 1.00f, 1.00f, 1.00f, 1, 50000),
            new RegionSpec("BG", 1.10f, 1.05f, 1.30f, 6, 25000),
            new RegionSpec("GE", 0.80f, 0.90f, 0.70f, 7, 30000),
            new RegionSpec("GR", 1.40f, 1.30f, 1.80f, 9, 60000),
            new RegionSpec("RO", 1.10f, 1.10f, 1.40f, 11, 70000),
            // Avrupa Paketi (ContentPacks.Europe): yakın Balkanlar erken ve ucuz; Batı ve Kuzey Avrupa geç ve pahalı.
            // Batı'da bilet yüksek ama maaş ve yakıt daha da yüksek: uzun ve dolu hatlar kâr eder.
            new RegionSpec("RS", 1.10f, 1.10f, 1.40f, 8, 40000),
            new RegionSpec("MK", 0.95f, 1.05f, 1.10f, 8, 25000),
            new RegionSpec("AL", 1.00f, 1.10f, 1.10f, 9, 30000),
            new RegionSpec("XK", 0.95f, 1.05f, 1.00f, 9, 25000),
            new RegionSpec("ME", 1.15f, 1.15f, 1.40f, 10, 30000),
            new RegionSpec("BA", 1.00f, 1.10f, 1.20f, 10, 35000),
            new RegionSpec("MD", 0.85f, 1.00f, 0.90f, 10, 25000),
            new RegionSpec("HU", 1.50f, 1.40f, 2.10f, 12, 120000),
            new RegionSpec("HR", 1.60f, 1.50f, 2.30f, 12, 120000),
            new RegionSpec("SI", 1.70f, 1.50f, 2.60f, 13, 140000),
            new RegionSpec("SK", 1.50f, 1.45f, 2.20f, 13, 120000),
            new RegionSpec("PL", 1.45f, 1.35f, 2.10f, 13, 150000),
            new RegionSpec("CZ", 1.60f, 1.45f, 2.40f, 14, 160000),
            new RegionSpec("LT", 1.50f, 1.40f, 2.30f, 15, 120000),
            new RegionSpec("LV", 1.50f, 1.40f, 2.20f, 15, 110000),
            new RegionSpec("EE", 1.60f, 1.45f, 2.50f, 16, 120000),
            new RegionSpec("AT", 1.85f, 1.60f, 3.40f, 15, 250000),
            new RegionSpec("IT", 1.80f, 1.75f, 3.10f, 15, 300000),
            new RegionSpec("ES", 1.65f, 1.55f, 2.80f, 16, 300000),
            new RegionSpec("PT", 1.55f, 1.65f, 2.40f, 16, 220000),
            new RegionSpec("DE", 2.00f, 1.70f, 3.60f, 16, 400000),
            new RegionSpec("FR", 1.90f, 1.75f, 3.50f, 16, 380000),
            new RegionSpec("BE", 1.90f, 1.70f, 3.60f, 17, 280000),
            new RegionSpec("NL", 2.00f, 1.80f, 3.80f, 17, 300000),
            new RegionSpec("LU", 2.10f, 1.50f, 4.20f, 17, 200000),
            new RegionSpec("GB", 2.05f, 1.75f, 3.70f, 18, 450000),
            new RegionSpec("IE", 2.05f, 1.75f, 3.80f, 19, 300000),
            new RegionSpec("DK", 2.15f, 1.90f, 4.40f, 19, 350000),
            new RegionSpec("CH", 2.55f, 1.80f, 4.90f, 19, 500000),
            new RegionSpec("SE", 2.10f, 1.90f, 4.10f, 20, 380000),
            new RegionSpec("NO", 2.45f, 2.00f, 4.90f, 20, 450000),
        };

        public static bool TryGetRegionSpec(string regionId, out RegionSpec spec)
        {
            foreach (var r in Regions)
                if (r.Id == regionId) { spec = r; return true; }
            spec = default;
            return false;
        }

        /// <summary>
        /// Tüm içeriği bellekte oluşturur. <paramref name="citiesCsv"/>: Resources/BusTycoon/cities.csv içeriği
        /// (sütunlar: city, lat, lng, country, iso2, population, capital, tourism, pack — pack: içerik paketi, ör. "europe").
        /// </summary>
        public static GameContent Build(string citiesCsv)
        {
            var content = new GameContent
            {
                Economy = Create<EconomyConfig>("EconomyConfig"),
                Progression = Create<ProgressionConfig>("ProgressionConfig"),
                Catalog = Create<BusCatalog>("BusCatalog"),
                Projection = Create<MercatorMapProjection>("MercatorProjection"),
                World = Create<WorldMapDatabase>("WorldMapDatabase")
            };

            var models = new List<BusModelData>();
            foreach (var spec in Models)
            {
                var model = Create<BusModelData>("Bus_" + spec.Id);
                spec.ApplyTo(model);
                models.Add(model);
            }
            if (Entitlements.Owns(ContentPacks.Europe))
                foreach (var spec in EuropeModels)
                {
                    var model = Create<BusModelData>("Bus_" + spec.Id);
                    spec.ApplyTo(model);
                    models.Add(model);
                }
            content.Catalog.SetModels(models);
            content.Progression.SetStartingBus(models[0], 0.6f);

            content.Projection.Configure(MinLongitude, MaxLongitude, MinLatitude, MaxLatitude, MapWidth);
            content.World.SetProjection(content.Projection);
            AddCities(content.World, citiesCsv);
            return content;
        }

        static void AddCities(WorldMapDatabase database, string csv)
        {
            List<string[]> rows = CsvUtility.Parse(csv ?? string.Empty);
            if (rows.Count < 2) throw new InvalidOperationException("Şehir CSV'si boş.");

            string[] header = rows[0];
            int colName = CsvUtility.Column(header, "city", "name");
            int colLat = CsvUtility.Column(header, "lat", "latitude");
            int colLon = CsvUtility.Column(header, "lng", "lon", "longitude");
            int colCountry = CsvUtility.Column(header, "country");
            int colIso = CsvUtility.Column(header, "iso2", "country_code");
            int colPopulation = CsvUtility.Column(header, "population");
            int colCapital = CsvUtility.Column(header, "capital");
            int colTourism = CsvUtility.Column(header, "tourism");
            int colPack = CsvUtility.Column(header, "pack");

            var regions = new Dictionary<string, RegionData>(StringComparer.Ordinal);
            var usedIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 1; i < rows.Count; i++)
            {
                string[] row = rows[i];
                string iso = CsvUtility.Cell(row, colIso).ToUpperInvariant();
                string name = CsvUtility.Cell(row, colName);
                if (string.IsNullOrEmpty(iso) || string.IsNullOrEmpty(name)) continue;

                if (!regions.TryGetValue(iso, out RegionData region))
                {
                    region = Create<RegionData>("Region_" + iso);
                    region.Initialize(iso, CsvUtility.Cell(row, colCountry), "eurasia");
                    if (colPack >= 0) region.SetPack(CsvUtility.Cell(row, colPack).Trim().ToLowerInvariant());
                    if (TryGetRegionSpec(iso, out RegionSpec spec))
                        region.SetEconomy(spec.Fare, spec.Fuel, spec.Wage, spec.Level, spec.License);
                    regions[iso] = region;
                    database.AddRegion(region);
                }

                string id = CsvUtility.UniqueId($"{iso}_{CsvUtility.Slug(name)}", usedIds);
                var city = Create<CityData>("City_" + id);
                city.Initialize(id, name, region,
                    new GeoCoordinate(CsvUtility.ParseDouble(CsvUtility.Cell(row, colLat)), CsvUtility.ParseDouble(CsvUtility.Cell(row, colLon))),
                    CsvUtility.ParseLong(CsvUtility.Cell(row, colPopulation)),
                    string.Equals(CsvUtility.Cell(row, colCapital), "primary", StringComparison.OrdinalIgnoreCase),
                    colTourism >= 0 ? (float)CsvUtility.ParseDouble(CsvUtility.Cell(row, colTourism), 1.0) : 1f);
                database.AddCity(city);
            }
        }

        static T Create<T>(string objectName) where T : ScriptableObject
        {
            var instance = ScriptableObject.CreateInstance<T>();
            instance.name = objectName;
            return instance;
        }
    }
}
