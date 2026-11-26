using BusTycoon.Core;

namespace BusTycoon.Data
{
    /// <summary>
    /// Gerçek parayla satılan küçük dükkan (App Store ve Google Play uygulama içi satın alma). Ürün kimlikleri iki
    /// mağazada aynı açılır (store/DUKKAN.md). Oyun dengesini bozmaz: görünüm ve destekçi paketi yalnız görünüştür,
    /// şehir lisansı oyun parasıyla da alınabilen bir kestirmedir, sermaye paketi bir kerelik küçük bir destektir.
    /// </summary>
    public static class ShopCatalog
    {
        public const string Looks = "gorunum_paketi";
        public const string CityLicense = "sehir_lisansi";
        public const string Supporter = "destekci_paketi";
        public const string Capital = "sermaye_paketi";

        /// <summary>Sermaye paketinin kasaya eklediği tutar: ₺1.000.000 (iç birim).</summary>
        public const double CapitalUnits = 1_000_000 / Money.LiraPerUnit;

        public sealed class Product
        {
            public string Id, Title, Description, Symbol;
            /// <summary>Mağaza fiyatı gelene kadar (ya da test dükkanında) gösterilen fiyat.</summary>
            public string FallbackPrice;
            /// <summary>Tekrar alınabilir (sermaye, lisans hakkı); değilse bir kez alınır, geri yüklenir.</summary>
            public bool Consumable;
            /// <summary>Çok oyunculu yarışta alınamaz ve uygulanmaz (oyun parasına ya da lisansa dokunur).</summary>
            public bool BlockedInRace;
        }

        public static readonly Product[] Products =
        {
            new Product
            {
                Id = Looks, Title = "Görünüm paketi", Symbol = "palette", FallbackPrice = "₺29,99",
                Description = "6 özel logo ve 6 araç rengi. Şirketim sayfasından seçilir; yalnız görünüştür."
            },
            new Product
            {
                Id = CityLicense, Title = "Şehir lisansı", Symbol = "location_city", FallbackPrice = "₺19,99", Consumable = true, BlockedInRace = true,
                Description = "Bir şehrin lisansını hemen açar. Aynı lisans oyun parasıyla da alınabilir."
            },
            new Product
            {
                Id = Supporter, Title = "Destekçi paketi", Symbol = "workspace_premium", FallbackPrice = "₺49,99",
                Description = "Altın logo çerçevesi, altın araç rengi ve destekçi rozeti. Oyunun gelişmesine destek olursun."
            },
            new Product
            {
                Id = Capital, Title = "Sermaye paketi", Symbol = "savings", FallbackPrice = "₺24,99", Consumable = true, BlockedInRace = true,
                Description = "Kasana bir kerelik ₺1.000.000 eklenir."
            },
        };

        public static Product Get(string id)
        {
            foreach (var product in Products) if (product.Id == id) return product;
            return null;
        }
    }
}
