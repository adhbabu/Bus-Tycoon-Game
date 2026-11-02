using System;
using System.Collections.Generic;
using BusTycoon.Data;
using BusTycoon.Economy;
using BusTycoon.Routes;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// Yeni arayüzde sağ paneller (UI Toolkit). Panel açılınca ya da verisi değişince (gün sonu, hat/filo değişikliği)
    /// içeriği bir kez kurulur; her kare yalnız yeri güncellenir. Taşınan paneller <see cref="ToolkitPanels"/>'te;
    /// diğerleri geçiş sürerken IMGUI'de çizilir.
    /// </summary>
    public sealed partial class GameUI
    {
        static readonly HashSet<Panel> ToolkitPanels = new HashSet<Panel>
        {
            Panel.Goals, Panel.Log, Panel.Help, Panel.Regions, Panel.Finance, Panel.RouteList, Panel.Fleet, Panel.Dealership, Panel.Route
        };

        VisualElement tkSheet;
        ScrollView tkSheetScroll;
        Label tkSheetTitle, tkSheetSubtitle;
        Panel tkBuiltPanel = Panel.None;
        bool tkSheetDirty;
        int tkSheetSignature, tkSheetOpenFrame = -1;
        VisualElement tkFinanceChart;

        /// <summary>Panel yeni arayüzde mi çiziliyor (IMGUI çizmez).</summary>
        bool ToolkitDrawsPanel(Panel target) => NewUi && ToolkitPanels.Contains(target);

        void BuildToolkitSheet()
        {
            tkSheet = Ui.El("bt-sheet", tkHud);
            VisualElement header = Ui.El("bt-sheet__header", tkSheet);
            tkSheetTitle = Ui.Text("bt-sheet__title", "", UiWeight.Black, header);
            Ui.El("bt-grow", header).pickingMode = PickingMode.Ignore;
            Button close = Ui.Button("bt-sheet__close", "×", ClosePanel, header, UiWeight.Bold);
            close.name = "close";
            tkSheetSubtitle = Ui.Text("bt-sheet__subtitle", "", UiWeight.Regular, tkSheet);
            tkSheetScroll = new ScrollView(ScrollViewMode.Vertical);
            tkSheetScroll.AddToClassList("bt-sheet__scroll");
            tkSheet.Add(tkSheetScroll);
            tkSheet.style.display = DisplayStyle.None;
        }

        void UpdateToolkitSheet(float k)
        {
            bool show = ToolkitDrawsPanel(panel) && !StationUi;
            Ui.Show(tkSheet, show);
            if (!show)
            {
                tkBuiltPanel = Panel.None;
                return;
            }
            Rect rect = PanelRect;
            Ui.Place(tkSheet, rect.x * k, rect.y * k, rect.width * k, rect.height * k);

            if (panel == Panel.Log) tkSheetDirty = true; // günlük gün içinde de dolar (imza ucuz)
            if (panel != tkBuiltPanel)
            {
                tkSheet.RemoveFromClassList("is-open"); // açılış: sağdan kayarak gelir (stilde geçiş)
                tkSheetOpenFrame = Time.frameCount + 1;
                tkSheetScroll.scrollOffset = Vector2.zero;
                tkSheetDirty = true;
            }
            else if (tkSheetOpenFrame >= 0 && Time.frameCount >= tkSheetOpenFrame)
            {
                tkSheetOpenFrame = -1;
                tkSheet.AddToClassList("is-open");
            }
            if (!tkSheetDirty) return;
            tkSheetDirty = false;
            if (panel == Panel.Fleet && tkBuiltPanel == Panel.Fleet)
            {
                RefreshFleet(); // kartlar yerinde güncellenir (yalnız değişenler)
                return;
            }
            // Veri değişti dense de panelin gösterdiği şey değişmediyse yeniden kurulmaz (ör. Bölgeler her gün değişmez)
            int signature = SheetSignature();
            if (panel == tkBuiltPanel && signature == tkSheetSignature) return;
            tkSheetSignature = signature;
            tkBuiltPanel = panel;
            RebuildToolkitSheet();
        }

        /// <summary>Panelin gösterdiği verinin özeti: değişmediyse panel yeniden kurulmaz.</summary>
        int SheetSignature()
        {
            switch (panel)
            {
                case Panel.Goals: return goals.CompletedCount;
                case Panel.Log:
                {
                    var log = notes.Log;
                    return log.Count == 0 ? 0 : log.Count * 31 + log[log.Count - 1].Count;
                }
                case Panel.Regions:
                {
                    int signature = session.Progression.Level * 7919;
                    foreach (var region in session.World.Regions)
                    {
                        signature = signature * 3 + (session.Progression.IsRegionUnlocked(region) ? 1 : 0) +
                                    (session.Progression.CanUnlockRegion(region).Success ? 2 : 0);
                        signature += Entitlements.Owns(region.PackId) ? 1 : 0;
                    }
                    return signature;
                }
                case Panel.Finance: return session.State.currentDay * 7 + session.State.recentTransactions.Count;
                case Panel.RouteList:
                {
                    int signature = session.State.currentDay * 7 + session.Fleet.Buses.Count;
                    foreach (var route in session.Routes.Routes)
                        signature = signature * 31 + route.routeId.GetHashCode() + Mathf.RoundToInt(route.ticketPrice * 10f) +
                                    session.Fleet.CountBusesOnRoute(route.routeId) * 7;
                    return signature;
                }
                case Panel.Dealership: return DealershipSignature();
                case Panel.Route: return RouteSignature();
                default: return 0;
            }
        }

        void RebuildToolkitSheet()
        {
            VisualElement body = tkSheetScroll.contentContainer;
            body.Clear();
            switch (panel)
            {
                case Panel.Goals:
                    SheetHeader("Görevler", $"{goals.CompletedCount} / {goals.Goals.Count} tamamlandı");
                    BuildGoals(body);
                    break;
                case Panel.Log:
                    SheetHeader("Günlük", "Son olaylar");
                    BuildLog(body);
                    break;
                case Panel.Help:
                    SheetHeader("Nasıl Oynanır?", null);
                    BuildHelp(body);
                    break;
                case Panel.Regions:
                    SheetHeader("Bölgeler", "Yeni ülkelerde hat açmak için işletme lisansı alın.");
                    BuildRegions(body);
                    break;
                case Panel.Finance:
                    SheetHeader("Finans", null);
                    BuildFinance(body);
                    break;
                case Panel.RouteList:
                    BuildRouteList(body);
                    break;
                case Panel.Fleet:
                    BuildFleet(body);
                    break;
                case Panel.Dealership:
                    BuildDealership(body);
                    break;
                case Panel.Route:
                    BuildRoutePanel(body);
                    break;
            }
        }

        // ------------------------------------------------------------------ ortak parçalar

        static Label Section(VisualElement parent, string title) => Ui.Text("bt-section", title, UiWeight.Bold, parent);

        /// <summary>Anahtar–değer satırı: solda ikon ve soluk anahtar, sağda kalın değer (tone: is-up / is-down / is-gold).</summary>
        static void KeyValue(VisualElement parent, string key, string value, string tone = null, UIIcon? icon = null)
        {
            VisualElement row = Ui.El("bt-kv", parent);
            if (icon.HasValue) Ui.Icon(icon.Value, "bt-kv__icon", row);
            Ui.Text("bt-kv__key", key, UiWeight.Regular, row);
            Label label = Ui.Text("bt-kv__value", value, UiWeight.Bold, row);
            if (!string.IsNullOrEmpty(tone)) label.AddToClassList(tone);
        }

        static string Tone(double value) => value >= 0 ? "is-up" : "is-down";

        static void Chip(VisualElement parent, UIIcon icon, string text)
        {
            VisualElement chip = Ui.El("bt-chip", parent);
            Ui.Icon(icon, "bt-chip__icon", chip);
            Ui.Text("bt-chip__text", text, UiWeight.Regular, chip);
        }

        static Button ActionButton(VisualElement parent, string text, Action action, bool primary = false, bool enabled = true)
        {
            Button button = Ui.Button(primary ? "bt-btn is-primary" : "bt-btn", text, action, parent);
            button.SetEnabled(enabled);
            return button;
        }

        // ------------------------------------------------------------------ Hatlarım

        static string InsightClass(InsightSeverity severity) => severity switch
        {
            InsightSeverity.Critical => "is-danger",
            InsightSeverity.Warning => "is-warning",
            _ => "is-success"
        };

        void BuildRouteList(VisualElement body)
        {
            var routes = session.Routes;
            var fleet = session.Fleet;
            double total = 0;
            foreach (var route in routes.Routes) total += route.LastReport.Profit;
            SheetHeader("Hatlarım", $"{routes.Routes.Count} / {session.Progression.MaxRoutes} hat · dün toplam {Fmt.Signed(total)}");

            Button create = ActionButton(body, "+ Yeni hat aç", () => OpenPlannerNow(null), primary: true);
            create.AddToClassList("bt-btn--wide");
            if (routes.Routes.Count == 0)
            {
                Ui.Text("bt-item__text", "Henüz hattınız yok. 'Yeni hat aç' ile ya da bir durağa girip 'Buradan hat aç' ile başlayın.", UiWeight.Regular, body);
                return;
            }

            EnsureRouteList();
            foreach (var route in sortedRoutes)
            {
                int buses = fleet.CountBusesOnRoute(route.routeId);
                if (!routeInsightCache.TryGetValue(route.routeId, out RouteInsight insight))
                    insight = RouteAdvisor.GetTopInsight(route, buses);
                RouteDayReport last = route.LastReport;

                VisualElement card = Ui.El("bt-item bt-route", body);
                card.style.borderLeftColor = world.RouteIdentityColor(route.routeId); // haritadaki hat rengi
                VisualElement header = Ui.El("bt-row", card);
                Ui.Text("bt-item__title bt-grow", RouteName(route), UiWeight.Bold, header);
                Ui.Text("bt-pill " + InsightClass(insight.Severity), InsightTag(insight.Severity), UiWeight.Bold, header);
                Label profit = Ui.Text("bt-route__profit", route.HasHistory ? $"{Fmt.Signed(last.Profit)}/gün" : "—", UiWeight.Black, header);
                profit.AddToClassList(Tone(last.Profit));

                VisualElement stats = Ui.El("bt-row bt-route__stats", card);
                Ui.Text("bt-route__detail bt-grow", $"{buses} otobüs · {Fmt.Price(route.ticketPrice)} · {Fmt.Num(last.passengers)} yolcu", UiWeight.Regular, stats);
                Ui.Text("bt-route__detail", Fmt.Pct(last.loadFactor), UiWeight.Bold, stats);
                VisualElement bar = Ui.El("bt-bar bt-route__load", stats);
                Ui.Fill(Ui.El("bt-bar__fill bt-bar__fill--blue", bar), last.loadFactor);

                Label message = Ui.Text("bt-item__text", insight.Message, UiWeight.Regular, card);
                message.AddToClassList(InsightClass(insight.Severity) + "-text");

                VisualElement actions = Ui.El("bt-actions bt-actions--start", card);
                RouteState target = route;
                ActionButton(actions, "Fiyat", () => OpenRoutePanelNow(target, RouteTab.Price));
                ActionButton(actions, "Otobüsler", () => OpenRoutePanelNow(target, RouteTab.Buses));
                ActionButton(actions, "Geçmiş", () => OpenRoutePanelNow(target, RouteTab.History));
            }
        }

        // ------------------------------------------------------------------ Bölgeler

        void BuildRegions(VisualElement body)
        {
            string currentPack = null;
            foreach (var region in SortedRegions())
            {
                if (region.PackId != currentPack)
                {
                    currentPack = region.PackId;
                    if (!string.IsNullOrEmpty(currentPack)) BuildPackHeader(body, currentPack);
                }

                bool unlocked = session.Progression.IsRegionUnlocked(region);
                bool packOwned = Entitlements.Owns(region.PackId);
                var check = session.Progression.CanUnlockRegion(region);

                VisualElement card = Ui.El("bt-item", body);
                VisualElement header = Ui.El("bt-row", card);
                Ui.Icon(UIIcons.Flag(region.Id), "bt-flag", header);
                Ui.Text("bt-item__title bt-grow", region.DisplayName, UiWeight.Bold, header);
                if (unlocked) Ui.Text("bt-pill is-success", "Açık", UiWeight.Bold, header);
                else Ui.Text("bt-pill is-warning", packOwned ? "Kilitli" : "Paket gerekli", UiWeight.Bold, header);

                VisualElement chips = Ui.El("bt-chips", card);
                Chip(chips, UIIcon.Building, $"{CityCount(region)} şehir");
                Chip(chips, UIIcon.Ticket, $"bilet ×{region.FareMultiplier:0.00}");
                Chip(chips, UIIcon.Fuel, $"yakıt ×{region.FuelPriceMultiplier:0.00}");
                Chip(chips, UIIcon.Pilot, $"maaş ×{region.WageMultiplier:0.00}");

                VisualElement actions = Ui.El("bt-actions", card);
                if (!unlocked && packOwned)
                {
                    Ui.Text("bt-actions__note bt-grow", $"Seviye {region.RequiredCompanyLevel} · lisans {Fmt.Money(region.OperatingLicenseCost)}", UiWeight.Regular, actions);
                    RegionData target = region;
                    ActionButton(actions, "Lisans al", () =>
                    {
                        var result = session.Progression.TryUnlockRegion(target);
                        if (!result) Toast(result.Message, UIStyles.Red);
                        goals.Evaluate();
                        tkSheetDirty = true;
                    }, primary: true, enabled: check.Success);
                }
                else
                {
                    Ui.El("bt-grow", actions);
                }
                RegionData focus = region;
                ActionButton(actions, "Haritada göster", () => FocusRegion(focus));
                if (!unlocked && !check.Success) Ui.Text("bt-item__text", check.Message, UiWeight.Regular, card);
            }
        }

        void BuildPackHeader(VisualElement body, string packId)
        {
            bool owned = Entitlements.Owns(packId);
            VisualElement header = Ui.El("bt-pack", body);
            VisualElement row = Ui.El("bt-row", header);
            Ui.Icon(UIIcon.Package, "bt-item__icon", row);
            Ui.Text("bt-pack__title bt-grow", ContentPacks.DisplayName(packId), UiWeight.Black, row);
            Ui.Text(owned ? "bt-pill is-success" : "bt-pill is-warning", owned ? "Sahipsiniz" : "Satın alınmadı", UiWeight.Bold, row);
            Ui.Text("bt-item__text", owned
                ? "Kıta Avrupası ve Britanya: 31 ülke, 128 şehir. Seviye ilerledikçe lisansla açılır; Batı'da bilet de maaş da yüksektir."
                : "Bu ülkeler Avrupa Paketi ile açılır.", UiWeight.Regular, header);
        }

        // ------------------------------------------------------------------ Finans

        void BuildFinance(VisualElement body)
        {
            var finance = session.Finance;
            var history = session.State.financeHistory;
            double fleetValue = 0;
            foreach (var bus in session.Fleet.Buses) fleetValue += session.Fleet.GetResaleValue(bus);

            VisualElement summary = Ui.El("bt-item", body);
            KeyValue(summary, "Kasa", Fmt.Money(finance.Cash), Tone(finance.Cash), UIIcon.Finance);
            KeyValue(summary, "Filo değeri (satış)", Fmt.Money(fleetValue), null, UIIcon.Fleet);
            KeyValue(summary, "Şirket değeri", Fmt.Money(finance.Cash + fleetValue), "is-gold", UIIcon.Bank);
            double week = finance.GetNetProfit(7), month = finance.GetNetProfit(30);
            KeyValue(summary, "Son 7 gün net", Fmt.Signed(week), Tone(week), week >= 0 ? UIIcon.ChartUp : UIIcon.ChartDown);
            KeyValue(summary, "Son 30 gün net", Fmt.Signed(month), Tone(month), month >= 0 ? UIIcon.ChartUp : UIIcon.ChartDown);
            KeyValue(summary, "Taşınan toplam yolcu", Fmt.Num(session.State.totalPassengers), null, UIIcon.People);

            Section(body, "Günlük net (son 60 gün)");
            if (financeChart == null) financeChart = new ChartTexture(640, 180);
            RenderFinanceChart();
            tkFinanceChart = Ui.El("bt-chart", body);
            tkFinanceChart.style.backgroundImage = new StyleBackground(financeChart.Texture);

            Section(body, "Son 7 gün gelir-gider dağılımı");
            var totals = new double[Enum.GetValues(typeof(LedgerCategory)).Length];
            for (int i = Mathf.Max(0, history.Count - 7); i < history.Count; i++)
            {
                double[] byCategory = history[i].byCategory;
                if (byCategory == null) continue;
                for (int c = 0; c < byCategory.Length && c < totals.Length; c++) totals[c] += byCategory[c];
            }
            VisualElement breakdown = Ui.El("bt-item", body);
            bool any = false;
            foreach (LedgerCategory category in Enum.GetValues(typeof(LedgerCategory)))
            {
                double amount = totals[(int)category];
                if (Math.Abs(amount) < 0.5) continue;
                any = true;
                KeyValue(breakdown, CategoryNames[category], Fmt.Signed(amount), Tone(amount), CategoryIcon(category));
            }
            if (!any) Ui.Text("bt-empty", "Henüz hareket yok.", UiWeight.Regular, breakdown);

            Section(body, "Son büyük işlemler");
            var transactions = session.State.recentTransactions;
            VisualElement list = Ui.El("bt-item", body);
            if (transactions.Count == 0) Ui.Text("bt-empty", "Henüz işlem yok.", UiWeight.Regular, list);
            for (int i = transactions.Count - 1; i >= Mathf.Max(0, transactions.Count - 12); i--)
            {
                var t = transactions[i];
                KeyValue(list, $"Gün {t.day + 1} · {CategoryNames[t.category]}", Fmt.Signed(t.amount), Tone(t.amount), CategoryIcon(t.category));
            }
        }

        void SheetHeader(string title, string subtitle)
        {
            tkSheetTitle.text = title;
            tkSheetSubtitle.text = subtitle ?? string.Empty;
            Ui.Show(tkSheetSubtitle, !string.IsNullOrEmpty(subtitle));
        }

        void BuildGoals(VisualElement body)
        {
            GoalTracker.Goal current = goals.Current;
            foreach (var goal in goals.Goals)
            {
                bool done = goals.IsCompleted(goal);
                VisualElement card = Ui.El("bt-item", body);
                Ui.Toggle(card, "is-done", done);
                Ui.Toggle(card, "is-current", goal == current);
                VisualElement row = Ui.El("bt-row", card);
                Ui.Icon(done ? UIIcon.Check : goal == current ? UIIcon.Goals : UIIcon.Lock, "bt-item__icon", row);
                Ui.Text("bt-item__title bt-grow", goal.Title, UiWeight.Bold, row);
                if (done) Ui.Text("bt-pill is-success", "Tamam", UiWeight.Bold, row);
                else if (goal == current) Ui.Text("bt-pill is-warning", "Sıradaki", UiWeight.Bold, row);
                Ui.Text("bt-item__text", goal.Hint, UiWeight.Regular, card);
                if (goal.Reward > 0f) Ui.Text("bt-item__reward", $"Ödül: {Fmt.Money(goal.Reward)}", UiWeight.Bold, card);
            }
        }

        void BuildLog(VisualElement body)
        {
            var log = notes.Log;
            if (log.Count == 0) Ui.Text("bt-empty", "Henüz olay yok.", UiWeight.Regular, body);
            for (int i = log.Count - 1; i >= 0; i--)
            {
                VisualElement row = Ui.El("bt-log", body);
                Ui.Icon(ToastIcon(log[i].Color), "bt-log__icon", row);
                Ui.Text("bt-log__day", $"Gün {log[i].Day + 1}", UiWeight.Bold, row);
                Ui.Text("bt-log__text", log[i].Display, UiWeight.Regular, row);
            }
        }

        void BuildHelp(VisualElement body)
        {
            foreach (var (title, text) in HelpSections())
            {
                Ui.Text("bt-help__title", title, UiWeight.Bold, body);
                Ui.Text("bt-help__text", text, UiWeight.Regular, body);
            }
        }
    }
}
