using System.Collections.Generic;
using BusTycoon.Data;
using BusTycoon.Urban;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;

namespace BusTycoon.Game
{
    /// <summary>
    /// Şehir içi harita: seçili şehrin gerçek sokak haritası (StreamingAssets/Cities, Tools/build_city.py), durakları ve
    /// ünlü mekânları. Ülke haritasından uzakta, kendi ölçeğinde bir düzlemdir (1 birim = 100 m); kamera şehir
    /// görünümüne geçince bu alana atlar (<see cref="MapCamera.SetArea"/>).
    /// <list type="bullet">
    /// <item>Harita dokusu ve şehir verisi (durak, mekân, yol ağı) arka planda iner; hazır olunca <see cref="Ready"/>.</item>
    /// <item>Durak ve mekân işaretleri tek örgüde (MapSprites); boyları kamera uzaklığıyla ölçeklenir (ekranda hep aynı boy).</item>
    /// <item>Dosya inemezse <see cref="Failed"/>: oyun ülke haritasıyla devam eder.</item>
    /// </list>
    /// </summary>
    public sealed class CityView : MonoBehaviour
    {
        /// <summary>Şehir haritasının dünyadaki yeri: ülke haritasından (±150 birim) çok uzakta, kamera ikisini birlikte görmez.</summary>
        public static readonly Vector3 Origin = new Vector3(5000f, 0f, 0f);
        /// <summary>1 birim = 100 m.</summary>
        public const float UnitsPerMeter = 0.01f;
        /// <summary>En yakın kamera uzaklığı (~200 m'lik görüş: mekân maketleri seçilir).</summary>
        public const float MinCameraDistance = 2.5f;

        static readonly Color StopFill = new Color(0.945f, 0.957f, 0.961f);       // #F1F4F5
        static readonly Color StopRing = new Color(0.039f, 0.082f, 0.106f);       // #0A151B
        static readonly Color MajorStopFill = new Color(0.961f, 0.769f, 0.318f);  // altın: ilçe merkezleri
        static readonly Color LandmarkFill = new Color(0.62f, 0.67f, 0.69f);      // mat gri (maket rengi)
        static readonly Color LandmarkRing = new Color(0.16f, 0.2f, 0.22f);

        MapCamera mapCamera;
        StreamingImage imageLoad;
        UnityWebRequest dataRequest;
        string loadingCityId;
        Texture2D texture;
        Material material;
        GameObject plane;
        Mesh planeMesh;
        MapSprites stopSprites, landmarkSprites, lineSprites, vehicleSprites;
        float spriteDistance = -1f;
        bool visible, linesDirty = true;

        /// <summary>Haritadaki bir hat: güzergâh noktaları (dünya), rengi ve üstündeki araç sayısı.</summary>
        sealed class Line
        {
            public string Id;
            public readonly List<Vector3> Points = new List<Vector3>();
            public float[] Cumulative;
            public Color Color;
            public int Vehicles;
            public bool Preview;
        }

        readonly Dictionary<string, Line> lines = new Dictionary<string, Line>();
        readonly List<int> pathBuffer = new List<int>();
        Line preview;
        UrbanStop previewOrigin, previewDestination;
        static readonly Color PreviewColor = new Color(0.961f, 0.769f, 0.318f, 0.95f);

        public CityData City { get; private set; }
        public UrbanMap Map { get; private set; }
        /// <summary>Harita ve veri hazır, çizilebilir.</summary>
        public bool Ready => Map != null && plane != null;
        /// <summary>Son yükleme başarısız (dosya yok ya da bozuk).</summary>
        public bool Failed { get; private set; }
        public bool Loading => loadingCityId != null;
        /// <summary>Haritanın dünyadaki alanı (XZ).</summary>
        public Rect WorldBounds { get; private set; }
        public bool Visible => visible;

        public void Init(MapCamera camera)
        {
            mapCamera = camera;
            lineSprites = new MapSprites("CityLines", transform, 0);
            landmarkSprites = new MapSprites("CityLandmarks", transform, 1);
            stopSprites = new MapSprites("CityStops", transform, 2);
            vehicleSprites = new MapSprites("CityVehicles", transform, 3);
            SetVisible(false);
        }

        public static bool Supports(CityData city) => city != null && UrbanCatalog.Has(city.Id);

