using BusTycoon.Data;
using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// Harita dokuları: Resources'taki JPEG/PNG baytlarından (Tools/build_map.py) açılışta çözülür ve mipmap'li,
    /// anizotropik süzmeli dokuya dönüştürülür. Unity'nin doku içe aktarımı bu dokuları web paketine sıkıştırmasız
    /// koyuyordu (~13 MB); baytlar olduğu gibi paketlenir (1,8 MB). Ayrıca harita gölgelendiricisinin gürültü
    /// dokusu (dalga, arazi dokusu, bulut) kodla üretilir.
    /// </summary>
    public static class MapTextures
    {
        static Texture2D noise;

        public static Texture2D LoadMap() => Load(DefaultContent.MapTextureResourcePath, "Map", 8);
        public static Texture2D LoadOuter() => Load(DefaultContent.OuterMapTextureResourcePath, "MapOuter", 4);
        public static Texture2D LoadWaterMask() => Load(DefaultContent.WaterMaskResourcePath, "MapWater", 4);

        /// <summary>Resources'taki görüntü baytını dokuya çevirir; yoksa ya da çözülemezse null.</summary>
        static Texture2D Load(string resourcePath, string name, int aniso)
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null) return null;
            // Mipmap zinciriyle oluşturulur: LoadImage görüntüyü yükleyip küçük seviyeleri üretir (uzakta kırpışma olmaz).
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, true) { name = name };
            bool loaded = texture.LoadImage(asset.bytes, true); // CPU kopyası bırakılır (bellek)
            Resources.UnloadAsset(asset);
            if (!loaded)
            {
                Debug.LogWarning($"[Bus Tycoon] Harita dokusu çözülemedi: {resourcePath}");
                Object.Destroy(texture);
                return null;
            }
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = aniso;
            return texture;
        }

        /// <summary>
        /// Döşenebilir (kenarları birbirine uyan) gürültü: R ve G ince ayrıntı ve dalga için iki ayrı desen, B bulutlar
        /// için iri desen. 256 px, mipmap'li; bir kez üretilir.
        /// </summary>
        public static Texture2D Noise
        {
            get
            {
                if (noise != null) return noise;
                const int size = 256;
                float[] fine = Fbm(size, 8, 4, 11);
                float[] ripples = Fbm(size, 16, 3, 29);
                float[] clouds = Fbm(size, 3, 5, 47);
                var pixels = new Color32[size * size];
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color32(ToByte(fine[i]), ToByte(ripples[i]), ToByte(clouds[i]), 255);
                noise = new Texture2D(size, size, TextureFormat.RGBA32, true)
                {
                    name = "MapNoise",
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Trilinear,
                    anisoLevel = 4
                };
                noise.SetPixels32(pixels);
                noise.Apply(true, true);
                return noise;
            }
        }

        static byte ToByte(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

        /// <summary>Katmanlı değer gürültüsü (0..1'e germiş); her katman iki kat sık, yarı güçte.</summary>
        internal static float[] Fbm(int size, int baseCells, int octaves, int seed)
        {
            var result = new float[size * size];
            float amplitude = 1f;
            int cells = baseCells;
            for (int octave = 0; octave < octaves && cells <= size; octave++)
            {
                AddValueNoise(result, size, cells, amplitude, seed + octave * 101);
                amplitude *= 0.5f;
                cells *= 2;
            }

            float min = float.MaxValue, max = float.MinValue;
            foreach (float v in result)
            {
                if (v < min) min = v;
                if (v > max) max = v;
            }
            float range = Mathf.Max(max - min, 1e-5f);
            for (int i = 0; i < result.Length; i++) result[i] = (result[i] - min) / range;
            return result;
        }

        static void AddValueNoise(float[] target, int size, int cells, float amplitude, int seed)
        {
            var random = new System.Random(seed);
            var lattice = new float[cells * cells];
            for (int i = 0; i < lattice.Length; i++) lattice[i] = (float)random.NextDouble();

            float step = (float)cells / size;
            for (int y = 0; y < size; y++)
            {
                float fy = y * step;
                int iy = (int)fy;
                float ty = Fade(fy - iy);
                int y0 = (iy % cells) * cells, y1 = ((iy + 1) % cells) * cells;
                for (int x = 0; x < size; x++)
                {
                    float fx = x * step;
                    int ix = (int)fx;
                    float tx = Fade(fx - ix);
                    int x0 = ix % cells, x1 = (ix + 1) % cells;
                    float top = lattice[y0 + x0] + (lattice[y0 + x1] - lattice[y0 + x0]) * tx;
                    float bottom = lattice[y1 + x0] + (lattice[y1 + x1] - lattice[y1 + x0]) * tx;
                    target[y * size + x] += (top + (bottom - top) * ty) * amplitude;
                }
            }
        }

        static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
    }
}
