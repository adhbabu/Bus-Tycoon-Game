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
            // Şehir içi turda kasa grafiği ana ekranda görünür: kırpılmaz (tur birkaç oyun günü sürer).
            if (!cityStarted && session.State.cash > 900000) session.State.cash = 640000;

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

        static readonly string[] CityLineModels = { "KENT_12", "KARTAL_M19", "ANKA_27", "KENT_12", "KARTAL_M16", "ANKA_31" };
        const int CityLines = 6;
        /// <summary>Şehir içi hatlarda bilet önerilen fiyatın en çok bu katı (yolcu memnun kalsın, uyarı çıkmasın).</summary>
        const float CityPriceCap = 1.0f;
        /// <summary>Ana ekrandaki 30 günlük kasa grafiğinin en alt noktası (iç para birimi).</summary>
        const double CityCashFloor = 260000;

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
            var nearRoutes = new List<UrbanLandmark>();
            var basePrices = new Dictionary<string, float>();
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
                    basePrices[route.routeId] = quote.SuggestedPrice;
                    uses[a] = usedA + 1;
                    uses[b] = usedB + 1;
                    foreach (var near in plan.Landmarks) if (!nearRoutes.Contains(near)) nearRoutes.Add(near);
                }
            if (routes.Count == 0) Debug.LogWarning("[Showcase] Şehir içi hat açılamadı");

            // Geçmiş grafikleri dolsun. Kasa kırpılmaz (ana ekranın kasa grafiği testere dişi olmasın); sonunda bütün
            // geçmiş aynı miktarda kaydırılır: grafiğin biçimi gerçek kalır, kasa orta oyun düzeyinde biter.
            state.cash = 320000;
            for (int day = 0; day < SimulatedDays; day++)
            {
                if (day == SimulatedDays / 3) SetBestPrices(basePrices);
                session.Clock.AdvanceOneDay();
            }
            SetBestPrices(basePrices);
            ShiftCashHistory(state);
            state.waitTimes = true;

            RouteState best = PickPriceRoute(routes, map);
            Vector2 closeUp;
            float closeUpSpan;
            bool hasCloseUp = PickLandmarkCluster(map, nearRoutes, out closeUp, out closeUpSpan);

            shots.Clear();
            shots.Add(new KeyValuePair<string, Action>("sehir-haritasi", () => ui.ShowcaseCityOverview(routes)));
            if (plannerA != null) shots.Add(new KeyValuePair<string, Action>("hat-planlama", () => ui.ShowcaseUrbanPlanner(plannerA, plannerB)));
            if (best != null) shots.Add(new KeyValuePair<string, Action>("bilet-fiyati", () => ui.ShowcaseRoute(best)));
            if (hasCloseUp) shots.Add(new KeyValuePair<string, Action>("yakin-plan", () => ui.ShowcaseCityCloseUp(closeUp, closeUpSpan)));
            shots.Add(new KeyValuePair<string, Action>("ana-ekran", () => ui.ShowcaseHome()));
            shots.Add(new KeyValuePair<string, Action>("galeri", () => ui.ShowcaseGallery("KENT_12")));
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

        /// <summary>Her hatta fiyatı tahmini en kârlı değere çeker (iyi yönetilen şirket görünümü). Taban fiyatı
        /// verilen hatlarda bilet en çok tabanın <see cref="CityPriceCap"/> katı olur.</summary>
        void SetBestPrices(Dictionary<string, float> basePrices = null)
        {
            var analysis = new PricingAnalysis();
            foreach (var route in session.Routes.Routes)
            {
                session.Routes.AnalyzePricing(route, 60, analysis);
                float price = analysis.EstimatedBestPrice;
                if (basePrices != null && basePrices.TryGetValue(route.routeId, out float basePrice)) price = Mathf.Min(price, basePrice * CityPriceCap);
                if (price > 0f) session.Routes.SetTicketPrice(route.routeId, price);
            }
        }

        /// <summary>Kasa geçmişini (ve kasayı) aynı miktarda kaydırır: son 30 günün en düşüğü <see cref="CityCashFloor"/> olur.</summary>
        static void ShiftCashHistory(CompanyState state)
        {
            var history = state.financeHistory;
            if (history == null || history.Count == 0) return;
            double low = double.MaxValue;
            for (int i = Math.Max(0, history.Count - 30); i < history.Count; i++) low = Math.Min(low, history[i].cashAtClose);
            double shift = CityCashFloor - low;
            foreach (var day in history) day.cashAtClose += shift;
            state.cash += shift;
        }

        /// <summary>Bilet fiyatı görüntüsünün hattı: en kârlılardan, haritanın kenarına en uzak olanı (kartta boşluk kalmasın).</summary>
        static RouteState PickPriceRoute(List<RouteState> routes, UrbanMap map)
        {
            RouteState best = null, fallback = null;
            float fallbackMargin = float.MinValue;
            var sorted = new List<RouteState>(routes);
            sorted.Sort((x, y) => y.LastReport.Profit.CompareTo(x.LastReport.Profit));
            for (int i = 0; i < sorted.Count; i++)
            {
                RouteState route = sorted[i];
                var mid = new Vector2((route.urbanAX + route.urbanBX) * 0.5f, (route.urbanAY + route.urbanBY) * 0.5f);
                Rect bounds = map.Bounds;
                float margin = Mathf.Min(Mathf.Min(mid.x - bounds.xMin, bounds.xMax - mid.x), Mathf.Min(mid.y - bounds.yMin, bounds.yMax - mid.y));
                if (margin > fallbackMargin) { fallbackMargin = margin; fallback = route; }
                if (best == null && i < 3 && margin >= 7000f) best = route;
            }
            return best ?? fallback;
        }

        /// <summary>Yakın plan: hatların yakınındaki mekânlardan çevresinde en çok mekân olanı; odak o kümenin ortası.</summary>
        static bool PickLandmarkCluster(UrbanMap map, List<UrbanLandmark> nearRoutes, out Vector2 center, out float span)
        {
            const float Radius = 1100f;
            center = Vector2.zero;
            span = 0f;
            int bestCount = 0;
            IEnumerable<UrbanLandmark> candidates = nearRoutes.Count > 0 ? (IEnumerable<UrbanLandmark>)nearRoutes : map.Landmarks;
            foreach (var landmark in candidates)
            {
                int count = 0;
                Vector2 sum = Vector2.zero, min = landmark.Position, max = landmark.Position;
                foreach (var other in map.Landmarks)
                {
                    if ((other.Position - landmark.Position).magnitude > Radius) continue;
                    count++;
                    sum += other.Position;
                    min = new Vector2(Mathf.Min(min.x, other.Position.x), Mathf.Min(min.y, other.Position.y));
                    max = new Vector2(Mathf.Max(max.x, other.Position.x), Mathf.Max(max.y, other.Position.y));
                }
                if (count <= bestCount) continue;
                bestCount = count;
                center = sum / count;
                span = Mathf.Max(max.x - min.x, max.y - min.y);
            }
            if (bestCount > 0) Debug.Log($"[Showcase] yakın plan: {bestCount} mekân, {span:0} m");
            return bestCount > 0;
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
