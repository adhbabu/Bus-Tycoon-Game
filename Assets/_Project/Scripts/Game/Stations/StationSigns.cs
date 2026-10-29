using BusTycoon.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace BusTycoon.Game
{
    /// <summary>
    /// Durak tabelaları: Inter ExtraBold ile 3B yazı (TextMesh) ve şehir ikonu levhaları. Yazılar derinlik testli
    /// shader'la çizilir (BusTycoon/SignText); shader yoksa yazı tipinin kendi malzemesine düşer.
    /// </summary>
    public static class StationSigns
    {
        const int FontSize = 64;
        /// <summary>TextMesh'te bir yazı tipi pikseli, characterSize × bu kadar dünya birimidir.</summary>
        const float UnitsPerPixel = 0.1f;

        static Font font;
        static Material textMaterial, iconMaterial;
        static bool listening;

        static Font Font
        {
            get
            {
                if (font == null) font = Resources.Load<Font>(DefaultContent.DisplayFontResourcePath);
                return font;
            }
        }

        static Material TextMaterial
        {
            get
            {
                if (textMaterial != null || Font == null) return textMaterial;
                Shader shader = Resources.Load<Shader>(DefaultContent.SignTextShaderResourcePath);
                if (shader == null) shader = Shader.Find("BusTycoon/SignText");
                textMaterial = shader != null ? new Material(shader) : new Material(Font.material);
                textMaterial.name = "SignText";
                textMaterial.mainTexture = Font.material.mainTexture;
                textMaterial.renderQueue = 3010;
                if (!listening)
                {
                    listening = true;
                    Font.textureRebuilt += OnFontTextureRebuilt; // dinamik yazı tipi dokusu büyüyünce yenisine geç
                }
                return textMaterial;
            }
        }

        static void OnFontTextureRebuilt(Font rebuilt)
        {
            if (rebuilt == font && textMaterial != null) textMaterial.mainTexture = rebuilt.material.mainTexture;
        }

        /// <summary>
        /// Ortalanmış yazı: <paramref name="position"/> yerel konum, <paramref name="yaw"/> yönü (0 = yazı −z'den okunur),
        /// <paramref name="height"/> satır yüksekliği; <paramref name="maxWidth"/>'e sığmazsa küçültülür.
        /// </summary>
        public static TextMesh Text(Transform parent, string text, Vector3 position, float yaw, float height, float maxWidth, Color color)
        {
            Material material = TextMaterial;
            if (material == null) return null;

            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var mesh = go.AddComponent<TextMesh>();
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            mesh.font = Font;
            mesh.fontSize = FontSize;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.richText = false;
            mesh.color = color;
            SetText(mesh, text, height, maxWidth);
            return mesh;
        }

        public static void SetText(TextMesh mesh, string text, float height, float maxWidth)
        {
            if (mesh == null) return;
            mesh.text = text;
            float size = height / (FontSize * UnitsPerPixel);
            float width = MeasurePixels(text) * size * UnitsPerPixel;
            if (maxWidth > 0f && width > maxWidth) size *= maxWidth / width;
            mesh.characterSize = size;
        }

        static float MeasurePixels(string text)
        {
            if (Font == null || string.IsNullOrEmpty(text)) return 0f;
            Font.RequestCharactersInTexture(text, FontSize, FontStyle.Normal);
            float total = 0f;
            foreach (char c in text)
                if (Font.GetCharacterInfo(c, out CharacterInfo info, FontSize, FontStyle.Normal)) total += info.advance;
            return total;
        }

        /// <summary>Şehrin ikonunu taşıyan kare levha (ön yüzü −z, yerel <paramref name="yaw"/> ile döner).</summary>
        public static Renderer CityIcon(Transform parent, CityData city, Vector3 position, float yaw, float size)
        {
            if (!UICityIcons.TryGetUv(city, out Texture2D texture, out Rect uv)) return null;
            if (iconMaterial == null)
                iconMaterial = new Material(MaterialFactory.LineMaterial) { name = "CityIconSign", mainTexture = texture, renderQueue = 3008 };

            float h = size / 2f;
            var mesh = new Mesh { name = "CityIcon" };
            mesh.vertices = new[] { new Vector3(-h, -h, 0f), new Vector3(-h, h, 0f), new Vector3(h, h, 0f), new Vector3(h, -h, 0f) };
            mesh.uv = new[] { new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMin, uv.yMax), new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMax, uv.yMin) };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("CityIcon");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = iconMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }
    }
}
