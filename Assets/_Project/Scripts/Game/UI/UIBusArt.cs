using BusTycoon.Data;
using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// Otobüs modellerinin çizimleri (Resources/BusTycoon/UI/buses.png): SketchUp'ta kurulan modellerin önden-yandan
    /// görünümü (Tools/vehicles: bus_gen.py → vehicles.html → atlas.py). 2 sütun × 6 satır, hücre en-boy oranı 2:1
    /// (hücre genişliği = atlas genişliği / 2; 256×128 ya da 2x 512×256); otobüs sağa bakar. Atlas yoksa hiçbir şey çizilmez.
    /// </summary>
    public static class UIBusArt
    {
        /// <summary>Hücre sırası Tools/vehicles/atlas.py → IDS ile aynı.</summary>
        static readonly string[] ModelIds = { "KARTAL_M16", "KARTAL_M19", "ANKA_27", "ANKA_31", "TOROS_46", "TOROS_42C", "ZIRVE_VIP", "ZIRVE_DD",
            "KENT_12", "KENT_12E", "KENT_18" };
        const int Columns = 2;

        /// <summary>Hücre boyu atlasın genişliğinden: aynı kod 1x ve 2x atlasla çalışır.</summary>
        static float CellWidth(Texture2D texture) => texture.width / (float)Columns;
        static float CellHeight(Texture2D texture) => CellWidth(texture) / Aspect;

        static Texture2D atlas;
        static bool loaded;

        /// <summary>Çizimin en-boy oranı (genişlik / yükseklik).</summary>
        public const float Aspect = 2f;

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

        static int CellOf(BusModelData model)
        {
            int index = System.Array.IndexOf(ModelIds, model.Id);
            if (index < 0) index = Mathf.Clamp(model.Tier switch { 0 => 0, 1 => 2, 2 => 4, 3 => 5, 4 => 6, _ => 7 }, 0, ModelIds.Length - 1);
            return index;
        }

        static readonly System.Collections.Generic.Dictionary<int, Sprite> sprites = new System.Collections.Generic.Dictionary<int, Sprite>();

        /// <summary>Yeni arayüz (UI Toolkit) için modelin çizimi (hücre başına bir Sprite, önbellekli); atlas yoksa null.</summary>
        public static Sprite Sprite(BusModelData model)
        {
            Texture2D texture = Atlas;
            if (texture == null || model == null) return null;
            int index = CellOf(model);
            if (sprites.TryGetValue(index, out Sprite sprite) && sprite != null) return sprite;
            float cw = CellWidth(texture), ch = CellHeight(texture);
            var rect = new Rect((index % Columns) * cw, texture.height - ((index / Columns) + 1) * ch, cw, ch);
            sprite = UnityEngine.Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprites[index] = sprite;
            return sprite;
        }

        /// <summary>Modelin çizimi; bilinmeyen model segmentinin ilk çizimine düşer. <paramref name="faceLeft"/> yatay çevirir.</summary>
        public static void Draw(Rect rect, BusModelData model, bool faceLeft = false)
        {
            Texture2D texture = Atlas;
            if (texture == null || model == null || Event.current.type != EventType.Repaint) return;
            int index = CellOf(model);

            float cw = CellWidth(texture), ch = CellHeight(texture);
            float u = (index % Columns) * cw / texture.width;
            float v = 1f - ((index / Columns) + 1) * ch / texture.height;
            float w = cw / texture.width, h = ch / texture.height;
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
