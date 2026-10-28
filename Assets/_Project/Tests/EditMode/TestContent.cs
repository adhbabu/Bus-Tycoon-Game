using System.Collections.Generic;
using BusTycoon.Data;
using BusTycoon.World;
using UnityEngine;

namespace BusTycoon.Tests
{
    /// <summary>Testler için bellekte (asset oluşturmadan) içerik üretir.</summary>
    internal sealed class TestContent
    {
        readonly List<Object> created = new List<Object>();

        public readonly EconomyConfig Economy;
        public readonly ProgressionConfig Progression;
        public readonly RegionData Turkey;
        public readonly CityData Istanbul;
        public readonly CityData Ankara;
        public readonly CityData Bursa;
        public readonly BusModelData Minibus;
        public readonly BusModelData Coach;
        public readonly BusModelData Luxury;
        public readonly BusCatalog Catalog;
        public readonly WorldMapDatabase Database;

        public TestContent()
        {
            Economy = Make<EconomyConfig>();
            Progression = Make<ProgressionConfig>();

            Turkey = Make<RegionData>();
            Turkey.Initialize("TR", "Türkiye", "eurasia");

            Istanbul = City("TR_ISTANBUL", "İstanbul", 41.0082, 28.9784, 15_600_000);
            Ankara = City("TR_ANKARA", "Ankara", 39.9334, 32.8597, 5_800_000);
            Bursa = City("TR_BURSA", "Bursa", 40.1885, 29.0610, 3_200_000);

            Minibus = Bus("MINI", BusSegment.Minibus, 22000, 1, 0, 16, 0.25f, 80, 300, 0.75f, 1.25f, 13, 0.06f, 6, 0.2f);
            Coach = Bus("COACH", BusSegment.StandardCoach, 185000, 5, 0, 46, 0.6f, 88, 1200, 1f, 1f, 28, 0.12f, 25, 0.5f);
            Luxury = Bus("VIP", BusSegment.LuxuryCoach, 420000, 12, 55, 33, 0.92f, 90, 2500, 1.55f, 0.75f, 31, 0.18f, 50, 2f);

            Catalog = Make<BusCatalog>();
            Catalog.SetModels(new[] { Minibus, Coach, Luxury });
            Progression.SetStartingBus(Minibus, 0.6f);

            Database = Make<WorldMapDatabase>();
            Database.AddRegion(Turkey);
            Database.AddCity(Istanbul);
            Database.AddCity(Ankara);
            Database.AddCity(Bursa);
        }

        CityData City(string id, string name, double lat, double lon, long population)
        {
            var city = Make<CityData>();
            city.Initialize(id, name, Turkey, new GeoCoordinate(lat, lon), population, false);
            return city;
        }

        BusModelData Bus(string id, BusSegment segment, float price, int level, float reputation, int seats, float comfort,
            float speed, float range, float fare, float elasticity, float fuel, float maintenance, float insurance, float service)
        {
            var bus = Make<BusModelData>();
            bus.Initialize(id, id, "Test", segment, "");
            bus.SetStats(price, level, reputation, seats, comfort, speed, range, 1_000_000, fare, elasticity, fuel, maintenance, insurance, service);
            return bus;
        }

        T Make<T>() where T : ScriptableObject
        {
            var instance = ScriptableObject.CreateInstance<T>();
            created.Add(instance);
            return instance;
        }

        public void Dispose()
        {
            foreach (var obj in created) Object.DestroyImmediate(obj);
            created.Clear();
        }
    }
}
