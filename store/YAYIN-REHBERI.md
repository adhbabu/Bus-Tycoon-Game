# Yayın Rehberi — Google Play ve App Store

Bu klasörde mağazaların istediği her şey hazır: ikonlar, Play tanıtım görseli, TR/EN mağaza metinleri ve
gizlilik politikası. Oyun da mobile hazır (dokunmatik kontrol, iki parmakla yakınlaştırma, telefona uygun
arayüz, çentik/güvenli alan, Android geri tuşu, arka plana alınınca kayıt). Bu rehber, **yalnızca sizin
yapabileceğiniz** adımları sırayla anlatır: hesaplar, imza anahtarı, derleme, yükleme ve inceleme.

> Mağaza kuralları sık değişir. Aşağıdaki şartlar yazıldığı tarihteki bilgiye dayanır; Play Console ve
> App Store Connect'teki uyarıları her zaman esas alın.

## Dosyalar

| Dosya | Nerede kullanılır |
|---|---|
| `Assets/_Project/Art/AppIcon.png` (1024×1024) | Unity uygulama ikonu (Android + iOS) — menü komutu otomatik ayarlar |
| `store/app-store/AppIcon-1024.png` | App Store Connect ikonu (saydamlık yok) |
| `store/google-play/icon-512.png` | Play Store ikonu (512×512) |
| `store/google-play/feature-graphic.png` | Play Store tanıtım görseli (1024×500) |
| `store/listing-tr.md`, `store/listing-en.md` | Ad, kısa/uzun açıklama, anahtar kelimeler, kategori |
| `store/privacy-policy.md` | Gizlilik politikası (TR + EN) — internette yayınlanmalı |
| `Tools/make_store_art.py` | Görselleri yeniden üretir |
| `store/BULUT-DERLEME.md` | **Mac'siz iOS** ve otomatik Android derlemesi (GitHub Actions) |

---

## 0. Önce karar verin (sonradan değiştirmesi zor)

`Assets/_Project/Scripts/Editor/StoreBuild.cs` → `StoreSettings`:

| Ayar | Varsayılan | Not |
|---|---|---|
| `ApplicationId` | `com.adhbabu.otobusimparatoru` | **Kalıcıdır.** İlk yüklemeden sonra değiştirilemez. Küçük harf, nokta ile. |
| `ProductName` | `Otobüs İmparatoru` | Telefonda ikonun altındaki ad. iPhone'da uzun adlar "Otobüs İmpar…" diye kısalabilir. |
| `Version` / `BuildNumber` | `1.0.0` / `1` | **Her yüklemede BuildNumber'ı 1 artırın** (mağazalar aynı numarayı ikinci kez kabul etmez). |

Mağazadaki ad (`Otobüs İmparatoru: Bus Tycoon`) ile cihazdaki ad farklı olabilir. Adı seçmeden önce iki
mağazada aynı/benzer adlı uygulama olup olmadığına bakın; App Store birebir aynı adı kabul etmez.

## 1. Unity'de hazırlık (her iki mağaza)

1. Unity Hub ▸ **Add ▸ Add project from disk** ▸ depo klasörü (proje sürümü: Unity 6.3 LTS, 6000.3.27f1).
   Yerelde derleyecekseniz Installs ▸ bu sürüm ▸ **Add modules**: **Android Build Support** (OpenJDK + Android
   SDK & NDK Tools dahil) ve **iOS Build Support**. Derlemeyi bulutta yapacaksanız modül gerekmez
   ([BULUT-DERLEME.md](BULUT-DERLEME.md)).
