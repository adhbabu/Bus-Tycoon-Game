using System;
using System.Collections.Generic;
using BusTycoon.Data;
using BusTycoon.Economy;
using BusTycoon.Fleet;
using BusTycoon.Routes;
using UnityEngine;

namespace BusTycoon.Game
{
    // Sağ yan paneller: Hatlarım, Filo, Galeri, Finans, Bölgeler, Görevler, Günlük, Yardım.
    public sealed partial class GameUI
    {
        static readonly Dictionary<LedgerCategory, string> CategoryNames = new Dictionary<LedgerCategory, string>
        {
            [LedgerCategory.TicketRevenue] = "Bilet geliri",
            [LedgerCategory.BusSale] = "Otobüs satışı",
            [LedgerCategory.Fuel] = "Yakıt",
            [LedgerCategory.DriverWages] = "Şoför maaşları",
            [LedgerCategory.Maintenance] = "Bakım",
            [LedgerCategory.Tolls] = "Otoyol",
            [LedgerCategory.TerminalFees] = "Otogar",
            [LedgerCategory.PassengerService] = "İkram / servis",
            [LedgerCategory.Insurance] = "Sigorta",
            [LedgerCategory.Repairs] = "Tamir / revizyon",
            [LedgerCategory.BusPurchase] = "Otobüs alımı",
            [LedgerCategory.RouteLicense] = "Hat lisansı",
            [LedgerCategory.RegionLicense] = "Bölge lisansı",
            [LedgerCategory.Other] = "Görev ödülü / diğer",
        };

        static readonly Dictionary<LedgerCategory, UIIcon> CategoryIcons = new Dictionary<LedgerCategory, UIIcon>
        {
            [LedgerCategory.TicketRevenue] = UIIcon.Ticket,
            [LedgerCategory.BusSale] = UIIcon.Fleet,
            [LedgerCategory.Fuel] = UIIcon.Fuel,
            [LedgerCategory.DriverWages] = UIIcon.Pilot,
            [LedgerCategory.Maintenance] = UIIcon.Wrench,
            [LedgerCategory.Tolls] = UIIcon.Road,
            [LedgerCategory.TerminalFees] = UIIcon.NewRoute,
            [LedgerCategory.PassengerService] = UIIcon.Coffee,
            [LedgerCategory.Insurance] = UIIcon.Shield,
            [LedgerCategory.Repairs] = UIIcon.Tools,
            [LedgerCategory.BusPurchase] = UIIcon.Dealership,
            [LedgerCategory.RouteLicense] = UIIcon.Log,
            [LedgerCategory.RegionLicense] = UIIcon.Regions,
            [LedgerCategory.Other] = UIIcon.Gift,
        };

        static UIIcon CategoryIcon(LedgerCategory category) => CategoryIcons.TryGetValue(category, out UIIcon icon) ? icon : UIIcon.Finance;

        Vector2 listScroll, fleetScroll, dealerScroll, financeScroll, regionScroll, goalScroll, logScroll;
        readonly List<RouteState> sortedRoutes = new List<RouteState>();
        bool fleetIdleOnly;
        string expandedBusId;
        bool dealershipUsed;
        RouteState dealershipTargetRoute;
        ChartTexture financeChart;
        bool financeChartDirty = true;

        void DrawPanel()
        {
            if (panel == Panel.None) return;
            var rect = PanelRect;
            uiRects.Add(rect);
            GUILayout.BeginArea(rect, s.Panel);
            switch (panel)
            {
                case Panel.Planner: DrawPlanner(); break;
                case Panel.Route: DrawRoutePanel(); break;
                case Panel.RouteList: DrawRouteList(); break;
                case Panel.Fleet: DrawFleet(); break;
                case Panel.Dealership: DrawDealership(); break;
                case Panel.Finance: DrawFinance(); break;
                case Panel.Regions: DrawRegions(); break;
                case Panel.Goals: DrawGoals(); break;
                case Panel.Log: DrawLog(); break;
                case Panel.Help: DrawHelpPanel(); break;
            }
            GUILayout.EndArea();
        }

