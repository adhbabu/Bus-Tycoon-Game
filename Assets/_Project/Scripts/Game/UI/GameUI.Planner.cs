using System.Collections.Generic;
using BusTycoon.Data;
using BusTycoon.Fleet;
using BusTycoon.Routes;
using UnityEngine;

namespace BusTycoon.Game
{
    // Hat planlayıcı: kalkış → varış → teklif kartı → "Hattı Aç". Kalkış seçilince en kârlı hedefleri önerir.
    public sealed partial class GameUI
    {
        const int SuggestionCount = 6;

        CityData plannerOrigin, plannerDestination;
        int plannerModelIndex;
        RouteQuote plannerQuote;
        bool plannerQuoteValid, quoteDirty;
        bool suggestionsDirty = true;
        bool assignIdleOnOpen = true;
        Vector2 plannerScroll;
        CityData suggestionsOrigin;
        BusModelData suggestionsModel;
        readonly List<BusModelData> plannerModels = new List<BusModelData>();
        readonly List<(CityData city, RouteQuote quote)> suggestions = new List<(CityData, RouteQuote)>();

        void OpenPlannerNow(CityData origin)
        {
            if (mode != Mode.Playing) return;
            panel = Panel.Planner;
            plannerDestination = null;
            plannerQuoteValid = false;
            plannerOrigin = origin;
            RefreshPlannerModels();
            suggestionsDirty = true;
            if (origin != null) mapCamera.FocusOn(world.GetCityPosition(origin));
        }

        void RefreshPlannerModels()
        {
            plannerModels.Clear();
            foreach (var model in session.Catalog.SortedByTier)
                if (session.Progression.IsModelUnlocked(model)) plannerModels.Add(model);

            // Varsayılan: garajda en çok boşta bekleyen model; yoksa açık olan en yüksek kademe.
            session.Fleet.GetIdleBuses(busBuffer);
            int best = plannerModels.Count - 1, bestCount = 0;
            for (int i = 0; i < plannerModels.Count; i++)
            {
                int count = busBuffer.FindAll(b => b.Model == plannerModels[i]).Count;
                if (count > bestCount) { bestCount = count; best = i; }
            }
            plannerModelIndex = Mathf.Max(0, best);
        }

        BusModelData PlannerModel =>
            plannerModels.Count == 0 ? null : plannerModels[Mathf.Clamp(plannerModelIndex, 0, plannerModels.Count - 1)];

        void PlannerCityClicked(CityData city)
        {
            if (plannerOrigin == null)
            {
                plannerOrigin = city;
                suggestionsDirty = true;
            }
            else if (city == plannerOrigin)
            {
                plannerOrigin = plannerDestination = null;
                plannerQuoteValid = false;
            }
            else
            {
                plannerDestination = city;
                RecomputeQuote();
            }
        }

        void RecomputeQuote()
        {
            quoteDirty = false;
            plannerQuoteValid = plannerOrigin != null && plannerDestination != null && PlannerModel != null;
            if (plannerQuoteValid) plannerQuote = session.Routes.QuoteRoute(plannerOrigin, plannerDestination, PlannerModel);
        }

        void RecomputeSuggestions()
        {
            suggestionsDirty = false;
            suggestions.Clear();
            suggestionsOrigin = plannerOrigin;
            suggestionsModel = PlannerModel;
            if (plannerOrigin == null || PlannerModel == null) return;
            if (!session.Progression.IsRegionUnlocked(plannerOrigin.Region)) return;

            foreach (var city in session.World.Cities)
            {
                if (city == plannerOrigin || !session.Progression.IsRegionUnlocked(city.Region)) continue;
                if (!session.World.CanConnect(plannerOrigin, city, out _)) continue;
                if (session.Routes.FindRoute(plannerOrigin, city) != null) continue;
                suggestions.Add((city, session.Routes.QuoteRoute(plannerOrigin, city, PlannerModel)));
            }

            suggestions.Sort((x, y) => y.quote.Forecast.Profit.CompareTo(x.quote.Forecast.Profit));
            if (suggestions.Count > SuggestionCount) suggestions.RemoveRange(SuggestionCount, suggestions.Count - SuggestionCount);
        }

