# TikTok Canlı Futbol – çalışma notları

## Seslendirme standardı (kullanıcının isteği)

Kullanıcı bir videoya, oyuna ya da başka bir içeriğe **ses / seslendirme eklenmesini istediğinde** her zaman şu yöntem kullanılır:

- **Servis:** ElevenLabs API (`https://api.elevenlabs.io/v1/text-to-speech/{voice_id}`), kullanıcının kendi hesabı (Starter plan; ticari kullanım hakkı var).
- **Ses:** `Hasan Yasin - Warm Turkish Narrator` · voice_id `tILIOdKERNNbBHVEXk7I`
- **Model:** `eleven_v4` · `voice_settings: { stability: 0.5, similarity_boost: 0.75 }` · çıktı `mp3_44100_128`
- **Metin:** ElevenLabs ses etiketleriyle yazılır: `[excited]`, `[gasps]`, `[cheering]`, `[short pause]`, `[sighs]`, `[warmly]`, `[playfully]`, `[cheerfully]`, `[curious]`. Goller uzun ve bağırarak: `[excited] Gooooool! [yelling] Gol! Gol! Gol! [cheering] ...`
- **Anahtar:** API anahtarı sohbete yazılmaz/istenmez. Bulut oturumunda ortamın ağ sırrı olarak durur (host `api.elevenlabs.io`, header `xi-api-key`); istekte header değeri `proxy-injected` gönderilir, proxy gerçeğini ekler.
- **Araç:** `node seslendirme/uret.mjs satirlar.json cikis_klasoru` (satırlar: `[{ "code": "P1", "text": "[excited] ..." }]`).
- Satırlar kodlanır (G = oyun spikeri, P/Q/K/B = videolar) ve `seslendirme/METINLER.md`'ye eklenir.
- Oyunun spiker sesleri `public/sesler/<KOD>.mp3` içindedir; yayın ekranı `/sesler/list` ile hangilerinin olduğunu öğrenir. Ses paketi varken kaydı olmayan cümleler yalnızca altyazıda görünür (tek tip ses).
- Azure/Gemini/cihaz sesi yalnızca ElevenLabs'e erişim yoksa yedek olarak kullanılır; bunu kullanıcıya söyle.
- Ses paketi değişince demo da güncellenir: `node build-demo.mjs <scratchpad>/canli-futbol-demo.html`, sonra "Canlı Futbol Demo" (https://claude.ai/artifact/UGJGZnL66p55qE9gV7rV55) `files` ile `sesler/Gxx.mp3` dosyalarıyla birlikte yeniden yayınlanır. Demo, iPhone'daki Claude uygulamasında cihaz sesi çalışmadığı için yalnızca bu kayıtlarla konuşur.
- Render'daki yayın ekranı yeni sürümü (sunucunun `build` kimliği) görünce maç oynanmıyorken kendini yeniler.

## Görsel standardı (kullanıcının isteği)

Videolar hazırlanırken görseller de ElevenLabs'ten (Image & Video) üretilir.
- API: `POST https://api.elevenlabs.io/v1/flows/image` (asenkron; `GET /v1/flows/image/{id}` ile durum + çıktı adresi). Video için eşdeğer flows video uçları.
- **Kısıt:** Bu uçlar API'de Pro plan ve üstünü ister. Hesap şu an Starter → API `paid_plan_required` döner (2026-11-16'da denendi).
  Kullanıcının seçimi (2026-11-16): **yeni videolarda** görselleri kullanıcı ElevenLabs uygulamasında üretir.
- **Akış (her yeni video için):**
  1. Video fikri + sahne akışı + her sahne için İngilizce görsel promptu (Türkçe ad ve "nerede kullanılacak" açıklamasıyla), oran (9:16 tam ekran, 1:1 yarım ekran) ve kod (ör. MG1).
  2. Her video için TikTok açıklaması (caption + hashtag) da yazılır.
  3. Hepsi kopyala butonlu "Görsel Promptları" sayfasına eklenir: https://claude.ai/artifact/14EEpU6kmSxKR6V8K1UW27 (kaynak: scratchpad'deki gorsel-promptlari.html; oturum değiştiyse sayfayı Artifact read ile al).
  4. Kullanıcı görselleri kodlarıyla gönderir → anlatım Hasan Yasin sesiyle üretilir → görseller hafif zoom/kaydırma ile, yazılar ve efektler üstüne eklenerek 1080x1920 30 fps montajlanır.
  Zaten yayınlanmış videolar yeniden yapılmaz.
- Görsellerde gerçek oyuncu yüzleri, kulüp armaları/logoları ve telifli maç görüntüleri üretilmez; yalnızca takım renkleri ve özgün sahneler.
