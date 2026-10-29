using System.Collections.Generic;
using BusTycoon.Data;
using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// Haritadaki 3B otobüs modelleri: segmente göre (kaputlu minibüs, midibüs, şehirlerarası, VIP, çift katlı) kodla
    /// üretilen tek parça modeller — gövde, pencere bantları, ön cam, kapı, tavan kliması, şerit, farlar, stop
    /// lambaları ve jantlı tekerlekler. Renkler 8×8'lik bir palet dokusundan UV ile okunur: tek malzeme (Standard),
    /// otobüs başına tek çizim. Ön taraf +z (gidiş yönü), yukarı +y, sağ +x (kapı sağda).
    /// Gövde renkleri galeri çizimleriyle (Tools/make_bus_art.py) aynıdır.
    /// </summary>
    public static class BusModels
    {
        /// <summary>Segment renkleri (BusSegment sırası): minibüs, midibüs, şehirlerarası, konfor, lüks VIP, çift katlı.</summary>
        public static readonly Color[] SegmentColors =
        {
            new Color(0.95f, 0.95f, 0.93f), new Color(0.3f, 0.8f, 0.8f), new Color(0.3f, 0.55f, 1f),
            new Color(0.7f, 0.45f, 1f), new Color(1f, 0.78f, 0.25f), new Color(0.95f, 0.35f, 0.3f)
        };

        static readonly Color[] StripeColors =
        {
            new Color(1f, 0.57f, 0f), Color.white, Color.white, new Color(1f, 0.77f, 0f), new Color(0.2f, 0.2f, 0.25f), Color.white
        };

        // Palet yuvaları
        const int Glass = 0, DarkGlass = 1, Roof = 2, RoofUnit = 3, Tire = 4, Hub = 5, Headlight = 6, Taillight = 7, Skirt = 8;
        const int BodyBase = 9, StripeBase = 15, PaletteSize = 8;

        sealed class Shape
        {
            public float Length, Height, Width = 0.26f, Wheel = 0.055f;
            public float[] Axles;
            public bool Van, DoubleDeck, RoofUnit, DoubleStripe, Tinted;
        }

        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        static Material material;
        static Texture2D palette;

        public static Material Material
        {
            get
            {
                if (material == null)
                {
                    material = MaterialFactory.CreateLit(Color.white, 0.45f);
                    material.mainTexture = Palette();
                }
                return material;
            }
        }

        public static Mesh MeshFor(BusModelData model)
        {
            string key = model != null ? model.Id : string.Empty;
            if (meshes.TryGetValue(key, out Mesh mesh) && mesh != null) return mesh;
            int segment = model != null ? Mathf.Clamp(model.Tier, 0, SegmentColors.Length - 1) : 2;
            mesh = Build(ShapeFor(key, segment), segment);
            mesh.name = "Bus_" + (model != null ? model.Id : "default");
            meshes[key] = mesh;
            return mesh;
        }

        static Shape ShapeFor(string id, int segment)
        {
            switch (id)
            {
                case "KARTAL_M16": return new Shape { Van = true, Length = 0.4f, Height = 0.19f, Width = 0.22f, Wheel = 0.045f, Axles = new[] { -0.12f, 0.12f } };
                case "KARTAL_M19": return new Shape { Van = true, RoofUnit = true, Length = 0.43f, Height = 0.2f, Width = 0.22f, Wheel = 0.045f, Axles = new[] { -0.13f, 0.13f } };
                case "ANKA_27": return new Shape { RoofUnit = true, Length = 0.5f, Height = 0.2f, Width = 0.24f, Wheel = 0.05f, Axles = new[] { -0.16f, 0.16f } };
                case "ANKA_31": return new Shape { RoofUnit = true, Length = 0.55f, Height = 0.2f, Width = 0.24f, Wheel = 0.05f, Axles = new[] { -0.18f, 0.18f } };
                case "TOROS_46": return new Shape { RoofUnit = true, Length = 0.62f, Height = 0.21f, Axles = new[] { -0.2f, 0.2f } };
                case "TOROS_42C": return new Shape { RoofUnit = true, DoubleStripe = true, Length = 0.62f, Height = 0.21f, Axles = new[] { -0.2f, 0.2f } };
                case "ZIRVE_VIP": return new Shape { RoofUnit = true, Tinted = true, Length = 0.64f, Height = 0.2f, Axles = new[] { -0.25f, -0.16f, 0.22f } };
                case "ZIRVE_DD": return new Shape { DoubleDeck = true, Length = 0.64f, Height = 0.31f, Axles = new[] { -0.25f, -0.16f, 0.22f } };
            }
            // Bilinmeyen model: segmentin tipik biçimi
            return segment switch
            {
                0 => ShapeFor("KARTAL_M16", 0),
                1 => ShapeFor("ANKA_27", 1),
                5 => ShapeFor("ZIRVE_DD", 5),
                _ => ShapeFor("TOROS_46", 2)
            };
        }

        static Mesh Build(Shape shape, int segment)
        {
            var b = new Builder();
            float L = shape.Length, H = shape.Height, W = shape.Width, r = shape.Wheel;
            float g = r * 0.8f;                       // gövdenin alt kenarı
            float front = L / 2f, rear = -L / 2f;
            int body = BodyBase + segment, stripe = StripeBase + segment;
            int glass = shape.Tinted ? DarkGlass : Glass;

            // Gövde (minibüste kaputlu burun) ve alt etek
            float cabinFront = shape.Van ? front - L * 0.17f : front;
            b.Box(new Vector3(0f, g + H / 2f, (rear + cabinFront) / 2f), new Vector3(W, H, cabinFront - rear), body);
            if (shape.Van) b.Box(new Vector3(0f, g + H * 0.27f, (cabinFront + front) / 2f), new Vector3(W, H * 0.54f, front - cabinFront), body);
            b.Box(new Vector3(0f, g + H * 0.08f, 0f), new Vector3(W + 0.004f, H * 0.16f, L * 0.98f), Skirt);

            // Şerit(ler)
            float stripeY = shape.DoubleDeck ? g + H * 0.58f : g + H * 0.47f;
            b.Box(new Vector3(0f, stripeY, 0f), new Vector3(W + 0.008f, H * 0.07f, L * 0.97f), stripe);
            if (shape.DoubleStripe) b.Box(new Vector3(0f, stripeY - H * 0.12f, 0f), new Vector3(W + 0.008f, H * 0.035f, L * 0.97f), stripe);

            // Yan pencereler (çift katlıda iki sıra), ön ve arka cam
            float windowFront = shape.Van ? cabinFront - L * 0.02f : front - L * 0.1f;
            float windowRear = rear + L * 0.04f;
            float[] rows = shape.DoubleDeck ? new[] { g + H * 0.4f, g + H * 0.8f } : new[] { g + H * 0.73f };
            float rowHeight = shape.DoubleDeck ? H * 0.2f : H * 0.34f;
            foreach (float y in rows)
                b.Box(new Vector3(0f, y, (windowFront + windowRear) / 2f), new Vector3(W + 0.006f, rowHeight, windowFront - windowRear), glass);

            if (shape.Van)
                b.Box(new Vector3(0f, g + H * 0.76f, cabinFront + 0.004f), new Vector3(W * 0.9f, H * 0.4f, 0.01f), Glass);
            else
                foreach (float y in rows)
                    b.Box(new Vector3(0f, y - (shape.DoubleDeck ? 0f : H * 0.04f), front + 0.004f), new Vector3(W * 0.88f, rowHeight * (shape.DoubleDeck ? 1f : 1.25f), 0.01f), Glass);
            b.Box(new Vector3(0f, g + H * 0.74f, rear - 0.004f), new Vector3(W * 0.78f, H * 0.24f, 0.01f), DarkGlass);

            // Kapı (sağ yan, önde)
            float doorZ = shape.Van ? cabinFront - L * 0.2f : front - L * 0.06f;
            b.Box(new Vector3(W / 2f + 0.004f, g + H * 0.45f, doorZ), new Vector3(0.006f, H * 0.66f, L * 0.08f), DarkGlass);

            // Tavan ve tavan kliması
            b.Box(new Vector3(0f, g + H + 0.008f, (rear + cabinFront) / 2f), new Vector3(W * 0.94f, 0.016f, (cabinFront - rear) * 0.97f), Roof);
            if (shape.RoofUnit)
                b.Box(new Vector3(0f, g + H + 0.027f, -L * 0.05f), new Vector3(W * 0.55f, 0.022f, L * 0.24f), RoofUnit);

            // Farlar ve stop lambaları
            float lightY = shape.Van ? g + H * 0.22f : g + H * 0.2f;
            foreach (float side in new[] { -1f, 1f })
            {
                b.Box(new Vector3(side * W * 0.3f, lightY, front + 0.005f), new Vector3(W * 0.22f, H * 0.09f, 0.01f), Headlight);
                b.Box(new Vector3(side * W * 0.36f, g + H * 0.32f, rear - 0.005f), new Vector3(W * 0.14f, H * 0.2f, 0.01f), Taillight);
            }

            // Tekerlekler
            foreach (float z in shape.Axles)
                foreach (float side in new[] { -1f, 1f })
                    b.Wheel(new Vector3(side * (W / 2f - 0.01f), r, z), r, 0.05f, side);

            return b.Build();
        }

        static Texture2D Palette()
        {
            if (palette != null) return palette;
            var colors = new Color[PaletteSize * PaletteSize];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.magenta;
            colors[Glass] = new Color(0.13f, 0.18f, 0.27f);
            colors[DarkGlass] = new Color(0.07f, 0.09f, 0.14f);
            colors[Roof] = new Color(0.97f, 0.97f, 0.95f);
            colors[RoofUnit] = new Color(0.78f, 0.8f, 0.84f);
            colors[Tire] = new Color(0.1f, 0.1f, 0.11f);
            colors[Hub] = new Color(0.76f, 0.77f, 0.8f);
            colors[Headlight] = new Color(1f, 0.95f, 0.62f);
            colors[Taillight] = new Color(0.92f, 0.16f, 0.12f);
            colors[Skirt] = new Color(0.22f, 0.22f, 0.25f);
            for (int s = 0; s < SegmentColors.Length; s++)
            {
                colors[BodyBase + s] = SegmentColors[s];
                colors[StripeBase + s] = StripeColors[s];
            }

            palette = new Texture2D(PaletteSize, PaletteSize, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            palette.SetPixels(colors);
            palette.Apply();
            return palette;
        }

        static Vector2 PaletteUv(int slot) =>
            new Vector2((slot % PaletteSize + 0.5f) / PaletteSize, (slot / PaletteSize + 0.5f) / PaletteSize);

        /// <summary>Düz gölgelendirilmiş üçgenlerden mesh kurar (her üçgen kendi köşeleri ve yüz normali ile).</summary>
        sealed class Builder
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<int> triangles = new List<int>();

            public void Box(Vector3 center, Vector3 size, int paint)
            {
                Vector3 h = size * 0.5f;
                Vector3 x = new Vector3(h.x, 0f, 0f), y = new Vector3(0f, h.y, 0f), z = new Vector3(0f, 0f, h.z);
                Quad(center + x, Vector3.right, z, y, paint);
                Quad(center - x, Vector3.left, z, y, paint);
                Quad(center + y, Vector3.up, x, z, paint);
                Quad(center - y, Vector3.down, x, z, paint);
                Quad(center + z, Vector3.forward, x, y, paint);
                Quad(center - z, Vector3.back, x, y, paint);
            }

            /// <summary>X eksenli on köşeli tekerlek: lastik yüzeyi + dış yüzde jant.</summary>
            public void Wheel(Vector3 center, float radius, float width, float outward)
            {
                const int sides = 10;
                float half = width / 2f;
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides, mid = (a0 + a1) / 2f;
                    var p0 = new Vector3(0f, Mathf.Cos(a0) * radius, Mathf.Sin(a0) * radius);
                    var p1 = new Vector3(0f, Mathf.Cos(a1) * radius, Mathf.Sin(a1) * radius);
                    var normal = new Vector3(0f, Mathf.Cos(mid), Mathf.Sin(mid));
                    Vector3 l = new Vector3(-half, 0f, 0f), r = new Vector3(half, 0f, 0f);
                    Triangle(center + p0 + l, center + p0 + r, center + p1 + r, normal, Tire);
                    Triangle(center + p0 + l, center + p1 + r, center + p1 + l, normal, Tire);

                    // Dış kapak jant (gri), iç kapak lastik rengi
                    var capOut = new Vector3(outward * half, 0f, 0f);
                    Triangle(center + capOut, center + capOut + p0, center + capOut + p1, new Vector3(outward, 0f, 0f), Hub);
                    Triangle(center - capOut, center - capOut + p0, center - capOut + p1, new Vector3(-outward, 0f, 0f), Tire);
                }
            }

            void Quad(Vector3 center, Vector3 normal, Vector3 a, Vector3 b, int paint)
            {
                Triangle(center - a - b, center - a + b, center + a + b, normal, paint);
                Triangle(center - a - b, center + a + b, center + a - b, normal, paint);
            }

            /// <summary>Unity'de ön yüz: Cross(b − a, c − a) yüz normali yönünde (izleyiciye göre saat yönü).</summary>
            void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal, int paint)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f) (b, c) = (c, b);
                Vector2 uv = PaletteUv(paint);
                int start = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                for (int i = 0; i < 3; i++)
                {
                    normals.Add(normal);
                    uvs.Add(uv);
                    triangles.Add(start + i);
                }
            }

            public Mesh Build()
            {
                var mesh = new Mesh();
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
