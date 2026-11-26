using BusTycoon.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// "Şirketini kur" ekranı: şirket adı, logo (özgün amblemler: renk + simge) ve başlangıç şehri (büyük şehirlerden
    /// biri ya da haritadan herhangi bir şehir). Arkada harita kendi kendine süzülür; şehir haritadan da seçilir.
    /// Şirket adı bilgisayarda UI Toolkit yazı alanıyla, dokunmatik web'de tarayıcının yazı penceresiyle girilir (TkTextInput).
    /// </summary>
    public sealed partial class GameUI
    {
        VisualElement tkNewGame, tkNewGameCard;
        int tkNewGameSignature = int.MinValue;

        /// <summary>Şirket logoları: simge, zemin rengi ve simgenin koyu mu açık mı olduğu. Sıra kayıtta saklanır: yalnız sona eklenir.</summary>
        static readonly (string Symbol, Color Background, bool DarkIcon)[] CompanyLogos =
        {
            ("directions_bus", new Color(0.961f, 0.769f, 0.318f), true),   // altın
            ("route", new Color(0.165f, 0.616f, 0.561f), false),          // deniz yeşili
            ("star", new Color(0.902f, 0.224f, 0.275f), false),           // kırmızı
            ("bolt", new Color(0.482f, 0.38f, 1f), false),                // mor
            ("public", new Color(0.184f, 0.502f, 0.929f), false),         // mavi
            ("flag", new Color(0.18f, 0.8f, 0.443f), true),               // yeşil
            ("sports_score", new Color(0.957f, 0.635f, 0.38f), true),     // turuncu
            ("speed", new Color(0.227f, 0.29f, 0.322f), false),           // arduvaz
            // Görünüm paketi (FreeLogoCount'tan sonrası)
            ("castle", new Color(0.56f, 0.13f, 0.19f), false),            // bordo
            ("location_city", new Color(0.06f, 0.45f, 0.52f), false),     // petrol
            ("sunny", new Color(1f, 0.6f, 0.2f), true),                   // gün batımı
            ("dark_mode", new Color(0.2f, 0.22f, 0.56f), false),          // gece mavisi
            ("stadium", new Color(0.09f, 0.55f, 0.31f), false),           // çim yeşili
            ("account_balance", new Color(0.12f, 0.13f, 0.16f), false),   // siyah
        };

        bool ToolkitDrawsNewGame => NewUi && mode == Mode.NewGame;

        /// <summary>Şirket logosu öğesi (yuvarlak köşeli kare, ortada simge). Boyutu sınıftan gelir.
        /// <paramref name="preview"/>: seçicideki örnek (kilitli de olsa kendi tasarımı, destekçi çerçevesi yok).</summary>
        static VisualElement CompanyLogo(VisualElement parent, int logoId, string classes, bool preview = false)
        {
            VisualElement logo = Ui.El("bt-logo " + classes, parent);
            logo.pickingMode = PickingMode.Ignore;
            Ui.Symbol(CompanyLogos[0].Symbol, "bt-logo__icon", logo);
            SetCompanyLogo(logo, logoId, preview);
            return logo;
        }

        /// <summary>Şirketin logosu: kilitli logo (paket bu cihazda yoksa) ilk logoya döner; destekçiye altın çerçeve.</summary>
        static void SetCompanyLogo(VisualElement logo, int logoId, bool preview = false)
        {
            if (!preview && !LogoUnlocked(logoId)) logoId = 0;
            var design = CompanyLogos[Mathf.Clamp(logoId, 0, CompanyLogos.Length - 1)];
            logo.style.backgroundColor = design.Background;
            VisualElement icon = logo.hierarchy[0];
            Ui.SetSymbol(icon, design.Symbol);
            Ui.Toggle(icon, "is-dark", design.DarkIcon);
            Ui.Toggle(logo, "is-supporter", !preview && ShopService.Owns(ShopCatalog.Supporter));
        }

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
            int signature = (startCity?.Id.GetHashCode() ?? 0) * 31 + (Entitlements.Owns(ContentPacks.Europe) ? 1 : 0) + companyLogo * 7919;
            if (signature == tkNewGameSignature) return;
            tkNewGameSignature = signature;
            BuildNewGameCard();
        }

        void BuildNewGameCard()
        {
            tkNewGameCard.Clear();
            VisualElement header = Ui.El("bt-row bt-newgame__header", tkNewGameCard);
            CompanyLogo(header, companyLogo, "bt-logo--large");
            VisualElement titles = Ui.El("bt-col bt-grow", header);
            Ui.Text("bt-sheet__title", "Şirketini kur", UiWeight.Black, titles);
            Ui.Text("bt-sheet__subtitle", "Adını ve logonu seç, ilk şehrini belirle.", UiWeight.Regular, titles);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.AddToClassList("bt-sheet__scroll");
            tkNewGameCard.Add(scroll);
            VisualElement body = scroll.contentContainer;

            Ui.Text("bt-caps bt-modal__caption", "ŞİRKET ADI", UiWeight.Bold, body);
            TkTextInput(body, "company", companyName, 28, "Şirket adı", value => companyName = value);

            Ui.Text("bt-caps bt-modal__caption", "LOGO", UiWeight.Bold, body);
            VisualElement logos = Ui.El("bt-logo-picker", body);
            for (int i = 0; i < CompanyLogos.Length; i++)
            {
                if (!LogoUnlocked(i)) continue; // görünüm paketinin logoları paket varsa
                int id = i;
                Button pick = Ui.Button("bt-logo-pick", string.Empty, () => companyLogo = id, logos);
                pick.name = "logo-" + i;
                Ui.Toggle(pick, "is-active", i == companyLogo);
                CompanyLogo(pick, i, "bt-logo--pick");
            }

            Ui.Text("bt-caps bt-modal__caption", "BAŞLANGIÇ ŞEHRİ", UiWeight.Bold, body);
            VisualElement cities = Ui.El("bt-city-picker", body);
            foreach (string cityId in QuickStartCityIds)
            {
                CityData city = session.World.GetCity(cityId);
                if (city == null) continue;
                Button pick = Ui.Button("bt-row bt-city-pick", string.Empty, () => SelectStartCity(city), cities);
                Ui.Toggle(pick, "is-active", city == startCity);
                VisualElement icon = Ui.El("bt-city-pick__icon", pick);
                icon.pickingMode = PickingMode.Ignore;
                Sprite sprite = UICityIcons.Sprite(city);
                if (sprite != null) icon.style.backgroundImage = new StyleBackground(sprite);
                VisualElement text = Ui.El("bt-col bt-grow", pick);
                text.pickingMode = PickingMode.Ignore;
                Ui.Text("bt-city-pick__name", city.DisplayName, UiWeight.Bold, text);
                Ui.Text("bt-city-pick__sub", Fmt.Population(city.Population), UiWeight.Regular, text);
            }
            Ui.Text("bt-item__text", "Haritada başka bir şehre dokunarak da seçebilirsin. Şehrin ülkesi ücretsiz açılır; diğer ülkeler sonra lisansla açılır.",
                UiWeight.Regular, body);

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

            VisualElement capital = Ui.El("bt-item", body);
            Ui.Text("bt-caps", "BAŞLANGIÇ SERMAYESİ", UiWeight.Bold, capital);
            bool cityStart = startCity == null || CityView.Supports(startCity);
            Ui.Text("bt-item__text", cityStart
                ? $"{Fmt.Money(session.ProgressionConfig.StartingCash)} nakit. İlk dolmuşunu galeriden alıp şehrinde ilk hattını kuracaksın."
                : $"{Fmt.Money(session.ProgressionConfig.StartingCash)} nakit + 2. el {session.ProgressionConfig.StartingBus.DisplayName} minibüs", UiWeight.Regular, capital);

            VisualElement actions = Ui.El("bt-actions bt-actions--fill", tkNewGameCard);
            ModalButton(actions, "Geri", EnterMainMenu);
            ModalButton(actions, "ŞİRKETİ KUR", StartCompany, "is-primary", enabled: startCity != null);
        }
    }
}