        // ------------------------------------------------------------------ Hatlarım

        void DrawRouteList()
        {
            var routes = session.Routes;
            var fleet = session.Fleet;
            double total = 0;
            foreach (var route in routes.Routes) total += route.LastReport.Profit;

            PanelHeader("Hatlarım", $"{routes.Routes.Count} / {session.Progression.MaxRoutes} hat · dün toplam {Fmt.Signed(total)}");
            if (GUILayout.Button("+ Yeni hat aç", s.Primary, Ht(38f))) Defer(() => OpenPlannerNow(null));

            if (routes.Routes.Count == 0)
            {
                Sp(10f);
                GUILayout.Label("Henüz hattınız yok. Haritada bir şehre tıklayarak başlayın.", s.Wrap);
                return;
            }

            sortedRoutes.Clear();
            sortedRoutes.AddRange(routes.Routes);
            sortedRoutes.Sort((x, y) =>
            {
                var ix = RouteAdvisor.GetTopInsight(x, fleet.CountBusesOnRoute(x.routeId));
                var iy = RouteAdvisor.GetTopInsight(y, fleet.CountBusesOnRoute(y.routeId));
                return ix.Severity != iy.Severity ? iy.Severity.CompareTo(ix.Severity) : y.LastReport.Profit.CompareTo(x.LastReport.Profit);
            });

            listScroll = BeginScroll(listScroll);
            foreach (var route in sortedRoutes)
            {
                int buses = fleet.CountBusesOnRoute(route.routeId);
                RouteInsight insight = RouteAdvisor.GetTopInsight(route, buses);
                RouteDayReport last = route.LastReport;

                GUILayout.BeginVertical(s.Card);
                GUILayout.BeginHorizontal();
                GUILayout.Label(RouteName(route), s.CardTitle);
                Badge(InsightTag(insight.Severity), InsightBadge(insight.Severity));
                GUILayout.FlexibleSpace();
                ColorLabel(route.HasHistory ? $"{Fmt.Signed(last.Profit)}/gün" : "—", last.Profit >= 0 ? UIStyles.Green : UIStyles.Red, s.Bold);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{buses} otobüs · {Fmt.Price(route.ticketPrice)} · {Fmt.Num(last.passengers)} yolcu", s.Small);
                GUILayout.FlexibleSpace();
                GUILayout.Label(Fmt.Pct(last.loadFactor), s.Small);
                LayoutBar(last.loadFactor, UIStyles.Blue, 6f, 80f, 8f);
                GUILayout.EndHorizontal();

                ColorLabel(insight.Message, InsightColor(insight.Severity), s.Small);

                GUILayout.BeginHorizontal();
                RouteState target = route;
                if (GUILayout.Button("Fiyat", s.Button, Ht(30f))) Defer(() => OpenRoutePanelNow(target, RouteTab.Price));
                if (GUILayout.Button("Otobüsler", s.Button, Ht(30f))) Defer(() => OpenRoutePanelNow(target, RouteTab.Buses));
                if (GUILayout.Button("Geçmiş", s.Button, Ht(30f))) Defer(() => OpenRoutePanelNow(target, RouteTab.History));
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------ Filo

        void DrawFleet()
        {
            var fleet = session.Fleet;
            int idle = 0;
            double value = 0;
            foreach (var bus in fleet.Buses)
            {
                if (!bus.IsAssigned) idle++;
                value += fleet.GetResaleValue(bus);
            }

            PanelHeader("Filo / Garaj", $"{fleet.Buses.Count} otobüs · {idle} garajda · filo değeri {Fmt.Money(value)}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Tümü", !fleetIdleOnly ? s.TabActive : s.Tab, Ht(32f))) Defer(() => fleetIdleOnly = false);
            if (GUILayout.Button($"Garajdakiler ({idle})", fleetIdleOnly ? s.TabActive : s.Tab, Ht(32f))) Defer(() => fleetIdleOnly = true);
            if (GUILayout.Button("Galeri", s.Primary, Wd(90f), Ht(32f))) Defer(() => OpenDealershipNow(null));
            GUILayout.EndHorizontal();

            fleetScroll = BeginScroll(fleetScroll);
            if (fleet.Buses.Count == 0) GUILayout.Label("Filonuz boş. Galeriden otobüs alın.", s.Dim);

            foreach (var bus in new List<BusInstance>(fleet.Buses))
            {
                if (fleetIdleOnly && bus.IsAssigned) continue;
                if (bus.Model == null) continue;
                DrawFleetCard(bus);
            }
            GUILayout.EndScrollView();
        }

        /// <summary>
        /// Otobüs kartı: başlık + durum rozeti (Yolda / Garajda / Arızalı), ikonlu ayrıntı satırları ve iki ana
        /// düğme (hatta atama birincil); altında revizyon, satış ve takasla yükseltme.
        /// </summary>
        void DrawFleetCard(BusInstance bus)
        {
            var fleet = session.Fleet;
            RouteState route = bus.IsAssigned ? session.Routes.GetRoute(bus.assignedRouteId) : null;
            string busId = bus.busId;

            GUILayout.BeginVertical(s.Card);
            GUILayout.BeginHorizontal();
            UIBusArt.Layout(bus.Model, 30f * u);
            Sp(6f);
            GUILayout.BeginVertical();
            Sp(2f);
            GUILayout.Label(bus.nickname, s.CardTitle);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (!bus.IsAvailableToday) Badge("Arızalı", s.BadgeDanger);
            else if (route != null) Badge("Yolda", s.BadgeSuccess);
            else Badge("Garajda", s.BadgeWarning);
            GUILayout.EndHorizontal();
            Sp(4f);

            DetailRow(UIIcon.Pin, route != null ? $"Hat: {RouteName(route)}" : "Hat: atanmadı (garajda sigorta ödüyor)");
            DetailRow(UIIcon.Seat, $"{bus.Model.DisplayName} · {SegmentName(bus.Model.Segment)} · {bus.Model.Seats} koltuk");
            if (route != null && route.HasHistory) DetailRow(UIIcon.People, $"Doluluk: {Fmt.Pct(route.LastReport.loadFactor)} · hat dün {Fmt.Num(route.LastReport.passengers)} yolcu");
            GUILayout.BeginHorizontal();
            DetailRow(UIIcon.Wrench, $"Durum: {Fmt.Pct(bus.condition)}", Wd(150f));
            LayoutBar(bus.condition, bus.condition > 0.6f ? UIStyles.Green : bus.condition > 0.35f ? UIStyles.Yellow : UIStyles.Red, 6f, 90f, 11f);
            GUILayout.FlexibleSpace();
            DetailRow(UIIcon.Road, $"{Fmt.Num(bus.odometerKm)} km");
            GUILayout.EndHorizontal();
            if (!bus.IsAvailableToday) DetailRow(UIIcon.Warning, $"{bus.outOfServiceDays} gün servis dışı", color: UIStyles.Orange);
            Sp(8f);

            // Ana düğmeler: hattaysa "Garaja çek" + "Hatta ata"; garajdaysa "Revizyon" + "Hatta ata" (turuncu)
            bool expanded = expandedBusId == busId;
            float overhaul = fleet.GetOverhaulCost(bus);
            bool canOverhaul = bus.condition < session.Economy.OverhaulTargetCondition - 0.01f;
            GUILayout.BeginHorizontal();
            if (route != null)
            {
                if (GUILayout.Button("Garaja çek", s.Button, Ht(44f)))
                    Defer(() =>
                    {
                        session.Routes.TryAssignBus(busId, null);
                        expandedBusId = null;
                        Toast("Otobüs garaja çekildi.", UIStyles.Blue);
                    });
            }
            else if (EnabledButton(canOverhaul, $"Revizyon ({Fmt.Compact(overhaul)})", s.Button, Ht(44f)))
            {
                Defer(() => Overhaul(busId));
            }
            if (GUILayout.Button(expanded ? "Kapat" : "Hatta ata", expanded ? s.Button : (route == null ? s.Primary : s.TabActive), Ht(44f)))
                Defer(() => expandedBusId = expanded ? null : busId);
            GUILayout.EndHorizontal();

            // İkincil eylemler
            float resale = fleet.GetResaleValue(bus);
            GUILayout.BeginHorizontal();
            if (route != null && EnabledButton(canOverhaul, $"Revizyon {Fmt.Compact(overhaul)}", s.Tab, Ht(34f)))
                Defer(() => Overhaul(busId));
            if (GUILayout.Button($"Sat {Fmt.Compact(resale)}", s.Danger, Ht(34f)))
            {
                string nickname = bus.nickname;
                Defer(() => AskConfirm("Otobüs satışı", $"{nickname} {Fmt.Money(resale)} karşılığında satılacak.", () =>
                {
                    var result = session.Fleet.TrySell(busId);
                    Toast(result.Message, result ? UIStyles.Green : UIStyles.Red);
                }));
            }
            GUILayout.EndHorizontal();

            BusModelData upgrade = BestUnlockedUpgrade(bus.Model);
            if (upgrade != null)
            {
                float net = fleet.GetPurchasePrice(upgrade) - resale;
                if (EnabledButton(session.Finance.CanAfford(net), $"Takasla yükselt: {upgrade.DisplayName} (net {Fmt.Compact(net)})", s.Tab, Ht(34f)))
                {
                    BusModelData target = upgrade;
                    Defer(() => AskConfirm("Takasla yükselt",
                        $"{bus.nickname} satılıp yerine sıfır {target.DisplayName} alınacak ve aynı hatta atanacak. Net maliyet {Fmt.Money(net)}.",
                        () =>
                        {
                            var result = session.Fleet.TryTradeIn(busId, target, out _);
                            Toast(result.Message, result ? UIStyles.Green : UIStyles.Red);
                            goals.Evaluate();
                        }));
                }
            }

            if (expanded)
            {
                Sp(4f);
                GUILayout.Label("Hangi hatta çalışsın?", s.Dim);
                foreach (var candidate in session.Routes.Routes)
                {
                    bool current = candidate.routeId == bus.assignedRouteId;
                    RouteState target = candidate;
                    string label = $"{RouteName(candidate)} · {session.Fleet.CountBusesOnRoute(candidate.routeId)} otobüs" + (current ? " (şu an)" : "");
                    if (EnabledButton(!current, label, s.Ghost, Ht(34f)))
                        Defer(() =>
                        {
                            session.Routes.TryAssignBus(busId, target.routeId);
                            expandedBusId = null;
                            Toast($"Otobüs {RouteName(target)} hattına atandı.", UIStyles.Green);
                        });
                }
                if (session.Routes.Routes.Count == 0) GUILayout.Label("Önce bir hat açın (Yeni Hat).", s.WrapDim);
            }
            GUILayout.EndVertical();
        }

        void Overhaul(string busId)
        {
            var result = session.Fleet.TryOverhaul(busId);
            Toast(result.Message, result ? UIStyles.Green : UIStyles.Red);
        }

        /// <summary>Durum rozeti: renkli yazı, aynı rengin saydam zemini.</summary>
        void Badge(string text, GUIStyle style)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(false)); // genişlemesin: yanındaki yazı rozete yaslansın
            Sp(2f);
            GUILayout.Label(text, style, GUILayout.ExpandWidth(false));
            GUILayout.EndVertical();
        }

