using System;
using System.Collections.Generic;
using BusTycoon.Core;
using BusTycoon.Data;
using BusTycoon.Economy;
using BusTycoon.Progression;
using BusTycoon.Save;
using UnityEngine;

namespace BusTycoon.Fleet
{
    /// <summary>
    /// Filo yönetimi: satın alma (sıfır / 2. el), satış, takasla yükseltme, hatta atama,
    /// yıpranma, arıza ve revizyon. Satın alma kilitleri <see cref="ProgressionSystem"/>'den sorulur.
    /// </summary>
    public sealed class FleetSystem
    {
        readonly CompanyState state;
        readonly BusCatalog catalog;
        readonly EconomyConfig economy;
        readonly FinanceSystem finance;
        readonly ProgressionSystem progression;

        public event Action<BusInstance> BusAdded;
        public event Action<BusInstance> BusRemoved;
        public event Action<BusInstance> BusChanged;
        public event Action<BusInstance> BusBrokeDown;

        public FleetSystem(CompanyState state, BusCatalog catalog, EconomyConfig economy,
            FinanceSystem finance, ProgressionSystem progression)
        {
            this.state = state;
            this.catalog = catalog;
            this.economy = economy;
            this.finance = finance;
            this.progression = progression;
        }

        public IReadOnlyList<BusInstance> Buses => state.buses;
        public BusCatalog Catalog => catalog;

        /// <summary>Kayıt yüklendikten sonra modelId → BusModelData referanslarını bağlar.</summary>
        public void ResolveModels()
        {
            foreach (var bus in state.buses)
            {
                bus.Model = catalog.GetById(bus.modelId);
                if (bus.Model == null) Debug.LogWarning($"[Fleet] '{bus.busId}' için model bulunamadı: {bus.modelId}");
            }
        }

        public BusInstance GetBus(string busId) => state.buses.Find(b => b.busId == busId);

        public void GetBusesOnRoute(string routeId, List<BusInstance> results)
        {
            results.Clear();
            foreach (var bus in state.buses)
                if (bus.assignedRouteId == routeId) results.Add(bus);
        }

        public int CountBusesOnRoute(string routeId)
        {
            int count = 0;
            foreach (var bus in state.buses)
                if (bus.assignedRouteId == routeId) count++;
            return count;
        }

        public void GetIdleBuses(List<BusInstance> results)
        {
            results.Clear();
            foreach (var bus in state.buses)
                if (!bus.IsAssigned) results.Add(bus);
        }

        // ------------------------------------------------------------------ fiyatlama

        /// <summary>Satın alma fiyatı. condition &lt; 1 ise 2. el: fiyat = liste × (taban + (1 − taban) × durum).</summary>
        public float GetPurchasePrice(BusModelData model, float condition = 1f) =>
            model.Price * ConditionValueFactor(condition);

        /// <summary>Galericinin bugün vereceği bedel: liste × yaş değer kaybı × durum × galerici marjı.</summary>
        public float GetResaleValue(BusInstance bus)
        {
            if (bus?.Model == null) return 0f;
            float years = Mathf.Max(0, state.currentDay - bus.purchaseDay) / (float)SimulationClock.DaysPerYear;
            float ageFactor = Mathf.Pow(1f - economy.AnnualDepreciation, years);
            return bus.Model.Price * ageFactor * ConditionValueFactor(bus.condition) * economy.ResaleRatio;
        }

        public float GetOverhaulCost(BusInstance bus)
        {
            if (bus?.Model == null) return 0f;
            float restored = Mathf.Max(0f, economy.OverhaulTargetCondition - bus.condition);
            return bus.Model.Price * economy.OverhaulCostFactor * restored;
        }

        float ConditionValueFactor(float condition) =>
            economy.UsedPriceFloor + (1f - economy.UsedPriceFloor) * Mathf.Clamp01(condition);

        // ------------------------------------------------------------------ satın alma / satış

        public OpResult CanPurchase(BusModelData model, float condition = 1f)
        {
            if (model == null) return OpResult.Fail("Geçersiz model.");
            if (!progression.IsModelUnlocked(model)) return OpResult.Fail(progression.GetLockReason(model));

            float price = GetPurchasePrice(model, condition);
            if (!finance.CanAfford(price)) return OpResult.Fail($"Yetersiz bakiye: {price:N0} gerekli.");
            return OpResult.Ok();
        }

        public OpResult TryPurchase(BusModelData model, float condition, out BusInstance bus)
        {
            bus = null;
            OpResult check = CanPurchase(model, condition);
            if (!check) return check;

            float price = GetPurchasePrice(model, condition);
            finance.TrySpend(price, LedgerCategory.BusPurchase, model.Id);
            bus = AddBus(model, condition, price);
            return OpResult.Ok($"{bus.nickname} filoya katıldı.");
        }

        /// <summary>Oyun başlangıcındaki hediye otobüs gibi ücretsiz eklemeler için.</summary>
        internal BusInstance GrantBus(BusModelData model, float condition) => AddBus(model, condition, 0f);

