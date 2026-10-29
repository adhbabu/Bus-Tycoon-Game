using System.Collections.Generic;
using BusTycoon.Stations;
using UnityEngine;

namespace BusTycoon.Game
{
    // Durak geliştirmelerinin 3B karşılıkları. Her geliştirme bir ya da birkaç "parça"dır: her parçanın kendi dayanak
    // noktası vardır, satın alınınca StationView parçaları yerinde büyüterek (zıplayarak) gösterir.
    public sealed partial class StationScene
    {
        readonly Dictionary<string, List<Transform>> upgradeParts = new Dictionary<string, List<Transform>>();
        List<Transform> currentParts;
        Transform currentRoot;

        /// <summary>Geliştirmenin parçaları (satın alma canlandırması için).</summary>
        public IReadOnlyList<Transform> PartsOf(string id) =>
            upgradeParts.TryGetValue(id, out var parts) ? parts : (IReadOnlyList<Transform>)System.Array.Empty<Transform>();

        GameObject BuildUpgrade(string id)
        {
            if (upgradeObjects.TryGetValue(id, out GameObject existing)) return existing;
            currentRoot = new GameObject("Upgrade_" + id).transform;
            currentRoot.SetParent(Root, false);
            currentParts = new List<Transform>();
            upgradeParts[id] = currentParts;
            upgradeObjects[id] = currentRoot.gameObject;

            switch (id)
            {
                case StationCatalog.Seats: BuildSeats(); break;
                case StationCatalog.Plants: BuildPlants(); break;
                case StationCatalog.Lights: BuildLights(); break;
                case StationCatalog.Kiosk: BuildKiosk(); break;
                case StationCatalog.Board: BuildBoard(); break;
                case StationCatalog.Wifi: BuildWifi(); break;
                case StationCatalog.Ticket: if (!isStop) BuildTicket(); break;
                case StationCatalog.Cafe: if (!isStop) BuildCafe(); break;
                case StationCatalog.Lockers: if (!isStop) BuildLockers(); break;
                case StationCatalog.Taxi: if (!isStop) BuildTaxi(); break;
                case StationCatalog.Canopy: if (!isStop) BuildCanopy(); break;
                case StationCatalog.Kids: if (!isStop) BuildKids(); break;
                case StationCatalog.Shop: if (!isStop) BuildShop(); break;
                case StationCatalog.Vip: if (!isStop) BuildVip(); break;
                case StationCatalog.Fountain: if (!isStop) BuildFountain(); break;
                case StationCatalog.Clock: if (!isStop) BuildClock(); break;
            }
            return currentRoot.gameObject;
        }

        // ------------------------------------------------------------------ parça yardımcıları

        /// <summary>Dayanak noktası <paramref name="anchor"/> olan parça başlatır (mesh mutlak koordinatlarla kurulur).</summary>
        static MeshKit Begin(Vector3 anchor)
        {
            var k = new MeshKit();
            k.Push(Matrix4x4.TRS(-anchor, Quaternion.identity, Vector3.one));
            return k;
        }

        Transform End(MeshKit k, Vector3 anchor, string name, Transform parent = null)
        {
            Transform holder = new GameObject(name).transform;
            holder.SetParent(parent != null ? parent : currentRoot, false);
            holder.localPosition = anchor;
            GameObject mesh = Make(k, "Mesh", holder);
            if (mesh == null) mesh = holder.gameObject;
            currentParts.Add(holder);
            return holder;
        }

        // ------------------------------------------------------------------ küçük durak + otogar

        /// <summary>Minderli turuncu koltuklar, kolçaklar (bankların yerine).</summary>
        void BuildSeats()
        {
            var rows = new Dictionary<int, List<StationSpot>>();
            foreach (var seat in SeatRows())
            {
                int key = Mathf.RoundToInt(seat.Position.z * 10f);
                if (!rows.TryGetValue(key, out var list)) rows[key] = list = new List<StationSpot>();
                list.Add(seat);
            }
            foreach (var row in rows.Values)
            {
                Vector3 anchor = (row[0].Position + row[row.Count - 1].Position) / 2f;
                MeshKit k = Begin(anchor);
                foreach (var seat in row)
                {
                    k.Push(seat.Position, Yaw(seat.Facing));
                    k.Box(new Vector3(0f, 0.2f, 0.12f), new Vector3(0.08f, 0.4f, 0.3f), Frame);
                    k.Box(new Vector3(0f, 0.41f, 0.17f), new Vector3(0.58f, 0.12f, 0.5f), Orange);
                    k.Box(new Vector3(0f, 0.74f, -0.1f), new Vector3(0.58f, 0.6f, 0.1f), Orange);
                    k.Box(new Vector3(0f, 0.74f, -0.16f), new Vector3(0.6f, 0.62f, 0.03f), Frame);
                    foreach (float x in new[] { -0.32f, 0.32f })
                    {
                        k.Box(new Vector3(x, 0.6f, 0.13f), new Vector3(0.06f, 0.05f, 0.44f), Frame);
                        k.Box(new Vector3(x, 0.52f, 0.3f), new Vector3(0.05f, 0.16f, 0.05f), Frame);
                    }
                    k.Pop();
                }
                End(k, anchor, "SeatRow");
            }
        }

