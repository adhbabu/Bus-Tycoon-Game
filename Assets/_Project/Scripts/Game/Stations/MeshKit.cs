using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BusTycoon.Game
{
    /// <summary>Yüzey katmanı: her katman ayrı alt mesh ve malzemedir (<see cref="StationMaterials"/>).</summary>
    public enum MeshLayer
    {
        /// <summary>Işıkla gölgelenen gövde (renkler <see cref="StationPalette"/> dokusundan).</summary>
        Lit,
        /// <summary>Pencereler: gündüz gökyüzü yansıması, gece sıcak ışık (ışıksız).</summary>
        Window,
        /// <summary>Kendinden ışıklı parçalar (lamba, ekran): köşe rengiyle, gece parlar.</summary>
        Glow,
        /// <summary>Yarı saydam cam bölmeler.</summary>
        Glass,
        /// <summary>Nesneye özel renkli malzeme (haritada çatı rengi şehrin durumunu gösterir).</summary>
        Accent
    }

    /// <summary>
    /// Düz gölgelendirilmiş, düşük poligonlu modeller için mesh kurucu: kutu, silindir/koni, küre, üçgen çatı, halka.
    /// Her üçgenin kendi köşeleri ve yüz normali vardır (keskin kenarlar). Dönüşüm yığını (Push/Pop) parçaları
    /// yerleştirir ve döndürür. Renk, ışıklı katmanda paletten UV olarak, ışıksız katmanlarda köşe rengi olarak okunur.
    /// </summary>
    public sealed class MeshKit
    {
        const int LayerCount = 5;

        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int>[] layers = new List<int>[LayerCount];
        readonly Stack<Matrix4x4> stack = new Stack<Matrix4x4>();
        Matrix4x4 matrix = Matrix4x4.identity;

        /// <summary>Sonraki parçaların katmanı.</summary>
        public MeshLayer Layer = MeshLayer.Lit;

        public MeshKit()
        {
            for (int i = 0; i < LayerCount; i++) layers[i] = new List<int>();
        }

        public int VertexCount => vertices.Count;
        public bool IsEmpty => vertices.Count == 0;

        // ------------------------------------------------------------------ dönüşüm

        /// <summary>Sonraki parçaları <paramref name="position"/>'a taşır ve y ekseninde döndürür (derece).</summary>
        public void Push(Vector3 position, float yawDegrees = 0f, float scale = 1f) =>
            Push(Matrix4x4.TRS(position, Quaternion.Euler(0f, yawDegrees, 0f), Vector3.one * scale));

        public void Push(Vector3 position, Quaternion rotation) => Push(Matrix4x4.TRS(position, rotation, Vector3.one));

        public void Push(Matrix4x4 local)
        {
            stack.Push(matrix);
            matrix = matrix * local;
        }

        public void Pop() => matrix = stack.Count > 0 ? stack.Pop() : Matrix4x4.identity;

        // ------------------------------------------------------------------ ilkel şekiller

        public void Box(Vector3 center, Vector3 size, Color color)
        {
            Vector3 h = size * 0.5f;
            Vector3 x = new Vector3(h.x, 0f, 0f), y = new Vector3(0f, h.y, 0f), z = new Vector3(0f, 0f, h.z);
            Face(center + x, Vector3.right, z, y, color);
            Face(center - x, Vector3.left, z, y, color);
            Face(center + y, Vector3.up, x, z, color);
            Face(center - y, Vector3.down, x, z, color);
            Face(center + z, Vector3.forward, x, y, color);
            Face(center - z, Vector3.back, x, y, color);
        }

        public void Box(Vector3 center, Vector3 size, Color color, MeshLayer layer)
        {
            MeshLayer previous = Layer;
            Layer = layer;
            Box(center, size, color);
            Layer = previous;
        }

        /// <summary>Tabanı alt yüzde olan kutu (yere oturan nesneler için kolaylık).</summary>
        public void Block(float x, float z, float width, float depth, float height, Color color, float y = 0f) =>
            Box(new Vector3(x, y + height / 2f, z), new Vector3(width, height, depth), color);

        /// <summary>Tek yüzlü dikdörtgen (yarı kenar vektörleri a ve b); ön yüz <paramref name="normal"/> yönünde.</summary>
        public void Face(Vector3 center, Vector3 normal, Vector3 a, Vector3 b, Color color)
        {
            Triangle(center - a - b, center - a + b, center + a + b, normal, color);
            Triangle(center - a - b, center + a + b, center + a - b, normal, color);
        }

        /// <summary>Düşey eksenli prizma/kesik koni (alt yarıçap → üst yarıçap). Üst yarıçap 0 ise koni.</summary>
        public void Frustum(Vector3 baseCenter, float bottomRadius, float topRadius, float height, Color color, int sides = 8, bool top = true, bool bottom = false, float angleOffset = 0f)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = angleOffset + i * Mathf.PI * 2f / sides, a1 = angleOffset + (i + 1) * Mathf.PI * 2f / sides;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Vector3 b0 = baseCenter + d0 * bottomRadius, b1 = baseCenter + d1 * bottomRadius;
                Vector3 t0 = baseCenter + d0 * topRadius + Vector3.up * height, t1 = baseCenter + d1 * topRadius + Vector3.up * height;
                float slope = (bottomRadius - topRadius) / Mathf.Max(0.0001f, height);
                Vector3 n = (d0 + d1).normalized + Vector3.up * slope;
                n.Normalize();
                if (topRadius > 0.0001f)
                {
                    Triangle(b0, t0, t1, n, color);
                    Triangle(b0, t1, b1, n, color);
                    if (top) Triangle(baseCenter + Vector3.up * height, t0, t1, Vector3.up, color);
                }
                else
                {
                    Triangle(b0, t0, b1, n, color); // koni: tepe tek nokta
                }
                if (bottom) Triangle(baseCenter, b0, b1, Vector3.down, color);
            }
        }

        public void Cylinder(Vector3 baseCenter, float radius, float height, Color color, int sides = 8, bool top = true, bool bottom = false) =>
            Frustum(baseCenter, radius, radius, height, color, sides, top, bottom);

        /// <summary>X eksenli silindir (tekerlek, boru): merkez, yarıçap, genişlik.</summary>
        public void CylinderX(Vector3 center, float radius, float width, Color color, int sides = 8)
        {
            float half = width / 2f;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides, mid = (a0 + a1) / 2f;
                var p0 = new Vector3(0f, Mathf.Cos(a0) * radius, Mathf.Sin(a0) * radius);
                var p1 = new Vector3(0f, Mathf.Cos(a1) * radius, Mathf.Sin(a1) * radius);
                var n = new Vector3(0f, Mathf.Cos(mid), Mathf.Sin(mid));
                Vector3 l = new Vector3(-half, 0f, 0f), r = new Vector3(half, 0f, 0f);
                Triangle(center + p0 + l, center + p0 + r, center + p1 + r, n, color);
                Triangle(center + p0 + l, center + p1 + r, center + p1 + l, n, color);
                Triangle(center + r, center + r + p0, center + r + p1, Vector3.right, color);
                Triangle(center + l, center + l + p0, center + l + p1, Vector3.left, color);
            }
        }

        /// <summary>Düşük poligonlu küre (enlem/boylam halkaları).</summary>
        public void Sphere(Vector3 center, float radius, Color color, int segments = 8, int rings = 5) =>
            Ellipsoid(center, new Vector3(radius, radius, radius), color, segments, rings);

        public void Ellipsoid(Vector3 center, Vector3 radii, Color color, int segments = 8, int rings = 5)
        {
            for (int r = 0; r < rings; r++)
            {
                float v0 = Mathf.PI * r / rings - Mathf.PI / 2f, v1 = Mathf.PI * (r + 1) / rings - Mathf.PI / 2f;
                for (int s = 0; s < segments; s++)
                {
                    float u0 = Mathf.PI * 2f * s / segments, u1 = Mathf.PI * 2f * (s + 1) / segments;
                    Vector3 p00 = Point(u0, v0), p10 = Point(u1, v0), p01 = Point(u0, v1), p11 = Point(u1, v1);
                    Vector3 n = Point((u0 + u1) / 2f, (v0 + v1) / 2f);
                    Vector3 normal = new Vector3(n.x / radii.x, n.y / radii.y, n.z / radii.z).normalized;
                    if (r > 0) Triangle(center + p00, center + p10, center + p11, normal, color);
                    if (r < rings - 1) Triangle(center + p00, center + p11, center + p01, normal, color);
                }
            }

            Vector3 Point(float u, float v) => new Vector3(Mathf.Cos(v) * Mathf.Cos(u) * radii.x, Mathf.Sin(v) * radii.y, Mathf.Cos(v) * Mathf.Sin(u) * radii.z);
        }

        /// <summary>Üçgen kesitli çatı: sırt X ekseni boyunca, taban <paramref name="size"/> (x uzunluk, y yükseklik, z derinlik).</summary>
        public void Gable(Vector3 baseCenter, Vector3 size, Color color)
        {
            float hx = size.x / 2f, hz = size.z / 2f;
            Vector3 ridgeL = baseCenter + new Vector3(-hx, size.y, 0f), ridgeR = baseCenter + new Vector3(hx, size.y, 0f);
            Vector3 fl = baseCenter + new Vector3(-hx, 0f, -hz), fr = baseCenter + new Vector3(hx, 0f, -hz);
            Vector3 bl = baseCenter + new Vector3(-hx, 0f, hz), br = baseCenter + new Vector3(hx, 0f, hz);
            Vector3 front = new Vector3(0f, hz, -size.y).normalized, back = new Vector3(0f, hz, size.y).normalized;
            Triangle(fl, ridgeL, ridgeR, front, color);
            Triangle(fl, ridgeR, fr, front, color);
            Triangle(bl, ridgeR, ridgeL, back, color);
            Triangle(bl, br, ridgeR, back, color);
            Triangle(fl, bl, ridgeL, Vector3.left, color);
            Triangle(fr, ridgeR, br, Vector3.right, color);
        }

        /// <summary>Yatay halka (simit, saksı kenarı): kutulardan oluşan çember.</summary>
        public void Ring(Vector3 center, float radius, float thickness, Color color, int segments = 8)
        {
            float chord = 2f * radius * Mathf.Sin(Mathf.PI / segments) + thickness * 0.4f;
            for (int i = 0; i < segments; i++)
            {
                float angle = (i + 0.5f) * 360f / segments;
                Push(center, Quaternion.Euler(0f, -angle, 0f));
                Box(new Vector3(radius, 0f, 0f), new Vector3(thickness, thickness, chord), color);
                Pop();
            }
        }

        /// <summary>Ön yüzü <paramref name="normal"/> yönünde olan üçgen (sarım gerekirse çevrilir).</summary>
        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal, Color color)
        {
            a = matrix.MultiplyPoint3x4(a);
            b = matrix.MultiplyPoint3x4(b);
            c = matrix.MultiplyPoint3x4(c);
            normal = matrix.MultiplyVector(normal).normalized;
            // Unity'de ön yüz: Cross(b − a, c − a) yüz normali yönünde (izleyiciye göre saat yönü).
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f) (b, c) = (c, b);

            Vector2 uv = StationPalette.Uv(color);
            List<int> indices = layers[(int)Layer];
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            for (int i = 0; i < 3; i++)
            {
                normals.Add(normal);
                uvs.Add(uv);
                colors.Add(color);
                indices.Add(start + i);
            }
        }

        // ------------------------------------------------------------------ sonuç

        /// <summary>Mesh'i kurar; dolu katmanlar sırayla alt mesh olur (<paramref name="usedLayers"/> malzeme sırasıdır).</summary>
        public Mesh Build(string name, out MeshLayer[] usedLayers)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);

            var used = new List<MeshLayer>();
            for (int i = 0; i < LayerCount; i++)
                if (layers[i].Count > 0) used.Add((MeshLayer)i);
            mesh.subMeshCount = Mathf.Max(1, used.Count);
            for (int i = 0; i < used.Count; i++) mesh.SetTriangles(layers[(int)used[i]], i);
            mesh.RecalculateBounds();
            StationPalette.Upload();

            usedLayers = used.ToArray();
            return mesh;
        }

        /// <summary>Mesh'i yeni bir nesneye koyar (malzemeler katmanlara göre; Accent için <paramref name="accent"/>).</summary>
        public MeshRenderer BuildObject(string name, Transform parent, Material accent = null, bool castShadows = true)
        {
            Mesh mesh = Build(name, out MeshLayer[] used);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = StationMaterials.For(used, accent);
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            return renderer;
        }
    }
}
