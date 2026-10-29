using System;
using System.Collections.Generic;
using BusTycoon.Core;
using BusTycoon.Data;
using BusTycoon.Economy;
using BusTycoon.Fleet;
using BusTycoon.Progression;
using BusTycoon.Save;
using BusTycoon.Stations;
using BusTycoon.World;
using UnityEngine;

namespace BusTycoon.Routes
{
    /// <summary>Hat açmadan önce oyuncuya gösterilen teklif kartının verisi.</summary>
    public struct RouteQuote
    {
        public bool IsValid;
        public string Error;
        public float DistanceKm;
        public float TravelHours;
        public float LicenseCost;
        public BusModelData PlannedModel;
        public int RecommendedBusCount;
        public float SuggestedPrice;
        public RouteForecast Forecast;
    }

    /// <summary>
    /// Hat açma/kapama, fiyat ve sefer ayarları, otobüs atama ve günlük hat simülasyonu.
    /// UI yalnızca bu sınıfın public API'sini çağırır; hiçbir zaman <see cref="RouteState"/>'i doğrudan değiştirmez.
    /// </summary>
    public sealed class RouteSystem
    {
        readonly CompanyState state;
        readonly WorldGraph world;
        readonly EconomyConfig economy;
        readonly FleetSystem fleet;
        readonly FinanceSystem finance;
        readonly ProgressionSystem progression;
        readonly StationSystem stations;

        readonly List<BusInstance> busBuffer = new List<BusInstance>();
        readonly List<BusInstance> virtualBuses = new List<BusInstance>();

        public event Action<RouteState> RouteOpened;
        public event Action<RouteState> RouteClosed;
        public event Action<RouteState> RouteChanged;

        public RouteSystem(CompanyState state, WorldGraph world, EconomyConfig economy,
            FleetSystem fleet, FinanceSystem finance, ProgressionSystem progression, StationSystem stations = null)
        {
            this.state = state;
            this.world = world;
            this.economy = economy;
            this.fleet = fleet;
            this.finance = finance;
            this.progression = progression;
            this.stations = stations;
        }

        public IReadOnlyList<RouteState> Routes => state.routes;

        public RouteState GetRoute(string routeId) => state.routes.Find(r => r.routeId == routeId);

        public RouteState FindRoute(CityData a, CityData b) => state.routes.Find(r =>
            (r.cityAId == a.Id && r.cityBId == b.Id) || (r.cityAId == b.Id && r.cityBId == a.Id));

        public CityData GetCityA(RouteState route) => world.GetCity(route.cityAId);
        public CityData GetCityB(RouteState route) => world.GetCity(route.cityBId);

        // ------------------------------------------------------------------ hat açma

        public OpResult CanOpenRoute(CityData a, CityData b)
        {
            if (!world.CanConnect(a, b, out string reason)) return OpResult.Fail(reason);
            if (FindRoute(a, b) != null) return OpResult.Fail("Bu hat zaten işletiliyor.");
            if (state.routes.Count >= progression.MaxRoutes)
                return OpResult.Fail($"Hat limiti dolu ({progression.MaxRoutes}). Şirket seviyesini yükseltin.");
            if (!progression.IsRegionUnlocked(a.Region))
                return OpResult.Fail($"{a.Region.DisplayName} için işletme lisansınız yok.");
            if (!progression.IsRegionUnlocked(b.Region))
                return OpResult.Fail($"{b.Region.DisplayName} için işletme lisansınız yok.");

            float license = economy.GetRouteLicenseCost(world.GetRoadDistanceKm(a, b));
            if (!finance.CanAfford(license)) return OpResult.Fail($"Hat lisansı için {license:N0} gerekli.");
            return OpResult.Ok();
        }

