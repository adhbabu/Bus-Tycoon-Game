using BusTycoon.Stations;
using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// Haritadaki şehir işaretleri: şehrin büyüklüğüne göre küçük bir otobüs durağı, otogar, büyük otogar ya da
    /// metropol terminali (kodla üretilen düşük poligonlu modeller, harita birimiyle). Çatı "Accent" katmanındadır:
    /// rengi şehrin durumunu gösterir (WorldRenderer.RefreshColors). Pencereler gece yanar. Ön yüz −z (kameraya bakar).
    /// </summary>
    public static class StationModels
    {
        static readonly Color Slab = new Color(0.86f, 0.86f, 0.84f);
        static readonly Color Asphalt = new Color(0.33f, 0.34f, 0.37f);
        static readonly Color Curb = new Color(0.93f, 0.93f, 0.91f);
        static readonly Color Wall = new Color(0.97f, 0.94f, 0.87f);
        static readonly Color WallSide = new Color(0.9f, 0.87f, 0.8f);
        static readonly Color Trim = new Color(0.42f, 0.45f, 0.5f);
        static readonly Color Door = new Color(0.2f, 0.25f, 0.32f);
        static readonly Color Post = new Color(0.6f, 0.62f, 0.66f);
        static readonly Color White = new Color(0.98f, 0.98f, 0.97f);
        static readonly Color BusGlass = new Color(0.16f, 0.24f, 0.36f);
        static readonly Color Tire = new Color(0.1f, 0.1f, 0.11f);
        static readonly Color Leaf = new Color(0.33f, 0.63f, 0.3f);
        static readonly Color Trunk = new Color(0.45f, 0.32f, 0.2f);
        public static readonly Color CompanyOrange = new Color(1f, 0.57f, 0f);

        // Otobüs gövde renkleri: beyaz şehirlerarası, turuncu şirket otobüsü, mavi
        static readonly Color[] BusColors = { White, CompanyOrange, new Color(0.3f, 0.55f, 1f), White, new Color(0.3f, 0.8f, 0.8f) };

        static readonly Mesh[] meshes = new Mesh[4];
        static readonly MeshLayer[][] layers = new MeshLayer[4][];
        static Mesh flagMesh;
        static MeshLayer[] flagLayers;

        /// <summary>İşaretin tepe yüksekliği (ölçeklenmeden önce): şehir adı bunun üstüne yazılır.</summary>
        public static float Height(StationTier tier) => tier switch
        {
            StationTier.Stop => 0.17f,
            StationTier.Terminal => 0.19f,
            StationTier.Large => 0.26f,
            _ => 0.44f
        };

        /// <summary>Merkez bayrağının yeri (çatının üstü).</summary>
        public static Vector3 FlagPosition(StationTier tier) => tier switch
        {
            StationTier.Stop => new Vector3(0.1f, 0.14f, 0.06f),
            StationTier.Terminal => new Vector3(0.0f, 0.15f, 0.14f),
            StationTier.Large => new Vector3(0.02f, 0.2f, 0.2f),
            _ => new Vector3(0.08f, 0.26f, 0.26f)
        };

        public static Mesh MeshFor(StationTier tier, out MeshLayer[] usedLayers)
        {
            int index = (int)tier;
            if (meshes[index] == null)
            {
                var kit = new MeshKit();
                switch (tier)
                {
                    case StationTier.Stop: BuildStop(kit); break;
                    case StationTier.Terminal: BuildTerminal(kit); break;
                    case StationTier.Large: BuildLarge(kit); break;
                    default: BuildMetro(kit); break;
                }
                meshes[index] = kit.Build("Station_" + tier, out layers[index]);
            }
            usedLayers = layers[index];
            return meshes[index];
        }

        /// <summary>Merkez ofis bayrağı (turuncu şirket bayrağı).</summary>
        public static Mesh FlagMesh(out MeshLayer[] usedLayers)
        {
            if (flagMesh == null)
            {
                var kit = new MeshKit();
                kit.Box(new Vector3(0f, 0.065f, 0f), new Vector3(0.007f, 0.13f, 0.007f), Post);
                kit.Box(new Vector3(0.034f, 0.11f, 0f), new Vector3(0.06f, 0.036f, 0.005f), CompanyOrange);
                kit.Box(new Vector3(0.034f, 0.11f, -0.003f), new Vector3(0.018f, 0.018f, 0.002f), White);
                flagMesh = kit.Build("HeadquartersFlag", out flagLayers);
            }
            usedLayers = flagLayers;
            return flagMesh;
        }

        // ------------------------------------------------------------------ modeller

        /// <summary>Küçük durak: sundurma (cam arka panel, bank), durak tabelası ve yol kenarında bir otobüs.</summary>
        static void BuildStop(MeshKit k)
        {
            k.Box(new Vector3(0f, 0.012f, -0.02f), new Vector3(0.46f, 0.024f, 0.32f), Slab);
            k.Box(new Vector3(0f, 0.0255f, -0.12f), new Vector3(0.46f, 0.003f, 0.1f), Asphalt);
            k.Box(new Vector3(0f, 0.027f, -0.07f), new Vector3(0.46f, 0.006f, 0.012f), Curb);

            // Sundurma
            foreach (float x in new[] { -0.11f, 0.11f })
                k.Box(new Vector3(x, 0.075f, 0.065f), new Vector3(0.012f, 0.1f, 0.012f), Post);
            k.Box(new Vector3(0f, 0.07f, 0.07f), new Vector3(0.21f, 0.085f, 0.006f), White, MeshLayer.Window);
            k.Box(new Vector3(0f, 0.045f, 0.045f), new Vector3(0.16f, 0.01f, 0.035f), Trim);
            k.Layer = MeshLayer.Accent;
            k.Box(new Vector3(0f, 0.132f, 0.035f), new Vector3(0.27f, 0.018f, 0.11f), White);
            k.Layer = MeshLayer.Lit;

            // Durak tabelası
            k.Box(new Vector3(-0.18f, 0.085f, -0.035f), new Vector3(0.009f, 0.12f, 0.009f), Post);
            k.Cylinder(new Vector3(-0.18f, 0.14f, -0.035f), 0.028f, 0.008f, CompanyOrange, 10, true, true);
            k.Box(new Vector3(-0.18f, 0.164f, -0.035f), new Vector3(0.03f, 0.03f, 0.006f), White);

            Tree(k, new Vector3(0.19f, 0.024f, 0.07f), 0.035f);
            MiniBus(k, new Vector3(0.05f, 0.027f, -0.12f), 90f, 0.18f, White);
        }

        /// <summary>Otogar: tek katlı bina, peron adası (küçük çatılı) ve önünde park etmiş otobüsler.</summary>
        static void BuildTerminal(MeshKit k)
        {
            k.Box(new Vector3(0f, 0.012f, -0.015f), new Vector3(0.62f, 0.024f, 0.44f), Slab);
            k.Box(new Vector3(0.12f, 0.0255f, -0.13f), new Vector3(0.38f, 0.003f, 0.18f), Asphalt);

            // Bina
            Building(k, new Vector3(-0.12f, 0.024f, 0.09f), new Vector3(0.32f, 0.11f, 0.18f), 1);
            k.Layer = MeshLayer.Accent;
            k.Box(new Vector3(-0.12f, 0.144f, 0.09f), new Vector3(0.35f, 0.02f, 0.21f), White);
            k.Layer = MeshLayer.Lit;
            k.Box(new Vector3(-0.12f, 0.066f, -0.012f), new Vector3(0.11f, 0.008f, 0.03f), Trim); // giriş saçağı

            // Peron adası ve çatısı
            k.Box(new Vector3(0.13f, 0.031f, -0.03f), new Vector3(0.34f, 0.012f, 0.04f), Curb);
            foreach (float x in new[] { -0.02f, 0.13f, 0.28f })
                k.Box(new Vector3(x, 0.065f, -0.03f), new Vector3(0.008f, 0.07f, 0.008f), Post);
            k.Layer = MeshLayer.Accent;
            k.Box(new Vector3(0.13f, 0.102f, -0.03f), new Vector3(0.36f, 0.01f, 0.06f), White);
            k.Layer = MeshLayer.Lit;

            for (int i = 0; i < 3; i++) MiniBus(k, new Vector3(0.02f + i * 0.11f, 0.027f, -0.14f), 180f, 0.16f, BusColors[i]);
            Tree(k, new Vector3(-0.25f, 0.024f, -0.13f), 0.04f);
        }

        /// <summary>Büyük otogar: iki katlı, beşik çatılı bina; çatılı peron adası, dört otobüs, tabela direği.</summary>
        static void BuildLarge(MeshKit k)
        {
            k.Box(new Vector3(0f, 0.012f, -0.03f), new Vector3(0.82f, 0.024f, 0.56f), Slab);
            k.Box(new Vector3(0.17f, 0.0255f, -0.17f), new Vector3(0.46f, 0.003f, 0.22f), Asphalt);

            Building(k, new Vector3(-0.16f, 0.024f, 0.12f), new Vector3(0.46f, 0.15f, 0.24f), 2);
            k.Layer = MeshLayer.Accent;
            k.Gable(new Vector3(-0.16f, 0.174f, 0.12f), new Vector3(0.49f, 0.075f, 0.27f), White);
            k.Box(new Vector3(-0.16f, 0.076f, -0.016f), new Vector3(0.13f, 0.01f, 0.05f), White); // giriş saçağı
            k.Layer = MeshLayer.Lit;

            // Peron adası
            k.Box(new Vector3(0.17f, 0.031f, -0.05f), new Vector3(0.44f, 0.012f, 0.045f), Curb);
            foreach (float x in new[] { -0.03f, 0.17f, 0.37f })
                k.Box(new Vector3(x, 0.07f, -0.05f), new Vector3(0.009f, 0.08f, 0.009f), Post);
            k.Layer = MeshLayer.Accent;
            k.Box(new Vector3(0.17f, 0.112f, -0.05f), new Vector3(0.46f, 0.012f, 0.07f), White);
            k.Layer = MeshLayer.Lit;
            for (int i = 0; i < 4; i++) MiniBus(k, new Vector3(0.02f + i * 0.1f, 0.027f, -0.17f), 180f, 0.17f, BusColors[i]);

            // Tabela direği
            k.Box(new Vector3(-0.35f, 0.1f, -0.2f), new Vector3(0.035f, 0.15f, 0.025f), Trim);
            k.Box(new Vector3(-0.35f, 0.17f, -0.2f), new Vector3(0.06f, 0.035f, 0.03f), CompanyOrange);
            Tree(k, new Vector3(-0.22f, 0.024f, -0.2f), 0.045f);
            Tree(k, new Vector3(-0.08f, 0.024f, -0.22f), 0.04f);
        }

        /// <summary>Metropol terminali: cam cepheli büyük salon, ışıklı çatı feneri, saat kulesi, iki peron adası.</summary>
        static void BuildMetro(MeshKit k)
        {
            k.Box(new Vector3(0f, 0.012f, -0.04f), new Vector3(1.02f, 0.024f, 0.72f), Slab);
            k.Box(new Vector3(0.2f, 0.0255f, -0.22f), new Vector3(0.58f, 0.003f, 0.3f), Asphalt);

            // Ana salon: cam cephe
            var hall = new Vector3(-0.16f, 0.024f, 0.16f);
            var size = new Vector3(0.62f, 0.19f, 0.3f);
            k.Box(hall + new Vector3(0f, size.y / 2f, 0f), size, Wall);
            k.Box(hall + new Vector3(0f, size.y * 0.52f, -size.z / 2f - 0.002f), new Vector3(size.x * 0.9f, size.y * 0.72f, 0.004f), White, MeshLayer.Window);
            for (int i = 1; i < 6; i++) // cam cephe kayıtları
                k.Box(hall + new Vector3(-size.x * 0.45f + i * size.x * 0.15f, size.y * 0.52f, -size.z / 2f - 0.004f), new Vector3(0.008f, size.y * 0.72f, 0.004f), Trim);
            k.Box(hall + new Vector3(0f, 0.03f, -size.z / 2f - 0.005f), new Vector3(0.1f, 0.06f, 0.004f), Door);
            k.Layer = MeshLayer.Accent;
            k.Box(hall + new Vector3(0f, size.y + 0.01f, 0f), new Vector3(size.x + 0.03f, 0.02f, size.z + 0.03f), White);
            k.Box(hall + new Vector3(0f, size.y + 0.064f, 0f), new Vector3(0.34f, 0.012f, 0.19f), White);
            k.Layer = MeshLayer.Lit;
            k.Box(hall + new Vector3(0f, size.y + 0.04f, 0f), new Vector3(0.3f, 0.036f, 0.15f), White, MeshLayer.Window); // çatı feneri
            k.Box(hall + new Vector3(0f, 0.1f, -size.z / 2f - 0.03f), new Vector3(0.16f, 0.01f, 0.06f), Trim); // giriş saçağı

            // Saat kulesi
            var tower = new Vector3(-0.43f, 0.024f, -0.08f);
            k.Box(tower + new Vector3(0f, 0.18f, 0f), new Vector3(0.07f, 0.36f, 0.07f), Wall);
            k.Box(tower + new Vector3(0f, 0.3f, -0.037f), new Vector3(0.05f, 0.05f, 0.004f), White);
            k.Box(tower + new Vector3(0f, 0.308f, -0.04f), new Vector3(0.004f, 0.022f, 0.002f), Trim);
            k.Box(tower + new Vector3(0.007f, 0.3f, -0.04f), new Vector3(0.016f, 0.004f, 0.002f), Trim);
            k.Layer = MeshLayer.Accent;
            k.Frustum(tower + new Vector3(0f, 0.36f, 0f), 0.06f, 0f, 0.07f, White, 4, false, true, Mathf.PI / 4f);
            k.Layer = MeshLayer.Lit;

            // İki peron adası
            for (int row = 0; row < 2; row++)
            {
                float z = row == 0 ? -0.07f : -0.32f;
                k.Box(new Vector3(0.21f, 0.031f, z), new Vector3(0.56f, 0.012f, 0.04f), Curb);
                foreach (float x in new[] { -0.04f, 0.21f, 0.46f })
                    k.Box(new Vector3(x, 0.07f, z), new Vector3(0.009f, 0.08f, 0.009f), Post);
                k.Layer = MeshLayer.Accent;
                k.Box(new Vector3(0.21f, 0.112f, z), new Vector3(0.58f, 0.012f, 0.065f), White);
                k.Layer = MeshLayer.Lit;
            }
            for (int i = 0; i < 5; i++) MiniBus(k, new Vector3(-0.01f + i * 0.11f, 0.027f, -0.19f), 180f, 0.17f, BusColors[i]);

            Tree(k, new Vector3(-0.3f, 0.024f, -0.26f), 0.05f);
            Tree(k, new Vector3(-0.14f, 0.024f, -0.3f), 0.045f);
            Tree(k, new Vector3(0.44f, 0.024f, 0.22f), 0.045f);
        }

        // ------------------------------------------------------------------ parçalar

        /// <summary>Bina gövdesi: krem duvarlar, ön yüzde kat sayısı kadar pencere bandı ve kapı.</summary>
        static void Building(MeshKit k, Vector3 baseCenter, Vector3 size, int floors)
        {
            k.Box(baseCenter + new Vector3(0f, size.y / 2f, 0f), size, Wall);
            k.Box(baseCenter + new Vector3(size.x / 2f + 0.001f, size.y / 2f, 0f), new Vector3(0.002f, size.y * 0.98f, size.z * 0.98f), WallSide);
            float front = baseCenter.z - size.z / 2f - 0.002f;
            for (int f = 0; f < floors; f++)
            {
                float y = baseCenter.y + size.y * (floors == 1 ? 0.62f : 0.36f + f * 0.36f);
                k.Box(new Vector3(baseCenter.x, y, front), new Vector3(size.x * 0.86f, size.y * (floors == 1 ? 0.34f : 0.2f), 0.004f), White, MeshLayer.Window);
            }
            k.Box(new Vector3(baseCenter.x, baseCenter.y + size.y * 0.18f, front - 0.001f), new Vector3(size.x * 0.16f, size.y * 0.36f, 0.004f), Door);
        }

        /// <summary>Haritadaki minik otobüs: gövde, pencere bandı, ön cam, tekerlekler. Burun yerel −z yönünde.</summary>
        static void MiniBus(MeshKit k, Vector3 position, float yaw, float length, Color body)
        {
            float w = 0.06f, h = 0.068f;
            k.Push(position, yaw);
            k.Box(new Vector3(0f, 0.012f + h * 0.5f, 0f), new Vector3(w, h, length), body);
            k.Box(new Vector3(0f, 0.012f + h * 0.68f, 0.004f), new Vector3(w + 0.003f, h * 0.3f, length * 0.86f), BusGlass);
            k.Box(new Vector3(0f, 0.012f + h * 0.62f, -length / 2f - 0.002f), new Vector3(w * 0.84f, h * 0.44f, 0.004f), BusGlass);
            k.Box(new Vector3(0f, 0.012f + h + 0.003f, 0.01f), new Vector3(w * 0.8f, 0.006f, length * 0.5f), White);
            foreach (float z in new[] { -length * 0.3f, length * 0.3f })
                foreach (float x in new[] { -w / 2f, w / 2f })
                    k.Box(new Vector3(x, 0.012f, z), new Vector3(0.012f, 0.024f, 0.026f), Tire);
            k.Pop();
        }

        static void Tree(MeshKit k, Vector3 position, float radius)
        {
            k.Box(position + new Vector3(0f, radius * 0.6f, 0f), new Vector3(radius * 0.25f, radius * 1.2f, radius * 0.25f), Trunk);
            k.Sphere(position + new Vector3(0f, radius * 1.65f, 0f), radius, Leaf, 7, 4);
        }
    }
}
