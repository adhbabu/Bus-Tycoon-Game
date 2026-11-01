using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BusTycoon.Fleet;
using BusTycoon.Routes;
using BusTycoon.Save;

namespace BusTycoon.AI
{
    /// <summary>Danışmana gönderilen şirket özeti için oyundan gelen ek bilgiler (CompanyState'te olmayanlar).</summary>
    public struct AdvisorContext
    {
        public string CurrencySymbol;
        public int MaxRoutes;
        public double VictoryCash;
        /// <summary>cityId → görünen ad. Boşsa kimlik kullanılır.</summary>
        public Func<string, string> CityName;
        /// <summary>modelId → görünen ad. Boşsa kimlik kullanılır.</summary>
        public Func<string, string> ModelName;
    }

    /// <summary>
    /// Yapay zekâ danışmanının sistem talimatı ve şirketin kısa metin özeti. Özet kısa tutulur (en kârlı/zararlı
    /// hatlar öne alınır) ki ücretsiz modellerin bağlam sınırına ve token kotasına sığsın.
    /// </summary>
    public static class AdvisorBrief
    {
        public const int MaxRoutesInBrief = 12;

        public const string SystemPrompt =
            "Sen 'Bus Tycoon' adlı otobüs işletmeciliği strateji oyununda oyuncunun şirket danışmanısın. " +
            "Türkçe, samimi ve kısa yaz (en fazla 6 madde ya da 120 kelime). Yalnızca sana verilen şirket verisine dayan; " +
            "veride olmayan rakam uydurma. Somut öneri ver: hangi hattın fiyatı artmalı/azalmalı, hangi hatta otobüs " +
            "eklenmeli, hangi hat kapatılmalı, nakit durumu riskli mi. Oyun kuralları: fiyat yükselince yolcu azalır; doluluk " +
            "%95 üstündeyse ve karşılanmayan talep varsa ya fiyat artırılır ya otobüs eklenir; doluluk %50 altındaysa fiyat " +
            "düşürülür ya da otobüs başka hatta alınır; minibüs 300 km üstü hatlarda yolcu kaybeder; kasa 30 gün eksi kalırsa " +
            "şirket iflas eder; seviye atladıkça daha çok hat ve yeni otobüs modelleri açılır.";

        public static readonly string[] QuickQuestions =
        {
            "Şirketimi değerlendir: en büyük 3 sorun ve çözümü?",
            "Bilet fiyatlarım doğru mu? Hangi hatta ne yapmalıyım?",
            "Otobüslerimi hatlara nasıl dağıtmalıyım?",
            "Şimdi hangi yeni hattı açmalıyım? Neden?",
        };

