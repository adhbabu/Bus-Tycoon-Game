using System.Collections.Generic;
using BusTycoon.Data;
using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// IMGUI görsel dili ("yönetim paneli"): kömür grisi paneller (#1e1e24) ve ince açık kenar çizgisi, yumuşak
    /// gölge; turuncu ana buton (#ff9100, siyah yazı) ve yarı saydam ikincil butonlar (basınca hafifçe küçülür);
    /// parlak yeşil kasa (#00e676); renkli zeminli rozetler ve hap biçimli bildirimler; Inter yazı tipi.
    /// Tüm ölçüler <c>unit</c> ile çarpılarak GERÇEK piksel olarak üretilir (GUI.matrix ölçeklemesi yazıları
    /// bulanıklaştırdığı için kullanılmaz). Ekran yoğunluğu değişince stiller yeniden kurulur.
    /// Dokular kodla üretilir; GUI.skin'e yalnızca OnGUI içinden erişilebildiği için ilk OnGUI'de kurulur.
    /// </summary>
    public sealed class UIStyles
    {
        public static readonly Color Text = Color.white;
        public static readonly Color DimText = new Color(0.63f, 0.63f, 0.63f);           // #a0a0a0
        public static readonly Color Green = new Color(0f, 0.9f, 0.46f);                 // #00e676
        public static readonly Color Yellow = new Color(1f, 0.84f, 0.25f);               // #ffd740
        public static readonly Color Orange = new Color(1f, 0.57f, 0f);                  // #ff9100
        public static readonly Color Red = new Color(1f, 0.32f, 0.32f);                  // #ff5252
        public static readonly Color Blue = new Color(0.25f, 0.77f, 1f);                 // #40c4ff
        public static readonly Color Gold = new Color(1f, 0.77f, 0f);                    // #ffc400

        public static readonly Color PanelColor = new Color(0.118f, 0.118f, 0.141f);    // #1e1e24
        static readonly Color AccentHover = new Color(1f, 0.67f, 0.2f);                 // #ffaa33
        static readonly Color Border = new Color(1f, 1f, 1f, 0.08f);
        static readonly Color ToastDefault = new Color(0.2f, 0.2f, 0.2f);                // #333
        static readonly Color ToastSuccess = new Color(0f, 0.78f, 0.33f);                // #00c853
        static readonly Color ToastError = new Color(0.84f, 0f, 0f);                     // #d50000

        static Font regularFont, heavyFont, displayFont;
        static bool fontsLoaded;

        readonly float u;
        readonly List<Texture2D> textures = new List<Texture2D>();
        readonly Font regular, heavy, display;

        public readonly GUIStyle Panel, TopBar, SideBar, BottomBar, Card, Modal, Tooltip, Bar, Dot;
        public readonly GUIStyle Toast, ToastSuccessBox, ToastErrorBox, ToastText, ToastDarkText;
        public readonly GUIStyle Title, H1, H2, Label, Small, Dim, Bold, Big, Center, Wrap, WrapDim, Right;
        public readonly GUIStyle StatLabel, Money, MoneyGlow, MoneyNegative, MoneyNegativeGlow, Value, CardTitle, Detail, CountBadge;
        public readonly GUIStyle BadgeSuccess, BadgeWarning, BadgeDanger, BadgeInfo;
        public readonly GUIStyle Button, Primary, Danger, Ghost, Tab, TabActive, SpeedButton, SpeedButtonActive, RaceBadge, NavItem, NavItemActive, IconOnly;
        public readonly GUIStyle Field, Toggle, Slider, SliderThumb, VerticalScrollbar;
        public readonly GUIStyle MapLabel, MapLabelBold, MapLabelShadow, MapLabelBoldShadow;

        public float Unit => u;

        public UIStyles(float unit)
        {
            u = unit;
            LoadFonts();
            regular = regularFont != null ? regularFont : GUI.skin.font;
            heavy = heavyFont != null ? heavyFont : regular;
            display = displayFont != null ? displayFont : heavy;

            var panel = new Color(PanelColor.r, PanelColor.g, PanelColor.b, 0.97f);
            var shadow = new Color(0f, 0f, 0f, 0.35f);
            Panel = Box(panel, 16, 16, 14, Border, 12, shadow);
            TopBar = Box(panel, 0, 0, 0, Border, 10, new Color(0f, 0f, 0f, 0.5f));
            SideBar = Box(panel, 0, 0, 0, Border, 10, new Color(0f, 0f, 0f, 0.45f));
            BottomBar = Box(panel, 0, 0, 0, Border, 10, new Color(0f, 0f, 0f, 0.45f));
            Card = Box(new Color(1f, 1f, 1f, 0.04f), 14, 14, 12, Border, 0, Color.clear);
            Modal = Box(panel, 20, 28, 24, Border, 20, new Color(0f, 0f, 0f, 0.5f));
            Tooltip = Box(new Color(0.16f, 0.16f, 0.19f, 0.97f), 10, 10, 6, Border, 6, shadow);

            // Hap biçimli bildirimler: koyu (bilgi), yeşil (başarı, siyah yazı), kırmızı (hata).
            var toastShadow = new Color(0f, 0f, 0f, 0.5f);
            Toast = Box(ToastDefault, 20, 0, 0, Color.clear, 10, toastShadow);
            ToastSuccessBox = Box(ToastSuccess, 20, 0, 0, Color.clear, 10, toastShadow);
            ToastErrorBox = Box(ToastError, 20, 0, 0, Color.clear, 10, toastShadow);

            // İlerleme çubuğu ve renkli nokta: beyaz doku, GUI.backgroundColor ile boyanır.
            Bar = new GUIStyle { border = new RectOffset(Px(3), Px(3), Px(3), Px(3)) };
            Bar.normal.background = Rounded(Color.white, 3);
            Dot = new GUIStyle();
            Dot.normal.background = Disc(Color.white, Color.white, 0f);

            Label = MakeLabel(15, Weight.Regular, Text);
            Small = MakeLabel(13, Weight.Regular, Text);
            Dim = MakeLabel(14, Weight.Regular, DimText);
            Bold = MakeLabel(15, Weight.Bold, Text);
            Big = MakeLabel(28, Weight.Display, Text);
            H2 = MakeLabel(17, Weight.Bold, Text);
            H1 = MakeLabel(22, Weight.Bold, Text);
            Title = MakeLabel(42, Weight.Display, Orange);
            Title.alignment = TextAnchor.MiddleCenter;
            Center = MakeLabel(15, Weight.Regular, DimText);
            Center.alignment = TextAnchor.MiddleCenter;
            Right = MakeLabel(15, Weight.Bold, Text);
            Right.alignment = TextAnchor.MiddleRight;
            Wrap = MakeLabel(14, Weight.Regular, Text);
            Wrap.wordWrap = true;
            WrapDim = MakeLabel(13, Weight.Regular, DimText);
            WrapDim.wordWrap = true;
            ToastText = MakeLabel(14, Weight.Bold, Text);
            ToastDarkText = MakeLabel(14, Weight.Bold, Color.black);

            // Gösterge paneli: küçük soluk BÜYÜK HARF etiket üstte, değer altta; kasa parlak yeşil.
            StatLabel = MakeLabel(11, Weight.Regular, DimText);
            Money = MakeLabel(26, Weight.Display, Green);
            MoneyGlow = MakeLabel(26, Weight.Display, new Color(Green.r, Green.g, Green.b, 0.07f));
            MoneyNegative = MakeLabel(26, Weight.Display, Red);
            MoneyNegativeGlow = MakeLabel(26, Weight.Display, new Color(Red.r, Red.g, Red.b, 0.07f));
            Value = MakeLabel(18, Weight.Bold, Text);
            CardTitle = MakeLabel(17, Weight.Bold, Text);
            Detail = MakeLabel(14, Weight.Regular, DimText);
            CountBadge = MakeLabel(10, Weight.Bold, Text);
            CountBadge.alignment = TextAnchor.MiddleCenter;
            CountBadge.margin = CountBadge.padding = new RectOffset();

            BadgeSuccess = Badge(Green);
            BadgeWarning = Badge(Orange);
            BadgeDanger = Badge(Red);
            BadgeInfo = Badge(Blue);

            Button = MakeButton(new Color(1f, 1f, 1f, 0.08f), new Color(1f, 1f, 1f, 0.13f), Text, 15, true);
            Primary = MakeButton(Orange, AccentHover, Color.black, 16, true, new Color(Orange.r, Orange.g, Orange.b, 0.28f));
            Danger = MakeButton(new Color(Red.r, Red.g, Red.b, 0.16f), new Color(Red.r, Red.g, Red.b, 0.26f), Red, 15, true);
            Ghost = MakeButton(new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 0.06f), Text, 14, false);
            Ghost.alignment = TextAnchor.MiddleLeft;
            Tab = MakeButton(new Color(1f, 1f, 1f, 0.05f), new Color(1f, 1f, 1f, 0.1f), DimText, 14, false);
            TabActive = MakeButton(new Color(Orange.r, Orange.g, Orange.b, 0.16f), new Color(Orange.r, Orange.g, Orange.b, 0.24f), Orange, 14, true);
            SpeedButton = MakeButton(new Color(1f, 1f, 1f, 0.06f), new Color(1f, 1f, 1f, 0.12f), DimText, 14, true);
            SpeedButton.fixedWidth = Px(44);
            SpeedButtonActive = MakeButton(Orange, AccentHover, Color.black, 14, true);
            SpeedButtonActive.fixedWidth = Px(44);
            // Yarışta hız düğmelerinin yerine: aynı görünüm, yazı kadar geniş ("Blitz · 2x")
            RaceBadge = new GUIStyle(SpeedButtonActive) { fixedWidth = 0f };

            // Yalnız ikonlu düğme: yazı saydam (düğmeyi adlandırır, ikon kodla çizilir).
            IconOnly = MakeButton(new Color(1f, 1f, 1f, 0.08f), new Color(1f, 1f, 1f, 0.13f), Color.clear, 1, false);

            // Menü öğesi: ikon üstte (kod çizer), yazı altta; seçili öğe turuncu.
            NavItem = MakeButton(new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 0.05f), DimText, 12, false);
            NavItem.alignment = TextAnchor.LowerCenter;
            NavItem.padding = Offset(2, 2, 2, 7);
            NavItem.hover.textColor = Text;
            NavItemActive = MakeButton(new Color(Orange.r, Orange.g, Orange.b, 0.1f), new Color(Orange.r, Orange.g, Orange.b, 0.16f), Orange, 12, true);
            NavItemActive.alignment = TextAnchor.LowerCenter;
            NavItemActive.padding = NavItem.padding;

            Field = new GUIStyle(GUI.skin.textField) { font = regular, fontSize = Px(17), padding = Offset(14, 14, 10, 10) };
            Field.normal.textColor = Field.focused.textColor = Field.hover.textColor = Text;
            Field.normal.background = Field.hover.background = BoxTexture(new Color(1f, 1f, 1f, 0.06f), 12, new Color(1f, 1f, 1f, 0.12f), 0, Color.clear, 0, 0, out RectOffset fieldBorder, out _);
            Field.focused.background = BoxTexture(new Color(1f, 1f, 1f, 0.09f), 12, Orange, 0, Color.clear, 0, 0, out _, out _);
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

            // Dokunmatik ekranda tutulabilir kalın kaydırıcı: yuvarlak uçlu ray + beyaz halkalı turuncu tutamaç.
            Slider = new GUIStyle(GUI.skin.horizontalSlider) { fixedHeight = Px(26), border = Offset(6, 6, 0, 0), margin = Offset(4, 4, 6, 6) };
            Slider.normal.background = Track(new Color(1f, 1f, 1f, 0.15f));
            SliderThumb = new GUIStyle(GUI.skin.horizontalSliderThumb) { fixedWidth = Px(26), fixedHeight = Px(26), border = new RectOffset(0, 0, 0, 0) };
            SliderThumb.normal.background = SliderThumb.hover.background = Disc(Orange, Color.white, 0.2f);
            SliderThumb.active.background = Disc(AccentHover, Color.white, 0.2f);

            // İnce, koyu temaya uyan kaydırma çubuğu. IMGUI tutamacı ada göre ("verticalscrollbarthumb") deriden bulur.
            VerticalScrollbar = new GUIStyle(GUI.skin.verticalScrollbar) { fixedWidth = Px(6), border = new RectOffset(Px(3), Px(3), Px(3), Px(3)), margin = Offset(6, 0, 2, 2), padding = new RectOffset() };
            VerticalScrollbar.normal.background = Rounded(new Color(1f, 1f, 1f, 0.04f), 3);
            GUIStyle thumb = GUI.skin.verticalScrollbarThumb;
            thumb.fixedWidth = Px(6);
            thumb.border = new RectOffset(Px(3), Px(3), Px(3), Px(3));
            thumb.normal.background = thumb.hover.background = thumb.active.background = Rounded(new Color(1f, 1f, 1f, 0.24f), 3);

            MapLabel = MakeLabel(13, Weight.Regular, Text);
            MapLabel.alignment = TextAnchor.MiddleCenter;
            MapLabelBold = MakeLabel(15, Weight.Bold, Text);
            MapLabelBold.alignment = TextAnchor.MiddleCenter;
            MapLabelShadow = new GUIStyle(MapLabel);
            MapLabelShadow.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            MapLabelBoldShadow = new GUIStyle(MapLabelBold);
            MapLabelBoldShadow.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
        }

        /// <summary>Stiller yeniden kurulurken eski dokuları serbest bırakır.</summary>
        public void Dispose()
        {
            foreach (var texture in textures)
                if (texture != null) Object.Destroy(texture);
            textures.Clear();
        }

        /// <summary>Bildirim kutusu rengi: yeşil = başarı, kırmızı = hata, diğerleri koyu.</summary>
        public GUIStyle ToastBox(Color color) => color.Equals(Green) ? ToastSuccessBox : color.Equals(Red) ? ToastErrorBox : Toast;

        static void LoadFonts()
        {
            if (fontsLoaded) return;
            fontsLoaded = true;
            regularFont = Resources.Load<Font>(DefaultContent.RegularFontResourcePath);
            heavyFont = Resources.Load<Font>(DefaultContent.HeavyFontResourcePath);
            displayFont = Resources.Load<Font>(DefaultContent.DisplayFontResourcePath);
            if (regularFont == null || heavyFont == null || displayFont == null)
                Debug.LogWarning("[UI] Inter yazı tipi bulunamadı; Unity'nin varsayılan yazı tipi kullanılıyor.");
        }

        int Px(float value) => Mathf.Max(1, Mathf.RoundToInt(value * u));

        RectOffset Offset(int left, int right, int top, int bottom) =>
            new RectOffset(Mathf.RoundToInt(left * u), Mathf.RoundToInt(right * u), Mathf.RoundToInt(top * u), Mathf.RoundToInt(bottom * u));

        enum Weight { Regular, Bold, Display }

        /// <summary>Kalın yazılar ayrı yazı tipi dosyalarıdır (Bold, ExtraBold); yapay kalınlaştırma yalnız yedek yazı tipinde.</summary>
        GUIStyle MakeLabel(int size, Weight weight, Color color)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = Px(size), wordWrap = false, richText = false };
            ApplyFont(style, weight);
            style.normal.textColor = color;
            style.margin = Offset(2, 2, 2, 2);
            style.padding = Offset(2, 2, 1, 1);
            return style;
        }

        void ApplyFont(GUIStyle style, Weight weight)
        {
            style.font = weight == Weight.Display ? display : weight == Weight.Bold ? heavy : regular;
            style.fontStyle = weight != Weight.Regular && heavyFont == null ? FontStyle.Bold : FontStyle.Normal;
        }

        GUIStyle Box(Color fill, int radius, int padX, int padY, Color outline, int shadow, Color shadowColor)
        {
            var style = new GUIStyle();
            style.normal.background = BoxTexture(fill, radius, outline, shadow, shadowColor, 0, 0, out RectOffset border, out RectOffset overflow);
            style.border = border;
            style.overflow = overflow;
            style.padding = Offset(padX, padX, padY, padY);
            style.margin = Offset(0, 0, 3, 3);
            return style;
        }

        /// <summary>Rozet: renkli yazı, aynı rengin saydam zemini, hap biçimi.</summary>
        GUIStyle Badge(Color color)
        {
            var style = MakeLabel(12, Weight.Bold, color);
            style.normal.background = BoxTexture(new Color(color.r, color.g, color.b, 0.15f), 11, Color.clear, 0, Color.clear, 0, 0, out RectOffset border, out _);
            style.border = border;
            style.alignment = TextAnchor.MiddleCenter;
            style.padding = Offset(10, 10, 4, 4);
            return style;
        }

        /// <summary>
        /// Yuvarlak köşeli buton (12 birim). Basılıyken gövde her kenardan biraz içeri çekilir: dokunmatikte
        /// "küçülerek basılma" hissi. İsteğe bağlı renkli parıltı (ana buton) gölge gibi dışa taşar.
        /// </summary>
        GUIStyle MakeButton(Color fill, Color hover, Color textColor, int fontSize, bool bold, Color? glow = null)
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = Px(fontSize),
                alignment = TextAnchor.MiddleCenter,
                padding = Offset(14, 14, 7, 7),
                margin = Offset(3, 3, 3, 3),
                wordWrap = false
            };
            ApplyFont(style, bold ? Weight.Bold : Weight.Regular);

            int press = Px(2);
            int glowSize = glow.HasValue ? 8 : 0;
            Color glowColor = glow ?? Color.clear;
            Color active = new Color(Mathf.Lerp(fill.r, hover.r, 0.5f), Mathf.Lerp(fill.g, hover.g, 0.5f), Mathf.Lerp(fill.b, hover.b, 0.5f), Mathf.Max(fill.a, hover.a));
            style.normal.background = BoxTexture(fill, 12, Color.clear, glowSize, glowColor, 0, press, out RectOffset border, out RectOffset overflow);
            style.hover.background = BoxTexture(hover, 12, Color.clear, glowSize, glowColor, 0, press, out _, out _);
            style.active.background = BoxTexture(active, 12, Color.clear, glowSize, glowColor, press, press, out _, out _);
            style.focused.background = style.normal.background;
            style.onNormal.background = style.active.background;
            style.border = border;
            style.overflow = overflow;
            style.normal.textColor = style.hover.textColor = style.active.textColor = style.focused.textColor = style.onNormal.textColor = textColor;
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
        /// Kutu dokusu: düz dolgu, içeriden ince kenar çizgisi ve altta biraz daha uzun yumuşak gölge. Gölge,
        /// stilin overflow'u ile kutunun dışına taşar; düzeni etkilemez. <paramref name="inset"/> gövdeyi her
        /// kenardan içeri çeker (basılı buton); <paramref name="reserve"/> aynı stilin tüm hâlleri için 9-dilim
        /// kenarının içeri çekilen payı da kapsamasını sağlar.
        /// </summary>
        Texture2D BoxTexture(Color fill, int radiusUnits, Color outline, int shadowUnits, Color shadowColor, int inset, int reserve,
            out RectOffset border, out RectOffset overflow)
        {
            int radius = radiusUnits > 0 ? Px(radiusUnits) : 0;
            int shadow = shadowUnits > 0 ? Px(shadowUnits) : 0;
            int drop = shadow / 3;
            int padLeft = shadow, padRight = shadow, padTop = Mathf.Max(0, shadow - drop), padBottom = shadow + drop;
            const int middle = 8;
            int bodyWidth = (radius + reserve) * 2 + middle, bodyHeight = (radius + reserve) * 2 + middle;
            int width = padLeft + bodyWidth + padRight, height = padTop + bodyHeight + padBottom;
            float stroke = Mathf.Max(1f, u);

            var texture = NewTexture(width, height);
            var pixels = new Color[width * height];
            float cx = padLeft + bodyWidth / 2f, cyTop = padTop + bodyHeight / 2f; // yukarıdan ölçülen merkez
            float hx = bodyWidth / 2f, hy = bodyHeight / 2f;
            float r = Mathf.Max(radius, 0.01f);

            for (int row = 0; row < height; row++)
            {
                float y = row + 0.5f; // yukarıdan
                for (int x = 0; x < width; x++)
                {
                    float px = x + 0.5f;
                    Color color = Color.clear;
                    if (shadow > 0)
                    {
                        float ds = RoundedRectDistance(px, y, cx, cyTop + drop, hx, hy, r);
                        float fall = Mathf.Clamp01(1f - Mathf.Max(ds, 0f) / shadow);
                        color = new Color(shadowColor.r, shadowColor.g, shadowColor.b, shadowColor.a * fall * fall);
                    }

                    float d = RoundedRectDistance(px, y, cx, cyTop, hx - inset, hy - inset, Mathf.Max(r - inset * 0.5f, 0.01f));
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

            border = new RectOffset(padLeft + radius + reserve, padRight + radius + reserve, padTop + radius + reserve, padBottom + radius + reserve);
            overflow = new RectOffset(padLeft, padRight, padTop, padBottom);
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

        /// <summary>Onay kutusu dokusu: dikeyde ortalanmış yuvarlak kare (seçiliyse turuncu + siyah tik), sağda saydam şerit.</summary>
        Texture2D Checkbox(int box, int height, bool on)
        {
            int width = box + 2;
            var texture = NewTexture(width, height);
            var pixels = new Color[width * height];
            float radius = box * 0.28f, stroke = Mathf.Max(1.5f, box * 0.09f), half = box / 2f;
            float centerY = height / 2f;
            Color fill = on ? Orange : new Color(1f, 1f, 1f, 0.06f);
            Color edge = on ? Orange : new Color(1f, 1f, 1f, 0.35f);

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
                    color = Color.Lerp(color, Color.black, mark * shape);
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
