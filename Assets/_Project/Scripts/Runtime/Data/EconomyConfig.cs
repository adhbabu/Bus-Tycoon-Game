using UnityEngine;

namespace BusTycoon.Data
{
    /// <summary>
    /// Ekonominin TÜM ayar katsayıları tek bir asset'te. Oyun dengesi (balancing) kod değiştirmeden
    /// buradan yapılır. Formüllerin açıklaması: docs/02-Hat-ve-Talep-Esnekligi.md
    /// </summary>
    [CreateAssetMenu(menuName = "Bus Tycoon/Config/Economy Config", fileName = "EconomyConfig")]
    public sealed class EconomyConfig : ScriptableObject
    {
        [Header("Para birimi")]
        [SerializeField] string currencySymbol = "$";

        [Header("Talep potansiyeli — çekim (gravity) modeli")]
        [Tooltip("K: Q_ref = K × (NüfusA_mn × NüfusB_mn)^α × MesafeEğrisi(km) × Turizm")]
        [SerializeField, Min(0f)] float demandScale = 60f;
        [Tooltip("α: nüfus çarpımının üssü. 0.5 = iki şehir nüfusunun geometrik ortalaması.")]
        [SerializeField, Range(0.2f, 1f)] float populationExponent = 0.5f;
        [Tooltip("X: karayolu km, Y: talep çarpanı. Kısa mesafede özel araç, uzun mesafede uçak rekabetini temsil eder.")]
        [SerializeField] AnimationCurve distanceDemandCurve = CreateDefaultDistanceCurve();

        [Header("Fiyat esnekliği")]
        [Tooltip("ε_ref: referans fiyattaki esneklik. Otobüs modeli çarpanıyla ölçeklenir. >1 = fiyata duyarlı.")]
        [SerializeField, Range(0.2f, 4f)] float baseElasticity = 1.5f;
        [Tooltip("Bilet fiyatı kaydırıcısının alt sınırı (referans fiyatın katı).")]
        [SerializeField, Range(0.05f, 1f)] float minPriceFactor = 0.3f;
        [Tooltip("Bilet fiyatı kaydırıcısının üst sınırı (referans fiyatın katı).")]
        [SerializeField, Range(1f, 5f)] float maxPriceFactor = 3f;

        [Header("Referans bilet fiyatı: P_ref = (taban + km × km_ücreti) × bölge × segment")]
        [SerializeField, Min(0f)] float baseFare = 3f;
        [SerializeField, Min(0f)] float farePerKm = 0.045f;

        [Header("Talep çarpanları")]
        [Tooltip("İtibar 0 iken talep çarpanı.")]
        [SerializeField, Min(0f)] float reputationDemandAtZero = 0.6f;
        [Tooltip("İtibar 100 iken talep çarpanı.")]
        [SerializeField, Min(0f)] float reputationDemandAtMax = 1.6f;
        [Tooltip("Yeni hattın bilinirliğinin oturma süresi (gün, üstel zaman sabiti).")]
        [SerializeField, Min(0f)] float routeRampUpDays = 10f;
        [Tooltip("Açılış günündeki talep oranı.")]
        [SerializeField, Range(0f, 1f)] float newRouteDemandFactor = 0.3f;
        [Tooltip("Otobüsün konforlu menzili aşıldığında talebin düşüş hızı.")]
        [SerializeField, Min(0f)] float discomfortSteepness = 1.5f;
        [Tooltip("Günlük rastgele talep dalgalanması (±).")]
        [SerializeField, Range(0f, 0.5f)] float dailyDemandNoise = 0.05f;
        [Tooltip("Kapasiteye yaklaşırken doluluğun yumuşaklığı. Büyük değer = sert sınır.")]
        [SerializeField, Range(2f, 20f)] float capacitySmoothness = 8f;

