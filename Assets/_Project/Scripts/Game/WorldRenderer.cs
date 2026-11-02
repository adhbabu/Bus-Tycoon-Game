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
    /// Haritanın tamamını prefab gerektirmeden kurar ve canlı tutar (sade, düz harita dili; Mini Metro / Mini Motorways
    /// gibi): harita zemini, soluk karayolları, şehir rozetleri, her hatta ayrı renkli kalın hat çizgileri ve hatlarda
    /// gidip gelen otobüs kapsülleri (kozmetik). Rozetler ve otobüsler <see cref="MapSprites"/> ile tek çizimde.
    /// Hatlar ve otobüsler <see cref="RoadNetwork"/> varsa gerçek yol güzergâhını izler, yoksa yay çizer.
    /// Seçim/vurgu durumunu UI belirler (Origin, Destination, Hovered, SelectedRouteId, ZoomDistance ...).
    /// </summary>
    public sealed class WorldRenderer : MonoBehaviour
    {
        public sealed class CityMarker
        {
            public CityData City;
            public Vector3 Position;
            /// <summary>Rozetin yarıçapı (ölçeklenmeden önce; nüfus kademesine göre).</summary>
            public float Radius;
            public Color Fill = Color.white, Outline = Color.black;
            public float Ring = 0.7f;
            /// <summary>Üzerine gelince / seçilince hafifçe büyüme (1 = normal); yaylı: hızlanır, hafifçe taşar, oturur.</summary>
            public float Bump = 1f;
            public float BumpVelocity;
            /// <summary>Son yazılan ölçek: durgun rozet her kare yeniden yazılmaz.</summary>
            public float AppliedScale = -1f;
            /// <summary>Ekrandaki yarıçap (dünya birimi; yakınlaştırma ve büyüme dahil).</summary>
            public float WorldRadius => Radius * (AppliedScale > 0f ? AppliedScale : 1f);
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

        /// <summary>Hattaki otobüs: düz kapsül (MapSprites); nesne yok, her kare konumu hesaplanır.</summary>
        sealed class BusToken
        {
            public Vector3 Position, Forward = Vector3.forward;
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

        // Şehir rozetleri (metro haritası istasyonu gibi): beyaz dolgu, koyu kenar. Hatlı şehrin kenarı kalın; merkez
        // altın, seçili kalkış mavi, varış turuncu; kilitli bölge soluk gri.
        static readonly Color BadgeFill = Color.white;
        static readonly Color BadgeEdge = new Color(0.27f, 0.29f, 0.33f);
        static readonly Color BadgeEdgeConnected = new Color(0.11f, 0.12f, 0.15f);
        static readonly Color BadgeLockedFill = new Color(0.9f, 0.9f, 0.9f, 0.8f);
        static readonly Color BadgeLockedEdge = new Color(0.62f, 0.63f, 0.65f, 0.8f);
        static readonly Color CityHeadquarters = new Color(1f, 0.76f, 0.2f);
        static readonly Color CityOrigin = new Color(0.2f, 0.5f, 0.95f);
        static readonly Color CityDestination = new Color(0.98f, 0.5f, 0.12f);
        static readonly Color BadgeHover = new Color(0.82f, 0.9f, 1f);
        /// <summary>Kenar çizgisinin başladığı oran (0..1): küçük değer kalın kenar.</summary>
        const float BadgeRing = 0.66f, BadgeRingConnected = 0.52f;
        /// <summary>Rozetin yarıçapı (dünya birimi, referans yakınlıkta) nüfus kademesine göre: metropol … küçük şehir.</summary>
        static readonly float[] BadgeRadii = { 0.44f, 0.36f, 0.29f, 0.23f };
        const float BadgeLift = 0.1f;

        /// <summary>Hat renkleri (Mini Metro paleti gibi): her hat kendi rengini taşır; kârlılık arayüzde gösterilir.</summary>
        static readonly Color[] RoutePalette =
        {
            Hex(0xE63946), Hex(0x2A9D8F), Hex(0xF4A261), Hex(0x457B9D), Hex(0x8E44AD),
            Hex(0xE9C46A), Hex(0x27AE60), Hex(0xD35400), Hex(0x16A2B8), Hex(0x34495E)
        };
        static readonly Color RoutePreview = new Color(0.2f, 0.45f, 0.85f);
        // Karayolu çizimi (RoadNetwork.RoadClass sırası): otoyol, ana yol, diğer yol, feribot. Açık zeminde ince, beyaz;
        // bağlam içindir, gözü hatlardan çalmasın.
        static readonly float[] RoadWidths = { 0.1f, 0.07f, 0.04f, 0.045f };
        static readonly Color[] RoadColors =
        {
            new Color(1f, 0.95f, 0.82f, 0.95f), new Color(1f, 1f, 1f, 0.75f),
            new Color(1f, 1f, 1f, 0.38f), new Color(0.32f, 0.55f, 0.75f, 0.45f)
        };
        static readonly Color MotorwayCasing = new Color(0.55f, 0.5f, 0.42f, 0.35f);

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

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
        Transform routeRoot;
        MapSprites cityBadges, busSprites;
        float animationTime;
        bool colorsDirty = true;
        bool tokensDirty = true;
        bool badgesDirty = true;
        /// <summary>Hattın renk sırası (açılış sırasıyla; kapanan hattın rengi boşa çıkar).</summary>
        readonly Dictionary<string, int> routeColorIndex = new Dictionary<string, int>();
        readonly int[] paletteUse = new int[10];

        readonly List<(RoadNetwork.RoadClass Class, Vector3[] Points)> roadPieces = new List<(RoadNetwork.RoadClass, Vector3[])>();
        Mesh roadMesh;
        Material roadMaterial;
        MeshRenderer roadRenderer;
        Color roadNightTint = Color.white;

        // Gece: haritayı karartan örtü (DayNightCycle → SetDaylight). Rozetler, hatlar ve otobüsler örtünün üstünde
        // kalır: gece koyu temalı metro haritası gibi görünür.
        static readonly Color NightTint = new Color(0.04f, 0.07f, 0.16f);
        static readonly Color DuskTint = new Color(1f, 0.55f, 0.3f);
        Renderer nightOverlay;
        Material nightMaterial;
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

        // Harita yüzeyi (BusTycoon/MapSurface): gün ışığı gölgelendiriciye genel değişkenle verilir
        static readonly int DaylightId = Shader.PropertyToID("_BT_Daylight");
        /// <summary>Düz harita dokusundaki deniz rengi (Tools/build_map.py FLAT_SEA); haritanın ötesi de bu renk.</summary>
        static readonly Color FlatSea = new Color(150f / 255f, 202f / 255f, 222f / 255f);
        Color backgroundColor = FlatSea;

        RouteSystem boundRoutes;
        FleetSystem boundFleet;
        ProgressionSystem boundProgression;

        CityData hovered, origin, destination, headquarters;
        string selectedRouteId;

        public IReadOnlyList<CityMarker> Markers => markers;
        /// <summary>Harita dokusu (mini harita da çizer). Açılıştan sonra iner; gelene kadar null.</summary>
        public Texture2D MapTexture { get; private set; }
        /// <summary>Harita yüzeyinin malzemesi (MapSurface); yakın plan döşemeleri bunu kopyalar. Yedek malzemede null.</summary>
        public Material MapSurfaceMaterial { get; private set; }
        /// <summary>Hat çizgileri ya da renkleri değişince artar (mini harita yeniden çizer).</summary>
        public int RoutesVersion { get; private set; }
        int routeSignature;
        public Rect MapBoundsXZ { get; private set; }
        public Color SeaColor => FlatSea;

        public CityData Hovered { get => hovered; set { if (hovered != value) { hovered = value; colorsDirty = true; } } }
        public CityData Origin { get => origin; set { if (origin != value) { origin = value; colorsDirty = true; } } }
        public CityData Destination { get => destination; set { if (destination != value) { destination = value; colorsDirty = true; } } }
        public CityData Headquarters { get => headquarters; set { if (headquarters != value) { headquarters = value; colorsDirty = true; } } }
        public string SelectedRouteId { get => selectedRouteId; set { if (selectedRouteId != value) { selectedRouteId = value; colorsDirty = true; } } }

        /// <summary>Kamera uzaklığı: yol ve hat kalınlıkları, otobüs ve şehir boyları buna göre ölçeklenir.</summary>
        public float ZoomDistance { set => zoomDistance = value; }

        float ZoomScale => Mathf.Clamp(zoomDistance / ReferenceZoom, 0.45f, 2.4f);

        // Harita dokuları StreamingAssets'ten iner (tarayıcıda arka planda çözülür); gelince yüzeye takılır
        StreamingImage mapLoad, outerLoad, waterLoad;
        MeshRenderer mapRenderer, outerRenderer;
        Material outerMaterial;

        public void Init(GameSession gameSession, MapProjection mapProjection, RoadNetwork roadNetwork)
        {
            session = gameSession;
            projection = mapProjection;
            roads = roadNetwork;
            routeRoot = new GameObject("Routes").transform;
            routeRoot.SetParent(transform, false);
            busSprites = new MapSprites("Buses", transform, 0);
            cityBadges = new MapSprites("CityBadges", transform, 2);

            Shader.SetGlobalFloat(DaylightId, 1f);
            backgroundColor = FlatSea;
            BuildMapSurface();
            BuildSurroundings();
            mapLoad = new StreamingImage(DefaultContent.MapTextureFile, "Map", 8);
            outerLoad = new StreamingImage(DefaultContent.OuterMapTextureFile, "MapOuter", 4);
            waterLoad = new StreamingImage(DefaultContent.WaterMaskFile, "MapWater", 4);
            BuildRoadLayer();
            BuildCityMarkers();
            BuildNightLayers();


            previewLine = CreateLine("Preview", 0.22f);
            previewLine.gameObject.SetActive(false);

            session.SessionStarted += HandleSessionStarted;
            session.DayCompleted += OnDayCompleted;
            if (session.IsRunning) HandleSessionStarted();
        }

        void OnDestroy()
        {
            if (session != null)
            {
                session.SessionStarted -= HandleSessionStarted;
                session.DayCompleted -= OnDayCompleted;
            }
            Unbind();
        }

        void OnDayCompleted(DayReport _)
        {
            colorsDirty = true;
            tokensDirty = true;
        }

        // ------------------------------------------------------------------ kurulum

        void BuildMapSurface()
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
            renderer.sharedMaterial = CreateMapMaterial(true);
            if (renderer.sharedMaterial.shader != null && renderer.sharedMaterial.shader.name == "BusTycoon/MapSurface")
                MapSurfaceMaterial = renderer.sharedMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false; // düz harita: gölge yok (GPU yükü de düşer)
            renderer.enabled = false; // doku inene kadar deniz rengi (kameranın arka planı)
            mapRenderer = renderer;
        }

        /// <summary>
        /// Harita malzemesi: BusTycoon/MapSurface (düz doku, kıyıda ince halka dalgaları). Gölgelendirici yoksa ya da
        /// cihaz desteklemiyorsa düz ışıksız dokuya düşer. Çevre haritasında kıyı halkaları kapalıdır.
        /// </summary>
        Material CreateMapMaterial(bool effects)
        {
            Shader shader = Resources.Load<Shader>(DefaultContent.MapSurfaceShaderResourcePath);
            if (shader == null) shader = Shader.Find("BusTycoon/MapSurface");
            if (shader == null || !shader.isSupported) return MaterialFactory.CreateUnlitTexture(null);

            var material = new Material(shader);
            material.SetTexture("_WaterTex", Texture2D.blackTexture); // deniz maskesi inene kadar halka yok
            material.SetFloat("_Effects", effects ? 1f : 0f);
            Rect map = MapBoundsXZ; // deniz maskesi ve kenar sönmesi dünya konumundan (döşemelerde de aynı)
            material.SetVector("_MapRect", new Vector4(map.xMin, map.yMin, map.width, map.height));
            return material;
        }

        /// <summary>
        /// Haritanın çevresi: haritanın biraz altında, daha geniş bir düzlemde bulanık "devam" dokusu. Kamera haritanın
        /// dışını gördüğünde düz renk yerine bulanıklaşarak denize karışan coğrafya görünür.
        /// </summary>
        void BuildSurroundings()
        {
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
            outerMaterial = CreateMapMaterial(false);
            renderer.sharedMaterial = outerMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false; // doku inene kadar
            outerRenderer = renderer;
        }

        /// <summary>İnen harita dokularını yüzeylere takar (her biri bir kez; tarayıcıda çözme arka planda).</summary>
        void PollMapTextures()
        {
            if (mapLoad != null && mapLoad.Poll())
            {
                MapTexture = mapLoad.Texture;
                if (MapTexture != null)
                {
                    mapRenderer.sharedMaterial.mainTexture = MapTexture;
                    mapRenderer.enabled = true;
                    Debug.Log($"[Bus Tycoon] Harita dokusu {MapTexture.width}x{MapTexture.height}, {MapTexture.mipmapCount} mip");
                }
                mapLoad = null;
            }
            if (outerLoad != null && outerLoad.Poll())
            {
                if (outerLoad.Texture != null)
                {
                    outerMaterial.mainTexture = outerLoad.Texture;
                    outerRenderer.enabled = true;
                }
                outerLoad = null;
            }
            if (waterLoad != null && waterLoad.Poll())
            {
                if (waterLoad.Texture != null && MapSurfaceMaterial != null) MapSurfaceMaterial.SetTexture("_WaterTex", waterLoad.Texture);
                waterLoad = null;
            }
        }

        /// <summary>
        /// Gece örtüsü (köşe renkli, ışıksız malzeme): haritayı ve çevresini karartır; yollar, hatlar, rozetler ve
        /// otobüsler örtünün üstünde kalır.
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

        /// <summary>Sıcak, ortası parlak yuvarlak ışık dokusu (durak içi sahnesinin ışık havuzları).</summary>
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

        /// <summary>
        /// Gün ışığı (DayNightCycle): <paramref name="night"/> 0 gündüz … 1 gece yarısı; <paramref name="dusk"/> gün
        /// doğumu/batımında 1 (turuncu ton). Gece harita koyulaşır (koyu tema), yollar biraz söner.
        /// </summary>
        public void SetDaylight(float night, float dusk)
        {
            if (nightOverlay == null) return;
            Color tint = Color.Lerp(DuskTint, NightTint, Mathf.Clamp01(night * 1.6f));
            tint.a = Mathf.Max(night * 0.55f, dusk * 0.12f);
            nightMaterial.color = tint;
            nightOverlay.enabled = tint.a > 0.005f;
            // Haritanın ötesi (kameranın arka planı) örtüyle aynı oranda kararır: gece sınır görünmez.
            backgroundColor = Color.Lerp(FlatSea, new Color(tint.r, tint.g, tint.b), tint.a);
            Shader.SetGlobalFloat(DaylightId, 1f - night);
            roadNightTint = Color.Lerp(Color.white, new Color(0.7f, 0.74f, 0.9f), night * 0.8f); // LateUpdate uygular
            StationMaterials.SetNight(night);
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
            roadRenderer = renderer;
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
        /// Şehir rozeti: nüfus kademesine göre boyu değişen düz daire (metro haritası istasyonu gibi). Durumunu dolgu ve
        /// kenar rengi gösterir (bkz. RefreshColors). Çizim <see cref="MapSprites"/>'ta, bütün şehirler tek çizimde.
        /// </summary>
        void BuildCityMarkers()
        {
            foreach (var city in session.World.Cities)
            {
                if (city == null) continue;
                var marker = new CityMarker
                {
                    City = city,
                    Position = projection.GeoToWorld(city.Coordinate),
                    Radius = BadgeRadii[Mathf.Clamp(city.MapTier, 0, BadgeRadii.Length - 1)],
                    Fill = BadgeFill, Outline = BadgeEdge, Ring = BadgeRing
                };
                markers.Add(marker);
                markerByCity[city] = marker;
            }
            badgesDirty = true;
        }

        /// <summary>Şehir adının yazılacağı nokta: rozetin ekrandaki üst kenarı (kameranın yukarı yönünde).</summary>
        public Vector3 LabelAnchor(CityMarker marker, Camera camera)
        {
            Vector3 center = marker.Position + Vector3.up * BadgeLift;
            return camera != null ? center + camera.transform.up * (marker.WorldRadius * 1.05f) : center;
        }

        /// <summary>Rozetleri yeniden yazar: önce sıradan şehirler, üstte seçili / merkez / üzerine gelinen.</summary>
        void RebuildBadges()
        {
            badgesDirty = false;
            cityBadges.Clear();
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var marker in markers)
                {
                    CityData city = marker.City;
                    bool front = city == hovered || city == origin || city == destination || city == headquarters;
                    if (front != (pass == 1)) continue;
                    Vector3 center = marker.Position + Vector3.up * BadgeLift;
                    float radius = marker.WorldRadius;
                    cityBadges.AddCircle(center, radius, marker.Fill, marker.Outline, marker.Ring);
                    // Merkez: altın rozetin ortasında koyu nokta
                    if (city == headquarters) cityBadges.AddCircle(center, radius * 0.34f, marker.Outline, marker.Outline, 1f);
                }
            }
            cityBadges.Apply(4f);
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
            routeVisualsVersion++; // yeni çizgilerin kalınlığı yazılsın
            previousRouteIds.Clear();
            var previousTokens = new Dictionary<string, List<BusToken>>();
            foreach (var visual in routeVisuals.Values)
            {
                previousRouteIds.Add(visual.Route.routeId);
                previousTokens[visual.Route.routeId] = visual.Tokens;
                Destroy(visual.Line.gameObject);
            }
            routeVisuals.Clear();
            AssignRouteColors();

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
                    // Kalan hatların otobüsleri yerinde sürer (başka hat açılınca sıçramasınlar)
                    if (previousTokens.TryGetValue(route.routeId, out List<BusToken> tokens)) visual.Tokens.AddRange(tokens);
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

        /// <summary>
        /// Hat renkleri: var olan hat rengini korur, yeni hat en az kullanılan paletteki rengi alır (açılış sırasıyla).
        /// Kapanan hattın rengi boşa çıkar.
        /// </summary>
        void AssignRouteColors()
        {
            if (!session.IsRunning)
            {
                routeColorIndex.Clear();
                return;
            }
            var alive = new HashSet<string>();
            foreach (var route in session.Routes.Routes) alive.Add(route.routeId);
            var stale = new List<string>();
            foreach (var pair in routeColorIndex)
                if (!alive.Contains(pair.Key)) stale.Add(pair.Key);
            foreach (string id in stale) routeColorIndex.Remove(id);

            Array.Clear(paletteUse, 0, paletteUse.Length);
            foreach (int index in routeColorIndex.Values) paletteUse[index]++;
            foreach (var route in session.Routes.Routes)
            {
                if (routeColorIndex.ContainsKey(route.routeId)) continue;
                int best = 0;
                for (int i = 1; i < RoutePalette.Length; i++)
                    if (paletteUse[i] < paletteUse[best]) best = i;
                routeColorIndex[route.routeId] = best;
                paletteUse[best]++;
            }
        }

        /// <summary>Hattın haritadaki rengi (arayüz de aynı rengi gösterebilir).</summary>
        public Color RouteIdentityColor(string routeId) =>
            routeId != null && routeColorIndex.TryGetValue(routeId, out int index) ? RoutePalette[index] : RoutePalette[0];

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
            if (mapLoad != null || outerLoad != null || waterLoad != null) PollMapTextures();
            // Kalınlıklar her kare yakınlaştırmayla birlikte akıcı değişir (eskiden %12'lik basamaklarla)
            float scale = ZoomScale;
            markerScale = Mathf.Clamp(zoomDistance / 32f, 0.7f, 2.1f); // uzaktan da duraklar seçilsin
            if (roadMesh != null)
            {
                // Yollar bağlam içindir: yakında ince, orta uzaklıkta soluk, uzakta hiç çizilmez (Avrupa'nın tüm ağı
                // haritayı boğmasın, oyuncunun hatları öne çıksın)
                float roadZoom = Mathf.Min(scale, 1.6f);
                if (roadWidthInShader) roadMaterial.SetFloat(WidthScaleId, roadZoom);
                else if (Mathf.Abs(roadZoom - roadScale) > roadScale * 0.12f) RebuildRoadMesh(roadZoom);
                Color tint = roadNightTint;
                tint.a *= 1f - Mathf.InverseLerp(45f, 100f, zoomDistance);
                if (!tint.Equals(roadMaterial.color)) roadMaterial.color = tint;
                bool roadsVisible = tint.a > 0.004f;
                if (roadRenderer.enabled != roadsVisible) roadRenderer.enabled = roadsVisible;
            }
            UpdateLineWidths(scale);
            ScaleCityMarkers();
            if (colorsDirty) RefreshColors();
            if (badgesDirty) RebuildBadges();
            if (tokensDirty) RebuildTokens();
            AnimateReveals();
            AnimateTokens();
            UpdateAtmosphere();
        }

        /// <summary>
        /// Düz harita: pus (sis) ve gölge yok; kameranın arka planı deniz rengi (gece örtüyle kararır). Yalnız harita
        /// görünürken (durak içinde kendi kamerası çizer ve kendi ayarlarını yapar).
        /// </summary>
        void UpdateAtmosphere()
        {
            Camera main = Camera.main; // durak içindeyken ana kamera kapalı → null
            if (main == null || !main.enabled) return;
            if (RenderSettings.fog) RenderSettings.fog = false;
            if (!main.backgroundColor.Equals(backgroundColor)) main.backgroundColor = backgroundColor;
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
        /// Yakınlaştıkça şehir rozetleri küçülür ki yollar ve otobüsler görünsün. Üzerine gelinen ya da planlayıcıda
        /// seçilen şehir hafifçe büyür (yaylı geçiş). Ölçek değişince rozet örgüsü yeniden yazılır.
        /// </summary>
        void ScaleCityMarkers()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            foreach (var marker in markers)
            {
                CityData city = marker.City;
                float target = city == hovered || city == origin || city == destination ? 1.18f : 1f;
                // Az sönümlü yay: seçilince hafifçe taşıp oturur
                bool resting = Mathf.Abs(target - marker.Bump) < 0.0005f && Mathf.Abs(marker.BumpVelocity) < 0.0005f;
                if (resting)
                {
                    marker.Bump = target;
                    marker.BumpVelocity = 0f;
                }
                else
                {
                    marker.BumpVelocity += ((target - marker.Bump) * 220f - marker.BumpVelocity * 15f) * dt;
                    marker.Bump = Mathf.Clamp(marker.Bump + marker.BumpVelocity * dt, 0.6f, 1.8f);
                }
                float scale = markerScale * marker.Bump;
                if (Mathf.Abs(scale - marker.AppliedScale) < 0.0005f) continue; // durgun: dokunma
                marker.AppliedScale = scale;
                badgesDirty = true;
            }
        }

        void RefreshColors()
        {
            colorsDirty = false;
            bool running = session.IsRunning;

            foreach (var marker in markers)
            {
                CityData city = marker.City;
                Color fill = BadgeFill, edge = BadgeEdge;
                float ring = BadgeRing;
                if (running)
                {
                    if (!session.Progression.IsRegionUnlocked(city.Region))
                    {
                        fill = BadgeLockedFill;
                        edge = BadgeLockedEdge;
                    }
                    else if (HasRoute(city))
                    {
                        edge = BadgeEdgeConnected;
                        ring = BadgeRingConnected;
                    }
                }
                if (city == headquarters)
                {
                    fill = CityHeadquarters;
                    edge = BadgeEdgeConnected;
                    ring = BadgeRingConnected;
                }
                if (city == origin) fill = CityOrigin;
                else if (city == destination) fill = CityDestination;
                if (city == hovered) fill = Color.Lerp(fill, BadgeHover, 0.6f);
                if (fill.Equals(marker.Fill) && edge.Equals(marker.Outline) && Mathf.Approximately(ring, marker.Ring)) continue;
                marker.Fill = fill;
                marker.Outline = edge;
                marker.Ring = ring;
                badgesDirty = true;
            }
            badgesDirty = true; // ön plandaki rozetler (seçim) sıralamayı da değiştirir

            int signature = routeVisuals.Count;
            bool anySelected = selectedRouteId != null && routeVisuals.ContainsKey(selectedRouteId);
            foreach (var visual in routeVisuals.Values)
            {
                bool selected = visual.Route.routeId == selectedRouteId;
                Color color = RouteColor(visual.Route);
                signature = signature * 31 + visual.Route.routeId.GetHashCode() + (selected ? 7 : 0) + Mathf.RoundToInt(color.r * 9 + color.g * 90 + color.b * 900);
                if (anySelected && !selected) color.a = 0.4f; // seçili hat öne çıksın
                visual.Line.startColor = visual.Line.endColor = color;
            }
            if (signature != routeSignature)
            {
                routeSignature = signature;
                RoutesVersion++; // mini harita yalnız hat renkleri/seçimi değişince yeniden çizer
            }
        }

        float appliedLineScale = -1f;
        string appliedLineSelection;
        int appliedLineCount = -1;

        /// <summary>Hat kalınlıkları yalnız yakınlaştırma, seçim ya da hat sayısı değişince yazılır.</summary>
        void UpdateLineWidths(float scale)
        {
            if (Mathf.Abs(scale - appliedLineScale) < 0.002f && appliedLineSelection == selectedRouteId &&
                appliedLineCount == routeVisuals.Count && routeVisualsVersion == appliedLineVersion)
                return;
            appliedLineScale = scale;
            appliedLineSelection = selectedRouteId;
            appliedLineCount = routeVisuals.Count;
            appliedLineVersion = routeVisualsVersion;
            foreach (var visual in routeVisuals.Values)
                visual.Line.widthMultiplier = (visual.Route.routeId == selectedRouteId ? 0.34f : 0.24f) * scale;
            previewLine.widthMultiplier = 0.22f * scale;
        }

        int routeVisualsVersion, appliedLineVersion = -1;

        bool HasRoute(CityData city)
        {
            foreach (var route in session.Routes.Routes)
                if (route.cityAId == city.Id || route.cityBId == city.Id) return true;
            return false;
        }

        Color RouteColor(RouteState route) => RouteIdentityColor(route.routeId);

        void RebuildTokens()
        {
            tokensDirty = false;
            if (!session.IsRunning) return;

            foreach (var visual in routeVisuals.Values)
            {
                session.Fleet.GetBusesOnRoute(visual.Route.routeId, busBuffer);
                busBuffer.RemoveAll(b => !b.IsAvailableToday || b.Model == null);
                int wanted = Mathf.Min(busBuffer.Count, MaxTokensPerRoute);

                if (visual.Tokens.Count > wanted) visual.Tokens.RemoveRange(wanted, visual.Tokens.Count - wanted);
                int existing = visual.Tokens.Count;
                while (visual.Tokens.Count < wanted) visual.Tokens.Add(new BusToken());

                for (int i = 0; i < visual.Tokens.Count; i++)
                {
                    visual.Tokens[i].Model = busBuffer[i].Model;
                    // Yalnız yeni otobüsler hatta eşit aralıkla yerleşir; var olanlar yerinde kalır (her gün sonunda
                    // ya da arıza sonrası sıçramasınlar)
                    if (i >= existing) visual.Tokens[i].Phase = i / (float)Mathf.Max(1, visual.Tokens.Count) * 2f;
                }
            }
        }

        /// <summary>
        /// Otobüsler hat renginde, beyaz kenarlı kapsüller (Mini Metro trenleri gibi); gidiş ve dönüş yolun iki
        /// yanında. Her kare tek örgüde yeniden yazılır (en çok birkaç yüz köşe).
        /// </summary>
        void AnimateTokens()
        {
            if (!session.IsRunning)
            {
                if (busSprites.Count > 0)
                {
                    busSprites.Clear();
                    busSprites.Apply(1f);
                }
                return;
            }
            int speed = session.Clock != null ? session.Clock.Speed : 0;
            animationTime += Time.unscaledDeltaTime * speed;
            // Uzaktan bakınca otobüsler büyür ki hatlarda gidişleri seçilsin.
            float scale = Mathf.Clamp(zoomDistance / 36f, 0.55f, 1.9f);
            float turn = Mathf.Clamp01(Time.unscaledDeltaTime * 10f);
            float length = 0.36f * scale, width = 0.17f * scale;

            bool hadBuses = busSprites.Count > 0;
            busSprites.Clear();
            bool anySelected = selectedRouteId != null && routeVisuals.ContainsKey(selectedRouteId);
            foreach (var visual in routeVisuals.Values)
            {
                Color fill = RouteColor(visual.Route);
                Color edge = Color.white;
                if (anySelected && visual.Route.routeId != selectedRouteId) fill.a = edge.a = 0.45f;
                foreach (var token in visual.Tokens)
                {
                    float cycle = Mathf.Repeat(animationTime / visual.TripSeconds + token.Phase, 2f);
                    bool returning = cycle > 1f;
                    Evaluate(visual, LegProgress(returning ? cycle - 1f : cycle, returning), out Vector3 position, out Vector3 direction);
                    if (returning) direction = -direction;

                    Vector3 side = direction.sqrMagnitude > 1e-8f ? Vector3.Cross(Vector3.up, direction).normalized * (0.07f * scale) : Vector3.zero;
                    token.Position = position + side + Vector3.up * 0.02f;
                    if (direction.sqrMagnitude > 1e-8f)
                    {
                        Quaternion current = Quaternion.LookRotation(token.Forward, Vector3.up);
                        token.Forward = Quaternion.Slerp(current, Quaternion.LookRotation(direction, Vector3.up), turn) * Vector3.forward;
                    }
                    busSprites.AddCapsule(token.Position, token.Forward, length, width, fill, edge, 0.62f);
                }
            }
            if (busSprites.Count > 0 || hadBuses) busSprites.Apply(1f);
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
                    results.Add(new BusMarker(token.Position, token.Forward, token.Model));
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
                Vector3 first = camera.WorldToScreenPoint(points[0]);
                Vector2 previous = first;
                bool previousVisible = first.z > 0f;
                for (int i = 1; i < points.Length; i++)
                {
                    Vector3 sp = camera.WorldToScreenPoint(points[i]);
                    Vector2 current = sp;
                    bool visible = sp.z > 0f;
                    bool segmentVisible = visible && previousVisible; // kameranın arkasındaki uç yanlış seçim yaptırmasın
                    previousVisible = visible;
                    if (segmentVisible)
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