        void DrawPlanner()
        {
            PanelHeader("Yeni Hat Planla", "Kalkış ve varış şehirlerini haritadan seçin.");
            plannerScroll = BeginScroll(plannerScroll);

            if (plannerOrigin == null)
            {
                GUILayout.Label("1) Haritada KALKIŞ şehrine tıklayın.", s.Bold);
                Sp(6f);
                CityData hq = session.Headquarters;
                if (hq != null && GUILayout.Button($"Merkez ofisten başla: {hq.DisplayName}", s.Button, Ht(38f)))
                    Defer(() => { plannerOrigin = hq; suggestionsDirty = true; });
                Sp(8f);
                GUILayout.Label("İpucu: Büyük ve yakın şehir çiftleri (ör. İstanbul–Bursa, İstanbul–Kocaeli) minibüsle bile kârlıdır.", s.WrapDim);
                GUILayout.EndScrollView();
                return;
            }

            // Kalkış satırı
            GUILayout.BeginHorizontal();
            GUILayout.BeginHorizontal();
            LayoutCityIcon(plannerOrigin, 24f, 2f);
            ColorLabel($"Kalkış: {plannerOrigin.DisplayName}", UIStyles.Blue, s.H2);
            LayoutIcon(UIIcons.Flag(plannerOrigin.Region.Id), 20f, 4f);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Değiştir", s.Ghost, Wd(90f)))
                Defer(() => { plannerOrigin = plannerDestination = null; plannerQuoteValid = false; });
            GUILayout.EndHorizontal();

            if (!session.Progression.IsRegionUnlocked(plannerOrigin.Region))
            {
                ColorLabel($"{plannerOrigin.Region.DisplayName} için işletme lisansınız yok.", UIStyles.Orange, s.Wrap);
                if (GUILayout.Button("Bölgeler panelini aç", s.Button, Ht(36f))) Defer(() => OpenPanelNow(Panel.Regions));
                GUILayout.EndScrollView();
                return;
            }

            DrawModelSelector();
            Divider();

            if (plannerDestination == null) DrawSuggestions();
            else DrawQuote();

            GUILayout.EndScrollView();
        }

        void DrawModelSelector()
        {
            BusModelData model = PlannerModel;
            if (model == null) return;

            GUILayout.Label("Planlanan araç", s.Dim);
            GUILayout.BeginHorizontal();
            if (EnabledButton(plannerModelIndex > 0, "<", s.Button, Wd(40f), Ht(34f)))
                Defer(() => ChangePlannerModel(-1));
            UIBusArt.Layout(model, 40f * u);
            Sp(4f);
            GUILayout.BeginVertical();
            GUILayout.Label($"{model.DisplayName} · {SegmentName(model.Segment)}", s.Bold);
            GUILayout.Label($"{model.Seats} koltuk · rahat menzil {Fmt.Km(model.ComfortableRangeKm)}", s.Small);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (EnabledButton(plannerModelIndex < plannerModels.Count - 1, ">", s.Button, Wd(40f), Ht(34f)))
                Defer(() => ChangePlannerModel(+1));
            GUILayout.EndHorizontal();
        }

        void ChangePlannerModel(int delta)
        {
            plannerModelIndex = Mathf.Clamp(plannerModelIndex + delta, 0, plannerModels.Count - 1);
            suggestionsDirty = true;
            if (plannerDestination != null) RecomputeQuote();
        }

        void DrawSuggestions()
        {
            GUILayout.Label("2) Haritada VARIŞ şehrine tıklayın veya önerilerden seçin:", s.Bold);
            if (suggestionsDirty || suggestionsOrigin != plannerOrigin || suggestionsModel != PlannerModel)
            {
                // Hesaplama yalnızca Layout geçişinde yapılır ki aynı karede Repaint ile tutarlı olsun.
                if (Event.current.type == EventType.Layout) RecomputeSuggestions();
            }

            if (suggestions.Count == 0)
            {
                GUILayout.Label("Uygun hedef bulunamadı (hat limiti, bölge lisansı veya mesafe).", s.WrapDim);
                return;
            }

            GUILayout.Label("En kârlı tahmini hedefler (bu araçla):", s.Dim);
            foreach (var (city, quote) in suggestions)
            {
                string profit = $"{Fmt.Signed(quote.Forecast.Profit)}/gün";
                string text = $"{city.DisplayName}  ·  {Fmt.Km(quote.DistanceKm)}  ·  {profit}";
                if (GUILayout.Button(text, s.Ghost, Ht(34f)))
                {
                    CityData selected = city;
                    Defer(() =>
                    {
                        plannerDestination = selected;
                        RecomputeQuote();
                    });
                }
            }
        }

