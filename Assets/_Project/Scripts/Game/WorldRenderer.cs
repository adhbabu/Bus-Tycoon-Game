using System.Collections.Generic;
using BusTycoon.Core;
using BusTycoon.Data;
using BusTycoon.Fleet;
using BusTycoon.Progression;
using BusTycoon.Routes;
using BusTycoon.World;
using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// 3D haritanın tamamını prefab gerektirmeden kurar ve canlı tutar: harita zemini, şehir işaretçileri,
    /// kârlılığa göre renklenen hat yayları ve hatlarda gidip gelen otobüsler (kozmetik).
    /// Seçim/vurgu durumunu UI belirler (Origin, Destination, Hovered, SelectedRouteId ...).
    /// </summary>
    public sealed class WorldRenderer : MonoBehaviour
    {
        public sealed class CityMarker
        {
            public CityData City;
            public Vector3 Position;
            public Material Material;
            public Transform Root;
        }

        sealed class RouteVisual
        {
            public RouteState Route;
            public LineRenderer Line;
            public Vector3[] Points;
            public float TripSeconds;
            public readonly List<BusToken> Tokens = new List<BusToken>();
        }

        sealed class BusToken
        {
            public Transform Transform;
            public float Phase;
        }

        const int ArcSegments = 40;
        const float LineHeight = 0.12f;
        const int MaxTokensPerRoute = 6;

        static readonly Color CityNormal = new Color(0.78f, 0.75f, 0.64f);   // yumuşak kum rengi: ışıkta beyaza patlamaz
        static readonly Color CityConnected = new Color(0.35f, 0.86f, 0.48f);
        static readonly Color CityLocked = new Color(0.42f, 0.44f, 0.44f);
        static readonly Color CityHeadquarters = new Color(1f, 0.78f, 0.25f);
        static readonly Color CityOrigin = new Color(0.35f, 0.68f, 1f);
        static readonly Color CityDestination = new Color(1f, 0.55f, 0.15f);
        static readonly Color RouteProfit = new Color(0.35f, 0.88f, 0.5f);
        static readonly Color RouteMarginal = new Color(1f, 0.82f, 0.3f);
        static readonly Color RouteLoss = new Color(0.96f, 0.35f, 0.35f);
        static readonly Color RouteIdle = new Color(0.7f, 0.72f, 0.75f);
        static readonly Color RoutePreview = new Color(0.45f, 0.78f, 1f);
        static readonly Color[] TierColors =
        {
            new Color(0.95f, 0.95f, 0.95f), new Color(0.3f, 0.8f, 0.8f), new Color(0.3f, 0.55f, 1f),
            new Color(0.7f, 0.45f, 1f), new Color(1f, 0.78f, 0.25f), new Color(0.95f, 0.35f, 0.3f)
        };

        GameSession session;
        MapProjection projection;
        readonly List<CityMarker> markers = new List<CityMarker>();
        readonly Dictionary<CityData, CityMarker> markerByCity = new Dictionary<CityData, CityMarker>();
        readonly Dictionary<string, RouteVisual> routeVisuals = new Dictionary<string, RouteVisual>();
        readonly List<BusInstance> busBuffer = new List<BusInstance>();
        Material[] tierMaterials;
        LineRenderer previewLine;
        Transform cityRoot, routeRoot;
        float animationTime;
        bool colorsDirty = true;
        bool tokensDirty = true;

        RouteSystem boundRoutes;
        FleetSystem boundFleet;
        ProgressionSystem boundProgression;

        CityData hovered, origin, destination, headquarters;
        string selectedRouteId;

        public IReadOnlyList<CityMarker> Markers => markers;
        public Rect MapBoundsXZ { get; private set; }
        public Color SeaColor => new Color(0.086f, 0.21f, 0.345f);

        public CityData Hovered { get => hovered; set { if (hovered != value) { hovered = value; colorsDirty = true; } } }
        public CityData Origin { get => origin; set { if (origin != value) { origin = value; colorsDirty = true; } } }
        public CityData Destination { get => destination; set { if (destination != value) { destination = value; colorsDirty = true; } } }
        public CityData Headquarters { get => headquarters; set { if (headquarters != value) { headquarters = value; colorsDirty = true; } } }
        public string SelectedRouteId { get => selectedRouteId; set { if (selectedRouteId != value) { selectedRouteId = value; colorsDirty = true; } } }

        public void Init(GameSession gameSession, MapProjection mapProjection, Texture2D mapTexture)
        {
            session = gameSession;
            projection = mapProjection;
            cityRoot = new GameObject("Cities").transform;
            cityRoot.SetParent(transform, false);
            routeRoot = new GameObject("Routes").transform;
            routeRoot.SetParent(transform, false);

            BuildMapSurface(mapTexture);
            BuildCityMarkers();

            tierMaterials = new Material[TierColors.Length];
            for (int i = 0; i < TierColors.Length; i++) tierMaterials[i] = MaterialFactory.CreateLit(TierColors[i], 0.4f);

            previewLine = CreateLine("Preview", 0.22f);
            previewLine.gameObject.SetActive(false);

            session.SessionStarted += HandleSessionStarted;
            session.DayCompleted += _ => { colorsDirty = true; tokensDirty = true; };
            if (session.IsRunning) HandleSessionStarted();
        }

        void OnDestroy()
        {
            if (session != null) session.SessionStarted -= HandleSessionStarted;
            Unbind();
        }

        // ------------------------------------------------------------------ kurulum

        void BuildMapSurface(Texture2D texture)
        {
            var mercator = projection as MercatorMapProjection;
            float width = mercator != null ? mercator.MapWidth : DefaultContent.MapWidth;
            float height = mercator != null ? mercator.MapHeight : width * 0.5f;
            MapBoundsXZ = new Rect(-width / 2f, -height / 2f, width, height);

            var mesh = new Mesh { name = "MapSurface" };
            mesh.vertices = new[]
            {
                new Vector3(-width / 2f, 0f, -height / 2f), new Vector3(width / 2f, 0f, -height / 2f),
                new Vector3(width / 2f, 0f, height / 2f), new Vector3(-width / 2f, 0f, height / 2f)
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.triangles = new[] { 0, 3, 2, 0, 2, 1 };
            mesh.RecalculateNormals();
            if (mesh.normals[0].y < 0f) // sarım yönü ters çıkarsa yüzü çevir
            {
                mesh.triangles = new[] { 0, 2, 3, 0, 1, 2 };
                mesh.RecalculateNormals();
            }
            mesh.RecalculateBounds();

            var surface = new GameObject("MapSurface");
            surface.transform.SetParent(transform, false);
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = surface.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = texture != null
                ? MaterialFactory.CreateUnlitTexture(texture)
                : MaterialFactory.CreateLit(new Color(0.35f, 0.45f, 0.32f), 0f);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void BuildCityMarkers()
        {
            foreach (var city in session.World.Cities)
            {
                if (city == null) continue;
                Vector3 position = projection.GeoToWorld(city.Coordinate);
                float size = city.MapTier switch { 0 => 0.85f, 1 => 0.62f, 2 => 0.46f, _ => 0.34f };

                var root = new GameObject("City_" + city.Id).transform;
                root.SetParent(cityRoot, false);
                root.position = position;

                var material = MaterialFactory.CreateLit(CityNormal, 0.1f);
                CreatePrimitive(PrimitiveType.Cylinder, root, new Vector3(0f, 0.04f, 0f), new Vector3(size, 0.04f, size), material);
                if (city.MapTier <= 1 || city.IsCapital)
                    CreatePrimitive(PrimitiveType.Cylinder, root, new Vector3(0f, 0.3f, 0f), new Vector3(size * 0.22f, 0.3f, size * 0.22f), material);

                var marker = new CityMarker { City = city, Position = position, Material = material, Root = root };
                markers.Add(marker);
                markerByCity[city] = marker;
            }
        }

        static void CreatePrimitive(PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
        {
            var go = Primitives.Create(type, type.ToString());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        LineRenderer CreateLine(string lineName, float width)
        {
            var go = new GameObject(lineName);
            go.transform.SetParent(routeRoot, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = MaterialFactory.LineMaterial;
            line.useWorldSpace = true;
            line.widthMultiplier = width;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        // ------------------------------------------------------------------ oturum olayları

        void HandleSessionStarted()
        {
            Unbind();
            boundRoutes = session.Routes;
            boundFleet = session.Fleet;
            boundProgression = session.Progression;
            boundRoutes.RouteOpened += HandleRouteSetChanged;
            boundRoutes.RouteClosed += HandleRouteSetChanged;
            boundRoutes.RouteChanged += HandleRouteChanged;
            boundFleet.BusAdded += HandleFleetChanged;
            boundFleet.BusRemoved += HandleFleetChanged;
            boundFleet.BusChanged += HandleFleetChanged;
            boundProgression.RegionUnlocked += HandleRegionUnlocked;
            RebuildRoutes();
        }

        void Unbind()
        {
            if (boundRoutes != null)
            {
                boundRoutes.RouteOpened -= HandleRouteSetChanged;
                boundRoutes.RouteClosed -= HandleRouteSetChanged;
                boundRoutes.RouteChanged -= HandleRouteChanged;
            }
            if (boundFleet != null)
            {
                boundFleet.BusAdded -= HandleFleetChanged;
                boundFleet.BusRemoved -= HandleFleetChanged;
                boundFleet.BusChanged -= HandleFleetChanged;
            }
            if (boundProgression != null) boundProgression.RegionUnlocked -= HandleRegionUnlocked;
            boundRoutes = null;
            boundFleet = null;
            boundProgression = null;
        }

        void HandleRouteSetChanged(RouteState _) => RebuildRoutes();
        void HandleRouteChanged(RouteState _) { colorsDirty = true; tokensDirty = true; }
        void HandleFleetChanged(BusInstance _) => tokensDirty = true;
        void HandleRegionUnlocked(RegionData _) => colorsDirty = true;

        void RebuildRoutes()
        {
            foreach (var visual in routeVisuals.Values)
            {
                foreach (var token in visual.Tokens) Destroy(token.Transform.gameObject);
                Destroy(visual.Line.gameObject);
            }
            routeVisuals.Clear();

            if (session.IsRunning)
            {
                foreach (var route in session.Routes.Routes)
                {
                    CityData a = session.World.GetCity(route.cityAId);
                    CityData b = session.World.GetCity(route.cityBId);
                    if (a == null || b == null) continue;

                    var visual = new RouteVisual
                    {
                        Route = route,
                        Line = CreateLine("Route_" + route.routeId, 0.2f),
                        Points = SampleArc(a.Coordinate, b.Coordinate),
                        TripSeconds = 2.5f + route.distanceKm / 120f
                    };
                    visual.Line.positionCount = visual.Points.Length;
                    visual.Line.SetPositions(visual.Points);
                    routeVisuals[route.routeId] = visual;
                }
            }

            colorsDirty = true;
            tokensDirty = true;
        }

        Vector3[] SampleArc(GeoCoordinate from, GeoCoordinate to)
        {
            var points = new Vector3[ArcSegments + 1];
            Vector3 start = projection.GeoToWorld(from);
            Vector3 end = projection.GeoToWorld(to);
            float arcHeight = Vector3.Distance(start, end) * 0.06f;
            for (int i = 0; i <= ArcSegments; i++)
            {
                float t = i / (float)ArcSegments;
                GeoCoordinate geo = GeoCoordinate.Interpolate(from, to, t);
                points[i] = projection.GeoToWorld(geo) + projection.SurfaceNormal(geo) * (LineHeight + 4f * t * (1f - t) * arcHeight);
            }
            return points;
        }

        public void SetPreview(CityData from, CityData to)
        {
            bool show = from != null && to != null && from != to;
            previewLine.gameObject.SetActive(show);
            if (!show) return;

            Vector3[] points = SampleArc(from.Coordinate, to.Coordinate);
            previewLine.positionCount = points.Length;
            previewLine.SetPositions(points);
            previewLine.startColor = previewLine.endColor = RoutePreview;
        }

        // ------------------------------------------------------------------ her kare

        void LateUpdate()
        {
            if (colorsDirty) RefreshColors();
            if (tokensDirty) RebuildTokens();
            AnimateTokens();
        }

        void RefreshColors()
        {
            colorsDirty = false;
            bool running = session.IsRunning;

            foreach (var marker in markers)
            {
                CityData city = marker.City;
                Color color = CityNormal;
                if (running)
                {
                    if (!session.Progression.IsRegionUnlocked(city.Region)) color = CityLocked;
                    else if (HasRoute(city)) color = CityConnected;
                }
                if (city == headquarters) color = CityHeadquarters;
                if (city == origin) color = CityOrigin;
                else if (city == destination) color = CityDestination;
                if (city == hovered) color = Color.Lerp(color, Color.white, 0.45f);
                marker.Material.color = color;
                if (marker.Material.HasProperty("_BaseColor")) marker.Material.SetColor("_BaseColor", color);
            }

            foreach (var visual in routeVisuals.Values)
            {
                bool selected = visual.Route.routeId == selectedRouteId;
                Color color = RouteColor(visual.Route);
                if (selected) color = Color.Lerp(color, Color.white, 0.35f);
                visual.Line.startColor = visual.Line.endColor = color;
                visual.Line.widthMultiplier = selected ? 0.38f : 0.2f;
            }
        }

        bool HasRoute(CityData city)
        {
            foreach (var route in session.Routes.Routes)
                if (route.cityAId == city.Id || route.cityBId == city.Id) return true;
            return false;
        }

        Color RouteColor(RouteState route)
        {
            if (session.Fleet.CountBusesOnRoute(route.routeId) == 0 || !route.HasHistory) return RouteIdle;
            RouteDayReport last = route.LastReport;
            float margin = last.revenue > 0f ? last.Profit / last.revenue : -1f;
            return margin < 0f ? RouteLoss : margin < 0.15f ? RouteMarginal : RouteProfit;
        }

        void RebuildTokens()
        {
            tokensDirty = false;
            if (!session.IsRunning) return;

            foreach (var visual in routeVisuals.Values)
            {
                session.Fleet.GetBusesOnRoute(visual.Route.routeId, busBuffer);
                busBuffer.RemoveAll(b => !b.IsAvailableToday || b.Model == null);
                int wanted = Mathf.Min(busBuffer.Count, MaxTokensPerRoute);

                while (visual.Tokens.Count > wanted)
                {
                    int last = visual.Tokens.Count - 1;
                    Destroy(visual.Tokens[last].Transform.gameObject);
                    visual.Tokens.RemoveAt(last);
                }
                while (visual.Tokens.Count < wanted)
                {
                    var go = Primitives.Create(PrimitiveType.Cube, "Bus");
                    go.transform.SetParent(routeRoot, false);
                    go.transform.localScale = new Vector3(0.28f, 0.2f, 0.62f);
                    visual.Tokens.Add(new BusToken { Transform = go.transform, Phase = visual.Tokens.Count / (float)Mathf.Max(1, wanted) * 2f });
                }

                for (int i = 0; i < visual.Tokens.Count; i++)
                {
                    int tier = Mathf.Clamp(busBuffer[i].Model.Tier, 0, tierMaterials.Length - 1);
                    visual.Tokens[i].Transform.GetComponent<Renderer>().sharedMaterial = tierMaterials[tier];
                    visual.Tokens[i].Phase = i / (float)Mathf.Max(1, visual.Tokens.Count) * 2f;
                }
            }
        }

        void AnimateTokens()
        {
            if (!session.IsRunning) return;
            int speed = session.Clock != null ? session.Clock.Speed : 0;
            animationTime += Time.unscaledDeltaTime * speed;

            foreach (var visual in routeVisuals.Values)
            {
                Vector3[] points = visual.Points;
                foreach (var token in visual.Tokens)
                {
                    float cycle = Mathf.Repeat(animationTime / visual.TripSeconds + token.Phase, 2f);
                    bool returning = cycle > 1f;
                    float t = returning ? 2f - cycle : cycle;

                    float f = t * (points.Length - 1);
                    int i = Mathf.Min(Mathf.FloorToInt(f), points.Length - 2);
                    Vector3 position = Vector3.Lerp(points[i], points[i + 1], f - i);
                    Vector3 direction = points[i + 1] - points[i];
                    if (returning) direction = -direction;

                    // Gidiş ve dönüş otobüsleri yan yana şeritlerde görünsün.
                    Vector3 side = Vector3.Cross(Vector3.up, direction.normalized) * 0.18f;
                    token.Transform.position = position + side + Vector3.up * 0.1f;
                    if (direction.sqrMagnitude > 1e-6f) token.Transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
                }
            }
        }

        // ------------------------------------------------------------------ seçim (ekran uzayında)

        public bool TryPickCity(Camera camera, Vector2 screenPoint, float radiusPixels, out CityData city)
        {
            city = null;
            float best = radiusPixels * radiusPixels;
            foreach (var marker in markers)
            {
                Vector3 sp = camera.WorldToScreenPoint(marker.Position);
                if (sp.z <= 0f) continue;
                float d = ((Vector2)sp - screenPoint).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    city = marker.City;
                }
            }
            return city != null;
        }

        public bool TryPickRoute(Camera camera, Vector2 screenPoint, float radiusPixels, out RouteState route)
        {
            route = null;
            float best = radiusPixels;
            foreach (var visual in routeVisuals.Values)
            {
                Vector3[] points = visual.Points;
                Vector2 previous = camera.WorldToScreenPoint(points[0]);
                for (int i = 1; i < points.Length; i++)
                {
                    Vector3 sp = camera.WorldToScreenPoint(points[i]);
                    Vector2 current = sp;
                    if (sp.z > 0f)
                    {
                        float d = DistanceToSegment(screenPoint, previous, current);
                        if (d < best)
                        {
                            best = d;
                            route = visual.Route;
                        }
                    }
                    previous = current;
                }
            }
            return route != null;
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        public Vector3 GetCityPosition(CityData city) =>
            city != null && markerByCity.TryGetValue(city, out var marker) ? marker.Position : Vector3.zero;

        public Vector3 GetRouteMidpoint(RouteState route) =>
            routeVisuals.TryGetValue(route.routeId, out var visual) ? visual.Points[visual.Points.Length / 2] : Vector3.zero;
    }
}
