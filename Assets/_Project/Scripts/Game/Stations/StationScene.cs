using System.Collections.Generic;
using BusTycoon.Data;
using BusTycoon.Stations;
using UnityEngine;
using UnityEngine.Rendering;

namespace BusTycoon.Game
{
    /// <summary>Durak sahnesinin girdileri: şehir, büyüklük, popülerlik, yapılan geliştirmeler ve hatlar.</summary>
    public sealed class StationInfo
    {
        public CityData City;
        public StationTier Tier;
        public StationPopularity Popularity;
        public readonly HashSet<string> Upgrades = new HashSet<string>();
        /// <summary>Kalkış panosundaki varış şehirleri (önce oyuncunun hatları).</summary>
        public readonly List<string> Destinations = new List<string>();
        /// <summary>Peronlarda bekleyen otobüslerin segmentleri (BusModelData.Tier).</summary>
        public readonly List<int> BaySegments = new List<int>();
        public string CompanyName;
        public bool IsHeadquarters;
        public int CrowdSize;
    }

    /// <summary>
    /// Durağın içi: şehir büyüklüğüne göre kurulan 3B sahne (metre ölçeği). Küçük durakta yol kenarında sundurma;
    /// otogarlarda kesit görünümlü salon (kameraya bakan duvarlar gizlenir), peronlar ve park etmiş otobüsler,
    /// giriş meydanı, cadde ve çevre binaları. Geliştirmeler ayrı nesnelerdir (StationScene.Upgrades.cs) ve satın
    /// alınınca sahneye eklenir. Kalabalık popülerliğe göre, yoldan geçen araçlar ve saat kulesi oyun zamanıyla canlanır.
    /// </summary>
    public sealed partial class StationScene
    {
        // ------------------------------------------------------------------ renkler
        static readonly Color Grass = new Color(0.5f, 0.68f, 0.38f);
        static readonly Color GrassDark = new Color(0.44f, 0.61f, 0.33f);
        static readonly Color Paving = new Color(0.84f, 0.82f, 0.78f);
        static readonly Color PavingDark = new Color(0.77f, 0.75f, 0.71f);
        static readonly Color Asphalt = new Color(0.31f, 0.32f, 0.35f);
        static readonly Color LaneWhite = new Color(0.95f, 0.95f, 0.92f);
        static readonly Color LaneYellow = new Color(1f, 0.8f, 0.2f);
        static readonly Color Curb = new Color(0.9f, 0.89f, 0.86f);
        static readonly Color FloorA = new Color(0.92f, 0.9f, 0.86f);
        static readonly Color FloorB = new Color(0.85f, 0.83f, 0.79f);
        static readonly Color Beam = new Color(0.66f, 0.69f, 0.74f);
        static readonly Color Frame = new Color(0.3f, 0.32f, 0.36f);
        static readonly Color Metal = new Color(0.72f, 0.74f, 0.78f);
        static readonly Color Orange = new Color(1f, 0.57f, 0f);
        static readonly Color Wood = new Color(0.68f, 0.48f, 0.3f);
        static readonly Color WoodDark = new Color(0.5f, 0.34f, 0.21f);
        static readonly Color White = new Color(0.97f, 0.97f, 0.96f);
        static readonly Color Dark = new Color(0.15f, 0.16f, 0.19f);
        static readonly Color Leaf = new Color(0.33f, 0.63f, 0.3f);
        static readonly Color LeafDark = new Color(0.24f, 0.5f, 0.25f);
        static readonly Color Trunk = new Color(0.45f, 0.32f, 0.2f);
        static readonly Color Pot = new Color(0.78f, 0.47f, 0.33f);
        static readonly Color ScreenBlue = new Color(0.1f, 0.18f, 0.36f);
        static readonly Color WarmLight = new Color(1f, 0.88f, 0.6f);
        static readonly Color[] Themes =
        {
            new Color(0.62f, 0.83f, 0.77f), new Color(0.98f, 0.77f, 0.63f), new Color(0.62f, 0.77f, 0.93f),
            new Color(0.79f, 0.71f, 0.91f), new Color(0.98f, 0.88f, 0.58f), new Color(0.95f, 0.64f, 0.62f)
        };
        static readonly Color[] BuildingColors =
        {
            new Color(0.95f, 0.91f, 0.82f), new Color(0.88f, 0.86f, 0.84f), new Color(0.93f, 0.78f, 0.66f),
            new Color(0.98f, 0.9f, 0.7f), new Color(0.8f, 0.84f, 0.88f), new Color(0.9f, 0.8f, 0.8f)
        };

        const float F = 0.15f;      // salon zemininin üstü
        const float BayPitch = 4.6f;

        public Transform Root { get; }
        public StationInfo Info { get; }
        public Vector3 Focus { get; private set; }
        public float DefaultDistance { get; private set; }
        public float MinDistance { get; private set; }
        public float MaxDistance { get; private set; }
        public StationCrowd Crowd { get; private set; }

        readonly List<(GameObject Object, Vector3 Normal)> walls = new List<(GameObject, Vector3)>();
        readonly Dictionary<string, GameObject> upgradeObjects = new Dictionary<string, GameObject>();
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        readonly List<StationSpot> seats = new List<StationSpot>();
        readonly List<StationSpot> spots = new List<StationSpot>();
        readonly StationGraph graph = new StationGraph();
        readonly List<(Vector3 Position, float Radius)> lamps = new List<(Vector3, float)>();
        readonly List<TextMesh> boardRows = new List<TextMesh>();
        float boardRowHeight, boardRowWidth;
        readonly List<(Transform Transform, float Speed)> traffic = new List<(Transform, float)>();
        readonly List<(Transform Hour, Transform Minute)> clockHands = new List<(Transform, Transform)>();
        readonly List<Transform> waterJets = new List<Transform>();
        readonly System.Random rng;
        Renderer glowPools;
        Material glowMaterial;
        GameObject baseBenches;
        Transform northWall, staticRoot;
        float boardTimer = -1f;

        // Yerleşim (salon: x0..x1, z0..z1; peron yürüme yolu: wx0..wx1; yolun kuzey kenarı roadN)
        readonly bool isStop;
        float W, D, H, x0, x1, z0, z1, doorW;
        float wx0, wx1, zw0, zw1, roadN, roadC, lotX0, lotX1, lotZ1;
        readonly List<float> bayZ = new List<float>();
        Color theme, themeDark;

