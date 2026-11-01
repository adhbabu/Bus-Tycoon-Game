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
            Assert.That(content.World.Cities.Count, Is.EqualTo(221), "81 il + 12 komşu ülke şehri + 128 Avrupa Paketi şehri");
            Assert.That(content.Catalog.Models.Count, Is.EqualTo(DefaultContent.Models.Length + DefaultContent.EuropeModels.Length));
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

        [Test]
        public void EuropePack_EveryRegionHasEconomyAndThePackHas31Countries()
        {
            var csv = Resources.Load<TextAsset>(DefaultContent.CitiesResourcePath);
            GameContent content = DefaultContent.Build(csv.text);
            int pack = 0;
            foreach (var region in content.World.Regions)
            {
                Assert.That(DefaultContent.TryGetRegionSpec(region.Id, out var spec), Is.True, $"{region.Id}: DefaultContent.Regions'ta yok");
                Assert.That(spec.Fare, Is.GreaterThan(0f), region.Id);
                if (region.PackId == ContentPacks.Europe)
                {
                    pack++;
                    Assert.That(region.RequiredCompanyLevel, Is.InRange(8, 20), region.Id);
                    Assert.That(region.OperatingLicenseCost, Is.GreaterThan(0f), region.Id);
                }
                else
                {
                    Assert.That(region.PackId, Is.Empty, region.Id);
                }
            }
            Assert.That(pack, Is.EqualTo(31));
            Assert.That(content.World.Regions.Count, Is.EqualTo(36));
        }

        [Test]
        public void EuropePack_LicenseNeedsOwnership()
        {
            var csv = Resources.Load<TextAsset>(DefaultContent.CitiesResourcePath);
            GameContent content = DefaultContent.Build(csv.text);
            RegionData germany = null;
            foreach (var region in content.World.Regions) if (region.Id == "DE") germany = region;
            Assert.That(germany, Is.Not.Null);

            var state = new BusTycoon.Save.CompanyState { cash = 10_000_000, level = 30 };
            var finance = new BusTycoon.Economy.FinanceSystem(state);
            var progression = new BusTycoon.Progression.ProgressionSystem(state, content.Progression, content.Catalog, content.Economy, finance);
            try
            {
                Entitlements.Revoke(ContentPacks.Europe);
                Assert.That(progression.CanUnlockRegion(germany).Success, Is.False, "paket yokken lisans alınamamalı");
                Assert.That(progression.TryUnlockRegion(germany).Success, Is.False);
                Assert.That(state.cash, Is.EqualTo(10_000_000).Within(0.01));

                Entitlements.Grant(ContentPacks.Europe);
                Assert.That(progression.TryUnlockRegion(germany).Success, Is.True);
                Assert.That(progression.IsRegionUnlocked(germany), Is.True);

                Entitlements.Revoke(ContentPacks.Europe);
                Assert.That(progression.IsRegionUnlocked(germany), Is.False, "paket gidince bölge kapanır");
            }
            finally
            {
                Entitlements.Grant(ContentPacks.Europe);
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
