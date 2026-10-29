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
        // Harita sınırları — Tools/build_world_data.py ile AYNI olmalı (map.png bu sınırlarla üretilir).
        public const double MinLongitude = 19.0;
        public const double MaxLongitude = 47.0;
        public const double MinLatitude = 34.0;
        public const double MaxLatitude = 45.0;
        public const float MapWidth = 100f;

        public const string CitiesResourcePath = "BusTycoon/cities";
        public const string MapTextureResourcePath = "BusTycoon/map";
        /// <summary>
        /// Haritanın çevresi (Tools/build_map.py → map_outer.jpg): düşük çözünürlüklü, uzaklaştıkça bulanıklaşan ve
        /// denize karışan devam; harita "tak" diye bitmez. Sınırlar build_map.py'deki OUTER_LON/OUTER_LAT ile AYNI.
        /// </summary>
        public const string OuterMapTextureResourcePath = "BusTycoon/map_outer";
        public const double OuterMinLongitude = 1.0;
        public const double OuterMaxLongitude = 65.0;
        public const double OuterMinLatitude = 27.0;
        public const double OuterMaxLatitude = 54.0;
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

        /// <summary>Bölge ekonomisi ve genişleme kilitleri. Listede olmayan bölgeler varsayılan (×1, seviye 1, lisans yok) alır.</summary>
        public static readonly RegionSpec[] Regions =
        {
            //             id    bilet× yakıt× maaş×  seviye  lisans
            new RegionSpec("TR", 1.00f, 1.00f, 1.00f, 1, 50000),
            new RegionSpec("BG", 1.10f, 1.05f, 1.30f, 6, 25000),
            new RegionSpec("GE", 0.80f, 0.90f, 0.70f, 7, 30000),
            new RegionSpec("GR", 1.40f, 1.30f, 1.80f, 9, 60000),
            new RegionSpec("RO", 1.10f, 1.10f, 1.40f, 11, 70000),
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
        /// (sütunlar: city, lat, lng, country, iso2, population, capital, tourism).
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
