using System;
using System.Collections.Generic;
using BusTycoon.Data;
using BusTycoon.Routes;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// Ana ekran (oyun açılınca gelir). Solda kasa kartı ve en kârlı hatlar; sağda bugünün görevi, kısayollar ve
    /// haritaya çağrı. Yapı bir kez kurulur; yazılar gün sonunda ya da ilgili değer değişince yenilenir.
    /// </summary>
    public sealed partial class GameUI
    {
        VisualElement tkHomePage, tkHomeRoutes, tkHomeBadge, tkHomeBadgeDot, tkHomeHelpTile;
        Label tkHomeCash, tkHomeWorth, tkHomeBadgeText, tkHomeRouteCount, tkHomeReward, tkHomeGoalsSub, tkHomeStationSub;
        UiSparkline tkHomeChart;
        readonly Label[] tkHomeStats = new Label[4];
        readonly Dictionary<string, Label> tkHomeTileSubs = new Dictionary<string, Label>();
        readonly List<RouteState> tkHomeRouteOrder = new List<RouteState>();
        readonly List<float> tkSparkBuffer = new List<float>();
        int tkHomeSignature = int.MinValue, tkHomeRowsShown = -1;
        long tkHomeCashShown = long.MinValue;

        static readonly Color SparkUp = new Color(0.239f, 0.863f, 0.518f);    // #3DDC84
        static readonly Color SparkDown = new Color(1f, 0.392f, 0.392f);      // #FF6464

        void BuildToolkitHome()
        {
            tkHomePage = Ui.El("bt-home", tkHud);
            tkHomePage.pickingMode = PickingMode.Ignore;
            tkHomePage.style.display = DisplayStyle.None;

            // ---- sol sütun
            VisualElement left = Ui.El("bt-home__left", tkHomePage);
            left.pickingMode = PickingMode.Ignore;
            // Kasa kartı akan düzende: yazı ölçüleri ne olursa olsun içerik kartın içinde kalır
            VisualElement cash = Ui.El("bt-card bt-home-cash", left);
            VisualElement cashTop = Ui.El("bt-home-cash__top", cash);
            cashTop.pickingMode = PickingMode.Ignore;
            VisualElement cashMain = Ui.El("bt-col bt-grow", cashTop);
            cashMain.pickingMode = PickingMode.Ignore;
            VisualElement head = Ui.El("bt-row", cashMain);
            head.pickingMode = PickingMode.Ignore;
            Ui.Text("bt-caps", "KASA", UiWeight.Bold, head);
            tkHomeBadge = Ui.El("bt-row bt-status-badge", head);
            tkHomeBadgeDot = Ui.El("bt-status-badge__dot", tkHomeBadge);
            tkHomeBadgeText = Ui.Text("bt-status-badge__text", "", UiWeight.Bold, tkHomeBadge);
            tkHomeCash = Ui.Text("bt-home-cash__amount", "", UiWeight.Black, cashMain);
            tkHomeWorth = Ui.Text("bt-home-cash__worth", "", UiWeight.Bold, cashMain);
            tkHomeChart = new UiSparkline();
            tkHomeChart.AddToClassList("bt-home-cash__chart");
            cashTop.Add(tkHomeChart);
            Ui.El("bt-home-cash__divider", cash).pickingMode = PickingMode.Ignore;
            VisualElement stats = Ui.El("bt-home-cash__stats", cash);
            stats.pickingMode = PickingMode.Ignore;
            string[] statNames = { "HATLAR", "FİLO", "SON 7 GÜN", "AYLIK GİDER" };
            for (int i = 0; i < statNames.Length; i++)
            {
                VisualElement column = Ui.El("bt-col bt-home-stat", stats);
                column.pickingMode = PickingMode.Ignore;
                Ui.Text("bt-caps", statNames[i], UiWeight.Bold, column);
                tkHomeStats[i] = Ui.Text("bt-home-stat__value", "", UiWeight.Black, column);
            }

            VisualElement section = Ui.El("bt-row bt-home-section", left);
            Ui.Text("bt-home-section__title", "En kârlı hatlar", UiWeight.Black, section);
            tkHomeRouteCount = Ui.Text("bt-chip-gold", "", UiWeight.Black, section);
            Ui.El("bt-grow", section).pickingMode = PickingMode.Ignore;
            Button all = Ui.Button("bt-link", "Tümü ›", () => OpenPage(Panel.RouteList), section);
            all.name = "home-all-routes";
            tkHomeRoutes = Ui.El("bt-col bt-home-routes", left);
            tkHomeRoutes.pickingMode = PickingMode.Ignore;

            // ---- sağ sütun
            VisualElement right = Ui.El("bt-home__right", tkHomePage);
            right.pickingMode = PickingMode.Ignore;
            VisualElement today = Ui.El("bt-card bt-home-today", right);
            VisualElement todayHead = Ui.El("bt-row", today);
            todayHead.pickingMode = PickingMode.Ignore;
            Ui.Text("bt-caps", "BUGÜN", UiWeight.Bold, todayHead);
            Ui.El("bt-grow", todayHead).pickingMode = PickingMode.Ignore;
            Button reward = Ui.Button("bt-row bt-reward", string.Empty, () => OpenPage(Panel.Goals), todayHead);
            VisualElement rewardDot = Ui.El("bt-reward__dot", reward);
            rewardDot.pickingMode = PickingMode.Ignore;
            Ui.Symbol("target", "bt-reward__icon", rewardDot);
            tkHomeReward = Ui.Text("bt-reward__text", "", UiWeight.Bold, reward);
            VisualElement minis = Ui.El("bt-row bt-home-minis", today);
            minis.pickingMode = PickingMode.Ignore;
            tkHomeGoalsSub = MiniTile(minis, "Görevler", "trophy", "is-amber", () => OpenPage(Panel.Goals), false);
            tkHomeStationSub = MiniTile(minis, "Merkez durak", "storefront", "is-purple", () => EnterStationNow(session.Headquarters), true);

            VisualElement tiles = Ui.El("bt-home-tiles", right);
            tiles.pickingMode = PickingMode.Ignore;
            Tile(tiles, "newroute", "Yeni hat", "add_road", "is-amber", false, OpenNewRoutePlanner);
            Tile(tiles, "company", "Şirketim", "account_balance", "is-blue", false, () => OpenPage(Panel.Regions));
            Tile(tiles, "stations", "Duraklar", "storefront", "is-teal", false, () => EnterStationNow(session.Headquarters));
            Tile(tiles, "maintenance", "Bakım", "build", "is-slate", false, () => OpenPage(Panel.Fleet));
            Tile(tiles, "shop", "Otobüs al", "directions_bus", null, true, () => OpenPage(Panel.Dealership));
            Tile(tiles, "log", "Günlük", "receipt_long", "is-red", false, () => OpenPage(Panel.Log));
            tkHomeHelpTile = Tile(right, "help", "Nasıl oynanır?", "help", "is-red", false, () => OpenPage(Panel.Help));
            tkHomeHelpTile.AddToClassList("bt-tile--wide");
            tkHomeTileSubs["help"].text = "İpuçları ve kısayollar";

            Ui.El("bt-grow", right).pickingMode = PickingMode.Ignore;
            Button cta = Ui.Button("bt-row bt-cta bt-cta--big", string.Empty, () => SelectTab(Tab.Map), right);
            cta.name = "home-map";
            VisualElement ctaText = Ui.El("bt-col bt-grow", cta);
            ctaText.pickingMode = PickingMode.Ignore;
            Ui.Text("bt-cta__kicker", "TÜRKİYE & AVRUPA HARİTASI", UiWeight.Black, ctaText);
            Ui.Text("bt-cta__title", "Haritayı keşfet →", UiWeight.Black, ctaText);
            VisualElement go = Ui.El("bt-cta__go", cta);
            go.pickingMode = PickingMode.Ignore;
            Ui.Symbol("chevron_right", "bt-cta__go-icon", go);
        }

        Label MiniTile(VisualElement parent, string title, string symbol, string tint, Action onClick, bool accent)
        {
            Button tile = Ui.Button("bt-row bt-mini" + (accent ? " is-accent" : string.Empty), string.Empty, onClick, parent);
            VisualElement box = Ui.El("bt-tile__box " + tint, tile);
            box.pickingMode = PickingMode.Ignore;
            Ui.Symbol(symbol, "bt-tile__icon", box);
            VisualElement text = Ui.El("bt-col bt-grow", tile);
            text.pickingMode = PickingMode.Ignore;
            Ui.Text("bt-tile__title", title, UiWeight.Bold, text);
            return Ui.Text("bt-tile__sub", "", UiWeight.Regular, text);
        }

        VisualElement Tile(VisualElement parent, string key, string title, string symbol, string tint, bool highlight, Action onClick)
        {
            Button tile = Ui.Button("bt-row bt-tile" + (highlight ? " is-highlight" : string.Empty), string.Empty, onClick, parent);
            tile.name = "tile-" + key;
            VisualElement box = Ui.El("bt-tile__box" + (tint != null ? " " + tint : string.Empty), tile);
            box.pickingMode = PickingMode.Ignore;
            Ui.Symbol(symbol, "bt-tile__icon", box);
            VisualElement text = Ui.El("bt-col bt-grow", tile);
            text.pickingMode = PickingMode.Ignore;
            Ui.Text("bt-tile__title", title, UiWeight.Bold, text);
            tkHomeTileSubs[key] = Ui.Text("bt-tile__sub", "", UiWeight.Regular, text);
            return tile;
        }

        void UpdateToolkitHome(Rect content, float k)
        {
            bool show = HomeShown;
            Ui.Show(tkHomePage, show);
            if (!show)
            {
                tkHomeSignature = int.MinValue; // açılınca yeniden doldurulur
                return;
            }
            Ui.Place(tkHomePage, content.x * k, content.y * k, content.width * k, content.height * k);
            tkCovered.Add(new Rect(0f, 0f, W, H)); // harita çizilmiyor: şehir adları da yok

            if (tkCashValue != tkHomeCashShown)
            {
                tkHomeCashShown = tkCashValue;
                tkHomeCash.text = Fmt.Money(tkCashValue);
                Ui.Toggle(tkHomeCash, "is-negative", tkCashValue < 0);
            }

            // Sığan hat satırı sayısı (telefonda 2-3, masaüstünde daha çok)
            float available = content.height * k - 190f - 40f; // kasa kartı ~190, başlık ~40 birim
            int rows = Mathf.Clamp(Mathf.FloorToInt(available / 66f), 1, 6);
            bool roomForHelp = content.height * k >= 400f;
            Ui.Show(tkHomeHelpTile, roomForHelp);

            int signature = session.State.currentDay * 7919 + session.Routes.Routes.Count * 131 + session.Fleet.Buses.Count * 17 +
                            goals.CompletedCount * 3 + rows + session.Progression.Level * 1009;
            if (signature == tkHomeSignature) return;
            tkHomeSignature = signature;
            RefreshHome(rows);
        }

        void RefreshHome(int rows)
        {
            var finance = session.Finance;
            var history = session.State.financeHistory;

            // Rozet: kârda / zararda / iflas yaklaşıyor
            double lastNet = LastDayNet();
            bool debt = finance.DaysInDebt > 0;
            string badge = debt ? $"İflasa {session.ProgressionConfig.BankruptcyGraceDays - finance.DaysInDebt} gün" : lastNet >= 0 ? "Kârda" : "Zararda";
            tkHomeBadgeText.text = badge;
            Ui.Toggle(tkHomeBadge, "is-down", debt || lastNet < 0);

            double fleetValue = 0;
            foreach (var bus in session.Fleet.Buses) fleetValue += session.Fleet.GetResaleValue(bus);
            tkHomeWorth.text = $"şirket değeri {Fmt.Money(finance.Cash + fleetValue)} · dün {Fmt.Signed(lastNet)}";

            tkSparkBuffer.Clear();
            for (int i = Mathf.Max(0, history.Count - 30); i < history.Count; i++) tkSparkBuffer.Add((float)history[i].cashAtClose);
            if (tkSparkBuffer.Count == 1) tkSparkBuffer.Insert(0, tkSparkBuffer[0]);
            tkHomeChart.SetData(tkSparkBuffer, tkSparkBuffer.Count >= 2 && tkSparkBuffer[tkSparkBuffer.Count - 1] < tkSparkBuffer[0] ? SparkDown : SparkUp);

            double monthlyCost = 0;
            for (int i = Mathf.Max(0, history.Count - 30); i < history.Count; i++) monthlyCost += history[i].expenses;
            double week = finance.GetNetProfit(7);
            tkHomeStats[0].text = $"{session.Routes.Routes.Count} / {session.Progression.MaxRoutes}";
            tkHomeStats[1].text = $"{session.Fleet.Buses.Count} otobüs";
            tkHomeStats[2].text = Fmt.Signed(week);
            Ui.Toggle(tkHomeStats[2], "is-up", week >= 0);
            Ui.Toggle(tkHomeStats[2], "is-down", week < 0);
            tkHomeStats[3].text = monthlyCost > 0 ? "−" + Fmt.Money(monthlyCost) : Fmt.Money(0);

            // En kârlı hatlar
            var routes = session.Routes.Routes;
            tkHomeRouteCount.text = $"{routes.Count} HAT";
            tkHomeRoutes.Clear();
            tkHomeRouteOrder.Clear();
            tkHomeRouteOrder.AddRange(routes);
            tkHomeRouteOrder.Sort((a, b) => b.LastReport.Profit.CompareTo(a.LastReport.Profit));
            if (tkHomeRouteOrder.Count == 0)
            {
                Button empty = Ui.Button("bt-col bt-home-empty", string.Empty, OpenNewRoutePlanner, tkHomeRoutes);
                empty.name = "home-first-route";
                VisualElement plus = Ui.El("bt-home-empty__plus", empty);
                plus.pickingMode = PickingMode.Ignore;
                Ui.Symbol("add", "bt-home-empty__icon", plus);
                Ui.Text("bt-home-empty__title", "İlk hattını aç", UiWeight.Black, empty);
                Ui.Text("bt-home-empty__sub", "Haritada iki şehir seç", UiWeight.Regular, empty);
            }
            for (int i = 0; i < Mathf.Min(rows, tkHomeRouteOrder.Count); i++) RouteRow(tkHomeRoutes, tkHomeRouteOrder[i]);

            // Bugün: sıradaki görev, görev ilerlemesi, merkez durak
            GoalTracker.Goal goal = goals.Current;
            tkHomeReward.text = goal != null ? (goal.Reward > 0 ? $"{goal.Title} · {Fmt.Money(goal.Reward)}" : goal.Title) : "Bütün görevler tamam";
            tkHomeGoalsSub.text = $"{goals.CompletedCount} / {goals.Goals.Count} tamam";
            CityData hq = session.Headquarters;
            if (hq != null)
            {
                float stars = session.Stations.Popularity(hq).Stars;
                tkHomeStationSub.text = $"{hq.DisplayName} · {stars:0.#} yıldız";
            }

            // Kısayolların alt yazıları
            int unlocked = 0;
            foreach (var region in session.World.Regions) if (session.Progression.IsRegionUnlocked(region)) unlocked++;
            tkHomeTileSubs["newroute"].text = routes.Count < session.Progression.MaxRoutes ? "Şehir seç, hat aç" : "Hat sınırı dolu";
            tkHomeTileSubs["company"].text = $"{unlocked} / {session.World.Regions.Count} ülke lisansı";
            tkHomeTileSubs["stations"].text = hq != null ? $"{session.Stations.UpgradeCount(hq)} geliştirme" : "Durak geliştir";
            tkHomeTileSubs["maintenance"].text = session.Maintenance.Enabled ? "Asistan açık" : "Asistan kapalı";
            tkHomeTileSubs["shop"].text = "Al · sat · yükselt";
            tkHomeTileSubs["log"].text = $"{notes.Log.Count} olay";
        }

        void RouteRow(VisualElement parent, RouteState route)
        {
            Color identity = world.RouteIdentityColor(route.routeId);
            Button row = Ui.Button("bt-row bt-card bt-home-route", string.Empty, () => OpenRoutePanelNow(route, RouteTab.Price), parent);
            VisualElement bar = Ui.El("bt-home-route__bar", row);
            bar.pickingMode = PickingMode.Ignore;
            bar.style.backgroundColor = identity;
            VisualElement text = Ui.El("bt-col bt-grow", row);
            text.pickingMode = PickingMode.Ignore;
            Label name = Ui.Text("bt-home-route__name", RouteName(route), UiWeight.Black, text);
            name.style.color = Color.Lerp(identity, Color.white, 0.35f);
            RouteDayReport last = route.LastReport;
            int buses = session.Fleet.CountBusesOnRoute(route.routeId);
            Ui.Text("bt-home-route__info", route.HasHistory ? $"{buses} otobüs · %{Mathf.RoundToInt(last.loadFactor * 100f)} dolu" : $"{buses} otobüs · yeni",
                UiWeight.Regular, text);

            tkSparkBuffer.Clear();
            if (route.history != null)
                for (int i = Mathf.Max(0, route.history.Count - 14); i < route.history.Count; i++) tkSparkBuffer.Add(route.history[i].Profit);
            bool up = route.history == null || route.history.Count < 2 || route.history[route.history.Count - 1].Profit >= route.history[route.history.Count - 2].Profit;
            if (tkSparkBuffer.Count >= 2)
            {
                var spark = new UiSparkline();
                spark.AddToClassList("bt-home-route__spark");
                spark.SetData(tkSparkBuffer, up ? SparkUp : SparkDown);
                row.Add(spark);
            }

            VisualElement value = Ui.El("bt-col bt-home-route__value", row);
            value.pickingMode = PickingMode.Ignore;
            float profit = route.HasHistory ? last.Profit : 0f;
            Ui.Text("bt-home-route__money", Fmt.Signed(profit), UiWeight.Black, value);
            string change = "günlük";
            if (route.history != null && route.history.Count >= 2)
            {
                float before = route.history[route.history.Count - 2].Profit;
                if (Mathf.Abs(before) > 1f)
                {
                    float percent = (profit - before) / Mathf.Abs(before) * 100f;
                    change = (percent >= 0f ? "+%" : "−%") + Mathf.Abs(percent).ToString("0.0");
                }
            }
            Ui.Text("bt-change " + (up ? "is-up" : "is-down"), change, UiWeight.Bold, value);
        }
    }
}