        /// <summary>Saksı bitkileri, çiçeklikler ve ek ağaçlar.</summary>
        void BuildPlants()
        {
            var pots = new List<Vector3>();
            var planters = new List<(Vector3 position, float yaw)>();
            var trees = new List<Vector3>();
            if (isStop)
            {
                planters.Add((new Vector3(-6.5f, 0.18f, 4.6f), 0f));
                planters.Add((new Vector3(6.6f, 0.18f, 4.6f), 0f));
                planters.Add((new Vector3(-12.5f, 0.18f, 4.6f), 0f));
                pots.Add(new Vector3(-3.8f, 0.18f, 3.6f));
                pots.Add(new Vector3(3.9f, 0.18f, 4.3f));
                trees.Add(new Vector3(6f, 0.18f, 9f));
                trees.Add(new Vector3(-7f, 0.18f, 10f));
            }
            else
            {
                pots.Add(Hall(0.9f, 0.9f));
                pots.Add(Hall(W / 2f - doorW / 2f - 0.9f, 0.9f));
                pots.Add(Hall(W * 0.31f, D - 0.9f));
                pots.Add(Hall(W * 0.67f, D - 0.9f));
                pots.Add(Hall(W * (Info.Tier == StationTier.Terminal ? 0.97f : 0.72f) - 0.6f, 0.9f));
                pots.Add(Hall(W - 0.9f, D * 0.5f - 2.8f));
                for (float x = x0 + 6f; x < x1 - 2f; x += 7f)
                    if (Mathf.Abs(x - (x0 + x1) / 2f) > doorW) planters.Add((new Vector3(x, 0.06f, z0 - 1.4f), 0f));
                planters.Add((new Vector3(wx0 + 0.6f, 0.2f, zw1 - 1.5f), 90f));
                trees.Add(new Vector3(x0 - 4f, 0.06f, z0 + 2f));
                trees.Add(new Vector3(x0 - 4f, 0.06f, z1 - 1f));
            }
            int index = 0;
            foreach (Vector3 p in pots)
            {
                MeshKit k = Begin(p);
                k.Frustum(p, 0.32f, 0.42f, 0.7f, Pot, 8, true, false);
                if (index++ % 2 == 0)
                {
                    k.Sphere(p + new Vector3(0f, 1.25f, 0f), 0.55f, Leaf, 8, 5);
                    k.Sphere(p + new Vector3(0.2f, 1.65f, 0.1f), 0.38f, LeafDark, 7, 4);
                    k.Cylinder(p + new Vector3(0f, 0.7f, 0f), 0.05f, 0.4f, Trunk, 5);
                }
                else
                {
                    for (int i = 0; i < 7; i++)
                    {
                        float a = i * Mathf.PI * 2f / 7f;
                        k.Push(p + new Vector3(Mathf.Cos(a) * 0.12f, 0.7f, Mathf.Sin(a) * 0.12f), Quaternion.Euler(Mathf.Sin(a) * 14f, 0f, -Mathf.Cos(a) * 14f));
                        k.Frustum(Vector3.zero, 0.09f, 0.0f, 0.95f + (i % 3) * 0.2f, i % 2 == 0 ? Leaf : LeafDark, 4);
                        k.Pop();
                    }
                }
                End(k, p, "Pot");
            }
            Color[] flowers = { new Color(1f, 0.45f, 0.6f), new Color(1f, 0.85f, 0.25f), new Color(0.95f, 0.3f, 0.3f), White, new Color(0.7f, 0.5f, 1f) };
            foreach (var (p, yaw) in planters)
            {
                MeshKit k = Begin(p);
                k.Push(p, yaw);
                k.Box(new Vector3(0f, 0.3f, 0f), new Vector3(2.2f, 0.6f, 0.7f), Wood);
                k.Box(new Vector3(0f, 0.6f, 0f), new Vector3(2.05f, 0.04f, 0.55f), Trunk);
                for (int i = 0; i < 9; i++)
                {
                    var f = new Vector3(-0.9f + i * 0.225f, 0.72f + (i % 2) * 0.06f, (i % 3 - 1) * 0.15f);
                    k.Sphere(f, 0.11f, LeafDark, 5, 3);
                    k.Sphere(f + new Vector3(0f, 0.1f, 0f), 0.09f, flowers[i % flowers.Length], 5, 3);
                }
                k.Pop();
                End(k, p, "Planter");
            }
            foreach (Vector3 p in trees)
            {
                MeshKit k = Begin(p);
                Tree(k, p, 1.5f);
                k.Cylinder(p, 0.9f, 0.25f, Wood, 8);
                End(k, p, "Tree");
            }
        }

        /// <summary>Sarkıt lambalar (salon), süslü sokak lambaları (meydan ve peron): gece ışık havuzları.</summary>
        void BuildLights()
        {
            var posts = new List<Vector3>();
            if (isStop)
            {
                posts.Add(new Vector3(-6.5f, 0.18f, 0.7f));
                posts.Add(new Vector3(2.8f, 0.18f, 0.7f));
            }
            else
            {
                for (float u = 3f; u < W - 1f; u += 5f)
                {
                    if (u < W * 0.3f || u > W * 0.7f) continue;
                    for (int r = 0; r < 2; r++)
                    {
                        Vector3 p = Hall(u, D * 0.36f + r * 2.6f, H - 0.25f);
                        MeshKit k = Begin(p);
                        k.Box(p + new Vector3(0f, -0.9f, 0f), new Vector3(0.03f, 1.8f, 0.03f), Dark);
                        k.Frustum(p + new Vector3(0f, -2.3f, 0f), 0.55f, 0.15f, 0.5f, Orange, 10, true, false);
                        k.Layer = MeshLayer.Glow;
                        k.Sphere(p + new Vector3(0f, -2.32f, 0f), 0.2f, WarmLight, 8, 4);
                        k.Layer = MeshLayer.Lit;
                        End(k, p, "Pendant");
                        lamps.Add((new Vector3(p.x, F, p.z), 2.6f));
                    }
                }
                for (float x = x0 + 5f; x < x1 - 1f; x += 8f) posts.Add(new Vector3(x, 0.06f, roadN + 4.6f));
                foreach (float z in bayZ) posts.Add(new Vector3(wx0 + 0.35f, 0.2f, z - 1.0f));
            }
            foreach (Vector3 p in posts)
            {
                MeshKit k = Begin(p);
                LampPost(k, p, true);
                End(k, p, "LampPost");
                lamps.Add((p, 4f));
            }
        }

