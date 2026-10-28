// Mağaza ekran görüntüleri — web sürümünü mağazaların istediği her boyutta açar, vitrin turunu
// (Assets/_Project/Scripts/Game/ShowcaseDirector.cs) çalıştırır ve her ekranın görüntüsünü kaydeder.
//
//   npm install playwright && npx playwright install chromium
//   node Tools/store-screenshots.mjs --url https://adhbabu.github.io/Bus-Tycoon-Game/oyna/ --out store/screenshots
//
// Seçenekler: --only app-store/iphone-6.9 (yalnız bir cihaz) · --timeout 900 (saniye, ilk yükleme dahil)
import { chromium } from "playwright";
import { mkdir } from "node:fs/promises";
import path from "node:path";

const DEVICES = [
  // Google Play: 16:9; telefon ve tablet görüntüleri ayrı bölümlere yüklenir.
  { store: "google-play", device: "telefon", profile: "telefon", width: 1920, height: 1080 },
  { store: "google-play", device: "tablet", profile: "tablet", width: 2560, height: 1440 },
  // App Store: iPhone 6,9" ve iPad 13" (yatay)
  { store: "app-store", device: "iphone-6.9", profile: "telefon", width: 2868, height: 1320 },
  { store: "app-store", device: "ipad-13", profile: "tablet", width: 2752, height: 2064 },
];

function option(name, fallback) {
  const index = process.argv.indexOf(`--${name}`);
  return index > 0 && index + 1 < process.argv.length ? process.argv[index + 1] : fallback;
}

const baseUrl = option("url", "https://adhbabu.github.io/Bus-Tycoon-Game/oyna/");
const outDir = option("out", "store/screenshots");
const only = option("only", null);
const timeoutMs = Number(option("timeout", "900")) * 1000;

/** Konsol satırlarını sırayla bekletmek için küçük bir kuyruk. */
function consoleQueue(page) {
  const lines = [];
  let waiter = null;
  page.on("console", (message) => {
    const text = message.text();
    if (message.type() === "error") console.log(`    [konsol hatası] ${text}`);
    if (!text.includes("[Showcase]")) return;
    lines.push(text);
    if (waiter) { const resolve = waiter; waiter = null; resolve(); }
  });
  page.on("pageerror", (error) => console.log(`    [sayfa hatası] ${error.message}`));
  return async function next(ms) {
    const deadline = Date.now() + ms;
    while (lines.length === 0) {
      const left = deadline - Date.now();
      if (left <= 0) throw new Error(`vitrin ${Math.round(ms / 1000)} sn içinde yanıt vermedi`);
      await Promise.race([new Promise((resolve) => { waiter = resolve; }), new Promise((resolve) => setTimeout(resolve, left))]);
    }
    return lines.shift();
  };
}

const browser = await chromium.launch({
  // GPU'suz sunucuda WebGL için yazılımsal çizim (SwiftShader).
  args: ["--use-angle=swiftshader", "--enable-unsafe-swiftshader", "--ignore-gpu-blocklist"],
});

let failures = 0;
for (const target of DEVICES) {
  const id = `${target.store}/${target.device}`;
  if (only && only !== id) continue;
  const folder = path.join(outDir, target.store, target.device);
  await mkdir(folder, { recursive: true });
  console.log(`▶ ${id} (${target.width}×${target.height}, profil ${target.profile})`);

  const context = await browser.newContext({ viewport: { width: target.width, height: target.height }, deviceScaleFactor: 1 });
  const page = await context.newPage();
  const next = consoleQueue(page);
  const url = new URL(baseUrl);
  url.searchParams.set("vitrin", "betik");
  url.searchParams.set("profil", target.profile);
  url.searchParams.set("dpr", "1");

  try {
    await page.goto(url.toString(), { waitUntil: "load", timeout: timeoutMs });
    let wait = timeoutMs;
    for (;;) {
      const line = await next(wait);
      wait = 180000;
      const ready = line.match(/READY (\d+)\/(\d+) (\S+)/);
      if (!ready) {
        if (line.includes("DONE")) break;
        continue;
      }
      const file = path.join(folder, `${ready[1].padStart(2, "0")}-${ready[3]}.png`);
      await page.screenshot({ path: file });
      console.log(`  ✓ ${file}`);
      await page.evaluate(() => window.unityInstance.SendMessage("ShowcaseDirector", "Continue"));
    }
  } catch (error) {
    failures++;
    console.log(`  ✗ ${id}: ${error.message}`);
    await page.screenshot({ path: path.join(folder, "hata.png") }).catch(() => {});
  }
  await context.close();
}

await browser.close();
process.exit(failures > 0 ? 1 : 0);
