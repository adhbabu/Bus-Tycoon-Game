using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BusTycoon.Game
{
    /// <summary>
    /// Duraktaki insanlar: büyük kafalı, oyuncak gibi sevimli figürler (ayakta, oturan, sırt çantalı, bavullu ve
    /// görevliler). Yürüyenler durağın yol ağında (<see cref="StationGraph"/>) dolaşır, bekleyenler peron ve
    /// gişelerde sallanır, oturanlar koltuklardadır. Modeller bir kez üretilip paylaşılır. Yerel ileri yön +z.
    /// </summary>
    public sealed class StationCrowd
    {
        public enum Role { Traveller, Attendant, Barista, Vendor }

        sealed class Person
        {
            public Transform Transform;
            public bool Walking, Seated;
            public Vector3 Position, Facing;
            public int From, To, Previous;
            public float Speed, Wait, Phase;
        }

        static readonly Color[] Shirts =
        {
            new Color(1f, 0.57f, 0f), new Color(0.2f, 0.7f, 0.68f), new Color(0.9f, 0.3f, 0.32f), new Color(1f, 0.82f, 0.25f),
            new Color(0.3f, 0.5f, 0.95f), new Color(0.6f, 0.42f, 0.86f), new Color(0.38f, 0.72f, 0.36f), new Color(0.98f, 0.6f, 0.72f),
            new Color(0.95f, 0.95f, 0.93f), new Color(0.2f, 0.26f, 0.42f)
        };
        static readonly Color[] Pants = { new Color(0.24f, 0.36f, 0.58f), new Color(0.25f, 0.26f, 0.29f), new Color(0.72f, 0.64f, 0.48f), new Color(0.14f, 0.14f, 0.16f), new Color(0.45f, 0.32f, 0.22f) };
        static readonly Color[] Skins = { new Color(1f, 0.85f, 0.72f), new Color(0.94f, 0.75f, 0.58f), new Color(0.8f, 0.6f, 0.44f), new Color(0.58f, 0.4f, 0.28f) };
        static readonly Color[] Hairs = { new Color(0.12f, 0.1f, 0.09f), new Color(0.3f, 0.2f, 0.12f), new Color(0.52f, 0.34f, 0.18f), new Color(0.92f, 0.78f, 0.45f), new Color(0.72f, 0.72f, 0.74f) };
        static readonly Color Shoe = new Color(0.16f, 0.16f, 0.18f);
        static readonly Color Bag = new Color(0.3f, 0.36f, 0.5f);

        const int StandingVariants = 18, SeatedVariants = 12;
        static Mesh[] standing, seated;
        static readonly Dictionary<Role, Mesh> staff = new Dictionary<Role, Mesh>();
        static MeshLayer[] layers = { MeshLayer.Lit };

        readonly Transform root;
        readonly StationGraph graph;
        readonly System.Random rng;
        readonly List<Person> people = new List<Person>();

        public StationCrowd(Transform parent, StationGraph graph, int seed)
        {
            root = new GameObject("People").transform;
            root.SetParent(parent, false);
            this.graph = graph;
            rng = new System.Random(seed);
            EnsureMeshes();
        }

        public int Count => people.Count;

        /// <summary>
        /// Kalabalığı kurar: oturanlar koltuklara, bekleyenler duraklama noktalarına, kalanı yürüyerek dolaşır.
        /// </summary>
        public void Populate(int total, IList<StationSpot> seats, IList<StationSpot> spots)
        {
            Clear();
            if (total <= 0) return;
            var freeSeats = new List<StationSpot>(seats);
            var freeSpots = new List<StationSpot>(spots);
            Shuffle(freeSeats);
            Shuffle(freeSpots);

            int seatedCount = Mathf.Min(freeSeats.Count, Mathf.RoundToInt(total * 0.35f));
            int standingCount = Mathf.Min(freeSpots.Count, Mathf.RoundToInt(total * 0.3f));
            for (int i = 0; i < seatedCount; i++) AddSeated(freeSeats[i]);
            for (int i = 0; i < standingCount; i++) AddStanding(freeSpots[i], Role.Traveller);
            int walkers = total - seatedCount - standingCount;
            for (int i = 0; i < walkers && graph.Count >= 2; i++) AddWalker();
        }

        /// <summary>Gişe, kafe ya da büfede çalışan görevli (sabit durur).</summary>
        public void AddStaff(StationSpot spot, Role role) => AddStanding(spot, role);

        public void Clear()
        {
            foreach (var person in people) Object.Destroy(person.Transform.gameObject);
            people.Clear();
        }

        void AddSeated(StationSpot seat)
        {
            var person = Create(seated[rng.Next(seated.Length)], "Seated");
            person.Seated = true;
            person.Position = seat.Position;
            person.Facing = seat.Facing;
            person.Phase = (float)rng.NextDouble() * 10f;
            Place(person, 0f);
        }

        void AddStanding(StationSpot spot, Role role)
        {
            Mesh mesh = role == Role.Traveller ? standing[rng.Next(standing.Length)] : staff[role];
            var person = Create(mesh, role == Role.Traveller ? "Waiting" : role.ToString());
            person.Position = spot.Position + new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f) * (role == Role.Traveller ? 0.5f : 0f);
            person.Facing = spot.Facing;
            person.Phase = (float)rng.NextDouble() * 10f;
            person.Transform.localScale = Vector3.one * spot.Scale;
            Place(person, 0f);
        }

        void AddWalker()
        {
            var person = Create(standing[rng.Next(standing.Length)], "Walking");
            person.Walking = true;
            person.From = rng.Next(graph.Count);
            person.Previous = -1;
            person.To = graph.RandomNeighbor(person.From, -1, rng);
            float t = (float)rng.NextDouble();
            person.Position = Vector3.Lerp(graph.Position(person.From), graph.Position(person.To), t);
            person.Speed = 1.1f + (float)rng.NextDouble() * 0.5f;
            person.Phase = (float)rng.NextDouble() * 10f;
            person.Facing = graph.Position(person.To) - person.Position;
            Place(person, 0f);
        }

        Person Create(Mesh mesh, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = StationMaterials.For(layers, null);
            renderer.shadowCastingMode = ShadowCastingMode.On;
            var person = new Person { Transform = go.transform };
            people.Add(person);
            return person;
        }

        /// <summary>Kare güncellemesi: <paramref name="dt"/> oyun hızıyla ölçekli süre (oyun duraklayınca 0).</summary>
        public void Update(float dt)
        {
            foreach (var person in people)
            {
                person.Phase += dt;
                if (person.Walking) Walk(person, dt);
                float bob = person.Walking && person.Wait <= 0f ? Mathf.Abs(Mathf.Sin(person.Phase * 9f)) * 0.06f : 0f;
                Place(person, bob);
            }
        }

        void Walk(Person person, float dt)
        {
            if (person.Wait > 0f)
            {
                person.Wait -= dt;
                return;
            }
            Vector3 target = graph.Position(person.To);
            Vector3 delta = target - person.Position;
            delta.y = 0f;
            float step = person.Speed * dt;
            if (delta.magnitude <= step)
            {
                person.Position = target;
                person.Previous = person.From;
                person.From = person.To;
                person.To = graph.RandomNeighbor(person.From, person.Previous, rng);
                if (graph.IsStop(person.From) && rng.NextDouble() < 0.45) person.Wait = 1.5f + (float)rng.NextDouble() * 5f;
                return;
            }
            person.Facing = delta;
            person.Position += delta.normalized * step;
        }

        void Place(Person person, float bob)
        {
            Vector3 facing = person.Facing;
            facing.y = 0f;
            Quaternion rotation = facing.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(facing, Vector3.up) : Quaternion.identity;
            // Yürürken hafif sağa sola yalpalama, beklerken çok hafif sallanma: oyuncak figür hissi.
            float sway = person.Seated ? 0f : person.Walking && person.Wait <= 0f ? Mathf.Sin(person.Phase * 9f) * 6f : Mathf.Sin(person.Phase * 1.3f) * 2f;
            person.Transform.localPosition = person.Position + Vector3.up * bob;
            person.Transform.localRotation = rotation * Quaternion.Euler(0f, 0f, sway);
        }

        void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // ------------------------------------------------------------------ modeller

        static void EnsureMeshes()
        {
            if (standing != null) return;
            var random = new System.Random(1923);
            standing = new Mesh[StandingVariants];
            for (int i = 0; i < StandingVariants; i++)
            {
                var look = Look.Random(random);
                int extra = i % 6 == 4 ? 1 : i % 6 == 5 ? 2 : 0; // sırt çantası / bavul
                standing[i] = BuildStanding(look, extra, "Person_" + i);
            }
            seated = new Mesh[SeatedVariants];
            for (int i = 0; i < SeatedVariants; i++) seated[i] = BuildSeated(Look.Random(random), "Seated_" + i);

            staff[Role.Traveller] = standing[0];
            staff[Role.Attendant] = BuildStanding(new Look(new Color(1f, 0.57f, 0f), Pants[1], Skins[1], Hairs[0], new Color(0.2f, 0.26f, 0.42f)), 0, "Attendant");
            staff[Role.Barista] = BuildStanding(new Look(new Color(0.95f, 0.95f, 0.93f), Pants[3], Skins[2], Hairs[1], new Color(0.45f, 0.3f, 0.2f)), 0, "Barista");
            staff[Role.Vendor] = BuildStanding(new Look(new Color(0.95f, 0.95f, 0.93f), Pants[1], Skins[0], Hairs[0], new Color(0.86f, 0.18f, 0.2f)), 0, "Vendor");
        }

        readonly struct Look
        {
            public readonly Color Shirt, Pants, Skin, Hair;
            /// <summary>Önlük/yelek rengi (görevliler); saydam ise yok.</summary>
            public readonly Color Apron;

            public Look(Color shirt, Color pants, Color skin, Color hair, Color apron)
            {
                Shirt = shirt;
                Pants = pants;
                Skin = skin;
                Hair = hair;
                Apron = apron;
            }

            public static Look Random(System.Random r) =>
                new Look(Shirts[r.Next(Shirts.Length)], StationCrowd.Pants[r.Next(StationCrowd.Pants.Length)], Skins[r.Next(Skins.Length)], Hairs[r.Next(Hairs.Length)], Color.clear);
        }

        static Mesh BuildStanding(Look look, int extra, string name)
        {
            var k = new MeshKit();
            foreach (float x in new[] { -0.1f, 0.1f })
            {
                k.Box(new Vector3(x, 0.05f, 0.03f), new Vector3(0.15f, 0.1f, 0.24f), Shoe);
                k.Box(new Vector3(x, 0.38f, 0f), new Vector3(0.15f, 0.58f, 0.17f), look.Pants);
            }
            Torso(k, look, 0.66f, 0.5f);
            foreach (float x in new[] { -0.27f, 0.27f })
            {
                k.Box(new Vector3(x, 0.92f, 0f), new Vector3(0.1f, 0.42f, 0.12f), look.Shirt);
                k.Box(new Vector3(x, 0.67f, 0f), new Vector3(0.09f, 0.09f, 0.1f), look.Skin);
            }
            Head(k, look, 1.38f);
            if (extra == 1) k.Box(new Vector3(0f, 0.95f, -0.19f), new Vector3(0.32f, 0.36f, 0.14f), Bag);
            if (extra == 2)
            {
                k.Box(new Vector3(0.42f, 0.27f, 0.05f), new Vector3(0.16f, 0.46f, 0.34f), look.Shirt * 0.75f + Color.black * 0.25f);
                k.Box(new Vector3(0.42f, 0.55f, 0.05f), new Vector3(0.03f, 0.12f, 0.1f), Shoe);
            }
            return k.Build(name, out layers);
        }

        static Mesh BuildSeated(Look look, string name)
        {
            var k = new MeshKit();
            foreach (float x in new[] { -0.1f, 0.1f })
            {
                k.Box(new Vector3(x, 0.5f, 0.18f), new Vector3(0.15f, 0.15f, 0.44f), look.Pants);    // uyluk
                k.Box(new Vector3(x, 0.26f, 0.38f), new Vector3(0.14f, 0.46f, 0.15f), look.Pants);   // baldır
                k.Box(new Vector3(x, 0.05f, 0.43f), new Vector3(0.15f, 0.1f, 0.24f), Shoe);
            }
            Torso(k, look, 0.53f, 0.5f);
            foreach (float x in new[] { -0.27f, 0.27f })
            {
                k.Box(new Vector3(x, 0.79f, 0.06f), new Vector3(0.1f, 0.38f, 0.12f), look.Shirt);
                k.Box(new Vector3(x, 0.6f, 0.14f), new Vector3(0.09f, 0.09f, 0.1f), look.Skin);
            }
            Head(k, look, 1.25f);
            return k.Build(name, out layers);
        }

        static void Torso(MeshKit k, Look look, float bottom, float height)
        {
            k.Box(new Vector3(0f, bottom + height / 2f, 0f), new Vector3(0.44f, height, 0.26f), look.Shirt);
            if (look.Apron.a > 0f) k.Box(new Vector3(0f, bottom + height * 0.45f, 0.135f), new Vector3(0.34f, height * 0.85f, 0.02f), look.Apron);
            k.Box(new Vector3(0f, bottom + height + 0.04f, 0f), new Vector3(0.12f, 0.08f, 0.12f), look.Skin);
        }

        /// <summary>Büyük, yuvarlak kafa: saç, iki göz ve küçük bir gülümseme (sevimli oyuncak figür).</summary>
        static void Head(MeshKit k, Look look, float bottom)
        {
            var center = new Vector3(0f, bottom + 0.21f, 0f);
            k.Ellipsoid(center, new Vector3(0.21f, 0.21f, 0.2f), look.Skin, 8, 5);
            k.Ellipsoid(center + new Vector3(0f, 0.06f, -0.03f), new Vector3(0.225f, 0.18f, 0.2f), look.Hair, 8, 4);
            var eye = new Color(0.1f, 0.1f, 0.12f);
            k.Box(center + new Vector3(-0.075f, 0.01f, 0.195f), new Vector3(0.04f, 0.05f, 0.02f), eye);
            k.Box(center + new Vector3(0.075f, 0.01f, 0.195f), new Vector3(0.04f, 0.05f, 0.02f), eye);
            k.Box(center + new Vector3(0f, -0.07f, 0.19f), new Vector3(0.07f, 0.015f, 0.02f), new Color(0.75f, 0.35f, 0.32f));
        }
    }

    /// <summary>Oturma/bekleme noktası: yerel konum, bakış yönü ve boy (çocuk oyun alanında küçük).</summary>
    public readonly struct StationSpot
    {
        public readonly Vector3 Position, Facing;
        public readonly float Scale;

        public StationSpot(Vector3 position, Vector3 facing, float scale = 1f)
        {
            Position = position;
            Facing = facing;
            Scale = scale;
        }
    }

    /// <summary>Yürüyen yolcuların dolaştığı yol ağı: düğümler ve aralarındaki doğru parçaları.</summary>
    public sealed class StationGraph
    {
        readonly List<Vector3> nodes = new List<Vector3>();
        readonly List<bool> stops = new List<bool>();
        readonly List<List<int>> links = new List<List<int>>();

        public int Count => nodes.Count;
        public Vector3 Position(int node) => nodes[node];
        public bool IsStop(int node) => stops[node];

        /// <summary>Düğüm ekler; <paramref name="stop"/> ise yolcular burada bir süre durabilir.</summary>
        public int Add(Vector3 position, bool stop = false)
        {
            nodes.Add(position);
            stops.Add(stop);
            links.Add(new List<int>());
            return nodes.Count - 1;
        }

        public void Link(int a, int b)
        {
            if (a < 0 || b < 0 || a == b || links[a].Contains(b)) return;
            links[a].Add(b);
            links[b].Add(a);
        }

        public int RandomNeighbor(int node, int previous, System.Random rng)
        {
            List<int> options = links[node];
            if (options.Count == 0) return node;
            if (options.Count == 1) return options[0];
            int pick;
            do pick = options[rng.Next(options.Count)];
            while (pick == previous);
            return pick;
        }
    }
}
