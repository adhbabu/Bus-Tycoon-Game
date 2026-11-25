using System.Collections.Generic;

namespace BusTycoon.Urban
{
    /// <summary>
    /// Sokak haritası olan şehirler (StreamingAssets/Cities/&lt;klasör&gt;: map.jpg + city.bytes, Tools/build_city.py).
    /// Oyundaki şehir kimliği (CityData.Id) → veri klasörü.
    /// </summary>
    public static class UrbanCatalog
    {
        /// <summary>StreamingAssets altındaki kök klasör.</summary>
        public const string Root = "Cities";

        static readonly Dictionary<string, string> Folders = new Dictionary<string, string>
        {
            { "TR_ISTANBUL", "istanbul" },
            { "TR_ANKARA", "ankara" },
            { "TR_IZMIR", "izmir" },
            { "TR_BURSA", "bursa" },
            { "TR_ANTALYA", "antalya" },
            { "TR_ADANA", "adana" },
            { "TR_KONYA", "konya" },
            { "TR_GAZIANTEP", "gaziantep" },
            { "TR_KAYSERI", "kayseri" },
            { "TR_SAMSUN", "samsun" },
        };

        public static IEnumerable<string> CityIds => Folders.Keys;

        /// <summary>Bu şehrin sokak haritası var mı.</summary>
        public static bool Has(string cityId) => cityId != null && Folders.ContainsKey(cityId);

        /// <summary>Veri klasörü (yoksa null).</summary>
        public static string FolderFor(string cityId) => cityId != null && Folders.TryGetValue(cityId, out string folder) ? folder : null;

        /// <summary>StreamingAssets'e göre dosya yolu (ör. "Cities/istanbul/map.jpg").</summary>
        public static string FilePath(string cityId, string file)
        {
            string folder = FolderFor(cityId);
            return folder != null ? $"{Root}/{folder}/{file}" : null;
        }
    }
}
