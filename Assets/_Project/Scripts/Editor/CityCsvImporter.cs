using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BusTycoon.Data;
using BusTycoon.World;
using UnityEditor;
using UnityEngine;

namespace BusTycoon.EditorTools
{
    /// <summary>
    /// Gerçek dünya şehir listesini (CSV) CityData / RegionData asset'lerine dönüştürür ve
    /// WorldMapDatabase'e kaydeder. simplemaps "worldcities.csv" biçimiyle doğrudan uyumludur.
    ///
    /// Tanınan sütunlar (başlık adına göre, sıra önemsiz):
    ///   city | name            → şehir adı (zorunlu)
    ///   lat | latitude         → enlem (zorunlu)
    ///   lng | lon | longitude  → boylam (zorunlu)
    ///   iso2 | country_code    → bölge kimliği (zorunlu)
    ///   country                → bölge görünen adı
    ///   population             → nüfus
    ///   capital                → "primary" ise başkent
    ///   tourism                → turizm çarpanı (opsiyonel, varsayılan 1)
    ///   landmass               → kara kütlesi (opsiyonel, ada şehirleri için)
    /// Var olan asset'ler GÜNCELLENİR (GUID korunur, sahne referansları kırılmaz).
    /// </summary>
    public sealed class CityCsvImporter : EditorWindow
    {
        TextAsset csvFile;
        WorldMapDatabase database;
        string outputFolder = "Assets/_Project/Data/World";
        long minPopulation = 100_000;
        string countryFilter = "";

        [MenuItem("Bus Tycoon/Import Cities From CSV")]
        static void OpenWindow() => GetWindow<CityCsvImporter>("City CSV Importer");

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Sütunlar: city, lat, lng, iso2, country, population, capital, tourism, landmass.\n" +
                "Binlerce şehir için nüfus filtresi kullanın (harita performansı ve oynanış odağı).",
                MessageType.Info);

            csvFile = (TextAsset)EditorGUILayout.ObjectField("CSV dosyası", csvFile, typeof(TextAsset), false);
            database = (WorldMapDatabase)EditorGUILayout.ObjectField("World Map Database", database, typeof(WorldMapDatabase), false);
            outputFolder = EditorGUILayout.TextField("Çıktı klasörü", outputFolder);
            minPopulation = EditorGUILayout.LongField("Minimum nüfus", minPopulation);
            countryFilter = EditorGUILayout.TextField(new GUIContent("Ülke filtresi", "ISO2, virgülle: TR,BG,GR (boş = hepsi)"), countryFilter);

