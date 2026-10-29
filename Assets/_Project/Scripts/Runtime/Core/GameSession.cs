using System;
using System.Collections.Generic;
using BusTycoon.Data;
using BusTycoon.Economy;
using BusTycoon.Fleet;
using BusTycoon.Progression;
using BusTycoon.Routes;
using BusTycoon.Save;
using BusTycoon.Stations;
using BusTycoon.World;
using UnityEngine;

namespace BusTycoon.Core
{
    /// <summary>Bir günün tüm sonuçları; UI (HUD, bildirimler, grafikler) bunu dinler.</summary>
    public sealed class DayReport
    {
        public int Day;
        public readonly List<RouteDayReport> Routes = new List<RouteDayReport>();
        public DailyFinanceSummary Finance;
        public double Passengers;
        public float AverageSatisfaction;
    }

    /// <summary>
    /// Kompozisyon kökü (composition root). Sahnede TEK MonoBehaviour'dur; tüm sistemler saf C#
    /// sınıflarıdır ve burada oluşturulup birbirine bağlanır. UI ve harita görünümleri sistemlere
    /// <see cref="Instance"/> üzerinden erişir ve olaylarına abone olur.
    ///
    ///  ScriptableObject (tasarım verisi, salt okunur) ──▶ Sistemler (kurallar) ──▶ CompanyState (kayıt)
    ///                                                         ▲          │ olaylar
    ///                                              komutlar   │          ▼
    ///                                                     UI / Harita görünümleri
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameSession : MonoBehaviour
    {
        [Header("Tasarım verisi")]
        [SerializeField] WorldMapDatabase worldDatabase;
        [SerializeField] EconomyConfig economyConfig;
        [SerializeField] ProgressionConfig progressionConfig;
        [SerializeField] BusCatalog busCatalog;

        [Header("Simülasyon")]
        [SerializeField, Min(0.1f)] float secondsPerDay = 4f;
        [Tooltip("0 = her oyunda farklı rastgelelik.")]
        [SerializeField] int randomSeed;
        [SerializeField] string autosaveSlot = "autosave";
        [SerializeField, Min(0)] int autosaveEveryDays = 7;

        public static GameSession Instance { get; private set; }

        public WorldMapDatabase WorldDatabase => worldDatabase;
        public EconomyConfig Economy => economyConfig;
        public ProgressionConfig ProgressionConfig => progressionConfig;
        public BusCatalog Catalog => busCatalog;

        public WorldGraph World { get; private set; }
        public CompanyState State { get; private set; }
        public SimulationClock Clock { get; private set; }
        public FinanceSystem Finance { get; private set; }
        public ProgressionSystem Progression { get; private set; }
        public FleetSystem Fleet { get; private set; }
        public RouteSystem Routes { get; private set; }
        public StationSystem Stations { get; private set; }
        public bool IsRunning => State != null;
        public bool IsBankrupt { get; private set; }

        /// <summary>1× hızda bir oyun gününün gerçek saniye karşılığı (ayarlar menüsünden değiştirilebilir).</summary>
        public float SecondsPerDay
        {
            get => secondsPerDay;
            set
            {
                secondsPerDay = Mathf.Max(0.1f, value);
                if (Clock != null) Clock.SecondsPerDay = secondsPerDay;
            }
        }
        public CityData Headquarters => State != null ? World.GetCity(State.startCityId) : null;

        /// <summary>Yeni oyun veya kayıt yüklendiğinde (sistemler yeniden kurulduğunda) tetiklenir.</summary>
        public event Action SessionStarted;
        public event Action<DayReport> DayCompleted;
        public event Action<int> MonthCompleted;
        public event Action Bankrupt;

        System.Random rng;
        DayReport lastReport;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (worldDatabase == null || economyConfig == null || progressionConfig == null || busCatalog == null)
            {
                Debug.LogError("[GameSession] Tasarım verisi atanmamış. 'Bus Tycoon/Create Default Content' ile oluşturup Inspector'da bağlayın.", this);
                enabled = false;
                return;
            }

            var errors = new List<string>();
            if (!worldDatabase.Validate(errors))
                foreach (var error in errors) Debug.LogError("[World] " + error);

            World = new WorldGraph(worldDatabase);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Sahnede asset bağlamadan, kodla kurulum için (bkz. <c>GameBootstrap</c>). GameObject pasifken,
        /// yani Awake'ten ÖNCE çağrılmalıdır.
        /// </summary>
        public void Configure(WorldMapDatabase world, EconomyConfig economy, ProgressionConfig progression, BusCatalog catalog)
        {
            worldDatabase = world;
            economyConfig = economy;
            progressionConfig = progression;
            busCatalog = catalog;
        }

        void Update() => Clock?.Tick(Time.unscaledDeltaTime);

        // ------------------------------------------------------------------ oturum

        public void StartNewGame(string companyName, CityData startCity)
        {
            var state = new CompanyState
            {
                companyName = companyName,
                startCityId = startCity.Id,
                cash = progressionConfig.StartingCash,
                reputation = progressionConfig.StartingReputation
            };

            Build(state);
            Progression.GrantRegion(startCity.Region);
            Progression.RefreshUnlocks(silent: true);
            if (progressionConfig.StartingBus != null)
                Fleet.GrantBus(progressionConfig.StartingBus, progressionConfig.StartingBusCondition);

            SessionStarted?.Invoke();
        }

        public void LoadGame(CompanyState state)
        {
            Build(state);
            Fleet.ResolveModels();
            SessionStarted?.Invoke();
        }

        public bool TryLoadGame(string slot)
        {
            if (!SaveSystem.TryLoad(slot, out CompanyState state)) return false;
            LoadGame(state);
            return true;
        }

        public bool SaveGame(string slot)
        {
            if (State == null) return false;
            try
            {
                SaveSystem.Save(State, slot);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] Kayıt başarısız ({slot}): {e.Message}");
                return false;
            }
        }