        /// <summary>
        /// Teklif kartı: mesafe, süre, lisans bedeli, önerilen otobüs sayısı ve başlangıç fiyatı.
        /// Fiyat ↔ kapasite birbirine bağlı olduğundan önce kapasitenin kısıt olmadığı durumda en iyi fiyat
        /// bulunur, sonra o fiyattaki talebi karşılayacak otobüs sayısı hesaplanır.
        /// </summary>
        public RouteQuote QuoteRoute(CityData a, CityData b, BusModelData plannedModel)
        {
            var quote = new RouteQuote { PlannedModel = plannedModel };
            OpResult check = CanOpenRoute(a, b);
            quote.IsValid = check.Success;
            quote.Error = check.Message;
            if (a == null || b == null || a == b || plannedModel == null) return quote;

            quote.DistanceKm = world.GetRoadDistanceKm(a, b);
            quote.TravelHours = plannedModel.TravelHours(quote.DistanceKm);
            quote.LicenseCost = economy.GetRouteLicenseCost(quote.DistanceKm);

            float bias = ElasticityBiasFor(a.Id, b.Id);
            float busSeatsAtTarget = plannedModel.Seats * economy.AutoScheduleTargetLoadFactor
                                     * RouteEconomics.MaxTripsPerDay(plannedModel, quote.DistanceKm, economy);

            // Talep 30. gün olgunluğunda değerlendirilir: açılış günündeki düşük talep oyuncuyu yanıltmasın.
            const int matureDays = 30;
            RouteEconomics.Inputs inputs = BuildInputs(a, b, quote.DistanceKm, 0f, matureDays, true, 1f, bias);

            // 1) En düşük fiyattaki talebi bile taşıyabilecek sanal filo → kapasite kısıt olmaz.
            //    Otomatik sefer planında fazladan otobüs yalnızca sabit sigorta maliyetidir; en iyi fiyatı değiştirmez.
            FillVirtualFleet(plannedModel, 1);
            float referencePrice = RouteEconomics.Evaluate(inputs, virtualBuses, economy).ReferencePrice;
            inputs.TicketPrice = referencePrice * economy.MinPriceFactor;
            float peakDemand = RouteEconomics.Evaluate(inputs, virtualBuses, economy).Demand;
            FillVirtualFleet(plannedModel, Mathf.CeilToInt(peakDemand / busSeatsAtTarget) + 1);

            // 2) Kısıtsız en iyi fiyat, 3) o fiyattaki talebi karşılayan otobüs sayısı.
            quote.SuggestedPrice = FindBestPrice(inputs, virtualBuses);
            inputs.TicketPrice = quote.SuggestedPrice;
            float demand = RouteEconomics.Evaluate(inputs, virtualBuses, economy).Demand;
            int busCount = Mathf.Max(1, Mathf.CeilToInt(demand / busSeatsAtTarget));

            FillVirtualFleet(plannedModel, busCount);
            quote.RecommendedBusCount = busCount;
            quote.Forecast = RouteEconomics.Evaluate(
                BuildInputs(a, b, quote.DistanceKm, quote.SuggestedPrice, matureDays, true, 1f, bias), virtualBuses, economy);
            return quote;
        }

        public OpResult TryOpenRoute(CityData a, CityData b, float initialPrice, out RouteState route)
        {
            route = null;
            OpResult check = CanOpenRoute(a, b);
            if (!check) return check;

            float km = world.GetRoadDistanceKm(a, b);
            finance.TrySpend(economy.GetRouteLicenseCost(km), LedgerCategory.RouteLicense, a.Id + "-" + b.Id);

            state.nextRouteSerial++;
            route = new RouteState
            {
                routeId = $"R{state.nextRouteSerial:000}",
                cityAId = a.Id,
                cityBId = b.Id,
                distanceKm = km,
                ticketPrice = Mathf.Max(0.01f, initialPrice),
                openedDay = state.currentDay
            };
            state.routes.Add(route);
            RouteOpened?.Invoke(route);
            return OpResult.Ok($"{a.DisplayName} – {b.DisplayName} hattı açıldı.");
        }

        /// <summary>Hattı kapatır; otobüsler garaja döner. Lisans bedeli iade edilmez.</summary>
        public OpResult TryCloseRoute(string routeId)
        {
            RouteState route = GetRoute(routeId);
            if (route == null) return OpResult.Fail("Hat bulunamadı.");

            fleet.GetBusesOnRoute(routeId, busBuffer);
            foreach (var bus in busBuffer) fleet.SetAssignment(bus, null);

            state.routes.Remove(route);
            RouteClosed?.Invoke(route);
            return OpResult.Ok("Hat kapatıldı, otobüsler garaja döndü.");
        }

        // ------------------------------------------------------------------ ayarlar