        /// <summary>Şehri yükler (zaten yüklüyse ya da yükleniyorsa bir şey yapmaz).</summary>
        public void Load(CityData city)
        {
            if (!Supports(city)) return;
            if (City == city && (Ready || Loading)) return;
            Unload();
            City = city;
            Failed = false;
            loadingCityId = city.Id;
            imageLoad = new StreamingImage(UrbanCatalog.FilePath(city.Id, "map.jpg"), "CityMap " + city.Id, 8);
            dataRequest = UnityWebRequest.Get(StreamingImage.Root + "/" + UrbanCatalog.FilePath(city.Id, "city.bytes"));
            dataRequest.SendWebRequest();
        }

        void Unload()
        {
            imageLoad?.Cancel();
            imageLoad = null;
            dataRequest?.Dispose();
            dataRequest = null;
            loadingCityId = null;
            Map = null;
            if (plane != null) Destroy(plane);
            if (planeMesh != null) Destroy(planeMesh);
            if (material != null) Destroy(material);
            if (texture != null) Destroy(texture);
            plane = null;
            planeMesh = null;
            material = null;
            texture = null;
            foreach (MapSprites sprites in new[] { stopSprites, landmarkSprites, lineSprites, vehicleSprites })
            {
                sprites?.Clear();
                sprites?.Apply(0f);
            }
            lines.Clear();
            preview = null;
            previewOrigin = previewDestination = null;
            spriteDistance = -1f;
        }

        public void SetVisible(bool show)
        {
            visible = show;
            if (plane != null) plane.SetActive(show);
            foreach (MapSprites sprites in new[] { stopSprites, landmarkSprites, lineSprites, vehicleSprites })
                if (sprites != null) sprites.Renderer.enabled = show;
        }

        void Update()
        {
            if (loadingCityId != null) PollLoad();
            if (!Ready || !visible || mapCamera == null) return;
            float distance = mapCamera.Distance;
            if (linesDirty || spriteDistance < 0f || Mathf.Abs(distance - spriteDistance) > spriteDistance * 0.03f) RebuildSprites(distance);
            UpdateVehicles(distance);
        }

        void PollLoad()
        {
            bool imageDone = imageLoad == null || imageLoad.Poll();
            bool dataDone = dataRequest == null || dataRequest.isDone;
            if (!imageDone || !dataDone) return;

            UrbanMap map = null;
            if (dataRequest != null && dataRequest.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    map = UrbanMap.Read(City.Id, dataRequest.downloadHandler.data);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[Bus Tycoon] Şehir verisi okunamadı ({City.Id}): {e.Message}");
                }
            }
            texture = imageLoad != null ? imageLoad.Texture : null;
            dataRequest?.Dispose();
            dataRequest = null;
            imageLoad = null;
            loadingCityId = null;

            if (map == null || texture == null)
            {
                Failed = true;
                if (texture != null) Destroy(texture);
                texture = null;
                Debug.LogWarning($"[Bus Tycoon] Şehir haritası yüklenemedi: {City.Id}");
                return;
            }
            Map = map;
            BuildPlane();
            SetVisible(visible);
        }

        void BuildPlane()
        {
            Rect m = Map.Bounds;
            Vector3 a = ToWorld(new Vector2(m.xMin, m.yMin)), b = ToWorld(new Vector2(m.xMax, m.yMax));
            WorldBounds = Rect.MinMaxRect(a.x, a.z, b.x, b.z);

            planeMesh = new Mesh { name = "CityMap" };
            planeMesh.vertices = new[]
            {
                new Vector3(a.x, 0f, a.z), new Vector3(b.x, 0f, a.z), new Vector3(b.x, 0f, b.z), new Vector3(a.x, 0f, b.z)
            };
            // Doku: v=0 resmin altı (güney), v=1 üstü (kuzey) — build_city.py'nin resmiyle aynı yön
            planeMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            planeMesh.triangles = new[] { 0, 3, 2, 0, 2, 1 };
            planeMesh.RecalculateNormals();
            planeMesh.RecalculateBounds();

            Shader shader = Resources.Load<Shader>(DefaultContent.CityMapShaderResourcePath);
            if (shader == null) shader = Shader.Find("BusTycoon/CityMap");
            material = shader != null && shader.isSupported ? new Material(shader) : MaterialFactory.CreateUnlitTexture(texture);
            material.name = "CityMap";
            material.mainTexture = texture;

            plane = new GameObject("CityMap");
            plane.transform.SetParent(transform, false);
            plane.AddComponent<MeshFilter>().sharedMesh = planeMesh;
            var renderer = plane.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            spriteDistance = -1f;
        }

