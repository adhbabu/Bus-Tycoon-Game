using System.Collections.Generic;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace BusTycoon.Game
{
    /// <summary>
    /// Tarayıcıda dokunuşun içinde yapılması gereken işler (Assets/Plugins/WebGL/BusTycoonTap.jslib): yazı girişi
    /// için klavyeli pencere (iPad ve telefonda Unity klavye açamaz) ve davet bağlantısını paylaşma/kopyalama.
    /// Arayüz her Repaint'te ekrandaki alanları bildirir; sonuçlar kimliğe göre <see cref="TryTake"/> ile alınır.
    /// Web dışında <see cref="Available"/> false: çağıran kendi yolunu (TextField, Unity panosu) kullanır.
    /// </summary>
    public static class WebTap
    {
        public const int PromptKind = 1, ShareKind = 2;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void BusTycoon_TapBegin();
        [DllImport("__Internal")] static extern void BusTycoon_TapZone(string id, int kind, float x, float y, float w, float h, string a, string b);
        [DllImport("__Internal")] static extern void BusTycoon_TapEnd();
        [DllImport("__Internal")] static extern string BusTycoon_TapPoll();

        public static bool Available => true;
#else
        public static bool Available => false;
#endif

        static readonly Dictionary<string, string> results = new Dictionary<string, string>();

        /// <summary>Repaint başı: bu karenin alanları toplanmaya başlar.</summary>
        public static void BeginFrame()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            BusTycoon_TapBegin();
#endif
        }

        /// <summary>Repaint sonu: toplanan alanlar dokunuşlara açılır (önceki karenin alanlarının yerine).</summary>
        public static void EndFrame()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            BusTycoon_TapEnd();
#endif
        }

        /// <summary>GUI dikdörtgenini (grup ve kaydırma konumu dahil) ekran pikseli olarak bildir.</summary>
        public static void Zone(string id, int kind, Rect guiRect, string a, string b)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Vector2 min = GUIUtility.GUIToScreenPoint(new Vector2(guiRect.x, guiRect.y));
            Vector2 max = GUIUtility.GUIToScreenPoint(new Vector2(guiRect.xMax, guiRect.yMax));
            BusTycoon_TapZone(id, kind, min.x, min.y, max.x - min.x, max.y - min.y, a ?? string.Empty, b ?? string.Empty);
#endif
        }

        /// <summary>Tarayıcıdan gelen sonuçları al (her karede).</summary>
        public static void Pump()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            for (int i = 0; i < 16; i++)
            {
                string result = BusTycoon_TapPoll();
                if (string.IsNullOrEmpty(result)) break;
                int split = result.IndexOf('\n');
                if (split > 0) results[result.Substring(0, split)] = result.Substring(split + 1);
            }
#endif
        }

        public static bool TryTake(string id, out string value) => results.Remove(id, out value);
    }
}