        public StationScene(Transform parent, StationInfo info)
        {
            Info = info;
            isStop = info.Tier == StationTier.Stop;
            uint hash = 2166136261;
            foreach (char c in info.City != null ? info.City.Id : "X") hash = (hash ^ c) * 16777619;
            rng = new System.Random((int)(hash & 0x7fffffff));
            theme = Themes[(int)(hash % (uint)Themes.Length)];
            themeDark = Color.Lerp(theme, Color.black, 0.25f);

            Root = new GameObject("Station_" + (info.City != null ? info.City.Id : "?")).transform;
            Root.SetParent(parent, false);
            staticRoot = new GameObject("Static").transform;
            staticRoot.SetParent(Root, false);

            if (isStop) BuildStopLayout();
            else BuildTerminalLayout();

            foreach (string id in info.Upgrades) BuildUpgrade(id);
            if (baseBenches != null) baseBenches.SetActive(!info.Upgrades.Contains(StationCatalog.Seats));

            BuildTraffic();
            BuildGlowPools();
            Crowd = new StationCrowd(Root, graph, (int)(hash & 0xffff));
            PopulateCrowd();
        }

        // ------------------------------------------------------------------ dış API

        public bool HasUpgradeObject(string id) => upgradeObjects.ContainsKey(id);

        /// <summary>Satın alınan geliştirmeyi sahneye ekler (canlandırma için nesnesini döndürür).</summary>
        public GameObject AddUpgrade(string id)
        {
            if (upgradeObjects.TryGetValue(id, out GameObject existing)) return existing;
            Info.Upgrades.Add(id);
            GameObject added = BuildUpgrade(id);
            if (id == StationCatalog.Seats && baseBenches != null) baseBenches.SetActive(false);
            BuildGlowPools();
            PopulateCrowd();
            return added;
        }

        /// <summary>Popülerlik değişince kalabalığı yeniden dağıtır.</summary>
        public void SetCrowdSize(int size)
        {
            if (size == Info.CrowdSize) return;
            Info.CrowdSize = size;
            PopulateCrowd();
        }

        void PopulateCrowd()
        {
            Crowd.Populate(Info.CrowdSize, seats, spots);
            foreach (var (spot, role) in staffSpots) Crowd.AddStaff(spot, role);
        }

        readonly List<(StationSpot Spot, StationCrowd.Role Role)> staffSpots = new List<(StationSpot, StationCrowd.Role)>();

        /// <summary>
        /// Kare güncellemesi: <paramref name="gameDt"/> oyun hızıyla ölçekli süre (duraklayınca 0), <paramref name="night"/>
        /// 0 gündüz … 1 gece, <paramref name="hours"/> günün saati (0..24).
        /// </summary>
        public void Update(float dt, float gameDt, float night, float hours)
        {
            Crowd.Update(gameDt);
            foreach (var (vehicle, speed) in traffic)
            {
                Vector3 p = vehicle.localPosition;
                p.x += speed * gameDt;
                if (p.x > 140f) p.x -= 280f;
                if (p.x < -140f) p.x += 280f;
                vehicle.localPosition = p;
            }
            foreach (var (hour, minute) in clockHands)
            {
                hour.localRotation = Quaternion.Euler(0f, 0f, -(hours % 12f) * 30f);
                minute.localRotation = Quaternion.Euler(0f, 0f, -(hours % 1f) * 360f);
            }
            for (int i = 0; i < waterJets.Count; i++)
            {
                float pulse = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 3.1f + i * 1.7f);
                waterJets[i].localScale = new Vector3(1f, pulse, 1f);
            }
            if (glowMaterial != null)
            {
                float lit = Mathf.Clamp01((night - 0.15f) / 0.5f);
                glowMaterial.color = new Color(1f, 1f, 1f, lit * 0.85f);
                glowPools.enabled = lit > 0.01f;
            }
            boardTimer -= dt;
            if (boardTimer <= 0f)
            {
                boardTimer = 1f;
                UpdateBoard(hours);
            }
        }

        /// <summary>Kameraya bakan duvarları gizler (kesit görünüm): içerisi hep görünür.</summary>
        public void UpdateCutaway(Vector3 cameraLocal)
        {
            Vector3 toCamera = cameraLocal - Focus;
            toCamera.y = 0f;
            toCamera.Normalize();
            foreach (var (wall, normal) in walls)
            {
                bool show = Vector3.Dot(normal, toCamera) < 0.2f;
                if (wall.activeSelf != show) wall.SetActive(show);
            }
        }

        public void Destroy()
        {
            Crowd?.Clear();
            foreach (Mesh mesh in ownedMeshes) if (mesh != null) Object.Destroy(mesh);
            ownedMeshes.Clear();
            if (glowMaterial != null) Object.Destroy(glowMaterial);
            if (Root != null) Object.Destroy(Root.gameObject);
        }

        // ------------------------------------------------------------------ yardımcılar

        GameObject Make(MeshKit kit, string name, Transform parent, bool shadows = true)
        {
            if (kit.IsEmpty) return null;
            MeshRenderer renderer = kit.BuildObject(name, parent != null ? parent : staticRoot, null, shadows);
            ownedMeshes.Add(renderer.GetComponent<MeshFilter>().sharedMesh);
            return renderer.gameObject;
        }

