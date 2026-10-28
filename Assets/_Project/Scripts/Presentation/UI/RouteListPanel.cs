using System.Collections.Generic;
using BusTycoon.Core;
using BusTycoon.Fleet;
using BusTycoon.Routes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusTycoon.Presentation.UI
{
    /// <summary>
    /// "Hatlarım" paneli: tüm hatlar, en acil sorun en üstte. Kart üzerindeki tek buton sorunu
    /// çözmeye en kısa yoldan götürür (boştaki otobüsü ata / galeriye git / fiyat paneli).
    /// </summary>
    public sealed class RouteListPanel : UIScreen
    {
        [SerializeField] RouteCardView cardPrefab;
        [SerializeField] Transform cardRoot;
        [SerializeField] TMP_Text summaryLabel;
        [SerializeField] TMP_Text feedbackLabel;
        [SerializeField] Button newRouteButton;
        [SerializeField] Button closeButton;

        readonly List<RouteCardView> cards = new List<RouteCardView>();
        readonly List<RouteState> sorted = new List<RouteState>();
        readonly List<BusInstance> idleBuses = new List<BusInstance>();

        protected override void OnInitialized()
        {
            newRouteButton.onClick.AddListener(() => Flow.Show(UIScreenId.RoutePlanner));
            closeButton.onClick.AddListener(() => Flow.Back());
        }

        protected override void OnShown()
        {
            Session.DayCompleted += HandleDayCompleted;
            feedbackLabel.text = string.Empty;
            Refresh();
        }

        protected override void OnHidden() => Session.DayCompleted -= HandleDayCompleted;

        void HandleDayCompleted(DayReport _) => Refresh();

        void Refresh()
        {
            var routes = Session.Routes;
            var fleet = Session.Fleet;

            sorted.Clear();
            sorted.AddRange(routes.Routes);
            sorted.Sort((x, y) =>
            {
                var ix = RouteAdvisor.GetTopInsight(x, fleet.CountBusesOnRoute(x.routeId));
                var iy = RouteAdvisor.GetTopInsight(y, fleet.CountBusesOnRoute(y.routeId));
                return ix.Severity != iy.Severity ? iy.Severity.CompareTo(ix.Severity) : y.LastReport.Profit.CompareTo(x.LastReport.Profit);
            });

            while (cards.Count < sorted.Count)
            {
                RouteCardView card = Instantiate(cardPrefab, cardRoot);
                card.PriceRequested += Flow.OpenPricing;
                card.ActionRequested += HandleAction;
                cards.Add(card);
            }

            double totalProfit = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                bool active = i < sorted.Count;
                cards[i].gameObject.SetActive(active);
                if (!active) continue;

                RouteState route = sorted[i];
                string title = $"{routes.GetCityA(route).DisplayName} ⇄ {routes.GetCityB(route).DisplayName}";
                cards[i].Bind(route, title, fleet.CountBusesOnRoute(route.routeId));
                totalProfit += route.LastReport.Profit;
            }

            summaryLabel.text = $"{sorted.Count}/{Session.Progression.MaxRoutes} hat · Dün toplam {UIFormat.SignedMoney(totalProfit)}";
        }

        void HandleAction(RouteState route, InsightAction action)
        {
            switch (action)
            {
                case InsightAction.AssignBus:
                case InsightAction.AddBus:
                    Session.Fleet.GetIdleBuses(idleBuses);
                    if (idleBuses.Count > 0)
                    {
                        Session.Routes.TryAssignBus(idleBuses[0].busId, route.routeId);
                        feedbackLabel.text = $"{idleBuses[0].nickname} hatta atandı.";
                        Refresh();
                    }
                    else
                    {
                        Flow.OpenDealership(); // garajda boş otobüs yok → galeriye
                    }
                    break;

                case InsightAction.UpgradeBus:
                    Flow.OpenDealership();
                    break;

                case InsightAction.AdjustPrice:
                    Flow.OpenPricing(route);
                    break;
            }
        }
    }
}
