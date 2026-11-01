using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace BusTycoon.Game
{
    /// <summary>
    /// Yakın plan harita döşemeleri. Genel harita (4096 px) tüm Avrupa'yı kapsar; yakınlaşınca kameranın gördüğü yerdeki
    /// yüksek çözünürlüklü döşemeler (StreamingAssets/MapTiles, Tools/build_map.py --tiles) indirilip genel haritanın
    /// üzerine belirerek gelir. Uzaklaşınca ya da başka yere gidince söner; bellekte en çok birkaç döşeme kalır.
    /// <list type="bullet">
    /// <item>Döşeme sayısı kaliteye göre: Yüksek 9, Orta 5, Düşük 0 (yalnız genel harita).</item>
    /// <item>Aynı anda en çok iki indirme. Tarayıcıda görüntü arka planda çözülür (WebImage); kare başına en çok bir
    /// döşeme ekran kartına yüklenir (takılma olmasın).</item>
    /// <item>Döşeme, harita gölgelendiricisinin BT_TILE çeşidiyle çizilir: deniz, bulut, gölge, pus aynen sürer.</item>
    /// <item>Dizin ya da bir döşeme inemezse oyun genel haritayla devam eder (hata göstermez).</item>
    /// </list>
    /// </summary>
    public sealed class MapTiles : MonoBehaviour
    {
        const string Folder = "MapTiles";
        /// <summary>Kamera bundan uzaktayken genel harita yeterince keskin: döşeme gerekmez.</summary>
        public const float NearDistance = 42f;
        const float FadeSeconds = 0.45f;
        const float CheckInterval = 0.2f;
        const int MaxConcurrentLoads = 2;
        const int ExtraCached = 3;

        static readonly int TileBlendId = Shader.PropertyToID("_TileBlend");

        [Serializable]
        sealed class TileIndex
        {
            public int version, size, cols, rows;
            public float spanU, spanV;
            public TileEntry[] tiles;
        }

        [Serializable]
        sealed class TileEntry
        {
            public string name;
            public int col, row;
        }

        sealed class Tile
        {
            public string Name;
            public Rect Area;       // dünya XZ
            public float TexV;      // harita sınırında kırpılan döşemenin doku üst kenarı
            public UnityWebRequest Request;
            public int ImageId;     // tarayıcıda çözülüyor (WebImage)
            public Texture2D Texture;
            public Material Material;
            public GameObject Object;
            public MeshRenderer Renderer;
            public float Blend, LastWanted;
            public bool Wanted, Failed;
        }

        WorldRenderer world;
        MapCamera mapCamera;
        Material template;
        string root;
        UnityWebRequest indexRequest;
        readonly List<Tile> tiles = new List<Tile>();
        readonly List<Tile> candidates = new List<Tile>();
        float nextCheck;
        Vector3 focus;

        /// <summary>Ekranda (kısmen de olsa) görünen döşeme sayısı.</summary>
        public int VisibleCount { get; private set; }
        /// <summary>Bellekteki (indirilmiş) döşeme sayısı.</summary>
        public int LoadedCount { get; private set; }
        /// <summary>Dizindeki döşeme sayısı (dizin inmediyse 0).</summary>
        public int IndexedCount => tiles.Count;

        public static int MaxTilesFor(QualityLevel level) =>
            level == QualityLevel.High ? 9 : level == QualityLevel.Medium ? 5 : 0;

        public void Init(WorldRenderer worldRenderer, MapCamera camera)
        {
            world = worldRenderer;
            mapCamera = camera;
            template = world.MapSurfaceMaterial;
            if (template == null) return; // harita gölgelendiricisi yok (yedek malzeme): döşeme de yok

            root = Application.streamingAssetsPath ?? string.Empty;
            if (!root.Contains("://")) root = "file://" + root;
            indexRequest = UnityWebRequest.Get($"{root}/{Folder}/index.json");
            indexRequest.SendWebRequest();
        }

        void OnDestroy()
        {
            indexRequest?.Dispose();
            foreach (var tile in tiles) Release(tile);
        }

        void Update()
        {
            if (indexRequest != null)
            {
                if (!indexRequest.isDone) return;
                ReadIndex(indexRequest);
                indexRequest.Dispose();
                indexRequest = null;
            }
            if (tiles.Count == 0) return;

            PollLoads();
            if (Time.unscaledTime >= nextCheck)
            {
                nextCheck = Time.unscaledTime + CheckInterval;
                ChooseTiles();
            }
            Animate(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
        }

        void ReadIndex(UnityWebRequest request)
        {
            if (request.result != UnityWebRequest.Result.Success) return; // döşemesiz sürüm: genel harita yeter
            TileIndex index;
            try
            {
                index = JsonUtility.FromJson<TileIndex>(request.downloadHandler.text);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Bus Tycoon] Harita döşeme dizini okunamadı: {e.Message}");
                return;
            }
            if (index?.tiles == null || index.spanU <= 0f || index.spanV <= 0f) return;

            Rect map = world.MapBoundsXZ;
            float width = index.spanU * map.width, depth = index.spanV * map.height;
            foreach (var entry in index.tiles)
            {
                if (entry == null || string.IsNullOrEmpty(entry.name)) continue;
                float x = map.xMin + entry.col * width, z = map.yMin + entry.row * depth;
                float top = Mathf.Min(z + depth, map.yMax);
                if (top <= z) continue;
                tiles.Add(new Tile { Name = entry.name, Area = new Rect(x, z, width, top - z), TexV = (top - z) / depth });
            }
            Debug.Log($"[Bus Tycoon] Harita döşemeleri: {tiles.Count}");
        }

        /// <summary>Kameranın gördüğü yerdeki döşemeler, ekran ortasına yakınlığa göre; kalite sınırına kadar.</summary>
        void ChooseTiles()
        {
            Camera main = mapCamera.Camera;
            bool mapVisible = main != null && main.enabled;
            int max = MaxTilesFor(GraphicsQuality.Current);
            foreach (var tile in tiles) tile.Wanted = false;
            if (!mapVisible || max == 0 || mapCamera.Distance > NearDistance) return;

            Rect view = VisibleGround(main);
            candidates.Clear();
            foreach (var tile in tiles)
                if (!tile.Failed && tile.Area.Overlaps(view)) candidates.Add(tile);
            candidates.Sort((a, b) => Score(a).CompareTo(Score(b)));
            float now = Time.unscaledTime;
            for (int i = 0; i < candidates.Count && i < max; i++)
            {
                candidates[i].Wanted = true;
                candidates[i].LastWanted = now;
            }

            int loading = 0;
            foreach (var tile in tiles) if (tile.Request != null || tile.ImageId != 0) loading++;
            foreach (var tile in candidates)
            {
                if (loading >= MaxConcurrentLoads) break;
                if (!tile.Wanted || tile.Texture != null || tile.Request != null || tile.ImageId != 0) continue;
                string url = $"{root}/{Folder}/{tile.Name}.jpg";
                if (WebImage.Supported)
                {
                    tile.ImageId = WebImage.Begin(url); // tarayıcı arka planda indirip çözer
                }
                else
                {
                    tile.Request = UnityWebRequest.Get(url);
                    tile.Request.SendWebRequest();
                }
                loading++;
            }
        }

        float Score(Tile tile) => (tile.Area.center - new Vector2(focus.x, focus.z)).sqrMagnitude;

        /// <summary>Ekran köşelerinin yerdeki izdüşümlerini kapsayan dikdörtgen (dünya XZ).</summary>
        Rect VisibleGround(Camera main)
        {
            float w = main.pixelWidth, h = main.pixelHeight;
            if (!mapCamera.TryGetGroundPoint(new Vector2(w * 0.5f, h * 0.5f), out focus)) focus = Vector3.zero;
            float xMin = float.MaxValue, xMax = float.MinValue, zMin = float.MaxValue, zMax = float.MinValue;
            int found = 0;
            for (int i = 0; i < 4; i++)
            {
                var corner = new Vector2((i & 1) == 0 ? 0f : w, (i & 2) == 0 ? 0f : h);
                if (!mapCamera.TryGetGroundPoint(corner, out Vector3 p)) continue;
                found++;
                xMin = Mathf.Min(xMin, p.x); xMax = Mathf.Max(xMax, p.x);
                zMin = Mathf.Min(zMin, p.z); zMax = Mathf.Max(zMax, p.z);
            }
            if (found < 2)
            {
                float r = mapCamera.Distance;
                return new Rect(focus.x - r, focus.z - r, 2f * r, 2f * r);
            }
            return Rect.MinMaxRect(xMin, zMin, xMax, zMax);
        }

        void PollLoads()
        {
            bool decoded = false;
            foreach (var tile in tiles)
            {
                if (tile.ImageId != 0)
                {
                    int state = WebImage.State(tile.ImageId);
                    if (state == 0 || (state == 1 && decoded)) continue; // bekliyor / kare başına bir yükleme
                    if (state == 1)
                    {
                        decoded = true;
                        Texture2D texture = WebImage.Finish(tile.ImageId, "MapTile_" + tile.Name, 8);
                        if (texture != null) CreateVisual(tile, texture);
                        else tile.Failed = true;
                    }
                    else
                    {
                        tile.Failed = true; // bu döşeme yok/inemedi: genel harita yeter
                    }
                    tile.ImageId = 0;
                    continue;
                }

                UnityWebRequest request = tile.Request;
                if (request == null || !request.isDone) continue;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    tile.Failed = true; // bu döşeme yok/inemedi: genel harita yeter
                }
                else
                {
                    if (decoded) continue; // kare başına bir çözme; kalan istek sonraki karede
                    decoded = true;
                    CreateVisualFromBytes(tile, request.downloadHandler.data);
                }
                request.Dispose();
                tile.Request = null;
            }
        }

        /// <summary>Eski yol (web dışı ya da ?eskidoku=1): görüntü ana iş parçacığında çözülür.</summary>
        void CreateVisualFromBytes(Tile tile, byte[] bytes)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, true) { name = "MapTile_" + tile.Name };
            if (bytes == null || !texture.LoadImage(bytes, true))
            {
                Destroy(texture);
                tile.Failed = true;
                return;
            }
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = 8;
            CreateVisual(tile, texture);
        }

        void CreateVisual(Tile tile, Texture2D texture)
        {
            tile.Texture = texture;

            var material = new Material(template) { name = "MapTile_" + tile.Name, mainTexture = texture };
            material.EnableKeyword("BT_TILE");
            material.SetTexture("_OverviewTex", template.mainTexture);
            material.SetFloat("_ZOffset", -1f);   // genel haritanın önünde (aynı yükseklikte)
            material.SetFloat(TileBlendId, 0f);
            tile.Material = material;

            Rect a = tile.Area;
            var mesh = new Mesh { name = "MapTile_" + tile.Name };
            mesh.vertices = new[]
            {
                new Vector3(a.xMin, 0f, a.yMin), new Vector3(a.xMax, 0f, a.yMin),
                new Vector3(a.xMax, 0f, a.yMax), new Vector3(a.xMin, 0f, a.yMax)
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, tile.TexV), new Vector2(0f, tile.TexV) };
            mesh.triangles = new[] { 0, 3, 2, 0, 2, 1 };
            mesh.RecalculateNormals();
            if (mesh.normals[0].y < 0f) // harita yüzeyiyle aynı: sarım ters çıkarsa çevir
            {
                mesh.triangles = new[] { 0, 2, 3, 0, 1, 2 };
                mesh.RecalculateNormals();
            }
            mesh.RecalculateBounds();

            var go = new GameObject("MapTile_" + tile.Name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.enabled = false;
            tile.Object = go;
            tile.Renderer = renderer;
        }

        void Animate(float dt)
        {
            int visible = 0, loaded = 0;
            foreach (var tile in tiles)
            {
                if (tile.Texture == null) continue;
                loaded++;
                float target = tile.Wanted ? 1f : 0f;
                if (!Mathf.Approximately(tile.Blend, target))
                {
                    tile.Blend = Mathf.MoveTowards(tile.Blend, target, dt / FadeSeconds);
                    tile.Material.SetFloat(TileBlendId, tile.Blend * tile.Blend * (3f - 2f * tile.Blend));
                }
                bool show = tile.Blend > 0.001f;
                if (tile.Renderer.enabled != show) tile.Renderer.enabled = show;
                if (show) visible++;
            }
            VisibleCount = visible;
            LoadedCount = loaded;

            // Bellek: kalite sınırının biraz fazlası kadar döşeme tutulur; en uzun süredir gerekmeyen atılır
            int keep = MaxTilesFor(GraphicsQuality.Current) + ExtraCached;
            while (loaded > keep)
            {
                Tile oldest = null;
                foreach (var tile in tiles)
                    if (tile.Texture != null && !tile.Wanted && tile.Blend <= 0f && (oldest == null || tile.LastWanted < oldest.LastWanted))
                        oldest = tile;
                if (oldest == null) break;
                Release(oldest);
                loaded--;
            }
            LoadedCount = loaded;
        }

        void Release(Tile tile)
        {
            if (tile.Request != null)
            {
                tile.Request.Dispose();
                tile.Request = null;
            }
            if (tile.ImageId != 0)
            {
                WebImage.Cancel(tile.ImageId);
                tile.ImageId = 0;
            }
            if (tile.Object != null)
            {
                var filter = tile.Object.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null) Destroy(filter.sharedMesh);
                Destroy(tile.Object);
            }
            if (tile.Material != null) Destroy(tile.Material);
            if (tile.Texture != null) WebImage.Release(tile.Texture); // tarayıcı dokusuysa WebGL dokusu da silinir
            tile.Object = null;
            tile.Renderer = null;
            tile.Material = null;
            tile.Texture = null;
            tile.Blend = 0f;
        }
    }
}
