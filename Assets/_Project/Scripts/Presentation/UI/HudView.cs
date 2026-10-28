using BusTycoon.Core;
using BusTycoon.Data;
using BusTycoon.Economy;
using BusTycoon.Fleet;
using BusTycoon.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusTycoon.Presentation.UI
{
    /// <summary>
    /// Her zaman görünen üst şerit: kasa (+ dünkü net), tarih ve hız kontrolleri, şirket seviyesi/XP,
    /// itibar, hat slotu ve ana menü kısayolları. Ayrıca önemli olayları kısa bildirim (toast) olarak gösterir.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] UIFlowController flow;

        [Header("Şirket")]
        [SerializeField] TMP_Text companyLabel;
        [SerializeField] TMP_Text cashLabel;
        [SerializeField] TMP_Text dailyNetLabel;
        [SerializeField] TMP_Text levelLabel;
        [SerializeField] Image xpFill;
        [SerializeField] TMP_Text reputationLabel;
        [SerializeField] TMP_Text routeSlotsLabel;

        [Header("Zaman")]
        [SerializeField] TMP_Text dateLabel;
        [SerializeField] Image dayProgressFill;
        [SerializeField] Button pauseButton;
        [SerializeField] Button speed1Button;
        [SerializeField] Button speed2Button;
        [SerializeField] Button speed4Button;

        [Header("Kısayollar")]
        [SerializeField] Button routesButton;
        [SerializeField] Button newRouteButton;
        [SerializeField] Button dealershipButton;

        [Header("Bildirim")]
        [SerializeField] TMP_Text toastLabel;
        [SerializeField, Min(0.5f)] float toastSeconds = 4f;

        [SerializeField] Color positiveColor = new Color(0.3f, 0.85f, 0.45f);
        [SerializeField] Color negativeColor = new Color(0.95f, 0.3f, 0.3f);

        GameSession session;
        FinanceSystem finance;
        ProgressionSystem progression;
        FleetSystem fleet;
        float toastTimer;

        void Start()
        {
            session = GameSession.Instance;
            session.SessionStarted += Bind;
            session.DayCompleted += HandleDayCompleted;
            session.Bankrupt += () => ShowToast("İFLAS! Kasa uzun süre eksiye düştü.");

            pauseButton.onClick.AddListener(() => session.Clock?.TogglePause());
            speed1Button.onClick.AddListener(() => session.Clock?.SetSpeed(1));
            speed2Button.onClick.AddListener(() => session.Clock?.SetSpeed(2));
            speed4Button.onClick.AddListener(() => session.Clock?.SetSpeed(4));
            routesButton.onClick.AddListener(() => flow.Show(UIScreenId.RouteList));
            newRouteButton.onClick.AddListener(() => flow.Show(UIScreenId.RoutePlanner));
            dealershipButton.onClick.AddListener(flow.OpenDealership);

            if (toastLabel != null) toastLabel.gameObject.SetActive(false);
            if (session.IsRunning) Bind();
        }

        void OnDestroy()
        {
            if (session == null) return;
            session.SessionStarted -= Bind;
            session.DayCompleted -= HandleDayCompleted;
            Unbind();
        }

        void Bind()
        {
            Unbind();
            finance = session.Finance;
            progression = session.Progression;
            fleet = session.Fleet;

            finance.CashChanged += HandleCashChanged;
            progression.LevelChanged += HandleLevelChanged;
            progression.ModelUnlocked += HandleModelUnlocked;
            fleet.BusBrokeDown += HandleBreakdown;

            companyLabel.text = session.State.companyName;
            RefreshAll();
        }

        void Unbind()
        {
            if (finance != null) finance.CashChanged -= HandleCashChanged;
            if (progression != null)
            {
                progression.LevelChanged -= HandleLevelChanged;
                progression.ModelUnlocked -= HandleModelUnlocked;
            }
            if (fleet != null) fleet.BusBrokeDown -= HandleBreakdown;
        }

        void Update()
        {
            if (session == null || !session.IsRunning) return;
            if (dayProgressFill != null) dayProgressFill.fillAmount = session.Clock.DayProgress01;

            if (toastTimer > 0f)
            {
                toastTimer -= Time.unscaledDeltaTime;
                if (toastTimer <= 0f) toastLabel.gameObject.SetActive(false);
            }
        }

        void RefreshAll()
        {
            HandleCashChanged(finance.Cash);
            dateLabel.text = SimulationClock.FormatDate(session.State.currentDay);
            levelLabel.text = $"Sv. {progression.Level}";
            xpFill.fillAmount = progression.LevelProgress01;
            reputationLabel.text = $"İtibar {progression.Reputation:0}";
            routeSlotsLabel.text = $"Hat {session.Routes.Routes.Count}/{progression.MaxRoutes}";
        }

        void HandleCashChanged(double cash)
        {
            cashLabel.text = UIFormat.MoneyCompact(cash);
            cashLabel.color = cash < 0 ? negativeColor : Color.white;
        }

        void HandleDayCompleted(DayReport report)
        {
            double net = report.Finance.Net;
            dailyNetLabel.text = UIFormat.SignedMoney(net);
            dailyNetLabel.color = net >= 0 ? positiveColor : negativeColor;
            RefreshAll();
        }

        void HandleLevelChanged(int level) =>
            ShowToast($"Şirket seviyesi {level}! Hat limiti: {progression.MaxRoutes}");

        void HandleModelUnlocked(BusModelData model) =>
            ShowToast($"Yeni otobüs açıldı: {model.DisplayName} — Galeriye göz atın");

        void HandleBreakdown(BusInstance bus) =>
            ShowToast($"{bus.nickname} arızalandı ({bus.outOfServiceDays} gün servis dışı)");

        public void ShowToast(string message)
        {
            if (toastLabel == null) return;
            toastLabel.text = message;
            toastLabel.gameObject.SetActive(true);
            toastTimer = toastSeconds;
        }
    }
}
