using BusTycoon.Data;
using BusTycoon.Economy;
using UnityEngine;
using UnityEngine.UIElements;

namespace BusTycoon.Game
{
    /// <summary>
    /// Gerçek paralı küçük dükkan (arayüz tarafı): üst çubuktaki dükkan düğmesi, Dükkan sayfası (4 ürün, geri yükleme),
    /// Şirketim sayfasında "Görünüm" (logo ve araç rengi), şehir lisansı hakkı. Satın almanın kendisi
    /// <see cref="ShopService"/>'te. Dükkan dengeyi bozmaz: görünüm ve destekçi paketi yalnız görünüş; sermaye paketi ve
    /// lisans hakkı çok oyunculu yarışta alınmaz ve uygulanmaz.
    /// </summary>
    public sealed partial class GameUI
    {
        /// <summary>İlk bu kadar logo ücretsiz; sonrakiler görünüm paketiyle açılır.</summary>
        const int FreeLogoCount = 8;

        /// <summary>Şirket araç renkleri (0: varsayılan, segment renkleri). Sıra kayıtta: yalnız sona eklenir.</summary>
        static readonly (string Name, Color Color)[] Liveries =
        {
            ("Varsayılan", Color.white),
            ("Kırmızı", new Color(0.86f, 0.2f, 0.22f)),
            ("Lacivert", new Color(0.16f, 0.27f, 0.58f)),
            ("Zümrüt", new Color(0.1f, 0.62f, 0.44f)),
            ("Turuncu", new Color(0.96f, 0.52f, 0.16f)),
            ("Mor", new Color(0.52f, 0.32f, 0.84f)),
            ("Gece", new Color(0.13f, 0.14f, 0.17f)),
            ("Altın", new Color(0.96f, 0.77f, 0.3f)), // destekçi paketi
        };
        const int SupporterLivery = 7;

        /// <summary>Şehir kartından başlatılan lisans satın alması: hak gelince bu şehre uygulanır.</summary>
        string shopLicenseCityId;

        void InitShop()
        {
            ShopService.Load();
            if (LaunchOptions.Get("dukkan") == "test") ShopService.SetBackend(new TestShopBackend());
            else if (ShopService.StoreFactory != null &&
                     (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer))
                ShopService.SetBackend(ShopService.StoreFactory()); // App Store / Google Play (BusTycoon.Store)
            ShopService.Granted += OnShopGranted;
            ShopService.PurchaseFailed += OnShopPurchaseFailed;
            ShopService.Changed += OnShopChanged;
        }

        void UnbindShop()
        {
            ShopService.Granted -= OnShopGranted;
            ShopService.PurchaseFailed -= OnShopPurchaseFailed;
            ShopService.Changed -= OnShopChanged;
        }

        void OnShopChanged()
        {
            if (panel == Panel.Shop || panel == Panel.Regions) tkSheetDirty = true;
        }

        void OnShopGranted(string productId)
        {
            if (productId == ShopCatalog.Looks)
                Toast("Görünüm paketi açıldı! Şirketim sayfasından logonu ve araç rengini seç.", UIStyles.Gold, Sfx.Success);
            else if (productId == ShopCatalog.Supporter)
                Toast("Destekçi paketi için teşekkürler! Altın çerçeve ve altın araç rengi açıldı.", UIStyles.Gold, Sfx.Success);
            // Sermaye ve lisans hakkı oyuna işlenince bildirilir (UpdateShop)
            if (session.IsRunning) ApplyLivery();
        }

        void OnShopPurchaseFailed(string productId, string message)
        {
            if (!string.IsNullOrEmpty(message)) Toast(message, UIStyles.Orange, Sfx.Warning);
            if (productId == ShopCatalog.CityLicense) shopLicenseCityId = null;
        }

        /// <summary>Her karede: bekleyen sermaye ve lisans hakları oyun açıkken (yarış dışında) kayda işlenir.</summary>
        void UpdateShop()
        {
            if (mode != Mode.Playing || !session.IsRunning || Room != null) return;
            if (ShopService.PendingCapital == 0 && ShopService.PendingLicenses == 0) return;

            int capital = ShopService.TakePendingCapital();
            if (capital > 0)
            {
                double amount = capital * ShopCatalog.CapitalUnits;
                session.Finance.Earn(amount, LedgerCategory.Other, "dukkan");
                Toast($"{Fmt.Money(amount)} kasaya eklendi. Bol yolcu!", UIStyles.Gold, Sfx.Coin);
                MarkDataDirty();
            }

            int licenses = ShopService.TakePendingLicenses();
            if (licenses > 0)
            {
                session.State.cityLicenseCredits += licenses;
                CityData target = shopLicenseCityId != null ? session.World.GetCity(shopLicenseCityId) : null;
                shopLicenseCityId = null;
                // Şehir kartından alındıysa o şehre hemen uygulanır (lisans bildirimi OnCityLicensed'dan)
                if (target == null || !session.Progression.TryUseCityLicenseCredit(target).Success)
                    Toast($"Şehir lisansı hakkın var ({session.Progression.CityLicenseCredits}). Şirketim → Şehir lisansları'ndan bir şehir seç.",
                        UIStyles.Gold, Sfx.Success);
                tkSheetDirty = true;
            }
        }

