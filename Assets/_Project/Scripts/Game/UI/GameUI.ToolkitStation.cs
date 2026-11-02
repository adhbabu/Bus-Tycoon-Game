using BusTycoon.Data;
using BusTycoon.Stations;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// Yeni arayüzde durak ekranı: solda durak kartı (şehir, büyüklük, popülerlik yıldızları ve bileşenleri, yolcu,
    /// konfor, "Buradan hat aç"), sağda geliştirme mağazası, üstte "Haritaya dön". İçerik durağa girince ve gösterdiği
    /// veri değişince kurulur. Sahne iki kartın arasındaki alanın ortasına kaydırılır.
    /// </summary>
    public sealed partial class GameUI
    {
        VisualElement tkStation, tkStationBack, tkStationCard, tkStationShop, tkStationHint;
        ScrollView tkStationScroll;
        int tkStationSignature = int.MinValue;

        void BuildToolkitStation()
        {
            tkStation = Ui.El("bt-station", tkHud);
            tkStation.pickingMode = PickingMode.Ignore;
            tkStationBack = Ui.Button("bt-station__back", string.Empty, ExitStationNow, tkStation);
            Ui.Icon(UIIcon.Routes, "bt-station__back-icon", tkStationBack);
            Ui.Text("bt-station__back-label", "Haritaya dön", UiWeight.Bold, tkStationBack);
            tkStationCard = Ui.El("bt-sheet bt-station__card is-open", tkStation);
            tkStationShop = Ui.El("bt-sheet bt-station__shop is-open", tkStation);
            tkStationHint = Ui.Text("bt-station__hint", "", UiWeight.Bold, tkStation);
            tkStation.style.display = DisplayStyle.None;
        }

        int StationSignature(CityData city)
        {
            StationSystem stations = session.Stations;
            int signature = city.Id.GetHashCode();
            signature = signature * 31 + session.State.currentDay;
            signature = signature * 31 + stations.UpgradeCount(city) + (session.Progression.IsRegionUnlocked(city.Region) ? 1000 : 0);
            foreach (var upgrade in StationCatalog.Upgrades)
                signature = signature * 3 + (stations.CanBuild(city, upgrade).Success ? 1 : 0);
            return signature;
        }

        void UpdateToolkitStation(float k)
        {
            bool show = StationUi && Stations.City != null;
            Ui.Show(tkStation, show);
            if (!show)
            {
                tkStationSignature = int.MinValue;
                return;
            }
            CityData city = Stations.City;
            var back = new Rect(SafeLeft + Margin, SafeTop + TopBarHeight + Margin, (compact ? 170f : 200f) * u, 42f * u);
            Ui.Place(tkStationBack, back.x * k, back.y * k, back.width * k, back.height * k);
            Rect card = StationCardRect, shop = PanelRect;
            Ui.Place(tkStationCard, card.x * k, card.y * k, card.width * k, card.height * k);
            Ui.Place(tkStationShop, shop.x * k, shop.y * k, shop.width * k, shop.height * k);
            var hint = new Rect(card.xMax + Margin, ContentBottom - Margin - 26f * u, shop.x - card.xMax - 2f * Margin, 24f * u);
            Ui.Show(tkStationHint, hint.width > 120f * u);
            Ui.Place(tkStationHint, hint.x * k, hint.y * k, hint.width * k, hint.height * k);
            Ui.SetText((Label)tkStationHint, mobile ? "Kaydır: döndür · İki parmak: yakınlaştır" : "Sürükle: döndür · Tekerlek: yakınlaştır · Esc: haritaya dön");
            tkCovered.Add(card);
            tkCovered.Add(shop);
            tkCovered.Add(back);

            // Sahne iki kartın arasındaki alanın ortasında görünsün
            float center = (card.xMax + shop.x) / 2f;
            Stations.SetViewportShift(1f - 2f * center / Mathf.Max(1f, W));

            int signature = StationSignature(city);
            if (signature == tkStationSignature) return;
            tkStationSignature = signature;
            BuildStationCard(city);
            BuildStationShop(city);
        }

        void BuildStationCard(CityData city)
        {
            StationSystem stations = session.Stations;
            StationTier tier = StationCatalog.TierOf(city);
            StationPopularity popularity = stations.Popularity(city);
            bool unlocked = session.Progression.IsRegionUnlocked(city.Region);
            bool operating = stations.IsOperating(city);

            tkStationCard.Clear();
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.AddToClassList("bt-station__scroll");
            tkStationCard.Add(scroll);
            VisualElement body = scroll.contentContainer;

            VisualElement header = Ui.El("bt-row", body);
            VisualElement icon = Ui.El("bt-station__city-icon", header);
            Sprite sprite = UICityIcons.Sprite(city);
            if (sprite != null) icon.style.backgroundImage = new StyleBackground(sprite);
            else Ui.SetIcon(icon, UIIcons.Flag(city.Region.Id));
            VisualElement names = Ui.El("bt-col bt-grow", header);
            Ui.Text("bt-sheet__title", city.DisplayName, UiWeight.Black, names);
            VisualElement subtitle = Ui.El("bt-row", names);
            Ui.Icon(UIIcons.Flag(city.Region.Id), "bt-detail__icon", subtitle);
            Ui.Text("bt-sheet__subtitle", $"{StationCatalog.TierName(tier)} · {city.Region.DisplayName}", UiWeight.Regular, subtitle);
            string famous = UICityIcons.Description(city);
            if (!compact && !string.IsNullOrEmpty(famous)) Ui.Text("bt-item__reward", famous, UiWeight.Bold, body);

            VisualElement stars = Ui.El("bt-row bt-popularity", body);
            Ui.Text("bt-caps bt-grow", "POPÜLERLİK", UiWeight.Bold, stars);
            for (int i = 0; i < 5; i++)
            {
                VisualElement slot = Ui.El("bt-star", stars);
                Ui.Icon(UIIcon.Star, "bt-star__empty", slot);
                float fill = Mathf.Clamp01(popularity.Stars - i);
                if (fill <= 0.01f) continue;
                VisualElement clip = Ui.El("bt-star__clip", slot);
                clip.style.width = Length.Percent(fill * 100f); // yarım yıldız: kırpılarak
                Ui.Icon(UIIcon.Star, "bt-star__full", clip);
            }
            Ui.Text("bt-popularity__score is-gold", $"{popularity.Score:0}", UiWeight.Black, stars);
            if (!compact)
            {
                PopularityRow(body, "Şehrin ünü", popularity.Fame, "bt-bar__fill--blue");
                PopularityRow(body, "Yolcu trafiği", popularity.Traffic, "bt-bar__fill--green");
                PopularityRow(body, "Konfor", popularity.Comfort, string.Empty);
            }

            VisualElement facts = Ui.El("bt-item bt-station__facts", body);
            int built = stations.UpgradeCount(city);
            float bonus = stations.DemandBonus(city);
            KeyValue(facts, "Nüfus", Fmt.Population(city.Population), null, UIIcon.Building);
            KeyValue(facts, "Hatlarınız", $"{stations.RouteCount(city)}", null, UIIcon.Routes);
            KeyValue(facts, "Günlük yolcu", $"{stations.DailyPassengers(city):N0}", null, UIIcon.People);
            KeyValue(facts, "Geliştirmeler", $"{built} / {StationCatalog.AvailableCount(tier)}", null, UIIcon.Sparkles);
            KeyValue(facts, "Talep bonusu", bonus > 0f ? $"+%{bonus * 100f:0.#}" : "—", bonus > 0f ? "is-up" : "bt-dim", UIIcon.ChartUp);

            if (!unlocked) Ui.Text("bt-item__text", "Kilitli bölge: Bölgeler (B) panelinden lisans alın.", UiWeight.Regular, body);
            else if (!operating) Ui.Text("bt-item__text is-warning-text", "Bu şehre hat açınca durağı geliştirebilirsiniz.", UiWeight.Bold, body);
            Button plan = ActionButton(tkStationCard, "Buradan hat aç", () => PlanFromStationNow(city), primary: true, enabled: unlocked);
            plan.AddToClassList("bt-btn--cta");
        }

        static void PopularityRow(VisualElement body, string label, float value01, string fillClass)
        {
            VisualElement row = Ui.El("bt-row bt-detail", body);
            Ui.Text("bt-detail__text bt-popularity__label", label, UiWeight.Regular, row);
            VisualElement bar = Ui.El("bt-bar bt-grow", row);
            VisualElement fill = Ui.El("bt-bar__fill", bar);
            if (!string.IsNullOrEmpty(fillClass)) fill.AddToClassList(fillClass);
            Ui.Fill(fill, value01);
        }

        void BuildStationShop(CityData city)
        {
            StationSystem stations = session.Stations;
            StationTier tier = StationCatalog.TierOf(city);
            tkStationShop.Clear();
            Ui.Text("bt-sheet__title", "Durak Geliştirmeleri", UiWeight.Black, tkStationShop);
            Ui.Text("bt-sheet__subtitle", $"{stations.UpgradeCount(city)} / {StationCatalog.AvailableCount(tier)} yapıldı · her konfor puanı bu duraktaki hatların talebini %{StationSystem.DemandBonusPerComfort * 100f:0.#} artırır", UiWeight.Regular, tkStationShop);
            tkStationScroll = new ScrollView(ScrollViewMode.Vertical);
            tkStationScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            tkStationScroll.AddToClassList("bt-sheet__scroll");
            tkStationShop.Add(tkStationScroll);
            VisualElement body = tkStationScroll.contentContainer;

            // Sıra: yapılabilecekler, yapılanlar, daha büyük durak isteyenler
            sortedUpgrades.Clear();
            foreach (var upgrade in StationCatalog.Upgrades)
                if (StationCatalog.IsAvailable(upgrade, tier) && !stations.Has(city, upgrade)) sortedUpgrades.Add(upgrade);
            foreach (var upgrade in StationCatalog.Upgrades)
                if (stations.Has(city, upgrade)) sortedUpgrades.Add(upgrade);
            foreach (var upgrade in StationCatalog.Upgrades)
                if (!StationCatalog.IsAvailable(upgrade, tier)) sortedUpgrades.Add(upgrade);

            foreach (var upgrade in sortedUpgrades)
            {
                bool built = stations.Has(city, upgrade);
                bool available = StationCatalog.IsAvailable(upgrade, tier);
                VisualElement card = Ui.El("bt-item bt-upgrade", body);
                Ui.Toggle(card, "is-locked", !available);
                VisualElement row = Ui.El("bt-row", card);
                Ui.Icon(UpgradeIcon(upgrade), "bt-upgrade__icon", row);
                VisualElement text = Ui.El("bt-col bt-grow", row);
                VisualElement title = Ui.El("bt-row", text);
                Ui.Text("bt-item__title bt-grow", upgrade.Name, UiWeight.Bold, title);
                if (built) Ui.Text("bt-pill is-success", "Yapıldı", UiWeight.Bold, title);
                else if (!available) Ui.Text("bt-pill", StationCatalog.TierName(upgrade.MinTier), UiWeight.Bold, title);
                Ui.Text("bt-item__text", upgrade.Description, UiWeight.Regular, text);
                Label effect = Ui.Text("bt-detail__text", $"Konfor +{upgrade.Comfort} · talep +%{upgrade.Comfort * StationSystem.DemandBonusPerComfort * 100f:0.#}", UiWeight.Bold, text);
                if (!built) effect.AddToClassList("is-up");
                if (built || !available) continue;
                var check = stations.CanBuild(city, upgrade);
                StationUpgrade target = upgrade;
                Button build = ActionButton(card, $"Yap · {Fmt.Money(stations.Cost(city, upgrade))}", () =>
                {
                    BuildUpgradeNow(city, target);
                    tkStationSignature = int.MinValue;
                }, primary: true, enabled: check.Success);
                build.AddToClassList("bt-btn--block");
                if (!check.Success && !string.IsNullOrEmpty(check.Message)) Ui.Text("bt-item__text is-warning-text", check.Message, UiWeight.Bold, card);
            }
        }
    }
}