            using (new EditorGUI.DisabledScope(csvFile == null || database == null))
            {
                if (GUILayout.Button("İçe aktar", GUILayout.Height(28)))
                {
                    string report = Import(csvFile.text, database, outputFolder, minPopulation, ParseFilter(countryFilter));
                    Debug.Log("[CityCsvImporter] " + report);
                    EditorUtility.DisplayDialog("Şehir içe aktarımı", report, "Tamam");
                }
            }
        }

        public static string Import(string csvText, WorldMapDatabase database, string outputFolder,
            long minPopulation, HashSet<string> countries)
        {
            List<string[]> rows = ParseCsv(csvText);
            if (rows.Count < 2) return "CSV boş veya yalnızca başlık içeriyor.";

            var header = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rows[0].Length; i++) header[rows[0][i].Trim()] = i;

            int colName = Column(header, "city", "name");
            int colLat = Column(header, "lat", "latitude");
            int colLon = Column(header, "lng", "lon", "longitude");
            int colIso = Column(header, "iso2", "country_code");
            int colCountry = Column(header, "country");
            int colPopulation = Column(header, "population");
            int colCapital = Column(header, "capital");
            int colTourism = Column(header, "tourism");
            int colLandmass = Column(header, "landmass");
            if (colName < 0 || colLat < 0 || colLon < 0 || colIso < 0)
                return "Zorunlu sütunlar eksik: city/name, lat, lng/lon, iso2.";

            string regionFolder = EnsureFolder(outputFolder + "/Regions");
            string cityFolder = EnsureFolder(outputFolder + "/Cities");

            // 1) Bölgeleri önce oluştur (şehirler bunlara referans verecek).
            var regions = new Dictionary<string, RegionData>(StringComparer.OrdinalIgnoreCase);
            foreach (var region in database.Regions)
                if (region != null) regions[region.Id] = region;

            var accepted = new List<string[]>();
            for (int r = 1; r < rows.Count; r++)
            {
                string[] row = rows[r];
                string iso = Cell(row, colIso).ToUpperInvariant();
                if (string.IsNullOrEmpty(iso) || (countries.Count > 0 && !countries.Contains(iso))) continue;
                if (ParseLong(Cell(row, colPopulation)) < minPopulation) continue;
                accepted.Add(row);

                if (regions.ContainsKey(iso)) continue;
                string path = $"{regionFolder}/Region_{iso}.asset";
                var region = AssetDatabase.LoadAssetAtPath<RegionData>(path);
                if (region == null)
                {
                    region = ScriptableObject.CreateInstance<RegionData>();
                    region.Initialize(iso, Cell(row, colCountry), "eurasia");
                    AssetDatabase.CreateAsset(region, path);
                }
                regions[iso] = region;
                database.AddRegion(region);
            }

            // 2) Şehirler.
            var usedIds = new HashSet<string>(StringComparer.Ordinal);
            int created = 0, updated = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < accepted.Count; i++)
                {
                    string[] row = accepted[i];
                    if (i % 50 == 0) EditorUtility.DisplayProgressBar("Şehirler içe aktarılıyor", Cell(row, colName), i / (float)accepted.Count);

                    string iso = Cell(row, colIso).ToUpperInvariant();
                    string name = Cell(row, colName);
                    string id = UniqueId($"{iso}_{Slug(name)}", usedIds);
                    var coordinate = new GeoCoordinate(ParseDouble(Cell(row, colLat)), ParseDouble(Cell(row, colLon)));
                    bool capital = string.Equals(Cell(row, colCapital), "primary", StringComparison.OrdinalIgnoreCase);
                    float tourism = colTourism >= 0 && float.TryParse(Cell(row, colTourism), NumberStyles.Float, CultureInfo.InvariantCulture, out float t) ? t : 1f;

                    string folder = EnsureFolder($"{cityFolder}/{iso}");
                    string path = $"{folder}/City_{id}.asset";
                    var city = AssetDatabase.LoadAssetAtPath<CityData>(path);
                    bool isNew = city == null;
                    if (isNew) city = ScriptableObject.CreateInstance<CityData>();

                    city.Initialize(id, name, regions[iso], coordinate, ParseLong(Cell(row, colPopulation)), capital, tourism);
                    if (colLandmass >= 0) city.SetLandmassOverride(Cell(row, colLandmass));

                    if (isNew) { AssetDatabase.CreateAsset(city, path); created++; }
                    else { EditorUtility.SetDirty(city); updated++; }
                    database.AddCity(city);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            return $"{created} şehir oluşturuldu, {updated} şehir güncellendi, {regions.Count} bölge.";
        }

        // ------------------------------------------------------------------ yardımcılar

        static int Column(Dictionary<string, int> header, params string[] names)
        {
            foreach (var name in names)
                if (header.TryGetValue(name, out int index)) return index;
            return -1;
        }

        static string Cell(string[] row, int column) =>
            column >= 0 && column < row.Length ? row[column].Trim() : string.Empty;

        static long ParseLong(string text) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? (long)value : 0;

        static double ParseDouble(string text) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0;

        static HashSet<string> ParseFilter(string filter)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in filter.Split(','))
                if (!string.IsNullOrWhiteSpace(part)) set.Add(part.Trim());
            return set;
        }

        /// <summary>"İstanbul" → "ISTANBUL", "Şanlıurfa" → "SANLIURFA". Kalıcı kimlik üretimi.</summary>
        public static string Slug(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char raw in text.Normalize(NormalizationForm.FormD))
            {
                char c = raw switch
                {
                    'ı' => 'i', 'İ' => 'I', 'ş' => 's', 'Ş' => 'S', 'ğ' => 'g', 'Ğ' => 'G',
                    'ø' => 'o', 'Ø' => 'O', 'ß' => 's', 'æ' => 'a', 'Æ' => 'A', 'ł' => 'l', 'Ł' => 'L',
                    _ => raw
                };
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (c < 128 && char.IsLetterOrDigit(c)) sb.Append(char.ToUpperInvariant(c));
                else if (sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
            }
            return sb.ToString().Trim('_');
        }

        static string UniqueId(string id, HashSet<string> used)
        {
            string candidate = id;
            for (int n = 2; !used.Add(candidate); n++) candidate = $"{id}_{n}";
            return candidate;
        }

        static string EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return path;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            return path;
        }

        /// <summary>RFC-4180 uyumlu basit CSV ayrıştırıcı (tırnaklı alanlar, kaçışlı "" ve CRLF).</summary>
        public static List<string[]> ParseCsv(string text)
        {
            var rows = new List<string[]>();
            var fields = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inQuotes)
                {
                    if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else if (c == '"') inQuotes = false;
                    else field.Append(c);
                    continue;
                }

                switch (c)
                {
                    case '"': inQuotes = true; break;
                    case ',': fields.Add(field.ToString()); field.Clear(); break;
                    case '\r': break;
                    case '\n':
                        fields.Add(field.ToString()); field.Clear();
                        if (fields.Count > 1 || fields[0].Length > 0) rows.Add(fields.ToArray());
                        fields.Clear();
                        break;
                    default: field.Append(c); break;
                }
            }

            if (field.Length > 0 || fields.Count > 0)
            {
                fields.Add(field.ToString());
                rows.Add(fields.ToArray());
            }
            return rows;
        }
    }
}
