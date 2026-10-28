using System;
using BusTycoon.Routes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusTycoon.Presentation.UI
{
    /// <summary>
    /// "Hatlarım" tepsisindeki kompakt hat kartı: başlık, günlük kâr, doluluk çubuğu ve
    /// <see cref="RouteAdvisor"/>'ün tek önerisi + o öneriyi uygulayan tek buton.
    /// </summary>
    public sealed class RouteCardView : MonoBehaviour
    {
        [SerializeField] TMP_Text titleLabel;
        [SerializeField] TMP_Text profitLabel;
        [SerializeField] TMP_Text detailLabel;
        [SerializeField] TMP_Text insightLabel;
        [SerializeField] Image loadFactorFill;
        [SerializeField] Image statusStripe;
        [SerializeField] Button priceButton;
        [SerializeField] Button actionButton;
        [SerializeField] TMP_Text actionButtonLabel;

        [SerializeField] Color okColor = new Color(0.3f, 0.85f, 0.45f);
        [SerializeField] Color warningColor = new Color(1f, 0.7f, 0.2f);
        [SerializeField] Color criticalColor = new Color(0.95f, 0.3f, 0.3f);

        RouteState route;
        RouteInsight insight;

        public event Action<RouteState> PriceRequested;
        public event Action<RouteState, InsightAction> ActionRequested;

        void Awake()
        {
            priceButton.onClick.AddListener(() => PriceRequested?.Invoke(route));
            actionButton.onClick.AddListener(() => ActionRequested?.Invoke(route, insight.Action));
        }

        public void Bind(RouteState routeState, string title, int busCount)
        {
            route = routeState;
            insight = RouteAdvisor.GetTopInsight(routeState, busCount);
            RouteDayReport last = routeState.LastReport;

            titleLabel.text = title;
            profitLabel.text = routeState.HasHistory ? $"{UIFormat.SignedMoney(last.Profit)}/gün" : "—";
            detailLabel.text = $"{busCount} otobüs · {UIFormat.Price(routeState.ticketPrice)} · {UIFormat.Number(last.passengers)} yolcu";
            loadFactorFill.fillAmount = last.loadFactor;

            insightLabel.text = insight.Message;
            statusStripe.color = insight.Severity switch
            {
                InsightSeverity.Critical => criticalColor,
                InsightSeverity.Warning => warningColor,
                _ => okColor
            };

            actionButton.gameObject.SetActive(insight.Action != InsightAction.None && insight.Action != InsightAction.AdjustPrice);
            actionButtonLabel.text = insight.Action switch
            {
                InsightAction.AssignBus => "Otobüs ata",
                InsightAction.AddBus => "Otobüs ekle",
                InsightAction.UpgradeBus => "Yükselt",
                _ => string.Empty
            };
        }
    }
}
