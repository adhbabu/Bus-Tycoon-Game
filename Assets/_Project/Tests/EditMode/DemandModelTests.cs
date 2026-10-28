using BusTycoon.Economy;
using BusTycoon.World;
using NUnit.Framework;

namespace BusTycoon.Tests
{
    public sealed class DemandModelTests
    {
        const float RefPrice = 20f;
        const float RefDemand = 300f;
        const float Elasticity = 1.5f;

        [Test]
        public void Haversine_IstanbulToAnkara_IsAbout350Km()
        {
            double km = GeoCoordinate.HaversineKm(new GeoCoordinate(41.0082, 28.9784), new GeoCoordinate(39.9334, 32.8597));
            Assert.That(km, Is.InRange(345.0, 355.0));
        }

        [Test]
        public void Demand_AtReferencePrice_EqualsReferenceDemand()
        {
            Assert.That(DemandModel.DemandAtPrice(RefDemand, RefPrice, RefPrice, Elasticity), Is.EqualTo(RefDemand).Within(0.001f));
        }

        [Test]
        public void Demand_DecreasesMonotonically_AsPriceRises()
        {
            float previous = float.MaxValue;
            for (float price = 1f; price <= 60f; price += 1f)
            {
                float demand = DemandModel.DemandAtPrice(RefDemand, price, RefPrice, Elasticity);
                Assert.That(demand, Is.LessThan(previous));
                previous = demand;
            }
        }

        [Test]
        public void PointElasticity_AtReferencePrice_EqualsConfiguredElasticity()
        {
            // Sayısal türev ile ε = −(dQ/dP)·(P/Q) doğrulaması
            const float h = 0.001f;
            float q = DemandModel.DemandAtPrice(RefDemand, RefPrice, RefPrice, Elasticity);
            float dq = DemandModel.DemandAtPrice(RefDemand, RefPrice + h, RefPrice, Elasticity) - q;
            float numeric = -(dq / h) * (RefPrice / q);

            Assert.That(numeric, Is.EqualTo(Elasticity).Within(0.01f));
            Assert.That(DemandModel.PointElasticity(RefPrice, RefPrice, Elasticity), Is.EqualTo(Elasticity).Within(1e-5f));
        }

        [Test]
        public void Revenue_HasSingleInteriorMaximum_WhereElasticityIsOne()
        {
            float best = PriceOptimizer.FindBestPrice(p => p * DemandModel.DemandAtPrice(RefDemand, p, RefPrice, Elasticity), 1f, 80f);
            Assert.That(best, Is.EqualTo(DemandModel.RevenueMaximizingPrice(RefPrice, Elasticity)).Within(0.05f));
            Assert.That(DemandModel.PointElasticity(best, RefPrice, Elasticity), Is.EqualTo(1f).Within(0.01f));
        }

        [Test]
        public void ProfitMaximizingPrice_MatchesLernerFormula()
        {
            const float marginalCost = 6f;
            float numeric = PriceOptimizer.FindBestPrice(
                p => (p - marginalCost) * DemandModel.DemandAtPrice(RefDemand, p, RefPrice, Elasticity), 1f, 80f);

            Assert.That(numeric, Is.EqualTo(DemandModel.ProfitMaximizingPrice(marginalCost, RefPrice, Elasticity)).Within(0.05f));
        }

        [TestCase(100f, 300f)]
        [TestCase(300f, 300f)]
        [TestCase(600f, 300f)]
        public void CarriedPassengers_NeverExceedsDemandOrCapacity(float demand, float capacity)
        {
            float carried = DemandModel.CarriedPassengers(demand, capacity, 8f);
            Assert.That(carried, Is.LessThanOrEqualTo(demand));
            Assert.That(carried, Is.LessThanOrEqualTo(capacity));
            Assert.That(carried, Is.GreaterThan(0.85f * System.Math.Min(demand, capacity)));
        }
    }
}
