using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace BusTycoon.Urban
{
    /// <summary>Ünlü mekânın türü (etkinlik günleri ve haritadaki simge buna göre).</summary>
    public enum LandmarkKind : byte { Palace, Mosque, Stadium, Museum, Monument, Bazaar, University, Park, Church }

    /// <summary>Şehir içi durak: mahalle ya da ilçe merkezi, yol ağındaki en yakın düğüme bağlı.</summary>
    public sealed class UrbanStop
    {
        public int Index;
        public string Name;
        /// <summary>Konum (metre; x doğu, y kuzey; şehir merkezi 0,0).</summary>
        public Vector2 Position;
        /// <summary>Önem (0..1): ilçe merkezi 1'e yakın, küçük mahalle düşük. Yolcu talebi buna göre.</summary>
        public float Weight;
        public int Node;
    }

    public sealed class UrbanLandmark
    {
        public string Name;
        public LandmarkKind Kind;
        public Vector2 Position;
        /// <summary>Ün (0..1): yakındaki duraklara gelen yolcu ve etkinlik günlerindeki akın buna göre.</summary>
        public float Fame;
    }

    /// <summary>
    /// Şehir içi harita verisi (Tools/build_city.py → StreamingAssets/Cities/&lt;kimlik&gt;/city.bytes): duraklar, ünlü
    /// mekânlar ve yol ağı. Yol ağında iki düğüm arası en kısa yol A* ile bulunur (ana yollar biraz yeğlenir:
    /// dolmuşlar da caddeden gider). Saf C#: oyun kurallarıyla birlikte test edilir.
    /// </summary>
    public sealed class UrbanMap
    {
        public const int FormatVersion = 1;

        public string Id { get; private set; }
        public string Name { get; private set; }
        public double Latitude { get; private set; }
        public double Longitude { get; private set; }
        /// <summary>Haritanın (map.jpg) kapladığı alan, metre.</summary>
        public Rect Bounds { get; private set; }
        public IReadOnlyList<UrbanStop> Stops => stops;
        public IReadOnlyList<UrbanLandmark> Landmarks => landmarks;
        public int NodeCount => nodeX.Length;

        readonly List<UrbanStop> stops = new List<UrbanStop>();
        readonly List<UrbanLandmark> landmarks = new List<UrbanLandmark>();
        float[] nodeX, nodeZ;
        // Komşuluk (CSR): düğüm i'nin kenarları adjStart[i] .. adjStart[i+1]
        int[] adjStart, adjTarget;
        float[] adjCost;

        // A* çalışma alanı (tekrar kullanılır: her aramada ayırma yok)
        float[] gScore;
        int[] cameFrom, visitStamp;
        int stamp;
        readonly List<(float f, int node)> heap = new List<(float, int)>();

        /// <summary>Yol sınıfının maliyet çarpanı: ana yol 0,85 · ara yol 1 · mahalle yolu 1,2.</summary>
        static readonly float[] ClassCost = { 0.85f, 1f, 1.2f };

        public static UrbanMap Read(string id, byte[] data)
        {
            if (data == null || data.Length < 6) throw new InvalidDataException($"Şehir verisi boş: {id}");
            var map = new UrbanMap { Id = id };
            using (var reader = new BinaryReader(new MemoryStream(data), Encoding.UTF8))
            {
                if (reader.ReadByte() != 'B' || reader.ReadByte() != 'T' || reader.ReadByte() != 'C' || reader.ReadByte() != 'Y')
                    throw new InvalidDataException($"Şehir verisi tanınmadı: {id}");
                int version = reader.ReadUInt16();
                if (version != FormatVersion) throw new InvalidDataException($"Şehir verisi sürümü {version} (beklenen {FormatVersion}): {id}");
                map.Name = ReadString(reader);
                map.Latitude = reader.ReadDouble();
                map.Longitude = reader.ReadDouble();
                float x0 = reader.ReadSingle(), z0 = reader.ReadSingle(), x1 = reader.ReadSingle(), z1 = reader.ReadSingle();
                map.Bounds = Rect.MinMaxRect(x0, z0, x1, z1);

                int stopCount = reader.ReadUInt16();
                for (int i = 0; i < stopCount; i++)
                {
                    var stop = new UrbanStop { Index = i, Name = ReadString(reader) };
                    stop.Position = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                    stop.Weight = reader.ReadSingle();
                    stop.Node = reader.ReadInt32();
                    map.stops.Add(stop);
                }
                int landmarkCount = reader.ReadUInt16();
                for (int i = 0; i < landmarkCount; i++)
                {
                    var landmark = new UrbanLandmark { Name = ReadString(reader), Kind = (LandmarkKind)reader.ReadByte() };
                    landmark.Position = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                    landmark.Fame = reader.ReadSingle();
                    map.landmarks.Add(landmark);
                }

                int nodeCount = reader.ReadInt32();
                map.nodeX = new float[nodeCount];
                map.nodeZ = new float[nodeCount];
                for (int i = 0; i < nodeCount; i++)
                {
                    map.nodeX[i] = reader.ReadSingle();
                    map.nodeZ[i] = reader.ReadSingle();
                }
                int edgeCount = reader.ReadInt32();
                var ea = new int[edgeCount];
                var eb = new int[edgeCount];
                var ec = new byte[edgeCount];
                var degree = new int[nodeCount + 1];
                for (int i = 0; i < edgeCount; i++)
                {
                    ea[i] = reader.ReadInt32();
                    eb[i] = reader.ReadInt32();
                    ec[i] = reader.ReadByte();
                    if ((uint)ea[i] >= nodeCount || (uint)eb[i] >= nodeCount) throw new InvalidDataException($"Şehir verisinde geçersiz kenar: {id}");
                    degree[ea[i]]++;
                    degree[eb[i]]++;
                }
                map.BuildAdjacency(ea, eb, ec, degree);
            }
            foreach (var stop in map.stops)
                if ((uint)stop.Node >= map.NodeCount) throw new InvalidDataException($"Durak yol ağına bağlı değil: {stop.Name}");
            return map;
        }

        static string ReadString(BinaryReader reader)
        {
            int length = reader.ReadUInt16();
            return Encoding.UTF8.GetString(reader.ReadBytes(length));
        }

        void BuildAdjacency(int[] ea, int[] eb, byte[] ec, int[] degree)
        {
            int n = nodeX.Length;
            adjStart = new int[n + 1];
            for (int i = 0; i < n; i++) adjStart[i + 1] = adjStart[i] + degree[i];
            adjTarget = new int[adjStart[n]];
            adjCost = new float[adjStart[n]];
            var fill = new int[n];
            for (int i = 0; i < ea.Length; i++)
            {
                float length = Distance(ea[i], eb[i]);
                float cost = length * ClassCost[Mathf.Clamp(ec[i], 0, ClassCost.Length - 1)];
                Add(ea[i], eb[i], cost);
                Add(eb[i], ea[i], cost);
            }

            void Add(int from, int to, float cost)
            {
                int slot = adjStart[from] + fill[from]++;
                adjTarget[slot] = to;
                adjCost[slot] = cost;
            }
        }

        public Vector2 NodePosition(int node) => new Vector2(nodeX[node], nodeZ[node]);

        float Distance(int a, int b)
        {
            float dx = nodeX[a] - nodeX[b], dz = nodeZ[a] - nodeZ[b];
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// İki düğüm arası en kısa yol (A*). Bulunursa düğümler <paramref name="path"/>'e (başlangıçtan varışa) yazılır,
        /// <paramref name="meters"/> gerçek uzunluktur.
        /// </summary>
        public bool FindPath(int from, int to, List<int> path, out float meters)
        {
            path.Clear();
            meters = 0f;
            int n = NodeCount;
            if ((uint)from >= n || (uint)to >= n) return false;
            if (gScore == null)
            {
                gScore = new float[n];
                cameFrom = new int[n];
                visitStamp = new int[n];
            }
            if (++stamp == int.MaxValue)
            {
                Array.Clear(visitStamp, 0, n);
                stamp = 1;
            }
            heap.Clear();
            Touch(from);
            gScore[from] = 0f;
            Push(Heuristic(from, to), from);
            bool found = false;
            while (heap.Count > 0)
            {
                var (f, node) = Pop();
                if (node == to)
                {
                    found = true;
                    break;
                }
                if (f - Heuristic(node, to) > gScore[node] + 1e-3f) continue; // eskimiş kayıt
                for (int e = adjStart[node]; e < adjStart[node + 1]; e++)
                {
                    int next = adjTarget[e];
                    float g = gScore[node] + adjCost[e];
                    if (visitStamp[next] == stamp && g >= gScore[next]) continue;
                    Touch(next);
                    gScore[next] = g;
                    cameFrom[next] = node;
                    Push(g + Heuristic(next, to), next);
                }
            }
            if (!found) return false;
            for (int node = to; ; node = cameFrom[node])
            {
                path.Add(node);
                if (node == from) break;
            }
            path.Reverse();
            for (int i = 1; i < path.Count; i++) meters += Distance(path[i - 1], path[i]);
            return true;
        }

        void Touch(int node)
        {
            if (visitStamp[node] == stamp) return;
            visitStamp[node] = stamp;
            gScore[node] = float.MaxValue;
            cameFrom[node] = -1;
        }

        /// <summary>Kuş uçuşu × en ucuz yol çarpanı: hiçbir zaman fazla tahmin etmez (A* en kısayı bulur).</summary>
        float Heuristic(int a, int b) => Distance(a, b) * ClassCost[0];

        void Push(float f, int node)
        {
            heap.Add((f, node));
            int i = heap.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (heap[parent].f <= heap[i].f) break;
                (heap[parent], heap[i]) = (heap[i], heap[parent]);
                i = parent;
            }
        }

        (float f, int node) Pop()
        {
            var top = heap[0];
            int last = heap.Count - 1;
            heap[0] = heap[last];
            heap.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int left = 2 * i + 1, right = left + 1, smallest = i;
                if (left < heap.Count && heap[left].f < heap[smallest].f) smallest = left;
                if (right < heap.Count && heap[right].f < heap[smallest].f) smallest = right;
                if (smallest == i) break;
                (heap[smallest], heap[i]) = (heap[i], heap[smallest]);
                i = smallest;
            }
            return top;
        }
    }
}
