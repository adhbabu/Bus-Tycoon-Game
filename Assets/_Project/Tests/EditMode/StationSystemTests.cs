using BusTycoon.Data;
using BusTycoon.Economy;
using BusTycoon.Fleet;
using BusTycoon.Progression;
using BusTycoon.Routes;
using BusTycoon.Save;
using BusTycoon.Stations;
using BusTycoon.World;
using NUnit.Framework;
using UnityEngine;

namespace BusTycoon.Tests
{
    public sealed class StationSystemTests
    {
        TestContent content;
        CompanyState state;
        FinanceSystem finance;
        ProgressionSystem progression;
        FleetSystem fleet;
        StationSystem stations;
        RouteSystem routes;

        [SetUp]
        public void SetUp()
        {
            content = new TestContent();
            state = new CompanyState { cash = 500_000, reputation = 30, startCityId = content.Istanbul.Id };
            finance = new FinanceSystem(state);
            progression = new ProgressionSystem(state, content.Progression, content.Catalog, content.Economy, finance);
            progression.GrantRegion(content.Turkey);
            fleet = new FleetSystem(state, content.Catalog, content.Economy, finance, progression);
            stations = new StationSystem(state, finance, progression);
            routes = new RouteSystem(state, new WorldGraph(content.Database), content.Economy, fleet, finance, progression, stations);
        }

        [TearDown]
        public void TearDown() => content.Dispose();

        CityData Town(long population)
        {
            var city = ScriptableObject.CreateInstance<CityData>();
            city.Initialize("TR_TOWN_" + population, "Kasaba", content.Turkey, new GeoCoordinate(39.0, 30.0), population, false);
            return city;
        }

        [Test]
        public void Tier_FollowsCityPopulation()
        {
            Assert.That(StationCatalog.TierOf(content.Istanbul), Is.EqualTo(StationTier.Metro));
            Assert.That(StationCatalog.TierOf(Town(1_500_000)), Is.EqualTo(StationTier.Large));
            Assert.That(StationCatalog.TierOf(Town(500_000)), Is.EqualTo(StationTier.Terminal));
            Assert.That(StationCatalog.TierOf(Town(120_000)), Is.EqualTo(StationTier.Stop));
            Assert.That(StationCatalog.AvailableCount(StationTier.Stop), Is.LessThan(StationCatalog.AvailableCount(StationTier.Metro)));
            Assert.That(StationCatalog.MaxComfort(StationTier.Metro), Is.GreaterThan(StationCatalog.MaxComfort(StationTier.Stop)));
        }

        [Test]
        public void Build_ChargesCostAndRaisesRouteDemand()
        {
            Assert.That(routes.TryOpenRoute(content.Istanbul, content.Ankara, 25f, out RouteState route).Success, Is.True);
            fleet.GrantBus(content.Coach, 1f);
            Assert.That(routes.TryAssignBus(fleet.Buses[0].busId, route.routeId).Success, Is.True);
            float demandBefore = routes.Forecast(route).Demand;

            StationUpgrade seats = StationCatalog.Get(StationCatalog.Seats);
            float cost = stations.Cost(content.Istanbul, seats);
            double cashBefore = finance.Cash;
            Assert.That(stations.TryBuild(content.Istanbul, seats).Success, Is.True);

            Assert.That(finance.Cash, Is.EqualTo(cashBefore - cost).Within(0.01));
            Assert.That(cost, Is.EqualTo(seats.BaseCost * StationCatalog.CostMultiplier(StationTier.Metro)).Within(500f));
            Assert.That(stations.Has(content.Istanbul, seats), Is.True);
            Assert.That(stations.Comfort(content.Istanbul), Is.EqualTo(seats.Comfort));

            float expected = 1f + 0.5f * seats.Comfort * StationSystem.DemandBonusPerComfort;
            Assert.That(stations.RouteDemandFactor(content.Istanbul, content.Ankara), Is.EqualTo(expected).Within(1e-5f));
            Assert.That(routes.Forecast(route).Demand / demandBefore, Is.EqualTo(expected).Within(1e-3f), "Hat talebi durak çarpanı kadar artmalı");
        }

        [Test]
        public void Build_RejectsDuplicateWrongTierNonOperatingAndUnaffordable()
        {
            StationUpgrade plants = StationCatalog.Get(StationCatalog.Plants);
            Assert.That(stations.TryBuild(content.Istanbul, plants).Success, Is.True);
            Assert.That(stations.TryBuild(content.Istanbul, plants).Success, Is.False, "Aynı geliştirme iki kez yapılamaz");

            Assert.That(stations.CanBuild(content.Ankara, plants).Success, Is.False, "Hattı olmayan şehrin durağı geliştirilemez");

            CityData town = Town(120_000);
            state.startCityId = town.Id;
            Assert.That(stations.CanBuild(town, StationCatalog.Get(StationCatalog.Vip)).Success, Is.False, "VIP salonu küçük durakta yok");
            Assert.That(stations.CanBuild(town, plants).Success, Is.True);

            state.cash = 10;
            Assert.That(stations.CanBuild(town, StationCatalog.Get(StationCatalog.Board)).Success, Is.False, "Para yetmezse yapılamaz");
            Assert.That(stations.UpgradeCount(content.Istanbul), Is.EqualTo(1));
        }

        [Test]
        public void Popularity_GrowsWithTrafficAndComfort()
        {
            Assert.That(routes.TryOpenRoute(content.Istanbul, content.Ankara, 25f, out RouteState route).Success, Is.True);
            StationPopularity quiet = stations.Popularity(content.Istanbul);
            Assert.That(quiet.Traffic, Is.EqualTo(0f));
            Assert.That(quiet.Fame, Is.GreaterThan(0.5f), "Büyük şehir tanınmışlıkla başlar");

            route.AddReport(new RouteDayReport { passengers = 1500f });
            StationPopularity busy = stations.Popularity(content.Istanbul);
            Assert.That(stations.DailyPassengers(content.Istanbul), Is.EqualTo(1500f));
            Assert.That(busy.Traffic, Is.GreaterThan(0.5f));
            Assert.That(busy.Score, Is.GreaterThan(quiet.Score));

            Assert.That(stations.TryBuild(content.Istanbul, StationCatalog.Get(StationCatalog.Kiosk)).Success, Is.True);
            Assert.That(stations.Popularity(content.Istanbul).Score, Is.GreaterThan(busy.Score));
            Assert.That(stations.Popularity(content.Istanbul).Stars, Is.InRange(0f, 5f));
        }
    }
}
