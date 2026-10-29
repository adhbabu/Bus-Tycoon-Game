using System.Collections.Generic;
using BusTycoon.Data;
using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// IMGUI görsel dili: yumuşak gölgeli, degrade dolgulu yuvarlak paneller; alt dudağı olan "şeker" butonlar
    /// (basınca içeri göçer); yuvarlak hatlı Nunito yazı tipi ve sıcak bir renk paleti.
    /// Tüm ölçüler <c>unit</c> ile çarpılarak GERÇEK piksel olarak üretilir (GUI.matrix ölçeklemesi yazıları
    /// bulanıklaştırdığı için kullanılmaz). Ekran yoğunluğu değişince stiller yeniden kurulur.
    /// Dokular kodla üretilir; GUI.skin'e yalnızca OnGUI içinden erişilebildiği için ilk OnGUI'de kurulur.
    /// </summary>
    public sealed class UIStyles
    {
        public static readonly Color Text = new Color(0.98f, 0.97f, 0.94f);
        public static readonly Color DimText = new Color(0.72f, 0.75f, 0.86f);
        public static readonly Color Green = new Color(0.42f, 0.9f, 0.56f);
        public static readonly Color Yellow = new Color(1f, 0.85f, 0.36f);
        public static readonly Color Orange = new Color(1f, 0.63f, 0.3f);
        public static readonly Color Red = new Color(1f, 0.46f, 0.48f);
        public static readonly Color Blue = new Color(0.47f, 0.77f, 1f);
        public static readonly Color Gold = new Color(1f, 0.81f, 0.32f);

        // Buton renkleri (gövdenin alt tonu; üstü daha açık, dudağı daha koyu çizilir)
        static readonly Color NeutralButton = new Color(0.29f, 0.34f, 0.56f);
        static readonly Color PrimaryButton = new Color(0.23f, 0.72f, 0.42f);
        static readonly Color DangerButton = new Color(0.9f, 0.34f, 0.38f);
        static readonly Color ActiveButton = new Color(0.25f, 0.56f, 0.95f);

        static Font regularFont, heavyFont;
        static bool fontsLoaded;

        readonly float u;
        readonly List<Texture2D> textures = new List<Texture2D>();
        readonly Font regular, heavy;

        public readonly GUIStyle Panel, TopBar, BottomBar, Card, Modal, Toast, Tooltip, Bar, Dot;
        public readonly GUIStyle Title, H1, H2, Label, Small, Dim, Bold, Big, Center, Wrap, WrapDim, Right;
        public readonly GUIStyle Button, Primary, Danger, Ghost, Tab, TabActive, SpeedButton, SpeedButtonActive, Field, Toggle;
        public readonly GUIStyle Slider, SliderThumb, VerticalScrollbar;
        public readonly GUIStyle MapLabel, MapLabelBold, MapLabelShadow, MapLabelBoldShadow;

        public float Unit => u;

        public UIStyles(float unit)
        {
            u = unit;
            LoadFonts();
            regular = regularFont != null ? regularFont : GUI.skin.font;
            heavy = heavyFont != null ? heavyFont : regular;

            Panel = Box(new Color(0.17f, 0.2f, 0.35f, 0.96f), new Color(0.11f, 0.13f, 0.25f, 0.96f), 16, 14, radius: 16, shadow: 12);
            TopBar = Box(new Color(0.09f, 0.11f, 0.21f, 0.97f), new Color(0.14f, 0.17f, 0.31f, 0.97f), 0, 0, radius: 0, shadow: 10, outline: 0f);
            BottomBar = Box(new Color(0.14f, 0.17f, 0.31f, 0.97f), new Color(0.09f, 0.11f, 0.21f, 0.97f), 0, 0, radius: 0, shadow: 10, outline: 0f);
            Card = Box(new Color(1f, 1f, 1f, 0.08f), new Color(1f, 1f, 1f, 0.05f), 10, 8, radius: 12, shadow: 0, outline: 0.06f);
            Modal = Box(new Color(0.19f, 0.22f, 0.39f, 0.98f), new Color(0.12f, 0.14f, 0.27f, 0.98f), 28, 24, radius: 22, shadow: 18);
            Toast = Box(new Color(0.18f, 0.21f, 0.37f, 0.97f), new Color(0.13f, 0.15f, 0.29f, 0.97f), 14, 8, radius: 18, shadow: 8);
            Tooltip = Box(new Color(0.1f, 0.11f, 0.2f, 0.96f), new Color(0.07f, 0.08f, 0.15f, 0.96f), 10, 6, radius: 10, shadow: 6);

            // İlerleme çubuğu ve renkli nokta: beyaz doku, GUI.backgroundColor ile boyanır.
            Bar = new GUIStyle { border = new RectOffset(Px(3), Px(3), Px(3), Px(3)) };
            Bar.normal.background = Rounded(Color.white, 3);
            Dot = new GUIStyle();
            Dot.normal.background = Disc(Color.white, Color.white, 0f);

            Label = MakeLabel(15, false, Text);
            Small = MakeLabel(13, false, Text);
            Dim = MakeLabel(14, false, DimText);
            Bold = MakeLabel(15, true, Text);
            Big = MakeLabel(26, true, Text);
            H2 = MakeLabel(17, true, Text);
            H1 = MakeLabel(21, true, Text);
            Title = MakeLabel(44, true, Gold);
            Title.alignment = TextAnchor.MiddleCenter;
            Center = MakeLabel(15, false, Text);
            Center.alignment = TextAnchor.MiddleCenter;
            Right = MakeLabel(15, true, Text);
            Right.alignment = TextAnchor.MiddleRight;
            Wrap = MakeLabel(14, false, Text);
            Wrap.wordWrap = true;
            WrapDim = MakeLabel(13, false, DimText);
            WrapDim.wordWrap = true;

            Button = CandyButton(NeutralButton, 15);
            Primary = CandyButton(PrimaryButton, 16);
            Danger = CandyButton(DangerButton, 15);
            Ghost = FlatButton(new Color(1f, 1f, 1f, 0.07f), 14);
            Ghost.alignment = TextAnchor.MiddleLeft;
            Tab = FlatButton(new Color(1f, 1f, 1f, 0.07f), 14);
            TabActive = CandyButton(ActiveButton, 14);
            SpeedButton = FlatButton(new Color(1f, 1f, 1f, 0.09f), 14);
            SpeedButton.fixedWidth = Px(42);
            SpeedButtonActive = CandyButton(ActiveButton, 14);
            SpeedButtonActive.fixedWidth = Px(42);

            Field = new GUIStyle(GUI.skin.textField) { font = regular, fontSize = Px(17), padding = Offset(12, 12, 8, 8) };
            Field.normal.textColor = Field.focused.textColor = Field.hover.textColor = Text;
            Field.normal.background = Field.hover.background = BoxTexture(new Color(1f, 1f, 1f, 0.09f), new Color(1f, 1f, 1f, 0.07f), 10, new Color(1f, 1f, 1f, 0.14f), 0, out RectOffset fieldBorder, out _);
            Field.focused.background = BoxTexture(new Color(1f, 1f, 1f, 0.15f), new Color(1f, 1f, 1f, 0.12f), 10, new Color(Blue.r, Blue.g, Blue.b, 0.8f), 0, out _, out _);
            Field.border = fieldBorder;

            // Onay kutusu ölçekle büyür: Unity'nin hazır kutusu sabit 14 px'tir, telefonda neredeyse görünmez.
            // Doku = kutu + saydam şerit; 9-dilimde kutu (sol kenar) esnemez, yükseklik satır yüksekliğiyle aynıdır.
            int box = Px(22), row = Px(32);
            Toggle = new GUIStyle(GUI.skin.label)
            {
                font = regular,
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

            // Dokunmatik ekranda tutulabilir kalın kaydırıcı: yuvarlak uçlu ray + beyaz halkalı büyük tutamaç.
            Slider = new GUIStyle(GUI.skin.horizontalSlider) { fixedHeight = Px(26), border = Offset(6, 6, 0, 0), margin = Offset(4, 4, 6, 6) };
            Slider.normal.background = Track(new Color(1f, 1f, 1f, 0.2f));
            SliderThumb = new GUIStyle(GUI.skin.horizontalSliderThumb) { fixedWidth = Px(26), fixedHeight = Px(26), border = new RectOffset(0, 0, 0, 0) };
            SliderThumb.normal.background = SliderThumb.hover.background = Disc(Blue, Color.white, 0.2f);
            SliderThumb.active.background = Disc(new Color(0.75f, 0.88f, 1f), Color.white, 0.2f);
            VerticalScrollbar = GUI.skin.verticalScrollbar;

            MapLabel = MakeLabel(13, false, Text);
            MapLabel.alignment = TextAnchor.MiddleCenter;
            MapLabelBold = MakeLabel(15, true, Text);
            MapLabelBold.alignment = TextAnchor.MiddleCenter;
            MapLabelShadow = new GUIStyle(MapLabel);
            MapLabelShadow.normal.textColor = new Color(0.04f, 0.05f, 0.12f, 0.85f);
            MapLabelBoldShadow = new GUIStyle(MapLabelBold);
            MapLabelBoldShadow.normal.textColor = new Color(0.04f, 0.05f, 0.12f, 0.85f);
        }

        /// <summary>Stiller yeniden kurulurken eski dokuları serbest bırakır.</summary>
        public void Dispose()
        {
            foreach (var texture in textures)
                if (texture != null) Object.Destroy(texture);
            textures.Clear();
        }

        static void LoadFonts()
        {
            if (fontsLoaded) return;
            fontsLoaded = true;
            regularFont = Resources.Load<Font>(DefaultContent.RegularFontResourcePath);
            heavyFont = Resources.Load<Font>(DefaultContent.HeavyFontResourcePath);
            if (regularFont == null || heavyFont == null)
                Debug.LogWarning("[UI] Nunito yazı tipi bulunamadı; Unity'nin varsayılan yazı tipi kullanılıyor.");
        }

        int Px(float value) => Mathf.Max(1, Mathf.RoundToInt(value * u));

        RectOffset Offset(int left, int right, int top, int bottom) =>
            new RectOffset(Mathf.RoundToInt(left * u), Mathf.RoundToInt(right * u), Mathf.RoundToInt(top * u), Mathf.RoundToInt(bottom * u));

        /// <summary>Kalın yazı ayrı bir yazı tipi dosyasıdır (ExtraBold); yapay kalınlaştırma yalnız yedek yazı tipinde.</summary>
        GUIStyle MakeLabel(int size, bool bold, Color color)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = Px(size), wordWrap = false, richText = false };
            ApplyFont(style, bold);
            style.normal.textColor = color;
            style.margin = Offset(2, 2, 2, 2);
            style.padding = Offset(2, 2, 1, 1);
            return style;
        }

        void ApplyFont(GUIStyle style, bool bold)
        {
            style.font = bold ? heavy : regular;
            style.fontStyle = bold && heavyFont == null ? FontStyle.Bold : FontStyle.Normal;
        }

        GUIStyle Box(Color top, Color bottom, int padX, int padY, int radius, int shadow, float outline = 0.1f)
        {
            var style = new GUIStyle();
            style.normal.background = BoxTexture(top, bottom, radius, new Color(1f, 1f, 1f, outline), shadow, out RectOffset border, out RectOffset overflow);
            style.border = border;
            style.overflow = overflow;
            style.padding = Offset(padX, padX, padY, padY);
            style.margin = Offset(0, 0, 3, 3);
            return style;
        }

        /// <summary>
        /// Şeker buton: üstten alta açılan degrade gövde, ince parlak üst çizgi ve koyu alt "dudak".
        /// Basılıyken dudak kaybolur ve gövde aşağı iner; yazı gövdenin ortasında kalsın diye alt boşluk dudak kadar artar.
        /// </summary>
        GUIStyle CandyButton(Color color, int fontSize)
        {
            int lip = Px(3);
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = Px(fontSize),
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(Px(12), Px(12), Px(6), Px(6) + lip),
                margin = Offset(3, 3, 3, 3),
                wordWrap = false
            };
            ApplyFont(style, true);

            Color hover = Color.Lerp(color, Color.white, 0.14f);
            style.normal.background = CandyTexture(color, lip, false, out RectOffset border);
            style.hover.background = CandyTexture(hover, lip, false, out _);
            style.active.background = CandyTexture(Color.Lerp(color, Color.black, 0.08f), lip, true, out _);
            style.focused.background = style.normal.background;
            style.onNormal.background = style.active.background;
            style.border = border;
            SetTextColor(style, Text);
            return style;
        }

        /// <summary>Düz, yarı saydam buton (sekme, hayalet, hız): zemini hafifçe aydınlanır.</summary>
        GUIStyle FlatButton(Color color, int fontSize)
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = Px(fontSize),
                alignment = TextAnchor.MiddleCenter,
                padding = Offset(12, 12, 7, 7),
                margin = Offset(3, 3, 3, 3),
                wordWrap = false
            };
            ApplyFont(style, false);

            Color hover = new Color(1f, 1f, 1f, color.a + 0.07f);
            Color active = new Color(1f, 1f, 1f, color.a + 0.13f);
            style.normal.background = BoxTexture(color, color, 10, Color.clear, 0, out RectOffset border, out _);
            style.hover.background = BoxTexture(hover, hover, 10, Color.clear, 0, out _, out _);
            style.active.background = BoxTexture(active, active, 10, Color.clear, 0, out _, out _);
            style.focused.background = style.normal.background;
            style.onNormal.background = style.active.background;
            style.border = border;
            SetTextColor(style, Text);
            return style;
        }

        static void SetTextColor(GUIStyle style, Color color) =>
            style.normal.textColor = style.hover.textColor = style.active.textColor = style.focused.textColor = style.onNormal.textColor = color;

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

        /// <summary>Yuvarlak köşeli dikdörtgene işaretli uzaklık (içeride negatif); merkez (cx, cy), yarı boyut (hx, hy).</summary>
        static float RoundedRectDistance(float x, float y, float cx, float cy, float hx, float hy, float radius)
        {
            float qx = Mathf.Abs(x - cx) - (hx - radius), qy = Mathf.Abs(y - cy) - (hy - radius);
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        static Color Over(Color under, Color over)
        {
            float a = over.a + under.a * (1f - over.a);
            if (a <= 1e-5f) return Color.clear;
            Color c = (over * over.a + under * under.a * (1f - over.a)) / a;
            c.a = a;
            return c;
        }

        /// <summary>
        /// Panel dokusu: dikey degrade dolgu (kenar boşluklarında sabit, ortada doğrusal; 9-dilimde ortası esner),
        /// içeriden ince açık kenar çizgisi ve altta biraz daha uzun yumuşak gölge. Gölge, stilin overflow'u ile
        /// kutunun dışına taşar; düzeni etkilemez.
        /// </summary>
        Texture2D BoxTexture(Color top, Color bottom, int radiusUnits, Color outline, int shadowUnits, out RectOffset border, out RectOffset overflow)
        {
            int radius = radiusUnits > 0 ? Px(radiusUnits) : 0;
            int shadow = shadowUnits > 0 ? Px(shadowUnits) : 0;
            int drop = shadow / 3;
            int padLeft = shadow, padRight = shadow, padTop = Mathf.Max(0, shadow - drop), padBottom = shadow + drop;
            const int middle = 16;
            int bodyWidth = radius * 2 + middle, bodyHeight = radius * 2 + middle;
            int width = padLeft + bodyWidth + padRight, height = padTop + bodyHeight + padBottom;
            float stroke = Mathf.Max(1f, u);

            var texture = NewTexture(width, height);
            var pixels = new Color[width * height];
            float cx = padLeft + bodyWidth / 2f, cyTop = padTop + bodyHeight / 2f; // yukarıdan ölçülen merkez
            float hx = bodyWidth / 2f, hy = bodyHeight / 2f;

            for (int row = 0; row < height; row++)
            {
                float y = row + 0.5f; // yukarıdan
                float t = Mathf.Clamp01((y - padTop - radius) / middle);
                Color fill = Color.Lerp(top, bottom, t);
                for (int x = 0; x < width; x++)
                {
                    float px = x + 0.5f;
                    Color color = Color.clear;
                    if (shadow > 0)
                    {
                        float ds = RoundedRectDistance(px, y, cx, cyTop + drop, hx, hy, radius);
                        float fall = Mathf.Clamp01(1f - Mathf.Max(ds, 0f) / shadow);
                        color = new Color(0.01f, 0.02f, 0.06f, 0.42f * fall * fall);
                    }

                    float d = RoundedRectDistance(px, y, cx, cyTop, hx, hy, Mathf.Max(radius, 0.01f));
                    float coverage = Mathf.Clamp01(0.5f - d);
                    if (coverage > 0f)
                    {
                        Color body = fill;
                        if (outline.a > 0f)
                        {
                            float ring = Mathf.Clamp01(0.5f - Mathf.Abs(d + stroke / 2f) + stroke / 2f);
                            body = Over(body, new Color(outline.r, outline.g, outline.b, outline.a * ring));
                        }
                        body.a *= coverage;
                        color = Over(color, body);
                    }
                    pixels[(height - 1 - row) * width + x] = color; // dokuda y aşağıdan yukarı
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();

            border = new RectOffset(padLeft + radius, padRight + radius, padTop + radius, padBottom + radius);
            overflow = new RectOffset(padLeft, padRight, padTop, padBottom);
            return texture;
        }

        /// <summary>Şeker buton dokusu (bkz. <see cref="CandyButton"/>). Gölge yok; dudak kutunun içinde kalır.</summary>
        Texture2D CandyTexture(Color color, int lip, bool pressed, out RectOffset border)
        {
            int radius = Px(10);
            const int middle = 8;
            int width = radius * 2 + middle, height = radius * 2 + middle + lip;
            float stroke = Mathf.Max(1f, u);
            Color topColor = Color.Lerp(color, Color.white, 0.22f);
            Color lipColor = Color.Lerp(color, Color.black, 0.38f);

            var texture = NewTexture(width, height);
            var pixels = new Color[width * height];
            float hx = width / 2f, hy = (height - lip) / 2f;
            float bodyCenter = pressed ? lip + hy : hy;  // yukarıdan; basılıyken gövde dudak kadar aşağıda
            float lipCenter = hy + lip;

            for (int row = 0; row < height; row++)
            {
                float y = row + 0.5f;
                float t = Mathf.Clamp01((y - (pressed ? lip : 0) - radius * 0.5f) / (height - lip - radius));
                Color fill = Color.Lerp(topColor, color, t);
                for (int x = 0; x < width; x++)
                {
                    float px = x + 0.5f;
                    Color result = Color.clear;
                    if (!pressed)
                    {
                        float dl = RoundedRectDistance(px, y, hx, lipCenter, hx, hy, radius);
                        result = new Color(lipColor.r, lipColor.g, lipColor.b, lipColor.a * Mathf.Clamp01(0.5f - dl));
                    }
                    float d = RoundedRectDistance(px, y, hx, bodyCenter, hx, hy, radius);
                    float coverage = Mathf.Clamp01(0.5f - d);
                    if (coverage > 0f)
                    {
                        // Üst kenarda ince parlak çizgi: gövdenin üstünden "stroke" kadar içeride.
                        float bodyTop = bodyCenter - hy;
                        float shine = Mathf.Clamp01(1f - Mathf.Abs(y - bodyTop - stroke * 1.5f) / stroke) * 0.35f;
                        Color body = Color.Lerp(fill, Color.white, shine * Mathf.Clamp01(-d / stroke));
                        body.a *= coverage;
                        result = Over(result, body);
                    }
                    pixels[(height - 1 - row) * width + x] = result;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            border = new RectOffset(radius, radius, radius + lip, radius + lip);
            return texture;
        }

        /// <summary>Kenarları yumuşatılmış (anti-aliased) düz yuvarlak köşeli doku; 9-dilim (border) ile esner.</summary>
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
                    float d = RoundedRectDistance(x + 0.5f, y + 0.5f, size / 2f, size / 2f, size / 2f, size / 2f, radius);
                    pixels[y * size + x] = new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(0.5f - d));
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
            float radius = box * 0.3f, stroke = Mathf.Max(1.5f, box * 0.09f), half = box / 2f;
            float centerY = height / 2f;
            Color fill = on ? ActiveButton : new Color(1f, 1f, 1f, 0.08f);
            Color edge = on ? ActiveButton : new Color(Text.r, Text.g, Text.b, 0.7f);

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float px = x + 0.5f - half, py = y + 0.5f - centerY;
                float distance = RoundedRectDistance(px, py, 0f, 0f, half, half, radius);
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

        /// <summary>Kaydırıcı rayı: dikeyde ortada yuvarlak uçlu ince bant (yükseklik boyunca saydam kenar).</summary>
        Texture2D Track(Color color)
        {
            const int width = 12, height = 24;
            var texture = NewTexture(width, height);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float d = RoundedRectDistance(x + 0.5f, y + 0.5f, width / 2f, height / 2f, width / 2f, 3.5f, 3.5f);
                pixels[y * width + x] = new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(0.5f - d));
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>Daire; <paramref name="ring"/> &gt; 0 ise dışında yarıçapın bu oranı kadar kalın bir halka.</summary>
        Texture2D Disc(Color color, Color ringColor, float ring)
        {
            const int size = 48;
            var texture = NewTexture(size, size);
            var pixels = new Color[size * size];
            float r = size / 2f - 1f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - size / 2f) * (x + 0.5f - size / 2f) + (y + 0.5f - size / 2f) * (y + 0.5f - size / 2f));
                float inRing = ring > 0f ? Mathf.Clamp01(d - r * (1f - ring) + 0.5f) : 0f;
                Color c = Color.Lerp(color, ringColor, inRing);
                pixels[y * size + x] = new Color(c.r, c.g, c.b, Mathf.Clamp01(r - d + 0.5f));
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
