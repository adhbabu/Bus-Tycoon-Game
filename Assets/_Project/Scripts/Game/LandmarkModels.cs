using System.Collections.Generic;
using BusTycoon.Urban;
using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// Ünlü mekân maketleri: her tür için kutu, silindir, kubbe ve halkadan yapılmış basit ama tanınır bir model
    /// (cami: kubbe ve dört minare; stadyum: oval tribün; saray: uzun yapı ve kuleler…). Bir şehrin bütün maketleri tek
    /// örgüde birleşir (tek çizim). Ölçü metre; haritada biraz büyütülür (<see cref="Exaggeration"/>) ki orta yakınlıkta da
    /// seçilsin. Renk griyle sınırlı: köşe rengi yalnız açık/koyu tonu taşır (gölgelendirici <c>BusTycoon/Maquette</c>).
    /// </summary>
    public static class LandmarkModels
    {
        /// <summary>Maketler gerçek boyutun bu katı (haritada okunur kalsın, şehri kaplamasın).</summary>
        public const float Exaggeration = 1.5f;

        /// <summary>Basit örgü kurucu (maketler ve şehir haritasındaki dolmuşlar).</summary>
        internal sealed class Builder
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Color> Colors = new List<Color>();
            public readonly List<int> Triangles = new List<int>();
            Vector3 origin;
            Quaternion rotation = Quaternion.identity;
            float scale = 1f;
            Color tone = Color.white;

            public void Place(Vector3 worldOrigin, float yawDegrees, float metersToWorld)
            {
                origin = worldOrigin;
                rotation = Quaternion.Euler(0f, yawDegrees, 0f);
                scale = metersToWorld;
            }

            public void Tone(float value) => tone = new Color(value, value, value, 1f);
            public void Paint(Color color) => tone = color;

            int Vertex(Vector3 local, Vector3 normal)
            {
                Vertices.Add(origin + rotation * (local * scale));
                Normals.Add(rotation * normal);
                Colors.Add(tone);
                return Vertices.Count - 1;
            }

            /// <summary>Üçgen; yönü istenen dış normale göre düzeltilir (arka yüz kırpılır, ters üçgen görünmez kalmasın).</summary>
            void Triangle(int a, int b, int c, Vector3 outward)
            {
                Vector3 pa = Vertices[a], pb = Vertices[b], pc = Vertices[c];
                if (Vector3.Dot(Vector3.Cross(pb - pa, pc - pa), rotation * outward) < 0f)
                {
                    int t = b;
                    b = c;
                    c = t;
                }
                Triangles.Add(a);
                Triangles.Add(b);
                Triangles.Add(c);
            }

            /// <summary>Düz yüz (dört köşe, tek normal).</summary>
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
            {
                int ia = Vertex(a, normal), ib = Vertex(b, normal), ic = Vertex(c, normal), id = Vertex(d, normal);
                Triangle(ia, ib, ic, normal);
                Triangle(ia, ic, id, normal);
            }

            /// <summary>Kutu (alt yüzü yok: zeminde durur). <paramref name="center"/> taban ortası.</summary>
            public void Box(Vector3 center, Vector3 size)
            {
                float x = size.x * 0.5f, z = size.z * 0.5f, y0 = center.y, y1 = center.y + size.y;
                Vector3 c = new Vector3(center.x, 0f, center.z);
                Vector3 p000 = c + new Vector3(-x, y0, -z), p100 = c + new Vector3(x, y0, -z), p010 = c + new Vector3(-x, y1, -z), p110 = c + new Vector3(x, y1, -z);
                Vector3 p001 = c + new Vector3(-x, y0, z), p101 = c + new Vector3(x, y0, z), p011 = c + new Vector3(-x, y1, z), p111 = c + new Vector3(x, y1, z);
                Quad(p010, p110, p111, p011, Vector3.up);
                Quad(p000, p100, p110, p010, Vector3.back);
                Quad(p001, p011, p111, p101, Vector3.forward);
                Quad(p000, p010, p011, p001, Vector3.left);
                Quad(p100, p101, p111, p110, Vector3.right);
            }

            /// <summary>Silindir ya da kesik koni (<paramref name="topRadius"/>); üstü kapalı, yan yüzler yumuşak.</summary>
            public void Cylinder(Vector3 baseCenter, float radius, float height, int segments, float topRadius = -1f, bool cap = true)
            {
                if (topRadius < 0f) topRadius = radius;
                float slope = (radius - topRadius) / Mathf.Max(0.001f, height);
                for (int i = 0; i < segments; i++)
                {
                    float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                    Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    Vector3 n0 = (d0 + Vector3.up * slope).normalized, n1 = (d1 + Vector3.up * slope).normalized;
                    int b0 = Vertex(baseCenter + d0 * radius, n0), b1 = Vertex(baseCenter + d1 * radius, n1);
                    int t0 = Vertex(baseCenter + d0 * topRadius + Vector3.up * height, n0);
                    int t1 = Vertex(baseCenter + d1 * topRadius + Vector3.up * height, n1);
                    Vector3 mid = ((d0 + d1) * 0.5f).normalized;
                    if (topRadius > 0.0001f)
                    {
                        Triangle(b0, b1, t1, mid);
                        Triangle(b0, t1, t0, mid);
                    }
                    else Triangle(b0, b1, t0, mid);
                }
                if (!cap || topRadius <= 0.0001f) return;
                Vector3 top = baseCenter + Vector3.up * height;
                int center = Vertex(top, Vector3.up);
                for (int i = 0; i < segments; i++)
                {
                    float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                    int v0 = Vertex(top + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * topRadius, Vector3.up);
                    int v1 = Vertex(top + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * topRadius, Vector3.up);
                    Triangle(center, v0, v1, Vector3.up);
                }
            }

            public void Cone(Vector3 baseCenter, float radius, float height, int segments) => Cylinder(baseCenter, radius, height, segments, 0f);

            /// <summary>Yarım küre kubbe (<paramref name="heightScale"/> ile basık ya da sivri).</summary>
            public void Dome(Vector3 baseCenter, float radius, int segments = 16, int rings = 5, float heightScale = 1f)
            {
                for (int r = 0; r < rings; r++)
                {
                    float e0 = r * Mathf.PI * 0.5f / rings, e1 = (r + 1) * Mathf.PI * 0.5f / rings;
                    for (int i = 0; i < segments; i++)
                    {
                        float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                        Vector3 p00 = Sphere(a0, e0), p10 = Sphere(a1, e0), p01 = Sphere(a0, e1), p11 = Sphere(a1, e1);
                        int i00 = Vertex(baseCenter + Scale(p00, radius, heightScale), Normal(p00, heightScale));
                        int i10 = Vertex(baseCenter + Scale(p10, radius, heightScale), Normal(p10, heightScale));
                        int i01 = Vertex(baseCenter + Scale(p01, radius, heightScale), Normal(p01, heightScale));
                        int i11 = Vertex(baseCenter + Scale(p11, radius, heightScale), Normal(p11, heightScale));
                        Vector3 outward = Sphere((a0 + a1) * 0.5f, (e0 + e1) * 0.5f);
                        Triangle(i00, i10, i11, outward);
                        if (r < rings - 1) Triangle(i00, i11, i01, outward);
                    }
                }
            }

            static Vector3 Sphere(float azimuth, float elevation) =>
                new Vector3(Mathf.Cos(azimuth) * Mathf.Cos(elevation), Mathf.Sin(elevation), Mathf.Sin(azimuth) * Mathf.Cos(elevation));

            static Vector3 Scale(Vector3 p, float radius, float heightScale) => new Vector3(p.x * radius, p.y * radius * heightScale, p.z * radius);
            static Vector3 Normal(Vector3 p, float heightScale) => new Vector3(p.x * heightScale, p.y, p.z * heightScale).normalized;

            /// <summary>Beşik çatı: x boyunca uzanan üçgen kesitli prizma. <paramref name="baseCenter"/> çatı tabanının ortası.</summary>
            public void Gable(Vector3 baseCenter, float length, float width, float height)
            {
                float x = length * 0.5f, z = width * 0.5f;
                Vector3 c = baseCenter;
                Vector3 a0 = c + new Vector3(-x, 0f, -z), a1 = c + new Vector3(x, 0f, -z);
                Vector3 b0 = c + new Vector3(-x, 0f, z), b1 = c + new Vector3(x, 0f, z);
                Vector3 r0 = c + new Vector3(-x, height, 0f), r1 = c + new Vector3(x, height, 0f);
                Quad(a0, a1, r1, r0, new Vector3(0f, z, -height).normalized);
                Quad(b0, r0, r1, b1, new Vector3(0f, z, height).normalized);
                int e0 = Vertex(a0, Vector3.left), e1 = Vertex(b0, Vector3.left), e2 = Vertex(r0, Vector3.left);
                Triangle(e0, e1, e2, Vector3.left);
                int f0 = Vertex(a1, Vector3.right), f1 = Vertex(b1, Vector3.right), f2 = Vertex(r1, Vector3.right);
                Triangle(f0, f1, f2, Vector3.right);
            }

            /// <summary>Oval tribün: dış duvar <paramref name="outerHeight"/>, içe doğru basamaklı eğim, iç duvar alçak.</summary>
            public void Bowl(Vector3 center, Vector2 outer, Vector2 inner, float outerHeight, float innerHeight, int segments)
            {
                for (int i = 0; i < segments; i++)
                {
                    float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                    Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    Vector3 o0 = center + new Vector3(d0.x * outer.x, 0f, d0.z * outer.y), o1 = center + new Vector3(d1.x * outer.x, 0f, d1.z * outer.y);
                    Vector3 i0 = center + new Vector3(d0.x * inner.x, 0f, d0.z * inner.y), i1 = center + new Vector3(d1.x * inner.x, 0f, d1.z * inner.y);
                    Vector3 mid = ((d0 + d1) * 0.5f).normalized;
                    Vector3 upO = Vector3.up * outerHeight, upI = Vector3.up * innerHeight;
                    Quad(o0, o1, o1 + upO, o0 + upO, mid);                         // dış duvar
                    Quad(i0, i0 + upI, i1 + upI, i1, -mid);                        // iç duvar (sahaya bakar)
                    Vector3 slope = (Vector3.up * 0.8f - mid * 0.6f).normalized;   // tribün eğimi (içe ve yukarı)
                    Quad(o0 + upO, o1 + upO, i1 + upI, i0 + upI, slope);
                }
            }
        }

        /// <summary>Bütün mekânların maketlerini tek örgüde kurar (dünya koordinatı; <paramref name="toWorld"/> metre → dünya).</summary>
        public static Mesh Build(IReadOnlyList<UrbanLandmark> landmarks, System.Func<Vector2, Vector3> toWorld, float unitsPerMeter)
        {
            var b = new Builder();
            foreach (UrbanLandmark landmark in landmarks)
            {
                float yaw = (Hash(landmark.Name) % 360 + 360) % 360;
                b.Place(toWorld(landmark.Position), yaw, unitsPerMeter * Exaggeration * Mathf.Lerp(0.85f, 1.1f, landmark.Fame));
                b.Tone(1f);
                AddModel(b, landmark.Kind);
            }
            var mesh = new Mesh { name = "LandmarkMaquettes" };
            if (b.Vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(b.Vertices);
            mesh.SetNormals(b.Normals);
            mesh.SetColors(b.Colors);
            mesh.SetTriangles(b.Triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static int Hash(string text)
        {
            unchecked
            {
                int h = 17;
                if (text != null) foreach (char c in text) h = h * 31 + c;
                return h;
            }
        }

        static void AddModel(Builder b, LandmarkKind kind)
        {
            switch (kind)
            {
                case LandmarkKind.Mosque: Mosque(b); break;
                case LandmarkKind.Stadium: Stadium(b); break;
                case LandmarkKind.Palace: Palace(b); break;
                case LandmarkKind.Museum: Museum(b); break;
                case LandmarkKind.Monument: Monument(b); break;
                case LandmarkKind.Bazaar: Bazaar(b); break;
                case LandmarkKind.University: University(b); break;
                case LandmarkKind.Park: Park(b); break;
                case LandmarkKind.Church: Church(b); break;
            }
        }

        /// <summary>Cami: kare gövde, kasnaklı büyük kubbe, dört yarım kubbe, dört şerefeli minare.</summary>
        static void Mosque(Builder b)
        {
            b.Box(Vector3.zero, new Vector3(56f, 14f, 56f));
            b.Cylinder(new Vector3(0f, 14f, 0f), 20f, 5f, 20);
            b.Tone(1.06f);
            b.Dome(new Vector3(0f, 19f, 0f), 20f, 20, 6, 0.9f);
            foreach (Vector3 side in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                b.Dome(side * 24f + Vector3.up * 14f, 10f, 12, 4, 0.8f);
            b.Tone(0.95f);
            foreach (Vector3 corner in new[] { new Vector3(34f, 0f, 34f), new Vector3(-34f, 0f, 34f), new Vector3(34f, 0f, -34f), new Vector3(-34f, 0f, -34f) })
            {
                b.Cylinder(corner, 2.4f, 52f, 10);
                b.Cylinder(corner + Vector3.up * 40f, 3.6f, 1.6f, 10);   // şerefe
                b.Cone(corner + Vector3.up * 52f, 2.6f, 10f, 10);
            }
        }

        /// <summary>Stadyum: oval tribün, açık saha, dört köşede ışık direği.</summary>
        static void Stadium(Builder b)
        {
            b.Bowl(Vector3.zero, new Vector2(78f, 60f), new Vector2(52f, 36f), 22f, 6f, 36);
            b.Tone(0.86f);
            b.Box(new Vector3(0f, 0.2f, 0f), new Vector3(96f, 0.5f, 66f));
            b.Tone(0.95f);
            foreach (Vector3 corner in new[] { new Vector3(66f, 0f, 50f), new Vector3(-66f, 0f, 50f), new Vector3(66f, 0f, -50f), new Vector3(-66f, 0f, -50f) })
            {
                b.Cylinder(corner, 1.2f, 40f, 6);
                b.Box(corner + Vector3.up * 40f, new Vector3(6f, 4f, 6f));
            }
        }

        /// <summary>Saray: uzun cephe, ortada yüksek giriş kütlesi ve küçük kubbe, iki uçta kule.</summary>
        static void Palace(Builder b)
        {
            b.Box(Vector3.zero, new Vector3(130f, 16f, 34f));
            b.Box(new Vector3(0f, 0f, 2f), new Vector3(34f, 24f, 40f));
            b.Tone(1.05f);
            b.Dome(new Vector3(0f, 24f, 2f), 9f, 14, 4, 0.9f);
            b.Tone(0.96f);
            b.Box(new Vector3(-58f, 0f, 0f), new Vector3(16f, 28f, 16f));
            b.Box(new Vector3(58f, 0f, 0f), new Vector3(16f, 28f, 16f));
            b.Gable(new Vector3(-58f, 28f, 0f), 16f, 16f, 6f);
            b.Gable(new Vector3(58f, 28f, 0f), 16f, 16f, 6f);
            b.Gable(new Vector3(-34f, 16f, 0f), 46f, 34f, 6f);
            b.Gable(new Vector3(34f, 16f, 0f), 46f, 34f, 6f);
        }

        /// <summary>Müze: sütunlu revak, düz çatı, alınlık.</summary>
        static void Museum(Builder b)
        {
            b.Box(new Vector3(0f, 0f, 6f), new Vector3(64f, 15f, 40f));
            b.Box(new Vector3(0f, 0f, -16f), new Vector3(52f, 1.5f, 10f)); // basamak
            for (int i = 0; i < 6; i++) b.Cylinder(new Vector3(-22f + i * 8.8f, 1.5f, -18f), 1.4f, 13.5f, 8);
            b.Box(new Vector3(0f, 15f, 2f), new Vector3(56f, 2f, 48f));
            b.Tone(1.05f);
            b.Gable(new Vector3(0f, 17f, 2f), 56f, 48f, 7f);
        }

        /// <summary>Anıt ve kule: kaide, üstte silindir kule ve sivri külah (Galata, saat kulesi gibi).</summary>
        static void Monument(Builder b)
        {
            b.Box(Vector3.zero, new Vector3(26f, 4f, 26f));
            b.Box(new Vector3(0f, 4f, 0f), new Vector3(18f, 4f, 18f));
            b.Cylinder(new Vector3(0f, 8f, 0f), 7.5f, 40f, 14, 6.8f);
            b.Cylinder(new Vector3(0f, 44f, 0f), 8.6f, 2f, 14);
            b.Tone(1.06f);
            b.Cone(new Vector3(0f, 48f, 0f), 7.6f, 16f, 14);
        }

        /// <summary>Kapalı çarşı: alçak uzun yapı, üstünde sıra sıra küçük kubbe.</summary>
        static void Bazaar(Builder b)
        {
            b.Box(Vector3.zero, new Vector3(92f, 9f, 48f));
            b.Tone(1.05f);
            for (int x = 0; x < 6; x++)
                for (int z = 0; z < 3; z++)
                    b.Dome(new Vector3(-38f + x * 15.2f, 9f, -14f + z * 14f), 5.6f, 10, 3, 0.9f);
        }

        /// <summary>Üniversite: U biçimli bina, ortada saat kulesi.</summary>
        static void University(Builder b)
        {
            b.Box(new Vector3(0f, 0f, 18f), new Vector3(84f, 18f, 18f));
            b.Box(new Vector3(-36f, 0f, -10f), new Vector3(14f, 14f, 42f));
            b.Box(new Vector3(36f, 0f, -10f), new Vector3(14f, 14f, 42f));
            b.Gable(new Vector3(0f, 18f, 18f), 84f, 18f, 5f);
            b.Tone(1.05f);
            b.Box(new Vector3(0f, 0f, 18f), new Vector3(9f, 34f, 9f));
            b.Cone(new Vector3(0f, 34f, 18f), 6.4f, 8f, 4);
        }

        /// <summary>Park: ağaç kümesi (gövde ve koni taç) ve küçük köşk.</summary>
        static void Park(Builder b)
        {
            var spots = new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(22f, 0f, 8f), new Vector3(-20f, 0f, 12f), new Vector3(8f, 0f, -22f),
                new Vector3(-14f, 0f, -18f), new Vector3(30f, 0f, -14f), new Vector3(-32f, 0f, -4f), new Vector3(12f, 0f, 26f)
            };
            for (int i = 0; i < spots.Length; i++)
            {
                if (i == 0)
                {
                    b.Box(spots[i], new Vector3(8f, 4f, 8f)); // köşk
                    b.Cone(spots[i] + Vector3.up * 4f, 6.5f, 4f, 8);
                    continue;
                }
                float size = 0.8f + (i % 3) * 0.15f;
                b.Tone(0.95f);
                b.Cylinder(spots[i], 1.2f * size, 5f * size, 6);
                b.Tone(1.04f);
                b.Cone(spots[i] + Vector3.up * 4f * size, 7f * size, 16f * size, 10);
            }
        }

        /// <summary>Kilise: beşik çatılı nef, çan kulesi ve sivri külah.</summary>
        static void Church(Builder b)
        {
            b.Box(new Vector3(6f, 0f, 0f), new Vector3(46f, 16f, 22f));
            b.Gable(new Vector3(6f, 16f, 0f), 46f, 22f, 9f);
            b.Tone(1.05f);
            b.Box(new Vector3(-22f, 0f, 0f), new Vector3(11f, 34f, 11f));
            b.Cone(new Vector3(-22f, 34f, 0f), 7.8f, 16f, 4);
        }

        /// <summary>
        /// Dolmuş örgüsü (birim boy, +z ileri, taban y=0): renkli gövde, koyu cam bandı, açık çatı, dört teker.
        /// <paramref name="bodyVertexCount"/>: baştaki bu kadar köşe gövde (hat rengine boyanır).
        /// </summary>
        internal static void Minibus(List<Vector3> vertices, List<Vector3> normals, List<Color> colors, List<int> triangles, out int bodyVertexCount)
        {
            var b = new Builder();
            b.Place(Vector3.zero, 0f, 1f);
            b.Paint(Color.white);
            b.Box(new Vector3(0f, 0.07f, 0f), new Vector3(0.42f, 0.2f, 1f));          // gövde (hat rengi)
            b.Box(new Vector3(0f, 0.07f, 0.47f), new Vector3(0.4f, 0.12f, 0.08f));    // ön tampon çıkıntısı
            bodyVertexCount = b.Vertices.Count;
            b.Paint(new Color(0.13f, 0.17f, 0.2f, 1f));
            b.Box(new Vector3(0f, 0.27f, -0.02f), new Vector3(0.4f, 0.11f, 0.9f));    // camlar
            b.Paint(new Color(0.93f, 0.95f, 0.95f, 1f));
            b.Box(new Vector3(0f, 0.38f, -0.03f), new Vector3(0.42f, 0.04f, 0.88f));  // çatı
            b.Paint(new Color(0.06f, 0.07f, 0.08f, 1f));
            foreach (float z in new[] { -0.32f, 0.32f })
                foreach (float x in new[] { -0.2f, 0.2f })
                    b.Box(new Vector3(x, 0f, z), new Vector3(0.06f, 0.12f, 0.16f));    // teker
            vertices.AddRange(b.Vertices);
            normals.AddRange(b.Normals);
            colors.AddRange(b.Colors);
            triangles.AddRange(b.Triangles);
        }
    }
}
