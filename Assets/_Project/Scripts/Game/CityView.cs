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
        MapSprites stopSprites, landmarkSprites;
        float spriteDistance = -1f;
        bool visible;

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
            stopSprites = new MapSprites("CityStops", transform, 2);
            landmarkSprites = new MapSprites("CityLandmarks", transform, 1);
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
            stopSprites?.Clear();
            stopSprites?.Apply(0f);
            landmarkSprites?.Clear();
            landmarkSprites?.Apply(0f);
            spriteDistance = -1f;
        }

        public void SetVisible(bool show)
        {
            visible = show;
            if (plane != null) plane.SetActive(show);
            if (stopSprites != null) stopSprites.Renderer.enabled = show;
            if (landmarkSprites != null) landmarkSprites.Renderer.enabled = show;
        }

        void Update()
        {
            if (loadingCityId != null) PollLoad();
            if (!Ready || !visible || mapCamera == null) return;
            float distance = mapCamera.Distance;
            if (spriteDistance < 0f || Mathf.Abs(distance - spriteDistance) > spriteDistance * 0.03f) RebuildSprites(distance);
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
            float unit = distance * 0.0105f; // ~7 piksellik daire (masaüstü)
            stopSprites.Clear();
            foreach (UrbanStop stop in Map.Stops)
            {
                bool major = stop.Weight >= 0.75f;
                float radius = unit * Mathf.Lerp(0.75f, 1.15f, stop.Weight);
                stopSprites.AddCircle(ToWorld(stop.Position) + Vector3.up * 0.02f, radius, major ? MajorStopFill : StopFill, StopRing, 0.62f);
            }
            stopSprites.Apply(unit * 2f);

            landmarkSprites.Clear();
            foreach (UrbanLandmark landmark in Map.Landmarks)
                landmarkSprites.AddCircle(ToWorld(landmark.Position) + Vector3.up * 0.01f, unit * 0.8f, LandmarkFill, LandmarkRing, 0.55f);
            landmarkSprites.Apply(unit * 2f);
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
