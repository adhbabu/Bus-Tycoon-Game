# Bulutta Derleme — Mac'siz iOS ve Otomatik Android

Bu depo, oyunu **GitHub'ın sunucularında** derleyen hazır iş akışları içerir. Bilgisayarınızda Unity'de
derleme yapmanız veya Mac sahibi olmanız gerekmez:

| İş akışı | Ne yapar | Nerede çalışır |
|---|---|---|
| **Android (Google Play)** | `.aab` (Play'e yüklenecek paket) veya `.apk` (telefonda deneme) üretir; isterseniz Play'e taslak olarak yükler | Linux sunucusu |
| **iOS (App Store)** | Unity'den Xcode projesi üretir, kiralık Mac'te imzalar, `.ipa` yapar ve **TestFlight**'a yükler | Linux + macOS sunucusu |
| **Web sitesi ve web oyunu** | `main`'e her gönderimde oyunun tarayıcı sürümünü, gizlilik ve destek sayfalarını GitHub Pages'te yayınlar | Linux sunucusu |
| **Mağaza ekran görüntüleri** | Web sürümünü mağaza boyutlarında açıp 4 cihaz × 5 ekran görüntüsü üretir | Linux sunucusu |

Kurulum **bir kerelik**: aşağıdaki "sırları" (şifre/anahtar) GitHub'a girersiniz. Sonra her yeni sürüm için
yalnızca **Actions ▸ iş akışı ▸ Run workflow** düğmesine basarsınız. Sürüm/build numarası her çalıştırmada
otomatik artar.

> Mağaza ve GitHub kuralları değişebilir. Ücret ve kota bilgileri yazıldığı tarihe aittir; ekrandaki güncel
> uyarıları esas alın.

---

## 0. Başlamadan önce

1. **İş akışları ana dalda olmalı.** "Run workflow" düğmesi yalnızca `.github/workflows/` dosyaları varsayılan
   dalda (`main`) olduğunda görünür.
2. **Depoyu herkese açın** (bir kez): Settings ▸ General ▸ en altta *Danger Zone* ▸ **Change visibility ▸ Public**.
   Böylece GitHub sunucuları ve web sitesi ücretsiz olur. Depoda şifre/anahtar yoktur; sırlar ayrı saklanır.
3. **Web sitesini açın** (bir kez): Settings ▸ **Pages** ▸ *Build and deployment* ▸ Source: **GitHub Actions**.
   Ardından Actions ▸ **Web sitesi ve web oyunu** ▸ Run workflow. Adresler:
   * Site: `https://adhbabu.github.io/Bus-Tycoon-Game/`
   * Oyun: `https://adhbabu.github.io/Bus-Tycoon-Game/oyna/` (Unity sırları eklendikten sonra)
   * Gizlilik politikası: `https://adhbabu.github.io/Bus-Tycoon-Game/gizlilik/`
   * Destek: `https://adhbabu.github.io/Bus-Tycoon-Game/destek/`
4. **Sır nasıl eklenir:** GitHub'da depo ▸ **Settings ▸ Secrets and variables ▸ Actions ▸ New repository secret**.
   *Name* kutusuna aşağıdaki adı **birebir** yazın, *Secret* kutusuna değeri yapıştırın. Sırlar şifrelenir;
   kayıttan sonra siz de dahil kimse göremez, günlüklerde `***` olarak gizlenir.
5. **Maliyet:** Herkese açık (public) depolarda GitHub'ın standart sunucuları ücretsizdir. Özel (private) depolarda
   aylık ücretsiz dakika kotası vardır ve **macOS dakikaları kotayı Linux'a göre çok daha hızlı tüketir** (yazıldığı
   tarihte 10 kat). Bir iOS yüklemesi Mac'te yaklaşık 15–30 dakika sürer. Kalan kotayı GitHub ▸ Settings ▸
   Billing'den görebilirsiniz.
