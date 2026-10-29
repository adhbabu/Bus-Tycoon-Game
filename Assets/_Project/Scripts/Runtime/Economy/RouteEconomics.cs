using System.Collections.Generic;
using BusTycoon.Data;
using BusTycoon.Fleet;
using UnityEngine;

namespace BusTycoon.Economy
{
    /// <summary>Bir hattın belirli bir bilet fiyatıyla bir günlük tam ekonomik dökümü.</summary>
    public struct RouteForecast
    {
        // Talep
        public float TicketPrice;
        public float ReferencePrice;
        public float ElasticityAtReference;
        public float ElasticityAtPrice;
        public float ReferenceDemand;
        public float Demand;

        // Kapasite ve sefer planı
        public int AvailableBuses;
        public float MaxDailySeats;
        public float Utilization;
        public float DailySeats;
        public float TripsPerDay;
        public float Passengers;
        public float LoadFactor;
        public float UnmetDemand;

        // Gelir / gider
        public float Revenue;
        public float FuelCost;
        public float DriverCost;
        public float MaintenanceCost;
        public float TollCost;
        public float TerminalCost;
        public float ServiceCost;
        public float InsuranceCost;

        public float Satisfaction;

        public float OperatingCost => FuelCost + DriverCost + MaintenanceCost + TollCost + TerminalCost + ServiceCost;
        public float TotalCost => OperatingCost + InsuranceCost;
        public float Profit => Revenue - TotalCost;
        public float ProfitMargin => Revenue > 0f ? Profit / Revenue : 0f;
        public float UnmetRatio => Demand > 0f ? UnmetDemand / Demand : 0f;
    }

    /// <summary>
    /// Hat ekonomisinin kalbi. Hem günlük simülasyon hem de UI'daki canlı fiyat önizlemesi
    /// AYNI fonksiyonu çağırır; böylece oyuncunun gördüğü tahmin ile gerçekleşen sonuç tutarlıdır.
    ///
    /// Akış: filo karışımı → maksimum kapasite → talep (gravity × itibar × olgunluk × konfor × durak × esneklik)
    ///       → sefer planı (kullanım oranı) → taşınan yolcu → gelir/gider → memnuniyet
    /// </summary>
    public static class RouteEconomics
    {
        public struct Inputs
        {
            public CityData CityA;
            public CityData CityB;
            public float DistanceKm;
            public float TicketPrice;
            public float CompanyReputation;
            public int DaysOpen;
            public bool AutoSchedule;
            public float ManualUtilization;
            /// <summary>Günlük rastgele talep çarpanı. Tahminlerde 1.</summary>
            public float DemandNoise;
            /// <summary>Esneklik tahmin yanılgısı (pazar araştırması). Gerçek simülasyonda 1.</summary>
            public float ElasticityBias;
            /// <summary>true: arızalı otobüsler de çalışıyor varsayılır (gelecek günler için tahmin).</summary>
            public bool IncludeUnavailableBuses;
            /// <summary>Durak geliştirmelerinin talep çarpanı (StationSystem.RouteDemandFactor). 0 = etkisiz (1).</summary>
            public float StationFactor;
        }

        /// <summary>Tek yön seferin saat cinsinden çevrim süresi: sürüş + indirme/bindirme.</summary>
        public static float CycleHours(BusModelData model, float roadKm, EconomyConfig cfg) =>
            model.TravelHours(roadKm) + cfg.TurnaroundHours;

        /// <summary>Bir otobüsün günde yapabileceği maksimum TEK YÖN sefer (kesirli, günlük ortalama).</summary>
        public static float MaxTripsPerDay(BusModelData model, float roadKm, EconomyConfig cfg) =>
            cfg.OperatingHoursPerDay / Mathf.Max(0.01f, CycleHours(model, roadKm, cfg));

        public static int DriversPerTrip(BusModelData model, float roadKm, EconomyConfig cfg) =>
            Mathf.Max(1, Mathf.CeilToInt(model.TravelHours(roadKm) / cfg.MaxDrivingHoursPerDriver));

