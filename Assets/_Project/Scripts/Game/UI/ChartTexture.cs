using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// Grafikleri piksel tamponuna çizip dokuya aktaran küçük rasterleştirici. IMGUI'de çizgi primitifi
    /// olmadığı için fiyat eğrisi ve çubuk grafikler bu yolla çizilir (shader gerektirmez).
    /// Koordinatlar piksel cinsindendir; (0,0) sol alt köşe.
    /// </summary>
    public sealed class ChartTexture
    {
        readonly Color32[] pixels;

        public ChartTexture(int width, int height)
        {
            Width = width;
            Height = height;
            pixels = new Color32[width * height];
            Texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }

        public int Width { get; }
        public int Height { get; }
        public Texture2D Texture { get; }

        public void Clear(Color32 color)
        {
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        }

        public void FillRect(float x0, float y0, float x1, float y1, Color32 color)
        {
            int xa = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(x0, x1)), 0, Width);
            int xb = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(x0, x1)), 0, Width);
            int ya = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(y0, y1)), 0, Height);
            int yb = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(y0, y1)), 0, Height);
            for (int y = ya; y < yb; y++)
            for (int x = xa; x < xb; x++)
                Blend(x, y, color);
        }

        /// <summary>Kalınlıklı çizgi (kare fırça ile Bresenham benzeri adımlama).</summary>
        public void Line(float x0, float y0, float x1, float y1, Color32 color, int thickness = 2)
        {
            float dx = x1 - x0, dy = y1 - y0;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy))));
            int half = thickness / 2;
            for (int i = 0; i <= steps; i++)
            {
                int cx = Mathf.RoundToInt(x0 + dx * i / steps);
                int cy = Mathf.RoundToInt(y0 + dy * i / steps);
                for (int oy = -half; oy < thickness - half; oy++)
                for (int ox = -half; ox < thickness - half; ox++)
                    Set(cx + ox, cy + oy, color);
            }
        }

        public void VerticalLine(float x, Color32 color, int thickness = 2) => Line(x, 0, x, Height - 1, color, thickness);

        public void HorizontalLine(float y, Color32 color, int thickness = 1) => Line(0, y, Width - 1, y, color, thickness);

        public void Apply()
        {
            Texture.SetPixels32(pixels);
            Texture.Apply(false);
        }

        void Set(int x, int y, Color32 color)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            pixels[y * Width + x] = color;
        }

        void Blend(int x, int y, Color32 color)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            int i = y * Width + x;
            Color32 dst = pixels[i];
            float a = color.a / 255f;
            pixels[i] = new Color32(
                (byte)(dst.r + (color.r - dst.r) * a),
                (byte)(dst.g + (color.g - dst.g) * a),
                (byte)(dst.b + (color.b - dst.b) * a),
                (byte)Mathf.Max(dst.a, color.a));
        }
    }
}
