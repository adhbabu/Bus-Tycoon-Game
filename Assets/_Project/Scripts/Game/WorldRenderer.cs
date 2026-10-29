using System;
using System.Collections.Generic;
using BusTycoon.Core;
using BusTycoon.Data;
using BusTycoon.Fleet;
using BusTycoon.Progression;
using BusTycoon.Routes;
using BusTycoon.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace BusTycoon.Game
{
    /// <summary>
    /// 3D haritanın tamamını prefab gerektirmeden kurar ve canlı tutar: harita zemini, karayolları, şehir
    /// işaretçileri, kârlılığa göre renklenen hat çizgileri ve hatlarda gidip gelen otobüsler (kozmetik).
    /// Hatlar ve otobüsler <see cref="RoadNetwork"/> varsa gerçek yol güzergâhını izler, yoksa yay çizer.
    /// Seçim/vurgu durumunu UI belirler (Origin, Destination, Hovered, SelectedRouteId, ZoomDistance ...).
    /// </summary>
    public sealed class WorldRenderer : MonoBehaviour
    {
        public sealed class CityMarker
        {
            public CityData City;
            public Vector3 Position;
            public Material Material;
            public Transform Root;
            /// <summary>İşaretin tepesi (ölçeklenmeden önce); şehir adı bunun üstüne yazılır.</summary>
            public float Height;
        }

        sealed class RouteVisual
        {
            public RouteState Route;
            public LineRenderer Line;
            public Vector3[] Points;
            public float[] Cumulative;
            public float TripSeconds;
            public readonly List<BusToken> Tokens = new List<BusToken>();
        }

        sealed class BusToken
        {
            public Transform Transform;
            public Renderer Body;
            public float Phase;
        }

        const int ArcSegments = 40;
        const float LineHeight = 0.12f;
        const float RoadHeight = 0.02f;
        const float RouteHeight = 0.05f;
        const int MaxTokensPerRoute = 6;
        /// <summary>Çizgi kalınlıkları ve otobüs boyu bu kamera uzaklığına göre ayarlanmıştır.</summary>
        const float ReferenceZoom = 40f;

        static readonly Color CityNormal = new Color(0.8f, 0.69f, 0.6f);     // şeftali-bej: ışıkta beyaza patlamaz
        static readonly Color CityRim = new Color(0.97f, 0.96f, 0.92f);
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

        // Karayolu çizimi (RoadNetwork.RoadClass sırası): otoyol, ana yol, diğer yol, feribot
        static readonly float[] RoadWidths = { 0.13f, 0.085f, 0.045f, 0.05f };
        static readonly Color[] RoadColors =
        {
            new Color(1f, 0.72f, 0.3f), new Color(0.98f, 0.95f, 0.86f, 0.85f),
            new Color(0.95f, 0.92f, 0.84f, 0.32f), new Color(0.55f, 0.82f, 1f, 0.75f)
        };
        static readonly Color MotorwayCasing = new Color(0.36f, 0.2f, 0.06f, 0.6f);

        GameSession session;
        MapProjection projection;
        RoadNetwork roads;
        readonly List<CityMarker> markers = new List<CityMarker>();
        readonly Dictionary<CityData, CityMarker> markerByCity = new Dictionary<CityData, CityMarker>();
        readonly Dictionary<string, RouteVisual> routeVisuals = new Dictionary<string, RouteVisual>();
        readonly Dictionary<string, Vector3[]> pathCache = new Dictionary<string, Vector3[]>();
        readonly List<GeoCoordinate> pathBuffer = new List<GeoCoordinate>();
        readonly List<BusInstance> busBuffer = new List<BusInstance>();
        Material[] tierMaterials;
        Material windowMaterial, roofMaterial, wheelMaterial, headlightMaterial, cityRimMaterial;
        LineRenderer previewLine;
        Transform cityRoot, routeRoot;
        float animationTime;
        bool colorsDirty = true;
        bool tokensDirty = true;

        readonly List<(RoadNetwork.RoadClass Class, Vector3[] Points)> roadPieces = new List<(RoadNetwork.RoadClass, Vector3[])>();
        Mesh roadMesh;
        float roadScale = -1f;
        float zoomDistance = ReferenceZoom;
        readonly List<Vector3> roadVertices = new List<Vector3>();
        readonly List<Color> roadVertexColors = new List<Color>();
        readonly List<int> roadIndices = new List<int>();

        RouteSystem boundRoutes;
        FleetSystem boundFleet;
        ProgressionSystem boundProgression;

        CityData hovered, origin, destination, headquarters;
        string selectedRouteId;

        public IReadOnlyList<CityMarker> Markers => markers;
        public Rect MapBoundsXZ { get; private set; }
        public Color SeaColor => new Color(0.05f, 0.15f, 0.26f);

        public CityData Hovered { get => hovered; set { if (hovered != value) { hovered = value; colorsDirty = true; } } }
        public CityData Origin { get => origin; set { if (origin != value) { origin = value; colorsDirty = true; } } }
        public CityData Destination { get => destination; set { if (destination != value) { destination = value; colorsDirty = true; } } }
        public CityData Headquarters { get => headquarters; set { if (headquarters != value) { headquarters = value; colorsDirty = true; } } }
        public string SelectedRouteId { get => selectedRouteId; set { if (selectedRouteId != value) { selectedRouteId = value; colorsDirty = true; } } }

        /// <summary>Kamera uzaklığı: yol ve hat kalınlıkları, otobüs ve şehir boyları buna göre ölçeklenir.</summary>
        public float ZoomDistance { set => zoomDistance = value; }

        float ZoomScale => Mathf.Clamp(zoomDistance / ReferenceZoom, 0.45f, 1.6f);

        public void Init(GameSession gameSession, MapProjection mapProjection, Texture2D mapTexture, RoadNetwork roadNetwork)
        {
            session = gameSession;
            projection = mapProjection;
            roads = roadNetwork;
            cityRoot = new GameObject("Cities").transform;
            cityRoot.SetParent(transform, false);
            routeRoot = new GameObject("Routes").transform;
            routeRoot.SetParent(transform, false);

            BuildMapSurface(mapTexture);
            BuildRoadLayer();
            BuildCityMarkers();

            tierMaterials = new Material[TierColors.Length];
            for (int i = 0; i < TierColors.Length; i++) tierMaterials[i] = MaterialFactory.CreateLit(TierColors[i], 0.4f);
            windowMaterial = MaterialFactory.CreateLit(new Color(0.16f, 0.22f, 0.32f), 0.7f);
            roofMaterial = MaterialFactory.CreateLit(new Color(0.97f, 0.97f, 0.95f), 0.3f);
            wheelMaterial = MaterialFactory.CreateLit(new Color(0.12f, 0.12f, 0.14f), 0.1f);
            headlightMaterial = MaterialFactory.CreateLit(new Color(1f, 0.93f, 0.55f), 0.6f);

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
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Tüm yollar tek bir şerit ağında (tek çizim çağrısı); kalınlık yakınlaştırmayla güncellenir.</summary>
        void BuildRoadLayer()
        {
            if (roads == null) return;

            // Ağ, güzergâhlar kopmasın diye harita sınırının biraz dışına taşar; çizim harita kenarında kesilir.
            roadPieces.Clear();
            var points = new List<Vector3>();
            foreach (RoadNetwork.Edge edge in roads.Edges)
            {
                points.Clear();
                foreach (GeoCoordinate geo in edge.Points) points.Add(projection.GeoToWorld(geo) + Vector3.up * RoadHeight);
                ClipToMap(edge.Class, points);
            }

            var go = new GameObject("Roads");
            go.transform.SetParent(transform, false);
            roadMesh = new Mesh { name = "Roads", indexFormat = IndexFormat.UInt32 };
            go.AddComponent<MeshFilter>().sharedMesh = roadMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            // Köşe renkli, ışıksız; hat çizgilerinden önce çizilir.
            renderer.sharedMaterial = new Material(MaterialFactory.LineMaterial) { renderQueue = 2950 };
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            RebuildRoadMesh(ZoomScale);
        }

        void RebuildRoadMesh(float scale)
        {
            roadScale = scale;
            roadVertices.Clear();
            roadVertexColors.Clear();
            roadIndices.Clear();

            // Çizim sırası: diğer yollar → feribot → ana yollar → otoyol kenarı → otoyol
            AppendRoadClass(RoadNetwork.RoadClass.MinorRoad, RoadWidths[2] * scale, RoadColors[2]);
            AppendRoadClass(RoadNetwork.RoadClass.Ferry, RoadWidths[3] * scale, RoadColors[3]);
            AppendRoadClass(RoadNetwork.RoadClass.MainRoad, RoadWidths[1] * scale, RoadColors[1]);
            AppendRoadClass(RoadNetwork.RoadClass.Motorway, RoadWidths[0] * scale * 1.6f, MotorwayCasing);
            AppendRoadClass(RoadNetwork.RoadClass.Motorway, RoadWidths[0] * scale, RoadColors[0]);

            roadMesh.Clear();
            roadMesh.SetVertices(roadVertices);
            roadMesh.SetColors(roadVertexColors);
            roadMesh.SetTriangles(roadIndices, 0);
            roadMesh.RecalculateBounds();
        }

        void AppendRoadClass(RoadNetwork.RoadClass roadClass, float width, Color color)
        {
            foreach (var piece in roadPieces)
                if (piece.Class == roadClass) AppendRibbon(piece.Points, width, color);
        }

        /// <summary>Çoklu çizginin harita dikdörtgeni içindeki parçalarını (kenarda kesilmiş olarak) ekler.</summary>
        void ClipToMap(RoadNetwork.RoadClass roadClass, List<Vector3> points)
        {
            Rect bounds = MapBoundsXZ;
            var piece = new List<Vector3>();
            for (int i = 1; i < points.Count; i++)
            {
                Vector3 a = points[i - 1], b = points[i];
                if (!ClipSegment(bounds, ref a, ref b))
                {
                    Flush(roadClass, piece);
                    continue;
                }
                if (piece.Count == 0 || (piece[piece.Count - 1] - a).sqrMagnitude > 1e-8f)
                {
                    Flush(roadClass, piece);
                    piece.Add(a);
                }
                piece.Add(b);
            }
            Flush(roadClass, piece);
        }

        void Flush(RoadNetwork.RoadClass roadClass, List<Vector3> piece)
        {
            if (piece.Count >= 2) roadPieces.Add((roadClass, piece.ToArray()));
            piece.Clear();
        }

        /// <summary>Liang–Barsky: doğru parçasını XZ dikdörtgenine kırpar; tamamen dışarıdaysa false.</summary>
        static bool ClipSegment(Rect bounds, ref Vector3 a, ref Vector3 b)
        {
            float t0 = 0f, t1 = 1f;
            float dx = b.x - a.x, dz = b.z - a.z;
            if (!ClipEdge(-dx, a.x - bounds.xMin, ref t0, ref t1) || !ClipEdge(dx, bounds.xMax - a.x, ref t0, ref t1) ||
                !ClipEdge(-dz, a.z - bounds.yMin, ref t0, ref t1) || !ClipEdge(dz, bounds.yMax - a.z, ref t0, ref t1))
                return false;
            Vector3 start = a, delta = b - a;
            a = start + delta * t0;
            b = start + delta * t1;
            return true;
        }

        static bool ClipEdge(float p, float q, ref float t0, ref float t1)
        {
            if (Mathf.Abs(p) < 1e-9f) return q >= 0f;
            float t = q / p;
            if (p < 0f)
            {
                if (t > t1) return false;
                if (t > t0) t0 = t;
            }
            else
            {
                if (t < t0) return false;
                if (t < t1) t1 = t;
            }
            return true;
        }

        /// <summary>Yere yatık şerit: her noktada iki köşe; dönüşlerde kalınlık korunur (sınırlı miter).</summary>
        void AppendRibbon(Vector3[] points, float width, Color color)
        {
            if (points.Length < 2) return;
            int start = roadVertices.Count;
            float half = width * 0.5f;
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 previous = points[Mathf.Max(0, i - 1)];
                Vector3 next = points[Mathf.Min(points.Length - 1, i + 1)];
                Vector3 direction = next - previous;
                direction.y = 0f;
                if (direction.sqrMagnitude < 1e-10f) direction = Vector3.forward;
                direction.Normalize();

                float miter = 1f;
                if (i > 0 && i < points.Length - 1)
                {
                    Vector3 incoming = points[i] - points[i - 1];
                    incoming.y = 0f;
                    if (incoming.sqrMagnitude > 1e-10f)
                        miter = Mathf.Min(1f / Mathf.Max(Vector3.Dot(incoming.normalized, direction), 0.5f), 2f);
                }

                Vector3 offset = new Vector3(-direction.z, 0f, direction.x) * (half * miter);
                roadVertices.Add(points[i] + offset);
                roadVertices.Add(points[i] - offset);
                roadVertexColors.Add(color);
                roadVertexColors.Add(color);

                if (i == 0) continue;
                int k = start + i * 2;
                roadIndices.Add(k - 2);
                roadIndices.Add(k);
                roadIndices.Add(k - 1);
                roadIndices.Add(k - 1);
                roadIndices.Add(k);
                roadIndices.Add(k + 1);
            }
        }

        /// <summary>
        /// Şehir işareti: beyaz taban halkası üstünde yarısı gömülü, parlak renkli bir kubbe ("şeker damlası").
        /// Büyük şehirlerde kubbenin üstünde ayrıca bir lolipop iğne durur. Renk durumu gösterir (bkz. RefreshColors).
        /// </summary>
        void BuildCityMarkers()
        {
            cityRimMaterial = MaterialFactory.CreateLit(CityRim, 0.2f);
            foreach (var city in session.World.Cities)
            {
                if (city == null) continue;
                Vector3 position = projection.GeoToWorld(city.Coordinate);
                float size = city.MapTier switch { 0 => 0.6f, 1 => 0.48f, 2 => 0.38f, _ => 0.3f };

                var root = new GameObject("City_" + city.Id).transform;
                root.SetParent(cityRoot, false);
                root.position = position;

                var material = MaterialFactory.CreateLit(CityNormal, 0.55f);
                CreatePart(PrimitiveType.Cylinder, root, new Vector3(0f, 0.015f, 0f), new Vector3(size * 1.35f, 0.015f, size * 1.35f), cityRimMaterial);
                CreatePart(PrimitiveType.Sphere, root, new Vector3(0f, 0.03f, 0f), new Vector3(size, size * 0.7f, size), material);
                bool pin = city.MapTier <= 1 || city.IsCapital;
                if (pin)
                {
                    CreatePart(PrimitiveType.Cylinder, root, new Vector3(0f, 0.03f + size * 0.65f, 0f), new Vector3(0.06f, size * 0.4f, 0.06f), cityRimMaterial);
                    CreatePart(PrimitiveType.Sphere, root, new Vector3(0f, 0.03f + size * 1.17f, 0f), Vector3.one * (size * 0.42f), material);
                }

                var marker = new CityMarker { City = city, Position = position, Material = material, Root = root, Height = size * (pin ? 1.4f : 0.4f) };
                markers.Add(marker);
                markerByCity[city] = marker;
            }
        }

        static Renderer CreatePart(PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
        {
            var go = Primitives.Create(type, type.ToString());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        /// <summary>Küçük, sevimli otobüs: renkli gövde, koyu cam bandı, beyaz tavan, iki tekerlek aksı ve farlar.</summary>
        BusToken CreateBus()
        {
            var root = new GameObject("Bus").transform;
            root.SetParent(routeRoot, false);
            Renderer body = CreatePart(PrimitiveType.Cube, root, new Vector3(0f, 0.13f, 0f), new Vector3(0.26f, 0.17f, 0.6f), tierMaterials[0]);
            CreatePart(PrimitiveType.Cube, root, new Vector3(0f, 0.165f, 0.02f), new Vector3(0.272f, 0.065f, 0.5f), windowMaterial);
            CreatePart(PrimitiveType.Cube, root, new Vector3(0f, 0.226f, 0f), new Vector3(0.23f, 0.025f, 0.54f), roofMaterial);
            foreach (float z in new[] { -0.19f, 0.19f })
            {
                var wheel = CreatePart(PrimitiveType.Cylinder, root, new Vector3(0f, 0.055f, z), new Vector3(0.1f, 0.14f, 0.1f), wheelMaterial);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            foreach (float x in new[] { -0.075f, 0.075f }) // farlar: otobüsün önü (+z) gidiş yönüne bakar
                CreatePart(PrimitiveType.Cube, root, new Vector3(x, 0.085f, 0.301f), new Vector3(0.06f, 0.04f, 0.012f), headlightMaterial);
            return new BusToken { Transform = root, Body = body };
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
            line.shadowCastingMode = ShadowCastingMode.Off;
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

                    Vector3[] points = GetPath(a, b);
                    var visual = new RouteVisual
                    {
                        Route = route,
                        Line = CreateLine("Route_" + route.routeId, 0.2f),
                        Points = points,
                        Cumulative = CumulativeLengths(points),
                        TripSeconds = 2.5f + route.distanceKm / 120f
                    };
                    visual.Line.positionCount = points.Length;
                    visual.Line.SetPositions(points);
                    routeVisuals[route.routeId] = visual;
                }
            }

            colorsDirty = true;
            tokensDirty = true;
        }

        /// <summary>İki şehir arasındaki yol güzergâhı (önbellekli); yol ağı yoksa ya da bağlantı bulunamazsa yay.</summary>
        Vector3[] GetPath(CityData a, CityData b)
        {
            bool ordered = string.CompareOrdinal(a.Id, b.Id) <= 0;
            CityData first = ordered ? a : b, second = ordered ? b : a;
            string key = first.Id + "|" + second.Id;
            if (pathCache.TryGetValue(key, out Vector3[] points)) return points;

            if (roads != null && roads.TryFindPath(first.Coordinate, second.Coordinate, pathBuffer))
            {
                points = new Vector3[pathBuffer.Count];
                for (int i = 0; i < points.Length; i++) points[i] = projection.GeoToWorld(pathBuffer[i]) + Vector3.up * RouteHeight;
            }
            else
            {
                points = SampleArc(first.Coordinate, second.Coordinate);
            }
            pathCache[key] = points;
            return points;
        }

        static float[] CumulativeLengths(Vector3[] points)
        {
            var lengths = new float[points.Length];
            for (int i = 1; i < points.Length; i++) lengths[i] = lengths[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            return lengths;
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

            Vector3[] points = GetPath(from, to);
            previewLine.positionCount = points.Length;
            previewLine.SetPositions(points);
            previewLine.startColor = previewLine.endColor = RoutePreview;
        }

        // ------------------------------------------------------------------ her kare

        void LateUpdate()
        {
            if (roadMesh != null && Mathf.Abs(ZoomScale - roadScale) > roadScale * 0.12f)
            {
                RebuildRoadMesh(ZoomScale);
                ScaleCityMarkers();
                colorsDirty = true;
            }
            if (colorsDirty) RefreshColors();
            if (tokensDirty) RebuildTokens();
            AnimateTokens();
        }

        /// <summary>Yakınlaştıkça şehir işaretleri küçülür ki yollar ve otobüsler görünsün.</summary>
        void ScaleCityMarkers()
        {
            float scale = Mathf.Clamp(zoomDistance / ReferenceZoom, 0.6f, 1.4f);
            foreach (var marker in markers) marker.Root.localScale = Vector3.one * scale;
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

            float scale = ZoomScale;
            foreach (var visual in routeVisuals.Values)
            {
                bool selected = visual.Route.routeId == selectedRouteId;
                Color color = RouteColor(visual.Route);
                if (selected) color = Color.Lerp(color, Color.white, 0.35f);
                visual.Line.startColor = visual.Line.endColor = color;
                visual.Line.widthMultiplier = (selected ? 0.32f : 0.17f) * scale;
            }
            previewLine.widthMultiplier = 0.2f * scale;
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
                while (visual.Tokens.Count < wanted) visual.Tokens.Add(CreateBus());

                for (int i = 0; i < visual.Tokens.Count; i++)
                {
                    int tier = Mathf.Clamp(busBuffer[i].Model.Tier, 0, tierMaterials.Length - 1);
                    visual.Tokens[i].Body.sharedMaterial = tierMaterials[tier];
                    visual.Tokens[i].Phase = i / (float)Mathf.Max(1, visual.Tokens.Count) * 2f;
                }
            }
        }

        void AnimateTokens()
        {
            if (!session.IsRunning) return;
            int speed = session.Clock != null ? session.Clock.Speed : 0;
            animationTime += Time.unscaledDeltaTime * speed;
            float scale = Mathf.Clamp(zoomDistance / ReferenceZoom, 0.5f, 1.5f);
            float turn = Mathf.Clamp01(Time.unscaledDeltaTime * 10f);

            foreach (var visual in routeVisuals.Values)
            {
                foreach (var token in visual.Tokens)
                {
                    float cycle = Mathf.Repeat(animationTime / visual.TripSeconds + token.Phase, 2f);
                    bool returning = cycle > 1f;
                    Evaluate(visual, returning ? 2f - cycle : cycle, out Vector3 position, out Vector3 direction);
                    if (returning) direction = -direction;

                    // Gidiş ve dönüş otobüsleri yolun iki şeridinde; yolda hafifçe sallanır.
                    Vector3 side = Vector3.Cross(Vector3.up, direction).normalized * (0.07f * scale);
                    float bob = speed > 0 ? Mathf.Abs(Mathf.Sin(animationTime * 9f + token.Phase * 7f)) * 0.012f * scale : 0f;
                    token.Transform.position = position + side + Vector3.up * bob;
                    token.Transform.localScale = Vector3.one * scale;
                    if (direction.sqrMagnitude > 1e-8f)
                        token.Transform.rotation = Quaternion.Slerp(token.Transform.rotation, Quaternion.LookRotation(direction, Vector3.up), turn);
                }
            }
        }

        /// <summary>Güzergâh boyunca t (0..1) oranındaki nokta ve yön (yay uzunluğuna göre, sabit hız).</summary>
        static void Evaluate(RouteVisual visual, float t, out Vector3 position, out Vector3 direction)
        {
            Vector3[] points = visual.Points;
            float[] lengths = visual.Cumulative;
            float target = t * lengths[lengths.Length - 1];
            int high = Array.BinarySearch(lengths, target);
            if (high < 0) high = ~high;
            high = Mathf.Clamp(high, 1, points.Length - 1);
            int low = high - 1;
            float segment = lengths[high] - lengths[low];
            float f = segment > 1e-6f ? (target - lengths[low]) / segment : 0f;
            position = Vector3.Lerp(points[low], points[high], f);
            direction = points[high] - points[low];
            direction.y = 0f;
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
