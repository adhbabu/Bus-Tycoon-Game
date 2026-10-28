using System.Collections.Generic;
using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// IMGUI görsel dili: koyu yarı saydam paneller, yuvarlak köşeli butonlar, renk paleti.
    /// Tüm ölçüler <c>unit</c> ile çarpılarak GERÇEK piksel olarak üretilir (GUI.matrix ölçeklemesi yazıları
    /// bulanıklaştırdığı için kullanılmaz). Ekran yoğunluğu değişince stiller yeniden kurulur.
    /// Dokular kodla üretilir; GUI.skin'e yalnızca OnGUI içinden erişilebildiği için ilk OnGUI'de kurulur.
    /// </summary>
    public sealed class UIStyles
    {
        public static readonly Color Text = new Color(0.93f, 0.95f, 0.98f);
        public static readonly Color DimText = new Color(0.62f, 0.68f, 0.76f);
        public static readonly Color Green = new Color(0.36f, 0.87f, 0.5f);
        public static readonly Color Yellow = new Color(1f, 0.83f, 0.32f);
        public static readonly Color Orange = new Color(1f, 0.6f, 0.24f);
        public static readonly Color Red = new Color(0.97f, 0.38f, 0.38f);
        public static readonly Color Blue = new Color(0.45f, 0.74f, 1f);
        public static readonly Color Gold = new Color(1f, 0.8f, 0.3f);

        readonly float u;
        readonly List<Texture2D> textures = new List<Texture2D>();

        public readonly GUIStyle Panel, TopBar, Card, Modal, Toast, Tooltip;
        public readonly GUIStyle Title, H1, H2, Label, Small, Dim, Bold, Big, Center, Wrap, WrapDim, Right;
        public readonly GUIStyle Button, Primary, Danger, Ghost, Tab, TabActive, SpeedButton, SpeedButtonActive, Field, Toggle;
        public readonly GUIStyle Slider, SliderThumb, VerticalScrollbar;
        public readonly GUIStyle MapLabel, MapLabelBold, MapLabelShadow, MapLabelBoldShadow;

        public float Unit => u;

        public UIStyles(float unit)
        {
            u = unit;
            Font font = GUI.skin.font;

            Panel = Box(new Color(0.06f, 0.08f, 0.11f, 0.94f), 16, 14);
            TopBar = Box(new Color(0.05f, 0.07f, 0.1f, 0.96f), 0, 0, radius: 0);
            Card = Box(new Color(1f, 1f, 1f, 0.05f), 10, 8);
            Modal = Box(new Color(0.08f, 0.1f, 0.14f, 0.98f), 28, 24);
            Toast = Box(new Color(0.1f, 0.13f, 0.18f, 0.95f), 14, 8);
            Tooltip = Box(new Color(0.02f, 0.03f, 0.05f, 0.92f), 10, 6);

            Label = MakeLabel(15, FontStyle.Normal, Text);
            Small = MakeLabel(13, FontStyle.Normal, Text);
            Dim = MakeLabel(14, FontStyle.Normal, DimText);
            Bold = MakeLabel(15, FontStyle.Bold, Text);
            Big = MakeLabel(26, FontStyle.Bold, Text);
            H2 = MakeLabel(17, FontStyle.Bold, Text);
            H1 = MakeLabel(21, FontStyle.Bold, Text);
            Title = MakeLabel(46, FontStyle.Bold, Text);
            Title.alignment = TextAnchor.MiddleCenter;
            Center = MakeLabel(15, FontStyle.Normal, Text);
            Center.alignment = TextAnchor.MiddleCenter;
            Right = MakeLabel(15, FontStyle.Bold, Text);
            Right.alignment = TextAnchor.MiddleRight;
            Wrap = MakeLabel(14, FontStyle.Normal, Text);
            Wrap.wordWrap = true;
            WrapDim = MakeLabel(13, FontStyle.Normal, DimText);
            WrapDim.wordWrap = true;

            Button = MakeButton(new Color(0.17f, 0.24f, 0.33f), 15);
            Primary = MakeButton(new Color(0.16f, 0.55f, 0.33f), 16);
            Primary.fontStyle = FontStyle.Bold;
            Danger = MakeButton(new Color(0.62f, 0.2f, 0.2f), 15);
            Ghost = MakeButton(new Color(1f, 1f, 1f, 0.06f), 14);
            Ghost.alignment = TextAnchor.MiddleLeft;
            Tab = MakeButton(new Color(1f, 1f, 1f, 0.06f), 14);
            TabActive = MakeButton(new Color(0.24f, 0.45f, 0.7f), 14);
            TabActive.fontStyle = FontStyle.Bold;
            SpeedButton = MakeButton(new Color(1f, 1f, 1f, 0.08f), 14);
            SpeedButton.fixedWidth = Px(42);
            SpeedButtonActive = MakeButton(new Color(0.24f, 0.45f, 0.7f), 14);
            SpeedButtonActive.fixedWidth = Px(42);
            SpeedButtonActive.fontStyle = FontStyle.Bold;

            Field = new GUIStyle(GUI.skin.textField) { fontSize = Px(17), padding = Offset(10, 10, 8, 8) };
            Field.normal.textColor = Field.focused.textColor = Field.hover.textColor = Text;
            Field.normal.background = Field.hover.background = Rounded(new Color(1f, 1f, 1f, 0.08f), 6);
            Field.focused.background = Rounded(new Color(1f, 1f, 1f, 0.14f), 6);
            Field.border = Offset(7, 7, 7, 7);

            // Onay kutusu ölçekle büyür: Unity'nin hazır kutusu sabit 14 px'tir, telefonda neredeyse görünmez.
            // Doku = kutu + saydam şerit; 9-dilimde kutu (sol kenar) esnemez, yükseklik satır yüksekliğiyle aynıdır.
            int box = Px(22), row = Px(32);
            Toggle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Px(14),
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(box + Px(10), 0, 0, 0),
                border = new RectOffset(box, 0, 0, 0),
                margin = Offset(4, 4, 2, 2)
            };
            Toggle.normal.background = Toggle.hover.background = Toggle.active.background = Checkbox(box, row, false);
            Toggle.onNormal.background = Toggle.onHover.background = Toggle.onActive.background = Checkbox(box, row, true);
            Toggle.normal.textColor = Toggle.hover.textColor = Toggle.active.textColor = Text;
            Toggle.onNormal.textColor = Toggle.onHover.textColor = Toggle.onActive.textColor = Text;

            // Dokunmatik ekranda tutulabilir kalın kaydırıcı: ince ray + büyük yuvarlak tutamaç.
            Slider = new GUIStyle(GUI.skin.horizontalSlider) { fixedHeight = Px(26), border = Offset(6, 6, 0, 0), margin = Offset(4, 4, 6, 6) };
            Slider.normal.background = Track(new Color(1f, 1f, 1f, 0.18f));
            SliderThumb = new GUIStyle(GUI.skin.horizontalSliderThumb) { fixedWidth = Px(26), fixedHeight = Px(26), border = new RectOffset(0, 0, 0, 0) };
            SliderThumb.normal.background = SliderThumb.hover.background = Disc(new Color(0.45f, 0.74f, 1f));
            SliderThumb.active.background = Disc(new Color(0.75f, 0.88f, 1f));
            VerticalScrollbar = GUI.skin.verticalScrollbar;

            MapLabel = MakeLabel(13, FontStyle.Normal, Text);
            MapLabel.alignment = TextAnchor.MiddleCenter;
            MapLabelBold = MakeLabel(15, FontStyle.Bold, Text);
            MapLabelBold.alignment = TextAnchor.MiddleCenter;
            MapLabelShadow = new GUIStyle(MapLabel);
            MapLabelShadow.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            MapLabelBoldShadow = new GUIStyle(MapLabelBold);
            MapLabelBoldShadow.normal.textColor = new Color(0f, 0f, 0f, 0.85f);

            if (font != null)
                foreach (var style in new[] { Label, Small, Dim, Bold, Big, H1, H2, Title, Center, Right, Wrap, WrapDim, Button, Primary, Danger, Ghost, Tab, TabActive, MapLabel, MapLabelBold })
                    style.font = font;
        }

        /// <summary>Stiller yeniden kurulurken eski dokuları serbest bırakır.</summary>
        public void Dispose()
        {
            foreach (var texture in textures)
                if (texture != null) Object.Destroy(texture);
            textures.Clear();
        }

        int Px(float value) => Mathf.Max(1, Mathf.RoundToInt(value * u));

        RectOffset Offset(int left, int right, int top, int bottom) =>
            new RectOffset(Mathf.RoundToInt(left * u), Mathf.RoundToInt(right * u), Mathf.RoundToInt(top * u), Mathf.RoundToInt(bottom * u));

        GUIStyle MakeLabel(int size, FontStyle fontStyle, Color color)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = Px(size), fontStyle = fontStyle, wordWrap = false, richText = false };
            style.normal.textColor = color;
            style.margin = Offset(2, 2, 2, 2);
            style.padding = Offset(2, 2, 1, 1);
            return style;
        }

        GUIStyle Box(Color color, int padX, int padY, int radius = 9)
        {
            int r = radius > 0 ? Px(radius) : 0;
            var style = new GUIStyle();
            style.normal.background = r > 0 ? Rounded(color, radius) : Solid(color);
            style.border = new RectOffset(r + 1, r + 1, r + 1, r + 1);
            style.padding = Offset(padX, padX, padY, padY);
            style.margin = Offset(0, 0, 3, 3);
            return style;
        }

        GUIStyle MakeButton(Color color, int fontSize)
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = Px(fontSize),
                alignment = TextAnchor.MiddleCenter,
                padding = Offset(12, 12, 7, 7),
                margin = Offset(3, 3, 3, 3),
                border = new RectOffset(Px(7) + 1, Px(7) + 1, Px(7) + 1, Px(7) + 1),
                wordWrap = false
            };
            Color hover = Color.Lerp(color, Color.white, 0.15f);
            hover.a = Mathf.Max(color.a, 0.2f);
            Color active = Color.Lerp(color, Color.black, 0.2f);
            active.a = Mathf.Max(color.a, 0.3f);

            style.normal.background = Rounded(color, 7);
            style.hover.background = Rounded(hover, 7);
            style.active.background = Rounded(active, 7);
            style.focused.background = style.normal.background;
            style.onNormal.background = style.active.background;
            style.normal.textColor = style.hover.textColor = style.active.textColor = style.focused.textColor = style.onNormal.textColor = Text;
            return style;
        }

        Texture2D NewTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            textures.Add(texture);
            return texture;
        }

        Texture2D Solid(Color color)
        {
            var texture = NewTexture(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        /// <summary>Kenarları yumuşatılmış (anti-aliased) yuvarlak köşeli doku; 9-dilim (border) ile esner.</summary>
        Texture2D Rounded(Color color, int radiusUnits)
        {
            int radius = Px(radiusUnits);
            int size = radius * 2 + 4;
            var texture = NewTexture(size, size);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float cx = Mathf.Clamp(px, radius, size - radius);
                    float cy = Mathf.Clamp(py, radius, size - radius);
                    float distance = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                    float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    pixels[y * size + x] = new Color(color.r, color.g, color.b, color.a * alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>Onay kutusu dokusu: dikeyde ortalanmış yuvarlak kare (seçiliyse mavi + beyaz tik), sağda saydam şerit.</summary>
        Texture2D Checkbox(int box, int height, bool on)
        {
            int width = box + 2;
            var texture = NewTexture(width, height);
            var pixels = new Color[width * height];
            float radius = box * 0.22f, stroke = Mathf.Max(1.5f, box * 0.09f), half = box / 2f;
            float centerY = height / 2f;
            Color fill = on ? Blue : new Color(1f, 1f, 1f, 0.08f);
            Color edge = on ? Blue : new Color(Text.r, Text.g, Text.b, 0.7f);

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float px = x + 0.5f - half, py = y + 0.5f - centerY;
                // Yuvarlak kareye işaretli uzaklık (içeride negatif)
                float qx = Mathf.Abs(px) - (half - radius), qy = Mathf.Abs(py) - (half - radius);
                float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                float distance = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;

                float shape = Mathf.Clamp01(0.5f - distance);
                float border = Mathf.Clamp01(0.5f - Mathf.Abs(distance + stroke / 2f) + stroke / 2f);
                Color color = Color.Lerp(fill, edge, border);
                color.a *= shape;

                if (on)
                {
                    // Tik: dokuda y yukarı doğru artar.
                    float u0 = (px + half) / box, v0 = (py + half) / box;
                    float tick = Mathf.Min(SegmentDistance(u0, v0, 0.24f, 0.52f, 0.42f, 0.32f), SegmentDistance(u0, v0, 0.42f, 0.32f, 0.78f, 0.72f)) * box;
                    float mark = Mathf.Clamp01(box * 0.07f - tick + 0.5f);
                    color = Color.Lerp(color, Color.white, mark * shape);
                }
                pixels[y * width + x] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        static float SegmentDistance(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy));
            float ex = px - (ax + t * dx), ey = py - (ay + t * dy);
            return Mathf.Sqrt(ex * ex + ey * ey);
        }

        /// <summary>Kaydırıcı rayı: dikeyde ortada ince bant (yükseklik boyunca saydam kenar).</summary>
        Texture2D Track(Color color)
        {
            const int width = 12, height = 24;
            var texture = NewTexture(width, height);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float band = Mathf.Clamp01(1f - Mathf.Abs(y + 0.5f - height / 2f) / 3.5f + 0.5f);
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = new Color(color.r, color.g, color.b, color.a * band);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        Texture2D Disc(Color color)
        {
            const int size = 48;
            var texture = NewTexture(size, size);
            var pixels = new Color[size * size];
            float r = size / 2f - 1f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - size / 2f) * (x + 0.5f - size / 2f) + (y + 0.5f - size / 2f) * (y + 0.5f - size / 2f));
                pixels[y * size + x] = new Color(color.r, color.g, color.b, Mathf.Clamp01(r - d + 0.5f));
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
