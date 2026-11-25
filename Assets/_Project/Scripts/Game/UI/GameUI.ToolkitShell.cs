using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// Sayfa düzeni: üç görünüm vardır.
    /// <list type="bullet">
    /// <item><b>Ana ekran</b> (açılışta): kasa, en kârlı hatlar, bugünün görevi, kısayollar.</item>
    /// <item><b>Sekme sayfaları</b> (Hatlarım, Filo, Galeri, Finans, Görevler ve Şirketim/Günlük/Yardım): içerik
    /// alanını kaplar.</item>
    /// <item><b>Harita</b>: yuvarlak köşeli harita kartı + sağda panel (hat listesi; hat paneli ve planlayıcı buraya
    /// açılır).</item>
    /// </list>
    /// Harita kamerası yalnız harita kartına çizer (<see cref="Camera.rect"/>); sayfa açıkken dünya hiç çizilmez
    /// (ekran kartına yük binmez). Kartın dışını arka plan kamerası koyu zeminle temizler.
    /// </summary>
    public sealed partial class GameUI
    {
        /// <summary>Ana ekran açık mı (bir sayfa ya da harita paneli açılınca kapanır).</summary>
        bool tkHome = true;
        Camera backdropCamera;
        int savedCullingMask = -1;
        bool worldHidden;
        Rect appliedViewport = new Rect(0f, 0f, 1f, 1f);

        static readonly Color ScreenBackground = new Color(0.055f, 0.102f, 0.129f); // #0E1A21
        /// <summary>İçerik en çok bu kadar geniş (tasarım birimi); geniş ekranda ortalanır.</summary>
        const float MaxContentUnits = 1400f;
        const float SideColumnUnits = 290f;

        /// <summary>Tam sayfa açılan paneller (harita gizlenir). Hat paneli ve planlayıcı haritanın yanında açılır.</summary>
        static bool IsPagePanel(Panel target) =>
            target == Panel.RouteList || target == Panel.Fleet || target == Panel.Dealership || target == Panel.Finance ||
            target == Panel.Goals || target == Panel.Regions || target == Panel.Log || target == Panel.Help;

        bool HomeShown => NewUi && mode == Mode.Playing && tkHome && panel == Panel.None && !StationUi;
        bool PageShown => NewUi && mode == Mode.Playing && IsPagePanel(panel) && !StationUi;
        /// <summary>Harita kartı görünüyor mu (oyunda ana ekran ya da sayfa açık değilken).</summary>
        bool MapShown => !NewUi || mode != Mode.Playing || (!HomeShown && !PageShown && !StationUi);

        /// <summary>Dar ekran (dikey tablet, küçük pencere): sekme yazıları ikonun altına iner, bazı yazılar kısalır.</summary>
        bool NarrowShell => (SafeRight - SafeLeft) / u < 1000f;

        Tab ActiveTab
        {
            get
            {
                if (HomeShown) return Tab.Home;
                switch (panel)
                {
                    case Panel.RouteList: return Tab.Routes;
                    case Panel.Fleet: return Tab.Fleet;
                    case Panel.Dealership: return Tab.Shop;
                    case Panel.Finance: return Tab.Finance;
                    case Panel.Goals: return Tab.Goals;
                    case Panel.Regions: case Panel.Log: case Panel.Help: return Tab.Home; // ana ekranın alt sayfaları
                    default: return Tab.Map;
                }
            }
        }

        void SelectTab(Tab tab)
        {
            if (mode != Mode.Playing || modal != Modal.None) return;
            if (StationBusy) Stations.ExitImmediate(); // sekmeler haritaya döner
            switch (tab)
            {
                case Tab.Home:
                    ClosePanel();
                    tkHome = true;
                    break;
                case Tab.Map:
                    if (IsPagePanel(panel)) ClosePanel();
                    tkHome = false;
                    break;
                case Tab.Routes: OpenPage(Panel.RouteList); break;
                case Tab.Fleet: OpenPage(Panel.Fleet); break;
                case Tab.Shop: OpenPage(Panel.Dealership); break;
                case Tab.Finance: OpenPage(Panel.Finance); break;
                case Tab.Goals: OpenPage(Panel.Goals); break;
            }
        }

        /// <summary>Sayfayı aç (açıksa açık kalır; sekmeye ikinci kez dokunmak kapatmaz).</summary>
        void OpenPage(Panel target)
        {
            if (mode != Mode.Playing) return;
            tkHome = false;
            if (panel == target) return;
            if (target == Panel.Dealership) OpenDealershipNow(null);
            else OpenPanelNow(target);
        }

        /// <summary>Üst çubuğun solundaki başlık: sayfada panelin adı ve özeti, haritada harita bilgisi.</summary>
        void ScreenHeading(out string kicker, out string title)
        {
            if (StationUi)
            {
                kicker = "DURAK";
                title = Stations.City != null ? Stations.City.DisplayName : "Durak";
                return;
            }
            if (PageShown && tkSheetTitle != null)
            {
                title = tkSheetTitle.text;
                kicker = (tkSheetSubtitle.text ?? string.Empty).ToUpperInvariant();
                return;
            }
            title = panel == Panel.Planner ? "Yeni hat" : panel == Panel.Route && selectedRoute != null ? RouteName(selectedRoute) : "Harita";
            kicker = $"{session.World.Cities.Count} ŞEHİR · {session.Routes.Routes.Count} HAT";
        }

        // ------------------------------------------------------------------ yerleşim (piksel)

        /// <summary>Üst çubuk ile alt sekmeler arasındaki içerik alanı; geniş ekranda ortada en çok 1400 birim.</summary>
        Rect ContentRect
        {
            get
            {
                float margin = (phone ? 14f : 26f) * u;
                float half = MaxContentUnits * u * 0.5f;
                float left = Mathf.Max(SafeLeft + margin, W * 0.5f - half);
                float right = Mathf.Min(SafeRight - margin, W * 0.5f + half);
                float top = SafeTop + TopBarHeight + 10f * u;
                float bottom = (mode == Mode.Playing && BottomBarVisible ? H - SafeBottom - BottomBarHeight : H - SafeBottom) - 10f * u;
                return Rect.MinMaxRect(left, top, Mathf.Max(left + 100f * u, right), Mathf.Max(top + 100f * u, bottom));
            }
        }

        float SideColumnWidth => Mathf.Min(SideColumnUnits * u, ContentRect.width * 0.42f);

        /// <summary>Harita sekmesinde sağdaki panelin yeri (hat listesi, hat paneli, planlayıcı).</summary>
        Rect SideRect
        {
            get
            {
                Rect content = ContentRect;
                float width = SideColumnWidth;
                return new Rect(content.xMax - width, content.y, width, content.height);
            }
        }

        /// <summary>Harita kartı (piksel, IMGUI koordinatı: sol üst başlangıç).</summary>
        Rect MapCardRect
        {
            get
            {
                Rect content = ContentRect;
                float width = content.width - SideColumnWidth - 14f * u;
                return new Rect(content.x, content.y, Mathf.Max(100f * u, width), content.height);
            }
        }

        /// <summary>Haritaya dokunma/kaydırma bu noktada geçerli mi (harita kartının içinde ve kart görünür).</summary>
        bool MapAcceptsPointer(Vector2 guiPoint)
        {
            if (!NewUi || mode != Mode.Playing) return true;
            return MapShown && MapCardRect.Contains(guiPoint);
        }

        // ------------------------------------------------------------------ kamera

        /// <summary>
        /// Oyunda harita kamerası yalnız harita kartına çizer; ana ekran ya da sayfa açıkken hiçbir şey çizmez (yalnız
        /// temizler). Menüde ve yeni oyunda harita bütün ekranı kaplar. Kartın dışını arka plan kamerası temizler.
        /// </summary>
        void UpdateMapViewport(bool playing)
        {
            Camera cam = mapCamera != null ? mapCamera.Camera : null;
            if (cam == null) return;
            EnsureBackdropCamera(cam);

            bool hide = playing && !MapShown && !StationUi;
            if (hide != worldHidden)
            {
                worldHidden = hide;
                if (hide)
                {
                    savedCullingMask = cam.cullingMask;
                    cam.cullingMask = 0;
                }
                else if (savedCullingMask != -1)
                {
                    cam.cullingMask = savedCullingMask;
                }
            }

            Rect viewport = new Rect(0f, 0f, 1f, 1f);
            if (playing && !StationUi && !hide && W > 0f && H > 0f)
            {
                Rect card = MapCardRect;
                viewport = new Rect(card.x / W, (H - card.yMax) / H, card.width / W, card.height / H);
            }
            if (!viewport.Equals(appliedViewport))
            {
                appliedViewport = viewport;
                cam.rect = viewport;
            }
        }

        void EnsureBackdropCamera(Camera main)
        {
            if (backdropCamera != null) return;
            var go = new GameObject("BackdropCamera");
            go.transform.SetParent(transform, false);
            backdropCamera = go.AddComponent<Camera>();
            backdropCamera.depth = main.depth - 10f;
            backdropCamera.cullingMask = 0;
            backdropCamera.clearFlags = CameraClearFlags.SolidColor;
            backdropCamera.backgroundColor = ScreenBackground;
            backdropCamera.allowHDR = false;
            backdropCamera.allowMSAA = false;
        }

        /// <summary>Harita kartının ortası (ekran pikseli, sol alt başlangıç): yakınlaştırma düğmeleri buraya yakınlaştırır.</summary>
        Vector2 MapCardCenterScreen
        {
            get
            {
                if (!NewUi || mode != Mode.Playing) return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                Rect card = MapCardRect;
                return new Vector2(card.center.x, H - card.center.y);
            }
        }
    }
}
