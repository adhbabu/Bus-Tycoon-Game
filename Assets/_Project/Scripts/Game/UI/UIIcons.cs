using BusTycoon.Data;
using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>Arayüz ikonları; sıra Tools/make_icons.py içindeki ICONS listesiyle aynıdır.</summary>
    public enum UIIcon
    {
        NewRoute, Routes, Fleet, Dealership, Finance, Regions, Goals, Log,
        Help, Save, Settings, Star,
        Pin, People, Fuel, Wrench, Road, Seat, Calendar, Ticket,
        ChartUp, ChartDown, Clock, Building,
        Check, Cross, Info, Trophy, Warning, Party, Unlock, Sparkles,
        FlagTR, FlagBG, FlagGE, FlagGR, FlagRO, Lock, Finish, Coffee,
        Shield, Tools, Gift, Pilot, Ruler, Hourglass, Bank,
        Sun, Moon, Sunrise, Sunset,
        Plant, Bulb, Bagel, Screen, Wifi, Luggage, Taxi, Umbrella,
        Teddy, Shopping, Sofa, Fountain, MantelClock, Door, Smile
    }

    /// <summary>
    /// Twemoji ikon atlası (Resources/BusTycoon/UI/icons.png): 8 sütun, 80 px hücre, ortada 72 px ikon.
    /// Atlas yoksa ikonlar çizilmez; düzen bozulmaz.
    /// </summary>
    public static class UIIcons
    {
        const int Columns = 8;
        const float Cell = 80f, Icon = 72f;

        static Texture2D atlas;
        static bool loaded;

        public static Texture2D Atlas
        {
            get
            {
                if (!loaded)
                {
                    loaded = true;
                    atlas = Resources.Load<Texture2D>(DefaultContent.IconsResourcePath);
                }
                return atlas;
            }
        }

        /// <summary>Bölgenin bayrağı (DefaultContent.Regions kimlikleri); bilinmeyen bölgede dünya ikonu.</summary>
        public static UIIcon Flag(string regionId) => regionId switch
        {
            "TR" => UIIcon.FlagTR,
            "BG" => UIIcon.FlagBG,
            "GE" => UIIcon.FlagGE,
            "GR" => UIIcon.FlagGR,
            "RO" => UIIcon.FlagRO,
            _ => UIIcon.Regions
        };

        /// <summary>İkonu çizer; <paramref name="fraction"/> &lt; 1 ise yalnız soldan o kadarı (yarım yıldız).</summary>
        public static void Draw(Rect rect, UIIcon icon, float fraction = 1f)
        {
            Texture2D texture = Atlas;
            if (texture == null || Event.current.type != EventType.Repaint) return;
            fraction = Mathf.Clamp01(fraction);
            int index = (int)icon;
            float pad = (Cell - Icon) / 2f;
            float x = (index % Columns) * Cell + pad;
            float yFromTop = (index / Columns) * Cell + pad;
            var uv = new Rect(x / texture.width, 1f - (yFromTop + Icon) / texture.height, Icon * fraction / texture.width, Icon / texture.height);
            GUI.DrawTextureWithTexCoords(new Rect(rect.x, rect.y, rect.width * fraction, rect.height), texture, uv);
        }
    }
}