        [Header("Operasyon")]
        [SerializeField, Range(4f, 24f)] float operatingHoursPerDay = 18f;
        [Tooltip("Her seferden sonra yolcu indirme/bindirme, temizlik, mola süresi (saat).")]
        [SerializeField, Min(0f)] float turnaroundHours = 0.5f;
        [Tooltip("Bir şoförün tek seferde sürebileceği maksimum saat; aşılırsa ikinci şoför gerekir.")]
        [SerializeField, Min(1f)] float maxDrivingHoursPerDriver = 9f;
        [Tooltip("Otomatik sefer planlamasının hedeflediği doluluk oranı.")]
        [SerializeField, Range(0.3f, 1f)] float autoScheduleTargetLoadFactor = 0.85f;
        [Tooltip("Otobüs atanmış bir hatta yapılacak minimum günlük sefer sayısı (lisans şartı).")]
        [SerializeField, Min(0f)] float minTripsPerDay = 2f;

        [Header("Değişken maliyetler")]
        [SerializeField, Min(0f)] float fuelPricePerLiter = 1.3f;
        [SerializeField, Min(0f)] float driverWagePerHour = 8f;
        [SerializeField, Min(0f)] float tollPerKm = 0.02f;
        [SerializeField, Min(0f)] float terminalFeeBase = 2f;
        [SerializeField, Min(0f)] float terminalFeePerSqrtMillion = 1.5f;
        [Tooltip("Durumu 0 olan otobüsün bakım maliyeti artışı: bakım × (1 + (1 − durum) × bu değer).")]
        [SerializeField, Min(0f)] float wornMaintenancePenalty = 1.5f;

        [Header("Hat lisansı")]
        [SerializeField, Min(0f)] float routeLicenseBase = 1000f;
        [SerializeField, Min(0f)] float routeLicensePerKm = 5f;

        [Header("Filo: değer kaybı, arıza, bakım")]
        [SerializeField, Range(0f, 0.5f)] float annualDepreciation = 0.12f;
        [Tooltip("Durumu 0 olan aracın fiyatı, sıfır fiyatının bu oranıdır (2. el fiyatlaması).")]
        [SerializeField, Range(0f, 1f)] float usedPriceFloor = 0.35f;
        [Tooltip("Satışta galerici marjı: satış bedeli = piyasa değeri × bu oran.")]
        [SerializeField, Range(0.3f, 1f)] float resaleRatio = 0.85f;
        [Tooltip("Durumu 0 olan otobüsün günlük arıza olasılığı (durumla karesel azalır).")]
        [SerializeField, Range(0f, 0.5f)] float breakdownChanceAtZeroCondition = 0.08f;
        [SerializeField, Min(1)] int maxBreakdownDays = 3;
        [Tooltip("Arıza tamir bedeli = otobüs fiyatı × bu oran.")]
        [SerializeField, Range(0f, 0.2f)] float repairCostFactor = 0.01f;
        [Tooltip("Revizyon bedeli = otobüs fiyatı × bu oran × giderilen yıpranma.")]
        [SerializeField, Range(0f, 1f)] float overhaulCostFactor = 0.3f;
        [SerializeField, Range(0.5f, 1f)] float overhaulTargetCondition = 0.9f;
        [Tooltip("Otomatik bakım açıkken durumu bu değerin altına düşen otobüs gün başında revizyona girer.")]
        [SerializeField, Range(0.2f, 0.85f)] float autoOverhaulBelowCondition = 0.6f;
        [Tooltip("Otomatik bakım kasada en az bu kadar günlük gider bırakır (iflas riskine girmesin).")]
        [SerializeField, Min(0f)] float autoOverhaulReserveDays = 2f;

        [Header("Memnuniyet → itibar")]
        [SerializeField, Range(0f, 1f)] float satisfactionBase = 0.35f;
        [SerializeField, Range(0f, 1f)] float satisfactionComfortWeight = 0.45f;
        [Tooltip("Fiyat referansın %10 üstündeyse memnuniyet bu değerin %10'u kadar düşer.")]
        [SerializeField, Range(0f, 2f)] float satisfactionPriceWeight = 0.4f;
        [Tooltip("Bilet bulamayan yolcu oranının memnuniyete etkisi.")]
        [SerializeField, Range(0f, 1f)] float satisfactionCrowdingWeight = 0.3f;
        [Tooltip("İtibarın günlük memnuniyete yaklaşma hızı.")]
        [SerializeField, Range(0f, 0.2f)] float reputationAdjustRate = 0.02f;

