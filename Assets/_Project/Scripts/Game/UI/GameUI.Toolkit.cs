using System;
using System.Collections.Generic;
using BusTycoon.Core;
using BusTycoon.Data;
using BusTycoon.Multiplayer;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// Arayüz (UI Toolkit) kabuğu: üst çubuk, alt sekmeler (ortada yuvarlak Harita düğmesi), bildirimler, menüler ve
    /// pencereler. Öğeler bir kez kurulur; her kare yalnız değişen değerler yazılır. Sayfa düzeni (ana ekran, sekme
    /// sayfaları, harita kartı) <see cref="GameUI"/>'nin ToolkitShell parçasında. IMGUI yalnız harita üstü yazıları
    /// (şehir adları, kazanç yazıları) çizer.
    /// </summary>
    public sealed partial class GameUI
    {
        UiHost toolkit;
        /// <summary>Yeni arayüz açık: IMGUI üst çubuğu, menüyü ve bildirimleri çizmez.</summary>
        bool NewUi => toolkit != null;

        enum Tab { Home, Routes, Fleet, Map, Shop, Finance, Goals }

        VisualElement tkHud, tkTop, tkBrandSub, tkKasa, tkDatePill, tkDateIcon, tkDayFill, tkPauseIcon, tkRace, tkTabs, tkMapButton, tkToasts;
        Label tkKicker, tkTitle, tkSubText, tkCash, tkDate, tkRaceText;
        Button tkPauseButton;
        readonly List<(VisualElement Button, Tab Tab, Label Badge)> tkNav = new List<(VisualElement, Tab, Label)>();
        readonly List<(VisualElement Box, VisualElement Icon, Label Text)> tkToastViews = new List<(VisualElement, VisualElement, Label)>();

        double tkShownCash = double.NaN;
        float tkCashTextTime;
        long tkCashValue = long.MinValue;
        int tkDayValue = -1, tkWarningValue = -1;
        bool? tkPausedShown;
        DayNightCycle.Phase? tkPhase;
        Tab? tkTabShown;
        /// <summary>Yeni arayüzün haritayı kapattığı yerler (piksel): şehir adları buraya çizilmez.</summary>
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

            // Sayfalar ve harita parçaları üst çubuğun ve sekmelerin altında kalır
            BuildToolkitHome();
            BuildToolkitMapWidgets();
            BuildToolkitRace();
            BuildToolkitSheet();
            BuildToolkitStation();

            // ---- üst çubuk: solda şirket (ana ekranda) ya da sayfa başlığı; sağda kasa, duraklat, kısayollar, tarih
            tkTop = Ui.El("bt-topbar", tkHud);
            VisualElement brand = Ui.El("bt-brand", tkTop);
            brand.pickingMode = PickingMode.Ignore;
            tkKicker = Ui.Text("bt-brand__kicker", "", UiWeight.Bold, brand);
            tkTitle = Ui.Text("bt-brand__title", "", UiWeight.Black, brand);
            tkBrandSub = Ui.El("bt-row bt-brand__sub", brand);
            tkBrandSub.pickingMode = PickingMode.Ignore;
            Ui.El("bt-brand__dot", tkBrandSub).pickingMode = PickingMode.Ignore;
            tkSubText = Ui.Text("bt-brand__subtext", "", UiWeight.Bold, tkBrandSub);
            Ui.El("bt-grow", tkTop).pickingMode = PickingMode.Ignore;

            tkKasa = Ui.El("bt-row bt-kasa", tkTop);
            tkKasa.pickingMode = PickingMode.Ignore;
            Ui.Text("bt-kasa__caps", "KASA", UiWeight.Bold, tkKasa);
            tkCash = Ui.Text("bt-kasa__value", "", UiWeight.Black, tkKasa);

            tkPauseButton = Ui.Button("bt-round", string.Empty, TogglePauseFromBar, tkTop);
            tkPauseButton.name = "pause";
            tkPauseIcon = Ui.Symbol("pause", "bt-round__icon", tkPauseButton);
            tkRace = Ui.El("bt-row bt-race", tkTop);
            Ui.Symbol("sports_score", "bt-race__icon", tkRace);
            tkRaceText = Ui.Text("bt-race__text", "", UiWeight.Bold, tkRace);

            Button goalsButton = Ui.Button("bt-round", string.Empty, () => OpenPage(Panel.Goals), tkTop);
            goalsButton.name = "goals";
            Ui.Symbol("trophy", "bt-round__icon is-gold", goalsButton);
            Button helpButton = Ui.Button("bt-round", string.Empty, () => OpenPage(Panel.Help), tkTop);
            helpButton.name = "help";
            Ui.Symbol("help", "bt-round__icon", helpButton);
            Button settingsButton = Ui.Button("bt-round", string.Empty, OpenPauseMenu, tkTop);
            settingsButton.name = "settings";
            Ui.Symbol("settings", "bt-round__icon", settingsButton);

            tkDatePill = Ui.El("bt-row bt-date", tkTop);
            tkDatePill.pickingMode = PickingMode.Ignore;
            tkDateIcon = Ui.Symbol("calendar_month", "bt-date__icon", tkDatePill);
            tkDate = Ui.Text("bt-date__text", "", UiWeight.Bold, tkDatePill);
            VisualElement dayTrack = Ui.El("bt-date__track", tkDatePill);
            dayTrack.pickingMode = PickingMode.Ignore;
            tkDayFill = Ui.El("bt-date__fill", dayTrack);
            tkDayFill.usageHints = UsageHints.DynamicTransform; // her kare ilerler: köşeler yeniden yüklenmesin
            tkDayFill.pickingMode = PickingMode.Ignore;

            // ---- alt sekmeler: üçer sekme iki yanda, ortada Harita düğmesinin yeri
            tkTabs = Ui.El("bt-tabs", tkHud);
            VisualElement left = Ui.El("bt-tabs__group", tkTabs);
            AddTab(left, Tab.Home, "home", "Ana ekran");
            AddTab(left, Tab.Routes, "route", "Hatlarım");
            AddTab(left, Tab.Fleet, "directions_bus", "Filo");
            Ui.El("bt-tabs__slot", tkTabs).pickingMode = PickingMode.Ignore;
            VisualElement right = Ui.El("bt-tabs__group", tkTabs);
            AddTab(right, Tab.Shop, "shopping_cart", "Galeri");
            AddTab(right, Tab.Finance, "bar_chart", "Finans");
            AddTab(right, Tab.Goals, "trophy", "Görevler");

            // Ortadaki yuvarlak Harita düğmesi: çubuğun biraz üstüne taşar
            tkMapButton = Ui.El("bt-mapbutton", tkHud);
            tkMapButton.name = "map";
            tkMapButton.pickingMode = PickingMode.Ignore;
            Ui.El("bt-mapbutton__ring", tkMapButton).pickingMode = PickingMode.Ignore;
            Button disc = Ui.Button("bt-mapbutton__disc", string.Empty, () => SelectTab(Tab.Map), tkMapButton);
            Ui.Symbol("map", "bt-mapbutton__icon", disc);
            Ui.Text("bt-mapbutton__label", "Harita", UiWeight.Black, tkMapButton);
            tkNav.Add((tkMapButton, Tab.Map, null));

            // ---- bildirimler
            tkToasts = Ui.El("bt-toasts", tkHud);
            tkToasts.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < 3; i++)
            {
                VisualElement box = Ui.El("bt-toast", tkToasts);
                box.usageHints = UsageHints.DynamicTransform | UsageHints.DynamicColor; // kayarak ve belirerek gelir
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

        void AddTab(VisualElement parent, Tab tab, string symbol, string label)
        {
            Button button = Ui.Button("bt-tab-item", string.Empty, () => SelectTab(tab), parent);
            button.name = "tab-" + tab.ToString().ToLowerInvariant();
            Ui.Symbol(symbol, "bt-tab-item__icon", button);
            Ui.Text("bt-tab-item__label", label, UiWeight.Bold, button);
            Ui.El("bt-tab-item__marker", button).pickingMode = PickingMode.Ignore;
            Label badge = null;
            if (tab == Tab.Routes)
            {
                badge = Ui.Text("bt-badge", "", UiWeight.Bold, button);
                badge.style.display = DisplayStyle.None;
            }
            tkNav.Add((button, tab, badge));
        }

        void TogglePauseFromBar()
        {
            if (session.IsRunning && !RaceLocked) session.Clock.TogglePause();
        }

        /// <summary>Her kare (Update): görünürlük, yerleşim ve yalnız değişen değerler.</summary>
        void UpdateToolkit()
        {
            if (toolkit == null) return;
            tkCovered.Clear();
            toolkit.SetScale(u);
            toolkit.SetClass("phone", phone);
            toolkit.SetClass("compact", compact);
            toolkit.SetClass("narrow", NarrowShell);
            PumpToolkitTaps();
            UpdateToolkitModal();
            UpdateToolkitMenu();
            UpdateToolkitNewGame();
            UpdateToolkitMultiplayer();
            UpdateToolkitTooltip();

            bool playing = mode == Mode.Playing && session.IsRunning;
            UpdateMapViewport(playing);
            Ui.Show(tkHud, playing);
            if (!playing) return;
            tkHud.SetEnabled(modal == Modal.None); // pencere açıkken arkadaki düğmeler tıklanmaz
            if (panel != Panel.None) tkHome = false; // bir sayfa ya da harita paneli açıldı

            float k = 1f / u;
            Rect content = ContentRect;
            Ui.Place(tkTop, 0f, 0f, W * k, (SafeTop + TopBarHeight) * k);
            SetPadding(tkTop, content.x * k, SafeTop * k, (W - content.xMax) * k);
            bool tabs = BottomBarVisible;
            Ui.Show(tkTabs, tabs);
            Ui.Show(tkMapButton, tabs);
            if (tabs)
            {
                float barTop = H - SafeBottom - BottomBarHeight;
                Ui.Place(tkTabs, 0f, barTop * k, W * k, (SafeBottom + BottomBarHeight) * k);
                SetPadding(tkTabs, content.x * k, 0f, (W - content.xMax) * k);
                tkTabs.style.paddingBottom = SafeBottom * k;
                Ui.Move(tkMapButton, W * 0.5f * k - MapButtonUnits * 0.5f, barTop * k - 18f);
            }

            UpdateToolkitTopBar();
            UpdateToolkitNav();
            UpdateToolkitHome(content, k);
            UpdateToolkitMapWidgets(k);
            UpdateToolkitRace(k);
            UpdateToolkitStation(k);
            UpdateToolkitSheet(k);
            UpdateToolkitToasts(k);
        }

        const float MapButtonUnits = 90f;

        static void SetPadding(VisualElement element, float left, float top, float right)
        {
            IStyle style = element.style;
            if (Mathf.Abs(style.paddingLeft.value.value - left) > 0.05f) style.paddingLeft = left;
            if (Mathf.Abs(style.paddingTop.value.value - top) > 0.05f) style.paddingTop = top;
            if (Mathf.Abs(style.paddingRight.value.value - right) > 0.05f) style.paddingRight = right;
        }

        void UpdateToolkitTopBar()
        {
            var progression = session.Progression;
            bool home = HomeShown;

            // Sol blok: ana ekranda şirket adı ve yeşil durum satırı; diğer ekranlarda üst başlık + sayfa adı
            Ui.Show(tkKicker, !home);
            Ui.Show(tkBrandSub, home);
            if (home)
            {
                Ui.SetText(tkTitle, session.State.companyName);
                CityData hq = session.Headquarters;
                Ui.SetText(tkSubText, $"Seviye {progression.Level} · İtibar {Mathf.RoundToInt(progression.Reputation)}" +
                                      (hq != null && !NarrowShell ? $" · Merkez: {hq.DisplayName}" : string.Empty));
            }
            else
            {
                ScreenHeading(out string kicker, out string title);
                Ui.SetText(tkKicker, kicker);
                Ui.SetText(tkTitle, title);
            }

            // Kasa (ana ekranda büyük kasa kartı var); sayaç gibi akar, gün sonunda birden atlamaz
            Ui.Show(tkKasa, !home);
            double cash = session.Finance.Cash;
            if (double.IsNaN(tkShownCash) || Math.Abs(cash - tkShownCash) > Math.Abs(cash) * 0.5 + 1e6) tkShownCash = cash;
            tkShownCash += (cash - tkShownCash) * (1.0 - Math.Exp(-Time.unscaledDeltaTime * 9.0));
            if (Math.Abs(cash - tkShownCash) < 1.0) tkShownCash = cash;
            long shown = (long)Math.Round(tkShownCash);
            // Sayaç yazısı en çok saniyede ~12 kez değişir: her değişen kare GPU'ya köşe yüklemesi demek (WebGL)
            bool settled = Math.Abs(cash - tkShownCash) < 1.0;
            if (shown != tkCashValue && (settled || Time.unscaledTime - tkCashTextTime >= 0.08f))
            {
                tkCashTextTime = Time.unscaledTime;
                tkCashValue = shown;
                tkCash.text = Fmt.Money(shown);
                Ui.Toggle(tkCash, "is-negative", shown < 0);
            }

            Ui.Fill(tkDayFill, session.Clock.DayProgress01);
            if (session.State.currentDay != tkDayValue)
            {
                tkDayValue = session.State.currentDay;
                tkDate.text = SimulationClock.FormatDate(tkDayValue);
            }
            DayNightCycle cycle = DayNightCycle.Instance;
            DayNightCycle.Phase? phase = cycle != null && cycle.Enabled ? cycle.CurrentPhase : (DayNightCycle.Phase?)null;
            if (phase != tkPhase)
            {
                tkPhase = phase;
                Ui.SetSymbol(tkDateIcon, phase == DayNightCycle.Phase.Night ? "dark_mode" : phase.HasValue ? "sunny" : "calendar_month");
            }

            // Duraklat (tek hız); yarışta hız odaya bağlı: yerine yarış etiketi
            bool race = RaceLocked;
            Ui.Show(tkPauseButton, !race);
            Ui.Show(tkRace, race);
            if (race)
            {
                Ui.SetText(tkRaceText, NarrowShell ? $"{Room.Speed}x" : $"{MatchRules.Name(Room.Mode)} · {Room.Speed}x");
            }
            else
            {
                bool paused = session.Clock.Speed == 0;
                if (paused != tkPausedShown)
                {
                    tkPausedShown = paused;
                    Ui.SetSymbol(tkPauseIcon, paused ? "play_arrow" : "pause");
                    Ui.Toggle(tkPauseButton, "is-paused", paused);
                }
            }
        }

        void UpdateToolkitNav()
        {
            Tab active = ActiveTab;
            if (active != tkTabShown)
            {
                tkTabShown = active;
                foreach (var item in tkNav) Ui.Toggle(item.Button, "is-active", item.Tab == active);
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
            Rect content = ContentRect;
            float top = SafeTop + TopBarHeight + (phone ? 8f : 14f) * u;
            Ui.Place(tkToasts, content.x * k, top * k, content.width * k, 200f);

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