        GameObject Place(Mesh mesh, MeshLayer[] layers, string name, Transform parent, Vector3 position, float yaw, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = StationMaterials.For(layers, null);
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Salon içi nokta: <paramref name="u"/> batı duvarından, <paramref name="v"/> güney duvarından metre.</summary>
        Vector3 Hall(float u, float v, float y = F) => new Vector3(x0 + u, y, z0 + v);

        float R(float min, float max) => min + (float)rng.NextDouble() * (max - min);

        string CityName => Info.City != null ? Info.City.DisplayName : "Şehir";

        /// <summary>Türkçe büyük harf (i → İ, ı → I); web derlemesinde kültür verisine güvenmeden.</summary>
        static string Upper(string text) => text.Replace('i', 'İ').Replace('ı', 'I').ToUpperInvariant();

        // ------------------------------------------------------------------ otogar (salon + peronlar)

        void BuildTerminalLayout()
        {
            switch (Info.Tier)
            {
                case StationTier.Terminal: W = 20f; D = 12f; H = 5.2f; doorW = 4f; break;
                case StationTier.Large: W = 28f; D = 15f; H = 6.5f; doorW = 5f; break;
                default: W = 38f; D = 19f; H = 8.5f; doorW = 6f; break;
            }
            int bays = StationCatalog.Bays(Info.Tier);
            float total = W + 18f;
            x0 = -total / 2f;
            x1 = x0 + W;
            z0 = -D / 2f;
            z1 = D / 2f;
            wx0 = x1;
            wx1 = x1 + 3.5f;
            float span = bays * BayPitch;
            for (int i = 0; i < bays; i++) bayZ.Add(-span / 2f + BayPitch / 2f + i * BayPitch);
            zw0 = Mathf.Min(z0, -span / 2f - 1f);
            zw1 = Mathf.Max(z1, span / 2f + 1f);
            roadN = zw0 - 9f;
            roadC = roadN - 4.5f;
            lotX0 = x0 - 8f;
            lotX1 = wx1 + 23f;
            lotZ1 = zw1 + 8f;

            Focus = new Vector3((x0 + wx1 + 12f) / 2f, 1.5f, (roadN + zw1) / 2f + 1f);
            DefaultDistance = Info.Tier == StationTier.Terminal ? 33f : Info.Tier == StationTier.Large ? 42f : 53f;
            MinDistance = DefaultDistance * 0.4f;
            MaxDistance = DefaultDistance * 1.3f;

            BuildGround();
            BuildHall();
            BuildPlatforms();
            BuildPlaza();
            BuildSurroundings(Mathf.Max(lotX1 - lotX0, lotZ1 - roadN) * 0.5f + 22f);
            BuildTerminalGraph();
        }

        void BuildGround()
        {
            var k = new MeshKit();
            k.Box(new Vector3(0f, -0.05f, 0f), new Vector3(320f, 0.1f, 320f), Grass);
            // Ana cadde ve yan sokaklar
            k.Box(new Vector3(0f, 0.01f, roadC), new Vector3(320f, 0.02f, 9f), Asphalt);
            for (float x = -150f; x < 150f; x += 6f)
                k.Box(new Vector3(x, 0.025f, roadC), new Vector3(3f, 0.01f, 0.18f), LaneWhite);
            k.Box(new Vector3(0f, 0.08f, roadN + 0.15f), new Vector3(320f, 0.16f, 0.3f), Curb);
            k.Box(new Vector3(0f, 0.08f, roadN - 9.15f), new Vector3(320f, 0.16f, 0.3f), Curb);
            k.Box(new Vector3(0f, 0.04f, roadN - 11f), new Vector3(320f, 0.08f, 3.6f), PavingDark); // karşı kaldırım
            foreach (float x in new[] { lotX0 - 6f, lotX1 + 6f })
                k.Box(new Vector3(x, 0.01f, roadN + 80f), new Vector3(8f, 0.02f, 160f), Asphalt);

            // Durak arsası: kaldırım taşı, dekoratif derz çizgileri
            float cx = (lotX0 + lotX1) / 2f, cz = (roadN + lotZ1) / 2f;
            k.Box(new Vector3(cx, 0.03f, cz), new Vector3(lotX1 - lotX0, 0.06f, lotZ1 - roadN), Paving);
            for (float x = lotX0 + 4f; x < lotX1; x += 4f)
                k.Box(new Vector3(x, 0.062f, cz), new Vector3(0.06f, 0.005f, lotZ1 - roadN), PavingDark);

            // Yaya geçidi (girişin önünde)
            float door = (x0 + x1) / 2f;
            for (int i = -3; i <= 3; i++)
                k.Box(new Vector3(door + i * 1.1f, 0.025f, roadC), new Vector3(0.6f, 0.012f, 8.4f), LaneWhite);
            Make(k, "Ground", staticRoot, false);
        }

        void BuildHall()
        {
            var k = new MeshKit();
            // Zemin döşemesi (iki tonlu karolar)
            k.Box(new Vector3((x0 + x1) / 2f, F / 2f, 0f), new Vector3(W + 0.6f, F, D + 0.6f), FloorB);
            const float tile = 2.5f;
            for (float u = 0f; u < W - 0.01f; u += tile)
                for (float v = 0f; v < D - 0.01f; v += tile)
                {
                    if (((int)(u / tile) + (int)(v / tile)) % 2 == 1) continue;
                    float w = Mathf.Min(tile, W - u), d = Mathf.Min(tile, D - v);
                    k.Box(Hall(u + w / 2f, v + d / 2f, F + 0.003f), new Vector3(w, 0.006f, d), FloorA);
                }
            // Duvar dipleri (duvar gizlenince salonun sınırı görünsün) ve çatı kirişleri
            Wall(k, true, 0.55f);
            for (float u = 3f; u < W - 1f; u += 5f)
            {
                k.Box(Hall(u, D / 2f, H - 0.2f), new Vector3(0.2f, 0.36f, D), Beam);
                k.Layer = MeshLayer.Glow; // tavan lambaları
                for (float v = 3f; v < D - 1.5f; v += 4f)
                    k.Box(Hall(u, v, H - 0.55f), new Vector3(0.9f, 0.08f, 0.5f), WarmLight);
                k.Layer = MeshLayer.Lit;
            }
            for (float u = 3f; u < W - 1f; u += 5f)
                for (float v = 3f; v < D - 1.5f; v += 4f)
                    lamps.Add((Hall(u, v), 3.2f));
            k.Box(Hall(W / 2f, 0.5f, H - 0.2f), new Vector3(W, 0.36f, 0.2f), Beam);
            k.Box(Hall(W / 2f, D - 0.5f, H - 0.2f), new Vector3(W, 0.36f, 0.2f), Beam);

            BaseFurniture(k);
            Make(k, "Hall", staticRoot);
            BuildBaseBenches();

            // Duvarlar: her yön ayrı nesne (kameraya bakan gizlenir)
            northWall = WallObject("North", Vector3.forward);
            WallObject("South", Vector3.back);
            WallObject("West", Vector3.left);
            WallObject("East", Vector3.right);

            // Kuzey duvarında şehir adı
            float signY = H - 0.75f;
            StationSigns.Text(northWall, Upper(CityName) + (Info.Tier == StationTier.Metro ? " TERMİNALİ" : " OTOGARI"),
                Hall(W / 2f, D - 0.33f, signY), 0f, Mathf.Min(1.1f, H * 0.16f), W * 0.5f, Orange);
            StationSigns.CityIcon(northWall, Info.City, Hall(W * 0.2f, D - 0.33f, signY), 0f, 1.2f);
        }

        /// <summary>Duvar (yükseklik <paramref name="height"/>): <paramref name="stub"/> ise yalnız dipler (tüm yönler).</summary>
        void Wall(MeshKit k, bool stub, float height, Vector3? only = null)
        {
            float t = 0.3f, cx = (x0 + x1) / 2f;
            float top = F + height;
            bool all = only == null;
            void Segment(Vector3 a, Vector3 b, bool alongX)
            {
                float length = alongX ? Mathf.Abs(b.x - a.x) : Mathf.Abs(b.z - a.z);
                if (length < 0.05f) return;
                Vector3 c = (a + b) / 2f;
                Vector3 size = alongX ? new Vector3(length, height, t) : new Vector3(t, height, length);
                k.Box(new Vector3(c.x, F + height / 2f, c.z), size, stub ? themeDark : theme);
                if (stub) return;
                // İç yüz açık renk, iki yüzde pencere bandı
                Vector3 inward = alongX ? new Vector3(0f, 0f, c.z > 0f ? -1f : 1f) : new Vector3(c.x > cx ? -1f : 1f, 0f, 0f);
                Vector3 skin = alongX ? new Vector3(length, height - 0.02f, 0.04f) : new Vector3(0.04f, height - 0.02f, length);
                k.Box(new Vector3(c.x, F + height / 2f, c.z) + inward * (t / 2f + 0.02f), skin, Color.Lerp(theme, White, 0.75f));
                if (length > 2.2f && height > 3f)
                {
                    float bandBottom = F + 1.3f, bandTop = F + height - 1.1f;
                    Vector3 band = alongX ? new Vector3(length - 1.2f, bandTop - bandBottom, 0.02f) : new Vector3(0.02f, bandTop - bandBottom, length - 1.2f);
                    Vector3 mid = new Vector3(c.x, (bandBottom + bandTop) / 2f, c.z);
                    k.Box(mid - inward * (t / 2f + 0.015f), band, White, MeshLayer.Window);
                    k.Box(mid + inward * (t / 2f + 0.05f), band, White, MeshLayer.Window);
                    // Pencere kayıtları (dikmeler)
                    int mullions = Mathf.FloorToInt(length / 2.4f);
                    for (int i = 1; i < mullions; i++)
                    {
                        float f = -0.5f + i / (float)mullions;
                        Vector3 m = alongX ? new Vector3(c.x + f * (length - 1.2f), mid.y, c.z) : new Vector3(c.x, mid.y, c.z + f * (length - 1.2f));
                        Vector3 ms = alongX ? new Vector3(0.12f, band.y, 0.09f) : new Vector3(0.09f, band.y, 0.12f);
                        k.Box(m - inward * (t / 2f + 0.02f), ms, Frame);
                        k.Box(m + inward * (t / 2f + 0.06f), ms, Frame);
                    }
                }
                // Saçak (duvar tepesi)
                Vector3 capSize = alongX ? new Vector3(length + 0.3f, 0.35f, t + 0.3f) : new Vector3(t + 0.3f, 0.35f, length + 0.3f);
                k.Box(new Vector3(c.x, top + 0.12f, c.z), capSize, themeDark);
            }

            float door = doorW / 2f;
            if (all || only == Vector3.back)
            {
                Segment(new Vector3(x0, 0f, z0), new Vector3(cx - door, 0f, z0), true);
                Segment(new Vector3(cx + door, 0f, z0), new Vector3(x1, 0f, z0), true);
                if (!stub)
                {
                    k.Box(new Vector3(cx, F + 3.1f + (height - 3.1f) / 2f, z0), new Vector3(doorW, height - 3.1f, t), theme); // kapı üstü
                    k.Box(new Vector3(cx, F + 3.25f, z0 - 1.3f), new Vector3(doorW + 2f, 0.25f, 2.6f), Orange);          // giriş saçağı
                    k.Box(new Vector3(cx, F + 1.55f, z0), new Vector3(doorW, 3.1f, 0.06f), White, MeshLayer.Glass);      // cam kapı
                    k.Box(new Vector3(cx, F + 1.55f, z0 - 0.05f), new Vector3(0.08f, 3.1f, 0.1f), Frame);
                }
            }
            if (all || only == Vector3.forward) Segment(new Vector3(x0, 0f, z1), new Vector3(x1, 0f, z1), true);
            if (all || only == Vector3.left) Segment(new Vector3(x0, 0f, z0), new Vector3(x0, 0f, z1), false);
            if (all || only == Vector3.right)
            {
                // Perona açılan kapı(lar)
                var doors = Info.Tier == StationTier.Metro ? new[] { -D * 0.22f, D * 0.22f } : new[] { 0f };
                float from = z0;
                foreach (float dz in doors)
                {
                    Segment(new Vector3(x1, 0f, from), new Vector3(x1, 0f, dz - 2f), false);
                    if (!stub)
                    {
                        k.Box(new Vector3(x1, F + 3.1f + (height - 3.1f) / 2f, dz), new Vector3(t, height - 3.1f, 4f), theme);
                        k.Box(new Vector3(x1, F + 1.55f, dz), new Vector3(0.06f, 3.1f, 4f), White, MeshLayer.Glass);
                    }
                    from = dz + 2f;
                }
                Segment(new Vector3(x1, 0f, from), new Vector3(x1, 0f, z1), false);
            }
        }

        Transform WallObject(string name, Vector3 normal)
        {
            var k = new MeshKit();
            Wall(k, false, H, normal);
            GameObject go = Make(k, "Wall" + name, staticRoot);
            walls.Add((go, normal));
            return go.transform;
        }

        /// <summary>Bekleme bankları: koltuk geliştirmesi aynı yerlere minderli koltuk koyar (yolcular hangisi olursa otursun).</summary>
        void BuildBaseBenches()
        {
            var bench = new MeshKit();
            foreach (var seat in SeatRows())
            {
                bench.Push(seat.Position, Yaw(seat.Facing));
                bench.Box(new Vector3(0f, 0.4f, 0.16f), new Vector3(0.62f, 0.06f, 0.46f), Metal);
                bench.Box(new Vector3(0f, 0.68f, -0.11f), new Vector3(0.62f, 0.5f, 0.05f), Metal);
                bench.Box(new Vector3(0f, 0.2f, 0.16f), new Vector3(0.05f, 0.4f, 0.4f), Frame);
                bench.Pop();
            }
            baseBenches = Make(bench, "BaseBenches", staticRoot);
        }

        /// <summary>Her otogarda olan eşyalar: otomatlar, çöp kutuları, tarife levhası.</summary>
        void BaseFurniture(MeshKit k)
        {
            for (int i = 0; i < 2; i++)
            {
                Vector3 p = Hall(W / 2f + doorW / 2f + 1.1f + i * 1.15f, 0.65f);
                k.Box(p + new Vector3(0f, 0.95f, 0f), new Vector3(1.0f, 1.9f, 0.8f), i == 0 ? new Color(0.85f, 0.2f, 0.2f) : new Color(0.2f, 0.45f, 0.8f));
                k.Box(p + new Vector3(0f, 1.15f, 0.41f), new Vector3(0.75f, 1.1f, 0.02f), WarmLight, MeshLayer.Glow);
            }
            foreach (var p in new[] { Hall(W * 0.3f, 0.6f), Hall(W * 0.62f, 0.6f), Hall(W * 0.71f, D - 1.2f) })
                TrashBin(k, p);
            // Basit tarife levhası (kuzey duvarı, gişelerle oyun alanı arasında)
            k.Box(Hall(W * 0.7f, D - 0.22f, F + 2.0f), new Vector3(1.9f, 1.3f, 0.06f), White);
            for (int i = 0; i < 5; i++)
                k.Box(Hall(W * 0.7f, D - 0.26f, F + 2.45f - i * 0.22f), new Vector3(1.6f, 0.05f, 0.02f), Frame);
        }

        /// <summary>Bekleme koltuklarının yerleri (otururken kişinin kalçası; yüz güneye).</summary>
        IEnumerable<StationSpot> SeatRows()
        {
            if (isStop)
            {
                for (int i = 0; i < 4; i++) yield return new StationSpot(new Vector3(-1.1f + i * 0.73f, 0.18f, 3.62f), Vector3.back);
                yield break;
            }
            int rows = Info.Tier == StationTier.Terminal ? 2 : Info.Tier == StationTier.Large ? 3 : 4;
            float from = W * 0.34f, to = W * 0.66f;
            for (int r = 0; r < rows; r++)
            {
                float v = D * 0.36f + r * 1.9f;
                for (float u = from; u <= to + 0.01f; u += 0.68f)
                    yield return new StationSpot(Hall(u, v), Vector3.back);
            }
        }

        static float Yaw(Vector3 facing) => Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;

        void BuildPlatforms()
        {
            var k = new MeshKit();
            // Yürüme yolu (yükseltilmiş, sarı hissedilebilir şerit)
            float zc = (zw0 + zw1) / 2f, len = zw1 - zw0;
            k.Box(new Vector3((wx0 + wx1) / 2f, 0.1f, zc), new Vector3(wx1 - wx0, 0.2f, len), Curb);
            k.Box(new Vector3(wx1 - 0.35f, 0.205f, zc), new Vector3(0.3f, 0.012f, len), LaneYellow);
            // Peron önü asfalt ve park çizgileri
            float apronX = wx1 + 9f;
            k.Box(new Vector3(apronX + 2f, 0.045f, (roadN + zw1 + 4f) / 2f), new Vector3(22f, 0.03f, zw1 + 4f - roadN), Asphalt);
            for (int i = 0; i <= bayZ.Count; i++)
            {
                float z = bayZ.Count > 0 ? bayZ[0] - BayPitch / 2f + i * BayPitch : 0f;
                k.Box(new Vector3(wx1 + 6.5f, 0.065f, z), new Vector3(12f, 0.01f, 0.15f), LaneWhite);
            }
            // Peron numaraları (direk + levha) ve banklar
            for (int i = 0; i < bayZ.Count; i++)
            {
                Vector3 post = new Vector3(wx1 - 0.6f, 0.2f, bayZ[i] - BayPitch / 2f + 0.3f);
                k.Box(post + new Vector3(0f, 1.4f, 0f), new Vector3(0.12f, 2.8f, 0.12f), Frame);
                k.Box(post + new Vector3(0f, 2.75f, 0.9f), new Vector3(0.1f, 0.7f, 1.6f), Orange);
                StationSigns.Text(staticRoot, "PERON " + (i + 1), post + new Vector3(-0.07f, 2.75f, 0.9f), 90f, 0.4f, 1.45f, White);
                StationSigns.Text(staticRoot, "PERON " + (i + 1), post + new Vector3(0.07f, 2.75f, 0.9f), -90f, 0.4f, 1.45f, White);
                if (i % 2 == 0)
                {
                    k.Box(new Vector3(wx0 + 0.9f, 0.45f, bayZ[i] + 1f), new Vector3(0.45f, 0.06f, 1.8f), Metal);
                    k.Box(new Vector3(wx0 + 0.7f, 0.7f, bayZ[i] + 1f), new Vector3(0.05f, 0.45f, 1.8f), Metal);
                }
            }
            Make(k, "Platforms", staticRoot);

            // Park etmiş otobüsler (popülerliğe göre dolu peron)
            for (int i = 0; i < Info.BaySegments.Count && i < bayZ.Count; i++)
            {
                int segment = Info.BaySegments[i];
                Mesh bus = StationVehicles.Bus(segment, out MeshLayer[] layers);
                float length = StationVehicles.BusLength(segment);
                Place(bus, layers, "Bus", staticRoot, new Vector3(wx1 + 0.8f + length / 2f, 0.05f, bayZ[i]), -90f);
            }
        }

        void BuildPlaza()
        {
            var k = new MeshKit();
            float door = (x0 + x1) / 2f;
            // Tabela direği: şehir adı ve ikonu
            Vector3 pylon = new Vector3(x0 + 3f, 0.06f, roadN + 3.2f);
            k.Box(pylon + new Vector3(0f, 2.6f, 0f), new Vector3(3.2f, 5.2f, 0.6f), theme);
            k.Box(pylon + new Vector3(0f, 5.35f, 0f), new Vector3(3.4f, 0.3f, 0.8f), themeDark);
            k.Box(pylon + new Vector3(0f, 2.3f, -0.31f), new Vector3(2.9f, 2.2f, 0.02f), White);
            Make(k, "Pylon", staticRoot);
            StationSigns.CityIcon(staticRoot, Info.City, pylon + new Vector3(0f, 4.3f, -0.33f), 0f, 1.3f);
            StationSigns.Text(staticRoot, Upper(CityName), pylon + new Vector3(0f, 2.75f, -0.34f), 0f, 0.62f, 2.7f, Dark);
            StationSigns.Text(staticRoot, Info.Tier == StationTier.Metro ? "TERMİNAL" : "OTOGAR", pylon + new Vector3(0f, 1.85f, -0.34f), 0f, 0.42f, 2.6f, Orange);

            // Ağaçlar, temel sokak lambaları, babalar, bisiklet parkı
            var p = new MeshKit();
            Tree(p, new Vector3(x0 + 8f, 0.06f, roadN + 2.4f), 1.6f);
            Tree(p, new Vector3(x1 - 3f, 0.06f, roadN + 2.4f), 1.4f);
            LampPost(p, new Vector3(door - doorW, 0.06f, roadN + 1f), false);
            LampPost(p, new Vector3(door + doorW, 0.06f, roadN + 1f), false);
            for (float x = x0; x < x1; x += 2.5f)
                if (Mathf.Abs(x - door) > 4.5f) p.Cylinder(new Vector3(x, 0.06f, roadN + 0.6f), 0.13f, 0.8f, Frame, 6);
            for (int i = 0; i < 4; i++)
                p.Box(new Vector3(x0 + 13f + i * 0.7f, 0.5f, roadN + 2.2f), new Vector3(0.05f, 0.8f, 0.9f), Metal);
            TrashBin(p, new Vector3(door + 3f, 0.06f, z0 - 1.2f));
            Make(p, "PlazaProps", staticRoot);
            lamps.Add((new Vector3(door - doorW, 0.06f, roadN + 1f), 4f));
            lamps.Add((new Vector3(door + doorW, 0.06f, roadN + 1f), 4f));
        }

        /// <summary>Arsanın çevresinde şehir dokusu: büyük şehirde yüksek bloklar, küçükte bahçeli evler; ağaçlar.</summary>
        void BuildSurroundings(float inner)
        {
            var k = new MeshKit();
            int count = isStop ? 14 : 20;
            float maxHeight = Info.Tier switch { StationTier.Stop => 8f, StationTier.Terminal => 14f, StationTier.Large => 22f, _ => 34f };
            for (int i = 0; i < count; i++)
            {
                float w = R(9f, 16f), d = R(9f, 14f), h = isStop ? R(4.5f, maxHeight) : R(6f, maxHeight);
                Vector3 c = default;
                bool placed = false;
                for (int attempt = 0; attempt < 12 && !placed; attempt++)
                {
                    float angle = (i + R(-0.45f, 0.45f)) / count * Mathf.PI * 2f;
                    float radius = inner + R(4f, 30f);
                    c = new Vector3(Focus.x + Mathf.Cos(angle) * radius, 0f, Focus.z + Mathf.Sin(angle) * radius);
                    placed = IsFree(c, Mathf.Max(w, d) / 2f + 1.5f);
                }
                if (!placed) continue;
                Color wall = BuildingColors[rng.Next(BuildingColors.Length)];
                k.Box(c + new Vector3(0f, h / 2f, 0f), new Vector3(w, h, d), wall);
                // Kat pencereleri
                for (float y = 1.8f; y < h - 0.8f; y += 3f)
                {
                    k.Box(c + new Vector3(0f, y, -d / 2f - 0.02f), new Vector3(w * 0.84f, 1.1f, 0.03f), White, MeshLayer.Window);
                    k.Box(c + new Vector3(0f, y, d / 2f + 0.02f), new Vector3(w * 0.84f, 1.1f, 0.03f), White, MeshLayer.Window);
                    k.Box(c + new Vector3(-w / 2f - 0.02f, y, 0f), new Vector3(0.03f, 1.1f, d * 0.8f), White, MeshLayer.Window);
                    k.Box(c + new Vector3(w / 2f + 0.02f, y, 0f), new Vector3(0.03f, 1.1f, d * 0.8f), White, MeshLayer.Window);
                }
                if (isStop || h < 9f) k.Gable(c + new Vector3(0f, h, 0f), new Vector3(w + 0.6f, 2.6f, d + 0.6f), new Color(0.78f, 0.36f, 0.26f));
                else k.Box(c + new Vector3(R(-2f, 2f), h + 0.6f, R(-2f, 2f)), new Vector3(2.5f, 1.2f, 2f), Metal);
            }
            for (int i = 0; i < 26; i++)
            {
                float angle = R(0f, Mathf.PI * 2f), radius = inner + R(-6f, 10f);
                var c = new Vector3(Focus.x + Mathf.Cos(angle) * radius, 0f, Focus.z + Mathf.Sin(angle) * radius);
                if (IsFree(c, 2f)) Tree(k, c, R(1.2f, 2f));
            }
            Make(k, "City", staticRoot);
        }

        /// <summary>Çevre binası/ağacı için yer boş mu: durak arsası, cadde, kaldırım ve yan sokaklar dışında.</summary>
        bool IsFree(Vector3 c, float radius)
        {
            if (c.z + radius > roadN - 13f && c.z - radius < roadN + (isStop ? 6.2f : 1f)) return false;        // cadde ve kaldırımlar
            if (!isStop)
            {
                if (c.x + radius > lotX0 - 2f && c.x - radius < lotX1 + 2f && c.z - radius < lotZ1 + 2f) return false; // arsa
                if (Mathf.Abs(c.x - (lotX0 - 6f)) < radius + 4.5f || Mathf.Abs(c.x - (lotX1 + 6f)) < radius + 4.5f) return c.z < roadN - 13f; // yan sokaklar
            }
            return true;
        }

        // ------------------------------------------------------------------ küçük durak

        void BuildStopLayout()
        {
            roadN = 0f;
            roadC = -4.5f;
            Focus = new Vector3(0f, 1f, 0.8f);
            DefaultDistance = 21f;
            MinDistance = 9f;
            MaxDistance = 28f;

            var k = new MeshKit();
            k.Box(new Vector3(0f, -0.05f, 0f), new Vector3(300f, 0.1f, 300f), Grass);
            k.Box(new Vector3(0f, 0.01f, roadC), new Vector3(300f, 0.02f, 9f), Asphalt);
            for (float x = -150f; x < 150f; x += 6f) k.Box(new Vector3(x, 0.025f, roadC), new Vector3(3f, 0.01f, 0.18f), LaneWhite);
            k.Box(new Vector3(0f, 0.09f, 2.6f), new Vector3(300f, 0.18f, 5.2f), Paving);
            k.Box(new Vector3(0f, 0.1f, 0.12f), new Vector3(300f, 0.2f, 0.25f), Curb);
            k.Box(new Vector3(0f, 0.04f, roadC - 6f), new Vector3(300f, 0.08f, 3.2f), PavingDark);
            // Otobüs cebi çizgisi
            k.Box(new Vector3(6f, 0.025f, -3f), new Vector3(20f, 0.01f, 0.15f), LaneYellow);
            for (int i = 0; i < 5; i++) k.Box(new Vector3(-3f + i * 4.5f, 0.025f, -1.6f), new Vector3(2.2f, 0.01f, 0.15f), LaneYellow);
            // Park: çimen ve yürüyüş yolu
            k.Box(new Vector3(0f, 0.02f, 11f), new Vector3(300f, 0.04f, 11f), GrassDark);
            k.Box(new Vector3(-3f, 0.05f, 11f), new Vector3(1.6f, 0.02f, 11f), Paving);

            // Sundurma: arka cam, yan paneller, çatı (şehrin renginde)
            float y = 0.18f;
            k.Box(new Vector3(0f, y + 1.3f, 3.85f), new Vector3(6.4f, 2.4f, 0.08f), White, MeshLayer.Glass);
            foreach (float x in new[] { -3.2f, 3.2f })
            {
                k.Box(new Vector3(x, y + 1.3f, 3.85f), new Vector3(0.12f, 2.6f, 0.12f), Frame);
                k.Box(new Vector3(x, y + 1.3f, 3.1f), new Vector3(0.06f, 2.2f, 1.4f), White, MeshLayer.Glass);
                k.Box(new Vector3(x, y + 1.3f, 2.4f), new Vector3(0.12f, 2.6f, 0.12f), Frame);
            }
            k.Box(new Vector3(0f, y + 2.68f, 3.1f), new Vector3(6.9f, 0.18f, 2.0f), theme);
            k.Box(new Vector3(0f, y + 2.8f, 3.1f), new Vector3(6.6f, 0.08f, 1.8f), themeDark);
            // Durak tabelası
            k.Box(new Vector3(5.2f, y + 1.5f, 0.9f), new Vector3(0.12f, 3f, 0.12f), Frame);
            k.Push(new Vector3(5.2f, y + 2.7f, 0.9f), Quaternion.Euler(90f, 0f, 0f)); // tabela diski −z'ye bakar
            k.Cylinder(new Vector3(0f, -0.04f, 0f), 0.5f, 0.08f, Orange, 12, true, true);
            k.Pop();
            k.Box(new Vector3(5.2f, y + 1.9f, 0.84f), new Vector3(1.6f, 0.55f, 0.06f), White);
            TrashBin(k, new Vector3(-4.2f, y, 1.0f));
            Tree(k, new Vector3(-11f, y, 3.5f), 1.6f);
            LampPost(k, new Vector3(8.5f, y, 0.7f), false);
            lamps.Add((new Vector3(8.5f, y, 0.7f), 3.5f));
            k.Layer = MeshLayer.Glow;
            k.Box(new Vector3(0f, y + 2.56f, 3.1f), new Vector3(4f, 0.05f, 0.3f), WarmLight);
            k.Layer = MeshLayer.Lit;
            lamps.Add((new Vector3(0f, y, 2.9f), 3f));
            Make(k, "Stop", staticRoot);

            BuildBaseBenches();
            StationSigns.Text(staticRoot, "D", new Vector3(5.2f, y + 2.74f, 0.8f), 0f, 0.6f, 0.6f, White);
            StationSigns.Text(staticRoot, Upper(CityName), new Vector3(5.2f, y + 1.92f, 0.8f), 0f, 0.3f, 1.45f, Dark);
            StationSigns.CityIcon(staticRoot, Info.City, new Vector3(0f, y + 2.7f, 2.08f), 0f, 0.9f);

            // Bekleyen otobüs
            if (Info.BaySegments.Count > 0)
            {
                int segment = Info.BaySegments[0];
                Mesh bus = StationVehicles.Bus(segment, out MeshLayer[] layers);
                Place(bus, layers, "Bus", staticRoot, new Vector3(12.5f, 0.02f, -1.7f), -90f); // sundurmayı kapatmasın: durağa yanaşıyor
            }

            BuildSurroundings(22f);
            BuildStopGraph();
        }

        // ------------------------------------------------------------------ yürüyüş ağı ve bekleme yerleri

        void BuildTerminalGraph()
        {
            float door = (x0 + x1) / 2f;
            int plazaW = graph.Add(new Vector3(x0 + 2f, 0.06f, roadN + 1.6f));
            int plazaC = graph.Add(new Vector3(door, 0.06f, roadN + 1.6f), true);
            int plazaE = graph.Add(new Vector3(wx0 + 1.6f, 0.2f, roadN + 1.6f));
            int outside = graph.Add(new Vector3(door, 0.06f, z0 - 2f));
            int inside = graph.Add(Hall(W / 2f, 1.4f));
            int frontW = graph.Add(Hall(W * 0.29f, D * 0.17f), true);
            int frontE = graph.Add(Hall(W * 0.71f, D * 0.17f), true);
            int backW = graph.Add(Hall(W * 0.29f, D - 3.6f));
            int backC = graph.Add(Hall(W * 0.5f, D - 3.6f), true);
            int backE = graph.Add(Hall(W * 0.71f, D - 3.6f));
            graph.Link(plazaW, plazaC);
            graph.Link(plazaC, plazaE);
            graph.Link(plazaC, outside);
            graph.Link(outside, inside);
            graph.Link(inside, frontW);
            graph.Link(inside, frontE);
            graph.Link(frontW, backW);
            graph.Link(frontE, backE);
            graph.Link(backW, backC);
            graph.Link(backC, backE);

            // Perona çıkış kapıları ve peron boyunca düğümler
            var doors = Info.Tier == StationTier.Metro ? new[] { -D * 0.22f, D * 0.22f } : new[] { 0f };
            int previousWalk = -1;
            var walkway = new List<(float z, int node)>();
            foreach (float dz in doors)
            {
                int inner = graph.Add(new Vector3(x1 - 1.4f, F, dz));
                int outer = graph.Add(new Vector3(wx0 + 1.6f, 0.2f, dz));
                graph.Link(frontE, inner);
                graph.Link(backE, inner);
                graph.Link(inner, outer);
                walkway.Add((dz, outer));
            }
            foreach (float z in bayZ) walkway.Add((z + 1.2f, graph.Add(new Vector3(wx0 + 1.6f, 0.2f, z + 1.2f), true)));
            walkway.Add((roadN + 1.6f, plazaE));
            walkway.Sort((a, b) => a.z.CompareTo(b.z));
            foreach (var (_, node) in walkway)
            {
                if (previousWalk >= 0) graph.Link(previousWalk, node);
                previousWalk = node;
            }

            // Peronda otobüs bekleyenler
            foreach (float z in bayZ)
            {
                spots.Add(new StationSpot(new Vector3(wx1 - 1.1f, 0.2f, z - 0.9f), Vector3.right));
                spots.Add(new StationSpot(new Vector3(wx1 - 1.3f, 0.2f, z + 0.4f), Vector3.right));
            }
            spots.Add(new StationSpot(new Vector3(x0 + 3.2f, 0.06f, roadN + 4.4f), Vector3.forward));
            spots.Add(new StationSpot(Hall(W * 0.7f, D - 1.9f), Vector3.forward));
            seats.AddRange(SeatRows());
            cafeNode = backW;
            frontWestNode = frontW;
            frontEastNode = frontE;
            backEastNode = backE;
        }

        int cafeNode = -1, frontWestNode = -1, frontEastNode = -1, backEastNode = -1;

        void BuildStopGraph()
        {
            const float y = 0.18f;
            var near = new[] { -13f, -6f, 0f, 6f, 13f };
            int[] front = new int[near.Length], back = new int[near.Length];
            for (int i = 0; i < near.Length; i++)
            {
                front[i] = graph.Add(new Vector3(near[i], y, 1.3f), near[i] == 0f);
                back[i] = graph.Add(new Vector3(near[i], y, 4.6f));
                if (i > 0)
                {
                    graph.Link(front[i - 1], front[i]);
                    graph.Link(back[i - 1], back[i]);
                }
            }
            graph.Link(front[0], back[0]);
            graph.Link(front[1], back[1]);
            graph.Link(front[3], back[3]);
            graph.Link(front[4], back[4]);
            for (int i = 0; i < 4; i++)
                spots.Add(new StationSpot(new Vector3(-1.8f + i * 1.3f, y, 1.0f + (i % 2) * 0.4f), Vector3.back));
            spots.Add(new StationSpot(new Vector3(4.6f, y, 1.6f), Vector3.back));
            seats.AddRange(SeatRows());
        }

        // ------------------------------------------------------------------ trafik, gece ışıkları, pano

        void BuildTraffic()
        {
            Transform parent = new GameObject("Traffic").transform;
            parent.SetParent(Root, false);
            int perLane = isStop ? 3 : 5;
            for (int lane = 0; lane < 2; lane++)
            {
                float z = roadC + (lane == 0 ? -1.6f : 1.6f);
                float speed = lane == 0 ? 9f : -8f;
                for (int i = 0; i < perLane; i++)
                {
                    float x = -140f + (i + R(0f, 0.6f)) * 280f / perLane;
                    GameObject go;
                    if (rng.NextDouble() < 0.18)
                    {
                        Mesh bus = StationVehicles.Bus(rng.Next(6), out MeshLayer[] busLayers);
                        go = Place(bus, busLayers, "TrafficBus", parent, new Vector3(x, 0.02f, z), speed > 0f ? 90f : -90f);
                    }
                    else
                    {
                        Mesh car = StationVehicles.Car(rng.Next(6), out MeshLayer[] carLayers);
                        go = Place(car, carLayers, "Car", parent, new Vector3(x, 0.02f, z), speed > 0f ? 90f : -90f);
                    }
                    traffic.Add((go.transform, speed * R(0.85f, 1.15f)));
                }
            }
        }

        /// <summary>Gece lambaların altında yere düşen sıcak ışık havuzları (tek mesh, ışıksız).</summary>
        void BuildGlowPools()
        {
            if (glowPools != null)
            {
                ownedMeshes.Remove(glowPools.GetComponent<MeshFilter>().sharedMesh);
                Object.Destroy(glowPools.GetComponent<MeshFilter>().sharedMesh);
                Object.Destroy(glowPools.gameObject);
            }
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            foreach (var (position, radius) in lamps)
            {
                int start = vertices.Count;
                Vector3 c = position + Vector3.up * 0.02f;
                vertices.Add(c + new Vector3(-radius, 0f, -radius));
                vertices.Add(c + new Vector3(-radius, 0f, radius));
                vertices.Add(c + new Vector3(radius, 0f, radius));
                vertices.Add(c + new Vector3(radius, 0f, -radius));
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(0f, 1f));
                uvs.Add(new Vector2(1f, 1f));
                uvs.Add(new Vector2(1f, 0f));
                for (int i = 0; i < 4; i++) colors.Add(Color.white);
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            var mesh = new Mesh { name = "GlowPools" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            ownedMeshes.Add(mesh);
            if (glowMaterial == null)
                glowMaterial = new Material(MaterialFactory.LineMaterial) { name = "StationGlowPools", mainTexture = WorldRenderer.GlowTexture(), renderQueue = 2980 };
            var go = new GameObject("GlowPools");
            go.transform.SetParent(Root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            glowPools = go.AddComponent<MeshRenderer>();
            glowPools.sharedMaterial = glowMaterial;
            glowPools.shadowCastingMode = ShadowCastingMode.Off;
            glowPools.receiveShadows = false;
            glowPools.enabled = false;
        }

        void UpdateBoard(float hours)
        {
            if (boardRows.Count == 0) return;
            int start = Mathf.FloorToInt(hours * 60f / 15f) + 1; // sonraki çeyrekler
            for (int i = 0; i < boardRows.Count; i++)
            {
                string destination = Info.Destinations.Count > 0 ? Info.Destinations[i % Info.Destinations.Count] : "—";
                int minutes = ((start + i * 2) * 15) % (24 * 60);
                string text = $"{Upper(destination)}   {minutes / 60:00}:{minutes % 60:00}   P{1 + i % Mathf.Max(1, StationCatalog.Bays(Info.Tier))}";
                if (boardRows[i].text != text) StationSigns.SetText(boardRows[i], text, boardRowHeight, boardRowWidth); // panoya sığsın
            }
        }

        // ------------------------------------------------------------------ küçük parçalar

        static void TrashBin(MeshKit k, Vector3 p)
        {
            k.Cylinder(p, 0.28f, 0.85f, new Color(0.35f, 0.55f, 0.4f), 8);
            k.Cylinder(p + new Vector3(0f, 0.85f, 0f), 0.3f, 0.06f, Frame, 8);
        }

        static void Tree(MeshKit k, Vector3 p, float size)
        {
            k.Cylinder(p, 0.18f * size, 1.4f * size, Trunk, 6);
            k.Sphere(p + new Vector3(0f, 2.2f * size, 0f), 1.15f * size, Leaf, 8, 5);
            k.Sphere(p + new Vector3(0.45f * size, 1.75f * size, 0.3f * size), 0.75f * size, LeafDark, 7, 4);
        }

        void LampPost(MeshKit k, Vector3 p, bool fancy)
        {
            k.Cylinder(p, fancy ? 0.12f : 0.09f, fancy ? 4.2f : 4.6f, Frame, 6);
            k.Box(p + new Vector3(0f, fancy ? 4.25f : 4.6f, 0.35f), new Vector3(0.2f, 0.12f, 0.8f), Frame);
            k.Layer = MeshLayer.Glow;
            if (fancy) k.Sphere(p + new Vector3(0f, 4.45f, 0f), 0.35f, WarmLight, 8, 5);
            else k.Box(p + new Vector3(0f, 4.52f, 0.6f), new Vector3(0.3f, 0.06f, 0.4f), WarmLight);
            k.Layer = MeshLayer.Lit;
        }
    }
}
