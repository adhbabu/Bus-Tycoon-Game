using System.Collections.Generic;
using BusTycoon.Data;
using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// Şehirlerin kendine özgü ikonları (Resources/BusTycoon/UI/cities.png, Tools/make_city_icons.py): 8 sütun, 80 px
    /// hücre. Dizin dosyası (city_icons.txt) şehir adını hücreye ve kısa "nesiyle ünlü" açıklamasına bağlar.
    /// </summary>
    public static class UICityIcons
    {
        const int Columns = 8;
        const float Cell = 80f, Icon = 72f;

        static Texture2D atlas;
        static readonly Dictionary<string, (int cell, string description)> index = new Dictionary<string, (int, string)>();
        static bool loaded;

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            atlas = Resources.Load<Texture2D>(DefaultContent.CityIconsResourcePath);
            var text = Resources.Load<TextAsset>(DefaultContent.CityIconIndexResourcePath);
            if (text == null) return;
            foreach (string raw in text.text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;
                string[] parts = line.Split('\t');
                if (parts.Length >= 2 && int.TryParse(parts[1], out int cell))
                    index[parts[0]] = (cell, parts.Length > 2 ? parts[2] : string.Empty);
            }
        }

        public static bool Has(CityData city)
        {
            Load();
            return city != null && atlas != null && index.ContainsKey(city.DisplayName);
        }

        /// <summary>Şehrin nesiyle ünlü olduğu (ipucunda gösterilir); yoksa boş.</summary>
        public static string Description(CityData city)
        {
            Load();
            return city != null && index.TryGetValue(city.DisplayName, out var entry) ? entry.description : string.Empty;
        }

        public static void Draw(Rect rect, CityData city)
        {
            if (Event.current.type != EventType.Repaint || !TryGetUv(city, out Texture2D texture, out Rect uv)) return;
            GUI.DrawTextureWithTexCoords(rect, texture, uv);
        }

        static readonly System.Collections.Generic.Dictionary<string, Sprite> sprites = new System.Collections.Generic.Dictionary<string, Sprite>();

        /// <summary>Yeni arayüz (UI Toolkit) için şehrin ikonu (önbellekli); ikon yoksa null.</summary>
        public static Sprite Sprite(CityData city)
        {
            if (city == null) return null;
            if (sprites.TryGetValue(city.DisplayName, out Sprite sprite)) return sprite;
            if (TryGetUv(city, out Texture2D texture, out Rect uv))
            {
                var rect = Rect.MinMaxRect(Mathf.Max(0f, uv.xMin * texture.width), Mathf.Max(0f, uv.yMin * texture.height),
                    Mathf.Min(texture.width, uv.xMax * texture.width), Mathf.Min(texture.height, uv.yMax * texture.height));
                sprite = UnityEngine.Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            }
            sprites[city.DisplayName] = sprite;
            return sprite;
        }

        /// <summary>Şehrin ikonunun atlas dokusu ve UV dikdörtgeni (3B tabelalar da kullanır).</summary>
        public static bool TryGetUv(CityData city, out Texture2D texture, out Rect uv)
        {
            Load();
            texture = atlas;
            uv = default;
            if (atlas == null || city == null || !index.TryGetValue(city.DisplayName, out var entry)) return false;
            float pad = (Cell - Icon) / 2f;
            float x = (entry.cell % Columns) * Cell + pad;
            float yFromTop = (entry.cell / Columns) * Cell + pad;
            // Atlas 640×2240 üretilir; Unity en çok 2048 piksel içe aktarır (585×2048). UV'ler dokunun gerçek boyutuyla
            // değil, üretildiği (mantıksal) boyutla hesaplanır: küçültülmüş dokuda da doğru hücre gelir.
            float width = Columns * Cell;
            float height = Mathf.Max(Cell, Mathf.Round(width * atlas.height / atlas.width / Cell) * Cell);
            uv = new Rect(x / width, 1f - (yFromTop + Icon) / height, Icon / width, Icon / height);
            return true;
        }
    }
}
