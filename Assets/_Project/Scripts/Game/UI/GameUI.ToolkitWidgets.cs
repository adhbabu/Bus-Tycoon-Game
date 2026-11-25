using System.Collections.Generic;
using BusTycoon.Data;
using BusTycoon.Routes;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// Harita kartının parçaları: yuvarlak köşe çerçevesi, merkez şehir ve yakınlaştırma düğmeleri, sağdaki hat listesi
    /// (hat paneli ya da planlayıcı açık değilken) ve şehir ipucu (fare şehrin üstündeyken).
    /// </summary>
    public sealed partial class GameUI
    {
        VisualElement tkMapFrame, tkMapEdge, tkMapControls, tkMapSide, tkMapSideList, tkTooltip, tkMapViewSwitch;
        Button tkMapViewCity, tkMapViewCountry;
        Label tkMapLocText, tkMapSideCaption, tkMapSideCta, tkMapViewCityText, tkMapAttribution;
        int tkTooltipSignature = int.MinValue, tkMapSideSignature = int.MinValue;
        readonly List<RouteState> tkMapSideRoutes = new List<RouteState>();

        void BuildToolkitMapWidgets()
        {
            // Yuvarlak köşeler: kartın çevresinde zemin renginde kalın kenarlı çerçeve (iç köşesi yuvarlak), üstünde ince kenar
            tkMapFrame = Ui.El("bt-mapframe", tkHud);
            tkMapFrame.pickingMode = PickingMode.Ignore;
            tkMapEdge = Ui.El("bt-mapedge", tkHud);
            tkMapEdge.pickingMode = PickingMode.Ignore;

            // Kartın üstündeki düğmeler: solda merkez şehir, sağda yakınlaştırma
            tkMapControls = Ui.El("bt-mapcontrols", tkHud);
            tkMapControls.pickingMode = PickingMode.Ignore;
            Button loc = Ui.Button("bt-row bt-maploc", string.Empty, FocusHeadquarters, tkMapControls);
            Ui.Symbol("location_on", "bt-maploc__icon", loc);
            tkMapLocText = Ui.Text("bt-maploc__text", "", UiWeight.Bold, loc);
            Ui.El("bt-grow", tkMapControls).pickingMode = PickingMode.Ignore;
            VisualElement zoom = Ui.El("bt-mapzoom", tkMapControls);
            zoom.pickingMode = PickingMode.Ignore;
            Button zoomIn = Ui.Button("bt-mapzoom__button", string.Empty, () => mapCamera.Zoom(-4f, MapCardCenterScreen), zoom);
            Ui.Symbol("add", "bt-mapzoom__icon", zoomIn);
            Button zoomOut = Ui.Button("bt-mapzoom__button", string.Empty, () => mapCamera.Zoom(4f, MapCardCenterScreen), zoom);
            Ui.Symbol("remove", "bt-mapzoom__icon", zoomOut);
            Button home = Ui.Button("bt-mapzoom__button", string.Empty, FocusHeadquarters, zoom);
            Ui.Symbol("my_location", "bt-mapzoom__icon", home);
            // Şehir haritasının kaynağı (ODbL gereği haritanın üstünde görünür)
            tkMapAttribution = Ui.Text("bt-map-attribution", "© OpenStreetMap katkıcıları · OpenMapTiles", UiWeight.Regular, tkHud);

            // Sağ panel (hat ya da planlayıcı açık değilken): şehir/Türkiye geçişi, hatlarım, işaretler, yeni hat
            tkMapSide = Ui.El("bt-card bt-mapside", tkHud);
            tkMapViewSwitch = Ui.El("bt-row bt-segment bt-mapside__switch", tkMapSide);
            tkMapViewCity = Ui.Button("bt-row bt-segment__item bt-grow", string.Empty, () => SelectCityMap(true), tkMapViewSwitch);
            tkMapViewCity.name = "map-city";
            Ui.Symbol("location_city", "bt-segment__icon", tkMapViewCity);
            tkMapViewCityText = Ui.Text("bt-segment__label", "Şehir", UiWeight.Bold, tkMapViewCity);
            Button country = Ui.Button("bt-row bt-segment__item bt-grow", string.Empty, () => SelectCityMap(false), tkMapViewSwitch);
            country.name = "map-country";
            tkMapViewCountry = country;
            Ui.Symbol("public", "bt-segment__icon", country);
            Ui.Text("bt-segment__label", "Türkiye", UiWeight.Bold, country);
            tkMapSideCaption = Ui.Text("bt-caps is-green", "", UiWeight.Bold, tkMapSide);
            tkMapSideList = Ui.El("bt-col bt-mapside__list", tkMapSide);
            Ui.Text("bt-caps is-green bt-mapside__caption", "İŞARETLER", UiWeight.Bold, tkMapSide);
            VisualElement hq = Ui.El("bt-row bt-mapside__marker", tkMapSide);
            Ui.Symbol("star", "bt-mapside__dot is-gold-bg", hq);
            Ui.Text("bt-mapside__marker-text", "Merkez ofis", UiWeight.Bold, hq);
            VisualElement linked = Ui.El("bt-row bt-mapside__marker", tkMapSide);
            Ui.Symbol("check_circle", "bt-mapside__dot is-green-bg", linked);
            Ui.Text("bt-mapside__marker-text", "Hattın olan şehir", UiWeight.Bold, linked);
            Ui.El("bt-grow", tkMapSide).pickingMode = PickingMode.Ignore;
            Button cta = Ui.Button("bt-cta", string.Empty, OpenNewRoutePlanner, tkMapSide);
            cta.name = "new-route";
            VisualElement ctaText = Ui.El("bt-col bt-grow", cta);
            ctaText.pickingMode = PickingMode.Ignore;
            Ui.Text("bt-cta__title", "Yeni hat aç", UiWeight.Black, ctaText);
            tkMapSideCta = Ui.Text("bt-cta__sub", "", UiWeight.Bold, ctaText);
            VisualElement go = Ui.El("bt-cta__go", cta);
            go.pickingMode = PickingMode.Ignore;
            Ui.Symbol("arrow_forward", "bt-cta__go-icon", go);
        }

        void FocusHeadquarters()
        {
            if (FocusCityCenter()) return; // şehir haritasında: şehrin ortası
            CityData hq = session.IsRunning ? session.Headquarters : null;
            if (hq != null) mapCamera.FocusOn(world.GetCityPosition(hq), 34f);
        }

        void UpdateToolkitMapWidgets(float k)
        {
            bool onMap = MapShown && !StationUi;
            if (!onMap) Ui.Show(tkMapAttribution, false);
            Ui.Show(tkMapFrame, onMap);
            Ui.Show(tkMapEdge, onMap);
            Ui.Show(tkMapControls, onMap);
            bool side = onMap && !ToolkitDrawsPanel(panel);
            Ui.Show(tkMapSide, side);
            if (!onMap) return;

            Rect card = MapCardRect;
            const float ring = 40f; // çerçeve kalınlığı (birim): köşelerin dışını kapatır
            Ui.Place(tkMapFrame, card.x * k - ring, card.y * k - ring, card.width * k + 2f * ring, card.height * k + 2f * ring);
            Ui.Place(tkMapEdge, card.x * k, card.y * k, card.width * k, card.height * k);
            float pad = 12f;
            Ui.Place(tkMapControls, card.x * k + pad, card.y * k + pad, card.width * k - 2f * pad, card.height * k - 2f * pad);
            bool attribution = CityMapShown;
            Ui.Show(tkMapAttribution, attribution);
            if (attribution) Ui.Move(tkMapAttribution, card.x * k + 16f, card.yMax * k - 26f);
            CityData hq = session.Headquarters;
            Ui.SetText(tkMapLocText, hq != null ? hq.DisplayName : "Merkez");

            if (side)
            {
                // Şehir/Türkiye geçişi: merkez şehrin sokak haritası varsa
                bool hasCity = HasCityMap;
                Ui.Show(tkMapViewSwitch, hasCity);
                if (hasCity)
                {
                    bool city = cityMapWanted && panel != Panel.Planner;
                    Ui.Toggle(tkMapViewCity, "is-active", city);
                    Ui.Toggle(tkMapViewCountry, "is-active", !city);
                    Ui.SetText(tkMapViewCityText, session.Headquarters.DisplayName);
                    tkMapViewCity.SetEnabled(cityView.Ready || cityView.Loading);
                }
                Rect rect = SideRect;
                Ui.Place(tkMapSide, rect.x * k, rect.y * k, rect.width * k, rect.height * k);
                tkCovered.Add(rect);
                RefreshMapSide();
            }
        }

        /// <summary>Sağ paneldeki hat listesi: en kârlı altı hat; yalnız gün ya da hat sayısı değişince yeniden kurulur.</summary>
        void RefreshMapSide()
        {
            var routes = session.Routes.Routes;
            int signature = session.State.currentDay * 397 + routes.Count;
            if (signature == tkMapSideSignature) return;
            tkMapSideSignature = signature;

            tkMapSideCaption.text = $"HATLARIM · {routes.Count}";
            tkMapSideList.Clear();
            tkMapSideRoutes.Clear();
            tkMapSideRoutes.AddRange(routes);
            tkMapSideRoutes.Sort((a, b) => b.LastReport.Profit.CompareTo(a.LastReport.Profit));
            if (tkMapSideRoutes.Count == 0)
                Ui.Text("bt-mapside__empty", "Henüz hattın yok. Haritada bir şehre dokun ya da aşağıdan yeni hat aç.", UiWeight.Regular, tkMapSideList);
            int shown = Mathf.Min(phone ? 4 : 6, tkMapSideRoutes.Count);
            for (int i = 0; i < shown; i++)
            {
                RouteState route = tkMapSideRoutes[i];
                Button row = Ui.Button("bt-row bt-mapside__route", string.Empty, () => OpenRoutePanelNow(route, RouteTab.Price), tkMapSideList);
                VisualElement swatch = Ui.El("bt-mapside__swatch", row);
                swatch.pickingMode = PickingMode.Ignore;
                swatch.style.backgroundColor = world.RouteIdentityColor(route.routeId);
                Ui.Text("bt-mapside__name bt-grow", RouteName(route), UiWeight.Bold, row);
                float profit = route.HasHistory ? route.LastReport.Profit : 0f;
                Ui.Text(profit >= 0f ? "bt-mapside__value is-up" : "bt-mapside__value is-down", Fmt.Signed(profit), UiWeight.Bold, row);
            }
            int locked = 0;
            foreach (var city in session.World.Cities)
                if (!session.Progression.IsRegionUnlocked(city.Region)) locked++;
            tkMapSideCta.text = $"{session.World.Cities.Count - locked} şehir seni bekliyor";
        }

        // ------------------------------------------------------------------ şehir ipucu

        void BuildToolkitTooltip()
        {
            tkTooltip = Ui.El("bt-tooltip", toolkit.Root);
            tkTooltip.usageHints = UsageHints.DynamicTransform; // fareyi izler
            tkTooltip.pickingMode = PickingMode.Ignore;
            tkTooltip.style.display = DisplayStyle.None;
        }

        /// <summary>Fare bir şehrin üstündeyken: ad, ünü, bölge ve nüfus, hat sayısı, tıklayınca ne olacağı. İçerik yalnız değişince kurulur.</summary>
        void UpdateToolkitTooltip()
        {
            CityData city = hoveredCity;
            bool show = city != null && !InMenu && modal == Modal.None;
            Ui.Show(tkTooltip, show);
            if (!show)
            {
                tkTooltipSignature = int.MinValue;
                return;
            }

            int routes = 0;
            if (mode == Mode.Playing)
                foreach (var route in session.Routes.Routes)
                    if (route.cityAId == city.Id || route.cityBId == city.Id) routes++;
            bool planning = mode == Mode.Playing && panel == Panel.Planner && plannerOrigin != null && plannerOrigin != city;
            int signature = city.Id.GetHashCode() * 31 + (int)mode;
            signature = signature * 31 + routes;
            signature = signature * 31 + (planning ? plannerOrigin.Id.GetHashCode() : 0);
            signature = signature * 31 + (mode == Mode.Playing && session.Progression.IsRegionUnlocked(city.Region) ? 1 : 0);
            if (signature != tkTooltipSignature)
            {
                tkTooltipSignature = signature;
                BuildTooltip(city, routes, planning);
            }

            // Fareyi izler; ekrandan taşacaksa öbür yana geçer
            float k = 1f / u;
            float width = (UICityIcons.Has(city) ? 372f : 330f) * u;
            float height = float.IsNaN(tkTooltip.resolvedStyle.height) ? 80f * u : tkTooltip.resolvedStyle.height * u;
            Vector2 mouse = hoverMouseGui;
            float x = mouse.x + 18f * u, y = mouse.y + 14f * u;
            if (x + width > W) x = mouse.x - width - 12f * u;
            if (y + height > H) y = mouse.y - height - 12f * u;
            Ui.Move(tkTooltip, x * k, y * k);
        }

        void BuildTooltip(CityData city, int routes, bool planning)
        {
            tkTooltip.Clear();
            tkTooltip.style.width = (UICityIcons.Has(city) ? 372f : 330f);
            VisualElement row = Ui.El("bt-row bt-tooltip__row", tkTooltip);
            if (UICityIcons.Has(city))
            {
                VisualElement icon = Ui.El("bt-tooltip__city-icon", row);
                Sprite sprite = UICityIcons.Sprite(city);
                if (sprite != null) icon.style.backgroundImage = new StyleBackground(sprite);
            }
            VisualElement text = Ui.El("bt-col bt-grow", row);
            Ui.Text("bt-tooltip__title", city.DisplayName, UiWeight.Black, text);
            string famousFor = UICityIcons.Description(city);
            if (!string.IsNullOrEmpty(famousFor)) Ui.Text("bt-tooltip__line is-gold", famousFor, UiWeight.Bold, text);
            Ui.Text("bt-tooltip__line bt-dim", $"{city.Region.DisplayName} · nüfus {Fmt.Population(city.Population)}", UiWeight.Regular, text);
            if (mode == Mode.Playing)
            {
                if (routes > 0) Ui.Text("bt-tooltip__line is-up", $"{routes} hattınız var", UiWeight.Bold, text);
                if (!session.Progression.IsRegionUnlocked(city.Region))
                    Ui.Text("bt-tooltip__line is-warning-text", "Kilitli bölge — Bölgeler (B) panelinden lisans alın", UiWeight.Bold, text);
                else if (planning)
                    Ui.Text("bt-tooltip__line is-blue", $"Tıkla: varış olarak seç · {Fmt.Km(session.World.GetRoadDistanceKm(plannerOrigin, city))}", UiWeight.Bold, text);
                else
                    Ui.Text("bt-tooltip__line is-blue", "Tıkla: durağa gir", UiWeight.Bold, text);
            }
            else if (mode == Mode.NewGame)
            {
                Ui.Text("bt-tooltip__line is-blue", "Tıkla: merkez ofis olarak seç", UiWeight.Bold, text);
            }
            Ui.Icon(UIIcons.Flag(city.Region.Id), "bt-tooltip__flag", row);
        }
    }
}
