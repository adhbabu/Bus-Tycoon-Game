using System.Collections.Generic;
using BusTycoon.Data;
using BusTycoon.Fleet;
using BusTycoon.Presentation.Map;
using BusTycoon.Routes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusTycoon.Presentation.UI
{
    /// <summary>
    /// "3 tıkla hat aç" akışı:
    ///   1) Haritada kalkış şehrine tıkla  2) Varış şehrine tıkla (teklif kartı anında dolar)
    ///   3) "Hattı Aç" → önerilen fiyatla açılır, garajdaki uygun otobüsler atanır, fiyat paneli açılır.
    /// Teklif kartında hangi araçla planlandığı seçilebilir; öneriler o araca göre yeniden hesaplanır.
    /// </summary>
    public sealed class RoutePlannerController : UIScreen
    {
        enum Step { PickOrigin, PickDestination, Review }

        [SerializeField] WorldMapView map;

        [Header("Yönlendirme")]
        [SerializeField] TMP_Text instructionLabel;

        [Header("Teklif kartı")]
        [SerializeField] GameObject quoteCard;
        [SerializeField] TMP_Text titleLabel;
        [SerializeField] TMP_Text distanceLabel;
        [SerializeField] TMP_Text licenseLabel;
        [SerializeField] TMP_Text demandLabel;
        [SerializeField] TMP_Text suggestedPriceLabel;
        [SerializeField] TMP_Text busPlanLabel;
        [SerializeField] TMP_Text profitLabel;
        [SerializeField] TMP_Text errorLabel;
        [SerializeField] TMP_Dropdown modelDropdown;
        [SerializeField] Toggle assignIdleBusesToggle;
        [SerializeField] Button openButton;
        [SerializeField] Button swapButton;
        [SerializeField] Button cancelButton;

        readonly List<BusModelData> modelOptions = new List<BusModelData>();
        readonly List<BusInstance> idleBuses = new List<BusInstance>();
        Step step;
        CityData origin;
        CityData destination;
        RouteQuote quote;

        protected override void OnInitialized()
        {
            openButton.onClick.AddListener(OpenRoute);
            swapButton.onClick.AddListener(SwapCities);
            cancelButton.onClick.AddListener(() => Flow.Back());
            modelDropdown.onValueChanged.AddListener(_ => RefreshQuote());
        }

        protected override void OnShown()
        {
            map.CityClicked += HandleCityClicked;
            FillModelDropdown();
            ResetSelection();
        }

        protected override void OnHidden()
        {
            map.CityClicked -= HandleCityClicked;
            map.HidePreview();
            map.ClearHighlights();
        }

        void ResetSelection()
        {
            origin = destination = null;
            step = Step.PickOrigin;
            map.HidePreview();
            map.ClearHighlights();
            quoteCard.SetActive(false);
            instructionLabel.text = "Kalkış şehrini seçin";
        }

        void HandleCityClicked(CityData city)
        {
            switch (step)
            {
                case Step.PickOrigin:
                    origin = city;
                    map.HighlightCity(origin, CityNodeState.Origin);
                    instructionLabel.text = $"{origin.DisplayName} → varış şehrini seçin";
                    step = Step.PickDestination;
                    break;

                case Step.PickDestination:
                case Step.Review:
                    if (city == origin)
                    {
                        ResetSelection(); // kalkışa tekrar tıklamak seçimi iptal eder
                        return;
                    }
                    if (destination != null) map.HighlightCity(destination, null);
                    destination = city;
                    map.HighlightCity(destination, CityNodeState.Destination);
                    map.ShowPreview(origin, destination);
                    instructionLabel.text = "Başka bir varış şehrine tıklayarak karşılaştırabilirsiniz";
                    step = Step.Review;
                    RefreshQuote();
                    break;
            }
        }

        void SwapCities()
        {
            if (origin == null || destination == null) return;
            (origin, destination) = (destination, origin);
            map.HighlightCity(origin, CityNodeState.Origin);
            map.HighlightCity(destination, CityNodeState.Destination);
            RefreshQuote();
        }

        /// <summary>Açık modeller; varsayılan seçim garajda en çok bulunan boşta model, yoksa en ucuz açık model.</summary>
        void FillModelDropdown()
        {
            modelOptions.Clear();
            foreach (var model in Session.Catalog.SortedByTier)
                if (Session.Progression.IsModelUnlocked(model)) modelOptions.Add(model);

            var labels = new List<string>();
            foreach (var model in modelOptions) labels.Add($"{model.DisplayName} ({model.Seats} koltuk)");
            modelDropdown.ClearOptions();
            modelDropdown.AddOptions(labels);

            Session.Fleet.GetIdleBuses(idleBuses);
            int bestIndex = 0, bestCount = 0;
            for (int i = 0; i < modelOptions.Count; i++)
            {
                int count = idleBuses.FindAll(b => b.Model == modelOptions[i]).Count;
                if (count > bestCount) { bestCount = count; bestIndex = i; }
            }
            modelDropdown.SetValueWithoutNotify(bestIndex);
        }

        BusModelData SelectedModel =>
            modelOptions.Count == 0 ? null : modelOptions[Mathf.Clamp(modelDropdown.value, 0, modelOptions.Count - 1)];

        void RefreshQuote()
        {
            if (origin == null || destination == null || SelectedModel == null) return;

            quote = Session.Routes.QuoteRoute(origin, destination, SelectedModel);
            quoteCard.SetActive(true);

            Session.Fleet.GetIdleBuses(idleBuses);
            int idleOfModel = idleBuses.FindAll(b => b.Model == SelectedModel).Count;

            titleLabel.text = $"{origin.DisplayName} ⇄ {destination.DisplayName}";
            distanceLabel.text = $"{UIFormat.Number(quote.DistanceKm)} km · {UIFormat.Hours(quote.TravelHours)} yolculuk";
            licenseLabel.text = $"Hat lisansı: {UIFormat.Money(quote.LicenseCost)}";
            demandLabel.text = $"Tahmini talep: ~{UIFormat.Number(quote.Forecast.Demand)} yolcu/gün";
            suggestedPriceLabel.text = $"Önerilen bilet: {UIFormat.Price(quote.SuggestedPrice)}";
            busPlanLabel.text = $"Önerilen: {quote.RecommendedBusCount} × {SelectedModel.DisplayName} (garajda {idleOfModel})";
            profitLabel.text = $"Tahmini kâr: {UIFormat.Money(quote.Forecast.Profit)}/gün";
            profitLabel.color = quote.Forecast.Profit >= 0f ? Color.white : new Color(0.95f, 0.3f, 0.3f);

            errorLabel.gameObject.SetActive(!quote.IsValid);
            errorLabel.text = quote.Error;
            openButton.interactable = quote.IsValid;
        }

        void OpenRoute()
        {
            var result = Session.Routes.TryOpenRoute(origin, destination, quote.SuggestedPrice, out RouteState route);
            if (!result)
            {
                errorLabel.gameObject.SetActive(true);
                errorLabel.text = result.Message;
                return;
            }

            if (assignIdleBusesToggle.isOn) AssignIdleBuses(route, quote.RecommendedBusCount);

            ResetSelection();
            Flow.CloseAll();          // "Geri" fiyat panelinden planlayıcıya değil, haritaya dönsün
            Flow.OpenPricing(route);
        }

        /// <summary>Önce seçili modelden, sonra en yüksek kademeden başlayarak boştaki otobüsleri atar.</summary>
        void AssignIdleBuses(RouteState route, int count)
        {
            Session.Fleet.GetIdleBuses(idleBuses);
            idleBuses.Sort((x, y) =>
            {
                int preferX = x.Model == SelectedModel ? 0 : 1;
                int preferY = y.Model == SelectedModel ? 0 : 1;
                return preferX != preferY ? preferX.CompareTo(preferY) : y.Model.Tier.CompareTo(x.Model.Tier);
            });

            for (int i = 0; i < idleBuses.Count && i < count; i++)
                Session.Routes.TryAssignBus(idleBuses[i].busId, route.routeId);
        }
    }
}