        public OpResult SetTicketPrice(string routeId, float price)
        {
            RouteState route = GetRoute(routeId);
            if (route == null) return OpResult.Fail("Hat bulunamadı.");
            if (price <= 0f) return OpResult.Fail("Bilet fiyatı sıfırdan büyük olmalı.");

            route.ticketPrice = price;
            RouteChanged?.Invoke(route);
            return OpResult.Ok();
        }

        public OpResult SetSchedule(string routeId, bool auto, float manualUtilization)
        {
            RouteState route = GetRoute(routeId);
            if (route == null) return OpResult.Fail("Hat bulunamadı.");

            route.autoSchedule = auto;
            route.manualUtilization = Mathf.Clamp01(manualUtilization);
            RouteChanged?.Invoke(route);
            return OpResult.Ok();
        }

        /// <summary>routeId null ise otobüs garaja çekilir.</summary>
        public OpResult TryAssignBus(string busId, string routeId)
        {
            BusInstance bus = fleet.GetBus(busId);
            if (bus == null) return OpResult.Fail("Otobüs bulunamadı.");

            RouteState route = null;
            if (!string.IsNullOrEmpty(routeId))
            {
                route = GetRoute(routeId);
                if (route == null) return OpResult.Fail("Hat bulunamadı.");
            }

            RouteState previous = bus.IsAssigned ? GetRoute(bus.assignedRouteId) : null;
            fleet.SetAssignment(bus, routeId);
            if (previous != null) RouteChanged?.Invoke(previous);
            if (route != null) RouteChanged?.Invoke(route);
            return OpResult.Ok();
        }

        // ------------------------------------------------------------------ tahmin (UI)

        /// <summary>Bugünkü koşullarla tam doğru tahmin (yalnızca sistem içi / debug).</summary>
        public RouteForecast Forecast(RouteState route, float? priceOverride = null)
        {
            fleet.GetBusesOnRoute(route.routeId, busBuffer);
            return RouteEconomics.Evaluate(BuildInputs(route, priceOverride ?? route.ticketPrice, 1f, 1f, true), busBuffer, economy);
        }

        /// <summary>
        /// Oyuncunun pazar araştırması seviyesine göre YANLI tahmin fonksiyonu. Fiyat paneli bunu kullanır.
        /// Yanılgı hat kimliğinden deterministik türetilir; böylece kaydırıcı oynatılırken titremez.
        /// </summary>
        public Func<float, RouteForecast> CreateEstimator(RouteState route)
        {
            var buses = new List<BusInstance>();
            fleet.GetBusesOnRoute(route.routeId, buses);
            float bias = ElasticityBiasFor(route.cityAId, route.cityBId);
            RouteEconomics.Inputs template = BuildInputs(route, route.ticketPrice, 1f, bias, true);

            return price =>
            {
                RouteEconomics.Inputs inputs = template;
                inputs.TicketPrice = price;
                return RouteEconomics.Evaluate(inputs, buses, economy);
            };
        }

        public void AnalyzePricing(RouteState route, int curveSamples, PricingAnalysis result)
        {
            Func<float, RouteForecast> estimator = CreateEstimator(route);
            float referencePrice = estimator(route.ticketPrice).ReferencePrice;
            PricingAdvisor.Analyze(estimator, referencePrice, progression.PriceEstimateError, economy, curveSamples, result);
        }

        // ------------------------------------------------------------------ günlük simülasyon

        public void SimulateDay(System.Random rng, List<RouteDayReport> reportsOut)
        {
            reportsOut.Clear();
            foreach (var route in state.routes)
                reportsOut.Add(SimulateRoute(route, rng));
        }