        // ------------------------------------------------------------------ logo ve araç rengi

        static bool LogoUnlocked(int logoId) => logoId < FreeLogoCount || ShopService.Owns(ShopCatalog.Looks);

        static bool LiveryUnlocked(int liveryId) =>
            liveryId <= 0 || (liveryId == SupporterLivery ? ShopService.Owns(ShopCatalog.Supporter) : ShopService.Owns(ShopCatalog.Looks));

        /// <summary>Üst çubuktaki logonun yenilenmesi için: logo, açık mı, destekçi çerçevesi.</summary>
        int CompanyLogoKey()
        {
            int id = session.State.logoId;
            return id * 4 + (LogoUnlocked(id) ? 2 : 0) + (ShopService.Owns(ShopCatalog.Supporter) ? 1 : 0);
        }

        /// <summary>Haritadaki araçlara şirket rengi: ülke haritasında gövde, şehir haritasında dolmuş tavanı.</summary>
        void ApplyLivery()
        {
            int id = session.State != null ? session.State.liveryId : 0;
            Color? color = id > 0 && id < Liveries.Length && LiveryUnlocked(id) ? Liveries[id].Color : (Color?)null;
            BusModels.SetLivery(color);
            if (cityView != null) cityView.SetRoofColor(color);
        }

        /// <summary>Şirketim → Görünüm: logo ve araç rengi (kilitliye dokununca dükkan açılır).</summary>
        void BuildLooks(VisualElement body)
        {
            Label caption = Section(body, "Görünüm");
            caption.AddToClassList("bt-section--full");
            VisualElement card = Ui.El("bt-item bt-full", body);
            bool looks = ShopService.Owns(ShopCatalog.Looks), supporter = ShopService.Owns(ShopCatalog.Supporter);
            if (supporter) Ui.Text("bt-pill is-gold bt-looks__badge", "DESTEKÇİ", UiWeight.Black, card);

            Ui.Text("bt-caps bt-model-caption", "LOGO", UiWeight.Bold, card);
            VisualElement logos = Ui.El("bt-logo-picker", card);
            for (int i = 0; i < CompanyLogos.Length; i++)
            {
                int id = i;
                bool unlocked = LogoUnlocked(i);
                Button pick = Ui.Button("bt-logo-pick", string.Empty, () =>
                {
                    if (!LogoUnlocked(id))
                    {
                        OpenPage(Panel.Shop);
                        return;
                    }
                    session.State.logoId = id;
                    tkSheetDirty = true;
                }, logos);
                pick.name = "looks-logo-" + i;
                Ui.Toggle(pick, "is-active", unlocked && i == session.State.logoId);
                Ui.Toggle(pick, "is-locked", !unlocked);
                CompanyLogo(pick, i, "bt-logo--pick", preview: true);
                if (!unlocked) Ui.Symbol("lock", "bt-logo-pick__lock", pick);
            }

            Ui.Text("bt-caps bt-model-caption", "ARAÇ RENGİ", UiWeight.Bold, card);
            VisualElement liveries = Ui.El("bt-livery-picker", card);
            for (int i = 0; i < Liveries.Length; i++)
            {
                int id = i;
                bool unlocked = LiveryUnlocked(i);
                Button pick = Ui.Button("bt-livery", string.Empty, () =>
                {
                    if (!LiveryUnlocked(id))
                    {
                        OpenPage(Panel.Shop);
                        return;
                    }
                    session.State.liveryId = id;
                    ApplyLivery();
                    tkSheetDirty = true;
                }, liveries);
                pick.name = "livery-" + i;
                Ui.Toggle(pick, "is-active", unlocked && i == session.State.liveryId);
                Ui.Toggle(pick, "is-locked", !unlocked);
                VisualElement swatch = Ui.El("bt-livery__swatch", pick);
                swatch.style.backgroundColor = i == 0 ? new Color(0.6f, 0.68f, 0.72f) : Liveries[i].Color;
                if (!unlocked) Ui.Symbol("lock", "bt-livery__lock", swatch);
                Ui.Text("bt-livery__name", Liveries[i].Name, UiWeight.Regular, pick);
            }
            if (!looks || !supporter)
                Ui.Text("bt-item__text", looks ? "Altın renk ve altın logo çerçevesi destekçi paketinde." : "Kilitli logo ve renkler Dükkan'daki görünüm paketiyle açılır.",
                    UiWeight.Regular, card);
        }

