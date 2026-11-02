using BusTycoon.Economy;
using BusTycoon.Fleet;
using BusTycoon.Routes;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// Yeni arayüzde hat paneli: durum önerisi, Fiyat / Otobüsler / Geçmiş sekmeleri. Fiyat kaydırıcısı oynarken panel
    /// yeniden kurulmaz: fiyat, bölge, tahmin değerleri, düğmeler ve fiyat–kâr eğrisi yerinde güncellenir.
    /// </summary>
    public sealed partial class GameUI
    {
        Slider tkPriceSlider;
        VisualElement tkPriceChart;
        Label tkPriceValue, tkPriceZone, tkFcPassengers, tkFcLoad, tkFcSpill, tkFcRevenue, tkFcCost, tkFcProfit, tkFcDelta;
        Button tkPriceUndo, tkPriceApply;
        int tkRouteBuiltDay = -1;

        int RouteSignature()
        {
            if (selectedRoute == null) return 0;
            int signature = selectedRoute.routeId.GetHashCode();
            signature = signature * 31 + (int)routeTab;
            // Fiyat değiştirilirken (kaydırıcı sürüklenirken) gün geçti diye panel yeniden kurulmaz
            bool editingPrice = routeTab == RouteTab.Price && Mathf.Abs(pendingPrice - selectedRoute.ticketPrice) > 0.001f;
            signature = signature * 31 + (editingPrice ? tkRouteBuiltDay : session.State.currentDay);
            signature = signature * 31 + session.Fleet.CountBusesOnRoute(selectedRoute.routeId) * 7 + session.Fleet.Buses.Count;
            signature = signature * 31 + Mathf.RoundToInt(selectedRoute.ticketPrice * 100f);
            signature = signature * 31 + (showAdvanced ? 1 : 0) + (selectedRoute.autoSchedule ? 2 : 0) + Mathf.RoundToInt(selectedRoute.manualUtilization * 100f) * 4;
            signature = signature * 31 + (session.Routes.GetRoute(selectedRoute.routeId) != null ? 1 : 0);
            return signature;
        }

        void BuildRoutePanel(VisualElement body)
        {
            tkPriceSlider = null;
            tkRouteBuiltDay = session.State.currentDay;
            if (selectedRoute == null || session.Routes.GetRoute(selectedRoute.routeId) == null)
            {
                SheetHeader("Hat", null);
                Ui.Text("bt-empty", "Bu hat artık yok.", UiWeight.Regular, body);
                return;
            }
            EnsureRouteAnalysisNow();
            if (estimator == null) return;

            int busCount = session.Fleet.CountBusesOnRoute(selectedRoute.routeId);
            SheetHeader(RouteName(selectedRoute), $"{Fmt.Km(selectedRoute.distanceKm)} · hat no {selectedRoute.routeId} · {busCount} otobüs");

            RouteInsight insight = RouteAdvisor.GetTopInsight(selectedRoute, busCount);
            VisualElement advice = Ui.El("bt-item bt-advice", body);
            VisualElement row = Ui.El("bt-row", advice);
            Ui.Text("bt-pill " + InsightClass(insight.Severity), InsightTag(insight.Severity), UiWeight.Bold, row);
            Label message = Ui.Text("bt-item__text bt-grow bt-advice__text", insight.Message, UiWeight.Regular, row);
            message.AddToClassList(InsightClass(insight.Severity) + "-text");
            if (insight.Action == InsightAction.AssignBus || insight.Action == InsightAction.AddBus)
                ActionButton(row, "Otobüs ekle", () => { routeTab = RouteTab.Buses; tkSheetDirty = true; }, primary: true);
            else if (insight.Action == InsightAction.UpgradeBus)
                ActionButton(row, "Galeri", () => OpenDealershipNow(selectedRoute));

            VisualElement tabs = Ui.El("bt-tabs-inline", body);
            RouteTabButton(tabs, "Fiyat", RouteTab.Price);
            RouteTabButton(tabs, $"Otobüsler ({busCount})", RouteTab.Buses);
            RouteTabButton(tabs, "Geçmiş", RouteTab.History);

            switch (routeTab)
            {
                case RouteTab.Price: BuildPriceTab(body); break;
                case RouteTab.Buses: BuildBusesTab(body); break;
                case RouteTab.History: BuildHistoryTab(body); break;
            }
        }

        void RouteTabButton(VisualElement parent, string text, RouteTab tab)
        {
            Button button = Ui.Button("bt-tab", text, () => { routeTab = tab; tkSheetDirty = true; }, parent);
            Ui.Toggle(button, "is-active", routeTab == tab);
        }

        // ------------------------------------------------------------------ Fiyat

        void BuildPriceTab(VisualElement body)
        {
            if (priceChart == null) priceChart = new ChartTexture(640, 220);
            RenderPriceChart();
            tkPriceChart = Ui.El("bt-chart bt-chart--price", body);
            tkPriceChart.style.backgroundImage = new StyleBackground(priceChart.Texture);
            VisualElement legend = Ui.El("bt-row bt-legend", body);
            Ui.Text("bt-legend__text", Fmt.Price(analysis.MinPrice), UiWeight.Regular, legend);
            Ui.Text("bt-legend__text bt-grow bt-legend__center", "turuncu: seçili · yeşil: önerilen · mavi: mevcut", UiWeight.Regular, legend);
            Ui.Text("bt-legend__text", Fmt.Price(analysis.MaxPrice), UiWeight.Regular, legend);

            tkPriceSlider = new Slider(analysis.MinPrice, analysis.MaxPrice);
            tkPriceSlider.AddToClassList("bt-slider");
            tkPriceSlider.focusable = false;
            tkPriceSlider.SetValueWithoutNotify(pendingPrice);
            tkPriceSlider.RegisterValueChangedCallback(evt =>
            {
                SetPendingPrice(evt.newValue);
                UpdatePriceViews();
            });
            body.Add(tkPriceSlider);

            VisualElement stepper = Ui.El("bt-row bt-stepper", body);
            Ui.Button("bt-step", "−", () => { SetPendingPrice(pendingPrice - PriceStep(pendingPrice)); UpdatePriceViews(); }, stepper);
            VisualElement center = Ui.El("bt-col bt-grow bt-stepper__center", stepper);
            tkPriceValue = Ui.Text("bt-price", "", UiWeight.Black, center);
            Ui.Text("bt-legend__text", $"piyasa referansı {Fmt.Price(analysis.ReferencePrice)}", UiWeight.Regular, center);
            Ui.Button("bt-step", "+", () => { SetPendingPrice(pendingPrice + PriceStep(pendingPrice)); UpdatePriceViews(); }, stepper);
            tkPriceZone = Ui.Text("bt-zone", "", UiWeight.Bold, body);

            VisualElement forecast = Ui.El("bt-item", body);
            tkFcPassengers = ForecastRow(forecast, "Yolcu / gün", UIIcon.People);
            tkFcLoad = ForecastRow(forecast, "Doluluk", null);
            tkFcSpill = ForecastRow(forecast, "Kaçan yolcu", null);
            tkFcRevenue = ForecastRow(forecast, "Gelir", UIIcon.Ticket);
            tkFcCost = ForecastRow(forecast, "Gider", UIIcon.Fuel);
            tkFcProfit = ForecastRow(forecast, "Kâr", UIIcon.ChartUp);
            tkFcDelta = ForecastRow(forecast, "Mevcut fiyata göre", null);

            if (selectedRoute.HasHistory)
            {
                RouteDayReport last = selectedRoute.LastReport;
                Ui.Text("bt-item__text", $"Dün ({Fmt.Price(last.ticketPrice)}): {Fmt.Num(last.passengers)} yolcu · doluluk {Fmt.Pct(last.loadFactor)} · kâr {Fmt.Signed(last.Profit)}", UiWeight.Regular, body);
            }
            Ui.Text("bt-item__text", analysis.EstimateError <= 0f
                ? "Pazar araştırması: tam isabetli tahmin"
                : $"Pazar araştırması: tahminler ±%{analysis.EstimateError * 100f:0} sapabilir (seviyeyle iyileşir)", UiWeight.Regular, body);

            VisualElement actions = Ui.El("bt-actions bt-actions--fill", body);
            ActionButton(actions, "Önerilen", () => { SetPendingPrice(analysis.EstimatedBestPrice); UpdatePriceViews(); });
            tkPriceUndo = ActionButton(actions, "Geri al", () => { SetPendingPrice(selectedRoute.ticketPrice); UpdatePriceViews(); });
            tkPriceApply = ActionButton(actions, "UYGULA", () =>
            {
                ApplyPendingPrice();
                tkSheetDirty = true;
            }, primary: true);

            // Gelişmiş: sefer planı
            Button advanced = ActionButton(body, showAdvanced ? "Gelişmiş ayarları gizle" : "Gelişmiş: sefer planı", () =>
            {
                showAdvanced = !showAdvanced;
                tkSheetDirty = true;
            });
            advanced.AddToClassList("bt-btn--ghost");
            if (showAdvanced)
            {
                bool auto = selectedRoute.autoSchedule;
                Button toggle = ActionButton(body, auto ? "Otomatik sefer planı: AÇIK (önerilen)" : "Otomatik sefer planı: KAPALI", () =>
                {
                    session.Routes.SetSchedule(selectedRoute.routeId, !auto, selectedRoute.manualUtilization);
                    tkSheetDirty = true;
                }, primary: auto);
                toggle.AddToClassList("bt-btn--block");
                if (!auto)
                {
                    Label intensity = Ui.Text("bt-item__text", $"Sefer yoğunluğu: {Fmt.Pct(selectedRoute.manualUtilization)} (otobüslerin maksimum kapasitesine göre)", UiWeight.Regular, body);
                    var utilization = new Slider(0.1f, 1f);
                    utilization.AddToClassList("bt-slider");
                    utilization.focusable = false;
                    utilization.SetValueWithoutNotify(selectedRoute.manualUtilization);
                    utilization.RegisterValueChangedCallback(evt =>
                    {
                        float value = Mathf.Round(evt.newValue * 20f) / 20f;
                        if (Mathf.Abs(value - selectedRoute.manualUtilization) < 0.005f) return;
                        session.Routes.SetSchedule(selectedRoute.routeId, false, value);
                        intensity.text = $"Sefer yoğunluğu: {Fmt.Pct(value)} (otobüslerin maksimum kapasitesine göre)";
                    });
                    body.Add(utilization);
                }
                else
                {
                    Ui.Text("bt-item__text", "Sistem sefer sayısını talebe göre ayarlar (hedef doluluk %85). Boş sefer yapılmaz.", UiWeight.Regular, body);
                }
            }
            UpdatePriceViews();
        }

        static Label ForecastRow(VisualElement parent, string key, UIIcon? icon)
        {
            VisualElement row = Ui.El("bt-kv", parent);
            if (icon.HasValue) Ui.Icon(icon.Value, "bt-kv__icon", row);
            Ui.Text("bt-kv__key", key, UiWeight.Regular, row);
            return Ui.Text("bt-kv__value", "", UiWeight.Bold, row);
        }

        static void SetTone(Label label, string tone)
        {
            Ui.Toggle(label, "is-up", tone == "is-up");
            Ui.Toggle(label, "is-down", tone == "is-down");
            Ui.Toggle(label, "is-warning-text", tone == "is-warning-text");
            Ui.Toggle(label, "bt-dim", tone == "bt-dim");
        }

        /// <summary>Kaydırıcı/düğme fiyatı değiştirince: değerler, düğmeler ve eğri yerinde güncellenir.</summary>
        void UpdatePriceViews()
        {
            if (tkPriceSlider == null || selectedRoute == null) return;
            if (!Mathf.Approximately(tkPriceSlider.value, pendingPrice)) tkPriceSlider.SetValueWithoutNotify(pendingPrice);
            if (!Mathf.Approximately(chartRenderedPrice, pendingPrice)) RenderPriceChart();
            bool changed = Mathf.Abs(pendingPrice - selectedRoute.ticketPrice) > 0.001f;
            Ui.SetText(tkPriceValue, Fmt.Price(pendingPrice));
            PriceZone zone = analysis.Classify(pendingPrice, pendingForecast.Profit);
            Ui.SetText(tkPriceZone, PricingAdvisor.Describe(zone));
            SetTone(tkPriceZone, zone == PriceZone.Ideal ? "is-up" : zone == PriceZone.Loss ? "is-down" : "is-warning-text");

            Ui.SetText(tkFcPassengers, Fmt.Num(pendingForecast.Passengers));
            Ui.SetText(tkFcLoad, $"{Fmt.Pct(pendingForecast.LoadFactor)} · {pendingForecast.TripsPerDay:0.#} sefer/gün");
            bool spill = pendingForecast.UnmetDemand > pendingForecast.Demand * 0.1f;
            Ui.SetText(tkFcSpill, spill ? $"~{Fmt.Num(pendingForecast.UnmetDemand)} / gün (otobüs ekleyin)" : "yok");
            SetTone(tkFcSpill, spill ? "is-warning-text" : "bt-dim");
            Ui.SetText(tkFcRevenue, $"{Fmt.Money(pendingForecast.Revenue)} / gün");
            Ui.SetText(tkFcCost, $"{Fmt.Money(pendingForecast.TotalCost)} / gün");
            Ui.SetText(tkFcProfit, $"{Fmt.Signed(pendingForecast.Profit)} / gün");
            SetTone(tkFcProfit, pendingForecast.Profit >= 0 ? "is-up" : "is-down");
            float delta = pendingForecast.Profit - appliedForecast.Profit;
            Ui.SetText(tkFcDelta, changed ? $"{Fmt.Signed(delta)} / gün" : "uygulanan fiyat");
            SetTone(tkFcDelta, !changed ? "bt-dim" : delta >= 0 ? "is-up" : "is-down");
            tkPriceUndo.SetEnabled(changed);
            tkPriceApply.SetEnabled(changed);
        }

        // ------------------------------------------------------------------ Otobüsler

        void BuildBusesTab(VisualElement body)
        {
            session.Fleet.GetBusesOnRoute(selectedRoute.routeId, busBuffer);
            Section(body, $"Hattaki otobüsler ({busBuffer.Count})");
            if (busBuffer.Count == 0) Ui.Text("bt-item__text is-warning-text", "Bu hatta otobüs yok; hat çalışmıyor.", UiWeight.Bold, body);
            foreach (var bus in busBuffer.ToArray())
            {
                VisualElement card = BusSummary(body, bus);
                string id = bus.busId;
                ActionButton(card, "Garaja çek", () =>
                {
                    session.Routes.TryAssignBus(id, null);
                    tkSheetDirty = true;
                });
            }

            session.Fleet.GetIdleBuses(busBuffer);
            Section(body, $"Garajdaki otobüsler ({busBuffer.Count})");
            if (busBuffer.Count == 0) Ui.Text("bt-empty", "Garaj boş. Galeriden otobüs alabilirsiniz.", UiWeight.Regular, body);
            foreach (var bus in busBuffer.ToArray())
            {
                VisualElement card = BusSummary(body, bus);
                string id = bus.busId;
                ActionButton(card, "Bu hatta ata", () =>
                {
                    var result = session.Routes.TryAssignBus(id, selectedRoute.routeId);
                    if (result) Toast($"Otobüs {RouteName(selectedRoute)} hattına atandı.", UIStyles.Green);
                    tkSheetDirty = true;
                }, primary: true);
            }
            Button dealer = ActionButton(body, "Galeriden otobüs al (bu hatta atanır)", () => OpenDealershipNow(selectedRoute));
            dealer.AddToClassList("bt-btn--block");
        }

        VisualElement BusSummary(VisualElement body, BusInstance bus)
        {
            VisualElement card = Ui.El("bt-item bt-row bt-bus-row", body);
            VisualElement art = Ui.El("bt-bus__art", card);
            Sprite sprite = UIBusArt.Sprite(bus.Model);
            if (sprite != null) art.style.backgroundImage = new StyleBackground(sprite);
            VisualElement info = Ui.El("bt-col bt-grow", card);
            Ui.Text("bt-item__title", bus.nickname, UiWeight.Bold, info);
            VisualElement row = Ui.El("bt-row bt-detail", info);
            Ui.Text("bt-detail__text", $"Durum {Fmt.Pct(bus.condition)}", UiWeight.Regular, row);
            VisualElement bar = Ui.El("bt-bar bt-bus__condition", row);
            VisualElement fill = Ui.El("bt-bar__fill", bar);
            fill.AddToClassList(bus.condition > 0.6f ? "bt-bar__fill--green" : bus.condition > 0.35f ? "bt-bar__fill--yellow" : "bt-bar__fill--red");
            Ui.Fill(fill, bus.condition);
            Label status = Ui.Text("bt-detail__text bt-status", !bus.IsAvailableToday ? $"Arızalı ({bus.outOfServiceDays} gün)" : $"{Fmt.Num(bus.odometerKm)} km", UiWeight.Regular, row);
            if (!bus.IsAvailableToday) status.AddToClassList("is-warning-text");
            return card;
        }

        // ------------------------------------------------------------------ Geçmiş

        void BuildHistoryTab(VisualElement body)
        {
            if (!selectedRoute.HasHistory)
            {
                Ui.Text("bt-empty", "Henüz sonuç yok; ilk gün bitince burada görünecek.", UiWeight.Regular, body);
            }
            else
            {
                if (historyChart == null) historyChart = new ChartTexture(640, 180);
                RenderHistoryChart();
                Section(body, $"Günlük kâr (son {selectedRoute.history.Count} gün)");
                VisualElement chart = Ui.El("bt-chart", body);
                chart.style.backgroundImage = new StyleBackground(historyChart.Texture);

                VisualElement table = Ui.El("bt-item bt-table", body);
                TableRow(table, true, "Gün", "Bilet", "Yolcu", "Doluluk", "Kâr", null);
                var history = selectedRoute.history;
                for (int i = history.Count - 1; i >= Mathf.Max(0, history.Count - 7); i--)
                {
                    RouteDayReport r = history[i];
                    TableRow(table, false, (r.day + 1).ToString(), Fmt.Price(r.ticketPrice), Fmt.Num(r.passengers), Fmt.Pct(r.loadFactor),
                        Fmt.Signed(r.Profit), Tone(r.Profit));
                }
            }

            VisualElement totals = Ui.El("bt-item", body);
            KeyValue(totals, "Açılış", $"{selectedRoute.openedDay + 1}. gün");
            KeyValue(totals, "Toplam yolcu", Fmt.Num(selectedRoute.lifetimePassengers));
            KeyValue(totals, "Toplam kâr", Fmt.Signed(selectedRoute.lifetimeProfit), Tone(selectedRoute.lifetimeProfit));
            RouteState route = selectedRoute;
            Button close = ActionButton(body, "Hattı kapat", () => AskConfirm("Hattı kapat",
                $"{RouteName(route)} hattı kapatılacak. Otobüsler garaja döner, lisans bedeli iade edilmez.",
                () =>
                {
                    var result = session.Routes.TryCloseRoute(route.routeId);
                    Toast(result.Message, result ? UIStyles.Orange : UIStyles.Red);
                    if (result) ClosePanel();
                }));
            close.AddToClassList("bt-btn--block");
            close.AddToClassList("is-danger");
        }

        static void TableRow(VisualElement table, bool header, string day, string price, string passengers, string load, string profit, string tone)
        {
            VisualElement row = Ui.El(header ? "bt-row bt-table__head" : "bt-row bt-table__row", table);
            UiWeight weight = header ? UiWeight.Bold : UiWeight.Regular;
            Ui.Text("bt-table__cell bt-table__cell--day", day, weight, row);
            Ui.Text("bt-table__cell", price, weight, row);
            Ui.Text("bt-table__cell", passengers, weight, row);
            Ui.Text("bt-table__cell", load, weight, row);
            Label value = Ui.Text("bt-table__cell bt-table__cell--last", profit, header ? UiWeight.Bold : UiWeight.Bold, row);
            if (!string.IsNullOrEmpty(tone)) value.AddToClassList(tone);
        }
    }
}
