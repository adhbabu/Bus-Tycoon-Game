using BusTycoon.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace BusTycoon.Game
{
    /// <summary>
    /// Malzemeleri kodla üretir. Built-in pipeline'da Resources'taki şablon malzemeler kopyalanır
    /// (<see cref="DefaultContent.LitMaterialResourcePath"/>): Unity hiçbir asset'in kullanmadığı shader'ları
    /// derlemeye koymaz, şablonlar olmasa nesneler build'de pembe görünür. URP'de pipeline'ın varsayılan
    /// malzemesi kullanılır. Şablon yoksa (ör. editörde ilk Play) shader adıyla oluşturulur.
    /// </summary>
    public static class MaterialFactory
    {
        static Material litTemplate;
        static Material lineMaterial;

        static bool Srp => GraphicsSettings.currentRenderPipeline != null;

        public static Material CreateLit(Color color, float smoothness = 0.15f)
        {
            if (litTemplate == null) litTemplate = LoadLitTemplate();

            var material = new Material(litTemplate) { color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            return material;
        }

        static Material LoadLitTemplate()
        {
            if (!Srp)
            {
                var template = Resources.Load<Material>(DefaultContent.LitMaterialResourcePath);
                if (template != null) return template;
                Shader standard = Shader.Find("Standard");
                if (standard != null) return new Material(standard);
            }

            // URP (veya Standard bulunamazsa): aktif pipeline'ın varsayılan malzemesi.
            var probe = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Material fallback = probe.GetComponent<Renderer>().sharedMaterial;
            Object.Destroy(probe);
            return fallback;
        }

        /// <summary>Harita dokusu için ışıktan etkilenmeyen malzeme; bulunamazsa lit malzemeye düşer.</summary>
        public static Material CreateUnlitTexture(Texture texture)
        {
            Material material;
            Material template = Srp ? null : Resources.Load<Material>(DefaultContent.MapMaterialResourcePath);
            if (template != null)
            {
                material = new Material(template);
            }
            else
            {
                Shader shader = Srp ? Shader.Find("Universal Render Pipeline/Unlit") : Shader.Find("Unlit/Texture");
                material = shader != null ? new Material(shader) : CreateLit(Color.white, 0f);
            }

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
