using System;
using System.Collections.Generic;
using BusTycoon.World;
using UnityEngine;

namespace BusTycoon.Data
{
    /// <summary>
    /// Bilinen gerçek karayolu mesafesi (kuş uçuşu × dolambaç katsayısı tahmini yerine) veya
    /// kapalı bağlantı (ör. iki şehir arasında sınır kapısı yok).
    /// </summary>
    [Serializable]
    public struct RoadDistanceOverride
    {
        public CityData cityA;
        public CityData cityB;
        [Min(0f)] public float roadKm;
        public bool blocked;
    }

    /// <summary>Farklı kara kütleleri arasında karayolu/feribot bağlantısı (ör. "eurasia" ↔ "crete").</summary>
    [Serializable]
    public struct LandmassLink
    {
        public string landmassA;
        public string landmassB;
    }

    /// <summary>
    /// Haritanın tek giriş noktası: tüm bölgeler, şehirler ve yol kuralları.
    /// Runtime'da hızlı sorgular için <see cref="WorldGraph"/> bu varlıktan inşa edilir;
    /// ScriptableObject'in kendisi oyun sırasında asla değiştirilmez.
    /// </summary>
    [CreateAssetMenu(menuName = "Bus Tycoon/World/World Map Database", fileName = "WorldMapDatabase")]
    public sealed class WorldMapDatabase : ScriptableObject
    {
        [SerializeField] MapProjection projection;
        [SerializeField] List<RegionData> regions = new List<RegionData>();
        [SerializeField] List<CityData> cities = new List<CityData>();

        [Header("Yol kuralları")]
        [Tooltip("Kuş uçuşu mesafeyi karayolu mesafesine çeviren katsayı. Gerçek dünyada tipik değer 1.2–1.35.")]
        [SerializeField, Range(1f, 2f)] float roadCircuityFactor = 1.25f;
        [Tooltip("Bu karayolu mesafesinin üzerinde hat açılamaz.")]
        [SerializeField, Min(50f)] float maxRouteDistanceKm = 3000f;
        [Tooltip("Bu mesafenin altındaki şehirler arasında hat açılamaz (şehir içi sayılır).")]
        [SerializeField, Min(0f)] float minRouteDistanceKm = 20f;
        [SerializeField] List<RoadDistanceOverride> roadOverrides = new List<RoadDistanceOverride>();
        [SerializeField] List<LandmassLink> landmassLinks = new List<LandmassLink>();

        public MapProjection Projection => projection;
        public IReadOnlyList<RegionData> Regions => regions;
        public IReadOnlyList<CityData> Cities => cities;
        public float RoadCircuityFactor => roadCircuityFactor;
        public float MaxRouteDistanceKm => maxRouteDistanceKm;
        public float MinRouteDistanceKm => minRouteDistanceKm;
        public IReadOnlyList<RoadDistanceOverride> RoadOverrides => roadOverrides;
        public IReadOnlyList<LandmassLink> LandmassLinks => landmassLinks;

        /// <summary>Kimlik çakışması ve eksik referansları raporlar. Editörde ve oyun başlangıcında çağrılır.</summary>
        public bool Validate(List<string> errors)
        {
            int before = errors.Count;
            var cityIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var city in cities)
            {
                if (city == null) { errors.Add("Şehir listesinde boş referans var."); continue; }
                if (string.IsNullOrEmpty(city.Id)) errors.Add($"'{city.name}' şehrinin kimliği yok.");
                else if (!cityIds.Add(city.Id)) errors.Add($"Tekrarlanan şehir kimliği: {city.Id}");
                if (city.Region == null) errors.Add($"'{city.Id}' bir bölgeye bağlı değil.");
                else if (!regions.Contains(city.Region)) errors.Add($"'{city.Id}' şehrinin bölgesi ({city.Region.Id}) veritabanında kayıtlı değil.");
            }

            var regionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var region in regions)
            {
                if (region == null) { errors.Add("Bölge listesinde boş referans var."); continue; }
                if (!regionIds.Add(region.Id)) errors.Add($"Tekrarlanan bölge kimliği: {region.Id}");
            }

            return errors.Count == before;
        }

        internal void SetProjection(MapProjection value) => projection = value;

        internal void AddRegion(RegionData region)
        {
            if (region != null && !regions.Contains(region)) regions.Add(region);
        }

        internal void AddCity(CityData city)
        {
            if (city != null && !cities.Contains(city)) cities.Add(city);
        }
    }
}
