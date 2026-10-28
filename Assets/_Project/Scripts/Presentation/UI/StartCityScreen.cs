using System.Collections.Generic;
using BusTycoon.Data;
using BusTycoon.Presentation.Map;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BusTycoon.Presentation.UI
{
    /// <summary>
    /// Yeni oyun: şirket adı + haritadan başlangıç şehri seçimi. Seçilen şehir için zorluk ipucu
    /// (çevredeki büyük pazarlar) gösterilir; böylece ilk karar bilinçli verilir.
    /// </summary>
    public sealed class StartCityScreen : UIScreen
    {
        [SerializeField] WorldMapView map;
        [SerializeField] TMP_InputField companyNameInput;
        [SerializeField] TMP_Text cityNameLabel;
        [SerializeField] TMP_Text cityInfoLabel;
        [SerializeField] TMP_Text difficultyLabel;
        [SerializeField] Button startButton;
        [Tooltip("Zorluk ipucu için taranan karayolu yarıçapı (km).")]
        [SerializeField, Min(50f)] float marketRadiusKm = 450f;

        readonly List<CityData> nearby = new List<CityData>();
        CityData selected;

        protected override void OnInitialized() => startButton.onClick.AddListener(StartGame);

        protected override void OnShown()
        {
            map.CityClicked += Select;
            selected = null;
            startButton.interactable = false;
            cityNameLabel.text = "Haritadan başlangıç şehrinizi seçin";
            cityInfoLabel.text = string.Empty;
            difficultyLabel.text = string.Empty;
        }

        protected override void OnHidden()
        {
            map.CityClicked -= Select;
            map.ClearHighlights();
        }

        void Select(CityData city)
        {
            if (selected != null) map.HighlightCity(selected, null);
            selected = city;
            map.HighlightCity(city, CityNodeState.Origin);

            Session.World.GetCitiesWithinKm(city, marketRadiusKm, nearby);
            long marketPopulation = city.Population;
            int bigCities = 0;
            foreach (var other in nearby)
            {
                if (other.Region != city.Region) continue; // başlangıçta yalnızca kendi bölgen açık
                marketPopulation += other.Population;
                if (other.Population >= CityData.LargeCityPopulation) bigCities++;
            }

            cityNameLabel.text = $"{city.DisplayName}, {city.Region.DisplayName}";
            cityInfoLabel.text =
                $"Nüfus {UIFormat.Number(city.Population)}\n" +
                $"{marketRadiusKm:N0} km içinde {nearby.Count} şehir, {bigCities} büyük şehir\n" +
                $"Erişilebilir pazar: {UIFormat.Number(marketPopulation)} kişi";

            difficultyLabel.text = marketPopulation > 15_000_000 ? "Kolay başlangıç"
                : marketPopulation > 5_000_000 ? "Orta zorluk"
                : "Zor başlangıç: küçük pazar";
            startButton.interactable = true;
        }

        void StartGame()
        {
            if (selected == null) return;
            string companyName = string.IsNullOrWhiteSpace(companyNameInput.text) ? "Yeni Turizm" : companyNameInput.text.Trim();
            Session.StartNewGame(companyName, selected);
            Flow.CloseAll();
            Flow.Show(UIScreenId.RoutePlanner); // ilk iş: ilk hattı aç (onboarding)
        }
    }
}