        void Build(CompanyState state)
        {
            if (Clock != null)
            {
                Clock.DayEnded -= HandleDayEnded;
                Clock.MonthEnded -= HandleMonthEnded;
            }

            State = state;
            IsBankrupt = false;
            rng = randomSeed != 0 ? new System.Random(randomSeed) : new System.Random();

            Finance = new FinanceSystem(state);
            Progression = new ProgressionSystem(state, progressionConfig, busCatalog, economyConfig, Finance);
            Fleet = new FleetSystem(state, busCatalog, economyConfig, Finance, Progression);
            Stations = new StationSystem(state, Finance, Progression);
            Routes = new RouteSystem(state, World, economyConfig, Fleet, Finance, Progression, Stations);

            Clock = new SimulationClock(state.currentDay, secondsPerDay);
            Clock.DayEnded += HandleDayEnded;
            Clock.MonthEnded += HandleMonthEnded;
        }

        // ------------------------------------------------------------------ günlük döngü

        /// <summary>
        /// Günlük simülasyon sırası:
        ///  1) Filo: otomatik bakım ve arıza zarları  2) Hatlar: talep → yolcu → gelir/gider  3) Garajdaki otobüslerin sigortası
        ///  4) İlerleme: XP + itibar  5) Finans: gün kapanışı  6) Olay yayını, otomatik kayıt, iflas kontrolü
        /// </summary>
        void HandleDayEnded(int day)
        {
            State.currentDay = day;
            var report = new DayReport { Day = day };

            Fleet.RunAutoMaintenance();
            Fleet.BeginDay(rng);
            Routes.SimulateDay(rng, report.Routes);
            Fleet.ChargeIdleInsurance();

            double satisfactionSum = 0;
            foreach (var routeReport in report.Routes)
            {
                report.Passengers += routeReport.passengers;
                satisfactionSum += routeReport.satisfaction * routeReport.passengers;
            }

            if (report.Passengers > 0)
            {
                report.AverageSatisfaction = (float)(satisfactionSum / report.Passengers);
                Progression.AddPassengers(report.Passengers);
                Progression.ApplyDailySatisfaction(report.AverageSatisfaction);
            }

            report.Finance = Finance.CloseDay(day);
            State.currentDay = day + 1;
            lastReport = report;
            DayCompleted?.Invoke(report);

            if (autosaveEveryDays > 0 && (day + 1) % autosaveEveryDays == 0 && !IsBankrupt) SaveGame(autosaveSlot);

            if (Finance.DaysInDebt >= progressionConfig.BankruptcyGraceDays && !IsBankrupt)
            {
                IsBankrupt = true;
                Clock.SetSpeed(0);
                Bankrupt?.Invoke();
            }
        }

        void HandleMonthEnded(int month) => MonthCompleted?.Invoke(month);

        public DayReport LastReport => lastReport;
    }
}
