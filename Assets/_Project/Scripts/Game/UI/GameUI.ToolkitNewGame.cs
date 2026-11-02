using BusTycoon.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// Yeni arayüzde "Yeni Şirket Kur" ekranı (solda kart; arkada harita, merkez şehir haritadan da seçilir).
    /// Şirket adı UI Toolkit yazı alanıyla girilir. Dokunmatik cihazlarda tarayıcının yazı penceresi (WebTap) IMGUI'ye
    /// bağlı olduğundan bu ekran orada şimdilik eski arayüzde kalır.
    /// </summary>
    public sealed partial class GameUI
    {
        VisualElement tkNewGame, tkNewGameCard;
        TextField tkCompanyField;
        int tkNewGameSignature = int.MinValue;

        bool ToolkitDrawsNewGame => NewUi && mode == Mode.NewGame && !mobile;

        void BuildToolkitNewGameLayer()
        {
            tkNewGame = Ui.El("bt-menu-layer", toolkit.Root);
            tkNewGame.pickingMode = PickingMode.Ignore;
            tkNewGameCard = Ui.El("bt-sheet is-open bt-newgame", tkNewGame);
            tkNewGame.style.display = DisplayStyle.None;
        }

        void UpdateToolkitNewGame()
        {
            bool show = ToolkitDrawsNewGame;
            Ui.Show(tkNewGame, show);
            if (!show)
            {
                tkNewGameSignature = int.MinValue;
                return;
            }
            tkNewGame.SetEnabled(modal == Modal.None);
            float k = 1f / u;
            Rect rect = NewGameRect;
            Ui.Place(tkNewGameCard, rect.x * k, rect.y * k, rect.width * k, rect.height * k);
            tkCovered.Add(rect);
            int signature = (startCity?.Id.GetHashCode() ?? 0) * 31 + (Entitlements.Owns(ContentPacks.Europe) ? 1 : 0);
            if (signature == tkNewGameSignature) return;
            tkNewGameSignature = signature;
            BuildNewGameCard();
        }

        void BuildNewGameCard()
        {
            tkNewGameCard.Clear();
            Ui.Text("bt-sheet__title", "Yeni Şirket Kur", UiWeight.Black, tkNewGameCard);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.AddToClassList("bt-sheet__scroll");
            tkNewGameCard.Add(scroll);
            VisualElement body = scroll.contentContainer;

            Ui.Text("bt-caps bt-modal__caption", "ŞİRKET ADI", UiWeight.Bold, body);
            tkCompanyField = new TextField { maxLength = 28, value = companyName };
            tkCompanyField.AddToClassList("bt-field");
            Ui.Weight(tkCompanyField, UiWeight.Bold);
            tkCompanyField.RegisterValueChangedCallback(evt => companyName = evt.newValue);
            body.Add(tkCompanyField);
            Ui.Text("bt-item__text", "Haritadan merkez ofisinizin bulunacağı şehre tıklayın. O şehrin ülkesi ücretsiz açılır; diğer ülkeler sonradan lisansla açılır.", UiWeight.Regular, body);

            Ui.Text("bt-caps bt-modal__caption", "HIZLI SEÇİM", UiWeight.Bold, body);
            VisualElement quick = Ui.El("bt-tabs-inline", body);
            foreach (string id in QuickStartCityIds)
            {
                CityData city = session.World.GetCity(id);
                if (city == null) continue;
                Button tab = Ui.Button("bt-tab", city.DisplayName, () => SelectStartCity(city), quick);
                Ui.Toggle(tab, "is-active", city == startCity);
            }

            if (startCity != null)
            {
                VisualElement card = Ui.El("bt-item is-current", body);
                VisualElement row = Ui.El("bt-row", card);
                VisualElement icon = Ui.El("bt-station__city-icon", row);
                Sprite sprite = UICityIcons.Sprite(startCity);
                if (sprite != null) icon.style.backgroundImage = new StyleBackground(sprite);
                Ui.Text("bt-item__title bt-grow", $"{startCity.DisplayName}, {startCity.Region.DisplayName}", UiWeight.Black, row);
                Ui.Icon(UIIcons.Flag(startCity.Region.Id), "bt-endpoint__flag", row);
                Ui.Text("bt-item__text", startCityInfo, UiWeight.Regular, card);
                Label difficulty = Ui.Text("bt-item__title", startDifficulty, UiWeight.Bold, card);
                difficulty.AddToClassList(startDifficultyColor.Equals(UIStyles.Green) ? "is-up" : "is-warning-text");
            }
            else
            {
                Ui.Text("bt-empty", "Henüz şehir seçilmedi.", UiWeight.Regular, body);
            }

            VisualElement capital = Ui.El("bt-item", body);
            Ui.Text("bt-caps", "BAŞLANGIÇ SERMAYESİ", UiWeight.Bold, capital);
            Ui.Text("bt-item__text", $"{Fmt.Money(session.ProgressionConfig.StartingCash)} nakit + 2. el {session.ProgressionConfig.StartingBus.DisplayName} minibüs", UiWeight.Regular, capital);

            VisualElement actions = Ui.El("bt-actions bt-actions--fill", tkNewGameCard);
            ModalButton(actions, "Geri", EnterMainMenu);
            ModalButton(actions, "ŞİRKETİ KUR", StartCompany, "is-primary", enabled: startCity != null);
        }
    }
}
