using BusTycoon.World;
using UnityEngine;

namespace BusTycoon.Data
{
    /// <summary>
    /// Haritadaki bir şehir düğümü (node). Yalnızca TASARIM verisi tutar ve oyun sırasında değişmez.
    /// Oyuncunun o şehirdeki durumu (terminal, hatlar) <c>CompanyState</c> içinde, şehir kimliğiyle tutulur.
    /// Binlerce şehir için elle oluşturmak yerine <c>Bus Tycoon/Import Cities From CSV</c> aracını kullanın.
    /// </summary>
    [CreateAssetMenu(menuName = "Bus Tycoon/World/City", fileName = "City_")]
    public sealed class CityData : ScriptableObject
    {
        public const long MetropolisPopulation = 3_000_000;
        public const long LargeCityPopulation = 1_000_000;
        public const long MediumCityPopulation = 300_000;

        [Header("Kimlik")]
        [Tooltip("Kalıcı kimlik, ör. TR_ISTANBUL. Kayıt dosyaları bu kimliği saklar; yayınlandıktan sonra değiştirmeyin.")]
        [SerializeField] string cityId;
        [SerializeField] string displayName;
        [SerializeField] RegionData region;

        [Header("Coğrafya")]
        [SerializeField] GeoCoordinate coordinate;
        [Tooltip("Boşsa bölgenin kara kütlesi kullanılır. Ada şehirleri için doldurun (ör. 'crete').")]
        [SerializeField] string landmassOverride;

        [Header("Talep")]
        [Tooltip("Şehir (metropol) nüfusu. Çekim (gravity) talep modelinin ana girdisi.")]
        [SerializeField] long population = 100_000;
        [Tooltip("Turizm / cazibe çarpanı: 1 = normal, 1.5 = Antalya gibi turistik şehir.")]
        [SerializeField, Range(0.5f, 2f)] float tourismIndex = 1f;
        [SerializeField] bool isCapital;

        [Header("Maliyet")]
        [Tooltip("Sefer başına otogar ücreti. Negatifse nüfusa göre otomatik hesaplanır.")]
        [SerializeField] float terminalFeeOverride = -1f;

        [Header("Görsel")]
        [Tooltip("İsteğe bağlı: haritada şehri temsil eden 3D simge yapı.")]
        [SerializeField] GameObject landmarkPrefab;

        public string Id => cityId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? cityId : displayName;
        public RegionData Region => region;
        public GeoCoordinate Coordinate => coordinate;
        public long Population => population;
        public double PopulationMillions => population / 1_000_000.0;
        public float TourismIndex => tourismIndex;
        public bool IsCapital => isCapital;
        public GameObject LandmarkPrefab => landmarkPrefab;

        public string LandmassId =>
            !string.IsNullOrEmpty(landmassOverride) ? landmassOverride :
            region != null ? region.DefaultLandmassId : "default";

        /// <summary>Harita LOD katmanı: 0 = metropol (her zoom'da görünür) ... 3 = küçük şehir.</summary>
        public int MapTier =>
            population >= MetropolisPopulation ? 0 :
            population >= LargeCityPopulation ? 1 :
            population >= MediumCityPopulation ? 2 : 3;

        public float GetTerminalFee(EconomyConfig config) =>
            terminalFeeOverride >= 0f
                ? terminalFeeOverride
                : config.TerminalFeeBase + config.TerminalFeePerSqrtMillion * Mathf.Sqrt((float)PopulationMillions);

        internal void Initialize(string id, string name, RegionData cityRegion, GeoCoordinate coord,
            long cityPopulation, bool capital, float tourism = 1f)
        {
            cityId = id;
            displayName = name;
            region = cityRegion;
            coordinate = coord;
            population = cityPopulation;
            isCapital = capital;
            tourismIndex = tourism;
        }

        internal void SetLandmassOverride(string landmassId) => landmassOverride = landmassId;

        void OnValidate()
        {
            if (population < 0) population = 0;
            coordinate = new GeoCoordinate(coordinate.latitude, coordinate.longitude);
            if (string.IsNullOrWhiteSpace(cityId)) cityId = name.Replace("City_", "").ToUpperInvariant();
        }
    }
}
