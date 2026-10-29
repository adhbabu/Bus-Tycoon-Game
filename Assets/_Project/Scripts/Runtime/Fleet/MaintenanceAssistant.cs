using System;
using System.Collections.Generic;
using BusTycoon.Data;
using BusTycoon.Economy;
using BusTycoon.Routes;
using BusTycoon.Save;

namespace BusTycoon.Fleet
{
    /// <summary>Bakım asistanının bir çalışmasının özeti (bildirim için).</summary>
    public struct MaintenanceReport
    {
        /// <summary>Revizyona giren otobüs.</summary>
        public int Overhauled;
        /// <summary>Acil tamirle bugün servise dönen arızalı otobüs.</summary>
        public int Repaired;
        /// <summary>Garajdan hattına (ya da en çok yolcu bekleyen hatta) gönderilen otobüs.</summary>
        public int Reassigned;
        /// <summary>Kasada yedek para kalmayacağı için sonraki güne bırakılan iş.</summary>
        public int Postponed;
        public double Spent;

        public bool IsEmpty => Overhauled + Repaired + Reassigned == 0;
    }

    /// <summary>
    /// Bakım asistanı: açıkken her gün başında durumu eşiğin altına düşen otobüsleri (en yıpranmış önce) revizyona
    /// sokar ve arızalananları acil tamirle hattına geri gönderir. "Hepsini onar" komutu ayrıca hedef durumun altındaki
    /// tüm otobüsleri onarır ve garajdakileri son hatlarına (yoksa en çok yolcu bekleyen hatta) gönderir.
    /// Kasada son günün giderinin <see cref="EconomyConfig.AutoOverhaulReserveDays"/> katı kadar para bırakılır: asistan
    /// şirketi borca sokmaz, parası yetmeyen iş ertesi güne kalır.
    /// </summary>
    public sealed class MaintenanceAssistant
    {
        readonly CompanyState state;
        readonly FleetSystem fleet;
        readonly RouteSystem routes;
        readonly FinanceSystem finance;
        readonly EconomyConfig economy;
        readonly List<BusInstance> buffer = new List<BusInstance>();

        /// <summary>Asistan bir iş yaptığında ya da para yetmediği için iş bıraktığında.</summary>
        public event Action<MaintenanceReport> Completed;

        public MaintenanceAssistant(CompanyState state, FleetSystem fleet, RouteSystem routes, FinanceSystem finance, EconomyConfig economy)
        {
            this.state = state;
            this.fleet = fleet;
            this.routes = routes;
            this.finance = finance;
            this.economy = economy;
        }

        public bool Enabled => state.autoMaintenance;

        public void SetEnabled(bool enabled) => state.autoMaintenance = enabled;

        /// <summary>Gün başında (açıksa): eşiğin altındakiler revizyona, arızalılar acil tamire.</summary>
        public MaintenanceReport RunDaily() =>
            state.autoMaintenance ? Run(economy.AutoOverhaulBelowCondition, sendToRoutes: false) : default;

        /// <summary>"Hepsini onar ve hatlara gönder": hedef durumun altındaki her otobüs, arızalılar ve garajdakiler.</summary>
        public MaintenanceReport RunNow() => Run(economy.OverhaulTargetCondition - 0.01f, sendToRoutes: true);

        /// <summary>"Hepsini onar" düğmesi için tahmini bedel ve yapılacak iş sayısı.</summary>
        public int PendingWork(out double cost)
        {
            cost = 0d;
            int count = 0;
            foreach (var bus in fleet.Buses)
            {
                if (bus.Model == null) continue;
                if (!bus.IsAvailableToday)
                {
                    cost += fleet.GetExpressRepairCost(bus);
                    count++;
                }
                if (bus.condition < economy.OverhaulTargetCondition - 0.01f)
                {
                    cost += fleet.GetOverhaulCost(bus);
                    count++;
                }
                if (!bus.IsAssigned && TargetRoute(bus, null) != null) count++;
            }
            return count;
        }

        MaintenanceReport Run(float overhaulBelow, bool sendToRoutes)
        {
            var report = new MaintenanceReport();
            double reserve = Reserve();

            // 1) Arızalılar: önce hatta çalışanlar (yolcu bekliyor)
            buffer.Clear();
            foreach (var bus in fleet.Buses)
                if (bus.Model != null && !bus.IsAvailableToday) buffer.Add(bus);
            buffer.Sort((a, b) => b.IsAssigned.CompareTo(a.IsAssigned));
            foreach (var bus in buffer)
            {
                float cost = fleet.GetExpressRepairCost(bus);
                if (finance.Cash - cost < reserve || !fleet.TryExpressRepair(bus.busId))
                {
                    report.Postponed++;
                    continue;
                }
                report.Repaired++;
                report.Spent += cost;
            }

            // 2) Yıpranmışlar: en yıpranmış önce
            buffer.Clear();
            foreach (var bus in fleet.Buses)
                if (bus.Model != null && bus.condition < overhaulBelow) buffer.Add(bus);
            buffer.Sort((a, b) => a.condition.CompareTo(b.condition));
            foreach (var bus in buffer)
            {
                float cost = fleet.GetOverhaulCost(bus);
                if (finance.Cash - cost < reserve || !fleet.TryOverhaul(bus.busId))
                {
                    report.Postponed++;
                    continue;
                }
                report.Overhauled++;
                report.Spent += cost;
            }

            // 3) Garajdakiler hatlarına
            if (sendToRoutes)
            {
                var added = new Dictionary<string, float>(); // bu çalışmada hatlara eklenen kapasite (koltuk/gün)
                buffer.Clear();
                foreach (var bus in fleet.Buses)
                    if (bus.Model != null && !bus.IsAssigned && bus.IsAvailableToday) buffer.Add(bus);
                foreach (var bus in buffer)
                {
                    RouteState target = TargetRoute(bus, added);
                    if (target == null || !routes.TryAssignBus(bus.busId, target.routeId)) continue;
                    added.TryGetValue(target.routeId, out float seats);
                    added[target.routeId] = seats + DailySeats(bus, target);
                    report.Reassigned++;
                }
            }

            if (!report.IsEmpty || report.Postponed > 0) Completed?.Invoke(report);
            return report;
        }

        /// <summary>Otobüsün gideceği hat: son hattı hâlâ açıksa o, değilse en çok yolcunun bilet bulamadığı hat.</summary>
        RouteState TargetRoute(BusInstance bus, Dictionary<string, float> added)
        {
            RouteState last = string.IsNullOrEmpty(bus.lastRouteId) ? null : routes.GetRoute(bus.lastRouteId);
            if (last != null) return last;

            RouteState best = null;
            float bestUnmet = 0f;
            foreach (var route in routes.Routes)
            {
                if (!route.HasHistory) continue;
                float unmet = route.LastReport.unmetDemand;
                if (added != null && added.TryGetValue(route.routeId, out float seats)) unmet -= seats;
                if (unmet < bus.Model.Seats || unmet <= bestUnmet) continue;
                best = route;
                bestUnmet = unmet;
            }
            return best;
        }

        float DailySeats(BusInstance bus, RouteState route) =>
            bus.Model.Seats * RouteEconomics.MaxTripsPerDay(bus.Model, route.distanceKm, economy) * economy.AutoScheduleTargetLoadFactor;

        double Reserve()
        {
            var history = state.financeHistory;
            return history != null && history.Count > 0 ? history[history.Count - 1].expenses * economy.AutoOverhaulReserveDays : 0d;
        }
    }
}
