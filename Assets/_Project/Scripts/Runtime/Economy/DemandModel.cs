using System;
using BusTycoon.Data;
using UnityEngine;

namespace BusTycoon.Economy
{
    /// <summary>
    /// Talep tarafının saf matematiği (durum tutmaz, birim test edilebilir).
    ///
    /// 1) Potansiyel talep — çekim (gravity) modeli:
    ///      Q_pot = K × (Pop_A × Pop_B)^α × f(d) × turizm
    ///
    /// 2) Referans fiyat:
    ///      P_ref = (taban + km_ücreti × d) × bölge_çarpanı × segment_çarpanı
    ///
    /// 3) Fiyat esnekliği — yarı-logaritmik (üstel) talep eğrisi:
    ///      Q(P) = Q_ref × exp(−ε_ref × (P / P_ref − 1))
    ///    Nokta esnekliği fiyatla doğrusal artar: ε(P) = ε_ref × P / P_ref
    ///    → Ucuz bilette yolcu fiyata pek duyarlı değildir, pahalı bilette çok duyarlıdır.
    ///    → Gelir (P × Q) TEK bir tepe noktasına sahiptir: ε(P) = 1 → P = P_ref / ε_ref.
    ///    → Yolcu başı marjinal maliyet c ise kârı maksimize eden fiyat: P* = c + P_ref / ε_ref.
    ///
    /// 4) Kapasite: taşınan yolcu = yumuşak-min(talep, koltuk). Kapasiteye yaklaşırken
    ///    günlük dalgalanma nedeniyle bazı yolcular yer bulamaz; doluluk %100'e asimptotik yaklaşır.
    /// </summary>
    public static class DemandModel
    {
        public static float PotentialDemand(CityData a, CityData b, float roadKm, EconomyConfig cfg)
        {
            double gravity = Math.Pow(Math.Max(0.0, a.PopulationMillions * b.PopulationMillions), cfg.PopulationExponent);
            float tourism = 0.5f * (a.TourismIndex + b.TourismIndex);
            return (float)(cfg.DemandScale * gravity) * cfg.EvaluateDistanceDemand(roadKm) * tourism;
        }

        public static float ReferenceFare(float roadKm, float priceMultiplier, EconomyConfig cfg) =>
            (cfg.BaseFare + cfg.FarePerKm * roadKm) * priceMultiplier;

        /// <summary>Q(P) = Q_ref × exp(−ε_ref × (P/P_ref − 1))</summary>
        public static float DemandAtPrice(float referenceDemand, float price, float referencePrice, float elasticityAtReference)
        {
            if (referenceDemand <= 0f || referencePrice <= 0f) return 0f;
            float ratio = Mathf.Max(0f, price) / referencePrice;
            return referenceDemand * Mathf.Exp(-elasticityAtReference * (ratio - 1f));
        }

        /// <summary>ε(P) = −(dQ/dP) × (P/Q) = ε_ref × P/P_ref</summary>
        public static float PointElasticity(float price, float referencePrice, float elasticityAtReference) =>
            referencePrice <= 0f ? 0f : elasticityAtReference * price / referencePrice;

        /// <summary>Maliyet ve kapasite yokken geliri maksimize eden fiyat (ε(P) = 1 noktası).</summary>
        public static float RevenueMaximizingPrice(float referencePrice, float elasticityAtReference) =>
            elasticityAtReference <= 0f ? float.PositiveInfinity : referencePrice / elasticityAtReference;

        /// <summary>
        /// Yolcu başı sabit marjinal maliyet varken kârı maksimize eden fiyat (kapasite kısıtı yokken).
        /// Lerner koşulu: (P − c) / P = 1 / ε(P)  →  P* = c + P_ref / ε_ref
        /// </summary>
        public static float ProfitMaximizingPrice(float marginalCostPerPassenger, float referencePrice, float elasticityAtReference) =>
            marginalCostPerPassenger + RevenueMaximizingPrice(referencePrice, elasticityAtReference);

        /// <summary>
        /// Yumuşak minimum: (D^−n + C^−n)^(−1/n). n → ∞ iken min(D, C)'ye eşittir.
        /// Sayısal kararlılık için min × (1 + (min/max)^n)^(−1/n) biçiminde hesaplanır.
        /// </summary>
        public static float CarriedPassengers(float demand, float capacity, float smoothness)
        {
            if (demand <= 0f || capacity <= 0f) return 0f;
            float lo = Mathf.Min(demand, capacity);
            float hi = Mathf.Max(demand, capacity);
            float r = lo / hi;
            return lo * Mathf.Pow(1f + Mathf.Pow(r, smoothness), -1f / smoothness);
        }

        public static float ReputationFactor(float reputation0To100, EconomyConfig cfg) =>
            Mathf.Lerp(cfg.ReputationDemandAtZero, cfg.ReputationDemandAtMax, Mathf.Clamp01(reputation0To100 / 100f));

        /// <summary>Yeni hat bilinirliği: açılışta newRouteDemandFactor, zamanla 1'e yaklaşır.</summary>
        public static float RampUpFactor(int daysOpen, EconomyConfig cfg)
        {
            if (cfg.RouteRampUpDays <= 0f) return 1f;
            float start = cfg.NewRouteDemandFactor;
            return start + (1f - start) * (1f - Mathf.Exp(-Mathf.Max(0, daysOpen) / cfg.RouteRampUpDays));
        }

        /// <summary>Küçük/rahatsız araçla uzun hat: konforlu menzil aşıldıkça talep üstel düşer.</summary>
        public static float ComfortRangeFactor(float roadKm, float comfortableRangeKm, EconomyConfig cfg)
        {
            if (comfortableRangeKm <= 0f || roadKm <= comfortableRangeKm) return 1f;
            float excess = (roadKm - comfortableRangeKm) / comfortableRangeKm;
            return Mathf.Exp(-cfg.DiscomfortSteepness * excess);
        }

        /// <summary>
        /// Günlük yolcu memnuniyeti (0..1): konfor artırır; referansın üstündeki fiyat ve
        /// bilet bulamayan yolcu oranı düşürür. Şirket itibarını yavaşça bu değere çeker.
        /// </summary>
        public static float Satisfaction(float priceRatio, float comfort, float unmetRatio, EconomyConfig cfg)
        {
            float s = cfg.SatisfactionBase
                      + cfg.SatisfactionComfortWeight * comfort
                      - cfg.SatisfactionPriceWeight * (priceRatio - 1f)
                      - cfg.SatisfactionCrowdingWeight * unmetRatio;
            return Mathf.Clamp01(s);
        }
    }
}
