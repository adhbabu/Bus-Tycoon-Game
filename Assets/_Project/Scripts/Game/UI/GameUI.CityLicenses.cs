using BusTycoon.Data;
using BusTycoon.Urban;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// Şehir lisansları (şehir içi başlangıçlı oyunlar): Şirketim sayfasında ülke lisanslarının altında, sokak haritası
    /// olan 10 şehir. Lisanslı şehirde dolmuş hattı kurulur; iki lisanslı şehir arasında şehirlerarası hat ruhsatsızdır.
    /// Harita sekmesinde lisanslı şehirler arasında geçilir (sağ paneldeki şehir düğmeleri).
    /// </summary>
    public sealed partial class GameUI
    {
        /// <summary>Haritada gösterilen şehir (lisanslı ve sokak haritası olan); yoksa merkez şehir.</summary>
        string cityMapCityId;

        CityData CurrentCity
        {
            get
            {
                if (!session.IsRunning) return null;
                CityData city = cityMapCityId != null ? session.World.GetCity(cityMapCityId) : null;
                if (city != null && CityView.Supports(city) && session.Progression.OwnsCity(city)) return city;
                return session.Headquarters;
            }
        }

        /// <summary>Haritada başka bir lisanslı şehre geçer (kamera o şehrin ortasından başlar).</summary>
        void SelectMapCity(CityData city)
        {
            if (city == null || city == CurrentCity) return;
            if (urbanPlanning) ClosePanel();
            cityMapCityId = city.Id;
            cityCameraDistance = -1f;
            cityMapWanted = true;
            cityRoutesSignature = int.MinValue;
            tkMapSideSignature = int.MinValue;
        }

        void OnCityLicensed(CityData city)
        {
            Toast($"{city.DisplayName} lisansı alındı! Haritada şehri seçip dolmuş hattı kurabilirsin.", UIStyles.Gold, Sfx.Horn);
            tkSheetDirty = true;
            tkMapSideSignature = int.MinValue;
        }

        /// <summary>Şirketim → Şehir lisansları (ülke lisanslarının altında).</summary>
        void BuildCityLicenses(VisualElement body)
        {
            if (!session.Progression.CityLicensesActive) return;
            Label caption = Section(body, "Şehir lisansları");
            caption.AddToClassList("bt-section--full");
            Ui.Text("bt-item__text bt-section--full", "Lisanslı şehirde dolmuş hattı kurarsın; iki lisanslı şehrin arasındaki şehirlerarası hat ruhsatsızdır.",
                UiWeight.Regular, body);
            foreach (string cityId in UrbanCatalog.CityIds)
            {
                CityData city = session.World.GetCity(cityId);
                if (city == null) continue;
                bool owned = session.Progression.OwnsCity(city);
                VisualElement card = Ui.El("bt-item", body);
                VisualElement header = Ui.El("bt-row", card);
                VisualElement icon = Ui.El("bt-city-pick__icon", header);
                Sprite sprite = UICityIcons.Sprite(city);
                if (sprite != null) icon.style.backgroundImage = new StyleBackground(sprite);
                VisualElement text = Ui.El("bt-col bt-grow", header);
                Ui.Text("bt-item__title", city.DisplayName, UiWeight.Bold, text);
                Ui.Text("bt-detail__text", $"{Fmt.Population(city.Population)} · {UrbanSummary(city)}", UiWeight.Regular, text);
                Ui.Text(owned ? "bt-pill is-success" : "bt-pill is-warning", owned ? (city == session.Headquarters ? "Merkez" : "Lisanslı") : "Kilitli",
                    UiWeight.Bold, header);
                if (owned) continue;
                VisualElement actions = Ui.El("bt-actions", card);
                var check = session.Progression.CanBuyCityLicense(city);
                Ui.Text("bt-actions__note bt-grow", $"Lisans {Fmt.Money(session.Progression.CityLicensePrice(city))}", UiWeight.Regular, actions);
                CityData target = city;
                bool regionOpen = session.Progression.IsRegionUnlocked(city.Region);
                if (session.Progression.CityLicenseCredits > 0)
                {
                    // Dükkandan alınan lisans hakkı: oyun parası harcanmaz
                    Button credit = ActionButton(actions, "Hakkını kullan", () =>
                    {
                        var result = session.Progression.TryUseCityLicenseCredit(target);
                        if (!result) Toast(result.Message, UIStyles.Red);
                        goals.Evaluate();
                        tkSheetDirty = true;
                    }, enabled: regionOpen);
                    credit.name = "city-credit-" + city.Id;
                }
                else if (ShopService.Available && Room == null)
                {
                    Button store = ActionButton(actions, ShopService.PriceText(ShopCatalog.CityLicense), () => BuyFromShop(ShopCatalog.CityLicense, target.Id),
                        enabled: regionOpen && ShopService.BusyProduct == null);
                    store.name = "city-store-" + city.Id;
                }
                Button buy = ActionButton(actions, "Lisans al", () =>
                {
                    var result = session.Progression.TryBuyCityLicense(target);
                    if (!result) Toast(result.Message, UIStyles.Red);
                    goals.Evaluate();
                    tkSheetDirty = true;
                }, primary: true, enabled: check.Success);
                buy.name = "city-license-" + city.Id;
            }
        }

        string UrbanSummary(CityData city)
        {
            int urban = 0;
            foreach (var route in session.Routes.Routes) if (route.IsUrban && route.urbanCityId == city.Id) urban++;
            return urban > 0 ? $"{urban} dolmuş hattı" : "sokak haritası var";
        }
    }
}
