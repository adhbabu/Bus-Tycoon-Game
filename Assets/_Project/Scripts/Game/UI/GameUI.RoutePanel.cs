using System;
using BusTycoon.Economy;
using BusTycoon.Fleet;
using BusTycoon.Routes;
using UnityEngine;

namespace BusTycoon.Game
{
    // Hat paneli: Fiyat (oyunun kalbi) · Otobüsler · Geçmiş
    public sealed partial class GameUI
    {
        enum RouteTab { Price, Buses, History }

        static readonly Color32 ChartBackground = new Color32(22, 26, 50, 255);
        static readonly Color32 ChartBand = new Color32(92, 222, 128, 46);
        static readonly Color32 ChartZero = new Color32(255, 255, 255, 60);
        static readonly Color32 ChartCurve = new Color32(235, 240, 248, 255);
        static readonly Color32 ChartLossCurve = new Color32(246, 96, 96, 255);
        static readonly Color32 ChartBest = new Color32(92, 222, 128, 255);
        static readonly Color32 ChartApplied = new Color32(115, 188, 255, 255);
        static readonly Color32 ChartPending = new Color32(255, 255, 255, 255);
        static readonly Color32 ChartPositive = new Color32(92, 222, 128, 230);
        static readonly Color32 ChartNegative = new Color32(246, 96, 96, 230);

        RouteState selectedRoute;
        RouteTab routeTab;
        Vector2 routeScroll;
        bool routeAnalysisDirty = true;
        bool historyChartDirty = true;
        bool showAdvanced;
        readonly PricingAnalysis analysis = new PricingAnalysis();
        Func<float, RouteForecast> estimator;
        RouteForecast pendingForecast, appliedForecast;
        float pendingPrice;
        float chartRenderedPrice = -1f;
        ChartTexture priceChart, historyChart;

        void OpenRoutePanelNow(RouteState route, RouteTab tab)
        {
            if (route == null || mode != Mode.Playing) return;
            selectedRoute = route;
            routeTab = tab;
            panel = Panel.Route;
            routeAnalysisDirty = true;
            historyChartDirty = true;
            pendingPrice = route.ticketPrice;
            routeScroll = Vector2.zero;
            mapCamera.FocusOn(world.GetRouteMidpoint(route));
        }

        void EnsureRouteAnalysis()
        {
            if (!routeAnalysisDirty || Event.current.type != EventType.Layout) return;
            routeAnalysisDirty = false;

            estimator = session.Routes.CreateEstimator(selectedRoute);
            session.Routes.AnalyzePricing(selectedRoute, 60, analysis);
            appliedForecast = estimator(selectedRoute.ticketPrice);
            if (pendingPrice <= 0f) pendingPrice = selectedRoute.ticketPrice;
            pendingPrice = Mathf.Clamp(pendingPrice, analysis.MinPrice, analysis.MaxPrice);
            pendingForecast = estimator(pendingPrice);
            chartRenderedPrice = -1f;
        }