        /// <summary>Şirketin o anki durumunun kısa metin özeti (modelin "gördüğü" tek veri).</summary>
        public static string BuildCompanySummary(CompanyState state, AdvisorContext context)
        {
            if (state == null) return "Henüz şirket yok (oyun başlamadı).";
            var inv = CultureInfo.InvariantCulture;
            string sym = string.IsNullOrEmpty(context.CurrencySymbol) ? "$" : context.CurrencySymbol;
            string City(string id) => context.CityName?.Invoke(id) ?? id;
            string Model(string id) => context.ModelName?.Invoke(id) ?? id;
            string Money(double v) => (v < 0 ? "-" : "") + sym + Math.Abs(Math.Round(v)).ToString("#,0", inv);

            var sb = new StringBuilder(1024);
            sb.Append("ŞİRKET: ").Append(string.IsNullOrEmpty(state.companyName) ? "(adsız)" : state.companyName)
              .Append(" · merkez ").Append(City(state.startCityId))
              .Append(" · gün ").Append(state.currentDay + 1).Append('\n');
            sb.Append("Kasa ").Append(Money(state.cash));
            if (context.VictoryCash > 0) sb.Append(" (hedef ").Append(Money(context.VictoryCash)).Append(')');
            if (state.daysInDebt > 0) sb.Append(" · ").Append(state.daysInDebt).Append(" gündür EKSİDE (30 günde iflas)");
            sb.Append('\n');
            sb.Append("Seviye ").Append(state.level).Append(" · itibar ").Append(state.reputation.ToString("0.00", inv))
              .Append(" · hat ").Append(state.routes?.Count ?? 0);
            if (context.MaxRoutes > 0) sb.Append('/').Append(context.MaxRoutes);
            sb.Append(" · toplam yolcu ").Append(state.totalPassengers.ToString("#,0", inv)).Append('\n');

            // Son 7 günün finansı
            var history = state.financeHistory;
            if (history != null && history.Count > 0)
            {
                int from = Math.Max(0, history.Count - 7);
                double income = 0, expenses = 0;
                for (int i = from; i < history.Count; i++)
                {
                    income += history[i].income;
                    expenses += history[i].expenses;
                }
                sb.Append("Son ").Append(history.Count - from).Append(" gün: gelir ").Append(Money(income))
                  .Append(", gider ").Append(Money(expenses)).Append(", net ").Append(Money(income - expenses)).Append('\n');
            }

            // Filo
            var buses = state.buses ?? new List<BusInstance>();
            var busesPerRoute = new Dictionary<string, int>();
            int idle = 0, broken = 0;
            float conditionSum = 0f;
            var models = new Dictionary<string, int>();
            foreach (var bus in buses)
            {
                if (bus.IsAssigned) busesPerRoute[bus.assignedRouteId] = (busesPerRoute.TryGetValue(bus.assignedRouteId, out int n) ? n : 0) + 1;
                else idle++;
                if (bus.outOfServiceDays > 0) broken++;
                conditionSum += bus.condition;
                models[bus.modelId] = (models.TryGetValue(bus.modelId, out int m) ? m : 0) + 1;
            }
            sb.Append("FİLO: ").Append(buses.Count).Append(" otobüs, ").Append(idle).Append(" garajda");
            if (broken > 0) sb.Append(", ").Append(broken).Append(" arızalı");
            if (buses.Count > 0) sb.Append(", ortalama durum %").Append(Math.Round(conditionSum / buses.Count * 100f));
            if (models.Count > 0)
            {
                sb.Append(" (");
                bool first = true;
                foreach (var pair in models)
                {
                    if (!first) sb.Append(", ");
                    sb.Append(pair.Value).Append("× ").Append(Model(pair.Key));
                    first = false;
                }
                sb.Append(')');
            }
            sb.Append(state.autoMaintenance ? " · bakım asistanı AÇIK" : " · bakım asistanı kapalı").Append('\n');

            // Hatlar: önce zarar edenler ve en kârlılar
            var routes = new List<RouteState>(state.routes ?? new List<RouteState>());
            if (routes.Count == 0)
            {
                sb.Append("HATLAR: henüz hat yok.\n");
                return sb.ToString();
            }
            routes.Sort((a, b) => a.LastReport.Profit.CompareTo(b.LastReport.Profit));
            var picked = new List<RouteState>();
            if (routes.Count <= MaxRoutesInBrief) picked.AddRange(routes);
            else
            {
                int half = MaxRoutesInBrief / 2;
                for (int i = 0; i < half; i++) picked.Add(routes[i]);
                for (int i = routes.Count - half; i < routes.Count; i++) picked.Add(routes[i]);
            }

            sb.Append("HATLAR (dünkü sonuç; ").Append(picked.Count).Append('/').Append(routes.Count).Append(" gösteriliyor, zarardan kâra):\n");
            foreach (var route in picked)
            {
                var r = route.LastReport;
                int assigned = busesPerRoute.TryGetValue(route.routeId, out int count) ? count : 0;
                sb.Append("- ").Append(City(route.cityAId)).Append(" ↔ ").Append(City(route.cityBId))
                  .Append(": ").Append(Math.Round(route.distanceKm)).Append(" km, bilet ").Append(Money(route.ticketPrice))
                  .Append(", ").Append(assigned).Append(" otobüs");
                if (route.HasHistory)
                {
                    sb.Append(", doluluk %").Append(Math.Round(r.loadFactor * 100f))
                      .Append(", yolcu ").Append(Math.Round(r.passengers))
                      .Append("/talep ").Append(Math.Round(r.demand));
                    if (r.unmetDemand >= 1f) sb.Append(" (karşılanmayan ").Append(Math.Round(r.unmetDemand)).Append(')');
                    sb.Append(", kâr ").Append(Money(r.Profit))
                      .Append(", memnuniyet %").Append(Math.Round(r.satisfaction * 100f));
                }
                else sb.Append(", henüz sonuç yok (yeni)");
                sb.Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>
        /// Modele gidecek mesaj listesi: sistem talimatı + güncel şirket özeti + son konuşma (en fazla
        /// <paramref name="maxTurns"/> mesaj) + yeni soru.
        /// </summary>
        public static List<ChatMessage> BuildMessages(string companySummary, IReadOnlyList<ChatMessage> history, string question, int maxTurns = 6)
        {
            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, SystemPrompt + "\n\nGÜNCEL ŞİRKET VERİSİ:\n" + companySummary),
            };
            if (history != null)
            {
                int start = Math.Max(0, history.Count - Math.Max(0, maxTurns));
                for (int i = start; i < history.Count; i++)
                    if (history[i].Role != ChatRole.System) messages.Add(history[i]);
            }
            messages.Add(new ChatMessage(ChatRole.User, question ?? string.Empty));
            return messages;
        }

        /// <summary>
        /// Model yanıtını oyunun yazı tipine uygun hale getirir: Markdown işaretleri (**, #, `) atılır, "* " ve "- "
        /// maddeleri "• " olur, yazı tipinde olmayan karakterler (emoji vb.) çıkarılır, fazla boş satırlar daralır.
        /// </summary>
        public static string CleanReply(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var sb = new StringBuilder(text.Length);
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            int blank = 0;
            foreach (string raw in lines)
            {
                string line = raw.Replace("**", "").Replace("__", "").Replace("`", "").TrimEnd();
                string body = line.TrimStart();
                if (body.StartsWith("#")) line = body = body.TrimStart('#').TrimStart();   // başlık
                if (body.StartsWith("* ") || body.StartsWith("- ")) line = "• " + body.Substring(2);

                var clean = new StringBuilder(line.Length);
                foreach (char c in line)
                    if (IsRenderable(c)) clean.Append(c);
                line = clean.ToString().TrimEnd();

                if (line.Length == 0)
                {
                    if (++blank > 1 || sb.Length == 0) continue;
                }
                else blank = 0;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(line);
            }
            return sb.ToString().Trim();
        }

        /// <summary>Tools/make_fonts.py'deki yazı tipi alt kümesiyle aynı aralıklar.</summary>
        static bool IsRenderable(char c) =>
            (c >= 0x20 && c < 0x7F) || (c >= 0xA0 && c < 0x180) || (c >= 0x2010 && c < 0x2028) || (c >= 0x2030 && c < 0x203B) ||
            (c >= 0x20A0 && c < 0x20C0) || c == 0x2122 || (c >= 0x2190 && c < 0x2196) || c == 0x21C4 || c == 0x2212 ||
            c == 0x2605 || c == 0x2606;
    }
}
