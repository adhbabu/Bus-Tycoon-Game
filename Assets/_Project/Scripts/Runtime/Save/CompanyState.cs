using System;
using System.Collections.Generic;
using BusTycoon.Economy;
using BusTycoon.Fleet;
using BusTycoon.Routes;
using BusTycoon.Stations;

namespace BusTycoon.Save
{
    /// <summary>
    /// Oyunun TÜM değişken durumu (kayıt dosyasının kökü). Kural:
    /// ScriptableObject referansı ASLA burada tutulmaz; yalnızca kalıcı kimlikler (cityId, modelId, regionId).
    /// Böylece JSON kaydı, asset'ler yeniden düzenlense bile bozulmaz.
    /// </summary>
    [Serializable]
    public sealed class CompanyState
    {
        public const int CurrentSaveVersion = 1;

        public int saveVersion = CurrentSaveVersion;
        public string companyName;
        public string startCityId;
        public int currentDay;

        // Finans
        public double cash;
        public int daysInDebt;
        public List<DailyFinanceSummary> financeHistory = new List<DailyFinanceSummary>();
        public List<TransactionRecord> recentTransactions = new List<TransactionRecord>();

        // İlerleme
        public long experience;
        public int level = 1;
        public float reputation;
        public long totalPassengers;
        public List<string> unlockedRegionIds = new List<string>();
        /// <summary>Oyuncuya "YENİ" olarak duyurulmuş modeller (kilit kontrolü için değil, bildirim için).</summary>
        public List<string> announcedModelIds = new List<string>();
        /// <summary>Tamamlanan görev (hedef) kimlikleri.</summary>
        public List<string> completedGoalIds = new List<string>();

        // Operasyon
        public int nextRouteSerial;
        public int nextBusSerial;
        public List<RouteState> routes = new List<RouteState>();
        public List<BusInstance> buses = new List<BusInstance>();

        // Duraklar (yalnızca geliştirilenler; geliştirmesiz durak kayıtta yer tutmaz)
        public List<StationState> stations = new List<StationState>();
    }
}
