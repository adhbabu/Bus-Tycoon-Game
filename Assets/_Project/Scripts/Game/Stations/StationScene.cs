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
    /// otogarlarda kesit görünümlü salon, peronlar ve park etmiş otobüsler, giriş meydanı, otopark, cadde ve çevre
    /// binaları. Kameraya bakan duvarlar alçak duvara dönüşür (içerisi hep görünür). Geliştirmeler ayrı nesnelerdir
    /// (StationScene.Upgrades.cs) ve satın alınınca sahneye eklenir. Kalabalık popülerliğe göre, yoldan geçen araçlar
    /// ve saatler oyun zamanıyla canlanır.
    ///
    /// Yükseklikler: çimen 0, asfalt <see cref="RoadY"/>, kaldırım taşı <see cref="P"/>, salon zemini <see cref="F"/>,
    /// peron <see cref="WalkY"/>. Üst üste binen yüzeyler aynı yükseklikte olmaz (titreşmesin); yer karoları üst üste
    /// değil yan yana dizilir.
    /// </summary>
    public sealed partial class StationScene
    {
        // ------------------------------------------------------------------ renkler (ışık ≈ 1 iken görünen ton)
        static readonly Color Grass = new Color(0.5f, 0.68f, 0.38f);
        static readonly Color GrassDark = new Color(0.43f, 0.6f, 0.33f);
        static readonly Color Hedge = new Color(0.31f, 0.54f, 0.3f);
        static readonly Color Paving = new Color(0.8f, 0.78f, 0.74f);
        static readonly Color PavingB = new Color(0.73f, 0.71f, 0.67f);
        static readonly Color PavingDark = new Color(0.66f, 0.64f, 0.6f);
        static readonly Color Asphalt = new Color(0.33f, 0.34f, 0.37f);
        static readonly Color LaneWhite = new Color(0.93f, 0.93f, 0.9f);
        static readonly Color LaneYellow = new Color(0.98f, 0.78f, 0.2f);
        static readonly Color Curb = new Color(0.88f, 0.87f, 0.84f);
        static readonly Color PlatformTop = new Color(0.83f, 0.81f, 0.77f);
        static readonly Color FloorA = new Color(0.9f, 0.87f, 0.82f);
        static readonly Color FloorB = new Color(0.79f, 0.76f, 0.72f);
        static readonly Color FloorBorder = new Color(0.58f, 0.53f, 0.49f);
        static readonly Color Mat = new Color(0.3f, 0.3f, 0.33f);
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
        static readonly Color WayBlue = new Color(0.13f, 0.3f, 0.55f);
        static readonly Color ExitGreen = new Color(0.12f, 0.58f, 0.33f);
        static readonly Color SignalRed = new Color(1f, 0.25f, 0.2f);
        static readonly Color SignalGreen = new Color(0.3f, 1f, 0.5f);
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

        // ------------------------------------------------------------------ yükseklikler ve ölçüler (metre)
        const float F = 0.18f;          // salon zemininin üstü
        const float P = 0.12f;          // meydan ve kaldırım taşlarının üstü
        const float RoadY = 0.02f;      // asfaltın üstü
        const float MarkY = 0.035f;     // yol çizgilerinin üstü
        const float WalkY = 0.26f;      // peron yürüme yolunun üstü
        const float WallT = 0.3f;       // duvar kalınlığı
        const float LowWall = 1.0f;     // kesit görünümde kameraya bakan duvarın yerine kalan alçak duvar
        const float DoorHeight = 3.1f;
        const float BayPitch = 4.6f;
        const float Tile = 3f;          // meydan taşı karesi
        const float PlatformDoorZ = 0f; // perona açılan kapı salonun doğu duvarının ortasında

        public Transform Root { get; }
        public StationInfo Info { get; }
        public Vector3 Focus { get; private set; }
        public float DefaultDistance { get; private set; }
        public float MinDistance { get; private set; }
        public float MaxDistance { get; private set; }
        public StationCrowd Crowd { get; private set; }

        readonly List<(GameObject Full, GameObject Low, Vector3 Normal)> walls = new List<(GameObject, GameObject, Vector3)>();
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

        // Yerleşim (salon: x0..x1, z0..z1; peron yürüme yolu: wx0..wx1 × zw0..zw1; caddenin kuzey kenarı roadN)
        readonly bool isStop;
        float W, D, H, x0, x1, z0, z1, doorW, platformDoorW, door;
        float wx0, wx1, zw0, zw1, roadN, roadC, lotX0, lotX1, lotZ1;
        float parkX0, parkX1, parkZ1, apronX1, taxiX0, taxiX1;
        readonly List<float> bayZ = new List<float>();
        readonly List<StationSpot> platformSeats = new List<StationSpot>();
        Color theme, themeDark, interior, wainscot;

        public StationScene(Transform parent, StationInfo info)
        {
            Info = info;
            isStop = info.Tier == StationTier.Stop;
            uint hash = 2166136261;
            foreach (char c in info.City != null ? info.City.Id : "X") hash = (hash ^ c) * 16777619;
            rng = new System.Random((int)(hash & 0x7fffffff));
            theme = Themes[(int)(hash % (uint)Themes.Length)];
            themeDark = Color.Lerp(theme, Color.black, 0.25f);
            interior = Color.Lerp(theme, White, 0.72f);
            wainscot = Color.Lerp(theme, new Color(0.3f, 0.32f, 0.38f), 0.42f);

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
                if (p.x > 150f) p.x -= 300f;
                if (p.x < -150f) p.x += 300f;
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

        /// <summary>Kameraya bakan duvarlar alçak duvara dönüşür (kesit görünüm): içerisi hep görünür.</summary>
        public void UpdateCutaway(Vector3 cameraLocal)
        {
            Vector3 toCamera = cameraLocal - new Vector3(door, 0f, (z0 + z1) / 2f);
            toCamera.y = 0f;
            toCamera.Normalize();
            foreach (var (full, low, normal) in walls)
            {
                bool show = Vector3.Dot(normal, toCamera) < 0.2f;
                if (full.activeSelf != show) full.SetActive(show);
                if (low.activeSelf == show) low.SetActive(!show);
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

        GameObject Make(MeshKit k, string name, Transform parent, bool shadows = true)
        {
            if (k.IsEmpty) return null;
            MeshRenderer renderer = k.BuildObject(name, parent != null ? parent : staticRoot, null, shadows);
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

        static float Yaw(Vector3 facing) => Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;

        /// <summary>Yere oturan dikdörtgen blok (alt yüzü 0'da).</summary>
        static void Slab(MeshKit k, Area a, float top, Color color) =>
            k.Box(new Vector3((a.X0 + a.X1) / 2f, top / 2f, (a.Z0 + a.Z1) / 2f), new Vector3(a.X1 - a.X0, top, a.Z1 - a.Z0), color);

        /// <summary>Asfalta boyanmış çizgi (asfaltın 1,5 cm üstünde).</summary>
        static void Mark(MeshKit k, float xa, float xb, float za, float zb, Color color) =>
            k.Box(new Vector3((xa + xb) / 2f, (RoadY + MarkY) / 2f, (za + zb) / 2f), new Vector3(Mathf.Abs(xb - xa), MarkY - RoadY, Mathf.Abs(zb - za)), color);

        // ------------------------------------------------------------------ alanlar (yer karoları, kaldırımlar)

        /// <summary>Eksene hizalı dikdörtgen alan (x0..x1 × z0..z1).</summary>
        readonly struct Area
        {
            public readonly float X0, X1, Z0, Z1;

            public Area(float x0, float x1, float z0, float z1)
            {
                X0 = Mathf.Min(x0, x1);
                X1 = Mathf.Max(x0, x1);
                Z0 = Mathf.Min(z0, z1);
                Z1 = Mathf.Max(z0, z1);
            }

            public bool Overlaps(Area o) => X0 < o.X1 - 0.001f && o.X0 < X1 - 0.001f && Z0 < o.Z1 - 0.001f && o.Z0 < Z1 - 0.001f;
        }

        /// <summary>Alanlardan bir dikdörtgeni çıkarır (kalan parçalar en fazla dört dikdörtgen olur).</summary>
        static List<Area> Subtract(List<Area> areas, Area hole)
        {
            var result = new List<Area>();
            foreach (Area a in areas)
            {
                if (!a.Overlaps(hole))
                {
                    result.Add(a);
                    continue;
                }
                if (hole.X0 > a.X0) result.Add(new Area(a.X0, hole.X0, a.Z0, a.Z1));
                if (hole.X1 < a.X1) result.Add(new Area(hole.X1, a.X1, a.Z0, a.Z1));
                float xa = Mathf.Max(a.X0, hole.X0), xb = Mathf.Min(a.X1, hole.X1);
                if (hole.Z0 > a.Z0) result.Add(new Area(xa, xb, a.Z0, hole.Z0));
                if (hole.Z1 < a.Z1) result.Add(new Area(xa, xb, hole.Z1, a.Z1));
            }
            return result;
        }

        /// <summary>Aralık listesinden [a, b] aralığını çıkarır.</summary>
        static List<(float, float)> Cut(List<(float, float)> parts, float a, float b)
        {
            var result = new List<(float, float)>();
            foreach (var (s0, s1) in parts)
            {
                if (b <= s0 || a >= s1)
                {
                    result.Add((s0, s1));
                    continue;
                }
                if (a > s0) result.Add((s0, a));
                if (b < s1) result.Add((b, s1));
            }
            return result;
        }

        /// <summary>İki tonlu kare taşlar: her taş kendi bloğudur (üst üste kaplama yok, titreşme olmaz).</summary>
        static void PaveTiles(MeshKit k, Area a)
        {
            int i0 = Mathf.FloorToInt(a.X0 / Tile), i1 = Mathf.CeilToInt(a.X1 / Tile);
            int j0 = Mathf.FloorToInt(a.Z0 / Tile), j1 = Mathf.CeilToInt(a.Z1 / Tile);
            for (int i = i0; i < i1; i++)
                for (int j = j0; j < j1; j++)
                {
                    float cx0 = Mathf.Max(a.X0, i * Tile), cx1 = Mathf.Min(a.X1, (i + 1) * Tile);
                    float cz0 = Mathf.Max(a.Z0, j * Tile), cz1 = Mathf.Min(a.Z1, (j + 1) * Tile);
                    if (cx1 - cx0 < 0.01f || cz1 - cz0 < 0.01f) continue;
                    Slab(k, new Area(cx0, cx1, cz0, cz1), P, ((i + j) & 1) == 0 ? Paving : PavingB);
                }
        }

        /// <summary>
        /// Asfaltın kaldırıma değdiği her kenara bordür: başka bir asfalt alanına bitişik kısımlar (cadde ağzı,
        /// otopark girişi) açık kalır. Bordür kaldırım tarafına, taşlardan 3 cm yüksek konur.
        /// </summary>
        static void Curbs(MeshKit k, List<Area> asphalt)
        {
            foreach (Area a in asphalt)
            {
                CurbEdge(k, asphalt, a.X0 - 0.3f, a.X1 + 0.3f, a.Z0, true, -1f);
                CurbEdge(k, asphalt, a.X0 - 0.3f, a.X1 + 0.3f, a.Z1, true, 1f);
                CurbEdge(k, asphalt, a.Z0, a.Z1, a.X0, false, -1f);
                CurbEdge(k, asphalt, a.Z0, a.Z1, a.X1, false, 1f);
            }
        }

        static void CurbEdge(MeshKit k, List<Area> asphalt, float from, float to, float at, bool alongX, float side)
        {
            var parts = new List<(float, float)> { (from, to) };
            float probe = at + side * 0.05f;
            foreach (Area o in asphalt)
            {
                bool touches = alongX ? probe > o.Z0 && probe < o.Z1 : probe > o.X0 && probe < o.X1;
                if (touches) parts = Cut(parts, alongX ? o.X0 : o.Z0, alongX ? o.X1 : o.Z1);
            }
            const float width = 0.3f, top = P + 0.03f;
            foreach (var (s0, s1) in parts)
            {
                if (s1 - s0 < 0.05f) continue;
                float c = at + side * width / 2f;
                if (alongX) k.Box(new Vector3((s0 + s1) / 2f, top / 2f, c), new Vector3(s1 - s0, top, width), Curb);
                else k.Box(new Vector3(c, top / 2f, (s0 + s1) / 2f), new Vector3(width, top, s1 - s0), Curb);
            }
        }

        // ------------------------------------------------------------------ otogar: yerleşim

        void BuildTerminalLayout()
        {
            switch (Info.Tier)
            {
                case StationTier.Terminal: W = 24f; D = 14f; H = 6.2f; doorW = 4f; platformDoorW = 4f; break;
                case StationTier.Large: W = 32f; D = 17f; H = 7.2f; doorW = 5f; platformDoorW = 5f; break;
                default: W = 42f; D = 21f; H = 8.8f; doorW = 6f; platformDoorW = 6f; break;
            }
            int bays = StationCatalog.Bays(Info.Tier);
            x0 = -(W + 18f) / 2f;
            x1 = x0 + W;
            z0 = -D / 2f;
            z1 = D / 2f;
            door = (x0 + x1) / 2f;
            wx0 = x1;
            wx1 = x1 + 3.5f;
            float span = bays * BayPitch;
            for (int i = 0; i < bays; i++) bayZ.Add(-span / 2f + BayPitch / 2f + i * BayPitch);
            zw0 = Mathf.Min(z0, -span / 2f - 1f);
            zw1 = Mathf.Max(z1, span / 2f + 1f);
            roadN = Mathf.Min(zw0 - 8f, z0 - 10f);
            roadC = roadN - 4.5f;
            parkX1 = x0 - 3.5f;
            parkX0 = parkX1 - 12f;
            parkZ1 = z1 + 1f;
            lotX0 = parkX0 - 2f;
            apronX1 = wx1 + 21f;
            lotX1 = apronX1 + 2f;
            lotZ1 = zw1 + 6f;
            taxiX0 = door + doorW / 2f + 4.2f;
            taxiX1 = Mathf.Min(taxiX0 + 17f, apronX1 - 10f);

            Focus = new Vector3((x0 + wx1) / 2f + 1.5f, 1.2f, (roadN + 4f + z1) / 2f);
            DefaultDistance = Info.Tier == StationTier.Terminal ? 31f : Info.Tier == StationTier.Large ? 38f : 47f;
            MinDistance = DefaultDistance * 0.4f;
            MaxDistance = DefaultDistance * 1.6f;

            BuildGround();
            BuildHall();
            BuildPlatforms();
            BuildPlaza();
            BuildCarPark();
            BuildSurroundings(Mathf.Max(lotX1 - lotX0, lotZ1 - roadN) * 0.5f + 4f);
            BuildTerminalGraph();
        }

        /// <summary>
        /// Zemin: çimen, cadde ve yan sokaklar, otopark, taksi cebi, peron önü ve otobüs giriş-çıkışı (asfalt);
        /// kalan her yer kare taş kaldırım. Bordürler asfaltın kenarlarından üretilir, yol çizgileri en üstte.
        /// </summary>
        void BuildGround()
        {
            var k = new MeshKit();
            k.Box(new Vector3(0f, -0.05f, 0f), new Vector3(340f, 0.1f, 340f), Grass);

            const float far = 170f;
            var lot = new Area(lotX0, lotX1, roadN, lotZ1);
            var asphalt = new List<Area>
            {
                new Area(-far, far, roadN - 9f, roadN),                // cadde
                new Area(lotX0 - 9f, lotX0 - 2f, roadN, far),          // yan sokaklar
                new Area(lotX1 + 2f, lotX1 + 9f, roadN, far),
                new Area(parkX0, parkX1, roadN + 3.6f, parkZ1),        // otopark ve girişi
                new Area(parkX1 - 6.5f, parkX1, roadN, roadN + 3.6f),
                new Area(taxiX0, taxiX1, roadN, roadN + 2.5f),         // yolcu indirme / taksi cebi
                new Area(wx1, apronX1, zw0 - 3f, zw1 + 4f),            // peron önü
                new Area(apronX1 - 9f, apronX1, roadN, zw0 - 3f),      // otobüs giriş-çıkışı
            };
            foreach (Area a in asphalt) Slab(k, a, RoadY, Asphalt);

            var paving = new List<Area> { lot, new Area(-far, far, roadN - 12.6f, roadN - 9f) };
            paving.AddRange(Subtract(new List<Area> { new Area(-far, far, roadN, roadN + 3.6f) }, lot));
            foreach (Area hole in asphalt) paving = Subtract(paving, hole);
            paving = Subtract(paving, new Area(x0 - WallT / 2f, x1 + WallT / 2f, z0 - WallT / 2f, z1 + WallT / 2f)); // salon
            paving = Subtract(paving, new Area(x1, wx1, zw0, zw1));                                                  // peron
            foreach (Area a in paving) PaveTiles(k, a);
            Curbs(k, asphalt);

            // Orta şerit (yaya geçidinde kesilir) ve girişin önünde yaya geçidi
            for (float x = -165f; x < 165f; x += 6f)
                if (Mathf.Abs(x + 1.5f - door) > 5.5f) Mark(k, x, x + 3f, roadC - 0.08f, roadC + 0.08f, LaneWhite);
            for (int i = -3; i <= 3; i++)
                Mark(k, door + i * 1.1f - 0.3f, door + i * 1.1f + 0.3f, roadN - 8.4f, roadN - 0.6f, LaneWhite);
            Make(k, "Ground", staticRoot, false);
        }

        // ------------------------------------------------------------------ otogar: salon

        void BuildHall()
        {
            BuildHallFloor();
            var k = new MeshKit();
            BaseFurniture(k);
            Make(k, "HallFurniture", staticRoot);
            BuildBaseBenches();
            BuildWalls();
            for (float u = 3f; u < W - 1f; u += 6f)
                for (float v = 3f; v < D - 1f; v += 5f)
                    lamps.Add((Hall(u, v), 3.4f));
            lamps.Add((new Vector3(door, P, z0 - 1.6f), 3f)); // giriş saçağının altı
        }

        float MedallionRadius => Mathf.Min(2.2f, D * 0.11f);
        /// <summary>Büfenin salondaki yeri (metroda VIP salonunun güneyinde kalsın).</summary>
        float KioskV => Info.Tier == StationTier.Metro ? D * 0.15f : D * 0.22f;
        Vector3 MedallionCenter => Hall(W / 2f, 1.2f + MedallionRadius);

        /// <summary>Salon zemini: koyu kenar bandı, iki tonlu büyük karolar, paspaslar ve şehrin amblemli madalyonu.</summary>
        void BuildHallFloor()
        {
            var k = new MeshKit();
            float h = WallT / 2f, b = 0.9f;
            Slab(k, new Area(x0 - h, x1 + h, z0 - h, z0 + b), F, FloorBorder);
            Slab(k, new Area(x0 - h, x1 + h, z1 - b, z1 + h), F, FloorBorder);
            Slab(k, new Area(x0 - h, x0 + b, z0 + b, z1 - b), F, FloorBorder);
            Slab(k, new Area(x1 - b, x1 + h, z0 + b, z1 - b), F, FloorBorder);
            int nu = Mathf.Max(1, Mathf.RoundToInt((W - 2f * b) / 2f)), nv = Mathf.Max(1, Mathf.RoundToInt((D - 2f * b) / 2f));
            float tu = (W - 2f * b) / nu, tv = (D - 2f * b) / nv;
            for (int i = 0; i < nu; i++)
                for (int j = 0; j < nv; j++)
                    Slab(k, new Area(x0 + b + i * tu, x0 + b + (i + 1) * tu, z0 + b + j * tv, z0 + b + (j + 1) * tv), F, ((i + j) & 1) == 0 ? FloorA : FloorB);

            k.Box(new Vector3(door, F + 0.0125f, z0 + 0.6f), new Vector3(doorW, 0.025f, 0.9f), Mat);
            k.Box(new Vector3(x1 - 0.6f, F + 0.0125f, PlatformDoorZ), new Vector3(0.9f, 0.025f, platformDoorW), Mat);

            float r = MedallionRadius;
            Vector3 m = MedallionCenter;
            k.Cylinder(m, r, 0.02f, themeDark, 28);
            k.Cylinder(m + Vector3.up * 0.02f, r - 0.22f, 0.015f, Color.Lerp(theme, White, 0.6f), 28);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                k.Cylinder(m + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (r - 0.11f), 0.06f, 0.03f, Orange, 6);
            }
            Make(k, "HallFloor", staticRoot, false);
            StationSigns.CityIcon(staticRoot, Info.City, m + Vector3.up * 0.05f, Quaternion.Euler(90f, 0f, 0f), (r - 0.3f) * 1.25f);
        }

        /// <summary>Bekleme bankları: koltuk geliştirmesi aynı yerlere minderli koltuk koyar (yolcular hangisi olursa otursun).</summary>
        void BuildBaseBenches()
        {
            var bench = new MeshKit();
            foreach (var seat in SeatRows()) BenchSeat(bench, seat);
            baseBenches = Make(bench, "BaseBenches", staticRoot);
        }

        static void BenchSeat(MeshKit k, StationSpot seat)
        {
            k.Push(seat.Position, Yaw(seat.Facing));
            k.Box(new Vector3(0f, 0.4f, 0.16f), new Vector3(0.62f, 0.06f, 0.46f), Metal);
            k.Box(new Vector3(0f, 0.68f, -0.11f), new Vector3(0.62f, 0.5f, 0.05f), Metal);
            k.Box(new Vector3(0f, 0.2f, 0.16f), new Vector3(0.05f, 0.4f, 0.4f), Frame);
            k.Pop();
        }

        /// <summary>Her otogarda olan eşyalar: otomatlar (kuzey duvarında, yüzleri salona) ve çöp kutuları.</summary>
        void BaseFurniture(MeshKit k)
        {
            for (int i = 0; i < 2; i++)
            {
                Vector3 p = Hall(W * 0.3f + (i - 0.5f) * 1.1f, D - 0.75f);
                k.Box(p + new Vector3(0f, 0.95f, 0f), new Vector3(1.0f, 1.9f, 0.8f), i == 0 ? new Color(0.85f, 0.2f, 0.2f) : new Color(0.2f, 0.45f, 0.8f));
                k.Box(p + new Vector3(0f, 1.94f, 0f), new Vector3(1.04f, 0.1f, 0.84f), Dark);
                k.Box(p + new Vector3(-0.12f, 1.2f, -0.41f), new Vector3(0.62f, 1.0f, 0.02f), WarmLight, MeshLayer.Glow);
                k.Box(p + new Vector3(0.34f, 1.15f, -0.41f), new Vector3(0.16f, 0.5f, 0.03f), Dark);
                k.Box(p + new Vector3(-0.1f, 0.35f, -0.41f), new Vector3(0.6f, 0.22f, 0.03f), Dark);
            }
            TrashBin(k, Hall(W / 2f - doorW / 2f - 2.2f, 0.55f));
            TrashBin(k, Hall(W / 2f + doorW / 2f + 2.2f, 0.55f));
            TrashBin(k, Hall(W * 0.7f + 1.1f, D - 0.6f));
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

        // ------------------------------------------------------------------ otogar: duvarlar

        enum WallStyle { Solid, Windows, Glass }

        /// <summary>
        /// Salonun bir duvarı, kendi çerçevesinde: s duvar boyunca (Along), y yukarı, d dışarı doğru (Normal; 0 duvarın
        /// ortası). Kuzey ve güney duvarları köşeleri de kapsar, batı ve doğu aralarında kalır.
        /// </summary>
        sealed class WallSide
        {
            public string Name;
            public Vector3 Origin, Along, Normal;
            public float From, To;
            public WallStyle Style;
            public readonly List<(float Center, float Width, float Height)> Openings = new List<(float, float, float)>();

            public Vector3 Point(float s, float y, float d) => Origin + Along * s + Vector3.up * y + Normal * d;
            /// <summary>Salonun içinden okunan yazının dönüşü (yazı yerel −z'den okunur).</summary>
            public float InteriorYaw => Mathf.Atan2(Normal.x, Normal.z) * Mathf.Rad2Deg;
            public bool CarriesCorners => Normal.z != 0f;
        }

        /// <summary>Duvar çerçevesinde kutu: s, y, d aralıkları.</summary>
        static void WB(MeshKit k, WallSide w, float s0, float s1, float y0, float y1, float d0, float d1, Color color, MeshLayer layer = MeshLayer.Lit)
        {
            Vector3 center = w.Point((s0 + s1) / 2f, (y0 + y1) / 2f, (d0 + d1) / 2f);
            Vector3 size = Abs(w.Along) * Mathf.Abs(s1 - s0) + Vector3.up * Mathf.Abs(y1 - y0) + Abs(w.Normal) * Mathf.Abs(d1 - d0);
            k.Box(center, size, color, layer);
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        static List<(float, float)> SolidParts(WallSide w)
        {
            var parts = new List<(float, float)> { (w.From, w.To) };
            foreach (var (c, width, _) in w.Openings) parts = Cut(parts, c - width / 2f, c + width / 2f);
            return parts;
        }

        /// <summary>
        /// Dört duvar, her biri iki hâliyle: tam yükseklik (lambri, sıva, pencereler, ışık bandı, saçak, tabelalar) ve
        /// kesit görünüm için alçak duvar (üstü koyu başlıklı). Kuzey: tabela ve reklam panosu; doğu (peronlar): cam
        /// cephe, saat ve "PERONLAR"; güney: giriş, saçak ve "ÇIKIŞ"; batı: üst pencereler.
        /// </summary>
        void BuildWalls()
        {
            float h = WallT / 2f;
            var north = new WallSide { Name = "North", Origin = new Vector3(0f, 0f, z1), Along = Vector3.right, Normal = Vector3.forward, From = x0 - h, To = x1 + h, Style = WallStyle.Solid };
            var south = new WallSide { Name = "South", Origin = new Vector3(0f, 0f, z0), Along = Vector3.right, Normal = Vector3.back, From = x0 - h, To = x1 + h, Style = WallStyle.Windows };
            var west = new WallSide { Name = "West", Origin = new Vector3(x0, 0f, 0f), Along = Vector3.forward, Normal = Vector3.left, From = z0 + h, To = z1 - h, Style = WallStyle.Windows };
            var east = new WallSide { Name = "East", Origin = new Vector3(x1, 0f, 0f), Along = Vector3.forward, Normal = Vector3.right, From = z0 + h, To = z1 - h, Style = WallStyle.Glass };
            south.Openings.Add((door, doorW, DoorHeight));
            east.Openings.Add((PlatformDoorZ, platformDoorW, DoorHeight));
            foreach (var side in new[] { north, south, west, east })
            {
                GameObject full = BuildWall(side, true);
                GameObject low = BuildWall(side, false);
                low.SetActive(false);
                walls.Add((full, low, side.Normal));
                if (side == north) northWall = full.transform;
                WallSigns(side, full.transform);
            }
        }

        GameObject BuildWall(WallSide w, bool full)
        {
            var k = new MeshKit();
            float h = WallT / 2f, top = F + (full ? H : LowWall);
            bool glass = full && w.Style == WallStyle.Glass;
            foreach (var (a, b) in SolidParts(w))
            {
                float body = glass ? F + 0.5f : top;
                WB(k, w, a, b, 0f, body, -h, h, theme);
                WB(k, w, a, b, 0f, 0.5f, h, h + 0.04f, themeDark);                                     // dış süpürgelik
                if (body > F + 1.1f) WB(k, w, a, b, F + 1.1f, body, -h - 0.04f, -h, interior);         // iç sıva
                WB(k, w, a, b, F, Mathf.Min(F + 1.1f, body), -h - 0.06f, -h, wainscot);                // lambri
                if (full && !glass) WB(k, w, a, b, F + 1.1f, F + 1.17f, -h - 0.09f, -h, White);        // lambri pervazı
                if (glass) GlassPane(k, w, a, b, F + 0.5f, top - 1.3f);
                else if (full && w.Style == WallStyle.Windows) WindowBand(k, w, a, b, F + 2.9f, top - 1.0f);
                if (!full) WB(k, w, a, b, top, top + 0.08f, -h - 0.08f, h + 0.08f, themeDark);           // kesit başlığı
            }
            foreach (var (c, width, height) in w.Openings)
            {
                float l = c - width / 2f, r = c + width / 2f;
                WB(k, w, l, r, F, F + 0.012f, -h, h, Metal);                                           // eşik
                if (!full) continue;
                if (glass)
                {
                    GlassPane(k, w, l, r, F + height + 0.14f, top - 1.3f);
                }
                else
                {
                    WB(k, w, l, r, F + height, top, -h, h, theme);
                    WB(k, w, l, r, F + height, top, -h - 0.04f, -h, interior);
                }
                WB(k, w, l - 0.12f, l, 0f, F + height, -h - 0.07f, h + 0.07f, Frame);                   // kapı kasası
                WB(k, w, r, r + 0.12f, 0f, F + height, -h - 0.07f, h + 0.07f, Frame);
                WB(k, w, l - 0.12f, r + 0.12f, F + height, F + height + 0.14f, -h - 0.07f, h + 0.07f, Frame);
            }
            if (full)
            {
                if (glass)
                {
                    WB(k, w, w.From, w.To, top - 1.3f, top, -h, h, theme);                             // alın bandı
                    WB(k, w, w.From, w.To, top - 1.3f, top, -h - 0.04f, -h, interior);
                }
                WB(k, w, w.From, w.To, top - 0.42f, top - 0.3f, -h - 0.1f, -h - 0.04f, WarmLight, MeshLayer.Glow); // ışık bandı
                float c0 = w.CarriesCorners ? w.From - 0.12f : w.From + 0.12f, c1 = w.CarriesCorners ? w.To + 0.12f : w.To - 0.12f;
                WB(k, w, c0, c1, top, top + 0.3f, -h - 0.12f, h + 0.12f, themeDark);                      // saçak
                if (w.CarriesCorners)
                    foreach (float s in new[] { w.From + 0.25f, w.To - 0.25f })
                        WB(k, w, s - 0.3f, s + 0.3f, 0f, top + 0.34f, -h - 0.15f, h + 0.15f, themeDark);  // köşe ayakları
                WallDetails(k, w, top);
            }
            return Make(k, "Wall" + w.Name + (full ? string.Empty : "Low"), staticRoot);
        }

        /// <summary>Cam cephe: tek yüzlü cam, dikmeler, alt-üst kayıtlar ve kapı hizasında yatay kayıt.</summary>
        static void GlassPane(MeshKit k, WallSide w, float a, float b, float y0, float y1)
        {
            if (b - a < 0.05f || y1 - y0 < 0.05f) return;
            k.Layer = MeshLayer.Glass;
            k.Face(w.Point((a + b) / 2f, (y0 + y1) / 2f, 0f), w.Normal, w.Along * ((b - a) / 2f), Vector3.up * ((y1 - y0) / 2f), White);
            k.Layer = MeshLayer.Lit;
            int n = Mathf.Max(1, Mathf.RoundToInt((b - a) / 1.8f));
            for (int i = 0; i <= n; i++)
            {
                float s = a + (b - a) * i / n;
                WB(k, w, s - 0.05f, s + 0.05f, y0, y1, -0.07f, 0.07f, Frame);
            }
            WB(k, w, a, b, y0, y0 + 0.08f, -0.07f, 0.07f, Frame);
            WB(k, w, a, b, y1 - 0.08f, y1, -0.07f, 0.07f, Frame);
            float transom = F + DoorHeight + 0.07f;
            if (transom > y0 + 0.3f && transom < y1 - 0.3f) WB(k, w, a, b, transom - 0.04f, transom + 0.04f, -0.07f, 0.07f, Frame);
        }

        /// <summary>Üst pencere bandı: iki yüzde cam (gündüz gök, gece sıcak ışık), beyaz denizlik, lento ve dikmeler.</summary>
        static void WindowBand(MeshKit k, WallSide w, float a, float b, float y0, float y1)
        {
            float s0 = a + 0.7f, s1 = b - 0.7f;
            if (s1 - s0 < 1.2f || y1 - y0 < 0.8f) return;
            float h = WallT / 2f;
            WB(k, w, s0, s1, y0, y1, h, h + 0.02f, White, MeshLayer.Window);
            WB(k, w, s0, s1, y0, y1, -h - 0.06f, -h - 0.04f, White, MeshLayer.Window);
            int n = Mathf.Max(1, Mathf.RoundToInt((s1 - s0) / 2.2f));
            foreach (bool inside in new[] { true, false })
            {
                float dIn = inside ? -h - 0.04f : h, dOut = inside ? -h - 0.09f : h + 0.05f;
                WB(k, w, s0 - 0.08f, s1 + 0.08f, y0 - 0.1f, y0, dIn, dOut, White);
                WB(k, w, s0 - 0.08f, s1 + 0.08f, y1, y1 + 0.1f, dIn, dOut, White);
                for (int i = 0; i <= n; i++)
                {
                    float s = s0 + (s1 - s0) * i / n;
                    WB(k, w, s - 0.05f, s + 0.05f, y0, y1, dIn, dOut, White);
                }
            }
        }

        // Kuzey duvarındaki ad tabelasının ölçüleri (yazı genişliği ölçülerek)
        string NameText => Upper(CityName) + (Info.Tier == StationTier.Metro ? " TERMİNALİ" : " OTOGARI");
        float NameHeight => Mathf.Min(1.0f, H * 0.12f);
        float NamePanelHeight => NameHeight + 0.3f;
        float NameY => F + H - 0.55f - NamePanelHeight / 2f;
        float NameTextWidth => Mathf.Min(Mathf.Max(StationSigns.Width(NameText, NameHeight), 3f), W * 0.6f);

        /// <summary>Tam duvarın üstündeki ayrıntılar (aynı mesh): tabela zeminleri, reklam panosu, giriş saçağı.</summary>
        void WallDetails(MeshKit k, WallSide w, float top)
        {
            float h = WallT / 2f;
            switch (w.Name)
            {
                case "North":
                {
                    float icon = NameHeight * 1.4f, panel = NameTextWidth + icon + 1.4f;
                    WB(k, w, door - panel / 2f, door + panel / 2f, NameY - NamePanelHeight / 2f, NameY + NamePanelHeight / 2f, -h - 0.09f, -h - 0.04f, White);
                    WB(k, w, door - panel / 2f, door + panel / 2f, NameY - NamePanelHeight / 2f - 0.08f, NameY - NamePanelHeight / 2f, -h - 0.1f, -h - 0.04f, Orange);
                    AdLightbox(k, x0 + W * 0.7f, z1 - h - 0.04f);
                    break;
                }
                case "East":
                    WB(k, w, PlatformDoorZ - 1.4f, PlatformDoorZ + 1.4f, F + 3.35f, F + 3.85f, -0.17f, -0.08f, WayBlue);
                    break;
                case "South":
                {
                    WB(k, w, door - 0.8f, door + 0.8f, F + 3.35f, F + 3.75f, -h - 0.12f, -h - 0.04f, ExitGreen);
                    // Giriş saçağı (iki dikmeli) ve altındaki ışık
                    float half = doorW / 2f + 1.6f;
                    WB(k, w, door - half, door + half, F + 3.45f, F + 3.7f, h, h + 2.9f, White);
                    WB(k, w, door - half, door + half, F + 3.3f, F + 3.95f, h + 2.9f, h + 3.05f, Orange);
                    WB(k, w, door - doorW / 2f, door + doorW / 2f, F + 3.42f, F + 3.45f, h + 0.5f, h + 2.5f, WarmLight, MeshLayer.Glow);
                    foreach (float s in new[] { door - half + 0.3f, door + half - 0.3f })
                        k.Cylinder(w.Point(s, P, h + 2.7f), 0.09f, F + 3.45f - P, Frame, 8);
                    break;
                }
            }
        }

        /// <summary>Işıklı reklam panosu (kuzey duvarı, salona bakar): deniz, güneş, yelkenli ve alt şerit.</summary>
        void AdLightbox(MeshKit k, float x, float wallFace)
        {
            const float y0 = F + 1.0f, y1 = F + 2.9f;
            k.Box(new Vector3(x, (y0 + y1) / 2f, wallFace - 0.06f), new Vector3(1.5f, y1 - y0, 0.12f), Dark);
            float z = wallFace - 0.125f;
            k.Layer = MeshLayer.Glow;
            k.Box(new Vector3(x, (y0 + y1) / 2f, z), new Vector3(1.3f, y1 - y0 - 0.2f, 0.01f), new Color(0.58f, 0.82f, 0.98f));
            k.Box(new Vector3(x, y0 + 0.55f, z - 0.006f), new Vector3(1.3f, 0.5f, 0.01f), new Color(0.2f, 0.5f, 0.85f));
            k.Push(new Vector3(x + 0.3f, y1 - 0.55f, z - 0.012f), Quaternion.Euler(90f, 0f, 0f));
            k.Cylinder(new Vector3(0f, -0.005f, 0f), 0.22f, 0.005f, new Color(1f, 0.82f, 0.3f), 16, true, true);
            k.Pop();
            k.Triangle(new Vector3(x - 0.25f, y0 + 0.82f, z - 0.012f), new Vector3(x - 0.25f, y0 + 1.35f, z - 0.012f), new Vector3(x + 0.05f, y0 + 0.82f, z - 0.012f), Vector3.back, White);
            k.Box(new Vector3(x - 0.2f, y0 + 0.78f, z - 0.012f), new Vector3(0.5f, 0.08f, 0.01f), new Color(0.85f, 0.3f, 0.25f));
            k.Box(new Vector3(x, y0 + 0.22f, z - 0.012f), new Vector3(1.3f, 0.24f, 0.01f), Orange);
            k.Layer = MeshLayer.Lit;
        }

        /// <summary>Tam duvarla birlikte gizlenen yazılar ve saat.</summary>
        void WallSigns(WallSide w, Transform parent)
        {
            float h = WallT / 2f;
            switch (w.Name)
            {
                case "North":
                {
                    float icon = NameHeight * 1.4f, total = NameTextWidth + 0.35f + icon;
                    float left = door - total / 2f;
                    StationSigns.CityIcon(parent, Info.City, w.Point(left + icon / 2f, NameY, -h - 0.11f), w.InteriorYaw, icon);
                    StationSigns.Text(parent, NameText, w.Point(left + icon + 0.35f + NameTextWidth / 2f, NameY, -h - 0.11f), w.InteriorYaw, NameHeight, NameTextWidth, Orange);
                    StationSigns.Text(parent, "TÜRKİYE'Yİ GEZ", new Vector3(x0 + W * 0.7f, F + 1.22f, z1 - h - 0.19f), 0f, 0.16f, 1.2f, White);
                    break;
                }
                case "East":
                    StationSigns.Text(parent, "PERONLAR", w.Point(PlatformDoorZ, F + 3.6f, -0.18f), w.InteriorYaw, 0.32f, 2.5f, White);
                    WallClock(parent, w.Point(PlatformDoorZ, F + H - 0.72f, -h - 0.1f), w.InteriorYaw, 0.42f);
                    break;
                case "South":
                    StationSigns.Text(parent, "ÇIKIŞ", w.Point(door, F + 3.55f, -h - 0.13f), w.InteriorYaw, 0.26f, 1.4f, White);
                    StationSigns.Text(parent, NameText, w.Point(door, F + 3.62f, h + 3.07f), 0f, 0.4f, doorW + 2.8f, White);
                    break;
            }
        }

        /// <summary>Duvar saati: kasnak, kadran, çentikler; akrep ve yelkovan oyun saatini gösterir.</summary>
        void WallClock(Transform parent, Vector3 center, float yaw, float radius)
        {
            var face = new GameObject("WallClock").transform;
            face.SetParent(parent, false);
            face.localPosition = center;
            face.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var dial = new MeshKit();
            dial.Push(Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
            dial.Cylinder(new Vector3(0f, -0.02f, 0f), radius + 0.07f, 0.08f, Frame, 24, true, true);
            dial.Cylinder(new Vector3(0f, -0.03f, 0f), radius, 0.02f, White, 24, false, true);
            dial.Pop();
            for (int i = 0; i < 12; i++)
            {
                dial.Push(Vector3.zero, Quaternion.Euler(0f, 0f, i * 30f));
                dial.Box(new Vector3(0f, radius * 0.82f, -0.04f), new Vector3(0.05f, i % 3 == 0 ? 0.14f : 0.08f, 0.015f), Dark);
                dial.Pop();
            }
            Make(dial, "Dial", face);
            clockHands.Add((Hand(face, radius * 0.5f, 0.07f, -0.06f), Hand(face, radius * 0.78f, 0.045f, -0.075f)));
        }

        // ------------------------------------------------------------------ otogar: peronlar

        void BuildPlatforms()
        {
            var k = new MeshKit();
            float zc = (zw0 + zw1) / 2f, len = zw1 - zw0;
            Slab(k, new Area(x1, wx1, zw0, zw1), WalkY, PlatformTop);
            k.Box(new Vector3(wx1 - 0.15f, WalkY + 0.0075f, zc), new Vector3(0.3f, 0.015f, len), Curb);       // kenar taşı
            k.Box(new Vector3(wx1 - 0.6f, WalkY + 0.0075f, zc), new Vector3(0.3f, 0.015f, len), LaneYellow);  // hissedilebilir şerit

            for (int i = 0; i < bayZ.Count; i++)
            {
                // Peron levhası: direk ve iki yüzü yazılı turuncu levha
                Vector3 post = new Vector3(wx1 - 1.05f, WalkY, bayZ[i] - BayPitch / 2f + 0.3f);
                k.Box(post + new Vector3(0f, 1.4f, 0f), new Vector3(0.12f, 2.8f, 0.12f), Frame);
                k.Box(post + new Vector3(0f, 2.75f, 0.9f), new Vector3(0.1f, 0.7f, 1.6f), Orange);
                StationSigns.Text(staticRoot, "PERON " + (i + 1), post + new Vector3(-0.07f, 2.75f, 0.9f), 90f, 0.4f, 1.45f, White);
                StationSigns.Text(staticRoot, "PERON " + (i + 1), post + new Vector3(0.07f, 2.75f, 0.9f), -90f, 0.4f, 1.45f, White);
                // Bekleme bankı (çift peronlarda) ya da çöp kutusu
                if (i % 2 == 0)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        var seat = new StationSpot(new Vector3(x1 + 0.62f, WalkY, bayZ[i] + 0.4f + (j - 1) * 0.64f), Vector3.right);
                        BenchSeat(k, seat);
                        platformSeats.Add(seat);
                    }
                }
                else TrashBin(k, new Vector3(x1 + 0.55f, WalkY, bayZ[i] + 1.0f));
            }

            // Peron önü: park çizgileri, peron numaraları, manevra yolunda ve çıkışta oklar
            for (int i = 0; i <= bayZ.Count; i++)
            {
                float z = bayZ[0] - BayPitch / 2f + i * BayPitch;
                Mark(k, wx1 + 0.3f, wx1 + 13.5f, z - 0.075f, z + 0.075f, LaneWhite);
            }
            float aisle = (wx1 + 13.5f + apronX1) / 2f;
            for (float z = zw1 - 1f; z > zw0 - 1f; z -= 9f) Arrow(k, aisle, z, Vector3.back);
            Arrow(k, apronX1 - 4.5f, (roadN + zw0 - 3f) / 2f, Vector3.back);
            Make(k, "Platforms", staticRoot);
            for (int i = 0; i < bayZ.Count; i++)
                StationSigns.Text(staticRoot, (i + 1).ToString(), new Vector3(wx1 + 14.5f, MarkY + 0.005f, bayZ[i]), Quaternion.Euler(90f, 0f, 0f), 1.1f, 1.2f, LaneYellow);

            // Park etmiş otobüsler (popülerliğe göre dolu peron)
            for (int i = 0; i < Info.BaySegments.Count && i < bayZ.Count; i++)
            {
                int segment = Info.BaySegments[i];
                Mesh bus = StationVehicles.Bus(segment, out MeshLayer[] layers);
                float length = StationVehicles.BusLength(segment);
                Place(bus, layers, "Bus", staticRoot, new Vector3(wx1 + 0.7f + length / 2f, RoadY, bayZ[i]), -90f);
            }
        }

        /// <summary>Asfalta boyanmış yön oku (gövde ve üçgen uç).</summary>
        static void Arrow(MeshKit k, float x, float z, Vector3 direction)
        {
            Vector3 d = direction.normalized, side = new Vector3(d.z, 0f, -d.x);
            Vector3 c = new Vector3(x, MarkY, z);
            Mark(k, x - Mathf.Abs(d.x) * 1.1f - Mathf.Abs(side.x) * 0.15f, x + Mathf.Abs(d.x) * 1.1f + Mathf.Abs(side.x) * 0.15f,
                z - Mathf.Abs(d.z) * 1.1f - Mathf.Abs(side.z) * 0.15f, z + Mathf.Abs(d.z) * 1.1f + Mathf.Abs(side.z) * 0.15f, LaneWhite);
            k.Triangle(c + d * 1.1f + side * 0.6f, c + d * 2.1f, c + d * 1.1f - side * 0.6f, Vector3.up, LaneWhite);
        }

        // ------------------------------------------------------------------ otogar: meydan, otopark, çevre

        void BuildPlaza()
        {
            var k = new MeshKit();
            // Tabela direği: şehir adı ve ikonu
            Vector3 pylon = new Vector3(x0 + 1.8f, P, roadN + 6.2f);
            k.Box(pylon + new Vector3(0f, 0.25f, 0f), new Vector3(3.6f, 0.5f, 1.0f), themeDark);
            k.Box(pylon + new Vector3(0f, 2.75f, 0f), new Vector3(3.2f, 4.5f, 0.6f), theme);
            k.Box(pylon + new Vector3(0f, 5.15f, 0f), new Vector3(3.4f, 0.3f, 0.8f), themeDark);
            k.Box(pylon + new Vector3(0f, 2.35f, -0.31f), new Vector3(2.9f, 2.1f, 0.02f), White);
            Make(k, "Pylon", staticRoot);
            StationSigns.CityIcon(staticRoot, Info.City, pylon + new Vector3(0f, 4.15f, -0.33f), 0f, 1.2f);
            StationSigns.Text(staticRoot, Upper(CityName), pylon + new Vector3(0f, 2.8f, -0.34f), 0f, 0.62f, 2.7f, Dark);
            StationSigns.Text(staticRoot, Info.Tier == StationTier.Metro ? "TERMİNAL" : "OTOGAR", pylon + new Vector3(0f, 1.85f, -0.34f), 0f, 0.42f, 2.6f, Orange);
            spots.Add(new StationSpot(pylon + new Vector3(0.3f, 0f, -1.6f), Vector3.forward));

            var p = new MeshKit();
            TreeSeat(p, new Vector3(x0 + 5.6f, P, roadN + 6.2f));
            TreeSeat(p, new Vector3(x1 - 3.5f, P, roadN + 6.2f));
            foreach (float x in new[] { door - doorW / 2f - 3.2f, door + doorW / 2f + 3.2f })
            {
                LampPost(p, new Vector3(x, P, roadN + 0.9f), false);
                lamps.Add((new Vector3(x, P, roadN + 0.9f), 4f));
            }
            for (float x = parkX1 + 1f; x < door - doorW / 2f - 4f; x += 2f) p.Cylinder(new Vector3(x, P, roadN + 0.6f), 0.13f, 0.8f, Frame, 6); // babalar
            TrashBin(p, new Vector3(door + doorW / 2f + 1.2f, P, z0 - 1.2f));
            // Yaya geçidinde ışıklar (iki kaldırımda)
            CrossingSignal(p, new Vector3(door + 3.9f, P, roadN + 0.5f), 0f);
            CrossingSignal(p, new Vector3(door - 3.9f, P, roadN - 9.5f), 180f);
            Make(p, "PlazaProps", staticRoot);
        }

        /// <summary>Gölgelik ağaç ve çevresinde oturma halkası; halkada oturanlar dışarı bakar.</summary>
        void TreeSeat(MeshKit k, Vector3 p)
        {
            k.Cylinder(p, 1.0f, 0.42f, PavingDark, 12);
            k.Cylinder(p + Vector3.up * 0.42f, 1.06f, 0.06f, Wood, 12);
            k.Cylinder(p + Vector3.up * 0.42f, 0.72f, 0.07f, GrassDark, 12);
            Tree(k, p + Vector3.up * 0.45f, 1.3f);
            foreach (float angle in new[] { -90f, -35f, -145f })
            {
                Vector3 dir = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad));
                seats.Add(new StationSpot(p + dir * 0.82f + Vector3.up * 0.06f, dir));
            }
        }

        static void CrossingSignal(MeshKit k, Vector3 p, float yaw)
        {
            k.Push(p, yaw);
            k.Cylinder(Vector3.zero, 0.07f, 2.9f, Frame, 6);
            k.Box(new Vector3(0f, 2.55f, -0.12f), new Vector3(0.36f, 0.8f, 0.24f), Dark);
            k.Layer = MeshLayer.Glow;
            k.Box(new Vector3(0f, 2.75f, -0.25f), new Vector3(0.2f, 0.2f, 0.02f), SignalRed);
            k.Box(new Vector3(0f, 2.35f, -0.25f), new Vector3(0.2f, 0.2f, 0.02f), SignalGreen);
            k.Layer = MeshLayer.Lit;
            k.Pop();
        }

        /// <summary>Salonun batısında otopark: park çizgileri, park etmiş arabalar, çit ve "P" levhası.</summary>
        void BuildCarPark()
        {
            var k = new MeshKit();
            float start = roadN + 3.9f;
            int spaces = Mathf.FloorToInt((parkZ1 - 0.3f - start) / 2.7f);
            for (int i = 0; i <= spaces; i++)
                Mark(k, parkX0 + 0.2f, parkX0 + 5.2f, start + i * 2.7f - 0.06f, start + i * 2.7f + 0.06f, LaneWhite);
            for (int i = 0; i < spaces; i++)
            {
                if (rng.NextDouble() < 0.33) continue;
                Mesh car = StationVehicles.Car(rng.Next(6), out MeshLayer[] layers);
                Place(car, layers, "ParkedCar", staticRoot, new Vector3(parkX0 + 2.75f, RoadY, start + (i + 0.5f) * 2.7f), -90f);
            }
            Arrow(k, parkX1 - 3.25f, roadN + 2f, Vector3.forward);
            k.Box(new Vector3(lotX0 + 1f, P + 0.45f, (roadN + 3.6f + parkZ1) / 2f), new Vector3(1.0f, 0.9f, parkZ1 - roadN - 3.6f), Hedge);
            Vector3 sign = new Vector3(parkX1 + 0.6f, P, roadN + 4.4f);
            k.Box(sign + new Vector3(0f, 1.25f, 0f), new Vector3(0.1f, 2.5f, 0.1f), Frame);
            k.Box(sign + new Vector3(0f, 2.55f, 0f), new Vector3(0.75f, 0.75f, 0.08f), WayBlue);
            Make(k, "CarPark", staticRoot);
            StationSigns.Text(staticRoot, "P", sign + new Vector3(0f, 2.55f, -0.05f), 0f, 0.55f, 0.6f, White);
            StationSigns.Text(staticRoot, "P", sign + new Vector3(0f, 2.55f, 0.05f), 180f, 0.55f, 0.6f, White);
        }

        /// <summary>Arsanın çevresinde şehir dokusu: büyük şehirde yüksek bloklar, küçükte bahçeli evler; ağaçlar.</summary>
        void BuildSurroundings(float inner)
        {
            var k = new MeshKit();
            int count = isStop ? 14 : 22;
            float maxHeight = Info.Tier switch { StationTier.Stop => 8f, StationTier.Terminal => 14f, StationTier.Large => 22f, _ => 34f };
            for (int i = 0; i < count; i++)
            {
                float w = R(9f, 16f), d = R(9f, 14f), h = isStop ? R(4.5f, maxHeight) : R(6f, maxHeight);
                Vector3 c = default;
                bool placed = false;
                for (int attempt = 0; attempt < 16 && !placed; attempt++)
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
            for (int i = 0; i < 30; i++)
            {
                float angle = R(0f, Mathf.PI * 2f), radius = inner + R(-6f, 12f);
                var c = new Vector3(Focus.x + Mathf.Cos(angle) * radius, 0f, Focus.z + Mathf.Sin(angle) * radius);
                if (IsFree(c, 2f)) Tree(k, c, R(1.2f, 2f));
            }
            Make(k, "City", staticRoot);
        }

        /// <summary>Çevre binası/ağacı için yer boş mu: durak arsası, cadde, kaldırımlar ve yan sokaklar dışında.</summary>
        bool IsFree(Vector3 c, float radius)
        {
            if (c.z + radius > roadN - 13.5f && c.z - radius < roadN + (isStop ? 6.2f : 4.5f)) return false; // cadde ve kaldırımlar
            if (!isStop)
            {
                if (c.x + radius > lotX0 - 1f && c.x - radius < lotX1 + 1f && c.z - radius < lotZ1 + 2f) return false;  // arsa
                if (Mathf.Abs(c.x - (lotX0 - 5.5f)) < radius + 4.5f || Mathf.Abs(c.x - (lotX1 + 5.5f)) < radius + 4.5f)
                    return c.z < roadN - 13.5f;                                                                       // yan sokaklar
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
            MaxDistance = 30f;

            var k = new MeshKit();
            k.Box(new Vector3(0f, -0.05f, 0f), new Vector3(300f, 0.1f, 300f), Grass);
            k.Box(new Vector3(0f, 0.01f, roadC), new Vector3(300f, 0.02f, 9f), Asphalt);
            for (float x = -150f; x < 150f; x += 6f) Mark(k, x, x + 3f, roadC - 0.08f, roadC + 0.08f, LaneWhite);
            k.Box(new Vector3(0f, 0.09f, 2.6f), new Vector3(300f, 0.18f, 5.2f), Paving);
            k.Box(new Vector3(0f, 0.1f, 0.12f), new Vector3(300f, 0.2f, 0.25f), Curb);
            k.Box(new Vector3(0f, 0.06f, roadC - 6f), new Vector3(300f, 0.12f, 3.2f), PavingDark);
            // Otobüs cebi çizgisi
            Mark(k, -4f, 16f, -3.08f, -2.92f, LaneYellow);
            for (int i = 0; i < 5; i++) Mark(k, -3f + i * 4.5f - 1.1f, -3f + i * 4.5f + 1.1f, -1.68f, -1.52f, LaneYellow);
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
                Place(bus, layers, "Bus", staticRoot, new Vector3(12.5f, RoadY, -1.7f), -90f); // sundurmayı kapatmasın: durağa yanaşıyor
            }

            BuildSurroundings(22f);
            BuildStopGraph();
        }

        // ------------------------------------------------------------------ yürüyüş ağı ve bekleme yerleri

        /// <summary>
        /// Yolcuların yürüdüğü ağ: kaldırım boyunca yol, girişten salona, salonun koridorları (koltuk sıralarının iki
        /// yanından), perona açılan kapı ve peron boyunca. Duraklar (stop) bekleme noktasıdır.
        /// </summary>
        void BuildTerminalGraph()
        {
            float walkX = x1 + 1.8f, pathZ = roadN + 3.4f;
            int pPark = graph.Add(new Vector3(x0 - 1.8f, P, pathZ));
            int pDoor = graph.Add(new Vector3(door, P, pathZ), true);
            int pEast = graph.Add(new Vector3(walkX, P, pathZ));
            int crossing = graph.Add(new Vector3(door, P, roadN + 0.9f), true);
            int carSide = graph.Add(new Vector3(x0 - 1.8f, P, z0 + 1f));
            int outside = graph.Add(new Vector3(door, P, z0 - 1.6f));
            int inside = graph.Add(Hall(W / 2f, 1.5f));
            int frontW = graph.Add(Hall(W * 0.29f, D * 0.18f), true);
            int frontE = graph.Add(Hall(W * 0.71f, D * 0.18f), true);
            int midW = graph.Add(Hall(W * 0.29f, D * 0.5f));
            int midE = graph.Add(Hall(W * 0.71f, D * 0.5f));
            int backW = graph.Add(Hall(W * 0.29f, D - 3.6f));
            int backC = graph.Add(Hall(W * 0.5f, D - 3.6f), true);
            int backE = graph.Add(Hall(W * 0.71f, D - 3.6f));
            graph.Link(pPark, pDoor);
            graph.Link(pDoor, pEast);
            graph.Link(pDoor, crossing);
            graph.Link(pPark, carSide);
            graph.Link(pDoor, outside);
            graph.Link(outside, inside);
            graph.Link(inside, frontW);
            graph.Link(inside, frontE);
            graph.Link(frontW, midW);
            graph.Link(midW, backW);
            graph.Link(frontE, midE);
            graph.Link(midE, backE);
            graph.Link(backW, backC);
            graph.Link(backC, backE);

            // Perona açılan kapı ve peron boyunca düğümler
            int inner = graph.Add(new Vector3(x1 - 1.4f, F, PlatformDoorZ));
            int outer = graph.Add(new Vector3(walkX, WalkY, PlatformDoorZ));
            graph.Link(midE, inner);
            graph.Link(inner, outer);
            var walkway = new List<(float z, int node)> { (PlatformDoorZ, outer) };
            foreach (float z in bayZ) walkway.Add((z + 1.2f, graph.Add(new Vector3(walkX, WalkY, z + 1.2f), true)));
            walkway.Add((zw0 + 0.6f, graph.Add(new Vector3(walkX, WalkY, zw0 + 0.6f))));
            walkway.Add((zw1 - 0.6f, graph.Add(new Vector3(walkX, WalkY, zw1 - 0.6f))));
            walkway.Sort((a, b) => a.z.CompareTo(b.z));
            for (int i = 1; i < walkway.Count; i++) graph.Link(walkway[i - 1].node, walkway[i].node);
            graph.Link(walkway[0].node, pEast);

            // Peronda otobüs bekleyenler, karşıya geçmeyi bekleyenler
            foreach (float z in bayZ)
            {
                spots.Add(new StationSpot(new Vector3(wx1 - 1.1f, WalkY, z - 0.9f), Vector3.right));
                spots.Add(new StationSpot(new Vector3(wx1 - 1.3f, WalkY, z + 0.4f), Vector3.right));
            }
            spots.Add(new StationSpot(new Vector3(door + 0.8f, P, roadN + 0.8f), Vector3.back));
            spots.Add(new StationSpot(Hall(W * 0.7f, D - 1.9f), Vector3.forward));
            seats.AddRange(SeatRows());
            seats.AddRange(platformSeats);
        }

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
                float z = roadC + (lane == 0 ? -1.5f : 1.5f);
                float speed = lane == 0 ? 9f : -8f;
                for (int i = 0; i < perLane; i++)
                {
                    float x = -150f + (i + R(0f, 0.6f)) * 300f / perLane;
                    GameObject go;
                    if (rng.NextDouble() < 0.18)
                    {
                        Mesh bus = StationVehicles.Bus(rng.Next(6), out MeshLayer[] busLayers);
                        go = Place(bus, busLayers, "TrafficBus", parent, new Vector3(x, RoadY, z), speed > 0f ? 90f : -90f);
                    }
                    else
                    {
                        Mesh car = StationVehicles.Car(rng.Next(6), out MeshLayer[] carLayers);
                        go = Place(car, carLayers, "Car", parent, new Vector3(x, RoadY, z), speed > 0f ? 90f : -90f);
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
                Vector3 c = position + Vector3.up * 0.06f;
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