        RouteDayReport SimulateRoute(RouteState route, System.Random rng)
        {
            fleet.GetBusesOnRoute(route.routeId, busBuffer);
            var report = new RouteDayReport { day = state.currentDay, routeId = route.routeId, ticketPrice = route.ticketPrice };

            if (busBuffer.Count == 0)
            {
                route.AddReport(report);
                return report;
            }

            float noise = 1f + (float)(rng.NextDouble() * 2.0 - 1.0) * economy.DailyDemandNoise;
            RouteForecast f = RouteEconomics.Evaluate(BuildInputs(route, route.ticketPrice, noise, 1f, false), busBuffer, economy);

            // Yıpranma: her çalışan otobüs, kullanım oranı kadar sefer yapar.
            foreach (var bus in busBuffer)
            {
                if (!bus.IsAvailableToday || bus.Model == null) continue;
                float trips = RouteEconomics.MaxTripsPerDay(bus.Model, route.distanceKm, economy) * f.Utilization;
                fleet.ApplyWear(bus, trips * route.distanceKm);
            }

            string id = route.routeId;
            finance.Earn(f.Revenue, LedgerCategory.TicketRevenue, id);
            finance.Charge(f.FuelCost, LedgerCategory.Fuel, id);
            finance.Charge(f.DriverCost, LedgerCategory.DriverWages, id);
            finance.Charge(f.MaintenanceCost, LedgerCategory.Maintenance, id);
            finance.Charge(f.TollCost, LedgerCategory.Tolls, id);
            finance.Charge(f.TerminalCost, LedgerCategory.TerminalFees, id);
            finance.Charge(f.ServiceCost, LedgerCategory.PassengerService, id);
            finance.Charge(f.InsuranceCost, LedgerCategory.Insurance, id);

            report.demand = f.Demand;
            report.passengers = f.Passengers;
            report.loadFactor = f.LoadFactor;
            report.unmetDemand = f.UnmetDemand;
            report.tripsPerDay = f.TripsPerDay;
            report.revenue = f.Revenue;
            report.cost = f.TotalCost;
            report.satisfaction = f.Satisfaction;
            report.activeBuses = f.AvailableBuses;
            route.AddReport(report);
            return report;
        }

        // ------------------------------------------------------------------ yardımcılar

        RouteEconomics.Inputs BuildInputs(RouteState route, float price, float noise, float elasticityBias, bool includeUnavailable)
        {
            RouteEconomics.Inputs inputs = BuildInputs(GetCityA(route), GetCityB(route), route.distanceKm, price,
                state.currentDay - route.openedDay, route.autoSchedule, route.manualUtilization, elasticityBias);
            inputs.DemandNoise = noise;
            inputs.IncludeUnavailableBuses = includeUnavailable;
            return inputs;
        }

        RouteEconomics.Inputs BuildInputs(CityData a, CityData b, float km, float price, int daysOpen,
            bool autoSchedule, float manualUtilization, float elasticityBias) => new RouteEconomics.Inputs
        {
            CityA = a,
            CityB = b,
            DistanceKm = km,
            TicketPrice = price,
            CompanyReputation = state.reputation,
            DaysOpen = daysOpen,
            AutoSchedule = autoSchedule,
            ManualUtilization = manualUtilization,
            DemandNoise = 1f,
            ElasticityBias = elasticityBias,
            IncludeUnavailableBuses = true,
            StationFactor = stations != null ? stations.RouteDemandFactor(a, b) : 1f
        };

        float FindBestPrice(RouteEconomics.Inputs template, IReadOnlyList<BusInstance> buses)
        {
            RouteForecast reference = RouteEconomics.Evaluate(template, buses, economy);
            return PriceOptimizer.FindBestPrice(price =>
            {
                RouteEconomics.Inputs inputs = template;
                inputs.TicketPrice = price;
                return RouteEconomics.Evaluate(inputs, buses, economy).Profit;
            }, reference.ReferencePrice * economy.MinPriceFactor, reference.ReferencePrice * economy.MaxPriceFactor);
        }

        void FillVirtualFleet(BusModelData model, int count)
        {
            virtualBuses.Clear();
            for (int i = 0; i < count; i++) virtualBuses.Add(BusInstance.CreateVirtual(model));
        }

        /// <summary>
        /// Pazar araştırması yanılgısı: esneklik tahmini (1 ± hata) ile çarpılır.
        /// Yön ve büyüklük şehir çiftinden (FNV-1a hash) deterministik türetilir; böylece teklif kartı
        /// ile hat açıldıktan sonraki fiyat paneli aynı yanılgıyı gösterir.
        /// </summary>
        float ElasticityBiasFor(string cityAId, string cityBId)
        {
            float error = progression.PriceEstimateError;
            if (error <= 0f) return 1f;

            string key = string.CompareOrdinal(cityAId, cityBId) <= 0 ? cityAId + "|" + cityBId : cityBId + "|" + cityAId;
            uint hash = 2166136261;
            foreach (char c in key) hash = (hash ^ c) * 16777619;
            float signed = (hash % 2001) / 1000f - 1f; // [-1, 1]
            return 1f + error * signed;
        }
    }
}