        /// <summary>İşaretler kamera uzaklığıyla ölçeklenir: ekranda hep yaklaşık aynı boyda.</summary>
        void RebuildSprites(float distance)
        {
            spriteDistance = distance;
            linesDirty = false;
            float unit = distance * 0.0105f; // ~7 piksellik daire (masaüstü)
            stopSprites.Clear();
            foreach (UrbanStop stop in Map.Stops)
            {
                bool major = stop.Weight >= 0.75f;
                bool picked = stop == previewOrigin || stop == previewDestination;
                float radius = unit * (picked ? 1.6f : Mathf.Lerp(0.75f, 1.15f, stop.Weight));
                Color fill = picked ? PreviewColor : major ? MajorStopFill : StopFill;
                stopSprites.AddCircle(ToWorld(stop.Position) + Vector3.up * 0.02f, radius, fill, StopRing, picked ? 0.7f : 0.62f);
            }
            stopSprites.Apply(unit * 2f);

            // Hatlar: güzergâh boyunca uç uca kapsüller (yuvarlak uçlar köşeleri kapatır); önizleme en üstte
            lineSprites.Clear();
            float width = unit * 0.85f;
            foreach (Line line in lines.Values) AddLine(line, width);
            if (preview != null) AddLine(preview, width * 1.15f);
            lineSprites.Apply(width * 2f);

            landmarkSprites.Clear();
            foreach (UrbanLandmark landmark in Map.Landmarks)
                landmarkSprites.AddCircle(ToWorld(landmark.Position) + Vector3.up * 0.01f, unit * 0.8f, LandmarkFill, LandmarkRing, 0.55f);
            landmarkSprites.Apply(unit * 2f);
        }

        void AddLine(Line line, float width)
        {
            var points = line.Points;
            if (points.Count < 2) return;
            float minStep = width * 0.6f; // çok sık noktalar seyreltilir (uzakta yüzlerce kapsül olmasın)
            Vector3 from = points[0];
            for (int i = 1; i < points.Count; i++)
            {
                Vector3 to = points[i];
                float length = Vector3.Distance(from, to);
                if (length < minStep && i < points.Count - 1) continue;
                if (length < 1e-4f) continue;
                lineSprites.AddCapsule((from + to) * 0.5f + Vector3.up * 0.015f, to - from, length + width, width, line.Color, line.Color, 1f);
                from = to;
            }
        }

        // ------------------------------------------------------------------ hatlar ve araçlar

        /// <summary>Hat güzergâhı: uç durakların konumuna (metre) en yakın duraklar arasında en kısa yol.</summary>
        bool BuildPath(Vector2 a, Vector2 b, List<Vector3> points)
        {
            points.Clear();
            UrbanStop from = UrbanPlanner.NearestStop(Map, a), to = UrbanPlanner.NearestStop(Map, b);
            if (from == null || to == null || !Map.FindPath(from.Node, to.Node, pathBuffer, out _)) return false;
            foreach (int node in pathBuffer) points.Add(ToWorld(Map.NodePosition(node)));
            return points.Count >= 2;
        }

