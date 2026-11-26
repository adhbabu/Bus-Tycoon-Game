using System;
using System.Collections.Generic;
using BusTycoon.Core;
using BusTycoon.Data;
using BusTycoon.Economy;
using BusTycoon.Fleet;
using BusTycoon.Routes;
using BusTycoon.Save;
using BusTycoon.Stations;
using BusTycoon.Urban;
using UnityEngine;

namespace BusTycoon.Game
{
    /// <summary>
    /// Mağaza ekran görüntüsü turu (…/oyna/?vitrin&amp;profil=telefon). Yeni arayüzde İstanbul'da şehir içi başlangıçlı,
    /// iyi yönetilen bir şirket kurar (6 dolmuş/şehir otobüsü hattı, önerilen sayıda araç, en kârlı fiyatlar, 2 aylık
    /// geçmiş); eski arayüzde (?arayuz=eski) şehirlerarası şirket (8 hat). Oyunu sırayla gösterilecek ekranlara getirir.
    /// Her ekran hazır olunca
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
        bool scripted, tidied, announced, continueRequested, cityStarted;
        CityData showcaseCity;

        public void Init(GameSession gameSession, GameUI gameUI)
        {
            session = gameSession;
            ui = gameUI;
            string mode = LaunchOptions.Get("vitrin") ?? LaunchOptions.Get("showcase");
            scripted = mode == "betik" || mode == "script";
            // Mağaza görüntüleri gündüz çekilir (?saat=… ile gece/akşam da çekilebilir).
            if (!LaunchOptions.Has("saat") && DayNightCycle.Instance != null) DayNightCycle.Instance.ForceTime(0.5f);
        }

        /// <summary>Ekran görüntüsünü alan betik çağırır.</summary>
        public void Continue() => continueRequested = true;