        void DrawRoutePanel()
        {
            // Hat kapatılmış olabilir
            if (selectedRoute == null || session.Routes.GetRoute(selectedRoute.routeId) == null)
            {
                PanelHeader("Hat");
                GUILayout.Label("Bu hat artık yok.", s.Dim);
                return;
            }

            EnsureRouteAnalysis();
            if (estimator == null) return;

            int busCount = session.Fleet.CountBusesOnRoute(selectedRoute.routeId);
            PanelHeader(RouteName(selectedRoute), $"{Fmt.Km(selectedRoute.distanceKm)} · hat no {selectedRoute.routeId} · {busCount} otobüs");

            RouteInsight insight = RouteAdvisor.GetTopInsight(selectedRoute, busCount);
            GUILayout.BeginHorizontal(s.Card);
            Badge(InsightTag(insight.Severity), InsightBadge(insight.Severity));
            Sp(4f);
            ColorLabel(insight.Message, InsightColor(insight.Severity), s.Wrap);
            if (insight.Action == InsightAction.AssignBus || insight.Action == InsightAction.AddBus)
            {
                if (GUILayout.Button("Otobüs ekle", s.Primary, Wd(120f), Ht(32f))) Defer(() => routeTab = RouteTab.Buses);
            }
            else if (insight.Action == InsightAction.UpgradeBus)
            {
                if (GUILayout.Button("Galeri", s.Button, Wd(100f), Ht(32f))) Defer(() => OpenDealershipNow(selectedRoute));
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            RouteTabButton("Fiyat", RouteTab.Price);
            RouteTabButton($"Otobüsler ({busCount})", RouteTab.Buses);
            RouteTabButton("Geçmiş", RouteTab.History);
            GUILayout.EndHorizontal();
            Sp(4f);

            routeScroll = BeginScroll(routeScroll);
            switch (routeTab)
            {
                case RouteTab.Price: DrawPriceTab(); break;
                case RouteTab.Buses: DrawBusesTab(); break;
                case RouteTab.History: DrawHistoryTab(); break;
            }
            GUILayout.EndScrollView();
            // Telefonda Önerilen / Geri al / UYGULA kaydırma alanının dışında, panelin altında sabit
            if (phone && routeTab == RouteTab.Price)
            {
                Sp(4f);
                DrawPriceActions(Mathf.Abs(pendingPrice - selectedRoute.ticketPrice) > 0.001f);
            }
        }

        void RouteTabButton(string text, RouteTab tab)
        {
            if (GUILayout.Button(text, routeTab == tab ? s.TabActive : s.Tab, Ht(32f))) Defer(() => routeTab = tab);
        }

        // ------------------------------------------------------------------ Fiyat sekmesi

        /// <summary>Fiyat-kâr eğrisi ve altındaki fiyat aralığı/renk açıklaması.</summary>
        void DrawPriceChart(float height)
        {
            Rect chartRect = GUILayoutUtility.GetRect(10f, height * u, GUILayout.ExpandWidth(true), Ht(height));
            if (Event.current.type == EventType.Repaint) GUI.DrawTexture(chartRect, priceChart.Texture);

            GUILayout.BeginHorizontal();
            GUILayout.Label(Fmt.Price(analysis.MinPrice), s.Small);
            GUILayout.FlexibleSpace();
            ColorLabel("beyaz: seçili · yeşil: önerilen · mavi: mevcut", UIStyles.DimText, s.Small);
            GUILayout.FlexibleSpace();
            GUILayout.Label(Fmt.Price(analysis.MaxPrice), s.Small);
            GUILayout.EndHorizontal();
        }

        /// <summary>Kaydırıcı, -/+ ile ince ayar, seçili fiyat ve fiyat bölgesi.</summary>
        void DrawPriceControls()
        {
            float slider = Slider(pendingPrice, analysis.MinPrice, analysis.MaxPrice);
            if (!Mathf.Approximately(slider, pendingPrice)) SetPendingPrice(slider);

            float step = phone ? 52f : 44f;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-", s.Button, Wd(step), Ht(40f))) SetPendingPrice(pendingPrice - PriceStep(pendingPrice));
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical();
            GUILayout.Label(Fmt.Price(pendingPrice), s.Big);
            ColorLabel($"piyasa referansı {Fmt.Price(analysis.ReferencePrice)}", UIStyles.DimText, s.Small);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+", s.Button, Wd(step), Ht(40f))) SetPendingPrice(pendingPrice + PriceStep(pendingPrice));
            GUILayout.EndHorizontal();

            PriceZone zone = analysis.Classify(pendingPrice, pendingForecast.Profit);
            ColorLabel(PricingAdvisor.Describe(zone), ZoneColor(zone), s.H2);
        }

        void DrawPriceActions(bool changed)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Önerilen", s.Button, Ht(40f))) SetPendingPrice(analysis.EstimatedBestPrice);
            if (EnabledButton(changed, "Geri al", s.Button, Ht(40f))) SetPendingPrice(selectedRoute.ticketPrice);
            if (EnabledButton(changed, "UYGULA", s.Primary, Ht(40f))) Defer(ApplyPendingPrice);
            GUILayout.EndHorizontal();
        }

