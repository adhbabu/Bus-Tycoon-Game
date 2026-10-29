using System;
using System.Collections.Generic;
using System.Globalization;

namespace BusTycoon.World
{
    /// <summary>
    /// Karayolu ağı: Natural Earth 1:10m yolları (Tools/build_map.py → Resources/BusTycoon/roads.txt).
    /// Hat çizgileri ve otobüsler şehirler arasında bu ağdaki en uygun yolu izler. Görseldir: hat ekonomisi
    /// <see cref="WorldGraph"/>'ın mesafe modelini kullanmaya devam eder.
    /// Şehirler ağın en büyük bağlı parçasındaki en yakın düğüme bağlanır; güzergâh A* ile bulunur.
    /// </summary>
    public sealed class RoadNetwork
    {
        public enum RoadClass { Motorway = 0, MainRoad = 1, MinorRoad = 2, Ferry = 3 }

        public sealed class Edge
        {
            public int A, B;
            public RoadClass Class;
            /// <summary>A'dan B'ye noktalar (uçlar dahil).</summary>
            public GeoCoordinate[] Points;
            public double LengthKm;
            public double Cost;
        }

        // Güzergâh maliyeti: otoyol tercih edilir, feribot ancak gerekirse (Tools/build_map.py ile aynı).
        static readonly double[] ClassCost = { 0.8, 0.95, 1.1, 1.6 };
        const double FerryPenaltyKm = 25;
        static readonly double MinClassCost = 0.8;

        readonly GeoCoordinate[] nodes;
        readonly Edge[] edges;
        readonly List<int>[] adjacency;
        readonly bool[] reachable;

        public IReadOnlyList<GeoCoordinate> Nodes => nodes;
        public IReadOnlyList<Edge> Edges => edges;

        RoadNetwork(GeoCoordinate[] nodes, Edge[] edges)
        {
            this.nodes = nodes;
            this.edges = edges;
            adjacency = new List<int>[nodes.Length];
            for (int i = 0; i < nodes.Length; i++) adjacency[i] = new List<int>(3);
            for (int e = 0; e < edges.Length; e++)
            {
                adjacency[edges[e].A].Add(e);
                adjacency[edges[e].B].Add(e);
            }
            reachable = LargestComponent();
        }

        /// <summary>
        /// Biçim: "N sayı", ardından "boylam enlem" satırları (derece × 10000); "E sayı", ardından
        /// "a b sınıf k  ara noktalar…" satırları. '#' ile başlayan satırlar yorumdur.
        /// </summary>
        public static RoadNetwork Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) throw new ArgumentException("Yol ağı verisi boş.");
            var lines = text.Split('\n');
            int index = 0;

            string NextLine()
            {
                while (index < lines.Length)
                {
                    string line = lines[index++].Trim();
                    if (line.Length > 0 && line[0] != '#') return line;
                }
                throw new FormatException("Yol ağı verisi beklenenden kısa.");
            }

            int ReadCount(string tag)
            {
                string[] header = NextLine().Split(' ');
                if (header.Length != 2 || header[0] != tag) throw new FormatException($"'{tag} sayı' satırı bekleniyordu.");
                return int.Parse(header[1], CultureInfo.InvariantCulture);
            }

            static GeoCoordinate Coordinate(string lon, string lat) =>
                new GeoCoordinate(int.Parse(lat, CultureInfo.InvariantCulture) / 1e4, int.Parse(lon, CultureInfo.InvariantCulture) / 1e4);

            var nodes = new GeoCoordinate[ReadCount("N")];
            for (int i = 0; i < nodes.Length; i++)
            {
                string[] parts = NextLine().Split(' ');
                nodes[i] = Coordinate(parts[0], parts[1]);
            }