        void DrawQuote()
        {
            if (quoteDirty && Event.current.type == EventType.Layout) RecomputeQuote();
            if (!plannerQuoteValid) return;
            RouteQuote quote = plannerQuote;
            BusModelData model = PlannerModel;

            GUILayout.BeginHorizontal();
            GUILayout.BeginHorizontal();
            LayoutCityIcon(plannerDestination, 24f, 2f);
            ColorLabel($"Varış: {plannerDestination.DisplayName}", UIStyles.Orange, s.H2);
            LayoutIcon(UIIcons.Flag(plannerDestination.Region.Id), 20f, 4f);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Değiştir", s.Ghost, Wd(90f)))
                Defer(() => { plannerDestination = null; plannerQuoteValid = false; });
            GUILayout.EndHorizontal();

            session.Fleet.GetIdleBuses(busBuffer);
            int idleSame = busBuffer.FindAll(b => b.Model == model).Count;

            GUILayout.BeginVertical(s.Card);
            KeyValue("Mesafe / süre", $"{Fmt.Km(quote.DistanceKm)} · {Fmt.Hours(quote.TravelHours)}", icon: UIIcon.Ruler);
            KeyValue("Tahmini talep", $"~{Fmt.Num(quote.Forecast.Demand)} yolcu/gün", icon: UIIcon.People);
            KeyValue("Önerilen bilet", Fmt.Price(quote.SuggestedPrice), UIStyles.Blue, UIIcon.Ticket);
            KeyValue("Önerilen otobüs", $"{quote.RecommendedBusCount} × {model.DisplayName}", icon: UIIcon.Fleet);
            KeyValue("Garajda bekleyen", $"{busBuffer.Count} otobüs ({idleSame} adet bu model)", icon: UIIcon.Building);
            KeyValue("Tahmini doluluk", Fmt.Pct(quote.Forecast.LoadFactor), icon: UIIcon.Seat);
            KeyValue("Tahmini kâr", $"{Fmt.Signed(quote.Forecast.Profit)}/gün", quote.Forecast.Profit >= 0 ? UIStyles.Green : UIStyles.Red, UIIcon.ChartUp);
            KeyValue("Hat lisansı", Fmt.Money(quote.LicenseCost), icon: UIIcon.Log);
            GUILayout.EndVertical();

            if (model.ComfortableRangeKm < quote.DistanceKm)
                ColorLabel($"Uyarı: {model.DisplayName} için hat uzun (rahat menzil {Fmt.Km(model.ComfortableRangeKm)}); yolcular konforsuz bulur, talep düşer.", UIStyles.Orange, s.Wrap);

            GUILayout.Label("Tahminler 30 günlük olgun hat içindir; yeni hatta talep ilk günlerde düşüktür.", s.WrapDim);
            assignIdleOnOpen = GUILayout.Toggle(assignIdleOnOpen, " Garajdaki otobüsleri bu hatta ata", s.Toggle, Ht(32f));

            if (!quote.IsValid) ColorLabel(quote.Error, UIStyles.Red, s.Wrap);
            Sp(6f);
            if (EnabledButton(quote.IsValid, $"HATTI AÇ  ·  lisans {Fmt.Money(quote.LicenseCost)}", s.Primary, Ht(48f)))
                Defer(OpenRouteFromPlanner);
        }

        void OpenRouteFromPlanner()
        {
            if (!plannerQuoteValid) return;
            CityData a = plannerOrigin, b = plannerDestination;
            var result = session.Routes.TryOpenRoute(a, b, plannerQuote.SuggestedPrice, out RouteState route);
            if (!result)
            {
                Toast(result.Message, UIStyles.Red);
                return;
            }

            int assigned = assignIdleOnOpen ? AssignIdleBuses(route, plannerQuote.RecommendedBusCount, PlannerModel) : 0;
            Toast(assigned > 0
                    ? $"{RouteName(route)} hattı açıldı · {assigned} otobüs atandı"
                    : $"{RouteName(route)} hattı açıldı · otobüs atamayı unutmayın!",
                assigned > 0 ? UIStyles.Green : UIStyles.Orange);

            goals.Evaluate();
            OpenRoutePanelNow(route, assigned > 0 ? RouteTab.Price : RouteTab.Buses);
        }

        /// <summary>Önce planlanan modelden, sonra en yüksek kademeden başlayarak boştaki otobüsleri atar.</summary>
        int AssignIdleBuses(RouteState route, int count, BusModelData preferred)
        {
            session.Fleet.GetIdleBuses(busBuffer);
            busBuffer.Sort((x, y) =>
            {
                int px = x.Model == preferred ? 0 : 1;
                int py = y.Model == preferred ? 0 : 1;
                return px != py ? px.CompareTo(py) : y.Model.Tier.CompareTo(x.Model.Tier);
            });

            int assigned = 0;
            var ids = new List<string>();
            foreach (var bus in busBuffer) ids.Add(bus.busId);
            foreach (var id in ids)
            {
                if (assigned >= count) break;
                if (session.Routes.TryAssignBus(id, route.routeId)) assigned++;
            }
            return assigned;
        }
    }
}
