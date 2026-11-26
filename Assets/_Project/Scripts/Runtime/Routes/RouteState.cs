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
        /// <summary>Önemli gün çarpanı (bayram, maç günü…; 0 ya da 1: sıradan gün).</summary>
        public float eventFactor;

        public float Profit => revenue - cost;
    }

    /// <summary>
    /// Hattın içinde bulunulan günün kasaya işlenmiş kısmı. Gelir ve giderler gün sonunu beklemeden saniyede bir
    /// işlenir (<c>RouteSystem.Accrue</c>); gün sonunda kalan kısım işlenir, toplamlar günün raporu olur.
    /// Kayda yazılır: gün ortasında kaydedilip açılan oyunda aynı tutar iki kez işlenmez.
    /// </summary>
    [Serializable]
    public sealed class RouteDayAccrual
    {
        /// <summary>Günün işlenmiş kesri (0..1). Gün ortasında açılan hatta açılış anından başlar.</summary>
        public float booked;
        /// <summary>Seferlerin yapıldığı kesir (araçlı ve kurulumu bitmiş kısım).</summary>
        public float operated;
        /// <summary>Günün talep oynaklığı çarpanı (0: çekilmedi, 1 sayılır).</summary>
        public float noise;
        public float revenue, cost, passengers, demand, unmet, trips, load, satisfaction, eventFactor;
        public int activeBuses;

        public void Reset(float nextNoise)
        {
            booked = operated = 0f;
            revenue = cost = passengers = demand = unmet = trips = load = satisfaction = eventFactor = 0f;
            activeBuses = 0;
            noise = nextNoise;
        }
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
        /// <summary>Hattın yakınındaki ünlü mekânların türlere göre ün toplamı: önemli günlerde yolcu akını buna göre (UrbanEvents).</summary>
        public float[] urbanKindFame;

        public bool IsUrban => !string.IsNullOrEmpty(urbanCityId);

        /// <summary>Kurulumun bitmesine kalan gün (0: hat çalışıyor). Kurulumda sefer yapılmaz.</summary>
        public int setupDaysLeft;
        public bool IsSettingUp => setupDaysLeft > 0;

        /// <summary>Bugünün kasaya işlenmiş kısmı (bkz. <see cref="RouteDayAccrual"/>).</summary>
        public RouteDayAccrual today = new RouteDayAccrual();
        public RouteDayAccrual Today => today ??= new RouteDayAccrual();

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
