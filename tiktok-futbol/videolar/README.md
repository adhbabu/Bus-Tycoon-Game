# TikTok videoları

**Ana stil:** "Gol mü, değil mi?" videosunun yazı tipleri ve görsel görünüşü. Konsept serbesttir; her yeni video, formatı ne olursa olsun `stil.cjs` ile kurulur (aynı yazı tipleri, renkler, görsel düzeni).

| Dosya | Ne işe yarar |
|---|---|
| `stil.cjs` | Ana stil: tam ekran görsel + zoom, sarı rozet ve noktalar, büyük başlık, iki seçenek + 3-2-1 halkası, eğik damga, kural kartı, kapanış listesi, düdük/gol sesleri |
| `v2.cjs` | Zamanlama (anlatım süresine göre) ve 30 fps kare kare kayıt, ses miksajı |
| `onizleme.cjs` | Birkaç anın karesini çekip tek kontrol görseli yapar |
| `golmu.cjs` | Örnek video: Gol mü, değil mi? |
| `dogru.cjs` | Doğru mu, yanlış mı? (görseller `gorseller/dogru/DG1–DG5`) |

Görseller `gorseller/<video>/<KOD>.jpg` (1080x1920), anlatım `../seslendirme/video-sesleri/<KOD>.mp3` (Hasan Yasin, ElevenLabs).

```bash
node videolar/golmu.cjs onizleme   # kontrol görseli: videolar/cikti/golmu-onizleme.jpg
node videolar/golmu.cjs            # video: videolar/cikti/gol-mu-degil-mi.mp4
```

Gelen görseli 1080x1920'ye çevirmek:
`ffmpeg -i gelen.webp -vf "scale=1080:1920:force_original_aspect_ratio=increase:flags=lanczos,crop=1080:1920,unsharp=5:5:0.5" -q:v 2 gorseller/<video>/<KOD>.jpg`

Son ses ayarı (TikTok seviyesi): `ffmpeg -i video.mp4 -c:v copy -af loudnorm=I=-14:TP=-1.5:LRA=11 -ar 44100 -c:a aac -b:a 160k -movflags +faststart son.mp4`
