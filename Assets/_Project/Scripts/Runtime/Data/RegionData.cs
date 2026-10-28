using UnityEngine;

namespace BusTycoon.Data
{
    /// <summary>
    /// Ülke / bölge verisi. Bölgesel ekonomi çarpanlarını ve coğrafi genişleme (progression)
    /// kurallarını tutar. Başlangıç şehrinin bölgesi ücretsiz açılır; diğerleri lisans ister.
    /// </summary>
    [CreateAssetMenu(menuName = "Bus Tycoon/World/Region", fileName = "Region_")]
    public sealed class RegionData : ScriptableObject
    {
        [Header("Kimlik")]
        [Tooltip("Kalıcı kimlik (kayıt dosyalarında kullanılır). Genelde ISO-3166 alpha-2: TR, DE, BG...")]
        [SerializeField] string regionId;
        [SerializeField] string displayName;
        [Tooltip("Karayolu bağlantısı olan kara kütlesi. Farklı kütleler arasında hat açılamaz (feribot bağlantısı hariç).")]
        [SerializeField] string defaultLandmassId = "eurasia";
        [SerializeField] Color mapColor = new Color(0.85f, 0.85f, 0.85f);

        [Header("Ekonomi çarpanları")]
        [Tooltip("Alım gücü: referans bilet fiyatını ölçekler.")]
        [SerializeField, Range(0.2f, 3f)] float fareMultiplier = 1f;
        [SerializeField, Range(0.2f, 3f)] float fuelPriceMultiplier = 1f;
        [SerializeField, Range(0.2f, 5f)] float wageMultiplier = 1f;

        [Header("Genişleme (progression)")]
        [SerializeField, Min(1)] int requiredCompanyLevel = 1;
        [Tooltip("Bu bölgede hat açabilmek için tek seferlik işletme lisansı.")]
        [SerializeField, Min(0f)] float operatingLicenseCost;

        public string Id => regionId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? regionId : displayName;
        public string DefaultLandmassId => defaultLandmassId;
        public Color MapColor => mapColor;
        public float FareMultiplier => fareMultiplier;
        public float FuelPriceMultiplier => fuelPriceMultiplier;
        public float WageMultiplier => wageMultiplier;
        public int RequiredCompanyLevel => requiredCompanyLevel;
        public float OperatingLicenseCost => operatingLicenseCost;

        internal void Initialize(string id, string name, string landmassId)
        {
            regionId = id;
            displayName = name;
            defaultLandmassId = landmassId;
        }

        internal void SetEconomy(float fare, float fuel, float wage, int requiredLevel, float licenseCost)
        {
            fareMultiplier = fare;
            fuelPriceMultiplier = fuel;
            wageMultiplier = wage;
            requiredCompanyLevel = Mathf.Max(1, requiredLevel);
            operatingLicenseCost = Mathf.Max(0f, licenseCost);
        }

        void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(regionId)) regionId = name.Replace("Region_", "").ToUpperInvariant();
        }
    }
}
