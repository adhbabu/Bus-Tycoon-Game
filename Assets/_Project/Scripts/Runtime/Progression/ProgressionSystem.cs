using System;
using BusTycoon.Core;
using BusTycoon.Data;
using BusTycoon.Economy;
using BusTycoon.Save;
using UnityEngine;

namespace BusTycoon.Progression
{
    /// <summary>
    /// Şirket ilerlemesi: XP → seviye, memnuniyet → itibar, ve bunlara bağlı kilitler
    /// (otobüs modelleri, bölgeler, hat slotu, pazar araştırması).
    ///
    /// İlerleme döngüsü:
    ///   yolcu taşı → XP → seviye ↑ → yeni segment/bölge/hat slotu
    ///   iyi hizmet + adil fiyat → memnuniyet → itibar ↑ → talep ↑ ve lüks segment kilidi
    /// </summary>
    public sealed class ProgressionSystem
    {
        readonly CompanyState state;
        readonly ProgressionConfig config;
        readonly BusCatalog catalog;
        readonly EconomyConfig economy;
        readonly FinanceSystem finance;

        public event Action<int> LevelChanged;
        public event Action<float> ReputationChanged;
        public event Action<BusModelData> ModelUnlocked;
        public event Action<RegionData> RegionUnlocked;

        public ProgressionSystem(CompanyState state, ProgressionConfig config, BusCatalog catalog,
            EconomyConfig economy, FinanceSystem finance)
        {
            this.state = state;
            this.config = config;
            this.catalog = catalog;
            this.economy = economy;
            this.finance = finance;
        }

        public int Level => state.level;
        public long Experience => state.experience;
        public float Reputation => state.reputation;
        public bool IsMaxLevel => state.level >= config.MaxLevel;
        public int MaxRoutes => config.MaxRoutesForLevel(state.level);
        public int MarketInsightLevel => config.InsightLevelFor(state.level);
        public float PriceEstimateError => config.PriceEstimateError(MarketInsightLevel);

        public long XpForCurrentLevel => config.XpRequiredForLevel(state.level);
        public long XpForNextLevel => config.XpRequiredForLevel(state.level + 1);

        public float LevelProgress01
        {
            get
            {
                if (IsMaxLevel) return 1f;
                long span = XpForNextLevel - XpForCurrentLevel;
                return span <= 0 ? 1f : Mathf.Clamp01((state.experience - XpForCurrentLevel) / (float)span);
            }
        }

        // ------------------------------------------------------------------ XP & seviye

        public void AddPassengers(double passengers)
        {
            if (passengers <= 0) return;
            long count = (long)Math.Round(passengers);
            state.totalPassengers += count;
            AddExperience((long)Math.Round(count * config.XpPerPassenger));
        }

        public void AddExperience(long xp)
        {
            if (xp <= 0) return;
            state.experience += xp;

            int newLevel = config.LevelForXp(state.experience);
            if (newLevel <= state.level) return;

            state.level = newLevel;
            LevelChanged?.Invoke(newLevel);
            RefreshUnlocks();
        }

        // ------------------------------------------------------------------ itibar

        /// <summary>
        /// Gün sonunda yolcu-ağırlıklı ortalama memnuniyet (0..1) ile çağrılır.
        /// İtibar, memnuniyet × 100 hedefine her gün <c>ReputationAdjustRate</c> oranında yaklaşır.
        /// </summary>
        public void ApplyDailySatisfaction(float averageSatisfaction01)
        {
            float target = Mathf.Clamp01(averageSatisfaction01) * 100f;
            float previous = state.reputation;
            state.reputation = Mathf.Clamp(previous + (target - previous) * economy.ReputationAdjustRate, 0f, 100f);

            if (Mathf.Abs(state.reputation - previous) > 0.0001f) ReputationChanged?.Invoke(state.reputation);
            RefreshUnlocks();
        }

        // ------------------------------------------------------------------ otobüs kilitleri

        public bool IsModelUnlocked(BusModelData model) =>
            model != null && state.level >= model.RequiredCompanyLevel && state.reputation >= model.RequiredReputation;

        public string GetLockReason(BusModelData model)
        {
            if (model == null) return "Geçersiz model.";
            if (state.level < model.RequiredCompanyLevel)
            {
                long remaining = config.XpRequiredForLevel(model.RequiredCompanyLevel) - state.experience;
                return $"Şirket seviyesi {model.RequiredCompanyLevel} gerekli ({remaining:N0} yolcu daha taşıyın).";
            }
            if (state.reputation < model.RequiredReputation)
                return $"İtibar {model.RequiredReputation:0} gerekli (şu an {state.reputation:0}).";
            return null;
        }

        /// <summary>Oyuncunun bir sonraki hedefi: henüz açılmamış en düşük kademeli model.</summary>
        public BusModelData GetNextLockedModel()
        {
            foreach (var model in catalog.SortedByTier)
                if (!IsModelUnlocked(model)) return model;
            return null;
        }

        /// <summary>Yeni açılan modelleri bulur ve bir kez duyurur (UI'da "YENİ" rozeti + bildirim).</summary>
        public void RefreshUnlocks(bool silent = false)
        {
            foreach (var model in catalog.SortedByTier)
            {
                if (!IsModelUnlocked(model) || state.announcedModelIds.Contains(model.Id)) continue;
                state.announcedModelIds.Add(model.Id);
                if (!silent) ModelUnlocked?.Invoke(model);
            }
        }

        // ------------------------------------------------------------------ bölge kilitleri

        public bool IsRegionUnlocked(RegionData region) =>
            region != null && state.unlockedRegionIds.Contains(region.Id);

        public OpResult CanUnlockRegion(RegionData region)
        {
            if (region == null) return OpResult.Fail("Geçersiz bölge.");
            if (IsRegionUnlocked(region)) return OpResult.Fail($"{region.DisplayName} zaten açık.");
            if (state.level < region.RequiredCompanyLevel)
                return OpResult.Fail($"{region.DisplayName} için şirket seviyesi {region.RequiredCompanyLevel} gerekli.");
            if (!finance.CanAfford(region.OperatingLicenseCost))
                return OpResult.Fail($"İşletme lisansı için {region.OperatingLicenseCost:N0} gerekli.");
            return OpResult.Ok();
        }

        public OpResult TryUnlockRegion(RegionData region)
        {
            OpResult check = CanUnlockRegion(region);
            if (!check) return check;

            finance.TrySpend(region.OperatingLicenseCost, LedgerCategory.RegionLicense, region.Id);
            state.unlockedRegionIds.Add(region.Id);
            RegionUnlocked?.Invoke(region);
            return OpResult.Ok($"{region.DisplayName} işletme lisansı alındı.");
        }

        /// <summary>Başlangıç bölgesi gibi ücretsiz açılışlar için.</summary>
        internal void GrantRegion(RegionData region)
        {
            if (region != null && !IsRegionUnlocked(region)) state.unlockedRegionIds.Add(region.Id);
        }
    }
}
