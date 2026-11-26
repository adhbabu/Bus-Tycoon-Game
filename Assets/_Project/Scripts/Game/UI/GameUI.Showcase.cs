using System.Collections.Generic;
using BusTycoon.Data;
using BusTycoon.Routes;
using BusTycoon.Urban;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// Mağaza ekran görüntüsü turu (<see cref="ShowcaseDirector"/>) için arayüzü belirli ekranlara getirir.
    /// Kare aralarında (Update/coroutine) çağrılır; OnGUI geçişinin ortasında düzen değişmez.
    /// </summary>
    public sealed partial class GameUI
    {
        internal void ShowcaseMap(CityData focus, float distance)
        {
            if (Stations != null) Stations.ExitImmediate();
            ClosePanel();
            mapCamera.FocusOn(world.GetCityPosition(focus), distance);
        }

        internal void ShowcasePlanner(CityData origin, CityData destination)
        {
            ClosePanel();
            OpenPlannerNow(origin);
            PlannerCityClicked(destination);
        }

        internal void ShowcaseRoute(RouteState route)
        {
            ClosePanel();
            if (NewUi) SelectTab(Tab.Map);
            OpenRoutePanelNow(route, RouteTab.Price);
        }

        // ------------------------------------------------------------------ şehir içi vitrin (yeni arayüz)

        internal bool ShowcaseNewUi => NewUi;

        /// <summary>Merkez şehrin sokak haritası yüklendi ve kamera şehirde.</summary>
        internal bool ShowcaseCityReady => cityView != null && cityView.Ready && cityView.City == CurrentCity && cameraInCity;

        internal UrbanMap ShowcaseCityMapData => cityView != null ? cityView.Map : null;

        internal void ShowcaseCityMap()
        {
            ClosePanel();
            SelectTab(Tab.Map);
            SelectCityMap(true);
        }

        /// <summary>Verilen alanı (metre, şehir haritası) kartın içine sığdıran kamera uzaklığı.</summary>
        float ShowcaseFitDistance(float spanMeters)
        {
            Rect card = MapCardRect;
            float fit = cityView.FitDistance(card.width / Mathf.Max(1f, card.height));
            float cityWidth = Mathf.Max(1f, Mathf.Max(cityView.Map.Bounds.width, cityView.Map.Bounds.height));
            return Mathf.Max(CityView.MinCameraDistance * 3f, fit * Mathf.Min(1f, spanMeters / cityWidth));
        }

        /// <summary>Şehir haritası: bütün hatlar kartın içinde (araçlar hatlarında dolaşır).</summary>
        internal void ShowcaseCityOverview(List<RouteState> routes)
        {
            ShowcaseCityMap();
            if (routes == null || routes.Count == 0 || cityView.Map == null) return;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var route in routes)
                foreach (var p in new[] { new Vector2(route.urbanAX, route.urbanAY), new Vector2(route.urbanBX, route.urbanBY) })
                {
                    min = new Vector2(Mathf.Min(min.x, p.x), Mathf.Min(min.y, p.y));
                    max = new Vector2(Mathf.Max(max.x, p.x), Mathf.Max(max.y, p.y));
                }
            Vector2 span = max - min;
            mapCamera.FocusOn(cityView.ToWorld((min + max) * 0.5f), ShowcaseFitDistance(Mathf.Max(span.x, span.y) * 1.35f));
        }

        /// <summary>Dolmuş hattı planlayıcısı: iki durak seçili, teklif kartı açık.</summary>
        internal void ShowcaseUrbanPlanner(UrbanStop a, UrbanStop b)
        {
            ShowcaseCityMap();
            OpenUrbanPlanner();
            UrbanStopClicked(a);
            UrbanStopClicked(b);
            float span = (a.Position - b.Position).magnitude;
            mapCamera.FocusOn(cityView.ToWorld((a.Position + b.Position) * 0.5f), ShowcaseFitDistance(span * 1.8f));
        }

        /// <summary>Yakın plan: ünlü mekânların 3B maketleri ve hatlarında giden araçlar (maketler tam görünür uzaklıkta).</summary>
        internal void ShowcaseCityCloseUp(Vector2 meters, float spanMeters)
        {
            ShowcaseCityMap();
            float distance = Mathf.Clamp(ShowcaseFitDistance(spanMeters * 1.5f + 600f), CityView.MinCameraDistance * 3f, CityView.MaquetteNear * 0.9f);
            mapCamera.FocusOn(cityView.ToWorld(meters), distance);
        }

        internal void ShowcaseHome()
        {
            ClosePanel();
            SelectTab(Tab.Home);
        }

        /// <summary>Galeri; <paramref name="modelId"/> verilirse o modelin kartı sayfanın başına kaydırılır.</summary>
        internal void ShowcaseGallery(string modelId = null)
        {
            ClosePanel();
            SelectTab(Tab.Shop);
            showcaseScrollTarget = modelId != null ? "model-" + modelId : null;
        }

        string showcaseScrollTarget;

        static VisualElement FindNamed(VisualElement root, string name)
        {
            if (root.name == name) return root;
            foreach (VisualElement child in root.Children())
            {
                VisualElement found = FindNamed(child, name);
                if (found != null) return found;
            }
            return null;
        }

        internal void ShowcaseDealership(CityData focus, float distance)
        {
            ShowcaseMap(focus, distance);
            OpenPanelNow(Panel.Dealership);
        }

        internal void ShowcaseFinance(CityData focus, float distance)
        {
            ShowcaseMap(focus, distance);
            OpenPanelNow(Panel.Finance);
        }

        /// <summary>Durağın içi (geliştirmeleri yapılmış merkez durak).</summary>
        internal void ShowcaseStation(CityData city)
        {
            if (Stations != null) Stations.ExitImmediate();
            ClosePanel();
            EnterStationNow(city);
        }

        /// <summary>Görüntü alınmadan hemen önce: bildirimler ve olası açılır pencereler kapanır.</summary>
        internal void ShowcaseTidy()
        {
            notes.Clear();
            modal = Modal.None;
            // Kaydırma: dolmuş planlayıcısında teklif kartı (yolcu, bilet, kâr), galeride seçilen model başa gelsin
            if (tkSheetScroll == null) return;
            VisualElement target = UrbanPlannerOpen ? tkUrbanOffer
                : showcaseScrollTarget != null ? FindNamed(tkSheetScroll.contentContainer, showcaseScrollTarget) : null;
            if (target == null) return;
            bool inside = false;
            for (var e = target.parent; e != null; e = e.parent) if (e == tkSheetScroll.contentContainer) inside = true;
            if (inside)
                tkSheetScroll.scrollOffset = new Vector2(0f, Mathf.Max(0f, target.worldBound.y - tkSheetScroll.contentContainer.worldBound.y - 4f));
        }
    }
}
