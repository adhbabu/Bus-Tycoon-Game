using System;
using System.Collections.Generic;

namespace BusTycoon.Routes
{
    /// <summary>Bir hattın tek günlük sonucu. Hat geçmişi grafiği ve raporlar için saklanır.</summary>
    [Serializable]
    public struct RouteDayReport
    {
        public int day;
        public string routeId;
        public float ticketPrice;
        public float demand;
        public float passengers;
        public float loadFactor;
        public float unmetDemand;
        public float tripsPerDay;
        public float revenue;
        public float cost;
        public float satisfaction;
        public int activeBuses;

        public float Profit => revenue - cost;
    }

    /// <summary>
    /// Oyuncunun açtığı A ↔ B hattının runtime durumu. Otobüs ataması otobüs tarafında
    /// (<c>BusInstance.assignedRouteId</c>) tutulur; tek doğruluk kaynağı odur.
    /// </summary>
    [Serializable]
    public sealed class RouteState
    {
        public const int HistoryLength = 30;

        public string routeId;
        public string cityAId;
        public string cityBId;
        /// <summary>Açılışta hesaplanan karayolu mesafesi (km).</summary>
        public float distanceKm;
        public float ticketPrice;
        public int openedDay;

        /// <summary>true: sefer sayısı talebe göre otomatik planlanır (önerilen). false: manualUtilization kullanılır.</summary>
        public bool autoSchedule = true;
        /// <summary>Atanmış otobüslerin maksimum sefer kapasitesinin kullanılacak oranı (0..1).</summary>
        public float manualUtilization = 1f;

        // ---- Şehir içi (dolmuş) hat: iki uç durak; şehirlerarası hatta boş. cityAId = cityBId = şehir.
        // Uçların adı ve konumu (metre) saklanır: şehir verisi yeniden üretilse de hat en yakın durağa oturur.
        public string urbanCityId;
        public string urbanNameA, urbanNameB;
        public float urbanAX, urbanAY, urbanBX, urbanBY;
        /// <summary>Açılışta hesaplanan olgun talep potansiyeli (yolcu/gün, referans fiyatta).</summary>
        public float urbanDemand;
        /// <summary>Güzergâhtaki durak sayısı (uçlar dahil).</summary>
        public int urbanStopCount;

        public bool IsUrban => !string.IsNullOrEmpty(urbanCityId);

        /// <summary>Kurulumun bitmesine kalan gün (0: hat çalışıyor). Kurulumda sefer yapılmaz.</summary>
        public int setupDaysLeft;
        public bool IsSettingUp => setupDaysLeft > 0;

        public double lifetimePassengers;
        public double lifetimeProfit;
        public List<RouteDayReport> history = new List<RouteDayReport>();

        public bool HasHistory => history != null && history.Count > 0;
        public RouteDayReport LastReport => HasHistory ? history[history.Count - 1] : default;

        public void AddReport(RouteDayReport report)
        {
            history ??= new List<RouteDayReport>();
            history.Add(report);
            if (history.Count > HistoryLength) history.RemoveAt(0);
            lifetimePassengers += report.passengers;
            lifetimeProfit += report.Profit;
        }
    }
}
