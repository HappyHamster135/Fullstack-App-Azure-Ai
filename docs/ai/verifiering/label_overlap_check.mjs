// Mäter om värdeetiketterna i prognosdiagrammet överlappar på olika skärmbredder, med vanliga och tunga belopp.
// Kräver att API:t (harness) och Vite körs och att demokontot finns (seed_demo.py). Se README i den här mappen.
import { chromium } from "playwright-core";

const WEB = process.env.WEB ?? "http://localhost:5173";
const SHOTS = process.env.SHOTS ?? ".";
const CHROMIUM = process.env.CHROMIUM_PATH ?? "/opt/pw-browsers/chromium-1194/chrome-linux/chrome";
const browser = await chromium.launch({ executablePath: CHROMIUM, args: ["--no-sandbox"] });

async function measure(width, totals, label, screenshot) {
  const context = await browser.newContext({ viewport: { width, height: 900 }, locale: "sv-SE" });
  const page = await context.newPage();
  await page.goto(`${WEB}/login`);
  await page.getByLabel("E-post").fill("demo@example.com");
  await page.getByLabel("Lösenord").fill("Lösenord123");
  await page.getByRole("button", { name: "Logga in" }).click();
  await page.waitForURL("**/dashboard");

  if (totals) {
    await page.route("**/api/forecast**", async (route) => {
      const response = await route.fetch();
      const body = await response.json();
      body.months.forEach((month, index) => { month.total = totals[index % totals.length]; });
      await route.fulfill({ response, json: body });
    });
    await page.reload();
  }

  const card = page.locator("div.card", { has: page.getByRole("heading", { name: "Prognos" }) });
  await card.locator(".recharts-bar-rectangle").first().waitFor();
  await page.waitForTimeout(300);

  const boxes = await card.locator(".recharts-label-list text").evaluateAll((nodes) =>
    nodes.filter((node) => node.getBoundingClientRect().width > 0).map((node) => {
      const rect = node.getBoundingClientRect();
      return { left: rect.left, right: rect.right, text: node.textContent };
    }));
  boxes.sort((a, b) => a.left - b.left);

  let overlaps = 0;
  let smallestGap = Infinity;
  for (let i = 1; i < boxes.length; i++) {
    const gap = boxes[i].left - boxes[i - 1].right;
    smallestGap = Math.min(smallestGap, gap);
    if (gap < 0) overlaps++;
  }

  const sample = boxes.slice(0, 3).map((box) => box.text).join(" | ");
  console.log(`  ${label.padEnd(14)} ${String(width).padStart(4)} px: ${boxes.length} etiketter (${sample} …), ${overlaps} överlapp, minsta lucka ${boxes.length > 1 ? smallestGap.toFixed(1) + " px" : "-"}`);
  if (screenshot) await card.screenshot({ path: `${SHOTS}/${screenshot}` });
  await context.close();
  return overlaps;
}

let totalOverlaps = 0;
console.log("Etiketter med demodata (fyrsiffriga belopp)");
for (const width of [320, 360, 390, 576]) totalOverlaps += await measure(width, null, "demodata", width === 360 ? "labels-360.png" : null);

console.log("Etiketter med tunga belopp (femsiffriga)");
for (const width of [320, 360, 390, 576]) totalOverlaps += await measure(width, [12999, 15432, 9999, 23093, 21000, 19999], "tunga belopp", width === 360 ? "labels-heavy-360.png" : null);

await browser.close();
console.log(`\nSammanlagt ${totalOverlaps} överlapp`);
process.exit(totalOverlaps ? 1 : 0);
