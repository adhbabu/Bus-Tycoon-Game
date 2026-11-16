// ElevenLabs ile seslendirme (proje standardı: Hasan Yasin, eleven_v4).
// Kullanım: node seslendirme/uret.mjs satirlar.json cikis_klasoru
//   satirlar.json: [{ "code": "P1", "text": "[excited] Penaltı challenge! ..." }, ...]
// Anahtar: ELEVENLABS_API_KEY ortam değişkeni; bulut oturumunda proxy eklediği için gerekmez.
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';

const VOICE_ID = 'tILIOdKERNNbBHVEXk7I'; // Hasan Yasin - Warm Turkish Narrator
const MODEL = 'eleven_v4';
const SETTINGS = { stability: 0.5, similarity_boost: 0.75 };

const [, , listFile, outDir = 'ses'] = process.argv;
if (!listFile) {
  console.error('Kullanım: node seslendirme/uret.mjs satirlar.json cikis_klasoru');
  process.exit(1);
}
const lines = JSON.parse(fs.readFileSync(listFile, 'utf8'));
fs.mkdirSync(outDir, { recursive: true });
const key = process.env.ELEVENLABS_API_KEY || 'proxy-injected';
const tmp = path.join(outDir, '.istek.json');
let ok = 0;
for (const { code, text } of lines) {
  const out = path.join(outDir, `${code}.mp3`);
  if (fs.existsSync(out) && fs.statSync(out).size > 5000) { ok++; continue; }
  fs.writeFileSync(tmp, JSON.stringify({ text, model_id: MODEL, voice_settings: SETTINGS }));
  // curl kullanılır: bulut oturumunun HTTPS proxy'si ve sertifikası otomatik uygulanır
  const status = execFileSync('curl', ['-sS', '-m', '120', '-o', out, '-w', '%{http_code}', '-H', `xi-api-key: ${key}`,
    '-H', 'Content-Type: application/json', '--data-binary', `@${tmp}`,
    `https://api.elevenlabs.io/v1/text-to-speech/${VOICE_ID}?output_format=mp3_44100_128`]).toString();
  if (status === '200') ok++;
  else console.error(`${code}: HTTP ${status} ${fs.readFileSync(out, 'utf8').slice(0, 200)}`);
}
fs.rmSync(tmp, { force: true });
console.log(`${ok} / ${lines.length} satır hazır → ${outDir}`);
