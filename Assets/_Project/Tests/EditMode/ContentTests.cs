using System.Collections.Generic;
using BusTycoon.Core;
using BusTycoon.Data;
using BusTycoon.World;
using NUnit.Framework;
using UnityEngine;

namespace BusTycoon.Tests
{
    public sealed class ContentTests
    {
        [Test]
        public void DefaultContent_BuildsFromBundledCityCsv()
        {
            var csv = Resources.Load<TextAsset>(DefaultContent.CitiesResourcePath);
            Assert.That(csv, Is.Not.Null, "Resources/BusTycoon/cities.csv bulunamadı");

            GameContent content = DefaultContent.Build(csv.text);
            var errors = new List<string>();
            Assert.That(content.World.Validate(errors), Is.True, string.Join("\n", errors));
            Assert.That(content.World.Cities.Count, Is.EqualTo(93), "81 il + 12 komşu ülke şehri");
            Assert.That(content.Catalog.Models.Count, Is.EqualTo(DefaultContent.Models.Length));
            Assert.That(content.Progression.StartingBus, Is.Not.Null);

            var world = new WorldGraph(content.World);
            Assert.That(world.GetCity("TR_ISTANBUL"), Is.Not.Null);
            Assert.That(world.GetCity("TR_ANKARA").IsCapital, Is.True);

            foreach (var city in content.World.Cities)
            {
                Assert.That(city.Coordinate.longitude, Is.InRange(DefaultContent.MinLongitude, DefaultContent.MaxLongitude), city.Id);
                Assert.That(city.Coordinate.latitude, Is.InRange(DefaultContent.MinLatitude, DefaultContent.MaxLatitude), city.Id);
                Assert.That(city.Population, Is.GreaterThan(0), city.Id);
            }
        }

        [TestCase("İstanbul", "ISTANBUL")]
        [TestCase("Şanlıurfa", "SANLIURFA")]
        [TestCase("Köstence", "KOSTENCE")]
        [TestCase("Afyon Karahisar", "AFYON_KARAHISAR")]
        public void Slug_ProducesStableAsciiIds(string name, string expected)
        {
            Assert.That(CsvUtility.Slug(name), Is.EqualTo(expected));
        }

        [Test]
        public void Csv_HandlesQuotedFieldsAndCrLf()
        {
            var rows = CsvUtility.Parse("city,population\r\n\"Kahramanmaraş, TR\",\"1177436\"\r\nVan,1128749\r\n");
            Assert.That(rows.Count, Is.EqualTo(3));
            Assert.That(rows[1][0], Is.EqualTo("Kahramanmaraş, TR"));
            Assert.That(CsvUtility.ParseLong(rows[2][1]), Is.EqualTo(1128749));
        }
    }
}