        /// <summary>Ayrıntı satırı: küçük ikon + soluk yazı (mockup'taki 📍 👥 ⛽ satırları).</summary>
        void DetailRow(UIIcon icon, string text, params GUILayoutOption[] options) => DetailRow(icon, text, null, options);

        void DetailRow(UIIcon icon, string text, Color? color, params GUILayoutOption[] options)
        {
            GUILayout.BeginHorizontal(options);
            LayoutIcon(icon, 16f, 4f);
            Sp(4f);
            if (color.HasValue) ColorLabel(text, color.Value, s.Detail);
            else GUILayout.Label(text, s.Detail);
            GUILayout.EndHorizontal();
        }

        /// <summary>Açık modeller arasında mevcut kademeden yüksek, en üst kademedeki en ucuz model.</summary>
        BusModelData BestUnlockedUpgrade(BusModelData current)
        {
            BusModelData best = null;
            foreach (var model in session.Catalog.SortedByTier)
            {
                if (model.Tier <= current.Tier || !session.Progression.IsModelUnlocked(model)) continue;
                if (best == null || model.Tier > best.Tier) best = model;
            }
            return best;
        }

        // ------------------------------------------------------------------ Galeri

        void OpenDealershipNow(RouteState targetRoute)
        {
            if (mode != Mode.Playing) return;
            panel = Panel.Dealership;
            dealershipTargetRoute = targetRoute;
            dealerScroll = Vector2.zero;
        }

