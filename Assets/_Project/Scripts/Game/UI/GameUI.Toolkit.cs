using System;
using System.Collections.Generic;
using BusTycoon.Core;
using BusTycoon.Multiplayer;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// Yeni arayüz (UI Toolkit, <c>?arayuz=yeni</c>): şimdilik üst çubuk, menü (masaüstünde solda, telefonda altta) ve
    /// bildirimler. Öğeler bir kez kurulur; her kare yalnız değişen değerler yazılır. IMGUI bu parçaları çizmez; kalan
    /// paneller geçiş sürerken IMGUI'de kalır ve aynı yerleşimi (üst çubuk yüksekliği, menü genişliği) kullanır.
    /// </summary>
    public sealed partial class GameUI
    {
        UiHost toolkit;
        /// <summary>Yeni arayüz açık: IMGUI üst çubuğu, menüyü ve bildirimleri çizmez.</summary>
        bool NewUi => toolkit != null;

        VisualElement tkHud, tkTop, tkRail, tkTabs, tkToasts, tkLevelFill, tkDayBar, tkDateIcon, tkSpeedBox, tkRace, tkStats, tkMenuButton;
        Label tkLevel, tkCompany, tkCash, tkNet, tkDate, tkRaceText, tkReputation, tkRoutes, tkFleet, tkIdle;
        readonly Dictionary<int, Button> tkSpeedButtons = new Dictionary<int, Button>();
        readonly List<(Button Button, NavEntry Entry, Label Badge)> tkNav = new List<(Button, NavEntry, Label)>();
        readonly List<(VisualElement Box, VisualElement Icon, Label Text)> tkToastViews = new List<(VisualElement, VisualElement, Label)>();

        double tkShownCash = double.NaN;
        long tkCashValue = long.MinValue;
        int tkLevelValue = -1, tkDayValue = -1, tkSpeedValue = -1, tkReputationValue = int.MinValue, tkRoutesValue = -1, tkMaxRoutesValue = -1;
        int tkFleetValue = -1, tkIdleValue = -1, tkWarningValue = -1, tkDebtDays = int.MinValue;
        double tkNetValue = double.NaN;
        DayNightCycle.Phase? tkPhase;
        Panel tkPanel = (Panel)(-1);
        bool tkNarrow;
        /// <summary>Yeni arayüzün haritayı kapattığı yerler (piksel; panel, görev kartı, yakınlaştırma): şehir adları buraya çizilmez.</summary>
        readonly List<Rect> tkCovered = new List<Rect>();

        /// <summary>Nokta (IMGUI koordinatı) yeni arayüzün bir kartının altında mı (ucuz dikdörtgen denetimi).</summary>
        bool ToolkitCovers(Vector2 guiPoint)
        {
            foreach (var rect in tkCovered)
                if (rect.Contains(guiPoint)) return true;
            return false;
        }

        void InitToolkit()
        {
            if (!UiHost.Requested || toolkit != null) return;
            toolkit = UiHost.Create(transform);
            BuildToolkitHud();
            Debug.Log("[Bus Tycoon] Yeni arayüz (UI Toolkit) açık");
        }

        void BuildToolkitHud()
        {
            VisualElement root = toolkit.Root;
            tkHud = Ui.El("bt-hud", root);
            tkHud.pickingMode = PickingMode.Ignore;
            tkHud.style.position = Position.Absolute;
            tkHud.style.left = 0f;
            tkHud.style.top = 0f;
            tkHud.style.right = 0f;
            tkHud.style.bottom = 0f;

            // ---- üst çubuk
            tkTop = Ui.El("bt-topbar", tkHud);

            VisualElement company = Ui.El("bt-topbar__block", tkTop);
            tkLevel = Ui.Text("bt-caps", "", UiWeight.Bold, company);
            tkCompany = Ui.Text("bt-company", "", UiWeight.Bold, company);
            VisualElement levelBar = Ui.El("bt-bar", company);
            levelBar.style.width = 170f;
            tkLevelFill = Ui.El("bt-bar__fill", levelBar);

            VisualElement cash = Ui.El("bt-topbar__block", tkTop);
            Ui.Text("bt-caps", "ŞİRKET KASASI", UiWeight.Bold, cash);
            VisualElement cashRow = Ui.El("bt-row", cash);
            tkCash = Ui.Text("bt-cash", "", UiWeight.Black, cashRow);
            tkNet = Ui.Text("bt-net", "", UiWeight.Bold, cashRow);

            Ui.El("bt-grow", tkTop).pickingMode = PickingMode.Ignore;

            VisualElement date = Ui.El("bt-topbar__block", tkTop);
            Ui.Text("bt-caps", "TARİH", UiWeight.Bold, date);
            VisualElement dateRow = Ui.El("bt-row", date);
            tkDateIcon = Ui.Icon(UIIcon.Sun, "bt-icon", dateRow);
            tkDate = Ui.Text("bt-date", "", UiWeight.Bold, dateRow);

            tkSpeedBox = Ui.El("bt-speed", tkTop);
            foreach ((string label, int speed) in new[] { ("II", 0), ("1x", 1), ("2x", 2), ("4x", 4) })
            {
                int value = speed;
                tkSpeedButtons[speed] = Ui.Button("bt-speed__button", label, () => session.Clock.SetSpeed(value), tkSpeedBox);
            }
            tkRace = Ui.El("bt-race", tkTop);
            Ui.Icon(UIIcon.Finish, "bt-icon", tkRace);
            tkRaceText = Ui.Text("bt-date", "", UiWeight.Bold, tkRace);

            Ui.El("bt-grow", tkTop).pickingMode = PickingMode.Ignore;

            tkStats = Ui.El("bt-row", tkTop);
            VisualElement reputation = Ui.El("bt-stat", tkStats);
            Ui.Text("bt-caps", "İTİBAR", UiWeight.Bold, reputation);
            VisualElement reputationRow = Ui.El("bt-row", reputation);
            Ui.Icon(UIIcon.Star, "bt-icon", reputationRow);
            tkReputation = Ui.Text("bt-stat__value", "", UiWeight.Bold, reputationRow);
            VisualElement routes = Ui.El("bt-stat", tkStats);
            Ui.Text("bt-caps", "HATLAR", UiWeight.Bold, routes);
            tkRoutes = Ui.Text("bt-stat__value", "", UiWeight.Bold, routes);
            VisualElement fleet = Ui.El("bt-stat", tkStats);
            Ui.Text("bt-caps", "FİLO", UiWeight.Bold, fleet);
            VisualElement fleetRow = Ui.El("bt-row", fleet);
            tkFleet = Ui.Text("bt-stat__value", "", UiWeight.Bold, fleetRow);
            tkIdle = Ui.Text("bt-stat__extra", "", UiWeight.Regular, fleetRow);

            tkMenuButton = Ui.Button("bt-menu-button", "", OpenPauseMenu, tkTop);
            Ui.Icon(UIIcon.Settings, "bt-icon", tkMenuButton);

            tkDayBar = Ui.El("bt-daybar", tkTop);
            tkDayBar.pickingMode = PickingMode.Ignore;

            // ---- menü: masaüstünde solda dikey, telefonda altta yatay
            tkRail = Ui.El("bt-rail", tkHud);
            tkTabs = Ui.El("bt-tabs", tkHud);
            foreach (NavEntry entry in NavEntries)
            {
                AddNavButton(tkRail, entry);
                if (InBottomBar(entry)) AddNavButton(tkTabs, entry);
            }
            Ui.El("bt-rail__spacer", tkRail).pickingMode = PickingMode.Ignore;
            AddRailButton(UIIcon.Save, "Kaydet", () => SaveTo(SaveSlot));
            AddRailButton(UIIcon.Settings, "Menü", OpenPauseMenu);

            // ---- harita üstü parçalar ve sağ panel
            BuildToolkitWidgets();
            BuildToolkitRace();
            BuildToolkitSheet();
            BuildToolkitStation();

            // ---- bildirimler
            tkToasts = Ui.El("bt-toasts", tkHud);
            tkToasts.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < 3; i++)
            {
                VisualElement box = Ui.El("bt-toast", tkToasts);
                box.pickingMode = PickingMode.Ignore;
                VisualElement icon = Ui.Icon(UIIcon.Info, "bt-toast__icon", box);
                Label text = Ui.Text("bt-toast__text", "", UiWeight.Bold, box);
                box.style.display = DisplayStyle.None;
                tkToastViews.Add((box, icon, text));
            }

            // ---- ana menü ve pencereler (ayrı katmanlar; pencere en üstte, tıklamayı tutar)
            BuildToolkitMenuLayer();
            BuildToolkitNewGameLayer();
            BuildToolkitMultiplayerLayer();
            BuildToolkitTooltip();
            BuildToolkitModalLayer();
        }

        void AddNavButton(VisualElement parent, NavEntry entry)
        {
            Button button = Ui.Button("bt-nav", string.Empty, () =>
            {
                if (panel == entry.Target) ClosePanel();
                else entry.Open();
            }, parent);
            Ui.Icon(entry.Icon, "bt-nav__icon", button);
            Ui.Text("bt-nav__label", entry.Label, UiWeight.Bold, button);
            Label badge = null;
            if (entry.Target == Panel.RouteList)
            {
                badge = Ui.Text("bt-badge", "", UiWeight.Bold, button);
                badge.style.display = DisplayStyle.None;
            }
            tkNav.Add((button, entry, badge));
        }

        void AddRailButton(UIIcon icon, string label, Action action)
        {
            Button button = Ui.Button("bt-nav", string.Empty, action, tkRail);
            Ui.Icon(icon, "bt-nav__icon", button);
            Ui.Text("bt-nav__label", label, UiWeight.Bold, button);
        }

        /// <summary>Her kare (Update): görünürlük, yerleşim ve yalnız değişen değerler.</summary>
        void UpdateToolkit()
        {
            if (toolkit == null) return;
            tkCovered.Clear();
            toolkit.SetScale(u);
            toolkit.SetClass("phone", phone);
            toolkit.SetClass("compact", compact);
            toolkit.SetClass("compact-top", SlimTopBar);
            PumpToolkitTaps();
            UpdateToolkitModal();
            UpdateToolkitMenu();
            UpdateToolkitNewGame();
            UpdateToolkitMultiplayer();
            UpdateToolkitTooltip();

            bool playing = mode == Mode.Playing && session.IsRunning;
            Ui.Show(tkHud, playing);
            if (!playing) return;
            tkHud.SetEnabled(modal == Modal.None); // pencere açıkken arkadaki düğmeler tıklanmaz

            // Yerleşim IMGUI ile aynı (geçiş sürerken paneller, mini harita aynı yerde kalır); tasarım birimine çevrilir
            float k = 1f / u;
            bool rail = !compact && !StationUi;
            float left = rail ? SafeLeft + SideBarWidth : 0f;
            Ui.Place(tkTop, left * k, 0f, (W - left) * k, (SafeTop + TopBarHeight) * k);
            tkTop.style.paddingTop = SafeTop * k;
            Ui.Show(tkRail, rail);
            if (rail) Ui.Place(tkRail, 0f, 0f, (SafeLeft + SideBarWidth) * k, H * k);
            bool tabs = BottomBarVisible;
            Ui.Show(tkTabs, tabs);
            if (tabs) Ui.Place(tkTabs, 0f, (H - SafeBottom - BottomBarHeight) * k, W * k, (SafeBottom + BottomBarHeight) * k);
            Ui.Show(tkMenuButton, compact);
            Ui.Show(tkStats, !SlimTopBar);

            UpdateToolkitTopBar();
            UpdateToolkitNav();
            UpdateToolkitWidgets(k);
            UpdateToolkitRace(k);
            UpdateToolkitStation(k);
            UpdateToolkitSheet(k);
            UpdateToolkitToasts(k);
        }

        void UpdateToolkitTopBar()
        {
            var progression = session.Progression;
            if (progression.Level != tkLevelValue)
            {
                tkLevelValue = progression.Level;
                tkLevel.text = SlimTopBar ? $"SEVİYE {tkLevelValue}" : $"ŞİRKET · SEVİYE {tkLevelValue}";
            }
            Ui.SetText(tkCompany, session.State.companyName);
            Ui.Fill(tkLevelFill, progression.LevelProgress01);
            Ui.Fill(tkDayBar, session.Clock.DayProgress01);

            // Kasa sayaç gibi akar (gün sonunda birden atlamasın)
            double cash = session.Finance.Cash;
            if (double.IsNaN(tkShownCash) || Math.Abs(cash - tkShownCash) > Math.Abs(cash) * 0.5 + 1e6) tkShownCash = cash;
            tkShownCash += (cash - tkShownCash) * (1.0 - Math.Exp(-Time.unscaledDeltaTime * 9.0));
            if (Math.Abs(cash - tkShownCash) < 1.0) tkShownCash = cash;
            long shown = (long)Math.Round(tkShownCash);
            if (shown != tkCashValue)
            {
                tkCashValue = shown;
                tkCash.text = Fmt.Money(shown);
                Ui.Toggle(tkCash, "is-negative", shown < 0);
            }

            bool debt = session.Finance.DaysInDebt > 0;
            int debtDays = debt ? session.ProgressionConfig.BankruptcyGraceDays - session.Finance.DaysInDebt : int.MinValue;
            double net = LastDayNet();
            bool showNet = !SlimTopBar || (SafeRight - SafeLeft) / u >= 880f;
            if (debtDays != tkDebtDays || (!debt && !net.Equals(tkNetValue)))
            {
                tkDebtDays = debtDays;
                tkNetValue = net;
                tkNet.text = debt ? $"İflasa {debtDays} gün!" : $"Dün {Fmt.Signed(net)}";
                Ui.Toggle(tkNet, "is-up", !debt && net >= 0);
                Ui.Toggle(tkNet, "is-down", debt || net < 0);
            }
            Ui.Show(tkNet, debt || showNet);

            if (session.State.currentDay != tkDayValue)
            {
                tkDayValue = session.State.currentDay;
                tkDate.text = SimulationClock.FormatDate(tkDayValue);
            }
            DayNightCycle cycle = DayNightCycle.Instance;
            bool showPhase = cycle != null && cycle.Enabled;
            Ui.Show(tkDateIcon, showPhase);
            if (showPhase && cycle.CurrentPhase != tkPhase)
            {
                tkPhase = cycle.CurrentPhase;
                Ui.SetIcon(tkDateIcon, cycle.CurrentPhase switch
                {
                    DayNightCycle.Phase.Sunrise => UIIcon.Sunrise,
                    DayNightCycle.Phase.Sunset => UIIcon.Sunset,
                    DayNightCycle.Phase.Night => UIIcon.Moon,
                    _ => UIIcon.Sun
                });
            }

            bool race = RaceLocked;
            Ui.Show(tkSpeedBox, !race);
            Ui.Show(tkRace, race);
            if (race)
            {
                Ui.SetText(tkRaceText, Narrow ? $"{Room.Speed}x" : $"{MatchRules.Name(Room.Mode)} · {Room.Speed}x");
            }
            else if (session.Clock.Speed != tkSpeedValue)
            {
                tkSpeedValue = session.Clock.Speed;
                foreach (var pair in tkSpeedButtons) Ui.Toggle(pair.Value, "is-active", pair.Key == tkSpeedValue);
            }

            if (SlimTopBar) return;
            int reputation = (int)Math.Round(progression.Reputation);
            if (reputation != tkReputationValue)
            {
                tkReputationValue = reputation;
                tkReputation.text = reputation.ToString();
            }
            if (session.Routes.Routes.Count != tkRoutesValue || progression.MaxRoutes != tkMaxRoutesValue)
            {
                tkRoutesValue = session.Routes.Routes.Count;
                tkMaxRoutesValue = progression.MaxRoutes;
                tkRoutes.text = $"{tkRoutesValue} / {tkMaxRoutesValue}";
            }
            int idle = 0;
            foreach (var bus in session.Fleet.Buses) if (!bus.IsAssigned) idle++;
            bool narrow = Narrow;
            if (session.Fleet.Buses.Count != tkFleetValue || idle != tkIdleValue || narrow != tkNarrow)
            {
                tkFleetValue = session.Fleet.Buses.Count;
                tkIdleValue = idle;
                tkNarrow = narrow;
                tkFleet.text = tkFleetValue.ToString();
                tkIdle.text = narrow ? (idle > 0 ? $"{idle} boşta" : string.Empty) : $"otobüs · {idle} garajda";
                Ui.Toggle(tkIdle, "is-warning", idle > 0);
            }
        }

        void UpdateToolkitNav()
        {
            if (panel != tkPanel)
            {
                tkPanel = panel;
                foreach (var item in tkNav) Ui.Toggle(item.Button, "is-active", item.Entry.Target == panel);
            }
            if (warningCount != tkWarningValue)
            {
                tkWarningValue = warningCount;
                foreach (var item in tkNav)
                {
                    if (item.Badge == null) continue;
                    Ui.Show(item.Badge, warningCount > 0);
                    item.Badge.text = warningCount > 9 ? "9+" : warningCount.ToString();
                }
            }
        }

        void UpdateToolkitToasts(float k)
        {
            notes.GetActive(phone ? 2 : 3, activeToasts);
            float leftEdge = ContentLeft + Margin;
            float rightEdge = (panel != Panel.None && !StationUi ? PanelRect.x : SafeRight) - Margin;
            if (rightEdge - leftEdge < 260f * u)
            {
                leftEdge = ContentLeft + Margin;
                rightEdge = SafeRight - Margin;
            }
            float top = SafeTop + TopBarHeight + (phone ? 8f : 14f) * u;
            Ui.Place(tkToasts, leftEdge * k, top * k, (rightEdge - leftEdge) * k, 200f);

            for (int i = 0; i < tkToastViews.Count; i++)
            {
                var view = tkToastViews[i];
                bool visible = i < activeToasts.Count;
                Ui.Show(view.Box, visible);
                if (!visible) continue;
                var toast = activeToasts[i];
                float age = Time.unscaledTime - toast.Time;
                float alpha = Mathf.Clamp01(Mathf.Min(age * 6f, (toast.Duration - age) * 2f));
                float slide = (1f - Mathf.Clamp01(age * 4f)) * -16f; // yukarıdan kayarak gelir
                if (!Mathf.Approximately(view.Box.style.opacity.value, alpha)) view.Box.style.opacity = alpha;
                if (age < 0.4f || view.Box.style.translate.keyword != StyleKeyword.Undefined) view.Box.style.translate = new Translate(0f, slide);
                if (view.Text.text != toast.Display)
                {
                    view.Text.text = toast.Display;
                    UIIcon icon = ToastIcon(toast.Color);
                    Ui.SetIcon(view.Icon, icon);
                    Ui.Toggle(view.Box, "is-success", icon == UIIcon.Check);
                    Ui.Toggle(view.Box, "is-error", icon == UIIcon.Cross);
                    Ui.Toggle(view.Box, "is-gold", icon == UIIcon.Trophy);
                    Ui.Toggle(view.Box, "is-warning", icon == UIIcon.Warning);
                }
            }
        }
    }
}
