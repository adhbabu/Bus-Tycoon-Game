// profil-logo.html dosyasından TikTok profil fotoğrafı (1080x1080 PNG) üretir.
// Gerekli: Playwright (npm i -D playwright && npx playwright install chromium). Kullanım: node brand/uret.mjs
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';

const dir = path.dirname(fileURLToPath(import.meta.url));
const require = createRequire(import.meta.url);
const { chromium } = require('playwright');

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1080, height: 1080 }, deviceScaleFactor: 1 });
await page.goto('file://' + path.join(dir, 'profil-logo.html'));
await page.evaluate(() => document.fonts.ready);
await page.waitForTimeout(500);
await page.screenshot({ path: path.join(dir, 'profil-logo.png') });
await browser.close();
console.log('Yazıldı: brand/profil-logo.png');