        BusInstance AddBus(BusModelData model, float condition, float paidPrice)
        {
            state.nextBusSerial++;
            var bus = new BusInstance
            {
                busId = $"BUS-{state.nextBusSerial:0000}",
                modelId = model.Id,
                Model = model,
                nickname = $"{model.DisplayName} #{state.nextBusSerial}",
                purchaseDay = state.currentDay,
                purchasePrice = paidPrice,
                condition = Mathf.Clamp01(condition)
            };
            state.buses.Add(bus);
            BusAdded?.Invoke(bus);
            return bus;
        }

        public OpResult TrySell(string busId)
        {
            BusInstance bus = GetBus(busId);
            if (bus == null) return OpResult.Fail("Otobüs bulunamadı.");

            float value = GetResaleValue(bus);
            state.buses.Remove(bus);
            finance.Earn(value, LedgerCategory.BusSale, bus.busId);
            BusRemoved?.Invoke(bus);
            return OpResult.Ok($"{bus.nickname} {value:N0} karşılığında satıldı.");
        }

        /// <summary>
        /// Takasla yükseltme (progression'ın ana eylemi): eski otobüs satılır, yenisi alınır ve
        /// AYNI hatta otomatik atanır. Bakiye kontrolü net fark üzerinden yapılır.
        /// </summary>
        public OpResult TryTradeIn(string oldBusId, BusModelData newModel, out BusInstance newBus)
        {
            newBus = null;
            BusInstance oldBus = GetBus(oldBusId);
            if (oldBus == null) return OpResult.Fail("Otobüs bulunamadı.");
            if (newModel == null) return OpResult.Fail("Geçersiz model.");
            if (!progression.IsModelUnlocked(newModel)) return OpResult.Fail(progression.GetLockReason(newModel));

            float tradeInValue = GetResaleValue(oldBus);
            float price = GetPurchasePrice(newModel);
            float netCost = price - tradeInValue;
            if (!finance.CanAfford(netCost)) return OpResult.Fail($"Takas için {netCost:N0} ek bakiye gerekli.");

            string routeId = oldBus.assignedRouteId;
            state.buses.Remove(oldBus);
            finance.Earn(tradeInValue, LedgerCategory.BusSale, oldBus.busId);
            BusRemoved?.Invoke(oldBus);

            finance.TrySpend(price, LedgerCategory.BusPurchase, newModel.Id);
            newBus = AddBus(newModel, 1f, price);
            if (!string.IsNullOrEmpty(routeId)) SetAssignment(newBus, routeId);
            return OpResult.Ok($"{oldBus.nickname} → {newBus.nickname} (net {netCost:N0}).");
        }

        // ------------------------------------------------------------------ atama

        /// <summary>
        /// Hat varlığı kontrolü <c>RouteSystem.TryAssignBus</c> içinde yapılır; buradan doğrudan
        /// çağrılmamalıdır (bu yüzden internal).
        /// </summary>
        internal void SetAssignment(BusInstance bus, string routeId)
        {
            if (bus.assignedRouteId == routeId) return;
            bus.assignedRouteId = routeId;
            BusChanged?.Invoke(bus);
        }

        // ------------------------------------------------------------------ günlük döngü

        /// <summary>Gün başında: arıza sayaçlarını düşür, hatta çalışan otobüsler için arıza zarı at.</summary>
        public void BeginDay(System.Random rng)
        {
            foreach (var bus in state.buses)
            {
                if (bus.outOfServiceDays > 0) bus.outOfServiceDays--;
                if (!bus.IsAssigned || !bus.IsAvailableToday || bus.Model == null) continue;

                float wornness = 1f - bus.condition;
                float chance = economy.BreakdownChanceAtZeroCondition * wornness * wornness;
                if (rng.NextDouble() >= chance) continue;

                bus.outOfServiceDays = rng.Next(1, economy.MaxBreakdownDays + 1);
                finance.Charge(bus.Model.Price * economy.RepairCostFactor, LedgerCategory.Repairs, bus.busId);
                BusBrokeDown?.Invoke(bus);
            }
        }

        public void ApplyWear(BusInstance bus, double km)
        {
            if (bus?.Model == null || km <= 0) return;
            bus.odometerKm += km;
            bus.condition = Mathf.Max(0.05f, bus.condition - (float)(km / bus.Model.LifetimeKm));
        }

        /// <summary>Garajdaki (atanmamış) otobüslerin sigortası. Hattakilerin sigortası hat maliyetine dahildir.</summary>
        public void ChargeIdleInsurance()
        {
            foreach (var bus in state.buses)
                if (!bus.IsAssigned && bus.Model != null)
                    finance.Charge(bus.Model.InsurancePerDay, LedgerCategory.Insurance, bus.busId);
        }

        public OpResult TryOverhaul(string busId)
        {
            BusInstance bus = GetBus(busId);
            if (bus == null) return OpResult.Fail("Otobüs bulunamadı.");
            if (bus.condition >= economy.OverhaulTargetCondition) return OpResult.Fail("Otobüs zaten iyi durumda.");

            float cost = GetOverhaulCost(bus);
            if (!finance.TrySpend(cost, LedgerCategory.Repairs, bus.busId))
                return OpResult.Fail($"Revizyon için {cost:N0} gerekli.");

            bus.condition = economy.OverhaulTargetCondition;
            BusChanged?.Invoke(bus);
            return OpResult.Ok($"{bus.nickname} revizyondan çıktı.");
        }
    }
}
