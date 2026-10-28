using System;
using System.Collections.Generic;
using BusTycoon.Core;
using BusTycoon.Data;
using BusTycoon.Progression;
using BusTycoon.Routes;
using UnityEngine;

namespace BusTycoon.Presentation.Map
{
    /// <summary>
    /// 3D haritanın görünüm katmanı: şehir düğümlerini ve hat çizgilerini oluşturur, hat kârlılığına göre
    /// renklendirir ve tıklamaları UI'a <see cref="CityClicked"/> olayıyla iletir. Oyun kuralı içermez.
    /// </summary>
    public sealed class WorldMapView : MonoBehaviour
    {
        [SerializeField] CityNodeView cityNodePrefab;
        [SerializeField] RouteLineView routeLinePrefab;
        [SerializeField] Transform cityRoot;
        [SerializeField] Transform routeRoot;
        [Tooltip("Bu kademeye kadar olan şehirler oluşturulur (0 = yalnız metropoller, 3 = hepsi).")]
        [SerializeField, Range(0, 3)] int maxSpawnTier = 3;

        [Header("Hat renkleri")]
        [SerializeField] Color profitableColor = new Color(0.3f, 0.85f, 0.45f);
        [SerializeField] Color marginalColor = new Color(1f, 0.8f, 0.2f);
        [SerializeField] Color lossColor = new Color(0.95f, 0.3f, 0.3f);
        [SerializeField] Color idleColor = new Color(0.6f, 0.6f, 0.6f);
        [SerializeField] Color previewColor = new Color(0.4f, 0.75f, 1f);

        readonly Dictionary<string, CityNodeView> nodes = new Dictionary<string, CityNodeView>();
        readonly Dictionary<string, RouteLineView> routeLines = new Dictionary<string, RouteLineView>();
        readonly HashSet<string> highlightedCityIds = new HashSet<string>();
        RouteLineView previewLine;
        GameSession session;
        RouteSystem subscribedRoutes;
        ProgressionSystem subscribedProgression;

        public event Action<CityData> CityClicked;
        public event Action<CityData> CityHoverEntered;
        public event Action<CityData> CityHoverExited;

        void Start()
        {
            session = GameSession.Instance;
            SpawnCities();
            session.SessionStarted += HandleSessionStarted;
            session.DayCompleted += HandleDayCompleted;
            if (session.IsRunning) HandleSessionStarted();
        }

        void OnDestroy()
        {
            if (session == null) return;
            session.SessionStarted -= HandleSessionStarted;
            session.DayCompleted -= HandleDayCompleted;
            UnsubscribeRoutes();
        }

        // ------------------------------------------------------------------ şehirler

        void SpawnCities()
        {
            var projection = session.World.Projection;
            foreach (var city in session.World.Cities)
            {
                if (city == null || city.MapTier > maxSpawnTier) continue;

                CityNodeView node = Instantiate(cityNodePrefab, cityRoot);
                node.Bind(city, projection.GeoToWorld(city.Coordinate), projection.SurfaceNormal(city.Coordinate));
                node.Clicked += n => CityClicked?.Invoke(n.City);
                node.HoverEntered += n => CityHoverEntered?.Invoke(n.City);
                node.HoverExited += n => CityHoverExited?.Invoke(n.City);
                nodes[city.Id] = node;
            }
        }

        public CityNodeView GetNode(CityData city) =>
            city != null && nodes.TryGetValue(city.Id, out var node) ? node : null;

        /// <summary>Hat planlama gibi geçici vurgular (başlangıç / varış). null ile vurgu kaldırılır.</summary>
        public void HighlightCity(CityData city, CityNodeState? highlight)
        {
            CityNodeView node = GetNode(city);
            if (node == null) return;

            if (highlight.HasValue)
            {
                highlightedCityIds.Add(city.Id);
                node.SetState(highlight.Value);
            }
            else
            {
                highlightedCityIds.Remove(city.Id);
                node.SetState(GetPersistentState(city));
            }
        }

        public void ClearHighlights()
        {
            var ids = new List<string>(highlightedCityIds);
            highlightedCityIds.Clear();
            foreach (var id in ids) HighlightCity(session.World.GetCity(id), null);
        }

