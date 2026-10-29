using System;
using BusTycoon.Core;
using BusTycoon.Data;
using BusTycoon.Economy;
using BusTycoon.Progression;
using BusTycoon.Save;
using UnityEngine;

namespace BusTycoon.Stations
{
    /// <summary>Durağın popülerliği (0..100) ve bileşenleri (her biri 0..1). Durağın içindeki kalabalığı belirler.</summary>
    public readonly struct StationPopularity
    {
        /// <summary>Şehrin büyüklüğü ve turistik cazibesi.</summary>
        public readonly float Fame;
        /// <summary>Hatlarınızla duraktan geçen günlük yolcu (durağın büyüklüğüne göre).</summary>
        public readonly float Traffic;
        /// <summary>Yapılan geliştirmelerin konforu (kademenin en yüksek konforuna oranı).</summary>
        public readonly float Comfort;
        public readonly float Score;

        public StationPopularity(float fame, float traffic, float comfort)
        {
            Fame = fame;
            Traffic = traffic;
            Comfort = comfort;
            Score = 100f * Mathf.Clamp01(0.35f * fame + 0.5f * traffic + 0.15f * comfort);
        }

        /// <summary>0..5 yıldız.</summary>
        public float Stars => Score / 20f;
    }

    /// <summary>
    /// Şehir durakları: geliştirme satın alma, konfor → talep bonusu ve popülerlik. Geliştirme yalnızca şirketin
    /// işlediği duraklara (merkez ya da hattı olan şehir) yapılabilir. Hat talebi iki ucun bonuslarının ortalaması
    /// kadar artar (<see cref="RouteDemandFactor"/>; RouteSystem talep hesabına katar).
    /// </summary>
    public sealed class StationSystem
    {
        /// <summary>Konfor puanı başına durağın talep bonusu.</summary>
        public const float DemandBonusPerComfort = 0.015f;

        readonly CompanyState state;
        readonly FinanceSystem finance;
        readonly ProgressionSystem progression;

        public event Action<CityData, StationUpgrade> UpgradeBuilt;

        public StationSystem(CompanyState state, FinanceSystem finance, ProgressionSystem progression)
        {
            this.state = state;
            this.finance = finance;
            this.progression = progression;
            this.state.stations ??= new System.Collections.Generic.List<StationState>();
        }

        public StationState Find(CityData city) => city == null ? null : state.stations.Find(s => s.cityId == city.Id);

        public bool Has(CityData city, StationUpgrade upgrade) => upgrade != null && Has(city, upgrade.Id);

        public bool Has(CityData city, string upgradeId)
        {
            StationState station = Find(city);
            return station != null && station.upgrades.Contains(upgradeId);
        }

        public int UpgradeCount(CityData city) => Find(city)?.upgrades.Count ?? 0;

        public int Comfort(CityData city)
        {
            StationState station = Find(city);
            if (station == null) return 0;
            int total = 0;
            foreach (string id in station.upgrades) total += StationCatalog.Get(id)?.Comfort ?? 0;
            return total;
        }

        /// <summary>Durağın kendi talep bonusu (0.06 = %6).</summary>
        public float DemandBonus(CityData city) => Comfort(city) * DemandBonusPerComfort;

        /// <summary>A ↔ B hattının talep çarpanı: iki ucun bonuslarının ortalaması.</summary>
        public float RouteDemandFactor(CityData a, CityData b) => 1f + 0.5f * (DemandBonus(a) + DemandBonus(b));

        public float Cost(CityData city, StationUpgrade upgrade)
        {
            float raw = upgrade.BaseCost * StationCatalog.CostMultiplier(StationCatalog.TierOf(city));
            return Mathf.Round(raw / 500f) * 500f;
        }

        public int RouteCount(CityData city)
        {
            int count = 0;
            if (city == null) return 0;
            foreach (var route in state.routes)
                if (route.cityAId == city.Id || route.cityBId == city.Id) count++;
            return count;
        }

        /// <summary>Şirketin bu şehirde durağı var mı (merkez ya da en az bir hat)?</summary>
        public bool IsOperating(CityData city) => city != null && (city.Id == state.startCityId || RouteCount(city) > 0);

        /// <summary>Son gün hatlarınızla duraktan geçen yolcu (gidiş + dönüş).</summary>
        public float DailyPassengers(CityData city)
        {
            float total = 0f;
            if (city == null) return 0f;
            foreach (var route in state.routes)
                if ((route.cityAId == city.Id || route.cityBId == city.Id) && route.HasHistory)
                    total += route.LastReport.passengers;
            return total;
        }

        public StationPopularity Popularity(CityData city)
        {
            if (city == null) return default;
            StationTier tier = StationCatalog.TierOf(city);
            float size = Mathf.Clamp01(((float)Math.Log10(Math.Max(1L, city.Population)) - 4.9f) / 2.3f);
            float tourism = Mathf.Clamp01((city.TourismIndex - 0.8f) / 0.7f);
            float fame = 0.7f * size + 0.3f * tourism;

            float passengers = DailyPassengers(city);
            float busy = StationCatalog.BusyPassengersPerDay(tier);
            float traffic = passengers <= 0f ? 0f : Mathf.Clamp01((float)(Math.Log(1.0 + passengers) / Math.Log(1.0 + busy)));

            int maxComfort = StationCatalog.MaxComfort(tier);
            float comfort = maxComfort > 0 ? Mathf.Clamp01(Comfort(city) / (float)maxComfort) : 0f;
            return new StationPopularity(fame, traffic, comfort);
        }

        public OpResult CanBuild(CityData city, StationUpgrade upgrade)
        {
            if (city == null || upgrade == null) return OpResult.Fail("Geliştirme bulunamadı.");
            if (Has(city, upgrade)) return OpResult.Fail("Bu geliştirme zaten yapıldı.");
            StationTier tier = StationCatalog.TierOf(city);
            if (!StationCatalog.IsAvailable(upgrade, tier))
                return OpResult.Fail($"{StationCatalog.TierName(upgrade.MinTier)} ve üstü duraklarda yapılabilir.");
            if (!progression.IsRegionUnlocked(city.Region))
                return OpResult.Fail($"{city.Region.DisplayName} için işletme lisansınız yok.");
            if (!IsOperating(city)) return OpResult.Fail("Bu şehre hat açınca durağı geliştirebilirsiniz.");
            float cost = Cost(city, upgrade);
            if (!finance.CanAfford(cost)) return OpResult.Fail($"Kasada yeterli para yok ({cost:N0} gerekli).");
            return OpResult.Ok();
        }

        public OpResult TryBuild(CityData city, StationUpgrade upgrade)
        {
            OpResult check = CanBuild(city, upgrade);
            if (!check) return check;

            finance.TrySpend(Cost(city, upgrade), LedgerCategory.StationUpgrade, city.Id + ":" + upgrade.Id);
            StationState station = Find(city);
            if (station == null)
            {
                station = new StationState { cityId = city.Id, firstUpgradeDay = state.currentDay };
                state.stations.Add(station);
            }
            station.upgrades.Add(upgrade.Id);
            UpgradeBuilt?.Invoke(city, upgrade);
            return OpResult.Ok($"{city.DisplayName}: {upgrade.Name} yapıldı.");
        }
    }
}