        void Update()
        {
            if (session == null || ui == null) return;
            if (index < 0)
            {
                if (ui.ShowcaseNewUi)
                {
                    // Şehir haritası tarayıcıda dosyadan iner: hazır olana kadar bekle
                    if (!cityStarted) StartCityCompany();
                    if (!ui.ShowcaseCityReady) return;
                    BuildCityCompany();
                }
                else BuildCompany();
                Show(0);
                return;
            }
            if (index >= shots.Count) return;

            // Tur boyunca oyun akmaya devam eder; kasa orta oyun düzeyinde kalsın (ekran görüntüleri tutarlı olsun).
            if (session.State.cash > 900000) session.State.cash = 640000;

            if (!tidied)
            {
                if (Time.unscaledTime - shownAt < SettleSeconds) return;
                StationView stations = StationView.Instance;
                if (stations != null && stations.IsActive && stations.IntroProgress < 1f) return; // durağa giriş bitsin
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

        // ------------------------------------------------------------------ vitrin şirketi (şehir içi, yeni arayüz)

        static readonly string[] CityLineModels = { "KENT_12", "KARTAL_M19", "ANKA_27", "KARTAL_M16", "KENT_12", "ANKA_31" };
        const int CityLines = 6;

        void StartCityCompany()
        {
            cityStarted = true;
            showcaseCity = session.World.GetCity("TR_ISTANBUL") ?? City("İstanbul");
            session.StartNewGame("Anadolu Ekspres", showcaseCity, withStartingBus: false);
            CompanyState state = session.State;
            state.logoId = 1;
            state.vehicleClasses = true;
            state.cityLicenses = true;
            state.waitTimes = false; // vitrin: hatlar ve araçlar hemen hazır (sonra gerçek kurallar)
            state.reputation = 72f;
            session.Progression.GrantCityLicense(showcaseCity);
            session.Progression.AddExperience(session.ProgressionConfig.XpRequiredForLevel(6));
            session.Progression.RefreshUnlocks(silent: true);
            state.cash = 6000000;
            ui.ShowcaseCityMap();
        }

        void BuildCityCompany()
        {
            CompanyState state = session.State;
            UrbanMap map = ui.ShowcaseCityMapData;
            var stops = new List<UrbanStop>(map.Stops);
            stops.Sort((a, b) => b.Weight.CompareTo(a.Weight));
            int take = Math.Min(40, stops.Count);
            var uses = new Dictionary<UrbanStop, int>();
            var routes = new List<RouteState>();
            RouteState landmarkRoute = null;
            UrbanLandmark landmark = null;
            UrbanStop plannerA = null, plannerB = null;

            for (int i = 0; i < take && (routes.Count < CityLines || plannerA == null); i++)
                for (int j = i + 1; j < take && (routes.Count < CityLines || plannerA == null); j++)
                {
                    UrbanStop a = stops[i], b = stops[j];
                    float km = (a.Position - b.Position).magnitude / 1000f;
                    if (km < 3.5f || km > 9f) continue;
                    var plan = new UrbanLinePlan();
                    if (!UrbanPlanner.TryPlan(map, a, b, showcaseCity, session.Economy, plan)) continue;
                    uses.TryGetValue(a, out int usedA);
                    uses.TryGetValue(b, out int usedB);
                    if (routes.Count >= CityLines || usedA >= 2 || usedB >= 2)
                    {
                        // Planlayıcı görüntüsü için açılmamış bir çift
                        if (plannerA == null && usedA == 0 && usedB == 0 && routes.Count >= CityLines) { plannerA = a; plannerB = b; }
                        continue;
                    }
                    BusModelData model = session.Catalog.GetById(CityLineModels[routes.Count % CityLineModels.Length]);
                    if (model == null) continue;
                    RouteQuote quote = session.Routes.QuoteUrbanRoute(showcaseCity, plan, model);
                    if (!quote.IsValid) continue;
                    var opened = session.Routes.TryOpenUrbanRoute(showcaseCity, plan, quote.SuggestedPrice, out RouteState route);
                    if (!opened) continue;
                    int busCount = Mathf.Clamp(quote.RecommendedBusCount, 1, MaxBusesPerRoute);
                    for (int k = 0; k < busCount; k++)
                    {
                        if (!session.Fleet.TryPurchase(model, 1f, out BusInstance bus)) break;
                        bus.deliveryDaysLeft = 0;
                        session.Routes.TryAssignBus(bus.busId, route.routeId);
                    }
                    routes.Add(route);
                    uses[a] = usedA + 1;
                    uses[b] = usedB + 1;
                    if (landmarkRoute == null && plan.Landmarks.Count >= 2)
                    {
                        landmarkRoute = route;
                        landmark = plan.Landmarks[0];
                    }
                }
            if (routes.Count == 0) Debug.LogWarning("[Showcase] Şehir içi hat açılamadı");

            // Geçmiş grafikleri dolsun; kasa orta oyun düzeyinde kalsın
            state.cash = 320000;
            for (int day = 0; day < SimulatedDays; day++)
            {
                if (state.cash > 850000) state.cash = 600000;
                if (day == SimulatedDays / 2) SetBestPrices();
                session.Clock.AdvanceOneDay();
            }
            SetBestPrices();
            state.cash = Math.Min(state.cash, 486350);
            state.waitTimes = true;

            RouteState best = null;
            foreach (var route in routes)
                if (best == null || route.LastReport.Profit > best.LastReport.Profit) best = route;

            shots.Clear();
            shots.Add(new KeyValuePair<string, Action>("sehir-haritasi", () => ui.ShowcaseCityOverview(routes)));
            if (plannerA != null) shots.Add(new KeyValuePair<string, Action>("hat-planlama", () => ui.ShowcaseUrbanPlanner(plannerA, plannerB)));
            if (best != null) shots.Add(new KeyValuePair<string, Action>("bilet-fiyati", () => ui.ShowcaseRoute(best)));
            if (landmark != null) shots.Add(new KeyValuePair<string, Action>("yakin-plan", () => ui.ShowcaseCityCloseUp(landmark.Position)));
            shots.Add(new KeyValuePair<string, Action>("ana-ekran", () => ui.ShowcaseHome()));
            shots.Add(new KeyValuePair<string, Action>("galeri", () => ui.ShowcaseGallery()));
        }

        // ------------------------------------------------------------------ vitrin şirketi (şehirlerarası, eski arayüz)

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
            GrantAllUpgrades(istanbul);

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
            shots.Add(new KeyValuePair<string, Action>("durak", () => ui.ShowcaseStation(istanbul)));
        }

        /// <summary>Vitrin: durağın tüm geliştirmeleri yapılmış görünür (kurallar atlanır; yalnız ekran görüntüsü için).</summary>
        void GrantAllUpgrades(CityData city)
        {
            if (city == null) return;
            CompanyState state = session.State;
            state.stations.RemoveAll(s => s.cityId == city.Id);
            var station = new StationState { cityId = city.Id, firstUpgradeDay = state.currentDay };
            StationTier tier = StationCatalog.TierOf(city);
            foreach (var upgrade in StationCatalog.Upgrades)
                if (StationCatalog.IsAvailable(upgrade, tier)) station.upgrades.Add(upgrade.Id);
            state.stations.Add(station);
        }

        string visiting;

        /// <summary>
        /// Test/ekran görüntüsü betiği için: <c>SendMessage('ShowcaseDirector', 'VisitStation', 'TR_ARTVIN')</c> o şehrin
        /// durağına (tüm geliştirmeleriyle) girer; giriş bitince konsola <c>[Showcase] STATION şehir</c> yazar.
        /// </summary>
        public void VisitStation(string cityId)
        {
            CityData city = session.World.GetCity(cityId);
            if (city == null)
            {
                Debug.LogWarning($"[Showcase] Şehir yok: {cityId}");
                return;
            }
            GrantAllUpgrades(city);
            ui.ShowcaseStation(city);
            visiting = cityId;
        }

        void LateUpdate()
        {
            if (visiting == null) return;
            StationView stations = StationView.Instance;
            if (stations == null || !stations.IsInside || stations.IntroProgress < 1f) return;
            ui.ShowcaseTidy();
            Debug.Log($"[Showcase] STATION {visiting}");
            visiting = null;
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
