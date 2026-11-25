using System.Collections.Generic;
using BusTycoon.Routes;
using BusTycoon.Urban;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// Önemli günler (bayram, maç günü, yaz sezonu…): gün başında duyuru, ana ekranda "Takvim" kutusu ve planlayıcıda
    /// "bu hat hangi günlerde kazanır" satırı. Kurallar <see cref="UrbanEvents"/>'te; yalnız şehir içi başlangıçlı oyunlarda.
    /// </summary>
    public sealed partial class GameUI
    {
        readonly List<UrbanEvents.Event> uiEvents = new List<UrbanEvents.Event>();
        readonly List<UrbanEvents.Event> uiEventsScratch = new List<UrbanEvents.Event>();

        /// <summary>Etkinlikler gösterilir mi (şehir içi başlangıçlı oyun; eski kayıtlarda dolmuş hattı yok).</summary>
        bool EventsShown => NewUi && session.State.cityLicenses;

        /// <summary>Gün başı: bugün başlayan etkinlik duyurulur (haftalık maç günü yalnız etkilenen hat varsa).</summary>
        void AnnounceEvents()
        {
            if (!EventsShown) return;
            int day = session.State.currentDay;
            UrbanEvents.ActiveOn(day, uiEvents);
            foreach (UrbanEvents.Event e in uiEvents)
            {
                if (!UrbanEvents.StartsOn(e, day, uiEventsScratch)) continue;
                int helped = 0;
                float best = 1f;
                foreach (RouteState route in session.Routes.Routes)
                {
                    if (!route.IsUrban) continue;
                    float factor = UrbanEvents.Factor(e, route.urbanKindFame);
                    if (factor >= 1.1f) helped++;
                    best = Mathf.Max(best, factor);
                }
                if (e.Recurring && e.Id == "mac" && helped == 0) continue; // her Pazar: yalnız stadyum hattı olana
                string tail = helped > 0
                    ? $" · {helped} hattında yolcu artar (en çok +%{Mathf.RoundToInt((best - 1f) * 100f)})"
                    : " · yakınından hat geçen kazanır";
                Toast($"{e.Title}: {e.Detail}{tail}", UIStyles.Gold, quiet: e.Recurring);
            }
        }

        /// <summary>Ana ekran "Takvim" kutusu: bugünkü etkinlik (hatlarındaki en büyük etkisiyle) ya da sıradaki.</summary>
        void UpdateHomeEvent(Label title, VisualElement icon, Label sub)
        {
            if (title == null) return;
            int day = session.State.currentDay;
            UrbanEvents.ActiveOn(day, uiEvents);
            UrbanEvents.Event today = null;
            foreach (UrbanEvents.Event e in uiEvents)
                if (today == null || (today.Recurring && !e.Recurring) || (today.Id == "yaz" && e.Id == "mac")) today = e;

            if (today != null && (!today.Recurring || today.Id == "mac"))
            {
                Ui.SetText(title, today.Title);
                Ui.SetSymbol(icon, today.Icon);
                float best = BestRouteFactor(today);
                Ui.SetText(sub, best >= 1.05f ? $"Bugün · hatların +%{Mathf.RoundToInt((best - 1f) * 100f)}" : "Bugün · " + today.Detail);
                return;
            }

            int next = UrbanEvents.NextEventDay(day, 60, uiEventsScratch);
            int match = UrbanEvents.NextEventDay(day, 7, uiEventsScratch, skipWeekly: false);
            if (next < 0 || (match > 0 && match < next && BestRouteFactorOn(match) >= 1.05f)) next = match;
            if (next < 0)
            {
                Ui.SetText(title, today != null ? today.Title : "Takvim");
                Ui.SetSymbol(icon, today != null ? today.Icon : "calendar_month");
                Ui.SetText(sub, today != null ? today.Detail : "Yakında önemli gün yok");
                return;
            }
            UrbanEvents.ActiveOn(next, uiEventsScratch);
            UrbanEvents.Event upcoming = null;
            foreach (UrbanEvents.Event e in uiEventsScratch)
                if (upcoming == null || (upcoming.Recurring && !e.Recurring)) upcoming = e;
            int inDays = next - day;
            Ui.SetText(title, upcoming.Title);
            Ui.SetSymbol(icon, upcoming.Icon);
            Ui.SetText(sub, inDays == 1 ? "Yarın" : $"{inDays} gün sonra");
        }

        float BestRouteFactor(UrbanEvents.Event e)
        {
            float best = 1f;
            foreach (RouteState route in session.Routes.Routes)
                if (route.IsUrban) best = Mathf.Max(best, UrbanEvents.Factor(e, route.urbanKindFame));
            return best;
        }

        float BestRouteFactorOn(int day)
        {
            float best = 1f;
            foreach (RouteState route in session.Routes.Routes)
                if (route.IsUrban) best = Mathf.Max(best, UrbanEvents.DemandFactor(day, route.urbanKindFame, uiEventsScratch));
            return best;
        }

        /// <summary>Takvim kutusuna dokunuş: yaklaşan önemli günler kısaca (bildirim olarak).</summary>
        void ShowCalendar()
        {
            int day = session.State.currentDay;
            var parts = new List<string>();
            for (int d = day + 1; d <= day + 180 && parts.Count < 3; d++)
            {
                UrbanEvents.ActiveOn(d, uiEventsScratch);
                foreach (UrbanEvents.Event e in uiEventsScratch)
                {
                    if (e.Recurring || !UrbanEvents.StartsOn(e, d, uiEvents)) continue;
                    parts.Add(d - day == 1 ? $"{e.Title} yarın" : $"{e.Title} {d - day} gün sonra");
                }
            }
            string list = parts.Count > 0 ? string.Join(" · ", parts) : "yakında bayram yok";
            Toast($"Takvim: {list}. Her Pazar maç günü (Haziran–Temmuz hariç). Mekânların yakınından geçen hat o gün çok yolcu taşır.", UIStyles.Gold);
        }

        /// <summary>Planlayıcı: bu hat hangi önemli günlerde ek yolcu alır (en etkili iki etkinlik).</summary>
        string EventHighlights(float[] kindFame)
        {
            var best = new List<(string title, float factor)>();
            foreach (UrbanEvents.Event e in UrbanEvents.All)
            {
                float factor = UrbanEvents.Factor(e, kindFame);
                if (factor < 1.1f) continue;
                string title = e.Id == "ramazan" || e.Id == "kurban" ? "Bayramlar" : e.Title;
                if (best.Exists(x => x.title == title)) continue;
                best.Add((title, factor));
            }
            if (best.Count == 0) return null;
            best.Sort((x, y) => y.factor.CompareTo(x.factor));
            var parts = new List<string>();
            for (int i = 0; i < Mathf.Min(2, best.Count); i++) parts.Add($"{best[i].title} +%{Mathf.RoundToInt((best[i].factor - 1f) * 100f)}");
            return string.Join(" · ", parts);
        }
    }
}