        static float[] Cumulative(List<Vector3> points)
        {
            var result = new float[points.Count];
            for (int i = 1; i < points.Count; i++) result[i] = result[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            return result;
        }

        /// <summary>
        /// Şehrin hatlarını eşitler (kimlik, uçlar, renk, araç sayısı). Yeni hatların güzergâhı bir kez hesaplanır;
        /// kapanan hatlar silinir. Çağıran yalnız hat/filo değişince çağırır.
        /// </summary>
        public void SetRoutes(IReadOnlyList<(string id, Vector2 a, Vector2 b, Color color, int vehicles)> routes)
        {
            if (!Ready) return;
            var alive = new HashSet<string>();
            foreach (var route in routes)
            {
                alive.Add(route.id);
                if (!lines.TryGetValue(route.id, out Line line))
                {
                    line = new Line { Id = route.id };
                    if (!BuildPath(route.a, route.b, line.Points)) continue;
                    line.Cumulative = Cumulative(line.Points);
                    lines[route.id] = line;
                }
                line.Color = route.color;
                line.Vehicles = route.vehicles;
            }
            var stale = new List<string>();
            foreach (string id in lines.Keys) if (!alive.Contains(id)) stale.Add(id);
            foreach (string id in stale) lines.Remove(id);
            linesDirty = true;
        }

        /// <summary>Planlayıcı önizlemesi: seçilen duraklar ve (varsa) güzergâh. null: önizleme yok.</summary>
        public void SetPreview(UrbanStop origin, UrbanStop destination, IReadOnlyList<int> path)
        {
            previewOrigin = origin;
            previewDestination = destination;
            preview = null;
            if (Ready && path != null && path.Count >= 2)
            {
                preview = new Line { Id = "preview", Color = PreviewColor, Preview = true };
                foreach (int node in path) preview.Points.Add(ToWorld(Map.NodePosition(node)));
                preview.Cumulative = Cumulative(preview.Points);
            }
            linesDirty = true;
        }

        public int LineCount => lines.Count;
        /// <summary>Oyun duraklatılınca dolmuşlar da durur.</summary>
        public bool Paused { get; set; }
        float animationTime;

        /// <summary>Dolmuşlar hat boyunca gidip gelir (görsel; hızı gerçek sefer süresinden bağımsız, akıcı).</summary>
        void UpdateVehicles(float distance)
        {
            vehicleSprites.Clear();
            float unit = distance * 0.0105f;
            if (!Paused) animationTime += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            float t = animationTime;
            foreach (Line line in lines.Values)
            {
                if (line.Vehicles <= 0 || line.Cumulative == null) continue;
                float total = line.Cumulative[line.Cumulative.Length - 1];
                if (total <= 0f) continue;
                float tripSeconds = Mathf.Clamp(total * 0.35f, 8f, 40f); // birim/sn: uzun hat daha uzun sürer
                for (int i = 0; i < line.Vehicles; i++)
                {
                    float phase = Mathf.Repeat(t / tripSeconds + i / (float)line.Vehicles, 2f);
                    float along = (phase <= 1f ? phase : 2f - phase) * total;
                    Sample(line, along, out Vector3 position, out Vector3 forward);
                    if (phase > 1f) forward = -forward;
                    vehicleSprites.AddCapsule(position + Vector3.up * 0.03f, forward, unit * 2.2f, unit * 1.1f, Color.white, line.Color, 0.55f);
                }
            }
            vehicleSprites.Apply(unit * 3f);
        }

        static void Sample(Line line, float along, out Vector3 position, out Vector3 forward)
        {
            float[] c = line.Cumulative;
            int lo = 0, hi = c.Length - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (c[mid] <= along) lo = mid;
                else hi = mid;
            }
            float span = Mathf.Max(1e-5f, c[hi] - c[lo]);
            position = Vector3.Lerp(line.Points[lo], line.Points[hi], (along - c[lo]) / span);
            forward = line.Points[hi] - line.Points[lo];
        }

        public Vector3 ToWorld(Vector2 meters) =>
            Origin + new Vector3(meters.x * UnitsPerMeter, 0f, meters.y * UnitsPerMeter);

        public Vector2 ToMeters(Vector3 world) =>
            new Vector2((world.x - Origin.x) / UnitsPerMeter, (world.z - Origin.z) / UnitsPerMeter);

        /// <summary>Şehrin ortası (dünya).</summary>
        public Vector3 Center => Ready ? ToWorld(Map.Bounds.center) : Origin;

        /// <summary>Haritanın tamamının görüneceği kamera uzaklığı (görüntü en/boy oranına göre).</summary>
        public float FitDistance(float aspect)
        {
            Rect r = WorldBounds;
            // Uzakta görüş açısı 40°: görünen yükseklik ≈ 0,73 × uzaklık
            float byHeight = r.height / 0.73f;
            float byWidth = r.width / (0.73f * Mathf.Max(0.5f, aspect));
            return Mathf.Max(byHeight, byWidth) * 1.02f;
        }

        /// <summary>Ekran noktasına (sol alt başlangıç) en yakın durak.</summary>
        public bool TryPickStop(Camera camera, Vector2 screenPoint, float radiusPixels, out UrbanStop stop)
        {
            stop = null;
            if (!Ready || camera == null) return false;
            float best = radiusPixels * radiusPixels;
            foreach (UrbanStop candidate in Map.Stops)
            {
                Vector3 sp = camera.WorldToScreenPoint(ToWorld(candidate.Position));
                if (sp.z <= 0f) continue;
                float d = ((Vector2)sp - screenPoint).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    stop = candidate;
                }
            }
            return stop != null;
        }

        void OnDestroy() => Unload();
    }
}