        /// <summary>Zoom seviyesine göre etiketleri aç/kapa (kamera kontrolcüsü çağırır).</summary>
        public void SetLabelTier(int maxLabelTier)
        {
            foreach (var node in nodes.Values) node.SetLabelVisible(node.City.MapTier <= maxLabelTier);
        }

        CityNodeState GetPersistentState(CityData city)
        {
            if (!session.IsRunning) return CityNodeState.Normal;
            if (!session.Progression.IsRegionUnlocked(city.Region)) return CityNodeState.Locked;
            foreach (var route in session.Routes.Routes)
                if (route.cityAId == city.Id || route.cityBId == city.Id) return CityNodeState.Connected;
            return CityNodeState.Normal;
        }

        void RefreshCityStates()
        {
            foreach (var node in nodes.Values)
                if (!highlightedCityIds.Contains(node.City.Id)) node.SetState(GetPersistentState(node.City));
        }

        // ------------------------------------------------------------------ hatlar

        public void ShowPreview(CityData from, CityData to)
        {
            if (previewLine == null) previewLine = Instantiate(routeLinePrefab, routeRoot);
            previewLine.gameObject.SetActive(true);
            previewLine.Draw("PREVIEW", session.World.Projection, from.Coordinate, to.Coordinate);
            previewLine.SetColor(previewColor);
        }

        public void HidePreview()
        {
            if (previewLine != null) previewLine.gameObject.SetActive(false);
        }

        void HandleSessionStarted()
        {
            UnsubscribeRoutes();
            subscribedRoutes = session.Routes;
            subscribedRoutes.RouteOpened += HandleRouteOpened;
            subscribedRoutes.RouteClosed += HandleRouteClosed;
            subscribedRoutes.RouteChanged += HandleRouteChanged;
            subscribedProgression = session.Progression;
            subscribedProgression.RegionUnlocked += HandleRegionUnlocked;

            foreach (var line in routeLines.Values) Destroy(line.gameObject);
            routeLines.Clear();
            foreach (var route in session.Routes.Routes) CreateRouteLine(route);
            RefreshCityStates();
        }

        void UnsubscribeRoutes()
        {
            if (subscribedRoutes == null) return;
            subscribedRoutes.RouteOpened -= HandleRouteOpened;
            subscribedRoutes.RouteClosed -= HandleRouteClosed;
            subscribedRoutes.RouteChanged -= HandleRouteChanged;
            subscribedProgression.RegionUnlocked -= HandleRegionUnlocked;
            subscribedRoutes = null;
            subscribedProgression = null;
        }

        void CreateRouteLine(RouteState route)
        {
            CityData a = session.World.GetCity(route.cityAId);
            CityData b = session.World.GetCity(route.cityBId);
            if (a == null || b == null) return;

            RouteLineView line = Instantiate(routeLinePrefab, routeRoot);
            line.Draw(route.routeId, session.World.Projection, a.Coordinate, b.Coordinate);
            routeLines[route.routeId] = line;
            ColorRoute(route);
        }

        void ColorRoute(RouteState route)
        {
            if (!routeLines.TryGetValue(route.routeId, out var line)) return;

            if (session.Fleet.CountBusesOnRoute(route.routeId) == 0 || !route.HasHistory)
            {
                line.SetColor(idleColor);
                return;
            }

            RouteDayReport last = route.LastReport;
            float margin = last.revenue > 0f ? last.Profit / last.revenue : -1f;
            line.SetColor(margin < 0f ? lossColor : margin < 0.15f ? marginalColor : profitableColor);
        }

        void HandleRouteOpened(RouteState route)
        {
            CreateRouteLine(route);
            RefreshCityStates();
        }

        void HandleRouteClosed(RouteState route)
        {
            if (routeLines.TryGetValue(route.routeId, out var line))
            {
                Destroy(line.gameObject);
                routeLines.Remove(route.routeId);
            }
            RefreshCityStates();
        }

        void HandleRouteChanged(RouteState route) => ColorRoute(route);

        void HandleRegionUnlocked(RegionData region) => RefreshCityStates();

        void HandleDayCompleted(DayReport report)
        {
            foreach (var route in session.Routes.Routes) ColorRoute(route);
        }
    }
}