        /// <summary>Simit ve çay büfesi: kırmızı-beyaz tenteli kulübe, vitrinde simitler, semaver ve ince belli bardaklar.</summary>
        void BuildKiosk()
        {
            Vector3 p = isStop ? new Vector3(-8f, 0.18f, 2.8f) : Hall(W * 0.14f, D * 0.22f);
            float yaw = isStop ? 180f : 90f; // tezgâh yönü: durakta caddeye, salonda doğuya (girişe)
            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            MeshKit k = Begin(p);
            k.Push(p, yaw);
            k.Box(new Vector3(0f, 1.25f, -0.25f), new Vector3(2.4f, 2.5f, 1.5f), White);                 // kulübe
            k.Box(new Vector3(0f, 0.55f, 0.6f), new Vector3(2.4f, 1.1f, 0.4f), new Color(0.86f, 0.18f, 0.2f)); // tezgâh
            k.Box(new Vector3(0f, 1.12f, 0.62f), new Vector3(2.5f, 0.06f, 0.5f), White);
            k.Box(new Vector3(-0.45f, 1.42f, 0.62f), new Vector3(1.2f, 0.55f, 0.42f), White, MeshLayer.Glass); // vitrin
            for (int i = 0; i < 6; i++)
                k.Ring(new Vector3(-0.85f + (i % 3) * 0.4f, 1.22f + (i / 3) * 0.22f, 0.62f), 0.12f, 0.06f, new Color(0.72f, 0.45f, 0.2f), 8);
            k.Cylinder(new Vector3(0.65f, 1.15f, 0.55f), 0.2f, 0.55f, Metal, 8);                       // semaver
            k.Sphere(new Vector3(0.65f, 1.78f, 0.55f), 0.13f, Metal, 6, 3);
            for (int i = 0; i < 4; i++)
                k.Frustum(new Vector3(0.2f + i * 0.13f, 1.15f, 0.75f), 0.035f, 0.045f, 0.11f, new Color(0.62f, 0.14f, 0.08f), 6, false);
            for (int i = 0; i < 8; i++) // tente şeritleri
                k.Box(new Vector3(-1.1f + i * 0.315f, 2.55f, 0.75f), new Vector3(0.315f, 0.12f, 1.0f), i % 2 == 0 ? new Color(0.86f, 0.18f, 0.2f) : White);
            k.Box(new Vector3(0f, 2.75f, -0.25f), new Vector3(2.6f, 0.18f, 1.7f), new Color(0.86f, 0.18f, 0.2f));
            k.Box(new Vector3(0f, 3.08f, 0.6f), new Vector3(2.4f, 0.48f, 0.1f), White); // tabela levhası
            k.Pop();
            Transform part = End(k, p, "Kiosk");
            StationSigns.Text(part, "SİMİT · ÇAY", Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 3.08f, 0.67f), yaw + 180f, 0.34f, 2.2f, new Color(0.86f, 0.18f, 0.2f));
            staffSpots.Add((new StationSpot(p + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0.1f, 0f, -0.15f), forward), StationCrowd.Role.Vendor));
            spots.Add(new StationSpot(p + forward * 1.4f + Quaternion.Euler(0f, yaw, 0f) * Vector3.right * 0.4f, -forward));
            spots.Add(new StationSpot(p + forward * 1.6f - Quaternion.Euler(0f, yaw, 0f) * Vector3.right * 0.6f, -forward));
        }

        /// <summary>Dijital kalkış panosu: hatlarınızın varış şehirleri, saatleri ve peronları (her saniye güncellenir).</summary>
        void BuildBoard()
        {
            Vector3 p;
            float width, height;
            Transform parent = null;
            if (isStop)
            {
                p = new Vector3(-4.8f, 0.18f, 1.7f);
                width = 1.4f;
                height = 1.2f;
            }
            else
            {
                width = W * 0.34f;
                height = Mathf.Min(1.6f, H * 0.24f);
                p = Hall(W / 2f, D - 0.3f, F + H * 0.6f);
                parent = northWall;
            }
            MeshKit k = Begin(p);
            Vector3 screen = p;
            if (isStop)
            {
                k.Box(p + new Vector3(0f, 1.2f, 0f), new Vector3(0.25f, 2.4f, 0.25f), Frame);
                screen = p + new Vector3(0f, 1.75f, -0.14f);
                k.Box(screen + new Vector3(0f, 0f, 0.05f), new Vector3(width + 0.15f, height + 0.15f, 0.12f), Frame);
            }
            else
            {
                k.Box(p, new Vector3(width + 0.3f, height + 0.3f, 0.2f), Frame);
                screen = p + new Vector3(0f, 0f, -0.11f);
            }
            k.Box(screen, new Vector3(width, height, 0.02f), ScreenBlue, MeshLayer.Glow);
            Transform part = End(k, p, "Board", parent);
            float rowHeight = height / 5.2f;
            Vector3 local = screen - p;
            StationSigns.Text(part, "KALKIŞLAR", local + new Vector3(0f, height / 2f - rowHeight * 0.7f, -0.02f), 0f, rowHeight * 0.8f, width * 0.9f, new Color(1f, 0.82f, 0.25f));
            int rows = 4;
            for (int i = 0; i < rows; i++)
            {
                TextMesh row = StationSigns.Text(part, " ", local + new Vector3(0f, height / 2f - rowHeight * (1.85f + i), -0.02f), 0f, rowHeight * 0.7f, width * 0.92f, White);
                if (row != null) boardRows.Add(row);
            }
            boardTimer = 0f;
            if (!isStop) spots.Add(new StationSpot(Hall(W * 0.42f, D - 4.4f), Vector3.forward));
        }

        /// <summary>Ücretsiz Wi-Fi ve şarj direkleri.</summary>
        void BuildWifi()
        {
            var positions = isStop ? new[] { new Vector3(3.9f, 0.18f, 3.2f) } : new[] { Hall(W * 0.4f, D * 0.27f), Hall(W * 0.6f, D * 0.27f) };
            foreach (Vector3 p in positions)
            {
                MeshKit k = Begin(p);
                k.Box(p + new Vector3(0f, 0.8f, 0f), new Vector3(0.45f, 1.6f, 0.45f), White);
                k.Box(p + new Vector3(0f, 1.66f, 0f), new Vector3(0.5f, 0.14f, 0.5f), Orange);
                k.Layer = MeshLayer.Glow;
                k.Box(p + new Vector3(0f, 1.2f, -0.23f), new Vector3(0.3f, 0.28f, 0.02f), new Color(0.35f, 0.8f, 1f));
                k.Box(p + new Vector3(0f, 1.2f, 0.23f), new Vector3(0.3f, 0.28f, 0.02f), new Color(0.35f, 0.8f, 1f));
                k.Layer = MeshLayer.Lit;
                for (int i = 0; i < 3; i++) k.Box(p + new Vector3(-0.12f + i * 0.12f, 0.85f, -0.24f), new Vector3(0.07f, 0.13f, 0.02f), Dark);
                Transform part = End(k, p, "Charger");
                StationSigns.Text(part, "Wi-Fi", new Vector3(0f, 1.66f, -0.26f), 0f, 0.12f, 0.45f, White);
                StationSigns.Text(part, "Wi-Fi", new Vector3(0f, 1.66f, 0.26f), 180f, 0.12f, 0.45f, White);
                spots.Add(new StationSpot(p + new Vector3(0.1f, 0f, -0.7f), Vector3.forward));
                spots.Add(new StationSpot(p + new Vector3(-0.5f, 0f, 0.6f), Vector3.back));
            }
        }

        // ------------------------------------------------------------------ otogar geliştirmeleri

        /// <summary>Şirketin turuncu bilet gişeleri, cam bölmeler, görevliler ve kuyruk bariyerleri.</summary>
        void BuildTicket()
        {
            float from = W * 0.36f, to = W * 0.64f;
            Vector3 p = Hall((from + to) / 2f, D - 1.0f);
            MeshKit k = Begin(p);
            int booths = Mathf.Max(2, Mathf.RoundToInt((to - from) / 2.6f));
            float boothWidth = (to - from) / booths;
            k.Box(Hall((from + to) / 2f, D - 1.0f, F + 0.55f), new Vector3(to - from, 1.1f, 0.9f), Orange);
            k.Box(Hall((from + to) / 2f, D - 1.0f, F + 1.13f), new Vector3(to - from + 0.1f, 0.06f, 1.0f), White);
            k.Box(Hall((from + to) / 2f, D - 1.0f, F + 1.53f), new Vector3(to - from, 0.8f, 0.04f), White, MeshLayer.Glass);
            for (int i = 0; i <= booths; i++)
                k.Box(Hall(from + i * boothWidth, D - 1.0f, F + 1.25f), new Vector3(0.08f, 2.2f, 1.0f), Frame);
            k.Box(Hall((from + to) / 2f, D - 1.45f, F + 2.12f), new Vector3(to - from + 0.2f, 0.34f, 0.08f), Orange); // alın levhası
            // Kuyruk bariyerleri
            for (int i = 0; i < 4; i++)
            {
                Vector3 post = Hall(W / 2f - 1.5f + i * 1f, D - 3.0f);
                k.Cylinder(post, 0.06f, 1.0f, Metal, 6);
                k.Cylinder(post, 0.18f, 0.05f, Metal, 8);
                if (i < 3) k.Box(post + new Vector3(0.5f, 0.92f, 0f), new Vector3(1f, 0.07f, 0.02f), Orange);
            }
            Transform part = End(k, p, "TicketDesk");
            string company = string.IsNullOrEmpty(Info.CompanyName) ? "BİLET" : "BİLET · " + Upper(Info.CompanyName);
            StationSigns.Text(part, company, Hall((from + to) / 2f, D - 1.5f, F + 2.12f) - p, 0f, 0.26f, to - from - 0.3f, White);
            for (int i = 0; i < booths; i++)
            {
                staffSpots.Add((new StationSpot(Hall(from + (i + 0.5f) * boothWidth, D - 0.45f), Vector3.back), StationCrowd.Role.Attendant));
                spots.Add(new StationSpot(Hall(from + (i + 0.5f) * boothWidth, D - 2.0f), Vector3.forward));
            }
            spots.Add(new StationSpot(Hall(W / 2f - 0.6f, D - 3.6f), Vector3.forward));
            spots.Add(new StationSpot(Hall(W / 2f + 0.4f, D - 4.1f), Vector3.forward));
        }

        /// <summary>Köşe kafe: tezgâh, kahve makinesi, menü panosu, masalar ve sandalyeler (oturanlar masaya bakar).</summary>
        void BuildCafe()
        {
            float cafeWidth = W * 0.24f;
            Vector3 p = Hall(0.5f + cafeWidth / 2f, D - 2.6f);
            MeshKit k = Begin(p);
            // Ahşap zemin
            k.Box(Hall(0.4f + cafeWidth / 2f, D - 3f, F + 0.012f), new Vector3(cafeWidth, 0.02f, 5.4f), new Color(0.78f, 0.6f, 0.42f));
            // Tezgâh (kuzey duvarı boyunca)
            k.Box(Hall(0.5f + cafeWidth / 2f, D - 0.9f, F + 0.53f), new Vector3(cafeWidth - 0.4f, 1.06f, 0.75f), WoodDark);
            k.Box(Hall(0.5f + cafeWidth / 2f, D - 0.9f, F + 1.08f), new Vector3(cafeWidth - 0.3f, 0.05f, 0.85f), White);
            k.Box(Hall(1.2f, D - 0.75f, F + 1.33f), new Vector3(0.6f, 0.5f, 0.45f), Metal);       // kahve makinesi
            k.Box(Hall(1.2f, D - 0.97f, F + 1.33f), new Vector3(0.45f, 0.25f, 0.02f), Dark);
            k.Box(Hall(2.3f, D - 0.9f, F + 1.25f), new Vector3(0.6f, 0.28f, 0.45f), White, MeshLayer.Glass); // pasta vitrini
            for (int i = 0; i < 3; i++) k.Cylinder(Hall(2.1f + i * 0.2f, D - 0.9f, F + 1.13f), 0.07f, 0.1f, new Color(0.55f, 0.3f, 0.2f), 6);
            int tables = Info.Tier == StationTier.Terminal ? 2 : Info.Tier == StationTier.Large ? 3 : 4;
            for (int i = 0; i < tables; i++)
            {
                Vector3 t = Hall(1.4f + (i % 2) * (cafeWidth - 1.9f), D - 3.0f - (i / 2) * 2.3f);
                k.Cylinder(t, 0.06f, 0.75f, Frame, 6);
                k.Cylinder(t + new Vector3(0f, 0.75f, 0f), 0.48f, 0.05f, White, 12);
                k.Cylinder(t + new Vector3(0f, 0.8f, -0.15f), 0.05f, 0.09f, White, 6);
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 chair = t + new Vector3(side * 0.85f, 0f, 0f);
                    Vector3 facing = new Vector3(-side, 0f, 0f);
                    k.Push(chair, Yaw(facing));
                    k.Box(new Vector3(0f, 0.42f, 0.12f), new Vector3(0.45f, 0.06f, 0.42f), Wood);
                    k.Box(new Vector3(0f, 0.72f, -0.1f), new Vector3(0.45f, 0.55f, 0.05f), Wood);
                    k.Box(new Vector3(0f, 0.2f, 0.12f), new Vector3(0.05f, 0.4f, 0.35f), Frame);
                    k.Pop();
                    seats.Add(new StationSpot(chair, facing));
                }
            }
            Transform part = End(k, p, "Cafe");
            // Menü panosu ve tabela (kuzey duvarında)
            Vector3 menu = Hall(0.5f + cafeWidth / 2f, D - 0.25f, F + 2.3f);
            MeshKit m = Begin(menu);
            m.Box(menu, new Vector3(cafeWidth * 0.7f, 1.0f, 0.05f), Dark);
            m.Layer = MeshLayer.Glow;
            for (int i = 0; i < 4; i++) m.Box(menu + new Vector3(0f, 0.3f - i * 0.18f, -0.03f), new Vector3(cafeWidth * 0.5f, 0.05f, 0.01f), new Color(0.95f, 0.9f, 0.75f));
            m.Layer = MeshLayer.Lit;
            Transform sign = End(m, menu, "CafeMenu", northWall);
            StationSigns.Text(sign, "KAFE", new Vector3(0f, 0.85f, -0.03f), 0f, 0.5f, cafeWidth * 0.6f, Orange);
            staffSpots.Add((new StationSpot(Hall(0.5f + cafeWidth / 2f, D - 0.4f), Vector3.back), StationCrowd.Role.Barista));
            spots.Add(new StationSpot(Hall(0.5f + cafeWidth * 0.62f, D - 1.8f), Vector3.forward));
        }

        /// <summary>Emanet dolapları (metro: güney duvarı, diğerleri: batı duvarı).</summary>
        void BuildLockers()
        {
            bool south = Info.Tier == StationTier.Metro;
            float length = south ? W * 0.14f : D * 0.22f;
            Vector3 p = south ? Hall(W * 0.33f, 0.5f) : Hall(0.5f, D * 0.41f);
            float yaw = south ? 0f : 90f; // dolap kapakları salona baksın
            MeshKit k = Begin(p);
            k.Push(p, yaw);
            k.Box(new Vector3(0f, 1.0f, 0f), new Vector3(length, 2.0f, 0.6f), Metal);
            int columns = Mathf.FloorToInt(length / 0.55f);
            for (int c = 0; c < columns; c++)
                for (int r = 0; r < 4; r++)
                {
                    var door = new Vector3(-length / 2f + (c + 0.5f) * length / columns, 0.3f + r * 0.47f, 0.31f);
                    k.Box(door, new Vector3(length / columns - 0.06f, 0.42f, 0.02f), (c + r) % 3 == 0 ? Orange : new Color(0.84f, 0.86f, 0.9f));
                    k.Box(door + new Vector3(0.15f, 0f, 0.015f), new Vector3(0.03f, 0.1f, 0.02f), Dark);
                }
            k.Pop();
            Transform part = End(k, p, "Lockers");
            StationSigns.Text(part, "EMANET", Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 2.3f, 0.2f), yaw + 180f, 0.32f, length, Orange);
            spots.Add(new StationSpot(p + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0.6f, 0f, 1.0f), Quaternion.Euler(0f, yaw, 0f) * Vector3.back));
        }

        /// <summary>Taksi durağı: cadde kenarında sarı taksiler, tabela ve sarı cep çizgisi.</summary>
        void BuildTaxi()
        {
            Vector3 p = new Vector3(x0 + 10f, 0.03f, roadN - 1.1f);
            MeshKit k = Begin(p);
            k.Box(new Vector3(x0 + 10f, 0.026f, roadN - 2.25f), new Vector3(17f, 0.012f, 0.15f), LaneYellow);
            for (int i = 0; i < 6; i++) k.Box(new Vector3(x0 + 2.5f + i * 3f, 0.026f, roadN - 1.1f), new Vector3(0.15f, 0.012f, 2.2f), LaneYellow);
            Vector3 pole = new Vector3(x0 + 2f, 0.06f, roadN + 0.7f);
            k.Box(pole + new Vector3(0f, 1.6f, 0f), new Vector3(0.12f, 3.2f, 0.12f), Frame);
            k.Box(pole + new Vector3(0f, 3.0f, 0f), new Vector3(1.6f, 0.6f, 0.1f), new Color(1f, 0.8f, 0.1f));
            Transform part = End(k, p, "TaxiRank");
            StationSigns.Text(part, "TAKSİ", pole + new Vector3(0f, 3.0f, -0.06f) - p, 0f, 0.4f, 1.4f, Dark);
            StationSigns.Text(part, "TAKSİ", pole + new Vector3(0f, 3.0f, 0.06f) - p, 180f, 0.4f, 1.4f, Dark);
            Mesh taxi = StationVehicles.Taxi(out MeshLayer[] layers);
            for (int i = 0; i < 3; i++)
            {
                Vector3 position = new Vector3(x0 + 4f + i * 5.5f, 0.03f, roadN - 1.1f);
                Transform holder = new GameObject("Taxi").transform;
                holder.SetParent(currentRoot, false);
                holder.localPosition = position;
                Place(taxi, layers, "Mesh", holder, Vector3.zero, -90f);
                currentParts.Add(holder);
            }
            spots.Add(new StationSpot(new Vector3(x0 + 6.5f, 0.06f, roadN + 0.9f), Vector3.back));
        }

        /// <summary>Peron çatısı: yürüme yolu ve otobüslerin burnu üstünde konsollu çatı, tavan lambaları.</summary>
        void BuildCanopy()
        {
            float height = 5.4f, left = x1 + 0.2f, right = wx1 + 3f;
            float length = zw1 - zw0;
            Vector3 p = new Vector3((left + right) / 2f, 0.2f, (zw0 + zw1) / 2f);
            MeshKit k = Begin(p);
            for (float z = zw0 + 1.5f; z < zw1; z += BayPitch * 2f)
            {
                k.Box(new Vector3(wx0 + 0.5f, 0.2f + height / 2f, z), new Vector3(0.3f, height, 0.3f), White);
                k.Box(new Vector3((wx0 + 0.5f + right) / 2f, 0.2f + height - 0.3f, z), new Vector3(right - wx0, 0.3f, 0.2f), Frame);
            }
            k.Box(new Vector3((left + right) / 2f, 0.2f + height, p.z), new Vector3(right - left, 0.3f, length), White);
            k.Box(new Vector3(right, 0.2f + height, p.z), new Vector3(0.2f, 0.55f, length), Orange);
            for (float z = zw0 + 2f; z < zw1 - 1f; z += 3f)
                k.Box(new Vector3((left + right) / 2f, 0.2f + height + 0.17f, z), new Vector3(right - left - 0.8f, 0.04f, 1.2f), White, MeshLayer.Glass);
            k.Layer = MeshLayer.Glow;
            k.Box(new Vector3((wx0 + right) / 2f, 0.2f + height - 0.18f, p.z), new Vector3(0.25f, 0.05f, length - 1f), WarmLight);
            k.Layer = MeshLayer.Lit;
            End(k, p, "Canopy");
            foreach (float z in bayZ) lamps.Add((new Vector3((wx0 + wx1) / 2f, 0.2f, z), 2.6f));
        }

        /// <summary>Çocuk oyun alanı: renkli yumuşak zemin, kaydırak, top havuzu, bloklar ve çocuklar.</summary>
        void BuildKids()
        {
            float u0 = W * 0.76f, u1 = W - 0.5f, v0 = D - 6f, v1 = D - 0.5f;
            Vector3 p = Hall((u0 + u1) / 2f, (v0 + v1) / 2f);
            MeshKit k = Begin(p);
            Color[] mat = { new Color(0.35f, 0.65f, 0.95f), new Color(1f, 0.82f, 0.25f), new Color(0.95f, 0.4f, 0.4f), new Color(0.45f, 0.8f, 0.45f) };
            int i = 0;
            for (float u = u0; u < u1 - 0.01f; u += 1.25f)
                for (float v = v0; v < v1 - 0.01f; v += 1.25f)
                    k.Box(Hall(u + 0.625f, v + 0.625f, F + 0.04f), new Vector3(1.24f, 0.08f, 1.24f), mat[i++ % mat.Length]);
            // Alçak çit (güney ve batı)
            for (float u = u0; u <= u1; u += 0.6f) k.Box(Hall(u, v0, F + 0.4f), new Vector3(0.06f, 0.8f, 0.06f), White);
            for (float v = v0; v <= v1; v += 0.6f) k.Box(Hall(u0, v, F + 0.4f), new Vector3(0.06f, 0.8f, 0.06f), White);
            k.Box(Hall((u0 + u1) / 2f, v0, F + 0.75f), new Vector3(u1 - u0, 0.08f, 0.08f), Orange);
            k.Box(Hall(u0, (v0 + v1) / 2f, F + 0.75f), new Vector3(0.08f, 0.08f, v1 - v0), Orange);
            // Kaydırak
            Vector3 slide = Hall(u0 + 1.4f, v1 - 1.5f);
            k.Box(slide + new Vector3(0f, 1.0f, 0f), new Vector3(1.0f, 0.12f, 1.0f), new Color(0.95f, 0.4f, 0.4f));
            foreach (var c in new[] { new Vector3(-0.45f, 0f, -0.45f), new Vector3(0.45f, 0f, -0.45f), new Vector3(-0.45f, 0f, 0.45f), new Vector3(0.45f, 0f, 0.45f) })
                k.Box(slide + c + new Vector3(0f, 0.5f, 0f), new Vector3(0.08f, 1.0f, 0.08f), White);
            for (int s = 0; s < 4; s++) k.Box(slide + new Vector3(0f, 0.2f + s * 0.25f, 0.6f + s * 0.0f), new Vector3(0.7f, 0.05f, 0.12f), new Color(0.35f, 0.65f, 0.95f));
            k.Push(slide + new Vector3(0f, 0.55f, -1.15f), Quaternion.Euler(-38f, 0f, 0f));
            k.Box(Vector3.zero, new Vector3(0.7f, 0.06f, 1.6f), new Color(1f, 0.82f, 0.25f));
            k.Box(new Vector3(-0.36f, 0.08f, 0f), new Vector3(0.04f, 0.14f, 1.6f), new Color(1f, 0.82f, 0.25f));
            k.Box(new Vector3(0.36f, 0.08f, 0f), new Vector3(0.04f, 0.14f, 1.6f), new Color(1f, 0.82f, 0.25f));
            k.Pop();
            // Top havuzu
            Vector3 pit = Hall(u1 - 1.4f, v1 - 1.6f);
            k.Box(pit + new Vector3(0f, 0.25f, 0f), new Vector3(2.0f, 0.5f, 2.0f), new Color(0.35f, 0.65f, 0.95f));
            for (int b = 0; b < 26; b++)
                k.Sphere(pit + new Vector3(R(-0.8f, 0.8f), 0.52f + R(0f, 0.08f), R(-0.8f, 0.8f)), 0.14f, mat[b % mat.Length], 5, 3);
            // Bloklar
            for (int b = 0; b < 5; b++)
                k.Box(Hall(u0 + 1.0f + b * 0.45f, v0 + 1.2f + (b % 2) * 0.3f, F + 0.08f + 0.2f + (b == 2 ? 0.4f : 0f)), Vector3.one * 0.4f, mat[(b + 1) % mat.Length]);
            Transform part = End(k, p, "Kids");
            Vector3 signPos = Hall((u0 + u1) / 2f, D - 0.28f, F + 2.6f);
            StationSigns.Text(northWall, "OYUN ALANI", signPos, 0f, 0.4f, u1 - u0 - 0.6f, new Color(0.35f, 0.65f, 0.95f));
            spots.Add(new StationSpot(Hall(u0 + 2.2f, v0 + 2.2f), Vector3.right, 0.62f));
            spots.Add(new StationSpot(Hall(u1 - 2.6f, v0 + 1.4f), Vector3.left, 0.6f));
            spots.Add(new StationSpot(Hall(u0 + 1.4f, v1 - 2.6f), Vector3.forward, 0.64f));
            spots.Add(new StationSpot(Hall(u0 - 0.6f, v0 + 1.5f), Vector3.right));
        }

        /// <summary>Hediyelik eşya dükkânı: cam önlü küçük dükkân, renkli raflar, tezgâh, şehrin ikonu.</summary>
        void BuildShop()
        {
            float u0 = W * 0.76f, u1 = W - 0.35f, v0 = 0.35f, v1 = D * 0.3f;
            Vector3 p = Hall((u0 + u1) / 2f, (v0 + v1) / 2f);
            MeshKit k = Begin(p);
            Color wall = Color.Lerp(theme, White, 0.55f);
            k.Box(Hall((u0 + u1) / 2f, v1, F + 1.5f), new Vector3(u1 - u0, 3f, 0.15f), wall);                    // kuzey duvarı
            float doorV = (v0 + v1) / 2f;
            k.Box(Hall(u0, (v0 + doorV - 0.8f) / 2f, F + 1.5f), new Vector3(0.12f, 3f, doorV - 0.8f - v0), White, MeshLayer.Glass);
            k.Box(Hall(u0, (doorV + 0.8f + v1) / 2f, F + 1.5f), new Vector3(0.12f, 3f, v1 - doorV - 0.8f), White, MeshLayer.Glass);
            k.Box(Hall(u0, (v0 + v1) / 2f, F + 3.1f), new Vector3(0.3f, 0.5f, v1 - v0 + 0.2f), themeDark);      // alın
            Color[] goods = { new Color(0.95f, 0.4f, 0.4f), new Color(0.35f, 0.65f, 0.95f), new Color(1f, 0.82f, 0.25f), new Color(0.45f, 0.8f, 0.45f), new Color(0.75f, 0.5f, 0.9f) };
            for (int r = 0; r < 3; r++)
            {
                Vector3 shelf = Hall(u1 - 0.35f, v0 + 0.8f + r * (v1 - v0 - 1.2f) / 2f, F);
                k.Box(shelf + new Vector3(0f, 1.0f, 0f), new Vector3(0.5f, 2.0f, 1.0f), Wood);
                for (int s = 0; s < 4; s++)
                    for (int g = 0; g < 3; g++)
                        k.Box(shelf + new Vector3(-0.28f, 0.35f + s * 0.45f, -0.3f + g * 0.3f), new Vector3(0.12f, 0.22f, 0.2f), goods[(r + s + g) % goods.Length]);
            }
            k.Box(Hall(u0 + 1.4f, v1 - 0.9f, F + 0.5f), new Vector3(1.4f, 1.0f, 0.6f), WoodDark);
            Transform part = End(k, p, "Shop");
            StationSigns.Text(part, "HEDİYELİK", Hall(u0 - 0.17f, (v0 + v1) / 2f + 0.5f, F + 3.1f) - p, 90f, 0.32f, (v1 - v0) * 0.6f, White);
            StationSigns.CityIcon(part, Info.City, Hall(u0 - 0.17f, v0 + 0.6f, F + 3.1f) - p, 90f, 0.45f);
            staffSpots.Add((new StationSpot(Hall(u0 + 1.4f, v1 - 0.35f), Vector3.back), StationCrowd.Role.Attendant));
            spots.Add(new StationSpot(Hall(u0 - 0.9f, doorV), Vector3.right));
            spots.Add(new StationSpot(Hall(u0 + 0.9f, v0 + 0.9f), Vector3.right));
        }

        /// <summary>VIP bekleme salonu: cam bölmeler, halı, kanepeler, sehpa, bitki ve altın tabela.</summary>
        void BuildVip()
        {
            float u0 = 0.4f, u1 = W * 0.24f, v0 = D * 0.26f, v1 = D * 0.54f;
            Vector3 p = Hall((u0 + u1) / 2f, (v0 + v1) / 2f);
            MeshKit k = Begin(p);
            Color carpet = new Color(0.55f, 0.16f, 0.2f), gold = new Color(0.95f, 0.75f, 0.3f), sofa = new Color(0.22f, 0.24f, 0.32f);
            k.Box(Hall((u0 + u1) / 2f, (v0 + v1) / 2f, F + 0.015f), new Vector3(u1 - u0, 0.03f, v1 - v0), carpet);
            // Cam bölmeler (doğu, güney, kuzey) ve çerçeve
            float doorV = (v0 + v1) / 2f;
            k.Box(Hall(u1, (v0 + doorV - 0.8f) / 2f, F + 1.3f), new Vector3(0.08f, 2.6f, doorV - 0.8f - v0), White, MeshLayer.Glass);
            k.Box(Hall(u1, (doorV + 0.8f + v1) / 2f, F + 1.3f), new Vector3(0.08f, 2.6f, v1 - doorV - 0.8f), White, MeshLayer.Glass);
            k.Box(Hall((u0 + u1) / 2f, v0, F + 1.3f), new Vector3(u1 - u0, 2.6f, 0.08f), White, MeshLayer.Glass);
            k.Box(Hall((u0 + u1) / 2f, v1, F + 1.3f), new Vector3(u1 - u0, 2.6f, 0.08f), White, MeshLayer.Glass);
            k.Box(Hall(u1, (v0 + v1) / 2f, F + 2.65f), new Vector3(0.12f, 0.12f, v1 - v0), gold);
            k.Box(Hall((u0 + u1) / 2f, v0, F + 2.65f), new Vector3(u1 - u0, 0.12f, 0.12f), gold);
            k.Box(Hall((u0 + u1) / 2f, v1, F + 2.65f), new Vector3(u1 - u0, 0.12f, 0.12f), gold);
            // Kanepeler (batı duvarına yaslı, doğuya bakar) ve sehpa
            float sofaLength = (v1 - v0) * 0.7f;
            Vector3 sofaCenter = Hall(u0 + 0.6f, (v0 + v1) / 2f);
            k.Push(sofaCenter, 90f);
            k.Box(new Vector3(0f, 0.25f, 0.1f), new Vector3(sofaLength, 0.5f, 0.85f), sofa);
            k.Box(new Vector3(0f, 0.72f, -0.25f), new Vector3(sofaLength, 0.6f, 0.25f), sofa);
            k.Box(new Vector3(-sofaLength / 2f, 0.45f, 0.05f), new Vector3(0.2f, 0.4f, 0.85f), sofa);
            k.Box(new Vector3(sofaLength / 2f, 0.45f, 0.05f), new Vector3(0.2f, 0.4f, 0.85f), sofa);
            for (int c = 0; c < 3; c++) k.Box(new Vector3(-sofaLength / 3f + c * sofaLength / 3f, 0.55f, 0.1f), new Vector3(sofaLength / 3.3f, 0.12f, 0.75f), new Color(0.3f, 0.32f, 0.42f));
            k.Pop();
            k.Box(Hall(u0 + 2.2f, (v0 + v1) / 2f, F + 0.4f), new Vector3(0.8f, 0.06f, 1.6f), gold);
            k.Box(Hall(u0 + 2.2f, (v0 + v1) / 2f, F + 0.2f), new Vector3(0.6f, 0.4f, 1.3f), Dark);
            k.Frustum(Hall(u1 - 0.7f, v1 - 0.7f), 0.28f, 0.36f, 0.6f, gold, 8);
            k.Sphere(Hall(u1 - 0.7f, v1 - 0.7f, F + 1.15f), 0.5f, Leaf, 8, 5);
            Transform part = End(k, p, "Vip");
            StationSigns.Text(part, "VIP SALON", Hall(u1 + 0.07f, (v0 + v1) / 2f, F + 2.95f) - p, -90f, 0.42f, (v1 - v0) * 0.7f, gold);
            for (int s = 0; s < 3; s++)
                seats.Add(new StationSpot(Hall(u0 + 0.75f, (v0 + v1) / 2f - sofaLength / 3f + s * sofaLength / 3f), Vector3.right));
            spots.Add(new StationSpot(Hall(u0 + 3.2f, (v0 + v1) / 2f + 1f), Vector3.left));
        }

        /// <summary>Giriş meydanında fıskiyeli süs havuzu (su fışkırması canlanır).</summary>
        void BuildFountain()
        {
            Vector3 p = new Vector3((x0 + x1) / 2f - W * 0.25f, 0.06f, (roadN + z0) / 2f + 0.5f);
            Color stone = new Color(0.86f, 0.84f, 0.8f), water = new Color(0.45f, 0.75f, 0.95f);
            MeshKit k = Begin(p);
            k.Cylinder(p, 3.2f, 0.6f, stone, 16);
            k.Cylinder(p + new Vector3(0f, 0.45f, 0f), 2.95f, 0.08f, water, 16);
            k.Cylinder(p, 0.5f, 1.3f, stone, 10);
            k.Frustum(p + new Vector3(0f, 1.3f, 0f), 0.3f, 1.1f, 0.35f, stone, 12, true, false);
            k.Cylinder(p + new Vector3(0f, 1.6f, 0f), 1.0f, 0.04f, water, 12);
            Transform part = End(k, p, "Fountain");
            // Su fışkırmaları (ayrı nesneler: yükseklikleri titreşir)
            var jets = new List<Vector3> { p + new Vector3(0f, 1.62f, 0f) };
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                jets.Add(p + new Vector3(Mathf.Cos(a) * 2.2f, 0.5f, Mathf.Sin(a) * 2.2f));
            }
            for (int i = 0; i < jets.Count; i++)
            {
                MeshKit j = Begin(jets[i]);
                j.Layer = MeshLayer.Glass;
                j.Frustum(jets[i], i == 0 ? 0.22f : 0.12f, 0.03f, i == 0 ? 1.8f : 1.1f, White, 8, false);
                j.Layer = MeshLayer.Lit;
                Transform jet = End(j, jets[i], "Jet", part);
                currentParts.Remove(jet);
                jet.localPosition = jets[i] - p;
                waterJets.Add(jet);
            }
            for (int i = 0; i < 3; i++)
                spots.Add(new StationSpot(p + Quaternion.Euler(0f, i * 120f + 20f, 0f) * new Vector3(0f, 0f, 3.8f), Quaternion.Euler(0f, i * 120f + 20f, 0f) * Vector3.back));
        }

        /// <summary>Saat kulesi: dört yüzde, oyun saatini gösteren akrep ve yelkovanlı saat; turuncu sivri çatı.</summary>
        void BuildClock()
        {
            Vector3 p = new Vector3(x0 + 3f, 0.06f, z1 + 3.6f); // salonun arkasında: kameradan bakınca salonu kapatmaz
            const float faceY = 15.5f;
            MeshKit k = Begin(p);
            k.Box(p + new Vector3(0f, 0.6f, 0f), new Vector3(3.6f, 1.2f, 3.6f), themeDark);
            k.Box(p + new Vector3(0f, 9f, 0f), new Vector3(3f, 18f, 3f), theme);
            for (float y = 3f; y < 13f; y += 3.3f)
                foreach (var side in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                    k.Box(p + new Vector3(0f, y, 0f) + side * 1.52f, side.x != 0f ? new Vector3(0.03f, 1.4f, 0.8f) : new Vector3(0.8f, 1.4f, 0.03f), White, MeshLayer.Window);
            k.Box(p + new Vector3(0f, 18.2f, 0f), new Vector3(3.4f, 0.4f, 3.4f), themeDark);
            k.Frustum(p + new Vector3(0f, 18.4f, 0f), 2.3f, 0f, 3.6f, Orange, 4, false, false, Mathf.PI / 4f);
            k.Cylinder(p + new Vector3(0f, 22f, 0f), 0.06f, 1.2f, Metal, 4);
            Transform part = End(k, p, "ClockTower");
            for (int side = 0; side < 4; side++)
            {
                float yaw = side * 90f;
                Transform face = new GameObject("Face").transform;
                face.SetParent(part, false);
                face.localPosition = new Vector3(0f, faceY, 0f) + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, -1.52f);
                face.localRotation = Quaternion.Euler(0f, yaw, 0f);
                var dial = new MeshKit();
                dial.Push(Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
                dial.Cylinder(new Vector3(0f, -0.04f, 0f), 1.15f, 0.08f, White, 20, true, true);
                dial.Pop();
                for (int h = 0; h < 12; h++)
                {
                    dial.Push(Vector3.zero, Quaternion.Euler(0f, 0f, h * 30f));
                    dial.Box(new Vector3(0f, 0.95f, -0.06f), new Vector3(0.07f, h % 3 == 0 ? 0.22f : 0.12f, 0.02f), Dark);
                    dial.Pop();
                }
                Make(dial, "Dial", face);
                Transform hour = Hand(face, 0.55f, 0.11f, -0.09f);
                Transform minute = Hand(face, 0.9f, 0.07f, -0.11f);
                clockHands.Add((hour, minute));
            }
        }

        Transform Hand(Transform face, float length, float width, float z)
        {
            var pivot = new GameObject("Hand").transform;
            pivot.SetParent(face, false);
            pivot.localPosition = new Vector3(0f, 0f, z);
            var k = new MeshKit();
            k.Box(new Vector3(0f, length / 2f - 0.08f, 0f), new Vector3(width, length, 0.03f), Dark);
            Make(k, "Mesh", pivot, false);
            return pivot;
        }
    }
}
