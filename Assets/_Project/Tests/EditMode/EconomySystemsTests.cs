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

        MaintenanceAssistant Assistant() => new MaintenanceAssistant(state, fleet, routes, finance, content.Economy);

        [Test]
        public void Assistant_Off_LeavesWornBusesAlone()
        {
            fleet.TryPurchase(content.Minibus, 1f, out BusInstance bus);
            bus.condition = 0.3f;
            Assert.That(Assistant().RunDaily().IsEmpty, Is.True);
            Assert.That(bus.condition, Is.EqualTo(0.3f));
        }

        [Test]
        public void Assistant_OverhaulsOnlyWornBuses_AndPaysForIt()
        {
            fleet.TryPurchase(content.Minibus, 1f, out BusInstance worn);
            fleet.TryPurchase(content.Minibus, 1f, out BusInstance fresh);
            worn.condition = 0.3f;
            fresh.condition = content.Economy.AutoOverhaulBelowCondition + 0.05f;
            var assistant = Assistant();
            assistant.SetEnabled(true);
            float cost = fleet.GetOverhaulCost(worn);
            double cashBefore = finance.Cash;
            MaintenanceReport notified = default;
            assistant.Completed += report => notified = report;

            MaintenanceReport result = assistant.RunDaily();
            Assert.That(result.Overhauled, Is.EqualTo(1));
            Assert.That(worn.condition, Is.EqualTo(content.Economy.OverhaulTargetCondition));
            Assert.That(fresh.condition, Is.EqualTo(content.Economy.AutoOverhaulBelowCondition + 0.05f));
            Assert.That(finance.Cash, Is.EqualTo(cashBefore - cost).Within(0.01));
            Assert.That(notified.Overhauled, Is.EqualTo(1));
        }

        [Test]
        public void Assistant_KeepsCashReserve_AndWorstBusGoesFirst()
        {
            fleet.TryPurchase(content.Minibus, 1f, out BusInstance a);
            fleet.TryPurchase(content.Minibus, 1f, out BusInstance b);
            a.condition = 0.4f;
            b.condition = 0.2f;
            var assistant = Assistant();
            assistant.SetEnabled(true);
            // Dün 1000 gider: 2 günlük (2000) yedek kalmalı; para yalnız en yıpranmış otobüse yetsin.
            state.financeHistory.Add(new DailyFinanceSummary { day = 0, expenses = 1000 });
            state.cash = 2000 + fleet.GetOverhaulCost(b) + 1;

            MaintenanceReport result = assistant.RunDaily();
            Assert.That(result.Overhauled, Is.EqualTo(1));
            Assert.That(result.Postponed, Is.EqualTo(1));
            Assert.That(b.condition, Is.EqualTo(content.Economy.OverhaulTargetCondition), "En yıpranmış otobüs önce bakıma girer.");
            Assert.That(a.condition, Is.EqualTo(0.4f), "Yedek parayı bozacak revizyon sonraki güne kalır.");
            Assert.That(finance.Cash, Is.GreaterThanOrEqualTo(2000));
        }

        [Test]
        public void Assistant_RepairsBrokenBus_SoItRunsToday()
        {
            routes.TryOpenRoute(content.Istanbul, content.Bursa, 8f, out RouteState route);
            fleet.TryPurchase(content.Minibus, 1f, out BusInstance bus);
            routes.TryAssignBus(bus.busId, route.routeId);
            bus.outOfServiceDays = 3;
            var assistant = Assistant();
            assistant.SetEnabled(true);
            double cashBefore = finance.Cash;

            MaintenanceReport result = assistant.RunDaily();
            Assert.That(result.Repaired, Is.EqualTo(1));
            Assert.That(bus.IsAvailableToday, Is.True);
            Assert.That(bus.assignedRouteId, Is.EqualTo(route.routeId));
            Assert.That(finance.Cash, Is.EqualTo(cashBefore - content.Minibus.Price * content.Economy.ExpressRepairCostFactor).Within(0.01));
        }

        [Test]
        public void RepairAll_OverhaulsEverything_AndSendsGarageBusesBackToTheirRoutes()
        {
            routes.TryOpenRoute(content.Istanbul, content.Bursa, 8f, out RouteState route);
            fleet.TryPurchase(content.Minibus, 1f, out BusInstance bus);
            routes.TryAssignBus(bus.busId, route.routeId);
            bus.condition = 0.7f; // eşiğin üstünde: günlük çalışma dokunmaz, "hepsini onar" onarır
            routes.TryAssignBus(bus.busId, null);
            Assert.That(bus.lastRouteId, Is.EqualTo(route.routeId));

            var assistant = Assistant();
            Assert.That(assistant.PendingWork(out double cost), Is.EqualTo(2));
            Assert.That(cost, Is.GreaterThan(0));
            MaintenanceReport result = assistant.RunNow();
            Assert.That(result.Overhauled, Is.EqualTo(1));
            Assert.That(result.Reassigned, Is.EqualTo(1));
            Assert.That(bus.assignedRouteId, Is.EqualTo(route.routeId));
            Assert.That(bus.condition, Is.EqualTo(content.Economy.OverhaulTargetCondition));
        }

        [Test]
        public void Wear_IsScaledByWearRate()
        {
            fleet.TryPurchase(content.Minibus, 1f, out BusInstance bus);
            fleet.ApplyWear(bus, 100_000);
            float expected = 1f - 100_000f * content.Economy.WearRate / content.Minibus.LifetimeKm;
            Assert.That(bus.condition, Is.EqualTo(expected).Within(1e-4));
            Assert.That(content.Economy.WearRate, Is.LessThan(1f), "Oyun günü kısa: otobüs gerçek ömründen yavaş yıpranmalı.");
        }
    }
}
