using System.Collections.Generic;
using BusTycoon.Data;
using BusTycoon.Routes;
using BusTycoon.Urban;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// Şehir içi (dolmuş) hat planlayıcı: şehir haritasında iki durağa dokunulur, hat gerçek sokaklardan en kısa yoldan
    /// gider; yol üstündeki duraklar ve ünlü mekânlar sayılır, teklif (talep, tam hat bileti, araç sayısı, kâr) sağ
    /// panelde görünür; "HATTI AÇ" ruhsatı öder ve garajdaki araçları atar. Haritada bir durağa dokunmak planlayıcıyı o
    /// duraktan başlatır.
    /// </summary>
    public sealed partial class GameUI
    {
        /// <summary>Planlayıcı şehir içi kipte (Panel.Planner açık ve şehir haritası gösteriliyor).</summary>
        bool urbanPlanning;
        UrbanStop urbanOrigin, urbanDestination;
        readonly UrbanLinePlan urbanPlan = new UrbanLinePlan();
        RouteQuote urbanQuote;
        bool urbanQuoteValid;
        int cityRoutesSignature = int.MinValue;
        /// <summary>Planlayıcının teklif kartı (vitrin görüntüsünde kaydırılıp gösterilir).</summary>
        VisualElement tkUrbanOffer;
        readonly List<(string id, Vector2 a, Vector2 b, Color color, int vehicles)> cityRouteBuffer = new List<(string, Vector2, Vector2, Color, int)>();

        bool UrbanPlannerOpen => urbanPlanning && panel == Panel.Planner;

        /// <summary>Şehir içi planlayıcıyı açar (isteğe bağlı başlangıç durağıyla); şehir haritasına geçer.</summary>
        void OpenUrbanPlanner(UrbanStop origin = null)
        {
            if (mode != Mode.Playing || !HasCityMap) return;
            tkHome = false;
            cityMapWanted = true;
            panel = Panel.Planner;
            urbanPlanning = true;
            plannerOrigin = plannerDestination = null;
            plannerQuoteValid = false;
            RefreshPlannerModels(urban: true);
            urbanOrigin = origin;
            urbanDestination = null;
            urbanQuoteValid = false;
            urbanPlan.Clear();
            SyncUrbanPreview();
            tkSheetDirty = true;
        }

        /// <summary>"Yeni hat" düğmeleri: merkez şehrin sokak haritası varsa dolmuş hattı, yoksa şehirlerarası planlayıcı.</summary>
        void OpenNewRoutePlanner()
        {
            if (HasCityMap && (cityMapWanted || !MapShown)) OpenUrbanPlanner();
            else OpenPlannerNow(null);
        }

        void CloseUrbanPlanner()
        {
            urbanPlanning = false;
            urbanOrigin = urbanDestination = null;
            urbanQuoteValid = false;
            urbanPlan.Clear();
            SyncUrbanPreview();
        }

        void UrbanStopClicked(UrbanStop stop)
        {
            if (urbanOrigin == null || (urbanDestination != null && stop != urbanDestination))
            {
                // Başlangıç seçimi; iki durak seçiliyken başka durağa dokunmak yeniden başlatır
                urbanOrigin = stop;
                urbanDestination = null;
                urbanQuoteValid = false;
                urbanPlan.Clear();
            }
            else if (stop == urbanOrigin)
            {
                urbanOrigin = urbanDestination = null;
                urbanQuoteValid = false;
                urbanPlan.Clear();
            }
            else
            {
                urbanDestination = stop;
                RecomputeUrbanQuote();
            }
            SyncUrbanPreview();
            tkSheetDirty = true;
        }

        void RecomputeUrbanQuote()
        {
            urbanQuoteValid = false;
            CityData city = CurrentCity;
            if (urbanOrigin == null || urbanDestination == null || cityView.Map == null || city == null) return;
            UrbanPlanner.TryPlan(cityView.Map, urbanOrigin, urbanDestination, city, session.Economy, urbanPlan);
            BusModelData model = PlannerModel;
            if (urbanPlan.IsValid && model != null)
            {
                urbanQuote = session.Routes.QuoteUrbanRoute(city, urbanPlan, model);
                urbanQuoteValid = true;
            }
        }

        void SyncUrbanPreview()
        {
            if (cityView == null) return;
            bool show = UrbanPlannerOpen;
            cityView.SetPreview(show ? urbanOrigin : null, show ? urbanDestination : null, show && urbanPlan.IsValid ? urbanPlan.Path : null);
        }

        int UrbanPlannerSignature()
        {
            int signature = 17;
            signature = signature * 31 + (urbanOrigin?.Index ?? -1);
            signature = signature * 31 + (urbanDestination?.Index ?? -1);
            signature = signature * 31 + plannerModelIndex;
            signature = signature * 31 + (urbanQuoteValid ? Mathf.RoundToInt(urbanQuote.Forecast.Profit) : 0);
            signature = signature * 31 + (assignIdleOnOpen ? 1 : 0) + session.Fleet.Buses.Count * 3 + session.Routes.Routes.Count * 1000;
            signature = signature * 31 + (cityView != null && cityView.Ready ? 1 : 0);
            return signature;
        }

        void BuildUrbanPlanner(VisualElement body)
        {
            tkUrbanOffer = null;
            CityData city = CurrentCity;
            SheetHeader("Yeni dolmuş hattı", $"{city?.DisplayName} · iki durak seç");
            if (cityView == null || !cityView.Ready)
            {
                Ui.Text("bt-item__text", cityView != null && cityView.Failed ? "Şehir haritası yüklenemedi." : "Şehir haritası yükleniyor…", UiWeight.Regular, body);
                return;
            }

            if (urbanOrigin == null)
            {
                Ui.Text("bt-step-title", "1) Haritada BAŞLANGIÇ durağına dokun.", UiWeight.Bold, body);
                Ui.Text("bt-item__text", "İpucu: Altın duraklar ilçe merkezleri: yolcusu çok. Ünlü mekânların (gri) yakınından geçen hat daha çok yolcu taşır.",
                    UiWeight.Regular, body);
                return;
            }
            if (urbanDestination == null)
            {
                StopRow(body, "Başlangıç", urbanOrigin, "bt-endpoint--origin", ResetUrbanEndpoints);
                Ui.Text("bt-step-title", "2) Haritada VARIŞ durağına dokun.", UiWeight.Bold, body);
                Ui.Text("bt-item__text", $"Hat sokaklardan en kısa yoldan gider; {UrbanPlanner.MinMeters / 1000f:0.#}–{UrbanPlanner.MaxMeters / 1000f:0} km arası olmalı. En kârlıları genelde 6–15 km.",
                    UiWeight.Regular, body);
                return;
            }
            EndpointsRow(body);

            if (!urbanPlan.IsValid)
            {
                Ui.Text("bt-item__text is-down", urbanPlan.Error ?? "Güzergâh bulunamadı.", UiWeight.Bold, body);
                return;
            }
            BuildModelSelector(body);
            if (!urbanQuoteValid) RecomputeUrbanQuote();
            if (!urbanQuoteValid) return;
            RouteQuote quote = urbanQuote;
            BusModelData model = PlannerModel;
            session.Fleet.GetIdleBuses(busBuffer);

            // Sade teklif: yalnız karar için gerekenler (uzunluk, yolcu, bilet, doluluk, kâr, ruhsat)
            VisualElement card = Ui.El("bt-item", body);
            tkUrbanOffer = card;
            KeyValue(card, "Uzunluk", $"{Fmt.Km(quote.DistanceKm)} · {Fmt.Hours(quote.TravelHours)}", null, UIIcon.Ruler);
            KeyValue(card, "Yolcu", $"~{Fmt.Num(quote.Forecast.Demand)}/gün", null, UIIcon.People);
            KeyValue(card, "Tam hat bileti", $"{Fmt.Price(quote.SuggestedPrice)} (ort. {Fmt.Price(quote.SuggestedPrice * session.Economy.UrbanRideShare)})", "is-blue", UIIcon.Ticket);
            KeyValue(card, "Doluluk", Fmt.Pct(quote.Forecast.LoadFactor), null, UIIcon.Seat);
            KeyValue(card, "Tahmini kâr", $"{Fmt.Signed(quote.Forecast.Profit)}/gün", Tone(quote.Forecast.Profit), UIIcon.ChartUp);
            KeyValue(card, "Ruhsat", Fmt.Money(quote.LicenseCost), null, UIIcon.Log);
            if (urbanPlan.Landmarks.Count > 0)
            {
                var names = new List<string>();
                for (int i = 0; i < Mathf.Min(3, urbanPlan.Landmarks.Count); i++) names.Add(urbanPlan.Landmarks[i].Name);
                Ui.Text("bt-item__text", "Yakından geçer: " + string.Join(", ", names) + (urbanPlan.Landmarks.Count > 3 ? "…" : ""), UiWeight.Regular, body);
                string highlights = EventHighlights(UrbanEvents.KindFame(urbanPlan.Landmarks));
                if (highlights != null) Ui.Text("bt-item__text is-gold", "Önemli günlerde: " + highlights, UiWeight.Bold, body);
            }

            // Panelin altında sabit (kaydırmadan görünür): araç atama ve "Hattı aç"
            VisualElement footer = tkSheetFooter;
            Button assign = Ui.Button("bt-check", string.Empty, () =>
            {
                assignIdleOnOpen = !assignIdleOnOpen;
                tkSheetDirty = true;
            }, footer);
            Ui.Toggle(assign, "is-on", assignIdleOnOpen);
            Ui.El("bt-check__box", assign);
            Ui.Text("bt-check__label", busBuffer.Count > 0
                ? $"Garajdaki {busBuffer.Count} aracı bu hatta ata (önerilen {quote.RecommendedBusCount})"
                : $"Garajda araç yok (önerilen {quote.RecommendedBusCount} × {model.DisplayName})", UiWeight.Regular, assign);

            if (!quote.IsValid) Ui.Text("bt-item__text is-down", quote.Error, UiWeight.Bold, footer);
            Button open = ActionButton(footer, $"HATTI AÇ  ·  ruhsat {Fmt.Money(quote.LicenseCost)}", () =>
            {
                OpenUrbanRouteFromPlanner();
                tkSheetDirty = true;
            }, primary: true, enabled: quote.IsValid);
            open.name = "urban-open";
            open.AddToClassList("bt-btn--cta");
        }

        void ResetUrbanEndpoints()
        {
            urbanOrigin = urbanDestination = null;
            urbanQuoteValid = false;
            urbanPlan.Clear();
            SyncUrbanPreview();
            tkSheetDirty = true;
        }

        /// <summary>İki durak seçiliyken tek satır (yer kazanır: telefonda teklif kaydırmadan görünür).</summary>
        void EndpointsRow(VisualElement body)
        {
            VisualElement row = Ui.El("bt-row bt-endpoint bt-endpoint--pair", body);
            VisualElement names = Ui.El("bt-col bt-grow", row);
            foreach (var (stop, tone) in new[] { (urbanOrigin, "is-origin"), (urbanDestination, "is-destination") })
            {
                VisualElement line = Ui.El("bt-row bt-endpoint__line", names);
                Ui.El("bt-endpoint__dot " + tone, line);
                Ui.Text("bt-endpoint__label", stop.Name, UiWeight.Black, line);
            }
            Button button = ActionButton(row, "Değiştir", ResetUrbanEndpoints);
            button.AddToClassList("bt-btn--ghost");
        }

        void StopRow(VisualElement body, string label, UrbanStop stop, string tone, System.Action change)
        {
            VisualElement row = Ui.El("bt-row bt-endpoint " + tone, body);
            Ui.El("bt-endpoint__dot", row);
            Ui.Text("bt-endpoint__label", $"{label}: {stop.Name}", UiWeight.Black, row);
            Ui.El("bt-grow", row);
            Button button = ActionButton(row, "Değiştir", change);
            button.AddToClassList("bt-btn--ghost");
        }

        void OpenUrbanRouteFromPlanner()
        {
            if (!urbanQuoteValid || !urbanPlan.IsValid) return;
            CityData city = CurrentCity;
            var result = session.Routes.TryOpenUrbanRoute(city, urbanPlan, urbanQuote.SuggestedPrice, out RouteState route);
            if (!result)
            {
                Toast(result.Message, UIStyles.Red);
                return;
            }
            int assigned = assignIdleOnOpen ? AssignIdleBuses(route, urbanQuote.RecommendedBusCount, PlannerModel) : 0;
            Toast(assigned > 0
                    ? $"{RouteName(route)} hattı açıldı · {assigned} araç atandı"
                    : $"{RouteName(route)} hattı açıldı · araç atamayı unutmayın!",
                assigned > 0 ? UIStyles.Green : UIStyles.Orange, Sfx.Horn);
            goals.Evaluate();
            CloseUrbanPlanner();
            cityRoutesSignature = int.MinValue;
            OpenRoutePanelNow(route, assigned > 0 ? RouteTab.Price : RouteTab.Buses);
        }

        /// <summary>Hatta bugün çalışan araç (teslimattaki ve arızalı sayılmaz).</summary>
        int ActiveVehicles(RouteState route)
        {
            session.Fleet.GetBusesOnRoute(route.routeId, busBuffer);
            int count = 0;
            foreach (var bus in busBuffer) if (bus.IsAvailableToday) count++;
            return count;
        }

        /// <summary>Şehir haritasındaki hatlar: hat/araç/renk değişince CityView'a yazılır.</summary>
        void SyncCityRoutes()
        {
            if (cityView == null || !cityView.Ready || session.Routes == null) return;
            string cityId = cityView.City.Id;
            ResolveContractSites();
            int signature = cityId.GetHashCode();
            AddContractLines(cityId, ref signature, collect: false);
            foreach (var route in session.Routes.Routes)
            {
                if (!route.IsUrban || route.urbanCityId != cityId) continue;
                signature = signature * 31 + route.routeId.GetHashCode();
                signature = signature * 31 + session.Fleet.CountBusesOnRoute(route.routeId) + (route.IsSettingUp ? 1000 : 0);
                signature = signature * 31 + world.RouteIdentityColor(route.routeId).GetHashCode();
            }
            if (signature == cityRoutesSignature) return;
            cityRoutesSignature = signature;
            cityRouteBuffer.Clear();
            foreach (var route in session.Routes.Routes)
            {
                if (!route.IsUrban || route.urbanCityId != cityId) continue;
                cityRouteBuffer.Add((route.routeId, new Vector2(route.urbanAX, route.urbanAY), new Vector2(route.urbanBX, route.urbanBY),
                    world.RouteIdentityColor(route.routeId), route.IsSettingUp ? 0 : ActiveVehicles(route)));
            }
            AddContractLines(cityId, ref signature, collect: true);
            cityView.SetRoutes(cityRouteBuffer);
        }
    }
}
