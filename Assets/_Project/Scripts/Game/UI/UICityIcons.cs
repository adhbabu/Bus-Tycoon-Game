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
            Load();
            if (atlas == null || city == null || Event.current.type != EventType.Repaint) return;
            if (!index.TryGetValue(city.DisplayName, out var entry)) return;
            float pad = (Cell - Icon) / 2f;
            float x = (entry.cell % Columns) * Cell + pad;
            float yFromTop = (entry.cell / Columns) * Cell + pad;
            var uv = new Rect(x / atlas.width, 1f - (yFromTop + Icon) / atlas.height, Icon / atlas.width, Icon / atlas.height);
            GUI.DrawTextureWithTexCoords(rect, atlas, uv);
        }
    }
}
