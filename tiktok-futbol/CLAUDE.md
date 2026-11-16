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

## Görsel standardı (kullanıcının isteği)

Videolar hazırlanırken görseller de ElevenLabs'ten (Image & Video) üretilir.
- API: `POST https://api.elevenlabs.io/v1/flows/image` (asenkron; `GET /v1/flows/image/{id}` ile durum + çıktı adresi). Video için eşdeğer flows video uçları.
- **Kısıt:** Bu uçlar API'de Pro plan ve üstünü ister. Hesap şu an Starter → API `paid_plan_required` döner (2026-11-16'da denendi).
  Starter'dayken: kullanıcı görselleri ElevenLabs uygulamasında üretip gönderir, ben videoya yerleştiririm; ya da kod ile çizilen görsellerle devam edilir. Hangisi olacağını kullanıcıya sor.
- Görsellerde gerçek oyuncu yüzleri, kulüp armaları/logoları ve telifli maç görüntüleri üretilmez; yalnızca takım renkleri ve özgün sahneler.