        void DrawPriceTab()
        {
            if (priceChart == null) priceChart = new ChartTexture(640, 220);
            if (!Mathf.Approximately(chartRenderedPrice, pendingPrice)) RenderPriceChart();
            bool changed = Mathf.Abs(pendingPrice - selectedRoute.ticketPrice) > 0.001f;

            if (phone)
            {
                // Telefonda panel kısa: önce fiyat ayarı (düğmeler panelin altında sabit), grafik altta
                DrawPriceControls();
                Sp(6f);
                DrawPriceChart(90f);
            }
            else
            {
                DrawPriceChart(150f);
                DrawPriceControls();
            }

            // Tahmin
            GUILayout.BeginVertical(s.Card);
            KeyValue("Yolcu / gün", Fmt.Num(pendingForecast.Passengers), icon: UIIcon.People);
            KeyValue("Doluluk", $"{Fmt.Pct(pendingForecast.LoadFactor)} · {pendingForecast.TripsPerDay:0.#} sefer/gün");
            // Not: satır sayısı sabit tutulur (IMGUI Layout/Repaint tutarlılığı); değer duruma göre değişir.
            bool spill = pendingForecast.UnmetDemand > pendingForecast.Demand * 0.1f;
            KeyValue("Kaçan yolcu", spill ? $"~{Fmt.Num(pendingForecast.UnmetDemand)} / gün (otobüs ekleyin)" : "yok",
                spill ? UIStyles.Orange : UIStyles.DimText);
            KeyValue("Gelir", $"{Fmt.Money(pendingForecast.Revenue)} / gün", icon: UIIcon.Ticket);
            KeyValue("Gider", $"{Fmt.Money(pendingForecast.TotalCost)} / gün", icon: UIIcon.Fuel);
            KeyValue("Kâr", $"{Fmt.Signed(pendingForecast.Profit)} / gün", pendingForecast.Profit >= 0 ? UIStyles.Green : UIStyles.Red, pendingForecast.Profit >= 0 ? UIIcon.ChartUp : UIIcon.ChartDown);
            float delta = pendingForecast.Profit - appliedForecast.Profit;
            KeyValue("Mevcut fiyata göre", changed ? $"{Fmt.Signed(delta)} / gün" : "uygulanan fiyat",
                !changed ? UIStyles.DimText : delta >= 0 ? UIStyles.Green : UIStyles.Red);
            GUILayout.EndVertical();

            // Gerçekleşen
            if (selectedRoute.HasHistory)
            {
                RouteDayReport last = selectedRoute.LastReport;
                GUILayout.Label($"Dün ({Fmt.Price(last.ticketPrice)}): {Fmt.Num(last.passengers)} yolcu · doluluk {Fmt.Pct(last.loadFactor)} · kâr {Fmt.Signed(last.Profit)}", s.WrapDim);
            }
            GUILayout.Label(analysis.EstimateError <= 0f
                ? "Pazar araştırması: tam isabetli tahmin"
                : $"Pazar araştırması: tahminler ±%{analysis.EstimateError * 100f:0} sapabilir (seviyeyle iyileşir)", s.WrapDim);

            if (!phone) DrawPriceActions(changed);

            // Gelişmiş: sefer planı
            Sp(6f);
            if (GUILayout.Button(showAdvanced ? "Gelişmiş ayarları gizle" : "Gelişmiş: sefer planı", s.Ghost)) Defer(() => showAdvanced = !showAdvanced);
            if (showAdvanced)
            {
                bool auto = GUILayout.Toggle(selectedRoute.autoSchedule, " Otomatik sefer planı (önerilen)", s.Toggle, Ht(32f));
                if (auto != selectedRoute.autoSchedule)
                {
                    bool value = auto;
                    Defer(() => session.Routes.SetSchedule(selectedRoute.routeId, value, selectedRoute.manualUtilization));
                }
                if (!selectedRoute.autoSchedule)
                {
                    GUILayout.Label($"Sefer yoğunluğu: {Fmt.Pct(selectedRoute.manualUtilization)} (otobüslerin maksimum kapasitesine göre)", s.Small);
                    float utilization = Slider(selectedRoute.manualUtilization, 0.1f, 1f);
                    if (Mathf.Abs(utilization - selectedRoute.manualUtilization) > 0.005f)
                        session.Routes.SetSchedule(selectedRoute.routeId, false, Mathf.Round(utilization * 20f) / 20f);
                }
                else
                {
                    GUILayout.Label("Sistem sefer sayısını talebe göre ayarlar (hedef doluluk %85). Boş sefer yapılmaz.", s.WrapDim);
                }
            }
        }

        void SetPendingPrice(float price)
        {
            if (estimator == null) return;
            float step = PriceStep(price);
            pendingPrice = Mathf.Clamp(Mathf.Max(step, Mathf.Round(price / step) * step), analysis.MinPrice, analysis.MaxPrice);
            pendingForecast = estimator(pendingPrice);
        }

        void ApplyPendingPrice()
        {
            var result = session.Routes.SetTicketPrice(selectedRoute.routeId, pendingPrice);
            if (!result)
            {
                Toast(result.Message, UIStyles.Red);
                return;
            }
            appliedForecast = estimator(pendingPrice);
            chartRenderedPrice = -1f;
            Toast($"{RouteName(selectedRoute)}: yeni bilet fiyatı {Fmt.Price(pendingPrice)}", UIStyles.Green);
            goals.NotifyPriceAdjusted();
        }

        static float PriceStep(float price) => price < 20f ? 0.1f : 0.5f;

        static Color ZoneColor(PriceZone zone) => zone switch
        {
            PriceZone.Ideal => UIStyles.Green,
            PriceZone.Good => UIStyles.Yellow,
            PriceZone.Loss => UIStyles.Red,
            _ => UIStyles.Orange
        };

