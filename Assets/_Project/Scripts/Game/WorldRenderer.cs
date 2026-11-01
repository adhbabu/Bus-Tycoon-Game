using System;
using System.Collections.Generic;
using BusTycoon.Core;
using BusTycoon.Data;
using BusTycoon.Fleet;
using BusTycoon.Progression;
using BusTycoon.Routes;
using BusTycoon.Stations;
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
            /// <summary>Durak modelinin malzemeleri; çatı (vurgu) katmanı renge göre paylaşılan malzemedir.</summary>
            public MeshRenderer Renderer;
            public Material[] Materials;
            public int AccentIndex = -1;
            public Color AccentColor;
            public Transform Root;
            /// <summary>İşaretin tepesi (ölçeklenmeden önce); şehir adı bunun üstüne yazılır.</summary>
            public float Height;
            /// <summary>Merkez ofis bayrağı (yalnız merkezde görünür).</summary>
            public GameObject Flag;
            /// <summary>Üzerine gelince / seçilince hafifçe büyüme (1 = normal); yaylı: hızlanır, hafifçe taşar, oturur.</summary>
            public float Bump = 1f;
            public float BumpVelocity;
        }

        sealed class RouteVisual
        {
            public RouteState Route;
            public LineRenderer Line;
            public Vector3[] Points;
            public float[] Cumulative;
            public float TripSeconds;
            /// <summary>Yeni açılan hat kalkıştan varışa doğru çizilerek belirir (0..1; 1 = tam).</summary>
            public float Reveal = 1f;
            public readonly List<BusToken> Tokens = new List<BusToken>();
        }

        sealed class BusToken
        {
            public Transform Transform;
            public MeshFilter Filter;
            public Renderer Beam;
            public BusModelData Model;
            public float Phase;
        }

        /// <summary>Mini harita için hat çizgisi: dünya noktaları ve haritadaki rengi.</summary>
        public readonly struct RouteShape
        {
            public readonly Vector3[] Points;
            public readonly Color Color;
            public readonly bool Selected;

            public RouteShape(Vector3[] points, Color color, bool selected)
            {
                Points = points;
                Color = color;
                Selected = selected;
            }
        }

        /// <summary>Mini harita için hareket eden otobüs: konum, gidiş yönü ve modeli.</summary>
        public readonly struct BusMarker
        {
            public readonly Vector3 Position, Forward;
            public readonly BusModelData Model;

            public BusMarker(Vector3 position, Vector3 forward, BusModelData model)
            {
                Position = position;
                Forward = forward;
                Model = model;
            }
        }

        const int ArcSegments = 40;
        const float LineHeight = 0.12f;
        const float RoadHeight = 0.02f;
        const float RouteHeight = 0.05f;
        /// <summary>Yolların harita kenarında silikleştiği şerit (dünya birimi; harita dokusunun kenar şeridiyle aynı, %4).</summary>
        const float RoadEdgeFade = 4f;
        const int MaxTokensPerRoute = 6;
        /// <summary>Çizgi kalınlıkları ve otobüs boyu bu kamera uzaklığına göre ayarlanmıştır.</summary>
        const float ReferenceZoom = 40f;

        // Durak çatısının rengi: hatsız şehir kiremit; hatlı (yeşil), merkez (altın), seçili (mavi/turuncu) şehirler öne çıksın.
        static readonly Color CityNormal = new Color(0.8f, 0.45f, 0.36f);
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
        LineRenderer previewLine;
        Transform cityRoot, routeRoot;
        float animationTime;
        bool colorsDirty = true;
        bool tokensDirty = true;

        readonly List<(RoadNetwork.RoadClass Class, Vector3[] Points)> roadPieces = new List<(RoadNetwork.RoadClass, Vector3[])>();
        Mesh roadMesh;
        Material roadMaterial;

        // Gece: haritayı karartan örtü, şehir ışıkları, otobüs farları (DayNightCycle → SetDaylight)
        static readonly Color NightTint = new Color(0.02f, 0.04f, 0.14f);
        static readonly Color DuskTint = new Color(1f, 0.5f, 0.22f);
        Renderer nightOverlay, cityLights;
        Material nightMaterial, cityLightMaterial, beamMaterial;
        Mesh beamMesh;
        bool beamsOn;
        float roadScale = -1f;
        float zoomDistance = ReferenceZoom;
        float markerScale = 1f;
        readonly List<Vector3> roadVertices = new List<Vector3>();
        readonly List<Color> roadVertexColors = new List<Color>();
        readonly List<Vector2> roadOffsets = new List<Vector2>();
        readonly List<int> roadIndices = new List<int>();
        /// <summary>Kalınlık gölgelendiricide (BusTycoon/MapRoads): ağ bir kez örülür. Gölgelendirici yoksa eski yol.</summary>
        bool roadWidthInShader;
        static readonly int WidthScaleId = Shader.PropertyToID("_WidthScale");

        // Harita yüzeyi (BusTycoon/MapSurface) ve atmosfer: gün ışığı efektlere, pus (sis) kameraya göre ayarlanır
        static readonly int DaylightId = Shader.PropertyToID("_BT_Daylight");
        static readonly Color DayHaze = new Color(0.55f, 0.66f, 0.76f);
        Color hazeColor = DayHaze;
        Color backgroundColor;

        RouteSystem boundRoutes;
        FleetSystem boundFleet;
        ProgressionSystem boundProgression;

        CityData hovered, origin, destination, headquarters;
        string selectedRouteId;

        public IReadOnlyList<CityMarker> Markers => markers;
        /// <summary>Harita dokusu (mini harita da çizer).</summary>
        public Texture2D MapTexture { get; private set; }
        /// <summary>Harita yüzeyinin malzemesi (MapSurface); yakın plan döşemeleri bunu kopyalar. Yedek malzemede null.</summary>
        public Material MapSurfaceMaterial { get; private set; }
        /// <summary>Hat çizgileri ya da renkleri değişince artar (mini harita yeniden çizer).</summary>
        public int RoutesVersion { get; private set; }
        int routeSignature;
        public Rect MapBoundsXZ { get; private set; }
        public Color SeaColor => new Color(0.05f, 0.15f, 0.26f);

        public CityData Hovered { get => hovered; set { if (hovered != value) { hovered = value; colorsDirty = true; } } }
        public CityData Origin { get => origin; set { if (origin != value) { origin = value; colorsDirty = true; } } }
        public CityData Destination { get => destination; set { if (destination != value) { destination = value; colorsDirty = true; } } }
        public CityData Headquarters { get => headquarters; set { if (headquarters != value) { headquarters = value; colorsDirty = true; } } }
        public string SelectedRouteId { get => selectedRouteId; set { if (selectedRouteId != value) { selectedRouteId = value; colorsDirty = true; } } }

        /// <summary>Kamera uzaklığı: yol ve hat kalınlıkları, otobüs ve şehir boyları buna göre ölçeklenir.</summary>
        public float ZoomDistance { set => zoomDistance = value; }

        float ZoomScale => Mathf.Clamp(zoomDistance / ReferenceZoom, 0.45f, 2.4f);

        public void Init(GameSession gameSession, MapProjection mapProjection, Texture2D mapTexture, Texture2D outerTexture,
            RoadNetwork roadNetwork, Texture2D waterMask = null)
        {
            session = gameSession;
            projection = mapProjection;
            roads = roadNetwork;
            MapTexture = mapTexture;
            cityRoot = new GameObject("Cities").transform;
            cityRoot.SetParent(transform, false);
            routeRoot = new GameObject("Routes").transform;
            routeRoot.SetParent(transform, false);

            Shader.SetGlobalFloat(DaylightId, 1f);
            backgroundColor = Color.Lerp(SeaColor, DayHaze, 0.12f);
            BuildMapSurface(mapTexture, waterMask);
            BuildSurroundings(outerTexture);
            BuildRoadLayer();
            BuildCityMarkers();
            BuildNightLayers();


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

        void BuildMapSurface(Texture2D texture, Texture2D waterMask)
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
                ? CreateMapMaterial(texture, waterMask, true)
                : MaterialFactory.CreateLit(new Color(0.35f, 0.45f, 0.32f), 0f);
            if (renderer.sharedMaterial.shader != null && renderer.sharedMaterial.shader.name == "BusTycoon/MapSurface")
                MapSurfaceMaterial = renderer.sharedMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true; // duraklar ve otobüsler haritaya gölge düşürür
        }

        /// <summary>
        /// Harita malzemesi: BusTycoon/MapSurface (canlı deniz, yakın arazi dokusu, bulut gölgesi, gölge, pus).
        /// Gölgelendirici yoksa ya da cihaz desteklemiyorsa düz ışıksız dokuya düşer. Çevre haritasında deniz ve arazi
        /// efektleri kapalıdır (bulut, gölge ve pus sürer).
        /// </summary>
        Material CreateMapMaterial(Texture2D texture, Texture2D waterMask, bool effects)
        {
            Shader shader = Resources.Load<Shader>(DefaultContent.MapSurfaceShaderResourcePath);
            if (shader == null) shader = Shader.Find("BusTycoon/MapSurface");
            if (shader == null || !shader.isSupported) return MaterialFactory.CreateUnlitTexture(texture);

            var material = new Material(shader) { mainTexture = texture };
            material.SetTexture("_WaterTex", waterMask != null ? waterMask : Texture2D.blackTexture);
            material.SetTexture("_NoiseTex", MapTextures.Noise);
            material.SetFloat("_Effects", effects && waterMask != null ? 1f : 0f);
            Rect map = MapBoundsXZ; // deniz maskesi ve kenar sönmesi dünya konumundan (döşemelerde de aynı)
            material.SetVector("_MapRect", new Vector4(map.xMin, map.yMin, map.width, map.height));
            return material;
        }

        /// <summary>
        /// Haritanın çevresi: haritanın biraz altında, daha geniş bir düzlemde bulanık "devam" dokusu. Kamera haritanın
        /// dışını gördüğünde düz renk yerine bulanıklaşarak denize karışan coğrafya görünür.
        /// </summary>
        void BuildSurroundings(Texture2D texture)
        {
            if (texture == null) return;
            Vector3 southWest = projection.GeoToWorld(new GeoCoordinate(DefaultContent.OuterMinLatitude, DefaultContent.OuterMinLongitude));
            Vector3 northEast = projection.GeoToWorld(new GeoCoordinate(DefaultContent.OuterMaxLatitude, DefaultContent.OuterMaxLongitude));
            const float y = -0.05f; // haritanın altında: çakışan alanda harita görünür

            var mesh = new Mesh { name = "Surroundings" };
            mesh.vertices = new[]
            {
                new Vector3(southWest.x, y, southWest.z), new Vector3(northEast.x, y, southWest.z),
                new Vector3(northEast.x, y, northEast.z), new Vector3(southWest.x, y, northEast.z)
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.triangles = new[] { 0, 3, 2, 0, 2, 1 };
            mesh.RecalculateNormals();
            if (mesh.normals[0].y < 0f)
            {
                mesh.triangles = new[] { 0, 2, 3, 0, 1, 2 };
                mesh.RecalculateNormals();
            }
            mesh.RecalculateBounds();

            var surface = new GameObject("Surroundings");
            surface.transform.SetParent(transform, false);
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = surface.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateMapMaterial(texture, null, false);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>
        /// Gece katmanları (köşe renkli, ışıksız malzeme): haritayı ve çevresini karartan örtü (yollar, şehirler ve
        /// otobüsler örtünün üstünde kalır), nüfusa göre büyüyen şehir ışıkları ve otobüs farlarının yere düşen ışığı.
        /// </summary>
        void BuildNightLayers()
        {
            Vector3 southWest = projection.GeoToWorld(new GeoCoordinate(DefaultContent.OuterMinLatitude, DefaultContent.OuterMinLongitude));
            Vector3 northEast = projection.GeoToWorld(new GeoCoordinate(DefaultContent.OuterMaxLatitude, DefaultContent.OuterMaxLongitude));
            var overlay = new Mesh { name = "NightOverlay" };
            overlay.vertices = new[]
            {
                new Vector3(southWest.x, 0.004f, southWest.z), new Vector3(northEast.x, 0.004f, southWest.z),
                new Vector3(southWest.x, 0.004f, northEast.z), new Vector3(northEast.x, 0.004f, northEast.z)
            };
            overlay.triangles = new[] { 0, 2, 3, 0, 3, 1 };
            overlay.RecalculateBounds();
            nightMaterial = new Material(MaterialFactory.LineMaterial) { renderQueue = 2945 }; // haritadan sonra, yollardan önce
            nightOverlay = CreateLayer("NightOverlay", overlay, nightMaterial);

            // Şehir ışıkları: nüfusla büyüyen yumuşak ışık havuzları
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            foreach (var marker in markers)
            {
                float magnitude = Mathf.Log10(Mathf.Max(1000f, marker.City.Population)) - 4.8f;
                float half = Mathf.Clamp(0.9f + 0.75f * magnitude, 0.8f, 3f) * 0.75f; // durak modelinin çevresine taşsın
                var color = new Color(1f, 1f, 1f, Mathf.Clamp(0.55f + 0.18f * magnitude, 0.45f, 1f));
                AppendFlatQuad(vertices, colors, uvs, triangles, marker.Position + Vector3.up * 0.006f, half, half, color);
            }
            var lights = new Mesh { name = "CityLights" };
            lights.SetVertices(vertices);
            lights.SetColors(colors);
            lights.SetUVs(0, uvs);
            lights.SetTriangles(triangles, 0);
            lights.RecalculateBounds();
            cityLightMaterial = new Material(MaterialFactory.LineMaterial) { renderQueue = 2955, mainTexture = GlowTexture() };
            cityLights = CreateLayer("CityLights", lights, cityLightMaterial);

            // Far ışığı: otobüsün önünden ileri doğru genişleyen yumuşak koni (otobüsün yerel uzayında, +z ileri)
            beamMesh = new Mesh { name = "Headlights" };
            beamMesh.vertices = new[] { new Vector3(-0.1f, 0f, 0f), new Vector3(0.1f, 0f, 0f), new Vector3(-0.3f, 0f, 0.85f), new Vector3(0.3f, 0f, 0.85f) };
            beamMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            beamMesh.triangles = new[] { 0, 2, 3, 0, 3, 1 };
            beamMesh.RecalculateBounds();
            beamMaterial = new Material(MaterialFactory.LineMaterial) { renderQueue = 2960, mainTexture = BeamTexture(), enableInstancing = true };
        }

        Renderer CreateLayer(string layerName, Mesh mesh, Material material)
        {
            var go = new GameObject(layerName);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            return renderer;
        }

        static void AppendFlatQuad(List<Vector3> vertices, List<Color> colors, List<Vector2> uvs, List<int> triangles, Vector3 center, float halfX, float halfZ, Color color)
        {
            int start = vertices.Count;
            vertices.Add(center + new Vector3(-halfX, 0f, -halfZ));
            vertices.Add(center + new Vector3(halfX, 0f, -halfZ));
            vertices.Add(center + new Vector3(-halfX, 0f, halfZ));
            vertices.Add(center + new Vector3(halfX, 0f, halfZ));
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));
            for (int i = 0; i < 4; i++) colors.Add(color);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
            triangles.Add(start);
            triangles.Add(start + 3);
            triangles.Add(start + 1);
        }

        /// <summary>Sıcak, ortası parlak yuvarlak ışık dokusu.</summary>
        internal static Texture2D GlowTexture()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                float a = (1f - r) * (1f - r);
                pixels[y * size + x] = new Color(1f, Mathf.Lerp(0.72f, 0.95f, a), Mathf.Lerp(0.35f, 0.75f, a), a);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>Far ışığı dokusu: v boyunca (ileri) sönen, kenarlara doğru yumuşayan koni.</summary>
        static Texture2D BeamTexture()
        {
            const int width = 32, height = 64;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float v = (y + 0.5f) / height, across = Mathf.Abs((x + 0.5f) / width * 2f - 1f);
                float a = Mathf.Pow(1f - v, 1.6f) * (1f - across * across) * 0.9f;
                pixels[y * width + x] = new Color(1f, 0.95f, 0.72f, a);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Gün ışığı (DayNightCycle): <paramref name="night"/> 0 gündüz … 1 gece yarısı; <paramref name="dusk"/> gün
        /// doğumu/batımında 1 (turuncu ton). Gece harita kararır, yollar biraz söner, şehir ışıkları ve farlar yanar.
        /// </summary>
        public void SetDaylight(float night, float dusk)
        {
            if (nightOverlay == null) return;
            Color tint = Color.Lerp(DuskTint, NightTint, Mathf.Clamp01(night * 1.6f));
            tint.a = Mathf.Max(night * 0.5f, dusk * 0.14f);
            nightMaterial.color = tint;
            nightOverlay.enabled = tint.a > 0.005f;
            // Pus (sis) ve çevre dokusunun ötesi (kameranın arka planı) örtüyle aynı oranda kararır: gece sınır görünmez.
            var tintColor = new Color(tint.r, tint.g, tint.b);
            hazeColor = Color.Lerp(DayHaze, tintColor, Mathf.Clamp01(tint.a * 1.6f));
            backgroundColor = Color.Lerp(Color.Lerp(SeaColor, DayHaze, 0.12f), tintColor, tint.a);
            Shader.SetGlobalFloat(DaylightId, 1f - night);
            if (roadMaterial != null) roadMaterial.color = Color.Lerp(Color.white, new Color(0.82f, 0.82f, 0.95f), night * 0.7f);
            StationMaterials.SetNight(night);

            float lights = Mathf.Clamp01((night - 0.15f) / 0.6f);
            cityLightMaterial.color = new Color(1f, 1f, 1f, lights * 0.9f);
            cityLights.enabled = lights > 0.005f;
            beamMaterial.color = new Color(1f, 1f, 1f, lights);
            bool on = lights > 0.005f;
            if (on == beamsOn) return;
            beamsOn = on;
            foreach (var visual in routeVisuals.Values)
                foreach (var token in visual.Tokens)
                    token.Beam.enabled = on;
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
            // Köşe renkli, ışıksız; hat çizgilerinden önce çizilir. Kalınlık gölgelendiricide ölçeklenir.
            Shader shader = Resources.Load<Shader>(DefaultContent.MapRoadsShaderResourcePath);
            if (shader == null) shader = Shader.Find("BusTycoon/MapRoads");
            roadWidthInShader = shader != null && shader.isSupported;
            roadMaterial = roadWidthInShader ? new Material(shader) { renderQueue = 2950 } : new Material(MaterialFactory.LineMaterial) { renderQueue = 2950 };
            renderer.sharedMaterial = roadMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            RebuildRoadMesh(ZoomScale);
        }

        void RebuildRoadMesh(float scale)
        {
            roadScale = scale;
            if (roadWidthInShader)
            {
                roadMaterial.SetFloat(WidthScaleId, scale);
                if (roadMesh.vertexCount > 0) return; // ağ hazır: yalnız kalınlık değişir
                scale = 1f;
            }
            roadVertices.Clear();
            roadVertexColors.Clear();
            roadOffsets.Clear();
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
            if (roadWidthInShader) roadMesh.SetUVs(0, roadOffsets);
            roadMesh.SetTriangles(roadIndices, 0);
            roadMesh.RecalculateBounds();
            if (roadWidthInShader)
            {
                // Merkez çizgiden hesaplanan sınırlar kalın yolları kesmesin
                Bounds bounds = roadMesh.bounds;
                bounds.Expand(1f);
                roadMesh.bounds = bounds;
            }
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

        /// <summary>
        /// Harita kenarına yaklaştıkça yollar silikleşir: harita dokusu da kenar şeridinde çevre haritasına (ve onun
        /// bulanık yollarına) karışır.
        /// </summary>
        float EdgeFade(Vector3 point)
        {
            Rect bounds = MapBoundsXZ;
            float distance = Mathf.Min(Mathf.Min(point.x - bounds.xMin, bounds.xMax - point.x), Mathf.Min(point.z - bounds.yMin, bounds.yMax - point.z));
            float t = Mathf.Clamp01(distance / RoadEdgeFade);
            return t * t * (3f - 2f * t);
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
                if (roadWidthInShader)
                {
                    // Köşe merkezde; kenara uzaklık gölgelendiricide kalınlık ölçeğiyle uygulanır
                    roadVertices.Add(points[i]);
                    roadVertices.Add(points[i]);
                    roadOffsets.Add(new Vector2(offset.x, offset.z));
                    roadOffsets.Add(new Vector2(-offset.x, -offset.z));
                }
                else
                {
                    roadVertices.Add(points[i] + offset);
                    roadVertices.Add(points[i] - offset);
                }
                Color faded = color;
                faded.a *= EdgeFade(points[i]);
                roadVertexColors.Add(faded);
                roadVertexColors.Add(faded);

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
        /// Şehir işareti: şehrin büyüklüğüne göre küçük durak, otogar, büyük otogar ya da metropol terminali
        /// (<see cref="StationModels"/>). Çatı rengi durumu gösterir (bkz. RefreshColors); merkezde şirket bayrağı dalgalanır.
        /// </summary>
        void BuildCityMarkers()
        {
            Mesh flagMesh = StationModels.FlagMesh(out MeshLayer[] flagLayers);
            foreach (var city in session.World.Cities)
            {
                if (city == null) continue;
                Vector3 position = projection.GeoToWorld(city.Coordinate);
                StationTier tier = StationCatalog.TierOf(city);

                var root = new GameObject("City_" + city.Id).transform;
                root.SetParent(cityRoot, false);
                root.position = position;

                // Çatı rengi şehre özel malzeme yerine renge göre paylaşılan malzemeyle: aynı kademedeki duraklar
                // GPU'da tek çizimde örneklenir (ör. 90 şehirde yüzlerce çizim çağrısı yerine birkaç tane)
                var station = new GameObject("Station");
                station.transform.SetParent(root, false);
                station.AddComponent<MeshFilter>().sharedMesh = StationModels.MeshFor(tier, out MeshLayer[] layers);
                Material[] materials = StationMaterials.For(layers, AccentMaterial(CityNormal));
                var renderer = station.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials;

                var flag = new GameObject("Flag");
                flag.transform.SetParent(root, false);
                flag.transform.localPosition = StationModels.FlagPosition(tier);
                flag.AddComponent<MeshFilter>().sharedMesh = flagMesh;
                flag.AddComponent<MeshRenderer>().sharedMaterials = StationMaterials.For(flagLayers, null);
                flag.SetActive(false);

                var marker = new CityMarker
                {
                    City = city, Position = position, Root = root, Height = StationModels.Height(tier), Flag = flag,
                    Renderer = renderer, Materials = materials, AccentIndex = Array.IndexOf(layers, MeshLayer.Accent), AccentColor = CityNormal
                };
                markers.Add(marker);
                markerByCity[city] = marker;
            }
        }

        static readonly Dictionary<Color, Material> accentMaterials = new Dictionary<Color, Material>();

        /// <summary>Durak çatısı malzemesi: aynı renkteki bütün duraklar aynı malzemeyi paylaşır (örneklemeli çizim).</summary>
        static Material AccentMaterial(Color color)
        {
            if (accentMaterials.TryGetValue(color, out Material material) && material != null) return material;
            material = MaterialFactory.CreateLit(color, 0.35f);
            material.name = "StationAccent";
            accentMaterials[color] = material;
            return material;
        }

        /// <summary>Haritadaki otobüs: modeline göre tek parça 3B model (<see cref="BusModels"/>).</summary>
        BusToken CreateBus(BusModelData model)
        {
            var go = new GameObject("Bus");
            go.transform.SetParent(routeRoot, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = BusModels.MeshFor(model);
            go.AddComponent<MeshRenderer>().sharedMaterial = BusModels.Material;

            // Farların yere düşen ışığı (gece görünür)
            var beam = new GameObject("Headlights");
            beam.transform.SetParent(go.transform, false);
            beam.transform.localPosition = new Vector3(0f, 0.004f, BusModels.Length(model) / 2f - 0.02f);
            beam.AddComponent<MeshFilter>().sharedMesh = beamMesh;
            var beamRenderer = beam.AddComponent<MeshRenderer>();
            beamRenderer.sharedMaterial = beamMaterial;
            beamRenderer.shadowCastingMode = ShadowCastingMode.Off;
            beamRenderer.receiveShadows = false;
            beamRenderer.enabled = beamsOn;
            return new BusToken { Transform = go.transform, Filter = filter, Model = model, Beam = beamRenderer };
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

        readonly HashSet<string> previousRouteIds = new HashSet<string>();
        readonly List<Vector3> revealBuffer = new List<Vector3>();
        const float RevealSeconds = 0.9f;

        void RebuildRoutes()
        {
            previousRouteIds.Clear();
            foreach (var visual in routeVisuals.Values)
            {
                previousRouteIds.Add(visual.Route.routeId);
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
                    if (!previousRouteIds.Contains(route.routeId))
                    {
                        // Yeni hat: çizilerek belirir, uçlarındaki duraklar yaylanır
                        visual.Reveal = 0f;
                        SetRevealedLine(visual, 0f);
                        Pop(a);
                        Pop(b);
                    }
                    routeVisuals[route.routeId] = visual;
                }
            }

            colorsDirty = true;
            tokensDirty = true;
            RoutesVersion++;
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
            // Kalınlıklar her kare yakınlaştırmayla birlikte akıcı değişir (eskiden %12'lik basamaklarla)
            float scale = ZoomScale;
            markerScale = Mathf.Clamp(zoomDistance / 32f, 0.7f, 2.1f); // uzaktan da duraklar seçilsin
            if (roadMesh != null)
            {
                if (roadWidthInShader) roadMaterial.SetFloat(WidthScaleId, scale);
                else if (Mathf.Abs(scale - roadScale) > roadScale * 0.12f) RebuildRoadMesh(scale);
            }
            UpdateLineWidths(scale);
            ScaleCityMarkers();
            if (colorsDirty) RefreshColors();
            if (tokensDirty) RebuildTokens();
            AnimateReveals();
            AnimateTokens();
            UpdateAtmosphere();
        }

        /// <summary>
        /// Ufuk pusu: kameranın uzaklığına göre doğrusal sis (ekranın üstü hafifçe puslanır, derinlik hissi). Yalnız
        /// harita görünürken (durak içinde kendi kamerası çizer) ve Düşük kalite dışında. Gölge mesafesi de uzaklıkla
        /// büyür: uzaktan bakınca da gölgeler görünür.
        /// </summary>
        void UpdateAtmosphere()
        {
            Camera main = Camera.main; // durak içindeyken ana kamera kapalı → null
            bool mapVisible = main != null && main.enabled;
            bool fog = mapVisible && GraphicsQuality.Atmosphere;
            if (RenderSettings.fog != fog) RenderSettings.fog = fog;
            if (!mapVisible) return;

            main.backgroundColor = backgroundColor;
            if (fog)
            {
                RenderSettings.fogColor = hazeColor;
                RenderSettings.fogStartDistance = zoomDistance * 0.9f;
                RenderSettings.fogEndDistance = zoomDistance * 5.5f;
            }
            QualitySettings.shadowDistance = Mathf.Clamp(zoomDistance * 2.4f, 24f, 170f);
        }

        /// <summary>Durağı yaylanarak büyüt (yeni hat bağlandı).</summary>
        public void Pop(CityData city)
        {
            if (city != null && markerByCity.TryGetValue(city, out CityMarker marker)) marker.BumpVelocity += 7f;
        }

        void AnimateReveals()
        {
            float step = Time.unscaledDeltaTime / RevealSeconds;
            foreach (var visual in routeVisuals.Values)
            {
                if (visual.Reveal >= 1f) continue;
                visual.Reveal = Mathf.Min(1f, visual.Reveal + step);
                float eased = 1f - (1f - visual.Reveal) * (1f - visual.Reveal) * (1f - visual.Reveal); // hızlı başlar, yavaşlayarak biter
                SetRevealedLine(visual, eased);
            }
        }

        /// <summary>Çizginin baştan <paramref name="fraction"/> kadarını göster (son nokta ara noktaya kesilir).</summary>
        void SetRevealedLine(RouteVisual visual, float fraction)
        {
            Vector3[] points = visual.Points;
            if (fraction >= 1f)
            {
                visual.Line.positionCount = points.Length;
                visual.Line.SetPositions(points);
                return;
            }
            float[] lengths = visual.Cumulative;
            float goal = Mathf.Max(fraction, 0.001f) * lengths[lengths.Length - 1];
            revealBuffer.Clear();
            revealBuffer.Add(points[0]);
            for (int i = 1; i < points.Length; i++)
            {
                if (lengths[i] < goal)
                {
                    revealBuffer.Add(points[i]);
                    continue;
                }
                float segment = lengths[i] - lengths[i - 1];
                revealBuffer.Add(Vector3.Lerp(points[i - 1], points[i], segment > 1e-6f ? (goal - lengths[i - 1]) / segment : 1f));
                break;
            }
            visual.Line.positionCount = revealBuffer.Count;
            for (int i = 0; i < revealBuffer.Count; i++) visual.Line.SetPosition(i, revealBuffer[i]);
        }

        /// <summary>
        /// Yakınlaştıkça şehir işaretleri küçülür ki yollar ve otobüsler görünsün. Üzerine gelinen ya da planlayıcıda
        /// seçilen durak hafifçe büyür (yumuşak geçiş).
        /// </summary>
        void ScaleCityMarkers()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            foreach (var marker in markers)
            {
                CityData city = marker.City;
                float target = city == hovered || city == origin || city == destination ? 1.18f : 1f;
                // Az sönümlü yay: seçilince hafifçe taşıp oturur
                marker.BumpVelocity += ((target - marker.Bump) * 220f - marker.BumpVelocity * 15f) * dt;
                marker.Bump = Mathf.Clamp(marker.Bump + marker.BumpVelocity * dt, 0.6f, 1.8f);
                marker.Root.localScale = Vector3.one * (markerScale * marker.Bump);
            }
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
                if (marker.AccentIndex >= 0 && !marker.AccentColor.Equals(color))
                {
                    marker.AccentColor = color;
                    marker.Materials[marker.AccentIndex] = AccentMaterial(color);
                    marker.Renderer.sharedMaterials = marker.Materials;
                }
                bool flag = city == headquarters;
                if (marker.Flag.activeSelf != flag) marker.Flag.SetActive(flag);
            }

            int signature = routeVisuals.Count;
            foreach (var visual in routeVisuals.Values)
            {
                bool selected = visual.Route.routeId == selectedRouteId;
                Color color = RouteColor(visual.Route);
                signature = signature * 31 + visual.Route.routeId.GetHashCode() + (selected ? 7 : 0) + Mathf.RoundToInt(color.r * 9 + color.g * 90 + color.b * 900);
                if (selected) color = Color.Lerp(color, Color.white, 0.35f);
                visual.Line.startColor = visual.Line.endColor = color;
            }
            if (signature != routeSignature)
            {
                routeSignature = signature;
                RoutesVersion++; // mini harita yalnız hat renkleri/seçimi değişince yeniden çizer
            }
        }

        void UpdateLineWidths(float scale)
        {
            foreach (var visual in routeVisuals.Values)
                visual.Line.widthMultiplier = (visual.Route.routeId == selectedRouteId ? 0.32f : 0.17f) * scale;
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
                while (visual.Tokens.Count < wanted) visual.Tokens.Add(CreateBus(busBuffer[visual.Tokens.Count].Model));

                for (int i = 0; i < visual.Tokens.Count; i++)
                {
                    BusModelData model = busBuffer[i].Model;
                    if (visual.Tokens[i].Model != model)
                    {
                        visual.Tokens[i].Model = model;
                        visual.Tokens[i].Filter.sharedMesh = BusModels.MeshFor(model);
                        visual.Tokens[i].Beam.transform.localPosition = new Vector3(0f, 0.004f, BusModels.Length(model) / 2f - 0.02f);
                    }
                    visual.Tokens[i].Phase = i / (float)Mathf.Max(1, visual.Tokens.Count) * 2f;
                }
            }
        }

        void AnimateTokens()
        {
            if (!session.IsRunning) return;
            int speed = session.Clock != null ? session.Clock.Speed : 0;
            animationTime += Time.unscaledDeltaTime * speed;
            // Uzaktan bakınca otobüsler büyür ki hatlarda gidişleri seçilsin.
            float scale = Mathf.Clamp(zoomDistance / 36f, 0.55f, 1.9f);
            float turn = Mathf.Clamp01(Time.unscaledDeltaTime * 10f);

            foreach (var visual in routeVisuals.Values)
            {
                foreach (var token in visual.Tokens)
                {
                    float cycle = Mathf.Repeat(animationTime / visual.TripSeconds + token.Phase, 2f);
                    bool returning = cycle > 1f;
                    Evaluate(visual, LegProgress(returning ? cycle - 1f : cycle, returning), out Vector3 position, out Vector3 direction);
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

        /// <summary>
        /// Bir seferin zamanı (0..1) → güzergâhtaki konum (0..1): uçlarda kısa bekleme, duraktan yavaşça çıkar ve
        /// yavaşlayarak girer (sabit hızlı kayma yerine). Dönüşte konum tersten okunur.
        /// </summary>
        static float LegProgress(float legTime, bool returning)
        {
            const float dwell = 0.08f, ease = 0.55f;
            float t = Mathf.Clamp01((legTime - dwell * 0.5f) / (1f - dwell));
            t -= ease * Mathf.Sin(2f * Mathf.PI * t) / (2f * Mathf.PI); // uçlarda hız ortalamanın %45'i
            return returning ? 1f - t : t;
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

        // ------------------------------------------------------------------ mini harita

        public void GetRouteShapes(List<RouteShape> results)
        {
            results.Clear();
            foreach (var visual in routeVisuals.Values)
                results.Add(new RouteShape(visual.Points, RouteColor(visual.Route), visual.Route.routeId == selectedRouteId));
        }

        public void GetBusMarkers(List<BusMarker> results)
        {
            results.Clear();
            foreach (var visual in routeVisuals.Values)
                foreach (var token in visual.Tokens)
                    results.Add(new BusMarker(token.Transform.position, token.Transform.forward, token.Model));
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
