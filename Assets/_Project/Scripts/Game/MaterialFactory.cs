using UnityEngine;
using UnityEngine.Rendering;

namespace BusTycoon.Game
{
    /// <summary>
    /// Malzemeleri asset gerektirmeden üretir. Lit malzeme, aktif render pipeline'ın varsayılan malzemesinden
    /// kopyalanır; böylece hem Built-in hem URP projelerinde (ve build'lerde) pembe "eksik shader" görülmez.
    /// </summary>
    public static class MaterialFactory
    {
        static Material litTemplate;
        static Material lineMaterial;

        public static Material CreateLit(Color color, float smoothness = 0.15f)
        {
            if (litTemplate == null)
            {
                var probe = GameObject.CreatePrimitive(PrimitiveType.Quad);
                litTemplate = probe.GetComponent<Renderer>().sharedMaterial;
                Object.Destroy(probe);
            }

            var material = new Material(litTemplate) { color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            return material;
        }

        /// <summary>Harita dokusu için ışıktan etkilenmeyen malzeme; bulunamazsa lit malzemeye düşer.</summary>
        public static Material CreateUnlitTexture(Texture texture)
        {
            bool srp = GraphicsSettings.currentRenderPipeline != null;
            Shader shader = srp ? Shader.Find("Universal Render Pipeline/Unlit") : Shader.Find("Unlit/Texture");

            Material material = shader != null ? new Material(shader) : CreateLit(Color.white, 0f);
            material.mainTexture = texture;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            return material;
        }

        /// <summary>LineRenderer için köşe rengini (startColor/endColor) kullanan paylaşılan malzeme.</summary>
        public static Material LineMaterial
        {
            get
            {
                if (lineMaterial != null) return lineMaterial;
                Shader shader = Shader.Find("Sprites/Default");
                lineMaterial = shader != null ? new Material(shader) : CreateLit(Color.white, 0f);
                return lineMaterial;
            }
        }
    }
}