        void DrawDealership()
        {
            PanelHeader("Otobüs Galerisi", $"Kasa {Fmt.Money(session.Finance.Cash)}");

            if (dealershipTargetRoute != null && session.Routes.GetRoute(dealershipTargetRoute.routeId) != null)
            {
                GUILayout.BeginHorizontal(s.Card);
                ColorLabel($"Alınan otobüs {RouteName(dealershipTargetRoute)} hattına atanacak.", UIStyles.Blue, s.Wrap);
                if (GUILayout.Button("Hatta dön", s.Button, Wd(100f)))
                {
                    RouteState route = dealershipTargetRoute;
                    Defer(() => OpenRoutePanelNow(route, RouteTab.Buses));
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Sıfır", !dealershipUsed ? s.TabActive : s.Tab, Ht(32f))) Defer(() => dealershipUsed = false);
            if (GUILayout.Button("2. el (durum %60, ucuz ama yıpranmış)", dealershipUsed ? s.TabActive : s.Tab, Ht(32f))) Defer(() => dealershipUsed = true);
            GUILayout.EndHorizontal();

            BusModelData next = session.Progression.GetNextLockedModel();
            if (next != null) ColorLabel($"Sıradaki: {next.DisplayName} — {session.Progression.GetLockReason(next)}", UIStyles.Gold, s.WrapDim);

            float condition = dealershipUsed ? session.ProgressionConfig.StartingBusCondition : 1f;
            dealerScroll = BeginScroll(dealerScroll);
            foreach (var model in session.Catalog.SortedByTier) DrawModelCard(model, condition);
            GUILayout.EndScrollView();
        }

        void DrawModelCard(BusModelData model, float condition)
        {
            bool unlocked = session.Progression.IsModelUnlocked(model);
            float price = session.Fleet.GetPurchasePrice(model, condition);
            int owned = 0;
            foreach (var bus in session.Fleet.Buses) if (bus.Model == model) owned++;

            GUILayout.BeginVertical(s.Card);
            GUILayout.BeginHorizontal();
            // Otobüsün çizimi (kilitliyse soluk) ve yanında künyesi
            GUILayout.BeginVertical(GUILayout.ExpandWidth(false));
            Sp(4f);
            UIBusArt.Layout(model, 64f * u, unlocked ? 1f : 0.4f);
            GUILayout.EndVertical();
            Sp(8f);
            GUILayout.BeginVertical();
            ColorLabel(model.DisplayName, unlocked ? UIStyles.Text : UIStyles.DimText, s.H2);
            GUILayout.Label($"{model.Manufacturer} · {SegmentName(model.Segment)} · kademe {model.Tier + 1}", s.Detail);
            DetailRow(UIIcon.Seat, $"{model.Seats} koltuk · {model.AverageSpeedKmh:0} km/s");
            DetailRow(UIIcon.Road, $"Rahat menzil {Fmt.Km(model.ComfortableRangeKm)}");
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            DetailRow(UIIcon.Star, "Konfor", Wd(84f));
            LayoutBar(model.Comfort, UIStyles.Gold, 6f, 110f, 11f);
            Sp(10f);
            DetailRow(UIIcon.Ticket, $"Bilet ×{model.FareMultiplier:0.00}");
            DetailRow(UIIcon.Fuel, $"{model.FuelLitersPer100Km:0} L/100 km");
            GUILayout.EndHorizontal();
            GUILayout.Label(model.Description, s.WrapDim);
            if (owned > 0) ColorLabel($"Filonuzda {owned} adet", UIStyles.Green, s.Small);

            GUILayout.BeginHorizontal();
            if (unlocked)
            {
                GUILayout.Label(Fmt.Money(price), s.H2);
                GUILayout.FlexibleSpace();
                bool affordable = session.Finance.CanAfford(price);
                if (EnabledButton(affordable, affordable ? "SATIN AL" : "Bakiye yetersiz", s.Primary, Wd(160f), Ht(36f)))
                {
                    BusModelData target = model;
                    RouteState route = dealershipTargetRoute;
                    float buyCondition = condition;
                    Defer(() => BuyBus(target, buyCondition, route));
                }
            }
            else
            {
                ColorLabel($"Kilitli: {session.Progression.GetLockReason(model)}", UIStyles.Orange, s.Wrap);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        void BuyBus(BusModelData model, float condition, RouteState route)
        {
            var result = session.Fleet.TryPurchase(model, condition, out BusInstance bus);
            if (!result)
            {
                Toast(result.Message, UIStyles.Red);
                return;
            }

            if (route != null && session.Routes.GetRoute(route.routeId) != null)
            {
                session.Routes.TryAssignBus(bus.busId, route.routeId);
                Toast($"{bus.nickname} alındı ve {RouteName(route)} hattına atandı.", UIStyles.Green);
            }
            else
            {
                Toast($"{bus.nickname} alındı. Filo (F) panelinden bir hatta atayın.", UIStyles.Green);
            }
            goals.Evaluate();
        }

        // ------------------------------------------------------------------ Finans

        void DrawFinance()
        {
            var finance = session.Finance;
            var history = session.State.financeHistory;
            double fleetValue = 0;
            foreach (var bus in session.Fleet.Buses) fleetValue += session.Fleet.GetResaleValue(bus);

            PanelHeader("Finans");
            financeScroll = BeginScroll(financeScroll);

            GUILayout.BeginVertical(s.Card);
            KeyValue("Kasa", Fmt.Money(finance.Cash), finance.Cash >= 0 ? UIStyles.Green : UIStyles.Red, UIIcon.Finance);
            KeyValue("Filo değeri (satış)", Fmt.Money(fleetValue), icon: UIIcon.Fleet);
            KeyValue("Şirket değeri", Fmt.Money(finance.Cash + fleetValue), UIStyles.Gold, UIIcon.Bank);
            double week = finance.GetNetProfit(7), month = finance.GetNetProfit(30);
            KeyValue("Son 7 gün net", Fmt.Signed(week), week >= 0 ? UIStyles.Green : UIStyles.Red, week >= 0 ? UIIcon.ChartUp : UIIcon.ChartDown);
            KeyValue("Son 30 gün net", Fmt.Signed(month), month >= 0 ? UIStyles.Green : UIStyles.Red, month >= 0 ? UIIcon.ChartUp : UIIcon.ChartDown);
            KeyValue("Taşınan toplam yolcu", Fmt.Num(session.State.totalPassengers), icon: UIIcon.People);
            GUILayout.EndVertical();

            GUILayout.Label("Günlük net (son 60 gün)", s.H2);
            if (financeChart == null) financeChart = new ChartTexture(640, 180);
            if (financeChartDirty && Event.current.type == EventType.Layout) RenderFinanceChart();
            Rect chartRect = GUILayoutUtility.GetRect(10f, 120f * u, GUILayout.ExpandWidth(true), Ht(120f));
            if (Event.current.type == EventType.Repaint) GUI.DrawTexture(chartRect, financeChart.Texture);

            GUILayout.Label("Son 7 gün gelir-gider dağılımı", s.H2);
            var totals = new double[Enum.GetValues(typeof(LedgerCategory)).Length];
            for (int i = Mathf.Max(0, history.Count - 7); i < history.Count; i++)
            {
                double[] byCategory = history[i].byCategory;
                if (byCategory == null) continue;
                for (int c = 0; c < byCategory.Length && c < totals.Length; c++) totals[c] += byCategory[c];
            }
            GUILayout.BeginVertical(s.Card);
            bool any = false;
            foreach (LedgerCategory category in Enum.GetValues(typeof(LedgerCategory)))
            {
                double amount = totals[(int)category];
                if (Math.Abs(amount) < 0.5) continue;
                any = true;
                KeyValue(CategoryNames[category], Fmt.Signed(amount), amount >= 0 ? UIStyles.Green : UIStyles.Red, CategoryIcon(category));
            }
            if (!any) GUILayout.Label("Henüz hareket yok.", s.Dim);
            GUILayout.EndVertical();

            GUILayout.Label("Son büyük işlemler", s.H2);
            var transactions = session.State.recentTransactions;
            if (transactions.Count == 0) GUILayout.Label("Henüz işlem yok.", s.Dim);
            for (int i = transactions.Count - 1; i >= Mathf.Max(0, transactions.Count - 12); i--)
            {
                var t = transactions[i];
                KeyValue($"Gün {t.day + 1} · {CategoryNames[t.category]}", Fmt.Signed(t.amount), t.amount >= 0 ? UIStyles.Green : UIStyles.Red, CategoryIcon(t.category));
            }
            GUILayout.EndScrollView();
        }

        void RenderFinanceChart()
        {
            financeChartDirty = false;
            ChartTexture chart = financeChart;
            chart.Clear(ChartBackground);
            var history = session.State.financeHistory;
            const int days = 60;
            int start = Mathf.Max(0, history.Count - days);

            double max = 1, min = 0;
            for (int i = start; i < history.Count; i++)
            {
                max = Math.Max(max, history[i].Net);
                min = Math.Min(min, history[i].Net);
            }
            float range = (float)(max - min);
            float zeroY = (float)(-min) / range * (chart.Height - 1);
            float barWidth = chart.Width / (float)days;

            for (int i = start; i < history.Count; i++)
            {
                float x0 = (days - (history.Count - start) + (i - start)) * barWidth;
                float y = (float)(history[i].Net - min) / range * (chart.Height - 1);
                chart.FillRect(x0 + 1f, Mathf.Min(zeroY, y), x0 + barWidth - 1f, Mathf.Max(zeroY, y) + 1f,
                    history[i].Net >= 0 ? ChartPositive : ChartNegative);
            }
            chart.HorizontalLine(zeroY, ChartZero, 1);
            chart.Apply();
        }

        // ------------------------------------------------------------------ Bölgeler

        void DrawRegions()
        {
            PanelHeader("Bölgeler", "Yeni ülkelerde hat açmak için işletme lisansı alın.");
            regionScroll = BeginScroll(regionScroll);

            foreach (var region in session.World.Regions)
            {
                int cities = 0;
                foreach (var city in session.World.Cities) if (city.Region == region) cities++;
                bool unlocked = session.Progression.IsRegionUnlocked(region);

                GUILayout.BeginVertical(s.Card);
                GUILayout.BeginHorizontal();
                LayoutIcon(UIIcons.Flag(region.Id), 30f, 0f);
                Sp(8f);
                GUILayout.Label(region.DisplayName, s.H2);
                GUILayout.FlexibleSpace();
                if (unlocked) Badge("Açık", s.BadgeSuccess);
                else Badge("Kilitli", s.BadgeWarning);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                DetailRow(UIIcon.Building, $"{cities} şehir");
                DetailRow(UIIcon.Ticket, $"bilet ×{region.FareMultiplier:0.00}");
                DetailRow(UIIcon.Fuel, $"yakıt ×{region.FuelPriceMultiplier:0.00}");
                DetailRow(UIIcon.Pilot, $"maaş ×{region.WageMultiplier:0.00}");
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (!unlocked)
                {
                    var check = session.Progression.CanUnlockRegion(region);
                    GUILayout.Label($"Seviye {region.RequiredCompanyLevel} · lisans {Fmt.Money(region.OperatingLicenseCost)}", s.Small);
                    GUILayout.FlexibleSpace();
                    if (EnabledButton(check.Success, "Lisans al", s.Primary, Wd(110f), Ht(32f)))
                    {
                        RegionData target = region;
                        Defer(() =>
                        {
                            var result = session.Progression.TryUnlockRegion(target);
                            if (!result) Toast(result.Message, UIStyles.Red);
                            goals.Evaluate();
                        });
                    }
                }
                else
                {
                    GUILayout.FlexibleSpace();
                }
                if (GUILayout.Button("Haritada göster", s.Button, Wd(140f), Ht(32f)))
                {
                    RegionData target = region;
                    Defer(() => FocusRegion(target));
                }
                GUILayout.EndHorizontal();

                if (!unlocked)
                {
                    var check = session.Progression.CanUnlockRegion(region);
                    if (!check.Success) ColorLabel(check.Message, UIStyles.DimText, s.WrapDim);
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
        }

        void FocusRegion(RegionData region)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (var marker in world.Markers)
            {
                if (marker.City.Region != region) continue;
                sum += marker.Position;
                count++;
            }
            if (count > 0) mapCamera.FocusOn(sum / count, 45f);
        }

        // ------------------------------------------------------------------ Görevler, günlük, yardım

        void DrawGoals()
        {
            PanelHeader("Görevler", $"{goals.CompletedCount} / {goals.Goals.Count} tamamlandı");
            goalScroll = BeginScroll(goalScroll);
            GoalTracker.Goal current = goals.Current;
            foreach (var goal in goals.Goals)
            {
                bool done = goals.IsCompleted(goal);
                GUILayout.BeginVertical(s.Card);
                GUILayout.BeginHorizontal();
                LayoutIcon(done ? UIIcon.Check : goal == current ? UIIcon.Goals : UIIcon.Lock, 22f, 1f);
                Sp(6f);
                ColorLabel(goal.Title, done ? UIStyles.DimText : UIStyles.Text, s.Bold);
                GUILayout.FlexibleSpace();
                if (done) Badge("Tamam", s.BadgeSuccess);
                else if (goal == current) Badge("Sıradaki", s.BadgeWarning);
                GUILayout.EndHorizontal();
                GUILayout.Label(goal.Hint, s.WrapDim);
                if (goal.Reward > 0f) ColorLabel($"Ödül: {Fmt.Money(goal.Reward)}", done ? UIStyles.DimText : UIStyles.Gold, s.Small);
                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
        }

        void DrawLog()
        {
            PanelHeader("Günlük", "Son olaylar");
            logScroll = BeginScroll(logScroll);
            var log = notes.Log;
            if (log.Count == 0) GUILayout.Label("Henüz olay yok.", s.Dim);
            for (int i = log.Count - 1; i >= 0; i--)
            {
                GUILayout.BeginHorizontal();
                LayoutIcon(ToastIcon(log[i].Color), 16f, 3f);
                Sp(4f);
                GUILayout.Label($"Gün {log[i].Day + 1}", s.Small, Wd(64f));
                ColorLabel(log[i].Text, log[i].Color, s.Wrap);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

        void DrawHelpPanel()
        {
            PanelHeader("Nasıl Oynanır?");
            helpScroll = BeginScroll(helpScroll);
            DrawHelpContent();
            GUILayout.EndScrollView();
        }
    }
}