        void RenderPriceChart()
        {
            chartRenderedPrice = pendingPrice;
            ChartTexture chart = priceChart;
            chart.Clear(ChartBackground);
            if (analysis.Curve.Count < 2) { chart.Apply(); return; }

            float minProfit = 0f, maxProfit = 1f;
            foreach (var point in analysis.Curve)
            {
                minProfit = Mathf.Min(minProfit, point.Profit);
                maxProfit = Mathf.Max(maxProfit, point.Profit);
            }
            float pad = (maxProfit - minProfit) * 0.08f;
            minProfit -= pad;
            maxProfit += pad;

            float X(float price) => Mathf.InverseLerp(analysis.MinPrice, analysis.MaxPrice, price) * (chart.Width - 1);
            float Y(float profit) => Mathf.InverseLerp(minProfit, maxProfit, profit) * (chart.Height - 1);

            // İdeal bölge bandı
            float bandThreshold = analysis.EstimatedBestProfit * PricingAnalysis.IdealThreshold;
            if (analysis.EstimatedBestProfit > 0f)
            {
                for (int i = 0; i < analysis.Curve.Count - 1; i++)
                {
                    if (analysis.Curve[i].Profit < bandThreshold || analysis.Curve[i + 1].Profit < bandThreshold) continue;
                    chart.FillRect(X(analysis.Curve[i].Price), 0, X(analysis.Curve[i + 1].Price) + 1, chart.Height, ChartBand);
                }
            }

            chart.HorizontalLine(Y(0f), ChartZero, 1);

            for (int i = 0; i < analysis.Curve.Count - 1; i++)
            {
                PriceCurvePoint a = analysis.Curve[i], b = analysis.Curve[i + 1];
                Color32 color = a.Profit < 0f && b.Profit < 0f ? ChartLossCurve : ChartCurve;
                chart.Line(X(a.Price), Y(a.Profit), X(b.Price), Y(b.Profit), color, 3);
            }

            chart.VerticalLine(X(analysis.EstimatedBestPrice), ChartBest, 2);
            chart.VerticalLine(X(selectedRoute.ticketPrice), ChartApplied, 2);
            chart.VerticalLine(X(pendingPrice), ChartPending, 3);
            chart.Apply();
        }

        // ------------------------------------------------------------------ Otobüsler sekmesi

        void DrawBusesTab()
        {
            session.Fleet.GetBusesOnRoute(selectedRoute.routeId, busBuffer);
            GUILayout.Label($"Hattaki otobüsler ({busBuffer.Count})", s.H2);
            if (busBuffer.Count == 0) ColorLabel("Bu hatta otobüs yok; hat çalışmıyor.", UIStyles.Orange, s.Label);

            foreach (var bus in busBuffer.ToArray())
            {
                GUILayout.BeginHorizontal(s.Card);
                DrawBusSummary(bus);
                if (GUILayout.Button("Garaja çek", s.Button, Wd(110f), Ht(34f)))
                {
                    string id = bus.busId;
                    Defer(() => session.Routes.TryAssignBus(id, null));
                }
                GUILayout.EndHorizontal();
            }

            Sp(8f);
            session.Fleet.GetIdleBuses(busBuffer);
            GUILayout.Label($"Garajdaki otobüsler ({busBuffer.Count})", s.H2);
            if (busBuffer.Count == 0) GUILayout.Label("Garaj boş. Galeriden otobüs alabilirsiniz.", s.Dim);

            foreach (var bus in busBuffer.ToArray())
            {
                GUILayout.BeginHorizontal(s.Card);
                DrawBusSummary(bus);
                if (GUILayout.Button("Bu hatta ata", s.Primary, Wd(120f), Ht(34f)))
                {
                    string id = bus.busId;
                    Defer(() =>
                    {
                        var result = session.Routes.TryAssignBus(id, selectedRoute.routeId);
                        if (result) Toast($"Otobüs {RouteName(selectedRoute)} hattına atandı.", UIStyles.Green);
                    });
                }
                GUILayout.EndHorizontal();
            }

            Sp(8f);
            if (GUILayout.Button("Galeriden otobüs al (bu hatta atanır)", s.Button, Ht(40f)))
                Defer(() => OpenDealershipNow(selectedRoute));
        }

