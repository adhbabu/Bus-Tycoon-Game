using UnityEngine;

namespace BusTycoon.Data
{
    /// <summary>
    /// Araç segmentleri, alttan üste progression sırasıyla. Sayısal değer = kademe (tier).
    /// </summary>
    public enum BusSegment
    {
        Minibus = 0,        // 14–19 koltuk, kısa hatlar, düşük alım gücü
        Midibus = 1,        // 25–31 koltuk, bölgesel hatlar
        StandardCoach = 2,  // 2+2, 46 koltuk, şehirlerarası standart
        ComfortCoach = 3,   // 2+2 geniş, ekranlı, ikramlı
        LuxuryCoach = 4,    // 2+1 VIP, yüksek fiyat, düşük esneklik
        DoubleDecker = 5    // Çift katlı, ana arterlerde ölçek ekonomisi
    }

    /// <summary>
    /// Satın alınabilir otobüs modelinin TASARIM verisi. Oyuncunun sahip olduğu her otobüs
    /// bir <c>BusInstance</c>'tır ve bu asset'e <see cref="Id"/> ile bağlanır.
    /// </summary>
    [CreateAssetMenu(menuName = "Bus Tycoon/Fleet/Bus Model", fileName = "Bus_")]
    public sealed class BusModelData : ScriptableObject
    {
        [Header("Kimlik")]
        [SerializeField] string modelId;
        [SerializeField] string displayName;
        [SerializeField] string manufacturer;
        [SerializeField] BusSegment segment;
        [SerializeField, TextArea(2, 4)] string description;
        [SerializeField] Sprite icon;
        [Tooltip("Galeri (showroom) ve harita üzerinde kullanılan 3D model.")]
        [SerializeField] GameObject prefab;

        [Header("Satın alma ve kilit (progression)")]
        [SerializeField, Min(0f)] float price = 20000f;
        [SerializeField, Min(1)] int requiredCompanyLevel = 1;
        [SerializeField, Range(0f, 100f)] float requiredReputation;

        [Header("Kapasite ve performans")]
        [SerializeField, Min(1)] int seats = 16;
        [Tooltip("0 = çok rahatsız, 1 = birinci sınıf. Memnuniyeti ve dolaylı olarak itibarı etkiler.")]
        [SerializeField, Range(0f, 1f)] float comfort = 0.3f;
        [Tooltip("Mola ve trafik dahil ortalama seyahat hızı.")]
        [SerializeField, Min(10f)] float averageSpeedKmh = 80f;
        [Tooltip("Bu mesafenin üzerindeki hatlarda yolcular aracı rahatsız bulur, talep düşer.")]
        [SerializeField, Min(50f)] float comfortableRangeKm = 300f;
        [Tooltip("Durumun 1'den 0'a inmesi için gereken toplam km.")]
        [SerializeField, Min(10000f)] float lifetimeKm = 500000f;

        [Header("Talep etkisi")]
        [Tooltip("Referans bilet fiyatı çarpanı (yolcunun bu araç için ödemeye razı olduğu fiyat).")]
        [SerializeField, Range(0.3f, 3f)] float fareMultiplier = 1f;
        [Tooltip("Fiyat esnekliği çarpanı. <1: yolcu fiyata daha az duyarlı (lüks segment).")]
        [SerializeField, Range(0.3f, 2f)] float elasticityMultiplier = 1f;

        [Header("İşletme maliyetleri")]
        [SerializeField, Min(0f)] float fuelLitersPer100Km = 13f;
        [SerializeField, Min(0f)] float maintenanceCostPerKm = 0.06f;
        [SerializeField, Min(0f)] float insurancePerDay = 6f;
        [Tooltip("İkram, host/hostes, bagaj hizmeti vb. yolcu başı maliyet.")]
        [SerializeField, Min(0f)] float serviceCostPerPassenger = 0.2f;

        public string Id => modelId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? modelId : displayName;
        public string Manufacturer => manufacturer;
        public BusSegment Segment => segment;
        public int Tier => (int)segment;
        public string Description => description;
        public Sprite Icon => icon;
        public GameObject Prefab => prefab;

        public float Price => price;
        public int RequiredCompanyLevel => requiredCompanyLevel;
        public float RequiredReputation => requiredReputation;

        public int Seats => seats;
        public float Comfort => comfort;
        public float AverageSpeedKmh => averageSpeedKmh;
        public float ComfortableRangeKm => comfortableRangeKm;
        public float LifetimeKm => lifetimeKm;

        public float FareMultiplier => fareMultiplier;
        public float ElasticityMultiplier => elasticityMultiplier;

        public float FuelLitersPer100Km => fuelLitersPer100Km;
        public float MaintenanceCostPerKm => maintenanceCostPerKm;
        public float InsurancePerDay => insurancePerDay;
        public float ServiceCostPerPassenger => serviceCostPerPassenger;

        public float TravelHours(float roadKm) => roadKm / averageSpeedKmh;

        internal void Initialize(string id, string name, string maker, BusSegment busSegment, string text)
        {
            modelId = id;
            displayName = name;
            manufacturer = maker;
            segment = busSegment;
            description = text;
        }

        internal void SetStats(float busPrice, int level, float reputation, int seatCount, float comfortValue,
            float speed, float rangeKm, float lifeKm, float fareMult, float elasticityMult,
            float fuel, float maintenance, float insurance, float service)
        {
            price = busPrice;
            requiredCompanyLevel = level;
            requiredReputation = reputation;
            seats = seatCount;
            comfort = comfortValue;
            averageSpeedKmh = speed;
            comfortableRangeKm = rangeKm;
            lifetimeKm = lifeKm;
            fareMultiplier = fareMult;
            elasticityMultiplier = elasticityMult;
            fuelLitersPer100Km = fuel;
            maintenanceCostPerKm = maintenance;
            insurancePerDay = insurance;
            serviceCostPerPassenger = service;
        }

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(modelId)) modelId = name.Replace("Bus_", "").ToUpperInvariant();
        }
    }
}
