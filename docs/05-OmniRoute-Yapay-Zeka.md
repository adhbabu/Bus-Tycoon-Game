# 05 — OmniRoute: Yapay Zekâ Ağ Geçidi

[OmniRoute](https://github.com/diegosouzapw/OmniRoute) açık kaynak (MIT) bir yapay zekâ ağ geçididir: Claude, GPT,
Gemini, GLM, Kimi, DeepSeek ve ücretsiz katmanlı onlarca sağlayıcıyı **tek bir OpenAI uyumlu adreste** toplar,
biri kotaya takılınca ötekine geçer. Bu projede iki yerde kullanılır:

1. **Oyunun içinde** — "Yapay Zekâ Danışman" paneli (kısayol **Y**).
2. **Kodlama araçlarında** — Claude Code, Cursor, Cline, Continue, Codex vb. aynı ağ geçidine bağlanır.

| | |
|---|---|
| Panel | http://localhost:20128 |
| API (OpenAI uyumlu) | `http://localhost:20128/v1` |
| Model | `auto` (OmniRoute seçer) veya `sağlayıcı/model` (ör. `glm/glm-5.2`) |
| Gereken | Node.js 22 (≥22.22.2) ya da 24–26 · Node 23 desteklenmez |

---

## 1. Kurulum (Windows)

Geliştirici bilgisayarında kurulum yapıldı: `Belgeler\OmniRoute\install-omniroute.cmd`
(Node.js LTS'yi winget ile, OmniRoute'u `npm install -g omniroute` ile kurar).

Elle kurmak için:

```powershell
winget install OpenJS.NodeJS.LTS
npm config set allow-scripts=omniroute,keytar,onnxruntime-node,@parcel/watcher,@swc/core,protobufjs,esbuild --location=user
npm install -g omniroute
omniroute
```

**Başlatmak:** `Belgeler\OmniRoute\start-omniroute.cmd` (pencere açık kaldığı sürece çalışır). Bu betik web sürümü
için CORS iznini de verir (bkz. §4).

### İlk açılışta (bir kez, 3 dakika)

1. http://localhost:20128 → varsayılan şifre `CHANGEME` ile girin ve **hemen değiştirin**.
2. **Providers** → en az bir sağlayıcı bağlayın (kendi hesabınızla). Ücretsiz seçenekler için OmniRoute'un
   `docs/getting-started/FREE-TIERS-GUIDE.md` rehberine bakın. Sağlayıcı yoksa `auto` "uygun model yok" der.
3. **Endpoints / API Keys** → bir API anahtarı oluşturup kopyalayın. Oyun ve kodlama araçları bu anahtarı kullanır.

Doğrulama:

```powershell
curl http://localhost:20128/v1/models -H "Authorization: Bearer ANAHTARINIZ"
```

---

## 2. Oyun içi: Yapay Zekâ Danışman

Oyunda **Danışman** menüsü (masaüstünde sol menü, telefonda Menü ▸ Yapay Zekâ Danışman) ya da **Y** tuşu.

1. **Ayarlar** → Adres `http://localhost:20128/v1`, API anahtarı, Model `auto` → **Kaydet ve sına**.
   Rozet "Bağlı" olur ve kullanılabilir modeller listelenir (birine dokunup seçebilirsiniz).
2. **Danışman AÇIK** → hızlı sorulardan birine basın ya da kendi sorunuzu yazın.

Danışman her soruda şirketin kısa özetini görür: kasa, borç günleri, seviye, son 7 günün geliri/gideri, filo
durumu ve hatlar (mesafe, bilet, otobüs, doluluk, karşılanmayan talep, kâr). 12'den fazla hat varsa en zararlı 6
ve en kârlı 6 hat gönderilir; böylece ücretsiz modellerin bağlam sınırına sığar.

### Kod

| Dosya | Görev |
|---|---|
| `Scripts/Runtime/AI/OmniRouteConfig.cs` | Adres/anahtar/model; "localhost:20128" gibi girdileri `…/v1`'e düzeltir |
| `Scripts/Runtime/AI/ChatJson.cs` | `/chat/completions` isteği ve yanıt/hata/model listesi ayrıştırma (Unity'den bağımsız) |
| `Scripts/Runtime/AI/AdvisorBrief.cs` | Sistem talimatı, şirket özeti, mesaj geçmişi, yanıt temizleme (yazı tipinde olmayan emoji/Markdown) |
| `Scripts/Game/AI/OmniRouteClient.cs` | `UnityWebRequest` ile istek; HTTP hatalarını Türkçe açıklar (401, 404, 429, 503…) |
| `Scripts/Game/UI/GameUI.Advisor.cs` | IMGUI paneli, ayarlar (PlayerPrefs), sohbet |
| `Tests/EditMode/OmniRouteAdvisorTests.cs` | 13 test: adres düzeltme, JSON, özet, mesajlar, temizleme |

`Packages/manifest.json`'a `com.unity.modules.unitywebrequest` eklendi.

### Güvenlik

- API anahtarı **derlemeye gömülmez**; yalnızca oyuncunun cihazında (PlayerPrefs) durur.
- Danışman varsayılan olarak **kapalıdır**; oyuncu açmadıkça hiçbir istek gitmez.
- Şirket verisi yalnızca oyuncunun girdiği OmniRoute adresine ve onun seçtiği sağlayıcıya gider.

---

## 3. Kodlama araçları

OmniRoute'un kendi kurulum komutları araç ayarlarını yazar (`--dry-run` ile önce ne yazacağını görün):

```powershell
omniroute setup-claude     # Claude Code   → ~/.claude/profiles/<ad>/settings.json
omniroute setup-codex      # Codex CLI     → ~/.codex/<ad>.config.toml
omniroute setup-cline      # Cline
omniroute setup-continue   # Continue (VS Code / JetBrains)
omniroute setup-cursor     # Cursor        → uygulama içi adımları yazdırır
omniroute launch           # Claude Code'u OmniRoute'a bağlı başlatır (ayar yazmadan)
```

Elle ayar:

| Araç | Ayar |
|---|---|
| **Claude Code** | `ANTHROPIC_BASE_URL=http://localhost:20128` (**`/v1` yok**), `ANTHROPIC_AUTH_TOKEN=<anahtar>`, isteğe bağlı `ANTHROPIC_MODEL=…`. Ortam değişkenleri açılışta okunur; değiştirince yeniden başlatın. |
| **Cursor / Cline / Continue / Roo / Kilo** | Sağlayıcı: *OpenAI Compatible* · Base URL `http://localhost:20128/v1` · API key `<anahtar>` · Model `auto` |
| **Codex CLI** | `omniroute launch-codex` ya da `setup-codex` profili |

PowerShell'de Claude Code için kalıcı ayar:

```powershell
setx ANTHROPIC_BASE_URL "http://localhost:20128"
setx ANTHROPIC_AUTH_TOKEN "ANAHTARINIZ"
```

> **Not:** Bu depoda bulutta çalışan Claude Code oturumları (claude.ai/code) sizin bilgisayarınızdaki
> `localhost`'a erişemez; OmniRoute yalnızca **kendi bilgisayarınızda** çalışan araçlarla kullanılır. Uzaktan
> erişim için OmniRoute'un `docs/guides/REMOTE-MODE.md` rehberine bakın (ağa açmadan önce şifre ve anahtar şart).

---

## 3b. Token tasarrufu ve ücretsiz kotalar

**Sıkıştırma (geliştirici bilgisayarında açık):** Panel → Bağlam → Sıkıştırma. Ayar: ana anahtar AÇIK,
varsayılan mod `stacked` (RTK → Caveman), 32.000 token üstü isteklerde otomatik `stacked`, sistem talimatı korunur,
yanıtlar kısa çıktı modunda. Önizlemede 120 satırlık test çıktısı 1.608 → 32 token'a indi.
Dikkat: RTK başarılı satırlarla birlikte bazı ayrıntıları da atabilir (örnekte "Expected 42, received 41" satırı
düştü). Araç hata ayıklarken bocalarsa modu `lite` yapın:

```bash
curl -X PUT http://localhost:20128/api/settings/compression -H "Content-Type: application/json" -d '{"defaultMode":"lite"}'
```

**"Ayda ~1,6 milyar ücretsiz token"**, OmniRoute'un verdiği bir kota değildir: her sağlayıcının ücretsiz katmanının
toplamıdır ve her biri için ayrı hesap gerekir. Resmî ve tekrarlayan kotası olanlar (Panel → Sağlayıcılar → ara):

| Sağlayıcı | Anahtar | Not |
|---|---|---|
| Google Gemini | https://aistudio.google.com/apikey | Proje başına hız sınırı |
| Groq | https://console.groq.com/keys | ~15M token/ay |
| GitHub Models | GitHub → Settings → Developer settings → Personal access tokens | ~18M token/ay |
| Cloudflare Workers AI | Cloudflare hesabı → AI → API token | ~30M token/ay |
| OpenRouter (`:free` modeller) | https://openrouter.ai/keys | İstek sınırlı |

Hepsini bağlayıp model olarak `auto` kullanınca OmniRoute kotası biteni atlayıp sıradakine geçer.

**Claude Code'u OmniRoute'tan geçirmek:** `Belgeler\OmniRoute\claude-code-omniroute-kur.cmd` Claude Code'u kurar,
OmniRoute anahtarını sorar ve `ANTHROPIC_BASE_URL` / `ANTHROPIC_AUTH_TOKEN` ayarlarını yapar.
`claude-code-omniroute-kaldir.cmd` geri alır.

## 4. Web (WebGL) sürümü

https://adhbabu.github.io/Bus-Tycoon-Game/oyna/ tarayıcıda açıldığında istek tarayıcıdan gider, bu yüzden
OmniRoute'un o adrese CORS izni vermesi gerekir. `start-omniroute.cmd` bunu ayarlar:

```bat
set "CORS_ALLOWED_ORIGINS=https://adhbabu.github.io"
omniroute
```

Chrome ve Edge `https` sayfadan `http://localhost`'a isteğe izin verir; ilk seferde "yerel ağdaki cihazlara erişim" izni sorabilir, izin verin. Safari bu isteği engeller. Yerel derleme (masaüstü, Android, iOS)
için CORS gerekmez; ancak telefon `localhost` ile kendi kendine bağlanır. Telefondan bilgisayardaki OmniRoute'a
bağlanmak için adres olarak bilgisayarın yerel IP'sini (ör. `http://192.168.1.20:20128/v1`) yazın ve OmniRoute'u
ağa açma rehberini izleyin.

---

## 5. Sorun giderme

| Belirti | Çözüm |
|---|---|
| "OmniRoute'a ulaşılamadı" | `start-omniroute.cmd` penceresi açık mı? http://localhost:20128 açılıyor mu? |
| "API anahtarı reddedildi" (401) | Panel → Endpoints'ten anahtarı yeniden kopyalayın |
| "Uygun model yok" (503) | Panel → Providers'tan bir sağlayıcı bağlayın; `tos: avoid` işaretliler `auto`'ya girmez |
| "Kota doldu" (429) | Biraz bekleyin ya da ikinci bir sağlayıcı ekleyin (OmniRoute otomatik geçer) |
| `npm` "allow-scripts" uyarısı | Uyarıdaki paket adlarını `npm config set allow-scripts=…` listesine ekleyip yeniden kurun |
| Güncelleme | `npm install -g omniroute@latest` |