        void DrawBusSummary(BusInstance bus)
        {
            GUILayout.BeginVertical();
            GUILayout.Label(bus.nickname, s.Bold);
            string status = !bus.IsAvailableToday ? $"Arızalı ({bus.outOfServiceDays} gün)" : $"{Fmt.Num(bus.odometerKm)} km";
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Durum {Fmt.Pct(bus.condition)}", s.Small, Wd(90f));
            LayoutBar(bus.condition, bus.condition > 0.6f ? UIStyles.Green : bus.condition > 0.35f ? UIStyles.Yellow : UIStyles.Red, 6f, 90f, 7f);
            ColorLabel(status, bus.IsAvailableToday ? UIStyles.DimText : UIStyles.Orange, s.Small);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        // ------------------------------------------------------------------ Geçmiş sekmesi

        void DrawHistoryTab()
        {
            if (!selectedRoute.HasHistory)
            {
                GUILayout.Label("Henüz sonuç yok; ilk gün bitince burada görünecek.", s.Dim);
            }
            else
            {
                if (historyChart == null) historyChart = new ChartTexture(640, 180);
                if (historyChartDirty && Event.current.type == EventType.Layout) RenderHistoryChart();

                GUILayout.Label($"Günlük kâr (son {selectedRoute.history.Count} gün)", s.H2);
                Rect rect = GUILayoutUtility.GetRect(10f, 120f * u, GUILayout.ExpandWidth(true), Ht(120f));
                if (Event.current.type == EventType.Repaint) GUI.DrawTexture(rect, historyChart.Texture);

                Sp(6f);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Gün", s.Dim, Wd(60f));
                GUILayout.Label("Bilet", s.Dim, Wd(80f));
                GUILayout.Label("Yolcu", s.Dim, Wd(70f));
                GUILayout.Label("Doluluk", s.Dim, Wd(80f));
                GUILayout.Label("Kâr", s.Dim);
                GUILayout.EndHorizontal();

                var history = selectedRoute.history;
                for (int i = history.Count - 1; i >= Mathf.Max(0, history.Count - 7); i--)
                {
                    RouteDayReport r = history[i];
                    GUILayout.BeginHorizontal();
                    GUILayout.Label((r.day + 1).ToString(), s.Small, Wd(60f));
                    GUILayout.Label(Fmt.Price(r.ticketPrice), s.Small, Wd(80f));
                    GUILayout.Label(Fmt.Num(r.passengers), s.Small, Wd(70f));
                    GUILayout.Label(Fmt.Pct(r.loadFactor), s.Small, Wd(80f));
                    ColorLabel(Fmt.Signed(r.Profit), r.Profit >= 0 ? UIStyles.Green : UIStyles.Red, s.Small);
                    GUILayout.EndHorizontal();
                }
            }

            Divider();
            KeyValue("Açılış", $"{selectedRoute.openedDay + 1}. gün");
            KeyValue("Toplam yolcu", Fmt.Num(selectedRoute.lifetimePassengers));
            KeyValue("Toplam kâr", Fmt.Signed(selectedRoute.lifetimeProfit), selectedRoute.lifetimeProfit >= 0 ? UIStyles.Green : UIStyles.Red);
            Sp(10f);
            if (GUILayout.Button("Hattı kapat", s.Danger, Ht(38f)))
            {
                RouteState route = selectedRoute;
                Defer(() => AskConfirm("Hattı kapat",
                    $"{RouteName(route)} hattı kapatılacak. Otobüsler garaja döner, lisans bedeli iade edilmez.",
                    () =>
                    {
                        var result = session.Routes.TryCloseRoute(route.routeId);
                        Toast(result.Message, result ? UIStyles.Orange : UIStyles.Red);
                        if (result) ClosePanel();
                    }));
            }
        }

        void RenderHistoryChart()
        {
            historyChartDirty = false;
            ChartTexture chart = historyChart;
            chart.Clear(ChartBackground);
            var history = selectedRoute.history;
            if (history.Count == 0) { chart.Apply(); return; }

            float max = 1f, min = 0f;
            foreach (var r in history) { max = Mathf.Max(max, r.Profit); min = Mathf.Min(min, r.Profit); }
            float range = max - min;
            float zeroY = (0f - min) / range * (chart.Height - 1);
            float barWidth = chart.Width / (float)RouteState.HistoryLength;

            for (int i = 0; i < history.Count; i++)
            {
                float x0 = (RouteState.HistoryLength - history.Count + i) * barWidth + 1f;
                float y = (history[i].Profit - min) / range * (chart.Height - 1);
                chart.FillRect(x0, Mathf.Min(zeroY, y), x0 + barWidth - 2f, Mathf.Max(zeroY, y) + 1f,
                    history[i].Profit >= 0 ? ChartPositive : ChartNegative);
            }
            chart.HorizontalLine(zeroY, ChartZero, 1);
            chart.Apply();
        }
    }
}
