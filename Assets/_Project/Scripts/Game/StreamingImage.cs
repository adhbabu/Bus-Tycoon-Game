using UnityEngine;
using UnityEngine.Networking;

namespace BusTycoon.Game
{
    /// <summary>
    /// StreamingAssets'teki bir görüntüyü (JPEG/PNG) oyunu dondurmadan dokuya çevirir: tarayıcıda indirme ve çözme
    /// arka planda yapılır (<see cref="WebImage"/>); web dışında (ya da ?eskidoku=1 ile) dosya UnityWebRequest ile okunur
    /// ve Unity'nin LoadImage'ı ile çözülür. Harita dokuları ve yakın plan döşemeleri bunu kullanır.
    /// <para>Kullanım: oluştur → her kare <see cref="Poll"/> true dönene kadar → <see cref="Texture"/> (olmadıysa null).</para>
    /// </summary>
    public sealed class StreamingImage
    {
        readonly string name;
        readonly int aniso;
        int imageId;
        UnityWebRequest request;

        /// <summary>StreamingAssets kök adresi (web'de http(s), cihazda file://).</summary>
        public static string Root
        {
            get
            {
                string root = Application.streamingAssetsPath ?? string.Empty;
                return root.Contains("://") ? root : "file://" + root;
            }
        }

        public string Url { get; }
        public bool Done { get; private set; }
        /// <summary>Henüz inmedi / çözülmedi (<see cref="Poll"/> şimdi çağrılırsa false döner).</summary>
        public bool Pending => !Done && (imageId != 0 ? WebImage.State(imageId) == 0 : request != null && !request.isDone);
        /// <summary>Hazır doku; yüklenemediyse null. Doku işi bitince <see cref="WebImage.Release"/> ile silinir.</summary>
        public Texture2D Texture { get; private set; }

        /// <param name="file">StreamingAssets'e göre yol (ör. "Map/map.jpg").</param>
        public StreamingImage(string file, string textureName, int anisoLevel)
        {
            name = textureName;
            aniso = anisoLevel;
            Url = Root + "/" + file;
            if (WebImage.Supported)
            {
                imageId = WebImage.Begin(Url);
            }
            else
            {
                request = UnityWebRequest.Get(Url);
                request.SendWebRequest();
            }
        }

        /// <summary>Bittiyse true (başarılı ya da değil). Bekleyen istek varken false.</summary>
        public bool Poll()
        {
            if (Done) return true;
            if (imageId != 0)
            {
                int state = WebImage.State(imageId);
                if (state == 0) return false;
                if (state == 1) Texture = WebImage.Finish(imageId, name, aniso);
                imageId = 0;
                Finish();
                return true;
            }
            if (request == null)
            {
                Finish();
                return true;
            }
            if (!request.isDone) return false;
            if (request.result == UnityWebRequest.Result.Success) Texture = Decode(request.downloadHandler.data);
            request.Dispose();
            request = null;
            Finish();
            return true;
        }

        void Finish()
        {
            Done = true;
            if (Texture == null) Debug.LogWarning($"[Bus Tycoon] Görüntü yüklenemedi: {Url}");
        }

        /// <summary>Bekleyen isteği bırakır (sonuç gelirse atılır).</summary>
        public void Cancel()
        {
            if (imageId != 0) WebImage.Cancel(imageId);
            imageId = 0;
            request?.Dispose();
            request = null;
            Done = true;
        }

        /// <summary>Eski yol: görüntü ana iş parçacığında çözülür (mipmap'li).</summary>
        Texture2D Decode(byte[] bytes)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, true) { name = name };
            if (bytes == null || !texture.LoadImage(bytes, true)) // CPU kopyası bırakılır (bellek)
            {
                Object.Destroy(texture);
                return null;
            }
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = aniso;
            return texture;
        }
    }
}
