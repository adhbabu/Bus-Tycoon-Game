using System;
using System.Collections.Generic;
using BusTycoon.Data;
using BusTycoon.World;
using NUnit.Framework;
using UnityEngine;

namespace BusTycoon.Tests
{
    public sealed class RoadNetworkTests
    {
        // Kare: 0(0,0) 1(1,0) 2(1,1) 3(0,1) — alttan ve sağdan diğer yol, soldan ve üstten otoyol.
        const string Square = @"# deneme
N 4
0 0
10000 0
10000 10000
0 10000
E 4
0 1 2 0
1 2 2 0
0 3 0 0
3 2 0 1 5000 10001
";

        static GeoCoordinate Geo(double lon, double lat) => new GeoCoordinate(lat, lon);

        [Test]
        public void Path_PrefersMotorway_AndIncludesEndpoints()
        {
            var network = RoadNetwork.Parse(Square);
            var path = new List<GeoCoordinate>();

            Assert.That(network.TryFindPath(Geo(0, 0), Geo(1, 1), path), Is.True);
            Assert.That(path[0], Is.EqualTo(Geo(0, 0)));
            Assert.That(path[path.Count - 1], Is.EqualTo(Geo(1, 1)));
            Assert.That(path.Exists(p => Math.Abs(p.longitude) < 1e-6 && Math.Abs(p.latitude - 1) < 1e-6), Is.True, "otoyol köşesinden (0,1) geçmeli");
            Assert.That(path.Exists(p => Math.Abs(p.longitude - 0.5) < 1e-6), Is.True, "ara nokta (0.5,1) korunmalı");
        }

        [Test]
        public void Path_IsReversible()
        {
            var network = RoadNetwork.Parse(Square);
            var forward = new List<GeoCoordinate>();
            var backward = new List<GeoCoordinate>();
            network.TryFindPath(Geo(0, 0), Geo(1, 1), forward);
            network.TryFindPath(Geo(1, 1), Geo(0, 0), backward);
            backward.Reverse();
            Assert.That(backward, Is.EqualTo(forward));
        }

        [Test]
        public void Ferry_IsUsedOnlyWhenWorthIt()
        {
            // 0—1 arasında uzun kara yolu (0→2→1) ve kısa feribot (0→1).
            const string text = "N 3\n0 0\n1000 0\n500 50000\nE 3\n0 2 1 0\n2 1 1 0\n0 1 3 0\n";
            var network = RoadNetwork.Parse(text);
            var path = new List<GeoCoordinate>();
            Assert.That(network.TryFindPath(Geo(0, 0), Geo(0.1, 0), path), Is.True);
            Assert.That(path.Count, Is.EqualTo(2), "10 km'lik feribot, 1000 km'lik kara yolundan iyi");

            const string shortRoad = "N 3\n0 0\n1000 0\n500 100\nE 3\n0 2 1 0\n2 1 1 0\n0 1 3 0\n";
            network = RoadNetwork.Parse(shortRoad);
            network.TryFindPath(Geo(0, 0), Geo(0.1, 0), path);
            Assert.That(path.Exists(p => Math.Abs(p.latitude - 0.01) < 1e-6), Is.True, "neredeyse eşit uzunlukta kara yolu feribota tercih edilir");
        }

        [Test]
        public void FarAwayPoint_HasNoPath()
        {
            var network = RoadNetwork.Parse(Square);
            var path = new List<GeoCoordinate>();
            Assert.That(network.TryFindPath(Geo(0, 0), Geo(5, 5), path, maxSnapKm: 60), Is.False);
            Assert.That(path, Is.Empty);
        }

        [Test]
        public void SmallComponents_AreIgnoredWhenSnapping()
        {
            // 0—1 ana parça (2 düğüm + 1 kenar), 2—3 de 2 düğüm; eşitlikte ilk parça seçilir. Ayrık 4. düğüm hiç seçilmez.
            const string text = "N 5\n0 0\n10000 0\n0 10000\n10000 10000\n5000 5000\nE 3\n0 1 1 0\n2 3 1 0\n1 3 1 0\n";
            var network = RoadNetwork.Parse(text);
            int nearest = network.FindNearestNode(Geo(0.5, 0.5), out _);
            Assert.That(nearest, Is.Not.EqualTo(4), "bağlantısız düğüme yapışılmamalı");
        }

        [TestCase("")]
        [TestCase("N 2\n0 0\n")]
        [TestCase("N 1\n0 0\nE 1\n0 5 0 0\n")]
        [TestCase("N 2\n0 0\n1 1\nE 1\n0 1 0 2 5 5\n")]
        public void MalformedData_Throws(string text)
        {
            Assert.That(() => RoadNetwork.Parse(text), Throws.InstanceOf<Exception>());
        }

        [Test]
        public void BundledNetwork_ConnectsEveryCityToIstanbulReasonably()
        {
            var csv = Resources.Load<TextAsset>(DefaultContent.CitiesResourcePath);
            var roads = Resources.Load<TextAsset>(DefaultContent.RoadsResourcePath);
            Assert.That(roads, Is.Not.Null, "Resources/BusTycoon/roads.txt bulunamadı");

            GameContent content = DefaultContent.Build(csv.text);
            var network = RoadNetwork.Parse(roads.text);
            var world = new WorldGraph(content.World);
            CityData istanbul = world.GetCity("TR_ISTANBUL");
            var path = new List<GeoCoordinate>();

            foreach (var city in content.World.Cities)
            {
                if (city == istanbul) continue;
                Assert.That(network.TryFindPath(istanbul.Coordinate, city.Coordinate, path), Is.True, city.Id);
                double straight = GeoCoordinate.HaversineKm(istanbul.Coordinate, city.Coordinate);
                double road = RoadNetwork.PathLengthKm(path);
                Assert.That(road, Is.InRange(straight, straight * 2.6), $"{city.Id}: kuş uçuşu {straight:0} km, yol {road:0} km");
            }
        }
    }
}
