using System;
using UnityEngine;

namespace BusTycoon.Data
{
    /// <summary>
    /// Şirket seviyesi, başlangıç koşulları ve seviyeyle açılan imkanlar.
    /// Seviye eğrisi: XP(L) = xpBase × (L − 1)^xpExponent. XP, taşınan yolcudan kazanılır.
    /// </summary>
    [CreateAssetMenu(menuName = "Bus Tycoon/Config/Progression Config", fileName = "ProgressionConfig")]
    public sealed class ProgressionConfig : ScriptableObject
    {
        [Header("Başlangıç")]
        [SerializeField, Min(0f)] float startingCash = 40000f;
        [SerializeField, Range(0f, 100f)] float startingReputation = 30f;
        [Tooltip("Oyuna hediye verilen (2. el) ilk otobüs. Boşsa otobüs verilmez.")]
        [SerializeField] BusModelData startingBus;
        [SerializeField, Range(0.1f, 1f)] float startingBusCondition = 0.6f;

        [Header("Seviye eğrisi")]
        [SerializeField, Min(1f)] float xpBase = 1500f;
        [SerializeField, Range(1f, 3f)] float xpExponent = 2.2f;
        [SerializeField, Min(2)] int maxLevel = 30;
        [SerializeField, Min(0f)] float xpPerPassenger = 1f;

        [Header("Hat limitleri (lisans slotu)")]
        [SerializeField, Min(1)] int baseRouteSlots = 2;
        [SerializeField, Min(0f)] float routeSlotsPerLevel = 1f;

        [Header("Pazar araştırması: fiyat tahmininin doğruluğu")]
        [Tooltip("Bu seviyelere ulaşıldığında pazar araştırması kademesi artar.")]
        [SerializeField] int[] marketInsightUnlockLevels = { 4, 8, 12 };
        [Tooltip("Kademe başına talep tahmini hata payı (±). Eleman sayısı = kilit sayısı + 1.")]
        [SerializeField] float[] priceEstimateErrorByInsight = { 0.2f, 0.12f, 0.06f, 0f };

        [Header("İflas")]
        [Tooltip("Kasa bu kadar gün üst üste eksi kalırsa şirket iflas eder.")]
        [SerializeField, Min(1)] int bankruptcyGraceDays = 30;

        public float StartingCash => startingCash;
        public float StartingReputation => startingReputation;
        public BusModelData StartingBus => startingBus;
        public float StartingBusCondition => startingBusCondition;
        public int MaxLevel => maxLevel;
        public float XpPerPassenger => xpPerPassenger;
        public int BankruptcyGraceDays => bankruptcyGraceDays;

        /// <summary>Seviyeye ulaşmak için gereken TOPLAM XP. Seviye 1 = 0 XP.</summary>
        public long XpRequiredForLevel(int level)
        {
            level = Mathf.Clamp(level, 1, maxLevel);
            return (long)Math.Round(xpBase * Math.Pow(level - 1, xpExponent));
        }

        public int LevelForXp(long xp)
        {
            int level = 1;
            while (level < maxLevel && xp >= XpRequiredForLevel(level + 1)) level++;
            return level;
        }

        public int MaxRoutesForLevel(int level) =>
            baseRouteSlots + Mathf.FloorToInt(routeSlotsPerLevel * (Mathf.Max(1, level) - 1));

        public int InsightLevelFor(int companyLevel)
        {
            int insight = 0;
            if (marketInsightUnlockLevels == null) return 0;
            foreach (int unlockLevel in marketInsightUnlockLevels)
                if (companyLevel >= unlockLevel) insight++;
            return insight;
        }

        public float PriceEstimateError(int insightLevel)
        {
            if (priceEstimateErrorByInsight == null || priceEstimateErrorByInsight.Length == 0) return 0f;
            return priceEstimateErrorByInsight[Mathf.Clamp(insightLevel, 0, priceEstimateErrorByInsight.Length - 1)];
        }

        internal void SetStartingBus(BusModelData bus, float condition)
        {
            startingBus = bus;
            startingBusCondition = condition;
        }
    }
}
