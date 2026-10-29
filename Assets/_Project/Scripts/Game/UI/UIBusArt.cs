using BusTycoon.Data;
using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// Otobüs modellerinin yandan çizimleri (Resources/BusTycoon/UI/buses.png, Tools/make_bus_art.py):
    /// 2 sütun × 4 satır, 256×128 px hücre; otobüs sağa bakar. Atlas yoksa hiçbir şey çizilmez.
    /// </summary>
    public static class UIBusArt
    {
        /// <summary>Hücre sırası Tools/make_bus_art.py → MODELS ile aynı.</summary>
        static readonly string[] ModelIds = { "KARTAL_M16", "KARTAL_M19", "ANKA_27", "ANKA_31", "TOROS_46", "TOROS_42C", "ZIRVE_VIP", "ZIRVE_DD" };
        const int Columns = 2;
        const float CellWidth = 256f, CellHeight = 128f;

        static Texture2D atlas;
        static bool loaded;

        /// <summary>Çizimin en-boy oranı (genişlik / yükseklik).</summary>
        public const float Aspect = CellWidth / CellHeight;

        static Texture2D Atlas
        {
            get
            {
                if (!loaded)
                {
                    loaded = true;
                    atlas = Resources.Load<Texture2D>(DefaultContent.BusArtResourcePath);
                }
                return atlas;
            }
        }

        /// <summary>Modelin çizimi; bilinmeyen model segmentinin ilk çizimine düşer. <paramref name="faceLeft"/> yatay çevirir.</summary>
        public static void Draw(Rect rect, BusModelData model, bool faceLeft = false)
        {
            Texture2D texture = Atlas;
            if (texture == null || model == null || Event.current.type != EventType.Repaint) return;
            int index = System.Array.IndexOf(ModelIds, model.Id);
            if (index < 0) index = Mathf.Clamp(model.Tier switch { 0 => 0, 1 => 2, 2 => 4, 3 => 5, 4 => 6, _ => 7 }, 0, ModelIds.Length - 1);

            float u = (index % Columns) * CellWidth / texture.width;
            float v = 1f - ((index / Columns) + 1) * CellHeight / texture.height;
            float w = CellWidth / texture.width, h = CellHeight / texture.height;
            var uv = faceLeft ? new Rect(u + w, v, -w, h) : new Rect(u, v, w, h);
            GUI.DrawTextureWithTexCoords(rect, texture, uv);
        }

        /// <summary>Düzen içinde verilen yükseklikte (piksel) çizim alanı ayırıp çizer; <paramref name="alpha"/> kilitli modelleri soluklaştırır.</summary>
        public static void Layout(BusModelData model, float heightPixels, float alpha = 1f)
        {
            float width = heightPixels * Aspect;
            Rect rect = GUILayoutUtility.GetRect(width, heightPixels, GUILayout.Width(width), GUILayout.Height(heightPixels));
            Color previous = GUI.color;
            GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * alpha);
            Draw(rect, model);
            GUI.color = previous;
        }
    }
}
