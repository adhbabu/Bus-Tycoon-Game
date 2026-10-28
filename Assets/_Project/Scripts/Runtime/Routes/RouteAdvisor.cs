using System.Collections.Generic;

namespace BusTycoon.Routes
{
    public enum InsightSeverity { Info = 0, Warning = 1, Critical = 2 }

    /// <summary>UI'daki tek tıkla çözüm butonunun ne yapacağı.</summary>
    public enum InsightAction { None, AssignBus, AddBus, AdjustPrice, UpgradeBus }

    public struct RouteInsight
    {
        public InsightSeverity Severity;
        public InsightAction Action;
        public string Message;
    }

    /// <summary>
    /// "Oyuncuyu boğmama" ilkesinin çekirdeği: onlarca sayı yerine her hat için EN ÖNEMLİ tek sorunu
    /// ve tek tıkla çözüm önerisini üretir. Gürültüden etkilenmemek için son 3 günün ortalamasına bakar.
    /// </summary>
    public static class RouteAdvisor
    {
        const int Window = 3;

        public static RouteInsight GetTopInsight(RouteState route, int assignedBuses)
        {
            if (assignedBuses == 0)
                return Make(InsightSeverity.Critical, InsightAction.AssignBus, "Otobüs atanmadı — hat çalışmıyor.");

            if (!route.HasHistory)
                return Make(InsightSeverity.Info, InsightAction.None, "Hat yeni açıldı, ilk sonuçlar yarın.");

            Average(route.history, out float profit, out float loadFactor, out float unmet, out float demand, out float satisfaction);

            if (profit < 0f)
                return Make(InsightSeverity.Critical, InsightAction.AdjustPrice, $"Zarar ediyor ({profit:N0}/gün).");

            if (demand > 0f && unmet / demand > 0.15f && loadFactor > 0.8f)
                return Make(InsightSeverity.Warning, InsightAction.AddBus, $"Kapasite yetersiz: günde ~{unmet:N0} yolcu kaçıyor.");

            if (loadFactor < 0.45f)
                return Make(InsightSeverity.Warning, InsightAction.AdjustPrice, $"Doluluk düşük (%{loadFactor * 100f:0}). Fiyatı düşürmeyi deneyin.");

            if (satisfaction < 0.35f)
                return Make(InsightSeverity.Warning, InsightAction.UpgradeBus, "Yolcular memnun değil: fiyat yüksek veya araç konforsuz.");

            return Make(InsightSeverity.Info, InsightAction.None, $"Sağlıklı · {profit:N0}/gün");
        }

        static void Average(List<RouteDayReport> history, out float profit, out float loadFactor,
            out float unmet, out float demand, out float satisfaction)
        {
            profit = loadFactor = unmet = demand = satisfaction = 0f;
            int start = history.Count > Window ? history.Count - Window : 0;
            int count = history.Count - start;
            for (int i = start; i < history.Count; i++)
            {
                RouteDayReport r = history[i];
                profit += r.Profit;
                loadFactor += r.loadFactor;
                unmet += r.unmetDemand;
                demand += r.demand;
                satisfaction += r.satisfaction;
            }
            profit /= count;
            loadFactor /= count;
            unmet /= count;
            demand /= count;
            satisfaction /= count;
        }

        static RouteInsight Make(InsightSeverity severity, InsightAction action, string message) =>
            new RouteInsight { Severity = severity, Action = action, Message = message };
    }
}