        // ------------------------------------------------------------------ dükkan sayfası

        int ShopSignature()
        {
            int signature = ShopService.Version * 31 + (Room != null ? 1 : 0) + session.Progression.CityLicenseCredits * 7;
            return signature;
        }

        void BuildShop(VisualElement body)
        {
            bool available = ShopService.Available, race = Room != null;
            if (!available)
                Ui.Text("bt-item__text bt-full bt-shop__note", "Satın alma yalnız App Store ve Google Play sürümünde. Web sürümünde dükkan kapalı; oyunun tamamı ücretsiz.",
                    UiWeight.Bold, body);
            else if (race)
                Ui.Text("bt-item__text bt-full bt-shop__note", "Yarış sürerken sermaye ve şehir lisansı alınmaz (yarış adil kalsın). Görünüm paketleri alınabilir.",
                    UiWeight.Bold, body);

            foreach (ShopCatalog.Product product in ShopCatalog.Products)
            {
                ShopCatalog.Product item = product;
                bool owned = !product.Consumable && ShopService.Owns(product.Id);
                bool busy = ShopService.BusyProduct == product.Id;
                VisualElement card = Ui.El("bt-item bt-shop-card", body);
                card.name = "shop-" + product.Id;
                VisualElement header = Ui.El("bt-row", card);
                VisualElement badge = Ui.El("bt-shop-card__badge", header);
                Ui.Symbol(product.Symbol, "bt-shop-card__icon", badge);
                VisualElement text = Ui.El("bt-col bt-grow", header);
                Ui.Text("bt-item__title", product.Title, UiWeight.Black, text);
                Ui.Text("bt-item__text", product.Description, UiWeight.Regular, text);

                VisualElement actions = Ui.El("bt-actions", card);
                string note = product.Consumable ? "Tekrar alınabilir" : "Bir kez alınır · bütün oyunlarında";
                if (product.Id == ShopCatalog.CityLicense && session.Progression.CityLicenseCredits > 0)
                    note = $"Kullanılmamış hakkın: {session.Progression.CityLicenseCredits}";
                Ui.Text("bt-actions__note bt-grow", note, UiWeight.Regular, actions);
                if (owned)
                {
                    Ui.Text("bt-pill is-success", "SAHİPSİN", UiWeight.Black, actions);
                    continue;
                }
                bool enabled = available && ShopService.BusyProduct == null && !(race && product.BlockedInRace);
                Button buy = ActionButton(actions, busy ? "İşleniyor…" : ShopService.PriceText(product.Id), () => BuyFromShop(item.Id),
                    primary: true, enabled: enabled);
                buy.name = "buy-" + product.Id;
            }

            if (available)
            {
                VisualElement footer = Ui.El("bt-actions bt-full", body);
                Ui.Text("bt-actions__note bt-grow", "Ödemeyi App Store / Google Play alır; oyun kart bilgini görmez.", UiWeight.Regular, footer);
                Button restore = ActionButton(footer, ShopService.Restoring ? "Geri yükleniyor…" : "Satın alımları geri yükle", () =>
                {
                    ShopService.Restore((success, message) =>
                        Toast(success ? "Satın alımların geri yüklendi." : "Geri yükleme olmadı" + (string.IsNullOrEmpty(message) ? "." : ": " + message),
                            success ? UIStyles.Green : UIStyles.Orange));
                }, enabled: !ShopService.Restoring);
                restore.name = "shop-restore";
            }
        }

        void BuyFromShop(string productId, string licenseCityId = null)
        {
            ShopCatalog.Product product = ShopCatalog.Get(productId);
            if (product == null) return;
            if (product.BlockedInRace && Room != null)
            {
                Toast("Yarış sürerken alınamaz.", UIStyles.Orange);
                return;
            }
            if (productId == ShopCatalog.CityLicense) shopLicenseCityId = licenseCityId;
            if (!ShopService.Purchase(productId)) Toast("Dükkan şu an hazır değil, birazdan yeniden dene.", UIStyles.Orange);
            tkSheetDirty = true;
        }
    }
}
