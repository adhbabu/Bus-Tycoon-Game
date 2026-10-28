using System;
using System.Collections.Generic;
using BusTycoon.Data;

namespace BusTycoon.World
{
    /// <summary>
    /// <see cref="WorldMapDatabase"/>'in runtime sorgu katmanı: kimlikle şehir bulma, karayolu mesafesi,
    /// bağlanabilirlik ve yakın şehir aramaları. Saf C# (MonoBehaviour değil) olduğu için test edilebilir.
    /// </summary>
    public sealed class WorldGraph
    {
        readonly WorldMapDatabase database;
        readonly Dictionary<string, CityData> citiesById = new Dictionary<string, CityData>(StringComparer.Ordinal);
        readonly Dictionary<string, RegionData> regionsById = new Dictionary<string, RegionData>(StringComparer.Ordinal);
        readonly Dictionary<string, RoadDistanceOverride> overrides = new Dictionary<string, RoadDistanceOverride>(StringComparer.Ordinal);
        readonly HashSet<string> landmassLinks = new HashSet<string>(StringComparer.Ordinal);

        public WorldGraph(WorldMapDatabase database)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));

            foreach (var region in database.Regions)
                if (region != null) regionsById[region.Id] = region;

            foreach (var city in database.Cities)
                if (city != null && !string.IsNullOrEmpty(city.Id)) citiesById[city.Id] = city;

            foreach (var road in database.RoadOverrides)
                if (road.cityA != null && road.cityB != null) overrides[PairKey(road.cityA.Id, road.cityB.Id)] = road;

            foreach (var link in database.LandmassLinks)
                landmassLinks.Add(PairKey(link.landmassA, link.landmassB));
        }

        public IReadOnlyList<CityData> Cities => database.Cities;
        public IReadOnlyList<RegionData> Regions => database.Regions;
        public MapProjection Projection => database.Projection;

        public CityData GetCity(string cityId) =>
            cityId != null && citiesById.TryGetValue(cityId, out var city) ? city : null;

        public RegionData GetRegion(string regionId) =>
            regionId != null && regionsById.TryGetValue(regionId, out var region) ? region : null;

        public double GetStraightDistanceKm(CityData a, CityData b) =>
            GeoCoordinate.HaversineKm(a.Coordinate, b.Coordinate);

        /// <summary>Karayolu mesafesi: varsa elle girilmiş değer, yoksa kuş uçuşu × dolambaç katsayısı.</summary>
        public float GetRoadDistanceKm(CityData a, CityData b)
        {
            if (overrides.TryGetValue(PairKey(a.Id, b.Id), out var road) && road.roadKm > 0f)
                return road.roadKm;
            return (float)(GetStraightDistanceKm(a, b) * database.RoadCircuityFactor);
        }

        /// <summary>İki şehir arasında fiziksel olarak hat kurulabilir mi? (Progression kilitleri burada DEĞİL.)</summary>
        public bool CanConnect(CityData a, CityData b, out string reason)
        {
            reason = null;
            if (a == null || b == null) { reason = "Şehir seçilmedi."; return false; }
            if (a == b) { reason = "Başlangıç ve varış aynı şehir olamaz."; return false; }

            if (overrides.TryGetValue(PairKey(a.Id, b.Id), out var road) && road.blocked)
            {
                reason = "Bu iki şehir arasında karayolu bağlantısı yok.";
                return false;
            }

            if (a.LandmassId != b.LandmassId && !landmassLinks.Contains(PairKey(a.LandmassId, b.LandmassId)))
            {
                reason = "Şehirler farklı kıtalarda/adalarda; karayolu bağlantısı yok.";
                return false;
            }

            float km = GetRoadDistanceKm(a, b);
            if (km < database.MinRouteDistanceKm) { reason = "Şehirler birbirine çok yakın."; return false; }
            if (km > database.MaxRouteDistanceKm) { reason = $"Mesafe çok uzun ({km:N0} km)."; return false; }
            return true;
        }

        /// <summary>Kaynak şehre belirli karayolu mesafesindeki şehirler, yakından uzağa sıralı.</summary>
        public void GetCitiesWithinKm(CityData origin, float maxRoadKm, List<CityData> results)
        {
            results.Clear();
            foreach (var city in database.Cities)
            {
                if (city == null || city == origin) continue;
                if (GetRoadDistanceKm(origin, city) <= maxRoadKm) results.Add(city);
            }
            results.Sort((x, y) => GetRoadDistanceKm(origin, x).CompareTo(GetRoadDistanceKm(origin, y)));
        }

        static string PairKey(string a, string b) =>
            string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;
    }
}
