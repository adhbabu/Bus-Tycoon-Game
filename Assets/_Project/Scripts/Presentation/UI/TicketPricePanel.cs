using System;
using System.Collections.Generic;
using BusTycoon.Core;
using BusTycoon.Economy;
using BusTycoon.Routes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusTycoon.Presentation.UI
{
    /// <summary>
    /// Bilet fiyatı paneli — oyunun kalbi. Tek kaydırıcı + canlı önizleme:
    ///  • Kaydırıcı oynarken yolcu, doluluk, gelir ve kâr ANINDA güncellenir (tahmin, gerçek değil).
    ///  • Kâr eğrisi üzerinde "ideal bölge" bandı ve tahmini en iyi fiyat gösterilir.
    ///  • Tahminler pazar araştırması seviyesine göre yanlı olabilir; doğruluk etikette yazar.
    ///  • "Uygula" basılana kadar hiçbir şey değişmez; "Geri al" tek tıkla mevcut fiyata döner.
    /// </summary>
    public sealed class TicketPricePanel : UIScreen
    {
        [Header("Kontroller")]
        [SerializeField] Slider priceSlider;
        [SerializeField] Button decreaseButton;
        [SerializeField] Button increaseButton;
        [SerializeField] Button suggestedButton;
        [SerializeField] Button applyButton;
        [SerializeField] Button revertButton;
        [SerializeField] Button closeButton;
        [Tooltip("Gelişmiş: otomatik sefer planlamasını kapat (varsayılan açık).")]
        [SerializeField] Toggle autoScheduleToggle;

        [Header("Metinler")]
        [SerializeField] TMP_Text routeTitleLabel;
        [SerializeField] TMP_Text priceLabel;
        [SerializeField] TMP_Text referencePriceLabel;
        [SerializeField] TMP_Text passengersLabel;
        [SerializeField] TMP_Text loadFactorLabel;
        [SerializeField] TMP_Text revenueLabel;
        [SerializeField] TMP_Text profitLabel;
        [SerializeField] TMP_Text profitDeltaLabel;
        [SerializeField] TMP_Text zoneLabel;
        [SerializeField] TMP_Text insightLabel;
        [SerializeField] TMP_Text lastResultLabel;

        [Header("Görsel geri bildirim")]
        [SerializeField] Image zoneBadge;
        [SerializeField] Image loadFactorFill;
        [SerializeField] UILineGraph profitGraph;
        [SerializeField] Color idealColor = new Color(0.3f, 0.85f, 0.45f);
        [SerializeField] Color goodColor = new Color(0.95f, 0.85f, 0.25f);
        [SerializeField] Color warningColor = new Color(1f, 0.55f, 0.2f);
        [SerializeField] Color lossColor = new Color(0.95f, 0.3f, 0.3f);

        [Header("Ayarlar")]
        [SerializeField, Range(10, 80)] int curveSamples = 40;

        readonly PricingAnalysis analysis = new PricingAnalysis();
        readonly List<Vector2> curvePoints = new List<Vector2>();
        Func<float, RouteForecast> estimator;
        RouteState route;
        float pendingPrice;
        bool suppressSliderEvent;

        protected override void OnInitialized()
        {
            priceSlider.onValueChanged.AddListener(value => { if (!suppressSliderEvent) SetPendingPrice(value); });
            decreaseButton.onClick.AddListener(() => SetPendingPrice(pendingPrice - PriceStep(pendingPrice)));
            increaseButton.onClick.AddListener(() => SetPendingPrice(pendingPrice + PriceStep(pendingPrice)));
            suggestedButton.onClick.AddListener(() => SetPendingPrice(analysis.EstimatedBestPrice));
            revertButton.onClick.AddListener(() => SetPendingPrice(route.ticketPrice));
            applyButton.onClick.AddListener(Apply);
            closeButton.onClick.AddListener(() => Flow.Back());
            autoScheduleToggle.onValueChanged.AddListener(OnAutoScheduleChanged);
        }

        public void Bind(RouteState targetRoute)
        {
            route = targetRoute;
            Rebuild();
        }

        protected override void OnShown()
        {
            Session.DayCompleted += HandleDayCompleted;
            if (route != null) Rebuild();
        }

        protected override void OnHidden() => Session.DayCompleted -= HandleDayCompleted;

        void HandleDayCompleted(DayReport _)
        {
            // Filo / itibar değişmiş olabilir: eğriyi yenile ama oyuncunun seçtiği fiyatı koru.
            float keep = pendingPrice;
            Rebuild();
            SetPendingPrice(keep);
        }

        void Rebuild()
        {
            if (route == null || Session == null || !Session.IsRunning) return;

            var a = Session.Routes.GetCityA(route);
            var b = Session.Routes.GetCityB(route);
            routeTitleLabel.text = $"{a.DisplayName} ⇄ {b.DisplayName} · {route.distanceKm:N0} km";

            estimator = Session.Routes.CreateEstimator(route);
            Session.Routes.AnalyzePricing(route, curveSamples, analysis);

            curvePoints.Clear();
            float bandFrom = float.MaxValue, bandTo = float.MinValue;
            foreach (var point in analysis.Curve)
            {
                curvePoints.Add(new Vector2(point.Price, point.Profit));
                if (analysis.EstimatedBestProfit > 0f && point.Profit >= analysis.EstimatedBestProfit * PricingAnalysis.IdealThreshold)
                {
                    bandFrom = Mathf.Min(bandFrom, point.Price);
                    bandTo = Mathf.Max(bandTo, point.Price);
                }
            }
            profitGraph.SetSeries(curvePoints, Color.white);
            if (bandTo >= bandFrom) profitGraph.SetBand(bandFrom, bandTo);
            else profitGraph.SetBand(null, null);

            suppressSliderEvent = true;
            priceSlider.minValue = analysis.MinPrice;
            priceSlider.maxValue = analysis.MaxPrice;
            suppressSliderEvent = false;

            referencePriceLabel.text = $"Piyasa referansı: {UIFormat.Price(analysis.ReferencePrice)}";
            insightLabel.text = analysis.EstimateError <= 0f
                ? "Pazar araştırması: tam isabetli tahmin"
                : $"Pazar araştırması: tahmin ±%{analysis.EstimateError * 100f:0} sapabilir";

            autoScheduleToggle.SetIsOnWithoutNotify(route.autoSchedule);
            UpdateLastResult();
            SetPendingPrice(route.ticketPrice);
        }

        void SetPendingPrice(float price)
        {
            if (estimator == null) return;
            pendingPrice = RoundPrice(Mathf.Clamp(price, analysis.MinPrice, analysis.MaxPrice));

            suppressSliderEvent = true;
            priceSlider.value = pendingPrice;
            suppressSliderEvent = false;

            RouteForecast preview = estimator(pendingPrice);
            RouteForecast currentPlan = estimator(route.ticketPrice);
            float delta = preview.Profit - currentPlan.Profit;

            priceLabel.text = UIFormat.Price(pendingPrice);
            passengersLabel.text = $"{UIFormat.Number(preview.Passengers)} yolcu/gün";
            loadFactorLabel.text = $"Doluluk {UIFormat.Percent(preview.LoadFactor)} · {preview.TripsPerDay:0.#} sefer/gün";
            if (loadFactorFill != null) loadFactorFill.fillAmount = preview.LoadFactor;
            revenueLabel.text = $"Gelir {UIFormat.Money(preview.Revenue)}/gün";
            profitLabel.text = $"Kâr {UIFormat.Money(preview.Profit)}/gün";

            bool changed = Mathf.Abs(pendingPrice - route.ticketPrice) > 0.001f;
            profitDeltaLabel.text = changed ? $"{UIFormat.SignedMoney(delta)}/gün" : "Mevcut fiyat";
            profitDeltaLabel.color = !changed ? Color.white : delta >= 0f ? idealColor : lossColor;
            applyButton.interactable = changed;
            revertButton.interactable = changed;

            PriceZone zone = analysis.Classify(pendingPrice, preview.Profit);
            zoneLabel.text = PricingAdvisor.Describe(zone);
            zoneBadge.color = ZoneColor(zone);

            profitGraph.SetMarkers(pendingPrice, analysis.EstimatedBestPrice);
        }

        void Apply()
        {
            var result = Session.Routes.SetTicketPrice(route.routeId, pendingPrice);
            if (!result) Debug.LogWarning(result.Message);
            SetPendingPrice(pendingPrice);
        }

        void OnAutoScheduleChanged(bool auto)
        {
            Session.Routes.SetSchedule(route.routeId, auto, route.manualUtilization);
            Rebuild();
        }

        /// <summary>Tahmin ile gerçeği yan yana göstermek, oyuncunun yanlı tahmini öğrenmesini sağlar.</summary>
        void UpdateLastResult()
        {
            if (lastResultLabel == null) return;
            if (!route.HasHistory)
            {
                lastResultLabel.text = "Henüz sonuç yok";
                return;
            }

            RouteDayReport last = route.LastReport;
            lastResultLabel.text =
                $"Dün ({UIFormat.Price(last.ticketPrice)}): {UIFormat.Number(last.passengers)} yolcu · " +
                $"doluluk {UIFormat.Percent(last.loadFactor)} · kâr {UIFormat.Money(last.Profit)}";
        }

        Color ZoneColor(PriceZone zone) => zone switch
        {
            PriceZone.Ideal => idealColor,
            PriceZone.Good => goodColor,
            PriceZone.Loss => lossColor,
            _ => warningColor
        };

        /// <summary>Ucuz biletlerde 0,10; pahalılarda 0,50 adım — fiyatlar "gerçekçi" görünür.</summary>
        static float PriceStep(float price) => price < 20f ? 0.1f : 0.5f;

        static float RoundPrice(float price)
        {
            float step = PriceStep(price);
            return Mathf.Max(step, Mathf.Round(price / step) * step);
        }
    }
}