            var edges = new Edge[ReadCount("E")];
            for (int e = 0; e < edges.Length; e++)
            {
                string[] parts = NextLine().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                int a = int.Parse(parts[0], CultureInfo.InvariantCulture);
                int b = int.Parse(parts[1], CultureInfo.InvariantCulture);
                int roadClass = int.Parse(parts[2], CultureInfo.InvariantCulture);
                int inner = int.Parse(parts[3], CultureInfo.InvariantCulture);
                if (a < 0 || b < 0 || a >= nodes.Length || b >= nodes.Length || roadClass < 0 || roadClass >= ClassCost.Length || parts.Length != 4 + inner * 2)
                    throw new FormatException($"Hatalı kenar satırı: {e}");

                var points = new GeoCoordinate[inner + 2];
                points[0] = nodes[a];
                for (int k = 0; k < inner; k++) points[k + 1] = Coordinate(parts[4 + 2 * k], parts[5 + 2 * k]);
                points[inner + 1] = nodes[b];

                double length = 0;
                for (int k = 1; k < points.Length; k++) length += GeoCoordinate.HaversineKm(points[k - 1], points[k]);
                edges[e] = new Edge
                {
                    A = a,
                    B = b,
                    Class = (RoadClass)roadClass,
                    Points = points,
                    LengthKm = length,
                    Cost = length * ClassCost[roadClass] + (roadClass == (int)RoadClass.Ferry ? FerryPenaltyKm : 0)
                };
            }
            return new RoadNetwork(nodes, edges);
        }

        /// <summary>Ağın en büyük bağlı parçasındaki en yakın düğüm; ağ boşsa -1.</summary>
        public int FindNearestNode(GeoCoordinate point, out double distanceKm)
        {
            int best = -1;
            double bestScore = double.MaxValue;
            double cosLat = Math.Cos(point.latitude * Math.PI / 180.0);
            for (int i = 0; i < nodes.Length; i++)
            {
                if (!reachable[i]) continue;
                double dLat = nodes[i].latitude - point.latitude;
                double dLon = (nodes[i].longitude - point.longitude) * cosLat;
                double score = dLat * dLat + dLon * dLon;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            distanceKm = best >= 0 ? GeoCoordinate.HaversineKm(point, nodes[best]) : double.MaxValue;
            return best;
        }

        /// <summary>
        /// İki nokta arasındaki yol güzergâhı (başlangıç ve bitiş noktaları dahil). Noktalardan biri ağa
        /// <paramref name="maxSnapKm"/>'den uzaksa ya da yol yoksa false döner.
        /// </summary>
        public bool TryFindPath(GeoCoordinate from, GeoCoordinate to, List<GeoCoordinate> path, double maxSnapKm = 60)
        {
            path.Clear();
            int start = FindNearestNode(from, out double startKm);
            int goal = FindNearestNode(to, out double goalKm);
            if (start < 0 || goal < 0 || startKm > maxSnapKm || goalKm > maxSnapKm) return false;
            if (!Search(start, goal, out int[] viaEdge)) return false;

            var traversed = new List<int>();
            for (int node = goal; node != start;)
            {
                int e = viaEdge[node];
                traversed.Add(e);
                node = edges[e].A == node ? edges[e].B : edges[e].A;
            }

            path.Add(from);
            int current = start;
            AddIfApart(path, nodes[start]);
            for (int i = traversed.Count - 1; i >= 0; i--)
            {
                Edge edge = edges[traversed[i]];
                bool forward = edge.A == current;
                for (int k = 1; k < edge.Points.Length; k++)
                    AddIfApart(path, edge.Points[forward ? k : edge.Points.Length - 1 - k]);
                current = forward ? edge.B : edge.A;
            }
            AddIfApart(path, to);
            return true;
        }

        static void AddIfApart(List<GeoCoordinate> path, GeoCoordinate point)
        {
            if (path.Count > 0)
            {
                GeoCoordinate last = path[path.Count - 1];
                if (Math.Abs(last.latitude - point.latitude) < 1e-6 && Math.Abs(last.longitude - point.longitude) < 1e-6) return;
            }
            path.Add(point);
        }

        public static double PathLengthKm(IReadOnlyList<GeoCoordinate> path)
        {
            double length = 0;
            for (int i = 1; i < path.Count; i++) length += GeoCoordinate.HaversineKm(path[i - 1], path[i]);
            return length;
        }

        /// <summary>A*: sezgisel = kuş uçuşu × en ucuz yol katsayısı (hiçbir zaman fazla tahmin etmez).</summary>
        bool Search(int start, int goal, out int[] viaEdge)
        {
            int count = nodes.Length;
            var best = new double[count];
            viaEdge = new int[count];
            var closed = new bool[count];
            for (int i = 0; i < count; i++)
            {
                best[i] = double.MaxValue;
                viaEdge[i] = -1;
            }

            var open = new MinHeap(64);
            best[start] = 0;
            open.Push(start, Heuristic(start, goal));
            while (open.Count > 0)
            {
                int node = open.Pop();
                if (node == goal) return true;
                if (closed[node]) continue;
                closed[node] = true;

                foreach (int e in adjacency[node])
                {
                    Edge edge = edges[e];
                    int next = edge.A == node ? edge.B : edge.A;
                    double cost = best[node] + edge.Cost;
                    if (cost >= best[next]) continue;
                    best[next] = cost;
                    viaEdge[next] = e;
                    open.Push(next, cost + Heuristic(next, goal));
                }
            }
            return false;
        }

        double Heuristic(int node, int goal) => GeoCoordinate.HaversineKm(nodes[node], nodes[goal]) * MinClassCost;

        bool[] LargestComponent()
        {
            var component = new int[nodes.Length];
            for (int i = 0; i < component.Length; i++) component[i] = -1;
            var sizes = new List<int>();
            var stack = new Stack<int>();
            for (int seed = 0; seed < nodes.Length; seed++)
            {
                if (component[seed] >= 0) continue;
                int id = sizes.Count;
                int size = 0;
                component[seed] = id;
                stack.Push(seed);
                while (stack.Count > 0)
                {
                    int node = stack.Pop();
                    size++;
                    foreach (int e in adjacency[node])
                    {
                        int next = edges[e].A == node ? edges[e].B : edges[e].A;
                        if (component[next] >= 0) continue;
                        component[next] = id;
                        stack.Push(next);
                    }
                }
                sizes.Add(size);
            }

            int largest = 0;
            for (int i = 1; i < sizes.Count; i++)
                if (sizes[i] > sizes[largest]) largest = i;
            var result = new bool[nodes.Length];
            for (int i = 0; i < result.Length; i++) result[i] = component[i] == largest;
            return result;
        }

        /// <summary>Öncelik kuyruğu (ikili yığın); .NET Standard 2.1'de PriorityQueue yok.</summary>
        sealed class MinHeap
        {
            int[] items;
            double[] priorities;
            public int Count { get; private set; }

            public MinHeap(int capacity)
            {
                items = new int[capacity];
                priorities = new double[capacity];
            }

            public void Push(int item, double priority)
            {
                if (Count == items.Length)
                {
                    Array.Resize(ref items, Count * 2);
                    Array.Resize(ref priorities, Count * 2);
                }
                int i = Count++;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (priorities[parent] <= priority) break;
                    items[i] = items[parent];
                    priorities[i] = priorities[parent];
                    i = parent;
                }
                items[i] = item;
                priorities[i] = priority;
            }

            public int Pop()
            {
                int top = items[0];
                int lastItem = items[--Count];
                double lastPriority = priorities[Count];
                int i = 0;
                while (true)
                {
                    int child = i * 2 + 1;
                    if (child >= Count) break;
                    if (child + 1 < Count && priorities[child + 1] < priorities[child]) child++;
                    if (priorities[child] >= lastPriority) break;
                    items[i] = items[child];
                    priorities[i] = priorities[child];
                    i = child;
                }
                if (Count > 0)
                {
                    items[i] = lastItem;
                    priorities[i] = lastPriority;
                }
                return top;
            }
        }
    }
}
