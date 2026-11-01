using System;
using System.Collections.Generic;

namespace BusTycoon.Data
{
    /// <summary>
    /// İçerik paketleri (DLC). Bir paket, bölgelere bağlıdır: cities.csv'deki "pack" sütunu şehrin ülkesini pakete
    /// koyar (<see cref="RegionData.PackId"/>). Paket yoksa o ülkeler haritada görünür ama lisansı alınamaz.
    /// </summary>
    public static class ContentPacks
    {
        /// <summary>Avrupa Paketi: Kıta Avrupası + Britanya, 31 ülke.</summary>
        public const string Europe = "europe";

        public static string DisplayName(string packId) =>
            packId == Europe ? "Avrupa Paketi" : string.IsNullOrEmpty(packId) ? "Temel oyun" : packId;
    }

    /// <summary>
    /// Paket sahipliği. Web sürümünde ve şimdilik mağaza sürümlerinde Avrupa Paketi ücretsiz (sahip olunur).
    /// Mağaza içi satın alma eklendiğinde yalnız burası değişir: satın alma doğrulanınca <see cref="Grant"/>,
    /// iade edilirse <see cref="Revoke"/>. Kayıt dosyasında tutulmaz (sahiplik cihaza/hesaba aittir).
    /// </summary>
    public static class Entitlements
    {
        static readonly HashSet<string> owned = new HashSet<string>(StringComparer.Ordinal) { ContentPacks.Europe };

        /// <summary>Sahiplik değişince (satın alma, iade, testler).</summary>
        public static event Action Changed;

        /// <summary>Paketsiz içerik (boş kimlik) herkese açıktır.</summary>
        public static bool Owns(string packId) => string.IsNullOrEmpty(packId) || owned.Contains(packId);

        public static void Grant(string packId)
        {
            if (!string.IsNullOrEmpty(packId) && owned.Add(packId)) Changed?.Invoke();
        }

        public static void Revoke(string packId)
        {
            if (!string.IsNullOrEmpty(packId) && owned.Remove(packId)) Changed?.Invoke();
        }
    }
}
