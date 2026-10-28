using System;
using System.Collections.Generic;
using BusTycoon.Data;
using UnityEngine;

namespace BusTycoon.Economy
{
    /// <summary>Fiyat kaydırıcısının renk bölgeleri.</summary>
    public enum PriceZone
    {
        Loss,          // Kırmızı: tahminen zarar
        TooCheap,      // Turuncu: dolu ama kâr kaçıyor
        Good,          // Sarı: makul
        Ideal,         // Yeşil: tahmini en iyi kârın %95'i ve üstü
        TooExpensive   // Turuncu: yolcu kaçıyor
    }

    [Serializable]
    public struct PriceCurvePoint
    {
        public float Price;
        public float Passengers;
        public float LoadFactor;
        public float Revenue;
        public float Profit;
    }

    /// <summary>Fiyat paneli için hazır veri: eğri, kaydırıcı sınırları ve tahmini en iyi fiyat.</summary>
    public sealed class PricingAnalysis
    {
        public const float IdealThreshold = 0.95f;
        public const float GoodThreshold = 0.80f;

        public readonly List<PriceCurvePoint> Curve = new List<PriceCurvePoint>();
        public float MinPrice;
        public float MaxPrice;
        public float ReferencePrice;
        public float EstimatedBestPrice;
        public float EstimatedBestProfit;
        /// <summary>Tahminin hata payı (±oran). Pazar araştırması seviyesiyle küçülür.</summary>
        public float EstimateError;

        public PriceZone Classify(float price, float estimatedProfit)
        {
            if (estimatedProfit < 0f) return PriceZone.Loss;
            if (EstimatedBestProfit <= 0f) return PriceZone.Good;

            float ratio = estimatedProfit / EstimatedBestProfit;
            if (ratio >= IdealThreshold) return PriceZone.Ideal;
            if (ratio >= GoodThreshold) return PriceZone.Good;
            return price < EstimatedBestPrice ? PriceZone.TooCheap : PriceZone.TooExpensive;
        }
    }

    /// <summary>
    /// UI'daki fiyat önerilerini üretir. ÖNEMLİ: Kullandığı tahmin fonksiyonu pazar araştırması
    /// seviyesine göre "yanlı" olabilir (bkz. RouteSystem.CreateEstimator). Böylece oyuna ilk başlayan
    /// oyuncu ideal fiyatı tam bilemez; hat geçmişine bakarak deneyerek bulur — oyunun beceri kısmı budur.
    /// </summary>
    public static class PricingAdvisor
    {
        public static void Analyze(Func<float, RouteForecast> estimate, float referencePrice, float estimateError,
            EconomyConfig cfg, int curveSamples, PricingAnalysis result)
        {
            result.Curve.Clear();
            result.ReferencePrice = referencePrice;
            result.EstimateError = estimateError;
            result.MinPrice = referencePrice * cfg.MinPriceFactor;
            result.MaxPrice = referencePrice * cfg.MaxPriceFactor;

            result.EstimatedBestPrice = PriceOptimizer.FindBestPrice(p => estimate(p).Profit, result.MinPrice, result.MaxPrice);
            result.EstimatedBestProfit = estimate(result.EstimatedBestPrice).Profit;

            curveSamples = Mathf.Max(2, curveSamples);
            for (int i = 0; i < curveSamples; i++)
            {
                float price = Mathf.Lerp(result.MinPrice, result.MaxPrice, i / (float)(curveSamples - 1));
                RouteForecast f = estimate(price);
                result.Curve.Add(new PriceCurvePoint
                {
                    Price = price,
                    Passengers = f.Passengers,
                    LoadFactor = f.LoadFactor,
                    Revenue = f.Revenue,
                    Profit = f.Profit
                });
            }
        }

        public static string Describe(PriceZone zone) => zone switch
        {
            PriceZone.Loss => "Zarar ediyorsunuz",
            PriceZone.TooCheap => "Çok ucuz — otobüsler dolu ama kâr kaçıyor",
            PriceZone.Good => "Makul fiyat",
            PriceZone.Ideal => "İdeal bölge",
            PriceZone.TooExpensive => "Çok pahalı — yolcu kaçıyor",
            _ => string.Empty
        };
    }
}
