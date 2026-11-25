using System;
using System.Collections.Generic;
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
        readonly Func<string, CityData> cityLookup;

        public event Action<CityData, StationUpgrade> UpgradeBuilt;
        /// <summary>Yapımı süren geliştirme bitti (bekleme süreli oyunlar; hemen yapılan için çağrılmaz).</summary>
        public event Action<CityData, StationUpgrade> UpgradeCompleted;

        public StationSystem(CompanyState state, FinanceSystem finance, ProgressionSystem progression, Func<string, CityData> cityLookup = null)
        {
            this.cityLookup = cityLookup;
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

        /// <summary>Yapım süresi (bekleme süreli oyunlarda): ucuz geliştirme 1, orta 2, büyük 3 gün.</summary>
        public int BuildDays(CityData city, StationUpgrade upgrade)
        {
            float cost = Cost(city, upgrade);
            return cost < 15000f ? 1 : cost < 60000f ? 2 : 3;
        }

        /// <summary>Yapımdaysa kalan gün; değilse 0.</summary>
        public int BuildDaysLeft(CityData city, StationUpgrade upgrade)
        {
            StationState station = Find(city);
            if (station?.building == null || upgrade == null) return 0;
            int index = station.building.IndexOf(upgrade.Id);
            return index >= 0 && index < station.buildingDays.Count ? Math.Max(1, station.buildingDays[index]) : 0;
        }

        public bool IsBuilding(CityData city, StationUpgrade upgrade) => BuildDaysLeft(city, upgrade) > 0;

        /// <summary>Gün sonu: yapımdaki geliştirmelerin süresi bir gün azalır; biten durağa eklenir.</summary>
        public void AdvanceDay()
        {
            for (int s = 0; s < state.stations.Count; s++)
            {
                StationState station = state.stations[s];
                if (station.building == null || station.building.Count == 0) continue;
                station.buildingDays ??= new List<int>();
                for (int i = station.building.Count - 1; i >= 0; i--)
                {
                    int left = (i < station.buildingDays.Count ? station.buildingDays[i] : 1) - 1;
                    if (i < station.buildingDays.Count) station.buildingDays[i] = left;
                    if (left <= 0) Complete(station, i);
                }
            }
        }

        /// <summary>Yapımı hemen bitirir (ödüllü reklam). Yapımda değilse false.</summary>
        public bool FinishNow(CityData city, StationUpgrade upgrade)
        {
            StationState station = Find(city);
            int index = station?.building?.IndexOf(upgrade?.Id) ?? -1;
            if (index < 0) return false;
            Complete(station, index);
            return true;
        }

        void Complete(StationState station, int index)
        {
            string id = station.building[index];
            station.building.RemoveAt(index);
            if (index < station.buildingDays.Count) station.buildingDays.RemoveAt(index);
            if (!station.upgrades.Contains(id)) station.upgrades.Add(id);
            CityData city = cityLookup?.Invoke(station.cityId);
            StationUpgrade upgrade = StationCatalog.Get(id);
            if (city == null || upgrade == null) return;
            UpgradeBuilt?.Invoke(city, upgrade);
            UpgradeCompleted?.Invoke(city, upgrade);
        }

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
                if (!route.IsUrban && (route.cityAId == city.Id || route.cityBId == city.Id)) count++; // otogar: şehirlerarası hatlar
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
                if (!route.IsUrban && (route.cityAId == city.Id || route.cityBId == city.Id) && route.HasHistory)
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
            if (IsBuilding(city, upgrade)) return OpResult.Fail("Yapımı sürüyor.");
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
            if (state.waitTimes)
            {
                station.building ??= new List<string>();
                station.buildingDays ??= new List<int>();
                int days = BuildDays(city, upgrade);
                station.building.Add(upgrade.Id);
                station.buildingDays.Add(days);
                return OpResult.Ok($"{city.DisplayName}: {upgrade.Name} yapımı başladı ({days} gün).");
            }
            station.upgrades.Add(upgrade.Id);
            UpgradeBuilt?.Invoke(city, upgrade);
            return OpResult.Ok($"{city.DisplayName}: {upgrade.Name} yapıldı.");
        }
    }
}
