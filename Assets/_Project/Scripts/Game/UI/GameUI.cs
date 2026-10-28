using System;
using System.Collections.Generic;
using BusTycoon.Core;
using BusTycoon.Data;
using BusTycoon.Economy;
using BusTycoon.Fleet;
using BusTycoon.Progression;
using BusTycoon.Routes;
using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// Oyunun tüm arayüzü (IMGUI). Kurulum gerektirmez: font, prefab, EventSystem veya Input System ayarı yok.
    /// Fare/klavye girdisi de IMGUI olaylarından okunur; bu yüzden projenin girdi ayarından bağımsızdır.
    ///
    /// Dosyalar (partial): GameUI.cs (çekirdek, girdi, harita etiketleri, modallar) · GameUI.Hud.cs ·
    /// GameUI.Menus.cs · GameUI.Planner.cs · GameUI.RoutePanel.cs · GameUI.Panels.cs
    ///
    /// IMGUI kuralı: Layout ve Repaint geçişlerinde aynı GUILayout çağrıları yapılmalıdır. Bu yüzden düzeni
    /// değiştiren tüm eylemler <see cref="Defer"/> ile OnGUI geçişinin sonuna ertelenir.
    /// </summary>
    public sealed partial class GameUI : MonoBehaviour
    {
        /// <summary>Arayüz düzenini zorlamak için (web'de ?profil=telefon, mağaza ekran görüntüleri). Auto = cihaza göre.</summary>
        internal enum DeviceProfile { Auto, Desktop, Phone, Tablet }
        internal static DeviceProfile ProfileOverride = DeviceProfile.Auto;

        enum Mode { MainMenu, NewGame, Playing }
        enum Panel { None, Planner, Route, RouteList, Fleet, Dealership, Finance, Regions, Goals, Log, Help }
        enum Modal { None, Pause, GameOver, Victory, Confirm }

        const string SaveSlot = "save1";
        const string AutosaveSlot = "autosave";

        GameSession session;
        WorldRenderer world;
        MapCamera mapCamera;
        GoalTracker goals;
        readonly Notifications notes = new Notifications();
        UIStyles s;

        Mode mode = Mode.MainMenu;
        Panel panel = Panel.None;
        Modal modal = Modal.None;

        // Ölçek: "u" = bir tasarım biriminin gerçek piksel karşılığı. Masaüstünde ekran yüksekliği 1000 birim,
        // telefonda 620 birim (daha büyük düğme ve yazı), tablette 820 birim. Tüm çizim gerçek piksellerle yapılır.
        float u = 1f;
        bool compact;   // telefon düzeni: alt menü çubuğu, dar üst çubuk
        bool mobile;    // dokunmatik platform: hover yok, kısayol ipuçları yok, parmakla kaydırma
        float W = 1920f, H = 1080f;
        float SafeLeft, SafeRight = 1920f, SafeTop, SafeBottom; // çentik/ev çubuğu güvenli alanı (GUI koordinatı)
        readonly List<Rect> uiRects = new List<Rect>();
        readonly List<Action> deferred = new List<Action>();
        readonly HashSet<KeyCode> heldKeys = new HashSet<KeyCode>();
        bool textFieldFocused;

        bool pointerOnMap, dragging;
        int pointerButton;
        Vector2 pointerDownPos, lastPointerPos;
        CityData hoveredCity;

        string confirmTitle, confirmText;
        Action confirmAction;
        int speedBeforePause = 1;
        int lastEscapeFrame = -1;

        // Dokunmatik: panel içinde parmakla kaydırma ve iki parmakla yakınlaştırma
        bool touchOnPanel, touchScrolling, multiTouch, suppressTap;
        Vector2 touchStart;
        float pendingScroll;
        readonly List<Rect> sliderRects = new List<Rect>();
        readonly List<Rect> sliderRectsNext = new List<Rect>();

        FinanceSystem boundFinance;
        ProgressionSystem boundProgression;
        FleetSystem boundFleet;
        RouteSystem boundRoutes;
        int warningCount;
        readonly HashSet<string> criticalRoutes = new HashSet<string>();
        readonly HashSet<string> connectedCityIds = new HashSet<string>();
        readonly List<BusInstance> busBuffer = new List<BusInstance>();

        // ------------------------------------------------------------------ kurulum

        public void Init(GameSession gameSession, WorldRenderer worldRenderer, MapCamera camera)
        {
            session = gameSession;
            world = worldRenderer;
            mapCamera = camera;
            goals = new GoalTracker(session);
            goals.Completed += OnGoalCompleted;

            session.SessionStarted += OnSessionStarted;
            session.DayCompleted += OnDayCompleted;
            session.MonthCompleted += OnMonthCompleted;
            session.Bankrupt += OnBankrupt;

            Fmt.Symbol = session.Economy.CurrencySymbol;
            EnterMainMenu();
        }

        void OnDestroy()
        {
            if (session == null) return;
            session.SessionStarted -= OnSessionStarted;
            session.DayCompleted -= OnDayCompleted;
            session.MonthCompleted -= OnMonthCompleted;
            session.Bankrupt -= OnBankrupt;
            UnbindSystems();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused) heldKeys.Clear();
        }

        void OnApplicationQuit()
        {
            if (session != null && session.IsRunning && !session.IsBankrupt) session.SaveGame(AutosaveSlot);
        }

        /// <summary>Mobilde uygulama arka plana alınınca kapatılabilir; çıkış olayına güvenmeden kaydet.</summary>
        void OnApplicationPause(bool paused)
        {
            if (paused && session != null && session.IsRunning && !session.IsBankrupt) session.SaveGame(AutosaveSlot);
        }

        void BindSystems()
        {
            UnbindSystems();
            boundFinance = session.Finance;
            boundProgression = session.Progression;
            boundFleet = session.Fleet;
            boundRoutes = session.Routes;

            boundProgression.LevelChanged += OnLevelChanged;
            boundProgression.ModelUnlocked += OnModelUnlocked;
            boundProgression.RegionUnlocked += OnRegionUnlocked;
            boundFleet.BusBrokeDown += OnBusBrokeDown;
            boundFleet.BusAdded += OnFleetChanged;
            boundFleet.BusRemoved += OnFleetChanged;
            boundFleet.BusChanged += OnFleetChanged;
            boundRoutes.RouteOpened += OnRouteSetChanged;
            boundRoutes.RouteClosed += OnRouteSetChanged;
            boundRoutes.RouteChanged += OnRouteChanged;
        }

        void UnbindSystems()
        {
            if (boundProgression != null)
            {
                boundProgression.LevelChanged -= OnLevelChanged;
                boundProgression.ModelUnlocked -= OnModelUnlocked;
                boundProgression.RegionUnlocked -= OnRegionUnlocked;
            }
            if (boundFleet != null)
            {
                boundFleet.BusBrokeDown -= OnBusBrokeDown;
                boundFleet.BusAdded -= OnFleetChanged;
                boundFleet.BusRemoved -= OnFleetChanged;
                boundFleet.BusChanged -= OnFleetChanged;
            }
            if (boundRoutes != null)
            {
                boundRoutes.RouteOpened -= OnRouteSetChanged;
                boundRoutes.RouteClosed -= OnRouteSetChanged;
                boundRoutes.RouteChanged -= OnRouteChanged;
            }
            boundFinance = null;
            boundProgression = null;
            boundFleet = null;
            boundRoutes = null;
        }

        // ------------------------------------------------------------------ oturum olayları

        void OnSessionStarted()
        {
            BindSystems();
            mode = Mode.Playing;
            modal = Modal.None;
            panel = Panel.None;
            ResetPanelState();
            goals.ResetSessionFlags();
            notes.Clear();
            criticalRoutes.Clear();
            mapCamera.AutoDrift = false;

            CityData hq = session.Headquarters;
            if (hq != null) mapCamera.FocusOn(world.GetCityPosition(hq), 34f);
            RefreshRouteIndicators(announce: false);
        }

        void OnDayCompleted(DayReport report)
        {
            MarkDataDirty();
            goals.Evaluate();
            RefreshRouteIndicators(announce: true);

            if (session.Finance.Cash < 0 && !session.IsBankrupt)
            {
                int left = session.ProgressionConfig.BankruptcyGraceDays - session.Finance.DaysInDebt;
                if (session.Finance.DaysInDebt == 1 || left % 5 == 0)
                    Toast($"Kasa eksiye düştü! {left} gün içinde toparlanmazsanız iflas edersiniz.", UIStyles.Red);
            }
        }

        void OnMonthCompleted(int month)
        {
            double net = session.Finance.GetNetProfit(SimulationClock.DaysPerMonth);
            Toast($"{month}. ay kapandı · Net {Fmt.Signed(net)}", net >= 0 ? UIStyles.Green : UIStyles.Red);
        }

        void OnBankrupt()
        {
            Toast("Şirket iflas etti.", UIStyles.Red);
            modal = Modal.GameOver;
        }

        void OnGoalCompleted(GoalTracker.Goal goal)
        {
            string reward = goal.Reward > 0f ? $" · Ödül {Fmt.Money(goal.Reward)}" : string.Empty;
            Toast($"Görev tamamlandı: {goal.Title}{reward}", UIStyles.Gold);
            if (goal.IsVictory) modal = Modal.Victory;
        }

        void OnLevelChanged(int level) =>
            Toast($"Şirket seviyesi {level}! Hat limiti: {session.Progression.MaxRoutes}", UIStyles.Gold);

        void OnModelUnlocked(BusModelData model) =>
            Toast($"Yeni otobüs açıldı: {model.DisplayName} — Galeri (G)", UIStyles.Blue);

        void OnRegionUnlocked(RegionData region) =>
            Toast($"{region.DisplayName} işletme lisansı alındı. Yeni şehirler açıldı!", UIStyles.Green);

        void OnBusBrokeDown(BusInstance bus) =>
            Toast($"{bus.nickname} arızalandı ({bus.outOfServiceDays} gün servis dışı)", UIStyles.Orange);

        void OnFleetChanged(BusInstance _) => MarkDataDirty();

        void OnRouteSetChanged(RouteState _)
        {
            MarkDataDirty();
            RefreshRouteIndicators(announce: false);
        }

        void OnRouteChanged(RouteState route)
        {
            if (selectedRoute != null && route.routeId == selectedRoute.routeId) routeAnalysisDirty = true;
        }

        void MarkDataDirty()
        {
            routeAnalysisDirty = true;
            historyChartDirty = true;
            financeChartDirty = true;
            suggestionsDirty = true;
            quoteDirty = true;
        }

        /// <summary>Uyarı sayacı, bağlı şehirler ve yeni kritik hat bildirimleri.</summary>
        void RefreshRouteIndicators(bool announce)
        {
            warningCount = 0;
            connectedCityIds.Clear();
            if (!session.IsRunning) return;

            foreach (var route in session.Routes.Routes)
            {
                connectedCityIds.Add(route.cityAId);
                connectedCityIds.Add(route.cityBId);

                RouteInsight insight = RouteAdvisor.GetTopInsight(route, session.Fleet.CountBusesOnRoute(route.routeId));
                if (insight.Severity >= InsightSeverity.Warning) warningCount++;

                if (insight.Severity == InsightSeverity.Critical && route.HasHistory)
                {
                    if (criticalRoutes.Add(route.routeId) && announce)
                        Toast($"{RouteName(route)}: {insight.Message}", UIStyles.Red);
                }
                else
                {
                    criticalRoutes.Remove(route.routeId);
                }
            }
        }

        // ------------------------------------------------------------------ kare döngüsü

        void Update()
        {
            if (session == null) return;
            float dt = Time.unscaledDeltaTime;

            // Android geri tuşu / Esc (IMGUI olayı da gelirse aynı karede ikinci kez işlenmez)
            if (TouchInput.BackPressed() && Time.frameCount != lastEscapeFrame)
            {
                lastEscapeFrame = Time.frameCount;
                OnEscape();
            }

            // İki parmakla yakınlaştırma
            int touches = TouchInput.Read(out Vector2 t0, out Vector2 t1, out Vector2 d0, out Vector2 d1);
            if (touches >= 2)
            {
                multiTouch = suppressTap = true;
                float now = (t0 - t1).magnitude;
                float before = ((t0 - d0) - (t1 - d1)).magnitude;
                if (before > 1f && mode != Mode.MainMenu && modal == Modal.None)
                    mapCamera.Zoom((before - now) / (Screen.height * 0.01f) * 0.15f, (t0 + t1) * 0.5f);
            }
            else if (touches == 0)
            {
                multiTouch = false;
            }

            if (mode != Mode.MainMenu && modal == Modal.None && !textFieldFocused)
            {
                Vector2 axis = Vector2.zero;
                if (Held(KeyCode.W) || Held(KeyCode.UpArrow)) axis.y += 1f;
                if (Held(KeyCode.S) || Held(KeyCode.DownArrow)) axis.y -= 1f;
                if (Held(KeyCode.D) || Held(KeyCode.RightArrow)) axis.x += 1f;
                if (Held(KeyCode.A) || Held(KeyCode.LeftArrow)) axis.x -= 1f;
                if (axis != Vector2.zero) mapCamera.PanByAxis(axis.normalized, dt);

                float zoom = 0f;
                if (Held(KeyCode.E) || Held(KeyCode.Equals) || Held(KeyCode.KeypadPlus)) zoom -= 1f;
                if (Held(KeyCode.Q) || Held(KeyCode.Minus) || Held(KeyCode.KeypadMinus)) zoom += 1f;
                if (zoom != 0f) mapCamera.Zoom(zoom * dt * 10f, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            }

            SyncWorldSelection();
        }

        bool Held(KeyCode key) => heldKeys.Contains(key);

        CityData lastPreviewFrom, lastPreviewTo;

        void SyncWorldSelection()
        {
            bool playing = mode == Mode.Playing;
            bool planning = playing && panel == Panel.Planner;

            world.Hovered = hoveredCity;
            world.Headquarters = playing ? session.Headquarters : null;
            world.Origin = mode == Mode.NewGame ? startCity : planning ? plannerOrigin : null;
            world.Destination = planning ? plannerDestination : null;
            world.SelectedRouteId = playing && panel == Panel.Route && selectedRoute != null ? selectedRoute.routeId : null;

            CityData from = planning ? plannerOrigin : null;
            CityData to = planning ? plannerDestination : null;
            if (from != lastPreviewFrom || to != lastPreviewTo)
            {
                lastPreviewFrom = from;
                lastPreviewTo = to;
                world.SetPreview(from, to);
            }
        }

        void OnGUI()
        {
            if (session == null) return;
            ComputeMetrics();
            GUI.skin.settings.cursorColor = UIStyles.Text;

            uiRects.Clear();
            Event e = Event.current;
            textFieldFocused = GUIUtility.keyboardControl != 0;
            if (e.type == EventType.Repaint) sliderRectsNext.Clear();

            TrackHeldKeys(e);
            if (e.type == EventType.KeyDown && e.keyCode != KeyCode.None && !textFieldFocused) HandleShortcut(e);
            HandleTouchScroll(e);

            if (mode != Mode.MainMenu && e.type == EventType.Repaint) DrawCityLabels();

            bool previousEnabled = GUI.enabled;
            GUI.enabled = modal == Modal.None;
            switch (mode)
            {
                case Mode.MainMenu:
                    DrawMainMenu();
                    break;
                case Mode.NewGame:
                    DrawNewGame();
                    break;
                case Mode.Playing:
                    DrawTopBar();
                    if (compact) DrawBottomBar();
                    else DrawToolbar();
                    DrawPanel();
                    DrawZoomButtons();
                    DrawGoalWidget();
                    DrawToasts();
                    break;
            }
            GUI.enabled = previousEnabled;

            if (modal != Modal.None) DrawModal();

            UpdateHover(e);
            if (e.type == EventType.Repaint)
            {
                DrawTooltip();
                sliderRects.Clear();
                sliderRects.AddRange(sliderRectsNext);
            }
            HandleMapInput(e);
            RunDeferred();
        }

        // ------------------------------------------------------------------ ölçek, yerleşim ve dokunmatik yardımcılar

        void ComputeMetrics()
        {
            mobile = Application.isMobilePlatform;
            float designHeight;
            if (!mobile)
            {
                designHeight = 1000f;
            }
            else if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                // Tarayıcıda DPI güvenilmez; yatay telefon ekranı tabletten belirgin şekilde daha geniştir.
                designHeight = Screen.width >= Screen.height * 1.7f ? 620f : 820f;
            }
            else
            {
                float dpi = Screen.dpi > 1f ? Screen.dpi : 96f;
                designHeight = Screen.height / dpi < 4.2f ? 620f : 820f;
            }

            switch (ProfileOverride)
            {
                case DeviceProfile.Desktop: mobile = false; designHeight = 1000f; break;
                case DeviceProfile.Phone: mobile = true; designHeight = 620f; break;
                case DeviceProfile.Tablet: mobile = true; designHeight = 820f; break;
            }
            float unit = Mathf.Clamp(Mathf.Round(Screen.height / designHeight * 20f) / 20f, 0.5f, 4f);

            if (s == null || !Mathf.Approximately(unit, u))
            {
                u = unit;
                s?.Dispose();
                s = new UIStyles(u);
            }

            compact = Screen.height / u < 700f;
            W = Screen.width;
            H = Screen.height;
            Rect safe = Screen.safeArea;
            SafeLeft = safe.xMin;
            SafeRight = safe.xMax;
            SafeTop = H - safe.yMax;
            SafeBottom = safe.yMin;
        }

        internal static DeviceProfile ParseProfile(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "telefon": case "phone": return DeviceProfile.Phone;
                case "tablet": return DeviceProfile.Tablet;
                case "masaustu": case "masaüstü": case "desktop": return DeviceProfile.Desktop;
                default: return DeviceProfile.Auto;
            }
        }

        float Margin => 12f * u;
        float TopBarHeight => 64f * u;
        float BottomBarHeight => 62f * u;
        float ToolbarWidth => 184f * u;
        float PanelWidth => compact ? Mathf.Min(560f * u, (SafeRight - SafeLeft) * 0.55f) : 490f * u;
        /// <summary>Harita/arayüz için kullanılabilir alanın alt sınırı (alt menü çubuğunun üstü).</summary>
        float ContentBottom => H - SafeBottom - (compact && mode == Mode.Playing ? BottomBarHeight : 0f);

        Rect PanelRect
        {
            get
            {
                float top = SafeTop + TopBarHeight + Margin;
                float bottom = ContentBottom - Margin;
                return new Rect(SafeRight - PanelWidth - Margin, top, PanelWidth, Mathf.Max(100f * u, bottom - top));
            }
        }

        GUILayoutOption Ht(float units) => GUILayout.Height(units * u);
        GUILayoutOption Wd(float units) => GUILayout.Width(units * u);
        void Sp(float units) => GUILayout.Space(units * u);
        /// <summary>Kullanılabilir genişlik dar mı (4:3 tablet, telefon)? Üst çubuk sütunları buna göre daralır.</summary>
        bool Narrow => (SafeRight - SafeLeft) / u < 1300f;
        float N(float wide, float narrow) => Narrow ? narrow : wide;
        /// <summary>Klavye kısayolu ipucu (dokunmatik cihazda gösterilmez).</summary>
        string Key(string label, string key) => mobile ? label : $"{label}  [{key}]";

        /// <summary>Kaydırılabilir alan: yatay kaydırma çubuğu yok; dokunmatik sürükleme miktarı burada uygulanır.</summary>
        Vector2 BeginScroll(Vector2 position)
        {
            if (pendingScroll != 0f)
            {
                position.y = Mathf.Max(0f, position.y + pendingScroll);
                pendingScroll = 0f;
            }
            return GUILayout.BeginScrollView(position, GUIStyle.none, s.VerticalScrollbar, GUILayout.ExpandHeight(true));
        }

        /// <summary>Kalın, dokunmatik dostu kaydırıcı. Konumu kaydedilir ki üzerinde sürükleme panel kaydırması sayılmasın.</summary>
        float Slider(float value, float min, float max)
        {
            float result = GUILayout.HorizontalSlider(value, min, max, s.Slider, s.SliderThumb, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                Rect local = GUILayoutUtility.GetLastRect();
                Vector2 topLeft = GUIUtility.GUIToScreenPoint(new Vector2(local.x, local.y));
                sliderRectsNext.Add(new Rect(topLeft.x, topLeft.y, local.width, local.height));
            }
            return result;
        }

        Rect CurrentScrollArea()
        {
            if (modal != Modal.None) return default;
            switch (mode)
            {
                case Mode.Playing: return panel != Panel.None ? PanelRect : default;
                case Mode.NewGame: return NewGameRect;
                case Mode.MainMenu: return showMenuHelp ? MainMenuRect : default;
                default: return default;
            }
        }

        /// <summary>
        /// IMGUI kaydırma alanları dokunmatik sürüklemeyi desteklemez; panel içinde dikey sürükleme burada
        /// kaydırmaya çevrilir. Sürükleme başlayınca basılı düğme iptal edilir (yanlışlıkla tıklama olmaz).
        /// </summary>
        void HandleTouchScroll(Event e)
        {
            if (!mobile) return;
            switch (e.type)
            {
                case EventType.MouseDown:
                    Rect area = CurrentScrollArea();
                    touchOnPanel = area.width > 0f && area.Contains(e.mousePosition) && !IsOverSlider(e.mousePosition);
                    touchScrolling = false;
                    touchStart = e.mousePosition;
                    break;

                case EventType.MouseDrag:
                    if (!touchOnPanel) return;
                    if (!touchScrolling && Mathf.Abs(e.mousePosition.y - touchStart.y) > 10f * u)
                    {
                        touchScrolling = true;
                        GUIUtility.hotControl = 0;
                    }
                    if (touchScrolling)
                    {
                        pendingScroll -= e.delta.y;
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (touchScrolling) e.Use();
                    touchOnPanel = touchScrolling = false;
                    break;
            }
        }

        bool IsOverSlider(Vector2 point)
        {
            foreach (var rect in sliderRects)
                if (rect.Contains(point)) return true;
            return false;
        }

        void DrawZoomButtons()
        {
            float size = 52f * u;
            float right = panel != Panel.None ? PanelRect.x : SafeRight;
            var zoomIn = new Rect(right - size - Margin, ContentBottom - 2f * size - 2f * Margin - (compact ? 0f : 0f), size, size);
            var zoomOut = new Rect(zoomIn.x, zoomIn.yMax + 6f * u, size, size);
            uiRects.Add(zoomIn);
            uiRects.Add(zoomOut);
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (GUI.Button(zoomIn, "+", s.Button)) mapCamera.Zoom(-4f, center);
            if (GUI.Button(zoomOut, "-", s.Button)) mapCamera.Zoom(4f, center);
        }

        void RunDeferred()
        {
            if (deferred.Count == 0) return;
            var actions = deferred.ToArray();
            deferred.Clear();
            foreach (var action in actions) action();
        }

        void Defer(Action action) => deferred.Add(action);

        void Toast(string text, Color color) => notes.Push(text, color, session.State != null ? session.State.currentDay : 0);

        // ------------------------------------------------------------------ girdi

        void TrackHeldKeys(Event e)
        {
            if (e.type == EventType.KeyDown && e.keyCode != KeyCode.None) heldKeys.Add(e.keyCode);
            else if (e.type == EventType.KeyUp) heldKeys.Remove(e.keyCode);
        }

        void HandleShortcut(Event e)
        {
            KeyCode key = e.keyCode;
            if (key == KeyCode.Escape)
            {
                if (Time.frameCount != lastEscapeFrame)
                {
                    lastEscapeFrame = Time.frameCount;
                    Defer(OnEscape);
                }
                e.Use();
                return;
            }
#if UNITY_EDITOR
            if (key == KeyCode.F12)
            {
                // Mağaza ekran görüntüsü: Game view'ı hedef çözünürlüğe ayarlayın (ör. 2868x1320), F12'ye basın.
                string folder = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Screenshots");
                System.IO.Directory.CreateDirectory(folder);
                string file = System.IO.Path.Combine(folder, $"bus_{Screen.width}x{Screen.height}_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
                ScreenCapture.CaptureScreenshot(file);
                Toast($"Ekran görüntüsü: Screenshots/{System.IO.Path.GetFileName(file)}", UIStyles.Blue);
                e.Use();
                return;
            }
#endif
            if (mode != Mode.Playing || modal != Modal.None) return;

            bool used = true;
            switch (key)
            {
                case KeyCode.Space: session.Clock.TogglePause(); break;
                case KeyCode.Alpha1: case KeyCode.Keypad1: session.Clock.SetSpeed(1); break;
                case KeyCode.Alpha2: case KeyCode.Keypad2: session.Clock.SetSpeed(2); break;
                case KeyCode.Alpha3: case KeyCode.Keypad3: session.Clock.SetSpeed(4); break;
                case KeyCode.N: Defer(() => OpenPlannerNow(null)); break;
                case KeyCode.R: Defer(() => OpenPanelNow(Panel.RouteList)); break;
                case KeyCode.F: Defer(() => OpenPanelNow(Panel.Fleet)); break;
                case KeyCode.G: Defer(() => OpenDealershipNow(null)); break;
                case KeyCode.K: Defer(() => OpenPanelNow(Panel.Finance)); break;
                case KeyCode.B: Defer(() => OpenPanelNow(Panel.Regions)); break;
                case KeyCode.J: Defer(() => OpenPanelNow(Panel.Goals)); break;
                case KeyCode.L: Defer(() => OpenPanelNow(Panel.Log)); break;
                case KeyCode.H: Defer(() => OpenPanelNow(Panel.Help)); break;
                case KeyCode.F5: Defer(() => SaveTo(SaveSlot)); break;
                default: used = false; break;
            }
            if (used) e.Use();
        }

        void OnEscape()
        {
            switch (modal)
            {
                case Modal.Confirm:
                    modal = Modal.None;
                    return;
                case Modal.Pause:
                    ClosePauseMenu();
                    return;
                case Modal.Victory:
                    modal = Modal.None;
                    return;
                case Modal.GameOver:
                    return;
            }

            if (mode == Mode.NewGame) EnterMainMenu();
            else if (mode == Mode.Playing)
            {
                if (panel != Panel.None) ClosePanel();
                else OpenPauseMenu();
            }
        }

        bool IsOverUI(Vector2 guiPoint)
        {
            foreach (var rect in uiRects)
                if (rect.Contains(guiPoint)) return true;
            return false;
        }

        Vector2 GuiToScreen(Vector2 gui) => new Vector2(gui.x, Screen.height - gui.y);
        float PickRadius => (mobile ? 28f : 18f) * u;

        void UpdateHover(Event e)
        {
            if (e.type != EventType.Repaint) return;
            hoveredCity = null;
            if (mode == Mode.MainMenu || modal != Modal.None || dragging || mobile) return; // dokunmatikte hover yok
            if (IsOverUI(e.mousePosition)) return;
            world.TryPickCity(mapCamera.Camera, GuiToScreen(e.mousePosition), PickRadius, out hoveredCity);
        }

        void HandleMapInput(Event e)
        {
            if (mode == Mode.MainMenu || modal != Modal.None) return;
            Vector2 mouse = e.mousePosition;

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (IsOverUI(mouse)) return;
                    pointerOnMap = true;
                    dragging = false;
                    if (!multiTouch) suppressTap = false; // önceki pinch hareketinin kilidi yeni dokunuşu yutmasın
                    pointerButton = e.button;
                    pointerDownPos = lastPointerPos = mouse;
                    if (textFieldFocused) GUIUtility.keyboardControl = 0;
                    e.Use();
                    break;

                case EventType.MouseDrag:
                    if (!pointerOnMap) return;
                    if (multiTouch) { e.Use(); return; } // iki parmak: yakınlaştırma Update'te
                    if (!dragging && (mouse - pointerDownPos).sqrMagnitude > 36f * u * u) dragging = true;
                    if (dragging) mapCamera.DragPan(GuiToScreen(lastPointerPos), GuiToScreen(mouse));
                    lastPointerPos = mouse;
                    e.Use();
                    break;

                case EventType.MouseUp:
                    if (!pointerOnMap) return;
                    pointerOnMap = false;
                    if (!dragging && !suppressTap)
                    {
                        if (pointerButton == 0) OnMapClick(mouse);
                        else if (pointerButton == 1) OnMapRightClick();
                    }
                    dragging = false;
                    if (!multiTouch) suppressTap = false;
                    e.Use();
                    break;

                case EventType.ScrollWheel:
                    if (IsOverUI(mouse)) return;
                    mapCamera.Zoom(Mathf.Clamp(e.delta.y, -6f, 6f) * 0.5f, GuiToScreen(mouse));
                    e.Use();
                    break;
            }
        }

        void OnMapClick(Vector2 guiPoint)
        {
            Vector2 screen = GuiToScreen(guiPoint);
            if (world.TryPickCity(mapCamera.Camera, screen, PickRadius, out CityData city))
            {
                OnCityClicked(city);
                return;
            }

            if (mode == Mode.Playing && panel != Panel.Planner &&
                world.TryPickRoute(mapCamera.Camera, screen, PickRadius * 0.6f, out RouteState route))
            {
                OpenRoutePanelNow(route, RouteTab.Price);
            }
        }

        void OnMapRightClick()
        {
            if (mode != Mode.Playing) return;
            if (panel == Panel.Planner && plannerOrigin != null)
            {
                plannerOrigin = plannerDestination = null;
                plannerQuoteValid = false;
            }
            else if (panel != Panel.None)
            {
                ClosePanel();
            }
        }

        void OnCityClicked(CityData city)
        {
            if (mode == Mode.NewGame)
            {
                SelectStartCity(city);
                return;
            }
            if (mode != Mode.Playing) return;

            if (panel == Panel.Planner) PlannerCityClicked(city);
            else OpenPlannerNow(city);
        }

        // ------------------------------------------------------------------ panel yönetimi

        void OpenPanelNow(Panel target)
        {
            if (mode != Mode.Playing) return;
            panel = panel == target ? Panel.None : target;
            if (panel == Panel.Finance) financeChartDirty = true;
        }

        void ClosePanel()
        {
            panel = Panel.None;
            plannerOrigin = plannerDestination = null;
            plannerQuoteValid = false;
            dealershipTargetRoute = null;
        }

        void ResetPanelState()
        {
            ClosePanel();
            selectedRoute = null;
            expandedBusId = null;
            MarkDataDirty();
        }

        // ------------------------------------------------------------------ harita etiketleri ve ipucu

        readonly List<Rect> placedLabels = new List<Rect>();
        readonly List<WorldRenderer.CityMarker> labelOrder = new List<WorldRenderer.CityMarker>();
        readonly Dictionary<string, float> labelWidths = new Dictionary<string, float>();
        UIStyles labelWidthStyles;
        static readonly GUIContent LabelContent = new GUIContent(string.Empty);

        /// <summary>
        /// Şehir adları: önce önemli şehirler (seçili, merkez, hat bağlantılı), sonra büyükten küçüğe. Başka bir
        /// adla çakışan ikincil ad çizilmez (ör. birbirine çok yakın Kocaeli ve Sakarya).
        /// </summary>
        void DrawCityLabels()
        {
            Camera cam = mapCamera.Camera;
            float distance = mapCamera.Distance;
            int tierLimit = distance < 22f ? 3 : distance < 38f ? 2 : distance < 62f ? 1 : 0;
            bool playing = mode == Mode.Playing;

            if (labelOrder.Count != world.Markers.Count)
            {
                labelOrder.Clear();
                labelOrder.AddRange(world.Markers);
                labelOrder.Sort((a, b) => a.City.MapTier.CompareTo(b.City.MapTier));
            }
            if (labelWidthStyles != s)
            {
                labelWidthStyles = s;
                labelWidths.Clear();
            }
            placedLabels.Clear();

            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var marker in labelOrder)
                {
                    CityData city = marker.City;
                    bool important = city == hoveredCity || city == world.Origin || city == world.Destination ||
                                     city == world.Headquarters || (playing && connectedCityIds.Contains(city.Id));
                    if (important != (pass == 0)) continue;
                    if (city.MapTier > tierLimit && !important) continue;

                    Vector3 sp = cam.WorldToScreenPoint(marker.Position + Vector3.up * 0.75f);
                    if (sp.z <= 0f) continue;
                    var p = new Vector2(sp.x, Screen.height - sp.y);
                    if (p.x < -100f * u || p.x > W + 100f * u || p.y < -40f * u || p.y > H + 40f * u) continue;

                    bool bold = city.MapTier == 0 || important;
                    GUIStyle style = bold ? s.MapLabelBold : s.MapLabel;
                    float width = LabelWidth(city, style, bold);
                    var box = new Rect(p.x - width * 0.5f - 3f * u, p.y - 22f * u, width + 6f * u, 18f * u);
                    if (!important && OverlapsPlacedLabel(box)) continue;
                    placedLabels.Add(box);

                    var rect = new Rect(p.x - 90f * u, p.y - 24f * u, 180f * u, 22f * u);
                    bool locked = playing && !session.Progression.IsRegionUnlocked(city.Region);
                    GUI.Label(new Rect(rect.x + u, rect.y + u, rect.width, rect.height), city.DisplayName, bold ? s.MapLabelBoldShadow : s.MapLabelShadow);
                    Color previous = GUI.contentColor;
                    if (locked) GUI.contentColor = new Color(0.75f, 0.75f, 0.75f, 0.8f);
                    GUI.Label(rect, city.DisplayName, style);
                    GUI.contentColor = previous;
                }
            }
        }

        float LabelWidth(CityData city, GUIStyle style, bool bold)
        {
            string key = bold ? "b:" + city.Id : city.Id;
            if (labelWidths.TryGetValue(key, out float width)) return width;
            LabelContent.text = city.DisplayName;
            width = style.CalcSize(LabelContent).x;
            labelWidths[key] = width;
            return width;
        }

        bool OverlapsPlacedLabel(Rect box)
        {
            foreach (var placed in placedLabels)
                if (placed.Overlaps(box)) return true;
            return false;
        }

        void DrawTooltip()
        {
            if (hoveredCity == null || mode == Mode.MainMenu) return;
            CityData city = hoveredCity;

            var lines = new List<(string text, GUIStyle style, Color color)>
            {
                (city.DisplayName, s.Bold, UIStyles.Text),
                ($"{city.Region.DisplayName} · nüfus {Fmt.Population(city.Population)}", s.Small, UIStyles.DimText)
            };

            if (mode == Mode.Playing)
            {
                int routes = 0;
                foreach (var route in session.Routes.Routes)
                    if (route.cityAId == city.Id || route.cityBId == city.Id) routes++;
                if (routes > 0) lines.Add(($"{routes} hattınız var", s.Small, UIStyles.Green));

                if (!session.Progression.IsRegionUnlocked(city.Region))
                    lines.Add(("Kilitli bölge — Bölgeler (B) panelinden lisans alın", s.Small, UIStyles.Orange));
                else if (panel == Panel.Planner && plannerOrigin != null && plannerOrigin != city)
                    lines.Add(($"Tıkla: varış olarak seç · {Fmt.Km(session.World.GetRoadDistanceKm(plannerOrigin, city))}", s.Small, UIStyles.Blue));
                else
                    lines.Add(("Tıkla: bu şehirden hat planla", s.Small, UIStyles.Blue));
            }
            else if (mode == Mode.NewGame)
            {
                lines.Add(("Tıkla: merkez ofis olarak seç", s.Small, UIStyles.Blue));
            }

            Vector2 mouse = Event.current.mousePosition;
            float line = 21f * u;
            float height = 14f * u + lines.Count * line;
            var rect = new Rect(mouse.x + 18f * u, mouse.y + 14f * u, 330f * u, height);
            if (rect.xMax > W) rect.x = mouse.x - rect.width - 12f * u;
            if (rect.yMax > H) rect.y = mouse.y - rect.height - 12f * u;

            GUI.Box(rect, GUIContent.none, s.Tooltip);
            for (int i = 0; i < lines.Count; i++)
            {
                Color previous = GUI.contentColor;
                GUI.contentColor = lines[i].color;
                GUI.Label(new Rect(rect.x + 10f * u, rect.y + 7f * u + i * line, rect.width - 20f * u, line), lines[i].text, lines[i].style);
                GUI.contentColor = previous;
            }
        }

        // ------------------------------------------------------------------ çizim yardımcıları

        static void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        static void DrawBar(Rect rect, float value01, Color color)
        {
            DrawRect(rect, new Color(1f, 1f, 1f, 0.1f));
            DrawRect(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value01), rect.height), color);
        }

        /// <summary>Düzen içinde yatay ilerleme çubuğu.</summary>
        void LayoutBar(float value01, Color color, float height = 8f, float width = -1f, float topPadding = 0f)
        {
            if (topPadding > 0f) GUILayout.BeginVertical();
            if (topPadding > 0f) Sp(topPadding);
            Rect rect = width > 0f
                ? GUILayoutUtility.GetRect(width * u, height * u, Wd(width), Ht(height))
                : GUILayoutUtility.GetRect(10f, height * u, GUILayout.ExpandWidth(true), Ht(height));
            if (Event.current.type == EventType.Repaint) DrawBar(rect, value01, color);
            if (topPadding > 0f) GUILayout.EndVertical();
        }

        void Divider()
        {
            Sp(4f);
            Rect rect = GUILayoutUtility.GetRect(10f, 1f, GUILayout.ExpandWidth(true), GUILayout.Height(Mathf.Max(1f, u)));
            if (Event.current.type == EventType.Repaint) DrawRect(rect, new Color(1f, 1f, 1f, 0.1f));
            Sp(4f);
        }

        static void ColorLabel(string text, Color color, GUIStyle style, params GUILayoutOption[] options)
        {
            Color previous = GUI.contentColor;
            GUI.contentColor = color;
            GUILayout.Label(text, style, options);
            GUI.contentColor = previous;
        }

        /// <summary>Anahtar–değer satırı: solda soluk etiket, sağda kalın değer.</summary>
        void KeyValue(string key, string value, Color? color = null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(key, s.Dim);
            GUILayout.FlexibleSpace();
            ColorLabel(value, color ?? UIStyles.Text, s.Bold);
            GUILayout.EndHorizontal();
        }

        bool PanelHeader(string title, string subtitle = null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(title, s.H1);
            GUILayout.FlexibleSpace();
            bool close = GUILayout.Button("×", s.Ghost, Wd(34f), Ht(30f));
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(subtitle)) GUILayout.Label(subtitle, s.Dim);
            Divider();
            if (close) Defer(ClosePanel);
            return close;
        }

        /// <summary>GUI.enabled'ı geçici olarak koşula bağlar (üst seviyedeki kilit korunur).</summary>
        bool EnabledButton(bool enabled, string text, GUIStyle style, params GUILayoutOption[] options)
        {
            bool previous = GUI.enabled;
            GUI.enabled = previous && enabled;
            bool clicked = GUILayout.Button(text, style, options);
            GUI.enabled = previous;
            return clicked && enabled;
        }

        string RouteName(RouteState route)
        {
            CityData a = session.World.GetCity(route.cityAId);
            CityData b = session.World.GetCity(route.cityBId);
            return $"{(a != null ? a.DisplayName : route.cityAId)} – {(b != null ? b.DisplayName : route.cityBId)}";
        }

        static Color InsightColor(InsightSeverity severity) => severity switch
        {
            InsightSeverity.Critical => UIStyles.Red,
            InsightSeverity.Warning => UIStyles.Orange,
            _ => UIStyles.Green
        };

        static string SegmentName(BusSegment segment) => segment switch
        {
            BusSegment.Minibus => "Minibüs",
            BusSegment.Midibus => "Midibüs",
            BusSegment.StandardCoach => "Şehirlerarası",
            BusSegment.ComfortCoach => "Konfor",
            BusSegment.LuxuryCoach => "Lüks VIP 2+1",
            BusSegment.DoubleDecker => "Çift Katlı",
            _ => segment.ToString()
        };
    }
}