2. Projeyi açın ▸ **Bus Tycoon ▸ Mağaza ▸ 1 - Mağaza ayarlarını uygula**.
   Bu komut: kimlik, sürüm, yalnız yatay ekran, IL2CPP + ARM64, Android App Bundle (.aab), uygulama ikonu
   ve `Assets/_Project/Scenes/Main.unity` sahnesini (Build Settings'e ekli) ayarlar.
3. Edit ▸ Project Settings ▸ **Services**: Unity Analytics / Cloud Diagnostics **kapalı** kalsın
   (gizlilik politikası ve form cevapları "veri toplanmaz" der).
4. Önce Editor'da Play ile oynayın. Telefon görünümünü denemek için Game view ▸ çözünürlük
   (ör. 2400×1080) seçin. Gerçek dokunmatik test için aşağıdaki "cihazda test" adımına bakın.

### Mağaza ekran görüntüleri

Gerçek oyundan alınmalıdır (iki mağaza da temsili/sahte görüntüyü reddedebilir). **En kolayı:** GitHub ▸ Actions ▸
**Mağaza ekran görüntüleri** iş akışı, web sürümünden dört cihaz boyutunda 5'er görüntüyü otomatik üretir
([BULUT-DERLEME.md §4](BULUT-DERLEME.md#4-web-sürümü-ve-mağaza-ekran-görüntüleri)). Elle almak için Editor'da:
Game view ▸ **+** ▸ sabit çözünürlük ekleyin, oyunu ilgi çekici bir ana getirin ve **F12**'ye basın.
Görüntüler proje klasöründe `Screenshots/` altına tam o çözünürlükte kaydedilir.

| Mağaza | Gerekli | Önerilen çözünürlük (yatay) |
|---|---|---|
| Google Play telefon | en az 2 (öneri 4–8) | 1920×1080 |
| Google Play tablet (önerilir) | 7" ve 10" için ayrı | 2560×1440 |
| App Store iPhone 6,9" | en az 1 (öneri 4–6) | 2868×1320 |
| App Store iPad 13" | oyun iPad'de çalıştığı için gerekli | 2752×2064 |

İyi kareler: hat açma teklif kartı, fiyat paneli (kâr eğrisi), çok hatlı ağ, galeri, harita genel görünümü.

---

## 2. Google Play

### 2.1 Hesap
* [Play Console](https://play.google.com/console)'da geliştirici hesabı açın: tek seferlik **25 $**, kimlik doğrulaması.
* **Önemli:** Kasım 2023'ten sonra açılan **kişisel** hesaplarda, üretime (herkese açık) çıkmadan önce
  **en az 12 test kullanıcısıyla en az 14 gün kesintisiz kapalı test** şartı vardır. Test kullanıcılarını
  (arkadaş, aile) önceden ayarlayın.

### 2.2 İmza anahtarı (bir kez)
Unity ▸ Edit ▸ Project Settings ▸ Player ▸ Android ▸ **Publishing Settings ▸ Keystore Manager** ▸
Create New ▸ dosyayı ve şifreleri **güvenli yerde yedekleyin**. Bu "yükleme anahtarıdır"; Google, uygulamayı
kendi anahtarıyla imzalar (Play App Signing). Anahtarı veya şifresini **asla** Git deposuna koymayın.

### 2.3 Derleme
**Bus Tycoon ▸ Mağaza ▸ 2 - Android derle** → `Builds/Android/OtobusImparatoru.aab`.

*Bulutta:* GitHub ▸ Actions ▸ **Android (Google Play)** ▸ Run workflow — anahtar GitHub sırlarından okunur,
sürüm kodu otomatik artar ([BULUT-DERLEME.md §2](BULUT-DERLEME.md#2-android-google-play)).

*Cihazda test:* Android telefonu USB ile bağlayıp (Geliştirici seçenekleri ▸ USB hata ayıklama) File ▸ Build
Profiles ▸ Android ▸ "Build App Bundle" işaretini kaldırıp **Build And Run** ile APK olarak yükleyin
(sonra tekrar işaretleyin) — ya da .aab'yi Play Console'da "Dahili test" kanalına yükleyin.

### 2.4 Play Console'da uygulama
1. **Uygulama oluştur**: ad, varsayılan dil Türkçe, Oyun, Ücretsiz.
2. **Uygulamanızı ayarlayın** listesindeki formlar (cevaplar §4'te):
   gizlilik politikası URL'si · uygulama erişimi · reklamlar · içerik derecelendirmesi · hedef kitle ·
   veri güvenliği · haber/hükümet/finans/sağlık uygulaması değil.
3. **Mağaza girişi**: `store/listing-tr.md` metinleri, `icon-512.png`, `feature-graphic.png`, ekran görüntüleri,
   kategori **Strateji**, iletişim e-postası. İsterseniz İngilizce çeviri ekleyin (`listing-en.md`).
4. **Test ▸ Kapalı test**: sürüm oluşturun, .aab'yi yükleyin, test kullanıcılarının e-postalarını ekleyin,
   katılım bağlantısını paylaşın. 14 gün sonunda **Üretim erişimi için başvurun**.
5. **Üretim** sürümü ▸ incele ▸ yayınla. İnceleme genelde birkaç gün sürer.

---

## 3. App Store (iOS)

### 3.1 Gerekenler
* [Apple Developer Program](https://developer.apple.com/programs/) üyeliği: **yıllık 99 $**.
* Derleme için **ya** bir Mac ve güncel **Xcode** (§3.2) **ya da** Mac'siz bulut derlemesi (aşağıda).

> **Mac'iniz yoksa:** GitHub ▸ Actions ▸ **iOS (App Store)** iş akışı Xcode projesini bulutta üretir, GitHub'ın
> Mac sunucusunda imzalar ve doğrudan **TestFlight**'a yükler. Bir kerelik kurulum (Bundle ID, API anahtarı,
> sertifika anahtarı): [BULUT-DERLEME.md §3](BULUT-DERLEME.md#3-ios-app-store--mac-olmadan). Bu durumda §3.2'yi
> atlayıp §3.3 ile devam edin.

### 3.2 Derleme ve yükleme (Mac ile)
1. Mac'te Unity ▸ **Bus Tycoon ▸ Mağaza ▸ 3 - iOS Xcode projesi üret** → `Builds/iOS/`.
   (İhracat uyumu cevabı — şifreleme yok — `IosPostProcess` ile Info.plist'e otomatik yazılır.)
2. `Builds/iOS/Unity-iPhone.xcodeproj`'u Xcode'da açın ▸ Unity-iPhone hedefi ▸ **Signing & Capabilities** ▸
   Team: geliştirici hesabınız ("Automatically manage signing" açık).
3. Kendi iPhone'unuzda deneyin (cihazı seçip ▶).
4. Product ▸ **Archive** ▸ Distribute App ▸ **App Store Connect** ▸ Upload.

### 3.3 App Store Connect
1. [App Store Connect](https://appstoreconnect.apple.com) ▸ Uygulamalar ▸ **+ Yeni Uygulama**: iOS, ad,
   ana dil Türkçe, Bundle ID (`StoreSettings.ApplicationId`), SKU (ör. `OTOBUS001`).
2. **Uygulama Bilgileri**: kategori Oyunlar ▸ Strateji (+ Simülasyon), içerik hakları (§4), yaş derecelendirmesi.
3. **Fiyatlandırma**: Ücretsiz. **Uygulama Gizliliği**: gizlilik URL'si + "Veri Toplanmıyor".
4. **Sürüm sayfası**: ekran görüntüleri (iPhone 6,9" + iPad 13"), tanıtım metni, açıklama, anahtar kelimeler,
   destek URL'si, telif hakkı, yüklenen **build**'i seçin.
5. **App Review bilgileri**: iletişim; not: "Giriş gerekmez. Oyun çevrimdışıdır; hat açmak için haritada iki
   şehre dokunun." ▸ **İncelemeye gönder**. Önce TestFlight ile kendiniz test etmeniz önerilir.

---

## 4. Formlardaki hazır cevaplar

| Soru | Cevap |
|---|---|
| Uygulama veri topluyor/paylaşıyor mu? (Play Veri güvenliği, Apple App Privacy) | **Hayır** — veri toplanmıyor |
| Reklam içeriyor mu? | Hayır |
| Uygulama içi satın alma | Yok |
| Uygulamaya erişim için giriş gerekiyor mu? | Hayır, tüm özellikler girişsiz |
| İçerik derecelendirmesi (IARC / Apple) | Şiddet, cinsellik, küfür, kumar, uyuşturucu, kullanıcı etkileşimi, konum paylaşımı: **yok** → beklenen PEGI 3 / ESRB Everyone / 4+ |
| Hedef kitle (Play) | 13 yaş ve üstü önerilir (13 altı seçilirse Aileler politikası ek şartları gelir) |
| Şifreleme / ihracat uyumu (Apple) | Şifreleme kullanmıyor (Info.plist'te otomatik) |
| Üçüncü taraf içerik (Apple içerik hakları) | Evet, haklara sahibim: harita Natural Earth (kamu malı), şehir verisi GeoNames (CC BY 4.0, oyunda atıf var) |
| İnternet izni | Gerekmez (oyun çevrimdışı) |

## 5. Gizlilik politikası ve destek sayfası

İki mağaza da herkese açık bir **gizlilik politikası URL'si**, App Store ayrıca bir **destek URL'si** ister.
"Web sitesi ve web oyunu" iş akışı bunları `store/privacy-policy.md` ve `store/support.md`'den üretip GitHub
Pages'te yayınlar (bir kerelik kurulum: [BULUT-DERLEME.md §0](BULUT-DERLEME.md#0-başlamadan-önce)):
* Gizlilik politikası: `https://adhbabu.github.io/Bus-Tycoon-Game/gizlilik/`
* Destek: `https://adhbabu.github.io/Bus-Tycoon-Game/destek/`

## 6. Güncelleme yayınlamak

`StoreSettings.BuildNumber`'ı (ve gerekirse `Version`'ı) artırın ▸ menüden yeniden derleyin ▸ Play'de yeni
sürüm / Xcode'da yeni Archive ▸ yükleyin. Bulut derlemesinde build numarası otomatik artar; yalnızca
`Version`'ı değiştirip iş akışlarını yeniden çalıştırmanız yeterlidir.
