using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// IMGUI görsel dili: koyu yarı saydam paneller, yuvarlak köşeli butonlar, renk paleti.
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

        public readonly GUIStyle Panel, TopBar, Card, Modal, Toast, Tooltip;
        public readonly GUIStyle Title, H1, H2, Label, Small, Dim, Bold, Big, Center, Wrap, WrapDim, Right;
        public readonly GUIStyle Button, Primary, Danger, Ghost, Tab, TabActive, SpeedButton, SpeedButtonActive, Field;
        public readonly GUIStyle MapLabel, MapLabelBold, MapLabelShadow, MapLabelBoldShadow;

        public UIStyles()
        {
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
            SpeedButton.fixedWidth = 42;
            SpeedButtonActive = MakeButton(new Color(0.24f, 0.45f, 0.7f), 14);
            SpeedButtonActive.fixedWidth = 42;
            SpeedButtonActive.fontStyle = FontStyle.Bold;

            Field = new GUIStyle(GUI.skin.textField) { fontSize = 17, padding = new RectOffset(10, 10, 8, 8) };
            Field.normal.textColor = Field.focused.textColor = Field.hover.textColor = Text;
            Field.normal.background = Field.hover.background = Rounded(new Color(1f, 1f, 1f, 0.08f), 6);
            Field.focused.background = Rounded(new Color(1f, 1f, 1f, 0.14f), 6);
            Field.border = new RectOffset(7, 7, 7, 7);

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

        static GUIStyle MakeLabel(int size, FontStyle fontStyle, Color color)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fontStyle, wordWrap = false, richText = false };
            style.normal.textColor = color;
            style.margin = new RectOffset(2, 2, 2, 2);
            style.padding = new RectOffset(2, 2, 1, 1);
            return style;
        }

        static GUIStyle Box(Color color, int padX, int padY, int radius = 9)
        {
            var style = new GUIStyle();
            style.normal.background = radius > 0 ? Rounded(color, radius) : Solid(color);
            style.border = new RectOffset(radius + 1, radius + 1, radius + 1, radius + 1);
            style.padding = new RectOffset(padX, padX, padY, padY);
            style.margin = new RectOffset(0, 0, 3, 3);
            return style;
        }

        static GUIStyle MakeButton(Color color, int fontSize)
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(12, 12, 7, 7),
                margin = new RectOffset(3, 3, 3, 3),
                border = new RectOffset(8, 8, 8, 8),
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

        static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        /// <summary>Kenarları yumuşatılmış (anti-aliased) yuvarlak köşeli doku; 9-dilim (border) ile esner.</summary>
        static Texture2D Rounded(Color color, int radius)
        {
            int size = radius * 2 + 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
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
    }
}
