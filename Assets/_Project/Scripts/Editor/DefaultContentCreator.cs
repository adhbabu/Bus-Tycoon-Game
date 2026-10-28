using System.Collections.Generic;
using System.IO;
using BusTycoon.Data;
using BusTycoon.World;
using UnityEditor;
using UnityEngine;

namespace BusTycoon.EditorTools
{
    /// <summary>
    /// Tek menü komutuyla oynanabilir başlangıç içeriği üretir: ayar asset'leri, 8 otobüs modeli
    /// (6 segment), Türkiye + komşular için Mercator projeksiyonu ve örnek CSV'den şehirler.
    /// Var olan asset'lere dokunmaz (tasarımcının yaptığı ayarlar ezilmez).
    /// </summary>
    public static class DefaultContentCreator
    {
        const string Root = "Assets/_Project/Data";
        const string SampleCsv = Root + "/World/cities_sample.csv";

        /// <summary>
        /// Model tablosu — docs/03-Filo-ve-Progression.md ile aynı tutun.
        /// fiyat, seviye, itibar, koltuk, konfor, hız, menzil, ömür(km), bilet×, esneklik×, yakıt, bakım/km, sigorta/gün, servis/yolcu
        /// </summary>
        static readonly (string id, string name, string maker, BusSegment segment, string text, float[] s)[] Models =
        {
            ("KARTAL_M16", "Kartal M16", "Kartal Oto", BusSegment.Minibus,
                "Eski ama dayanıklı minibüs. Kısa hatlar için ideal başlangıç aracı.",
                new[] { 22000f, 1, 0, 16, 0.25f, 80, 300, 400000, 0.75f, 1.25f, 13, 0.06f, 6, 0.2f }),
            ("KARTAL_M19", "Kartal M19 Plus", "Kartal Oto", BusSegment.Minibus,
                "Klimalı, 19 koltuklu yeni nesil minibüs.",
                new[] { 34000f, 2, 0, 19, 0.35f, 82, 350, 450000, 0.8f, 1.2f, 12, 0.055f, 7, 0.25f }),
            ("ANKA_27", "Anka 27", "Anka Motor", BusSegment.Midibus,
                "Bölgesel hatların midibüsü: 27 koltuk, bagaj bölmesi.",
                new[] { 78000f, 3, 0, 27, 0.45f, 84, 600, 600000, 0.9f, 1.1f, 18, 0.08f, 12, 0.35f }),
            ("ANKA_31", "Anka 31 Line", "Anka Motor", BusSegment.Midibus,
                "Uzatılmış şasi, 31 koltuk, daha konforlu koltuklar.",
                new[] { 98000f, 4, 0, 31, 0.5f, 85, 700, 650000, 0.92f, 1.05f, 19, 0.085f, 14, 0.4f }),
            ("TOROS_46", "Toros 46", "Toros Otobüs", BusSegment.StandardCoach,
                "Şehirlerarası standart: 2+2, 46 koltuk. Ölçek ekonomisinin başladığı yer.",
                new[] { 185000f, 5, 0, 46, 0.6f, 88, 1200, 1000000, 1f, 1f, 28, 0.12f, 25, 0.5f }),
            ("TOROS_42C", "Toros 42 Comfort", "Toros Otobüs", BusSegment.ComfortCoach,
                "Koltuk ekranı, ikram servisi, geniş diz mesafesi.",
                new[] { 280000f, 8, 40, 42, 0.75f, 90, 1800, 1100000, 1.2f, 0.85f, 30, 0.14f, 35, 1f }),
            ("ZIRVE_VIP", "Zirve VIP 2+1", "Zirve Coach", BusSegment.LuxuryCoach,
                "2+1 deri koltuk, host/hostes, sıcak ikram. Yolcu fiyata en az duyarlı segment.",
                new[] { 420000f, 12, 55, 33, 0.92f, 90, 2500, 1200000, 1.55f, 0.75f, 31, 0.18f, 50, 2f }),
            ("ZIRVE_DD", "Zirve Çift Katlı", "Zirve Coach", BusSegment.DoubleDecker,
                "72 koltuk. Yoğun ana arterlerde koltuk başı en düşük maliyet.",
                new[] { 600000f, 16, 50, 72, 0.8f, 85, 2000, 1200000, 1.3f, 0.8f, 38, 0.22f, 70, 1.2f }),
        };

