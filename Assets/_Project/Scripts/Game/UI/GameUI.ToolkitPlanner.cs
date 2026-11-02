using BusTycoon.Data;
using BusTycoon.Routes;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// Yeni arayüzde hat planlayıcı: kalkış → (araç) → varış önerileri ya da haritadan seçilen varış → teklif kartı →
    /// HATTI AÇ. Öneriler kare başına birkaç teklifle hesaplanır (takılma olmasın); bitince liste kurulur.
    /// </summary>
    public sealed partial class GameUI
    {
        /// <summary>Planlayıcı açıkken her kare: önerileri kare kare hesaplar, teklifi tazeler.</summary>
        void TickToolkitPlanner()
        {
            if (panel != Panel.Planner || plannerOrigin == null) return;
            if (plannerDestination == null)
            {
                bool stale = session.State != null && session.State.currentDay - suggestionsDay >= SuggestionRefreshDays;
                if (suggestionsDirty || stale || suggestionsOrigin != plannerOrigin || suggestionsModel != PlannerModel)
                {
                    StartSuggestions();
                    tkSheetDirty = true;
                }
                if (suggestionsComputing)
                {
                    ContinueSuggestions();
                    if (!suggestionsComputing) tkSheetDirty = true; // bitti: liste kurulsun
                }
            }
            else if (quoteDirty)
            {
                RecomputeQuote();
                tkSheetDirty = true;
            }
        }

        int PlannerSignature()
        {
            int signature = plannerOrigin?.Id.GetHashCode() ?? 0;
            signature = signature * 31 + (plannerDestination?.Id.GetHashCode() ?? 0);
            signature = signature * 31 + plannerModelIndex;
            signature = signature * 31 + suggestions.Count * 2 + (suggestionsComputing ? 1 : 0);
            signature = signature * 31 + (plannerQuoteValid ? 1 : 0) + (assignIdleOnOpen ? 2 : 0);
            signature = signature * 31 + session.Fleet.Buses.Count + session.Routes.Routes.Count * 1000;
            if (plannerOrigin != null) signature = signature * 31 + (session.Progression.IsRegionUnlocked(plannerOrigin.Region) ? 1 : 0);
            if (plannerQuoteValid) signature = signature * 31 + Mathf.RoundToInt(plannerQuote.Forecast.Profit);
            return signature;
        }

        void BuildPlanner(VisualElement body)
        {
            SheetHeader("Yeni Hat Planla", "Kalkış ve varış şehirlerini haritadan seçin.");

            if (plannerOrigin == null)
            {
                Ui.Text("bt-step-title", "1) Haritada KALKIŞ şehrine tıklayın.", UiWeight.Bold, body);
                CityData hq = session.Headquarters;
                if (hq != null)
                {
                    Button start = ActionButton(body, $"Merkez ofisten başla: {hq.DisplayName}", () =>
                    {
                        plannerOrigin = hq;
                        suggestionsDirty = true;
                        tkSheetDirty = true;
                    });
                    start.AddToClassList("bt-btn--block");
                }
                Ui.Text("bt-item__text", "İpucu: Büyük ve yakın şehir çiftleri (ör. İstanbul–Bursa, İstanbul–Kocaeli) minibüsle bile kârlıdır.", UiWeight.Regular, body);
                return;
            }

            CityRow(body, "Kalkış", plannerOrigin, "bt-endpoint--origin", () =>
            {
                plannerOrigin = plannerDestination = null;
                plannerQuoteValid = false;
                tkSheetDirty = true;
            });

            if (!session.Progression.IsRegionUnlocked(plannerOrigin.Region))
            {
                Ui.Text("bt-item__text is-warning-text", $"{plannerOrigin.Region.DisplayName} için işletme lisansınız yok.", UiWeight.Bold, body);
                Button regions = ActionButton(body, "Bölgeler panelini aç", () => OpenPanelNow(Panel.Regions));
                regions.AddToClassList("bt-btn--block");
                return;
            }

            BuildModelSelector(body);
            if (plannerDestination == null) BuildSuggestions(body);
            else BuildQuote(body);
        }

        void CityRow(VisualElement body, string label, CityData city, string tone, System.Action change)
        {
            VisualElement row = Ui.El("bt-row bt-endpoint " + tone, body);
            Ui.El("bt-endpoint__dot", row);
            Ui.Text("bt-endpoint__label", $"{label}: {city.DisplayName}", UiWeight.Black, row);
            Ui.Icon(UIIcons.Flag(city.Region.Id), "bt-endpoint__flag", row);
            Ui.El("bt-grow", row);
            Button button = ActionButton(row, "Değiştir", change);
            button.AddToClassList("bt-btn--ghost");
        }

        void BuildModelSelector(VisualElement body)
        {
            BusModelData model = PlannerModel;
            if (model == null) return;
            Ui.Text("bt-caps bt-model-caption", "PLANLANAN ARAÇ", UiWeight.Bold, body);
            VisualElement row = Ui.El("bt-item bt-row bt-model-picker", body);
            Button previous = Ui.Button("bt-step", "‹", () => { ChangePlannerModel(-1); tkSheetDirty = true; }, row);
            previous.SetEnabled(plannerModelIndex > 0);
            VisualElement art = Ui.El("bt-bus__art", row);
            Sprite sprite = UIBusArt.Sprite(model);
            if (sprite != null) art.style.backgroundImage = new StyleBackground(sprite);
            VisualElement info = Ui.El("bt-col bt-grow", row);
            Ui.Text("bt-item__title", $"{model.DisplayName} · {SegmentName(model.Segment)}", UiWeight.Bold, info);
            Ui.Text("bt-detail__text", $"{model.Seats} koltuk · rahat menzil {Fmt.Km(model.ComfortableRangeKm)}", UiWeight.Regular, info);
            Button next = Ui.Button("bt-step", "›", () => { ChangePlannerModel(+1); tkSheetDirty = true; }, row);
            next.SetEnabled(plannerModelIndex < plannerModels.Count - 1);
        }

        void BuildSuggestions(VisualElement body)
        {
            Ui.Text("bt-step-title", "2) Haritada VARIŞ şehrine tıklayın veya önerilerden seçin:", UiWeight.Bold, body);
            if (suggestions.Count == 0)
            {
                Ui.Text("bt-item__text", suggestionsComputing ? "Öneriler hesaplanıyor…"
                    : "Uygun hedef bulunamadı (hat limiti, bölge lisansı veya mesafe).", UiWeight.Regular, body);
                return;
            }
            Ui.Text("bt-caps bt-model-caption", "EN KÂRLI TAHMİNİ HEDEFLER (BU ARAÇLA)", UiWeight.Bold, body);
            foreach (var (city, quote) in suggestions)
            {
                CityData selected = city;
                Button button = Ui.Button("bt-suggestion", string.Empty, () =>
                {
                    plannerDestination = selected;
                    RecomputeQuote();
                    tkSheetDirty = true;
                }, body);
                Ui.Icon(UIIcons.Flag(city.Region.Id), "bt-suggestion__flag", button);
                Ui.Text("bt-suggestion__city bt-grow", city.DisplayName, UiWeight.Bold, button);
                Ui.Text("bt-suggestion__km", Fmt.Km(quote.DistanceKm), UiWeight.Regular, button);
                Label profit = Ui.Text("bt-suggestion__profit", $"{Fmt.Signed(quote.Forecast.Profit)}/gün", UiWeight.Black, button);
                profit.AddToClassList(Tone(quote.Forecast.Profit));
            }
        }

        void BuildQuote(VisualElement body)
        {
            if (quoteDirty) RecomputeQuote();
            if (!plannerQuoteValid) return;
            RouteQuote quote = plannerQuote;
            BusModelData model = PlannerModel;

            CityRow(body, "Varış", plannerDestination, "bt-endpoint--destination", () =>
            {
                plannerDestination = null;
                plannerQuoteValid = false;
                tkSheetDirty = true;
            });

            session.Fleet.GetIdleBuses(busBuffer);
            int idleSame = 0;
            foreach (var bus in busBuffer) if (bus.Model == model) idleSame++;

            VisualElement card = Ui.El("bt-item", body);
            KeyValue(card, "Mesafe / süre", $"{Fmt.Km(quote.DistanceKm)} · {Fmt.Hours(quote.TravelHours)}", null, UIIcon.Ruler);
            KeyValue(card, "Tahmini talep", $"~{Fmt.Num(quote.Forecast.Demand)} yolcu/gün", null, UIIcon.People);
            KeyValue(card, "Önerilen bilet", Fmt.Price(quote.SuggestedPrice), "is-blue", UIIcon.Ticket);
            KeyValue(card, "Önerilen otobüs", $"{quote.RecommendedBusCount} × {model.DisplayName}", null, UIIcon.Fleet);
            KeyValue(card, "Garajda bekleyen", $"{busBuffer.Count} otobüs ({idleSame} adet bu model)", null, UIIcon.Building);
            KeyValue(card, "Tahmini doluluk", Fmt.Pct(quote.Forecast.LoadFactor), null, UIIcon.Seat);
            KeyValue(card, "Tahmini kâr", $"{Fmt.Signed(quote.Forecast.Profit)}/gün", Tone(quote.Forecast.Profit), UIIcon.ChartUp);
            KeyValue(card, "Hat lisansı", Fmt.Money(quote.LicenseCost), null, UIIcon.Log);

            if (model.ComfortableRangeKm < quote.DistanceKm)
                Ui.Text("bt-item__text is-warning-text", $"Uyarı: {model.DisplayName} için hat uzun (rahat menzil {Fmt.Km(model.ComfortableRangeKm)}); yolcular konforsuz bulur, talep düşer.", UiWeight.Bold, body);
            Ui.Text("bt-item__text", "Tahminler 30 günlük olgun hat içindir; yeni hatta talep ilk günlerde düşüktür.", UiWeight.Regular, body);

            Button assign = Ui.Button("bt-check", string.Empty, () =>
            {
                assignIdleOnOpen = !assignIdleOnOpen;
                tkSheetDirty = true;
            }, body);
            Ui.Toggle(assign, "is-on", assignIdleOnOpen);
            Ui.El("bt-check__box", assign);
            Ui.Text("bt-check__label", "Garajdaki otobüsleri bu hatta ata", UiWeight.Regular, assign);

            if (!quote.IsValid) Ui.Text("bt-item__text is-down", quote.Error, UiWeight.Bold, body);
            Button open = ActionButton(body, $"HATTI AÇ  ·  lisans {Fmt.Money(quote.LicenseCost)}", () =>
            {
                OpenRouteFromPlanner();
                tkSheetDirty = true;
            }, primary: true, enabled: quote.IsValid);
            open.AddToClassList("bt-btn--cta");
        }
    }
}
