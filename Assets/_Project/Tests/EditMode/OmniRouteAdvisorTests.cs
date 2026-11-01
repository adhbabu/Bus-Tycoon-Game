using System.Collections.Generic;
using BusTycoon.AI;
using BusTycoon.Economy;
using BusTycoon.Fleet;
using BusTycoon.Routes;
using BusTycoon.Save;
using NUnit.Framework;

namespace BusTycoon.Tests
{
    public sealed class OmniRouteAdvisorTests
    {
        [TestCase("localhost:20128", "http://localhost:20128/v1")]
        [TestCase(" http://localhost:20128/v1/ ", "http://localhost:20128/v1")]
        [TestCase("http://127.0.0.1:20128", "http://127.0.0.1:20128/v1")]
        [TestCase("https://gw.example.com/v1/chat/completions", "https://gw.example.com/v1")]
        [TestCase("", OmniRouteConfig.DefaultBaseUrl)]
        public void NormalizeBaseUrl_FixesCommonInput(string input, string expected)
        {
            Assert.That(OmniRouteConfig.NormalizeBaseUrl(input), Is.EqualTo(expected));
        }

        [Test]
        public void Config_BuildsEndpointUrls_AndDefaultsModel()
        {
            var config = new OmniRouteConfig { baseUrl = "localhost:20128", model = " " };
            Assert.That(config.ChatCompletionsUrl, Is.EqualTo("http://localhost:20128/v1/chat/completions"));
            Assert.That(config.ModelsUrl, Is.EqualTo("http://localhost:20128/v1/models"));
            Assert.That(config.EffectiveModel, Is.EqualTo("auto"));
            Assert.That(config.HasApiKey, Is.False);
        }

        [Test]
        public void ChatRequest_IsValidJson_AndEscapesText()
        {
            var messages = new[]
            {
                new ChatMessage(ChatRole.System, "a\"b\n\\c"),
                new ChatMessage(ChatRole.User, "Şirket ↔ \u0001"),
            };
            string json = ChatJson.BuildChatRequest("auto", messages, 600, 0.4f);

            Assert.That(ChatJson.TryParse(json, out object root), Is.True);
            var obj = (Dictionary<string, object>)root;
            Assert.That(obj["model"], Is.EqualTo("auto"));
            Assert.That(obj["stream"], Is.EqualTo(false));
            var list = (List<object>)obj["messages"];
            Assert.That(list.Count, Is.EqualTo(2));
            Assert.That(((Dictionary<string, object>)list[0])["role"], Is.EqualTo("system"));
            Assert.That(((Dictionary<string, object>)list[0])["content"], Is.EqualTo("a\"b\n\\c"));
            Assert.That(((Dictionary<string, object>)list[1])["content"], Is.EqualTo("Şirket ↔ \u0001"));
        }

        [Test]
        public void ParseChatContent_ReadsFirstChoice()
        {
            const string json = "{\"id\":\"x\",\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":\" Merhaba \\u00e7 \"}," +
                                "\"finish_reason\":\"stop\"}],\"usage\":{\"total_tokens\":12}}";
            Assert.That(ChatJson.TryParseChatContent(json, out string content, out _), Is.True);
            Assert.That(content, Is.EqualTo("Merhaba ç"));
        }

        [Test]
        public void ParseChatContent_ReturnsGatewayError()
        {
            Assert.That(ChatJson.TryParseChatContent("{\"error\":{\"message\":\"No eligible route\"}}", out _, out string error), Is.False);
            Assert.That(error, Is.EqualTo("No eligible route"));
            Assert.That(ChatJson.TryParseChatContent("<html>502</html>", out _, out _), Is.False);
        }

        [Test]
        public void ParseModelIds_ReadsDataArray()
        {
            var ids = new List<string>();
            Assert.That(ChatJson.TryParseModelIds("{\"object\":\"list\",\"data\":[{\"id\":\"auto\"},{\"id\":\"glm/glm-5.2\",\"owned_by\":null}]}", ids, out _), Is.True);
            Assert.That(ids, Is.EqualTo(new[] { "auto", "glm/glm-5.2" }));
        }

        [Test]
        public void CompanySummary_ContainsKeyFacts_AndCapsRouteCount()
        {
            var state = new CompanyState
            {
                companyName = "Anadolu", startCityId = "IST", currentDay = 41, cash = -1234.5, daysInDebt = 3, level = 4,
                reputation = 0.62f, totalPassengers = 50000,
            };
            state.financeHistory.Add(new DailyFinanceSummary { income = 1000, expenses = 1500 });
            for (int i = 0; i < 16; i++)
            {
                var route = new RouteState { routeId = "r" + i, cityAId = "IST", cityBId = "C" + i, distanceKm = 100 + i * 30, ticketPrice = 20 + i };
                route.AddReport(new RouteDayReport { loadFactor = 0.6f, passengers = 100, demand = 130, revenue = 1000, cost = 900 + i * 20, satisfaction = 0.8f });
                state.routes.Add(route);
            }
            state.buses.Add(new BusInstance { modelId = "mini", assignedRouteId = "r1", condition = 0.7f });

            string summary = AdvisorBrief.BuildCompanySummary(state, new AdvisorContext
            {
                CurrencySymbol = "$", MaxRoutes = 20, VictoryCash = 10_000_000,
                CityName = id => id == "IST" ? "İstanbul" : id,
            });

            StringAssert.Contains("Anadolu", summary);
            StringAssert.Contains("İstanbul", summary);
            StringAssert.Contains("EKSİDE", summary);
            StringAssert.Contains($"{AdvisorBrief.MaxRoutesInBrief}/16", summary);
            Assert.That(AdvisorBrief.BuildCompanySummary(null, default), Is.Not.Empty);
        }

        [Test]
        public void BuildMessages_KeepsSystemFirst_AndOnlyRecentTurns()
        {
            var history = new List<ChatMessage>();
            for (int i = 0; i < 10; i++) history.Add(new ChatMessage(i % 2 == 0 ? ChatRole.User : ChatRole.Assistant, "m" + i));

            var messages = AdvisorBrief.BuildMessages("özet", history, "soru", maxTurns: 6);

            Assert.That(messages.Count, Is.EqualTo(8));
            Assert.That(messages[0].Role, Is.EqualTo(ChatRole.System));
            StringAssert.Contains("özet", messages[0].Content);
            Assert.That(messages[1].Content, Is.EqualTo("m4"));
            Assert.That(messages[7].Content, Is.EqualTo("soru"));
        }

        [Test]
        public void CleanReply_StripsMarkdownAndUnsupportedGlyphs()
        {
            string cleaned = AdvisorBrief.CleanReply("## Öneriler \U0001F68C\n\n\n**1.** Fiyatı `artır`\n* Hat A\n- Hat B\r\n");
            Assert.That(cleaned, Is.EqualTo("Öneriler\n\n1. Fiyatı artır\n• Hat A\n• Hat B"));
        }
    }
}