6. **İlk derleme uzun sürer** (Unity'nin içe aktarma önbelleği boştur: 30–60 dk). Sonrakiler daha hızlıdır.

---

## 1. Unity lisansı (Android, iOS ve web oyunu için gerekli)

Unity, ücretsiz **Personal** lisansla da bulutta çalışır. Lisans dosyasını bir kez almanız gerekir:

1. Windows'a **[Unity Hub](https://unity.com/download)**'ı kurun ve Unity hesabınızla giriş yapın.
2. Hub ▸ sol alttaki dişli (**Preferences**) ▸ **Licenses** ▸ **Add** ▸ **Get a free personal license**.
3. Not Defteri ile şu dosyayı açın ve **tüm içeriğini** kopyalayın: `C:\ProgramData\Unity\Unity_lic.ulf`
   (Klasör gizliyse Dosya Gezgini'nin adres çubuğuna yolu yapıştırın.)

| Sır adı | Değer |
|---|---|
| `UNITY_LICENSE` | `Unity_lic.ulf` dosyasının tüm içeriği (`<?xml` ile başlar) |
| `UNITY_EMAIL` | Unity hesabınızın e-postası |
| `UNITY_PASSWORD` | Unity hesabınızın şifresi |

> Unity'ye Google/Apple ile giriş yapıyorsanız hesabınızda bir şifre olmayabilir: id.unity.com ▸ **Security**
> bölümünden şifre belirleyin.

---

## 2. Android (Google Play)

### 2.1 Yükleme anahtarı (keystore) — bir kez
Google Play'e yüklenen her paket **aynı** anahtarla imzalanmalıdır. **Anahtarı ve şifresini kaybederseniz**
Play Console üzerinden anahtar sıfırlama talebi açmanız gerekir; iki yerde yedekleyin. **Asla depoya eklemeyin.**

**Yol A — Unity ile (kolay):** Projeyi Unity'de açın ▸ Edit ▸ Project Settings ▸ Player ▸ Android ▸
**Publishing Settings ▸ Keystore Manager** ▸ Keystore… ▸ **Create New ▸ Anywhere** ▸ dosya adı `upload.keystore`,
şifre, ardından **Alias** (ör. `upload`), alias şifresi ve en az 25 yıl geçerlilik ▸ **Add Key**.

**Yol B — Komut satırıyla:** Unity'nin içindeki Java aracıyla (PowerShell):
```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.27f1\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe" `
  -genkeypair -v -keystore upload.keystore -alias upload -keyalg RSA -keysize 2048 -validity 10000
```

Anahtar dosyasını GitHub sırrına koymak için metne (Base64) çevirip panoya kopyalayın (PowerShell):
```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\Users\<adınız>\upload.keystore")) | Set-Clipboard
```

| Sır adı | Değer |
|---|---|
| `ANDROID_KEYSTORE_BASE64` | Yukarıdaki komutun panoya kopyaladığı uzun metin |
| `ANDROID_KEYSTORE_PASS` | Keystore şifresi |
| `ANDROID_KEYALIAS_NAME` | Alias adı (ör. `upload`) |
| `ANDROID_KEYALIAS_PASS` | Alias şifresi (keystore şifresiyle aynıysa bu sırrı eklemeyebilirsiniz) |

> Şifreler `-` (eksi) işaretiyle başlamamalı; Unity'nin komut satırı bunu yeni bir seçenek sanar.

### 2.2 Derleme
GitHub ▸ **Actions ▸ Android (Google Play) ▸ Run workflow**:

* **aab** — Google Play'e yüklenecek paket (imza anahtarı gerekli).
* **apk** — Kendi Android telefonunuzda denemek için. Anahtar sırları yoksa test (debug) anahtarıyla imzalanır.
  Telefonda "bilinmeyen kaynaklardan yükleme" izni isteyebilir.

Bitince çalıştırmanın sayfasının altındaki **Artifacts** bölümünden zip'i indirip açın.

### 2.3 Play Console'a yükleme
* **İlk paket elle yüklenmelidir** (Google, yeni bir uygulamanın ilk paketini API ile kabul etmez):
  Play Console ▸ uygulamanız ▸ **Test ▸ Kapalı test** (veya Dahili test) ▸ yeni sürüm ▸ `.aab`'yi sürükleyin.
  Sonraki adımlar: [YAYIN-REHBERI.md §2.4](YAYIN-REHBERI.md#24-play-consoleda-uygulama).
* **Sonraki sürümleri otomatik yüklemek (isteğe bağlı):**
  1. [Google Cloud Console](https://console.cloud.google.com)'da bir proje açın ▸ **Google Play Android Developer API**'yi etkinleştirin.
  2. IAM ▸ **Service Accounts** ▸ hesap oluşturun ▸ **Keys ▸ Add key ▸ JSON** ▸ dosya iner.
  3. Play Console ▸ **Kullanıcılar ve izinler** ▸ hizmet hesabının e-postasını davet edin ▸ uygulamanız için
     sürüm yönetme izinleri verin.
  4. JSON dosyasının tüm içeriğini `PLAY_SERVICE_ACCOUNT_JSON` sırrına koyun.
  5. Run workflow'da **Google Play'e taslak olarak yükle** kutusunu işaretleyin. Paket **Dahili test** kanalına
     **taslak** olarak gelir; Play Console'da gözden geçirip yayınlarsınız.

---

## 3. iOS (App Store) — Mac olmadan

Akış: Linux sunucusunda Unity **Xcode projesini** üretir → GitHub'ın Mac sunucusu, açık kaynaklı
[Codemagic CLI araçlarıyla](https://github.com/codemagic-ci-cd/cli-tools) sertifika ve profili App Store Connect
API'si üzerinden hazırlar, `.ipa` üretir ve **App Store Connect'e (TestFlight)** yükler.

### 3.1 Apple tarafı — bir kez (hepsi tarayıcıdan)
1. **[Apple Developer Program](https://developer.apple.com/programs/enroll/)** üyeliği (yıllık 99 $). Kayıt web'den
   veya iPhone'daki **Apple Developer** uygulamasından yapılabilir.
2. **Bundle ID kaydı:** [developer.apple.com/account](https://developer.apple.com/account) ▸ **Certificates, Identifiers & Profiles ▸
   Identifiers ▸ +** ▸ App IDs ▸ App ▸ *Explicit* Bundle ID: `com.adhbabu.otobusimparatoru`
   (`StoreBuild.cs` ▸ `StoreSettings.ApplicationId` ile **birebir** aynı). Ek yetenek (capability) seçmeyin.
3. **Uygulama kaydı:** [App Store Connect](https://appstoreconnect.apple.com) ▸ Uygulamalar ▸ **+ Yeni Uygulama** ▸
   iOS, ad, ana dil Türkçe, az önceki Bundle ID, SKU (ör. `OTOBUS001`).
4. **API anahtarı:** App Store Connect ▸ **Kullanıcılar ve Erişim ▸ Entegrasyonlar ▸ App Store Connect API** ▸
   (ilk seferde hesap sahibi **Erişim İste** demelidir) ▸ **Takım Anahtarları ▸ +** ▸ ad: `GitHub`, erişim: **Admin** ▸
   Oluştur. **`.p8` dosyasını indirin — yalnızca bir kez indirilebilir.** Sayfada **Key ID** ve
   **Issuer ID** (Yayıncı Kimliği) görünür.

| Sır adı | Değer |
|---|---|
| `APPSTORE_ISSUER_ID` | Issuer ID (uzun, tireli kimlik) |
| `APPSTORE_KEY_ID` | Key ID (10 karakter) |
| `APPSTORE_P8` | `.p8` dosyasının Not Defteri'ndeki tüm içeriği (`-----BEGIN PRIVATE KEY-----` satırları dahil) |

### 3.2 Dağıtım sertifikası anahtarı — bir kez
Apple sertifikası bir özel anahtara bağlıdır. Bu anahtarı bir kez üretip sır olarak saklarsınız; iş akışı ilk
çalıştırmada bu anahtarla bir **Apple Distribution** sertifikası oluşturur, sonraki çalıştırmalarda aynısını
kullanır (her seferinde yeni sertifika açılmaz). Windows 10/11'de (PowerShell):

```powershell
ssh-keygen -t rsa -b 2048 -m PEM -f ios_dagitim_anahtari
```
Parola sorulduğunda **iki kez Enter**'a basın (parolasız olmalı). İki dosya oluşur; **`.pub` uzantısız** olanı
Not Defteri ile açın:

| Sır adı | Değer |
|---|---|
| `IOS_CERTIFICATE_PRIVATE_KEY` | `ios_dagitim_anahtari` dosyasının tüm içeriği (`-----BEGIN RSA PRIVATE KEY-----` ile başlar) |

Bu dosyayı güvenli bir yerde yedekleyin ve depoya eklemeyin.

### 3.3 Derleme ve TestFlight
GitHub ▸ **Actions ▸ iOS (App Store) ▸ Run workflow**:

* **testflight** — Unity derlemesi → Mac'te imzalama → `.ipa` → App Store Connect'e yükleme.
* **xcode** — Yalnızca Unity kısmını dener, Apple hesabı gerektirmez (sırlarınızı girmeden önce projenin
  derlendiğini görmek için iyi bir ilk adım).

Yüklemeden sonra Apple'ın işlemesi 5–30 dakika sürer; build, App Store Connect ▸ **TestFlight**'ta görünür.
iPhone'unuza **TestFlight** uygulamasını kurup kendinizi dahili test kullanıcısı olarak ekleyerek oyunu
deneyin. İhracat uyumu (şifreleme) sorusu otomatik cevaplanır. Mağaza sayfası, ekran görüntüleri ve incelemeye
gönderme: [YAYIN-REHBERI.md §3.3](YAYIN-REHBERI.md#33-app-store-connect) — hepsi tarayıcıdan yapılır.

> **Ekran görüntüleri** Mac gerektirmez: Unity Editor'da Game view çözünürlüğünü 2868×1320 (iPhone) veya
> 2752×2064 (iPad) yapıp **F12**'ye basın ([YAYIN-REHBERI.md](YAYIN-REHBERI.md#mağaza-ekran-görüntüleri)).

---

## 4. Web sürümü ve mağaza ekran görüntüleri

* **Oynamak / test etmek:** `https://adhbabu.github.io/Bus-Tycoon-Game/oyna/` — bilgisayarda ve telefonun
  tarayıcısında çalışır, kayıtlar tarayıcıda saklanır. Bilgisayarda telefon düzenini görmek için adresin sonuna
  `?profil=telefon` (veya `?profil=tablet`) ekleyin. Test kullanıcılarınıza da bu bağlantıyı gönderebilirsiniz.
* **Mağaza ekran görüntüleri:** Actions ▸ **Mağaza ekran görüntüleri** ▸ Run workflow ▸ Artifacts ▸
  `store-screenshots`. Klasörler mağaza ve cihaza göre ayrılmıştır:

| Klasör | Boyut | Nereye yüklenir |
|---|---|---|
| `google-play/telefon` | 1920×1080 | Play Console ▸ Mağaza girişi ▸ Telefon ekran görüntüleri |
| `google-play/tablet` | 2560×1440 | 7" ve 10" tablet ekran görüntüleri |
| `app-store/iphone-6.9` | 2868×1320 | App Store Connect ▸ iPhone 6,9" |
| `app-store/ipad-13` | 2752×2064 | App Store Connect ▸ iPad 13" |

Görüntüler, oyunun içindeki vitrin turuyla (orta seviyede, iyi yönetilen bir şirket: 8 hat, ~30 otobüs) gerçek oyundan alınır.
Turu tarayıcıda izlemek için: `…/oyna/?vitrin&profil=telefon`.

## 5. Yeni sürüm yayınlamak

1. Değişikliklerinizi `main`'e gönderin. Kullanıcıya görünen sürümü değiştirmek isterseniz GitHub'da
   `Assets/_Project/Scripts/Editor/StoreBuild.cs` ▸ kalem simgesi ▸ `Version = "1.0.1"` ▸ Commit.
2. İki iş akışını da çalıştırın. Build numarası (Android sürüm kodu, iOS build) otomatik olarak çalıştırma
   numarasını alır ve her seferinde artar.
3. Daha önce elle daha büyük bir numarayla yükleme yaptıysanız Run workflow'daki **build numarası** kutusuna
   o numaradan büyük bir sayı yazın.

---

## 6. Sorun giderme

| Belirti (iş akışı günlüğünde) | Çözüm |
|---|---|
| `'UNITY_LICENSE' sırrı eksik` vb. | İlgili sırrı §1–3'teki adla ekleyin (büyük/küçük harf önemli). |
| Unity lisansı etkinleştirilemedi | `.ulf` dosyasının **tamamını** kopyaladığınızdan, e-posta/şifrenin doğru olduğundan emin olun. |
| `No space left on device` | İş akışını yeniden çalıştırın. |
| `Keystore was tampered with, or password was incorrect` | `ANDROID_KEYSTORE_PASS` / `ANDROID_KEYALIAS_PASS` yanlış. |
| Play: `Version code … has already been used` | Run workflow'da daha büyük bir **build numarası** girin. |
| Play: `Package not found` | Uygulamanın ilk paketini Play Console'a elle yükleyin (§2.3). |
| iOS: Bundle ID / uygulama bulunamadı | §3.1'deki Bundle ID kaydını ve App Store Connect uygulamasını oluşturun; kimlik `StoreSettings.ApplicationId` ile aynı olmalı. |
| iOS: `maximum number of certificates` | developer.apple.com ▸ Certificates'ta kullanılmayan eski bir **Apple Distribution** sertifikasını iptal edin, tekrar çalıştırın. |
| iOS: `bundle version must be higher` | Daha büyük bir **build numarası** girin. |
| Unity derleme hatası (C# hata satırları) | Günlüğü paylaşın; Claude'a "iş akışı başarısız oldu" demeniz yeterli, GitHub'daki günlüğü okuyup düzeltebilir. |

**Neden bu araçlar?** Unity derlemesi için yaygın kullanılan açık kaynaklı [GameCI](https://game.ci) eylemi,
iOS imzalama için [Codemagic CLI araçları](https://github.com/codemagic-ci-cd/cli-tools), Play'e yükleme için
[upload-google-play](https://github.com/r0adkll/upload-google-play) eylemi kullanılır. Hepsi GitHub'ın kendi
sunucularında çalışır; sırlarınız yalnızca ilgili hizmetlere (Unity lisans sunucusu, Apple ve Google API'leri) iletilir.