        public string CurrencySymbol => currencySymbol;

        public float DemandScale => demandScale;
        public float PopulationExponent => populationExponent;
        public float BaseElasticity => baseElasticity;
        public float MinPriceFactor => minPriceFactor;
        public float MaxPriceFactor => maxPriceFactor;
        public float BaseFare => baseFare;
        public float FarePerKm => farePerKm;

        public float ReputationDemandAtZero => reputationDemandAtZero;
        public float ReputationDemandAtMax => reputationDemandAtMax;
        public float RouteRampUpDays => routeRampUpDays;
        public float NewRouteDemandFactor => newRouteDemandFactor;
        public float DiscomfortSteepness => discomfortSteepness;
        public float DailyDemandNoise => dailyDemandNoise;
        public float CapacitySmoothness => capacitySmoothness;

        public float OperatingHoursPerDay => operatingHoursPerDay;
        public float TurnaroundHours => turnaroundHours;
        public float MaxDrivingHoursPerDriver => maxDrivingHoursPerDriver;
        public float AutoScheduleTargetLoadFactor => autoScheduleTargetLoadFactor;
        public float MinTripsPerDay => minTripsPerDay;

        public float FuelPricePerLiter => fuelPricePerLiter;
        public float DriverWagePerHour => driverWagePerHour;
        public float TollPerKm => tollPerKm;
        public float TerminalFeeBase => terminalFeeBase;
        public float TerminalFeePerSqrtMillion => terminalFeePerSqrtMillion;
        public float WornMaintenancePenalty => wornMaintenancePenalty;

        public float RouteLicenseBase => routeLicenseBase;
        public float RouteLicensePerKm => routeLicensePerKm;

        public float AnnualDepreciation => annualDepreciation;
        public float UsedPriceFloor => usedPriceFloor;
        public float ResaleRatio => resaleRatio;
        public float BreakdownChanceAtZeroCondition => breakdownChanceAtZeroCondition;
        public int MaxBreakdownDays => maxBreakdownDays;
        public float RepairCostFactor => repairCostFactor;
        public float OverhaulCostFactor => overhaulCostFactor;
        public float OverhaulTargetCondition => overhaulTargetCondition;
        public float AutoOverhaulBelowCondition => autoOverhaulBelowCondition;
        public float AutoOverhaulReserveDays => autoOverhaulReserveDays;

        public float SatisfactionBase => satisfactionBase;
        public float SatisfactionComfortWeight => satisfactionComfortWeight;
        public float SatisfactionPriceWeight => satisfactionPriceWeight;
        public float SatisfactionCrowdingWeight => satisfactionCrowdingWeight;
        public float ReputationAdjustRate => reputationAdjustRate;

        public float EvaluateDistanceDemand(float roadKm) =>
            distanceDemandCurve == null || distanceDemandCurve.length == 0 ? 1f : Mathf.Max(0f, distanceDemandCurve.Evaluate(roadKm));

        public float GetRouteLicenseCost(float roadKm) => routeLicenseBase + routeLicensePerKm * roadKm;

        static AnimationCurve CreateDefaultDistanceCurve() => new AnimationCurve(
            new Keyframe(0f, 0.10f),
            new Keyframe(30f, 0.35f),
            new Keyframe(100f, 0.80f),
            new Keyframe(250f, 1.00f),
            new Keyframe(600f, 1.00f),
            new Keyframe(1000f, 0.70f),
            new Keyframe(1500f, 0.40f),
            new Keyframe(2500f, 0.15f),
            new Keyframe(4000f, 0.05f));

        void OnValidate()
        {
            if (maxPriceFactor <= minPriceFactor) maxPriceFactor = minPriceFactor + 0.5f;
            if (reputationDemandAtMax < reputationDemandAtZero) reputationDemandAtMax = reputationDemandAtZero;
        }
    }
}
