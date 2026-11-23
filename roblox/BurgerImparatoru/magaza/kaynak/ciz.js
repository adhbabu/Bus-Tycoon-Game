const { chromium } = require('playwright');
(async () => {
  const browser = await chromium.launch();
  const isler = [["ikon", 512, 512], ["tanitim1", 1920, 1080], ["tanitim2", 1920, 1080], ["tanitim3", 1920, 1080]];
  for (const [ad, w, h] of isler) {
    const page = await browser.newPage({ viewport: { width: w, height: h }, deviceScaleFactor: 1 });
    await page.goto("file://" + process.cwd() + "/sahne.html#" + ad);
    await page.evaluate(() => document.fonts.ready);
    await page.waitForTimeout(300);
    await page.screenshot({ path: ad + ".png" });
    await page.close();
  }
  await browser.close();
  console.log("bitti");
})();
