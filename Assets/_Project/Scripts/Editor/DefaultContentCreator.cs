using System.Collections.Generic;
using System.IO;
using BusTycoon.Data;
using BusTycoon.World;
using UnityEditor;
using UnityEngine;

namespace BusTycoon.EditorTools
{
    /// <summary>
    /// Oyunun çalışma anında kendini kurduğu içeriği (<see cref="DefaultContent"/>) düzenlenebilir ASSET'ler olarak üretir:
    /// ayarlar, 8 otobüs modeli, Mercator projeksiyonu ve Resources/BusTycoon/cities.csv'deki 93 şehir.
    /// Kendi sahnenizi kuracaksanız (uGUI/prefab yolu) bu asset'leri GameSession'a bağlayın.
    /// Var olan asset'lere dokunmaz (tasarımcının yaptığı ayarlar ezilmez).
    /// </summary>
    public static class DefaultContentCreator
    {
        const string Root = "Assets/_Project/Data";
        const string CitiesCsv = "Assets/_Project/Resources/" + DefaultContent.CitiesResourcePath + ".csv";

        [MenuItem("Bus Tycoon/Create Default Content")]
        public static void Create()
        {
            string configFolder = EnsureFolder(Root + "/Config");
            string fleetFolder = EnsureFolder(Root + "/Fleet");
            string worldFolder = EnsureFolder(Root + "/World");

            GetOrCreate<EconomyConfig>($"{configFolder}/EconomyConfig.asset", out _);
            var progression = GetOrCreate<ProgressionConfig>($"{configFolder}/ProgressionConfig.asset", out bool progressionCreated);

            var models = new List<BusModelData>();
            foreach (var spec in DefaultContent.Models)
            {
                var asset = GetOrCreate<BusModelData>($"{fleetFolder}/Bus_{spec.Id}.asset", out bool created);
                if (created)
                {
                    spec.ApplyTo(asset);
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

            var projection = GetOrCreate<MercatorMapProjection>($"{worldFolder}/MercatorProjection.asset", out bool projectionCreated);
            if (projectionCreated)
            {
                projection.Configure(DefaultContent.MinLongitude, DefaultContent.MaxLongitude,
                    DefaultContent.MinLatitude, DefaultContent.MaxLatitude, DefaultContent.MapWidth);
                EditorUtility.SetDirty(projection);
            }

            var database = GetOrCreate<WorldMapDatabase>($"{worldFolder}/WorldMapDatabase.asset", out _);
            if (database.Projection == null) database.SetProjection(projection);

            var csv = AssetDatabase.LoadAssetAtPath<TextAsset>(CitiesCsv);
            string importReport = csv != null
                ? CityCsvImporter.Import(csv.text, database, worldFolder, 0, new HashSet<string>())
                : $"Şehir CSV'si bulunamadı: {CitiesCsv}";

            foreach (var region in database.Regions)
            {
                if (region == null || !DefaultContent.TryGetRegionSpec(region.Id, out var spec)) continue;
                region.SetEconomy(spec.Fare, spec.Fuel, spec.Wage, spec.Level, spec.License);
                EditorUtility.SetDirty(region);
            }

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Selection.activeObject = database;

            EditorUtility.DisplayDialog("Bus Tycoon",
                "Varsayılan içerik asset olarak oluşturuldu.\n\n" + importReport +
                "\n\nNot: Oyunu oynamak için buna gerek yok; Play'e basmanız yeterli (oyun içeriği kendisi kurar).", "Tamam");
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
