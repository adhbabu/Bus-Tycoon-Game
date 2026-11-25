using System.Collections.Generic;
using BusTycoon.Data;
using UnityEngine;

namespace BusTycoon.Urban
{
    /// <summary>Şehir içi hat planı: güzergâh, üstündeki duraklar ve mekânlar, talep potansiyeli.</summary>
    public sealed class UrbanLinePlan
    {
        public UrbanStop A, B;
        /// <summary>Yol ağındaki düğümler (A'dan B'ye).</summary>
        public readonly List<int> Path = new List<int>();
        public float Meters;
        /// <summary>Güzergâha yakın duraklar, güzergâh sırasıyla (uçlar dahil).</summary>
        public readonly List<UrbanStop> Stops = new List<UrbanStop>();
        /// <summary>Güzergâha yakın ünlü mekânlar.</summary>
        public readonly List<UrbanLandmark> Landmarks = new List<UrbanLandmark>();
        /// <summary>Olgun talep potansiyeli (yolcu/gün, referans fiyatta; itibar ve yeni hat çarpanı hariç).</summary>
        public float PotentialDemand;
        public string Error;

        public float Km => Meters / 1000f;
        public bool IsValid => string.IsNullOrEmpty(Error) && Path.Count >= 2;

        public void Clear()
        {
            A = B = null;
            Path.Clear();
            Stops.Clear();
            Landmarks.Clear();
            Meters = 0f;
            PotentialDemand = 0f;
            Error = null;
        }
    }

    /// <summary>
    /// İki durak arasında dolmuş hattı planlar (saf C#; oyun ve testler aynı hesabı kullanır).
    /// <para>Talep = K × nüfus_mn^α × (Σ(0,4 + durak önemi) + 0,15 × km) × uzunluk eğrisi × (1 + 0,15 × Σ mekân ünü).
    /// Km terimi duraklar arasındaki mahallelerin yolcusudur (her sokak başında dolmuş durur).
    /// Güzergâha <c>UrbanCatchmentMeters</c> yakın duraklar hattın durağıdır; mekânlar için iki katı mesafe.</para>
    /// </summary>
    public static class UrbanPlanner
    {
        /// <summary>Hat en az bu kadar uzun olmalı (daha kısası yürünür).</summary>
        public const float MinMeters = 1500f;
        /// <summary>En uzun dolmuş hattı.</summary>
        public const float MaxMeters = 40000f;

        public static bool TryPlan(UrbanMap map, UrbanStop a, UrbanStop b, CityData city, EconomyConfig cfg, UrbanLinePlan plan)
        {
            plan.Clear();
            plan.A = a;
            plan.B = b;
            if (map == null || a == null || b == null) return Fail(plan, "Durak seçilmedi.");
            if (a == b) return Fail(plan, "İki farklı durak seçin.");
            if (!map.FindPath(a.Node, b.Node, plan.Path, out float meters)) return Fail(plan, "Bu iki durak arasında yol bulunamadı.");
            plan.Meters = meters;
            if (meters < MinMeters) return Fail(plan, $"Hat çok kısa ({meters / 1000f:0.0} km): en az {MinMeters / 1000f:0.#} km olmalı.");
            if (meters > MaxMeters) return Fail(plan, $"Hat çok uzun ({meters / 1000f:0} km): dolmuş hattı en çok {MaxMeters / 1000f:0} km.");

            CollectNearby(map, plan, cfg.UrbanCatchmentMeters);
            plan.PotentialDemand = Demand(plan, city, cfg);
            return true;
        }

        static bool Fail(UrbanLinePlan plan, string error)
        {
            plan.Error = error;
            return false;
        }

        /// <summary>Güzergâha yakın durak ve mekânlar. Yol düğümleri seyreltilerek (~100 m) taranır.</summary>
        static void CollectNearby(UrbanMap map, UrbanLinePlan plan, float catchment)
        {
            var samples = new List<Vector2>();
            Vector2 last = map.NodePosition(plan.Path[0]);
            samples.Add(last);
            for (int i = 1; i < plan.Path.Count; i++)
            {
                Vector2 p = map.NodePosition(plan.Path[i]);
                if ((p - last).sqrMagnitude >= 100f * 100f || i == plan.Path.Count - 1)
                {
                    samples.Add(p);
                    last = p;
                }
            }

            var order = new List<(int index, UrbanStop stop)>();
            float stopRange = catchment * catchment;
            foreach (UrbanStop stop in map.Stops)
            {
                int first = stop == plan.A ? -1 : stop == plan.B ? int.MaxValue : FirstNear(samples, stop.Position, stopRange);
                if (first >= 0 || stop == plan.A) order.Add((first, stop));
            }
            order.Sort((x, y) => x.index.CompareTo(y.index));
            foreach (var entry in order) plan.Stops.Add(entry.stop);

            float landmarkRange = 4f * stopRange; // iki katı mesafe
            foreach (UrbanLandmark landmark in map.Landmarks)
                if (FirstNear(samples, landmark.Position, landmarkRange) >= 0) plan.Landmarks.Add(landmark);
        }

        static int FirstNear(List<Vector2> samples, Vector2 point, float rangeSquared)
        {
            for (int i = 0; i < samples.Count; i++)
                if ((samples[i] - point).sqrMagnitude <= rangeSquared) return i;
            return -1;
        }

        static float Demand(UrbanLinePlan plan, CityData city, EconomyConfig cfg)
        {
            float stops = 0f;
            foreach (UrbanStop stop in plan.Stops) stops += 0.4f + stop.Weight;
            stops += 0.15f * plan.Km;
            float fame = 0f;
            foreach (UrbanLandmark landmark in plan.Landmarks) fame += landmark.Fame;
            float population = city != null ? Mathf.Pow(Mathf.Max(0.05f, (float)city.PopulationMillions), cfg.UrbanPopulationExponent) : 1f;
            float tourism = city != null ? Mathf.Lerp(1f, city.TourismIndex, 0.3f) : 1f;
            return cfg.UrbanDemandScale * population * stops * EconomyConfig.UrbanLengthFactor(plan.Km) * (1f + 0.15f * fame) * tourism;
        }

        /// <summary>Konuma (metre) en yakın durak: kayıttaki hat uçlarını şehir verisine oturtmak için.</summary>
        public static UrbanStop NearestStop(UrbanMap map, Vector2 meters)
        {
            UrbanStop best = null;
            float bestDistance = float.MaxValue;
            foreach (UrbanStop stop in map.Stops)
            {
                float d = (stop.Position - meters).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = stop;
                }
            }
            return best;
        }
    }
}
