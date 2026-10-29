using System.Collections.Generic;
using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// Durak modellerinin renk paleti: her yeni renk 32×32'lik dokuda bir hücre alır, modeller rengi UV ile okur.
    /// Böylece binlerce parça tek malzemeyle (Standard) çizilir. Renkler 8 bit'e yuvarlanarak eşlenir.
    /// </summary>
    public static class StationPalette
    {
        const int Size = 32;

        static readonly Dictionary<int, int> slots = new Dictionary<int, int>();
        static readonly Color32[] pixels = new Color32[Size * Size];
        static Texture2D texture;
        static Material material;
        static bool dirty;

        public static Vector2 Uv(Color color)
        {
            Color32 c = color;
            int key = (c.r << 16) | (c.g << 8) | c.b;
            if (!slots.TryGetValue(key, out int slot))
            {
                slot = slots.Count < pixels.Length ? slots.Count : 0; // dolarsa ilk renk (olmamalı: ~200 renk kullanılır)
                if (slots.Count < pixels.Length)
                {
                    slots[key] = slot;
                    pixels[slot] = new Color32(c.r, c.g, c.b, 255);
                    dirty = true;
                }
            }
            return new Vector2((slot % Size + 0.5f) / Size, (slot / Size + 0.5f) / Size);
        }

        public static int ColorCount => slots.Count;

        public static Texture2D Texture
        {
            get
            {
                if (texture == null)
                {
                    texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
                    {
                        name = "StationPalette",
                        hideFlags = HideFlags.HideAndDontSave,
                        filterMode = FilterMode.Point,
                        wrapMode = TextureWrapMode.Clamp
                    };
                    dirty = true;
                }
                Upload();
                return texture;
            }
        }

        /// <summary>Yeni renkleri dokuya yükler (mesh kurulduktan sonra çağrılır).</summary>
        public static void Upload()
        {
            if (!dirty || texture == null) return;
            dirty = false;
            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        /// <summary>Paylaşılan ışıklı malzeme (Standard + palet dokusu).</summary>
        public static Material Material
        {
            get
            {
                if (material != null) return material;
                material = MaterialFactory.CreateLit(Color.white, 0.18f);
                material.name = "StationLit";
                material.mainTexture = Texture;
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", Texture);
                return material;
            }
        }
    }
}
