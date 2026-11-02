using System.Collections.Generic;
using BusTycoon.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace BusTycoon.Game
{
    /// <summary>
    /// Haritadaki düz işaretler tek örgüde, tek çizim çağrısında: şehir rozetleri (kameraya dönük daire) ve otobüsler
    /// (yolda yatan kapsül). Şekil ve kenar çizgisi BusTycoon/MapSprite gölgelendiricisinde çizilir. Örgü yalnız
    /// içerik değişince yeniden yazılır (rozetler); otobüsler her kare (yüzlerce köşe, ucuz).
    /// Eski 3D durak maketleri ve otobüs modelleri her biri ayrı nesne ve çizim çağrısıydı.
    /// </summary>
    public sealed class MapSprites
    {
        readonly Mesh mesh;
        readonly Material material;
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector2> locals = new List<Vector2>();
        readonly List<Vector4> shapes = new List<Vector4>();
        readonly List<Vector4> outlines = new List<Vector4>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> triangles = new List<int>();

        public MeshRenderer Renderer { get; }
        public int Count { get; private set; }

        public MapSprites(string name, Transform parent, int renderQueueOffset)
        {
            mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            Shader shader = Resources.Load<Shader>(DefaultContent.MapSpriteShaderResourcePath);
            if (shader == null) shader = Shader.Find("BusTycoon/MapSprite");
            material = shader != null && shader.isSupported ? new Material(shader) : new Material(MaterialFactory.LineMaterial);
            material.name = name;
            material.renderQueue = 3010 + renderQueueOffset;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            Renderer = go.AddComponent<MeshRenderer>();
            Renderer.sharedMaterial = material;
            Renderer.shadowCastingMode = ShadowCastingMode.Off;
            Renderer.receiveShadows = false;
        }

        public void Clear()
        {
            vertices.Clear();
            locals.Clear();
            shapes.Clear();
            outlines.Clear();
            colors.Clear();
            triangles.Clear();
            Count = 0;
        }

        /// <summary>Kameraya dönük daire; <paramref name="ringFrom"/> 0..1: kenar çizgisinin başladığı oran.</summary>
        public void AddCircle(Vector3 center, float radius, Color fill, Color outline, float ringFrom)
        {
            int start = vertices.Count;
            var shape = new Vector4(radius, 0f, 1f, ringFrom);
            var edge = new Vector4(outline.r, outline.g, outline.b, outline.a);
            for (int i = 0; i < 4; i++)
            {
                vertices.Add(center);
                locals.Add(new Vector2((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f));
                shapes.Add(shape);
                outlines.Add(edge);
                colors.Add(fill);
            }
            AddQuad(start);
        }

        /// <summary>Yolda yatan kapsül (otobüs): uzunluk ve genişlik dünya biriminde, yön XZ düzleminde.</summary>
        public void AddCapsule(Vector3 center, Vector3 forward, float length, float width, Color fill, Color outline, float ringFrom)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-8f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 side = new Vector3(forward.z, 0f, -forward.x);
            Vector3 along = forward * (length * 0.5f), across = side * (width * 0.5f);
            int start = vertices.Count;
            var shape = new Vector4(0f, 1f, Mathf.Max(1f, length / Mathf.Max(width, 1e-4f)), ringFrom);
            var edge = new Vector4(outline.r, outline.g, outline.b, outline.a);
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? -1f : 1f, y = (i & 2) == 0 ? -1f : 1f;
                vertices.Add(center + along * x + across * y);
                locals.Add(new Vector2(x, y));
                shapes.Add(shape);
                outlines.Add(edge);
                colors.Add(fill);
            }
            AddQuad(start);
        }

        void AddQuad(int start)
        {
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
            triangles.Add(start);
            triangles.Add(start + 3);
            triangles.Add(start + 1);
            Count++;
        }

        /// <summary>Örgüyü yazar. Daireler gölgelendiricide büyütüldüğü için sınırlar biraz genişletilir.</summary>
        public void Apply(float boundsPadding)
        {
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, locals);
            mesh.SetUVs(1, shapes);
            mesh.SetUVs(2, outlines);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            Bounds bounds = mesh.bounds;
            bounds.Expand(boundsPadding);
            mesh.bounds = bounds;
            Renderer.enabled = Count > 0;
        }

        public Color Tint
        {
            set
            {
                if (material.HasProperty("_Tint")) material.SetColor("_Tint", value);
            }
        }
    }
}