        /// <summary>Örnek bölgeler: bilet×, yakıt×, maaş×, gereken seviye, lisans bedeli.</summary>
        static readonly Dictionary<string, (float fare, float fuel, float wage, int level, float license)> Regions =
            new Dictionary<string, (float, float, float, int, float)>
            {
                ["TR"] = (1.0f, 1.0f, 1.0f, 1, 50000f),
                ["BG"] = (1.1f, 1.05f, 1.3f, 6, 25000f),
                ["GE"] = (0.8f, 0.9f, 0.7f, 7, 30000f),
                ["GR"] = (1.4f, 1.3f, 1.8f, 9, 60000f),
            };

        [MenuItem("Bus Tycoon/Create Default Content")]
        public static void Create()
        {
            string configFolder = EnsureFolder(Root + "/Config");
            string fleetFolder = EnsureFolder(Root + "/Fleet");
            string worldFolder = EnsureFolder(Root + "/World");

            GetOrCreate<EconomyConfig>($"{configFolder}/EconomyConfig.asset", out _);
            var progression = GetOrCreate<ProgressionConfig>($"{configFolder}/ProgressionConfig.asset", out bool progressionCreated);

            // Otobüs modelleri + katalog
            var models = new List<BusModelData>();
            foreach (var m in Models)
            {
                var asset = GetOrCreate<BusModelData>($"{fleetFolder}/Bus_{m.id}.asset", out bool created);
                if (created)
                {
                    float[] s = m.s;
                    asset.Initialize(m.id, m.name, m.maker, m.segment, m.text);
                    asset.SetStats(s[0], (int)s[1], s[2], (int)s[3], s[4], s[5], s[6], s[7], s[8], s[9], s[10], s[11], s[12], s[13]);
                    EditorUtility.SetDirty(asset);
                }
                models.Add(asset);
            }

            var catalog = GetOrCreate<BusCatalog>($"{fleetFolder}/BusCatalog.asset", out bool catalogCreated);
            if (catalogCreated)
            {
                catalog.SetModels(models);
                EditorUtility.SetDirty(catalog);
            }

            if (progressionCreated)
            {
                progression.SetStartingBus(models[0], 0.6f);
                EditorUtility.SetDirty(progression);
            }

            // Harita: Türkiye + Balkanlar + Kafkasya
            var projection = GetOrCreate<MercatorMapProjection>($"{worldFolder}/MercatorProjection.asset", out bool projectionCreated);
            if (projectionCreated)
            {
                projection.Configure(19.0, 47.0, 34.0, 45.0, 100f);
                EditorUtility.SetDirty(projection);
            }

            var database = GetOrCreate<WorldMapDatabase>($"{worldFolder}/WorldMapDatabase.asset", out _);
            if (database.Projection == null) database.SetProjection(projection);

            var csv = AssetDatabase.LoadAssetAtPath<TextAsset>(SampleCsv);
            string importReport = csv != null
                ? CityCsvImporter.Import(csv.text, database, worldFolder, 0, new HashSet<string>())
                : $"Örnek CSV bulunamadı: {SampleCsv}";

            foreach (var region in database.Regions)
            {
                if (region == null || !Regions.TryGetValue(region.Id, out var e)) continue;
                region.SetEconomy(e.fare, e.fuel, e.wage, e.level, e.license);
                EditorUtility.SetDirty(region);
            }

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Selection.activeObject = database;

            EditorUtility.DisplayDialog("Bus Tycoon",
                "Varsayılan içerik hazır.\n\n" + importReport +
                "\n\nSahneye bir GameSession ekleyip bu asset'leri bağlayın (README → Hızlı Başlangıç).", "Tamam");
        }

        static T GetOrCreate<T>(string path, out bool created) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            created = asset == null;
            if (!created) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static string EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return path;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            return path;
        }
    }
}