        public static RouteForecast Evaluate(in Inputs input, IReadOnlyList<BusInstance> buses, EconomyConfig cfg)
        {
            var f = new RouteForecast { TicketPrice = Mathf.Max(0f, input.TicketPrice) };
            float km = input.DistanceKm;

            RegionData regionA = input.CityA.Region;
            RegionData regionB = input.CityB.Region;
            float fareRegion = 0.5f * ((regionA ? regionA.FareMultiplier : 1f) + (regionB ? regionB.FareMultiplier : 1f));
            float fuelRegion = 0.5f * ((regionA ? regionA.FuelPriceMultiplier : 1f) + (regionB ? regionB.FuelPriceMultiplier : 1f));
            float wageRegion = 0.5f * ((regionA ? regionA.WageMultiplier : 1f) + (regionB ? regionB.WageMultiplier : 1f));
            // Seferler iki uç arasında dönüşümlü kalktığı için ortalama otogar ücreti.
            float terminalFee = 0.5f * (input.CityA.GetTerminalFee(cfg) + input.CityB.GetTerminalFee(cfg));

            // ---------- 1) Filo karışımı ve tam kullanımda kapasite / maliyet ----------
            float maxSeats = 0f, maxTrips = 0f;
            float fareMix = 0f, elasticityMix = 0f, comfortMix = 0f, rangeMix = 0f, serviceMix = 0f;
            float fuelMax = 0f, driverMax = 0f, maintenanceMax = 0f, tollMax = 0f, terminalMax = 0f;

            if (buses != null)
            {
                for (int i = 0; i < buses.Count; i++)
                {
                    BusInstance bus = buses[i];
                    BusModelData m = bus?.Model;
                    if (m == null) continue;

                    f.InsuranceCost += m.InsurancePerDay; // arızalı da olsa sigorta ödenir
                    if (!input.IncludeUnavailableBuses && !bus.IsAvailableToday) continue;

                    f.AvailableBuses++;
                    float trips = MaxTripsPerDay(m, km, cfg);
                    float seats = m.Seats * trips;
                    maxTrips += trips;
                    maxSeats += seats;

                    // Koltuk-ağırlıklı karışım: 1 VIP + 3 minibüslük hatta yolcu ağırlıklı olarak minibüs deneyimi yaşar.
                    fareMix += m.FareMultiplier * seats;
                    elasticityMix += m.ElasticityMultiplier * seats;
                    comfortMix += m.Comfort * seats;
                    rangeMix += m.ComfortableRangeKm * seats;
                    serviceMix += m.ServiceCostPerPassenger * seats;

                    float wear = 1f + (1f - bus.condition) * cfg.WornMaintenancePenalty;
                    fuelMax += trips * km * m.FuelLitersPer100Km / 100f * cfg.FuelPricePerLiter * fuelRegion;
                    driverMax += trips * DriversPerTrip(m, km, cfg) * CycleHours(m, km, cfg) * cfg.DriverWagePerHour * wageRegion;
                    maintenanceMax += trips * km * m.MaintenanceCostPerKm * wear;
                    tollMax += trips * km * cfg.TollPerKm;
                    terminalMax += trips * terminalFee;
                }
            }

            if (maxSeats > 0f)
            {
                fareMix /= maxSeats;
                elasticityMix /= maxSeats;
                comfortMix /= maxSeats;
                rangeMix /= maxSeats;
                serviceMix /= maxSeats;
            }
            else
            {
                fareMix = 1f;
                elasticityMix = 1f;
                comfortMix = 0.5f;
                rangeMix = 0f; // konfor cezası uygulanmaz
            }

            // ---------- 2) Talep ----------
            f.ReferencePrice = DemandModel.ReferenceFare(km, fareRegion * fareMix, cfg);
            f.ElasticityAtReference = cfg.BaseElasticity * elasticityMix * Mathf.Max(0.05f, input.ElasticityBias);

            f.ReferenceDemand = DemandModel.PotentialDemand(input.CityA, input.CityB, km, cfg)
                                * DemandModel.ReputationFactor(input.CompanyReputation, cfg)
                                * DemandModel.RampUpFactor(input.DaysOpen, cfg)
                                * DemandModel.ComfortRangeFactor(km, rangeMix, cfg)
                                * (input.StationFactor > 0f ? input.StationFactor : 1f);

            float noise = input.DemandNoise > 0f ? input.DemandNoise : 1f;
            f.Demand = DemandModel.DemandAtPrice(f.ReferenceDemand, f.TicketPrice, f.ReferencePrice, f.ElasticityAtReference) * noise;
            f.ElasticityAtPrice = DemandModel.PointElasticity(f.TicketPrice, f.ReferencePrice, f.ElasticityAtReference);

            // ---------- 3) Sefer planı ----------
            f.MaxDailySeats = maxSeats;
            if (maxSeats > 0f)
            {
                float minUtilization = Mathf.Min(1f, cfg.MinTripsPerDay / maxTrips);
                float utilization = input.AutoSchedule
                    ? f.Demand / (cfg.AutoScheduleTargetLoadFactor * maxSeats)
                    : input.ManualUtilization;
                f.Utilization = Mathf.Clamp(utilization, minUtilization, 1f);
            }

            f.DailySeats = maxSeats * f.Utilization;
            f.TripsPerDay = maxTrips * f.Utilization;
            f.Passengers = DemandModel.CarriedPassengers(f.Demand, f.DailySeats, cfg.CapacitySmoothness);
            f.LoadFactor = f.DailySeats > 0f ? f.Passengers / f.DailySeats : 0f;
            f.UnmetDemand = Mathf.Max(0f, f.Demand - f.Passengers);

            // ---------- 4) Gelir / gider ----------
            f.Revenue = f.Passengers * f.TicketPrice;
            f.FuelCost = fuelMax * f.Utilization;
            f.DriverCost = driverMax * f.Utilization;
            f.MaintenanceCost = maintenanceMax * f.Utilization;
            f.TollCost = tollMax * f.Utilization;
            f.TerminalCost = terminalMax * f.Utilization;
            f.ServiceCost = f.Passengers * serviceMix;

            // ---------- 5) Memnuniyet ----------
            float priceRatio = f.ReferencePrice > 0f ? f.TicketPrice / f.ReferencePrice : 1f;
            f.Satisfaction = DemandModel.Satisfaction(priceRatio, comfortMix, f.UnmetRatio, cfg);
            return f;
        }
    }
}
