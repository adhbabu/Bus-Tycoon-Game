using System.Collections.Generic;
using BusTycoon.Economy;
using BusTycoon.Fleet;
using BusTycoon.Progression;
using BusTycoon.Routes;
using BusTycoon.Save;
using BusTycoon.World;
using NUnit.Framework;

namespace BusTycoon.Tests
{
    public sealed class EconomySystemsTests
    {
        TestContent content;
        CompanyState state;
        FinanceSystem finance;
        ProgressionSystem progression;
        FleetSystem fleet;
        RouteSystem routes;

        [SetUp]
        public void SetUp()
        {
            content = new TestContent();
            state = new CompanyState { cash = 100_000, reputation = 30 };
            finance = new FinanceSystem(state);
            progression = new ProgressionSystem(state, content.Progression, content.Catalog, content.Economy, finance);
            progression.GrantRegion(content.Turkey);
            fleet = new FleetSystem(state, content.Catalog, content.Economy, finance, progression);
            routes = new RouteSystem(state, new WorldGraph(content.Database), content.Economy, fleet, finance, progression);
        }

        [TearDown]
        public void TearDown() => content.Dispose();

        RouteEconomics.Inputs Inputs(float price) => new RouteEconomics.Inputs
        {
            CityA = content.Istanbul,
            CityB = content.Ankara,
            DistanceKm = 440f,
            TicketPrice = price,
            CompanyReputation = 50f,
            DaysOpen = 60,
            AutoSchedule = true,
            ManualUtilization = 1f,
            DemandNoise = 1f,
            ElasticityBias = 1f,
            IncludeUnavailableBuses = true
        };

        [Test]
        public void RouteWithoutBuses_CarriesNobody()
        {
            RouteForecast f = RouteEconomics.Evaluate(Inputs(25f), new List<BusInstance>(), content.Economy);
            Assert.That(f.Passengers, Is.EqualTo(0f));
            Assert.That(f.Revenue, Is.EqualTo(0f));
            Assert.That(f.Demand, Is.GreaterThan(0f), "Talep otobüs olmasa da hesaplanmalı (teklif kartı için).");
        }

        [Test]
        public void AddingBuses_RaisesPassengers_WhenCapacityConstrained()
        {
            var one = new List<BusInstance> { BusInstance.CreateVirtual(content.Coach) };
            var three = new List<BusInstance> { BusInstance.CreateVirtual(content.Coach), BusInstance.CreateVirtual(content.Coach), BusInstance.CreateVirtual(content.Coach) };

            float withOne = RouteEconomics.Evaluate(Inputs(22f), one, content.Economy).Passengers;
            float withThree = RouteEconomics.Evaluate(Inputs(22f), three, content.Economy).Passengers;
            Assert.That(withThree, Is.GreaterThan(withOne * 1.5f));
        }

        [Test]
        public void LuxurySegment_HasHigherReferencePrice_AndLowerElasticity()
        {
            var coach = RouteEconomics.Evaluate(Inputs(30f), new[] { BusInstance.CreateVirtual(content.Coach) }, content.Economy);
            var luxury = RouteEconomics.Evaluate(Inputs(30f), new[] { BusInstance.CreateVirtual(content.Luxury) }, content.Economy);

            Assert.That(luxury.ReferencePrice, Is.GreaterThan(coach.ReferencePrice));
            Assert.That(luxury.ElasticityAtReference, Is.LessThan(coach.ElasticityAtReference));
        }

        [Test]
        public void Minibus_OnLongRoute_LosesDemandToDiscomfort()
        {
            var mini = RouteEconomics.Evaluate(Inputs(20f), new[] { BusInstance.CreateVirtual(content.Minibus) }, content.Economy);
            var coach = RouteEconomics.Evaluate(Inputs(20f), new[] { BusInstance.CreateVirtual(content.Coach) }, content.Economy);
            Assert.That(mini.ReferenceDemand, Is.LessThan(coach.ReferenceDemand));
        }

        [Test]
        public void Quote_SuggestsProfitablePrice_OnTrunkRoute()
        {
            state.level = 5;
            RouteQuote quote = routes.QuoteRoute(content.Istanbul, content.Ankara, content.Coach);

            Assert.That(quote.IsValid, Is.True, quote.Error);
            Assert.That(quote.Forecast.Profit, Is.GreaterThan(0f));
            Assert.That(quote.RecommendedBusCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(quote.Forecast.LoadFactor, Is.InRange(0.5f, 1f));
        }

        [Test]
        public void LockedModel_CannotBePurchased()
        {
            Assert.That(fleet.CanPurchase(content.Coach).Success, Is.False, "Seviye 5 gerekli");
            Assert.That(fleet.CanPurchase(content.Minibus).Success, Is.True);
        }

        [Test]
        public void TradeIn_ReplacesBus_AndKeepsRouteAssignment()
        {
            Assert.That(routes.TryOpenRoute(content.Istanbul, content.Bursa, 8f, out RouteState route).Success, Is.True);
            Assert.That(fleet.TryPurchase(content.Minibus, 1f, out BusInstance oldBus).Success, Is.True);
            routes.TryAssignBus(oldBus.busId, route.routeId);

            state.level = 5;
            state.cash = 1_000_000;
            Assert.That(fleet.TryTradeIn(oldBus.busId, content.Coach, out BusInstance newBus).Success, Is.True);

            Assert.That(fleet.GetBus(oldBus.busId), Is.Null);
            Assert.That(newBus.assignedRouteId, Is.EqualTo(route.routeId));
        }

        [Test]
        public void LevelCurve_IsMonotonic_AndInvertible()
        {
            var config = content.Progression;
            for (int level = 1; level < config.MaxLevel; level++)
            {
                long xp = config.XpRequiredForLevel(level);
                Assert.That(config.XpRequiredForLevel(level + 1), Is.GreaterThan(xp));
                Assert.That(config.LevelForXp(xp), Is.EqualTo(level));
            }
        }

        [Test]
        public void SimulatedDay_MovesMoneyAndExperience()
        {
            routes.TryOpenRoute(content.Istanbul, content.Bursa, 8f, out RouteState route);
            fleet.TryPurchase(content.Minibus, 1f, out BusInstance bus);
            routes.TryAssignBus(bus.busId, route.routeId);
            double cashBefore = finance.Cash;

            var reports = new List<RouteDayReport>();
            routes.SimulateDay(new System.Random(42), reports);
            progression.AddPassengers(reports[0].passengers);

            Assert.That(reports[0].passengers, Is.GreaterThan(0f));
            Assert.That(finance.Cash, Is.Not.EqualTo(cashBefore));
            Assert.That(progression.Experience, Is.GreaterThan(0));
            Assert.That(bus.odometerKm, Is.GreaterThan(0));
        }
    }
}
