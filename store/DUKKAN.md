# Dükkan (uygulama içi satın alma) — kurulum rehberi

Oyunda isteğe bağlı küçük bir dükkan var: üst çubuktaki **dükkan** (vitrin) düğmesi. Reklam yok.
Kod hazır: Unity IAP 5 (`com.unity.purchasing` 5.5.1), ürünler kodda tanımlı (`Scripts/Runtime/Data/ShopCatalog.cs`).
Senin yapacağın: iki mağazada **aynı kimliklerle** 4 ürünü açmak ve ödeme bilgilerini tamamlamak.

## Ürünler

| Kimlik (Product ID) | Ad | Tür | Önerilen fiyat | Ne verir |
|---|---|---|---|---|
| `gorunum_paketi` | Görünüm paketi | Tüketilmeyen (Non-consumable / bir kez) | ₺29,99 | 6 özel logo + 6 araç rengi (yalnız görünüş) |
| `sehir_lisansi` | Şehir lisansı | Tüketilen (Consumable / tekrar alınır) | ₺19,99 | Bir şehrin lisansını açar (oyun parasıyla da alınabilir) |
| `destekci_paketi` | Destekçi paketi | Tüketilmeyen (Non-consumable / bir kez) | ₺49,99 | Altın logo çerçevesi, altın araç rengi, destekçi rozeti |
| `sermaye_paketi` | Sermaye paketi | Tüketilen (Consumable / tekrar alınır) | ₺24,99 | Kasaya ₺1.000.000 (oyun parası) |

* Fiyatlar mağazanın Türkiye fiyat basamaklarına yuvarlanır; oyunda **mağazanın gösterdiği fiyat** yazar.
* Sermaye ve şehir lisansı **çok oyunculu yarışta** alınmaz ve uygulanmaz (yarış adil kalsın).
* Bir kez alınan paketler cihazda tutulur ve **Satın alımları geri yükle** ile aynı mağaza hesabında geri gelir
  (Apple bu düğmeyi şart koşar; oyunda var). Sermaye ve lisans hakkı alındığı oyuna işlenir.
* Web sürümünde dükkan sayfası açılır ama satın alma kapalıdır ("yalnız mobil uygulamada").
* Test: `…/oyna/?dukkan=test` (ya da `-dukkan test`) sahte dükkan açar; ödeme alınmaz, ürün anında verilir.

## Google Play Console

1. **Ödeme profili:** Play Console ▸ Kurulum ▸ Ödemeler profili (Payments profile). Satış yapmak için gerekli.
   Banka hesabı (IBAN) ve vergi bilgileri burada; adı soyadı/adres bilgisini sen girersin.
2. Uygulamanın **ilk sürümünü yükle** (kapalı test kanalı yeterli). Google, faturalandırma izni (BILLING) olan bir
   sürüm yüklenmeden ürün açtırmaz. Unity IAP bu izni derlemeye kendisi ekler.
3. Uygulama ▸ **Gelir elde et ▸ Ürünler ▸ Uygulama içi ürünler ▸ Ürün oluştur** — her ürün için:
   * Ürün kimliği: tablodaki kimlik (sonradan değiştirilemez).
   * Ad ve açıklama: tablodaki gibi (Türkçe; istersen İngilizce çeviri).
   * Fiyat: önerilen fiyat ▸ **Etkinleştir**.
   * Play'de tüketilen/tüketilmeyen ayrımı ürün ayarında yok; oyun kendisi yönetir (kodda tanımlı).
4. **Test:** Kurulum ▸ Lisans testi ▸ kendi Google hesabını ekle. Test hesabıyla yapılan satın almalardan para
   çekilmez.
5. **Veri güvenliği formu:** Oyun satın alma geçmişini kendisi toplamıyor/göndermiyor (ödemeyi Google işliyor) →
   "veri toplanmıyor" cevabı geçerli kalır.

## App Store Connect

1. **Sözleşmeler:** App Store Connect ▸ İş (Business) ▸ **Ücretli Uygulamalar Sözleşmesi** (Paid Apps Agreement)
   → kabul et, banka ve vergi formlarını doldur. Bu olmadan satın alma çalışmaz (test dahil).
2. Uygulama ▸ **Monetizasyon ▸ Uygulama İçi Satın Almalar ▸ +** — her ürün için:
   * Tür: tablodaki gibi **Tüketilen** (Consumable) ya da **Tüketilmeyen** (Non-Consumable).
   * Referans adı: ürün adı · Ürün kimliği: tablodaki kimlik.
   * Fiyat: önerilen fiyata en yakın basamak · Yerelleştirme: ad + açıklama.
   * **İnceleme bilgisi:** dükkan sayfasının ekran görüntüsü (oyunda üst çubuk ▸ dükkan) ve kısa not:
     "Üst çubuktaki dükkan düğmesinden açılır."
3. İlk satın almalar **uygulamanın sürümüyle birlikte incelemeye** gönderilir: sürüm sayfasında "Uygulama İçi
   Satın Almalar" bölümünden 4 ürünü sürüme ekle.
4. **Test:** Kullanıcılar ve Erişim ▸ Sandbox ▸ test hesabı. TestFlight derlemesinde sandbox hesabıyla satın al.
5. **App Privacy:** satın alma Apple üzerinden; uygulama veri toplamıyor → "Veri toplanmıyor" geçerli.

## Kodda nerede?

* `Scripts/Runtime/Data/ShopCatalog.cs` — ürünler, adlar, yedek fiyatlar, sermaye tutarı.
* `Scripts/Game/Shop/ShopService.cs` — sahiplik (cihazda), bekleyen ürünler, aynı işlemin bir kez verilmesi, test dükkanı.
* `Scripts/Store/UnityIapShopBackend.cs` — App Store / Google Play bağlantısı (Unity IAP 5, `PurchasingController`).
  Kendi derlemesinde (`BusTycoon.Store`): yalnız paket kuruluyken derlenir. Web derlemesinde paket çıkarılır
  (`.github/workflows/site.yml`), web indirmesi büyümez.
* `Scripts/Game/UI/GameUI.Shop.cs` — Dükkan sayfası, Şirketim ▸ Görünüm (logo, araç rengi), lisans hakkı.
