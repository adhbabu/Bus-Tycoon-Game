using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace BusTycoon.Game
{
    /// <summary>
    /// Tarayıcıda görüntüyü oyunu dondurmadan dokuya çevirir (Assets/Plugins/WebGL/BusTycoonImage.jslib): indirme ve
    /// JPEG/PNG çözme tarayıcının arka planında yapılır, mipmap'i ekran kartı üretir. Unity'nin LoadImage'ı ana iş
    /// parçacığında çözer ve büyük görüntülerde kareleri takılatır.
    /// <para>Kullanım: <see cref="Begin"/> → her kare <see cref="State"/> → hazırsa <see cref="Finish"/>; doku işi bitince
    /// <see cref="Release"/>. Web dışında (ya da ?eskidoku=1 ile) <see cref="Supported"/> false olur; çağıran eski yolu
    /// (UnityWebRequest + LoadImage) kullanır.</para>
    /// </summary>
    public static class WebImage
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int BusTycoon_ImageLoad(string url);
        [DllImport("__Internal")] static extern int BusTycoon_ImageState(int id);
        [DllImport("__Internal")] static extern int BusTycoon_ImageWidth(int id);
        [DllImport("__Internal")] static extern int BusTycoon_ImageHeight(int id);
        [DllImport("__Internal")] static extern int BusTycoon_ImageUpload(int id, int maxAniso);
        [DllImport("__Internal")] static extern void BusTycoon_ImageForget(int id);
        [DllImport("__Internal")] static extern void BusTycoon_ImageRelease(int name);
#endif

        /// <summary>Tarayıcıda oluşturulan dokular ve WebGL kimlikleri (silmek için).</summary>
        static readonly Dictionary<Texture2D, int> external = new Dictionary<Texture2D, int>();

        public static bool Supported
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return !LaunchOptions.Has("eskidoku");
#else
                return false;
#endif
            }
        }

        /// <summary>İndirip çözmeye başlar; istek kimliği (desteklenmiyorsa 0).</summary>
        public static int Begin(string url)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return BusTycoon_ImageLoad(url);
#else
            return 0;
#endif
        }

        /// <summary>0 bekliyor, 1 hazır, -1 başarısız.</summary>
        public static int State(int id)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return id > 0 ? BusTycoon_ImageState(id) : -1;
#else
            return -1;
#endif
        }

        /// <summary>Hazır görüntüyü mipmap'li dokuya çevirir (tek texImage2D; ucuz). Olmazsa null.</summary>
        public static Texture2D Finish(int id, string name, int anisoLevel)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            int width = BusTycoon_ImageWidth(id), height = BusTycoon_ImageHeight(id);
            int native = BusTycoon_ImageUpload(id, anisoLevel);
            if (native == 0 || width <= 0 || height <= 0) return null;
            var texture = Texture2D.CreateExternalTexture(width, height, TextureFormat.RGBA32, true, false, new IntPtr(native));
            texture.name = name;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = anisoLevel;
            external[texture] = native;
            return texture;
#else
            return null;
#endif
        }

        /// <summary>Bekleyen isteği bırakır.</summary>
        public static void Cancel(int id)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (id > 0) BusTycoon_ImageForget(id);
#endif
        }

        /// <summary>Dokuyu siler: tarayıcıda oluşturulduysa WebGL dokusu da silinir (yoksa bellek sızardı).</summary>
        public static void Release(Texture2D texture)
        {
            if (texture == null) return;
            if (external.TryGetValue(texture, out int native))
            {
                external.Remove(texture);
#if UNITY_WEBGL && !UNITY_EDITOR
                BusTycoon_ImageRelease(native);
#endif
            }
            UnityEngine.Object.Destroy(texture);
        }
    }
}
