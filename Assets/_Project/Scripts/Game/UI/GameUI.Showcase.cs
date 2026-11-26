using System.Collections.Generic;
using BusTycoon.Data;
using BusTycoon.Routes;
using BusTycoon.Urban;
using UnityEngine;

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

        /// <summary>Yakın plan: ünlü mekânların 3B maketleri ve hatlarında giden araçlar.</summary>
        internal void ShowcaseCityCloseUp(Vector2 meters)
        {
            ShowcaseCityMap();
            mapCamera.FocusOn(cityView.ToWorld(meters), CityView.MaquetteNear * 0.75f);
        }

        internal void ShowcaseHome()
        {
            ClosePanel();
            SelectTab(Tab.Home);
        }

        internal void ShowcaseGallery()
        {
            ClosePanel();
            SelectTab(Tab.Shop);
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
        }
    }
}
