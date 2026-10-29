using System;
using System.Collections.Generic;
using BusTycoon.Core;
using BusTycoon.Data;
using BusTycoon.Economy;
using BusTycoon.Fleet;
using BusTycoon.Routes;
using BusTycoon.Save;
using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// Mağaza ekran görüntüsü turu (…/oyna/?vitrin&amp;profil=telefon). Orta oyunda iyi yönetilen bir şirket kurar
    /// (8 hat, önerilen sayıda otobüs, en kârlı fiyatlar, 2 aylık geçmiş) ve oyunu sırayla gösterilecek
    /// ekranlara getirir. Her ekran hazır olunca
    /// konsola <c>[Showcase] READY n/N ad</c> yazar.
    ///  ?vitrin        → elle izleme: her ekran birkaç saniye görünür, kendiliğinden ilerler.
    ///  ?vitrin=betik  → ekran görüntüsünü alan betik (Tools/store-screenshots.mjs) her görüntüden sonra
    ///                   <c>unityInstance.SendMessage('ShowcaseDirector', 'Continue')</c> ile ilerletir.
    /// </summary>
    public sealed class ShowcaseDirector : MonoBehaviour
    {
        public const string ObjectName = "ShowcaseDirector";

        const float SettleSeconds = 2.5f;     // kamera geçişi, grafikler ve şehir etiketleri otursun
        const float AutoAdvanceSeconds = 6f;  // elle izlemede sonraki ekrana geçiş
        const int SimulatedDays = 65;         // 60 günlük finans grafiğinde açılış alımları görünmesin

        GameSession session;
        GameUI ui;
        readonly List<KeyValuePair<string, Action>> shots = new List<KeyValuePair<string, Action>>();
        int index = -1;
        float shownAt, announcedAt;
        int tidyFrame;
        bool scripted, tidied, announced, continueRequested;

        public void Init(GameSession gameSession, GameUI gameUI)
        {
            session = gameSession;
            ui = gameUI;
            string mode = LaunchOptions.Get("vitrin") ?? LaunchOptions.Get("showcase");
            scripted = mode == "betik" || mode == "script";
        }

        /// <summary>Ekran görüntüsünü alan betik çağırır.</summary>
        public void Continue() => continueRequested = true;

        void Update()
        {
            if (session == null || ui == null) return;
            if (index < 0)
            {
                BuildCompany();
                Show(0);
                return;
            }
            if (index >= shots.Count) return;

            // Tur boyunca oyun akmaya devam eder; kasa orta oyun düzeyinde kalsın (ekran görüntüleri tutarlı olsun).
            if (session.State.cash > 900000) session.State.cash = 640000;

            if (!tidied)
            {
                if (Time.unscaledTime - shownAt < SettleSeconds) return;
                ui.ShowcaseTidy();
                tidied = true;
                tidyFrame = Time.frameCount;
                return;
            }

            // Bildirimlerin kalktığı karenin çizildiğinden emin olmak için iki kare bekle.
            if (!announced)
            {
                if (Time.frameCount < tidyFrame + 2) return;
                announced = true;
                announcedAt = Time.unscaledTime;
                Debug.Log($"[Showcase] READY {index + 1}/{shots.Count} {shots[index].Key}");
                return;
            }

            bool timedOut = !scripted && Time.unscaledTime - announcedAt > AutoAdvanceSeconds;
            if (continueRequested || timedOut)
            {
                continueRequested = false;
                Show(index + 1);
            }
        }

        void Show(int next)
        {
            index = next;
            tidied = announced = false;
            shownAt = Time.unscaledTime;
            if (index < shots.Count) shots[index].Value();
            else Debug.Log("[Showcase] DONE");
        }

        // ------------------------------------------------------------------ vitrin şirketi

        void BuildCompany()
        {
            CityData istanbul = City("İstanbul"), ankara = City("Ankara"), izmir = City("İzmir"),
                antalya = City("Antalya"), bursa = City("Bursa"), trabzon = City("Trabzon"),
                sofya = City("Sofya"), selanik = City("Selanik"), center = City("Eskişehir");

            session.StartNewGame("Anadolu Ekspres", istanbul);
            CompanyState state = session.State;
            state.cash = 8000000;
            state.reputation = 74f;
            session.Progression.AddExperience(session.ProgressionConfig.XpRequiredForLevel(16));
            session.Progression.RefreshUnlocks(silent: true);
            UnlockRegion(sofya);
            UnlockRegion(selanik);

            RouteState flagship = OpenRoute(istanbul, ankara, "TOROS_46");
            OpenRoute(istanbul, izmir, "TOROS_42C");
            OpenRoute(ankara, antalya, "TOROS_46");
            RouteState bursaLine = OpenRoute(istanbul, bursa, "ANKA_31");
            OpenRoute(izmir, antalya, "ANKA_31");
            OpenRoute(ankara, trabzon, "ZIRVE_VIP");
            OpenRoute(istanbul, sofya, "TOROS_46");
            OpenRoute(istanbul, selanik, "ZIRVE_DD");
            AssignIdleBuses(bursaLine);

            // Geçmiş grafikleri dolsun; kasa orta oyun düzeyinde kalsın.
            state.cash = 320000;
            for (int day = 0; day < SimulatedDays; day++)
            {
                if (state.cash > 850000) state.cash = 600000;
                if (day == SimulatedDays / 2) SetBestPrices();
                session.Clock.AdvanceOneDay();
            }
            SetBestPrices();
            state.cash = Math.Min(state.cash, 486350);

            shots.Clear();
            shots.Add(new KeyValuePair<string, Action>("harita", () => ui.ShowcaseMap(center, 58f)));
            shots.Add(new KeyValuePair<string, Action>("planlayici", () => ui.ShowcasePlanner(istanbul, antalya)));
            if (flagship != null) shots.Add(new KeyValuePair<string, Action>("fiyat", () => ui.ShowcaseRoute(flagship)));
            shots.Add(new KeyValuePair<string, Action>("galeri", () => ui.ShowcaseDealership(center, 52f)));
            shots.Add(new KeyValuePair<string, Action>("finans", () => ui.ShowcaseFinance(center, 52f)));
        }

        CityData City(string name)
        {
            foreach (var city in session.World.Cities)
                if (city.DisplayName == name) return city;
            Debug.LogWarning($"[Showcase] Şehir bulunamadı: {name}");
            return session.Headquarters;
        }

        void UnlockRegion(CityData city)
        {
            if (city == null || session.Progression.IsRegionUnlocked(city.Region)) return;
            var result = session.Progression.TryUnlockRegion(city.Region);
            if (!result) Debug.LogWarning($"[Showcase] Bölge açılamadı ({city.DisplayName}): {result.Message}");
        }

        const int MaxBusesPerRoute = 4;

        RouteState OpenRoute(CityData a, CityData b, string modelId)
        {
            BusModelData model = session.Catalog.GetById(modelId);
            RouteQuote quote = session.Routes.QuoteRoute(a, b, model);
            var opened = session.Routes.TryOpenRoute(a, b, quote.IsValid ? quote.SuggestedPrice : 25f, out RouteState route);
            if (!opened)
            {
                Debug.LogWarning($"[Showcase] Hat açılamadı ({a.DisplayName}–{b.DisplayName}): {opened.Message}");
                return null;
            }

            int busCount = Mathf.Clamp(quote.IsValid ? quote.RecommendedBusCount : 1, 1, MaxBusesPerRoute);
            for (int i = 0; i < busCount && model != null; i++)
            {
                if (!session.Fleet.TryPurchase(model, 1f, out BusInstance bus)) break;
                session.Routes.TryAssignBus(bus.busId, route.routeId);
            }
            return route;
        }

        /// <summary>Her hatta fiyatı tahmini en kârlı değere çeker (iyi yönetilen şirket görünümü).</summary>
        void SetBestPrices()
        {
            var analysis = new PricingAnalysis();
            foreach (var route in session.Routes.Routes)
            {
                session.Routes.AnalyzePricing(route, 60, analysis);
                if (analysis.EstimatedBestPrice > 0f) session.Routes.SetTicketPrice(route.routeId, analysis.EstimatedBestPrice);
            }
        }

        void AssignIdleBuses(RouteState route)
        {
            if (route == null) return;
            var idle = new List<BusInstance>();
            session.Fleet.GetIdleBuses(idle);
            foreach (var bus in idle) session.Routes.TryAssignBus(bus.busId, route.routeId);
        }
    }
}
