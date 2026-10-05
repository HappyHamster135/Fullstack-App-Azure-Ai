// Helkörning av prognosen i riktig webbläsare mot riktigt API (SQLite) och Vite. Kontrollerar siffror, inte bara utseende.
// Kräver att API:t (harness) och Vite körs. Se README i den här mappen.
import assert from "node:assert/strict";
import { chromium } from "playwright-core";

const WEB = process.env.WEB ?? "http://localhost:5173";
const API = process.env.API ?? "http://localhost:5077";
const SHOTS = process.env.SHOTS ?? ".";
const CHROMIUM = process.env.CHROMIUM_PATH ?? "/opt/pw-browsers/chromium-1194/chrome-linux/chrome";

//-----------------------------------------------------------
//-----Oberoende beräkning (egen JS-version, delar ingen kod)
//-----------------------------------------------------------

const iso = (d) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
const startOfDay = (d) => new Date(d.getFullYear(), d.getMonth(), d.getDate());
const plusDays = (d, n) => new Date(d.getFullYear(), d.getMonth(), d.getDate() + n);

function plusMonthsClamped(d, n) {
  const target = new Date(d.getFullYear(), d.getMonth() + n, 1);
  const lastDay = new Date(target.getFullYear(), target.getMonth() + 1, 0).getDate();
  return new Date(target.getFullYear(), target.getMonth(), Math.min(d.getDate(), lastDay));
}

function occurrence(anchor, interval, k) {
  if (interval === "Weekly") return plusDays(anchor, 7 * k);
  if (interval === "Monthly") return plusMonthsClamped(anchor, k);
  if (interval === "Quarterly") return plusMonthsClamped(anchor, 3 * k);
  return plusMonthsClamped(anchor, 12 * k);
}

function expectedForecast(subs, months, today) {
  const first = new Date(today.getFullYear(), today.getMonth(), 1);
  const last = new Date(first.getFullYear(), first.getMonth() + months, 0);
  const perMonth = Array.from({ length: months }, () => 0);

  for (const s of subs) {
    for (let k = 0; ; k++) {
      const d = occurrence(s.next, s.interval, k);
      if (d > last) break;
      if (d >= today) {
        perMonth[(d.getFullYear() - first.getFullYear()) * 12 + d.getMonth() - first.getMonth()] += s.price;
      }
    }
  }

  return { perMonth, total: perMonth.reduce((a, b) => a + b, 0), last };
}

const parseKr = (text) => parseFloat(text.replace(/[^\d,]/g, "").replace(",", "."));
const monthYear = (d) => new Intl.DateTimeFormat("sv-SE", { month: "long", year: "numeric" }).format(d);

//---------------------
//-----Testkörning
//---------------------

const results = [];
async function check(name, fn) {
  try {
    await fn();
    results.push({ name, ok: true });
    console.log(`  ✔ ${name}`);
  } catch (error) {
    results.push({ name, ok: false });
    console.log(`  ✘ ${name}\n      ${String(error.message).split("\n").slice(0, 4).join("\n      ")}`);
  }
}

// Servern räknar "idag" i svensk tid. Skriptet gör likadant så att körningen är rätt även mellan 22:00 och 24:00 UTC.
const swedishDate = new Intl.DateTimeFormat("sv-SE", { timeZone: "Europe/Stockholm", year: "numeric", month: "2-digit", day: "2-digit" }).format(new Date());
const [year, month, day] = swedishDate.split("-").map(Number);
const today = new Date(year, month - 1, day);
const email = `e2e-${Date.now()}@example.com`;
const password = "Lösenord123";

const subs = [
  { name: "Netflix", category: "Streaming", price: 149, interval: "Monthly", label: "Varje månad", next: plusDays(today, 5) },
  { name: "Matkasse", category: "Övrigt", price: 249, interval: "Weekly", label: "Varje vecka", next: plusDays(today, 1) },
  { name: "Microsoft 365", category: "Mjukvara", price: 995, interval: "Yearly", label: "Varje år", next: plusDays(today, 40) },
  { name: "Tidskriften", category: "Nyheter & media", price: 449, interval: "Quarterly", label: "Varje kvartal", next: plusDays(today, -20) },
];

const browser = await chromium.launch({ executablePath: CHROMIUM, args: ["--no-sandbox"] });
const context = await browser.newContext({ viewport: { width: 1280, height: 900 }, locale: "sv-SE" });
const page = await context.newPage();

const consoleErrors = [];
let expected = null; // text som ett pågående scenario väntar sig i konsolen (t.ex. ett medvetet serverfel)
page.on("console", (m) => {
  if (["error", "warning"].includes(m.type()) && !(expected && expected.test(m.text()))) consoleErrors.push(m.text());
});
page.on("pageerror", (e) => {
  if (!(expected && expected.test(e.message))) consoleErrors.push(`pageerror: ${e.message}`);
});

const forecastRequests = [];
page.on("request", (r) => { if (r.url().includes("/api/forecast")) forecastRequests.push(r.url()); });

const forecastCard = () => page.locator("div.card", { has: page.getByRole("heading", { name: "Prognos" }) });
const forecastTotal = async () => parseKr(await forecastCard().locator("span.fs-2").innerText());
const forecastLabel = async () => (await forecastCard().locator("span.text-body-secondary").first().innerText()).trim();
const barCount = () => forecastCard().locator(".recharts-bar-rectangle").count();

async function selectMonths(n) {
  const response = page.waitForResponse((r) => r.url().includes(`/api/forecast?months=${n}`) && r.status() === 200);
  await forecastCard().getByText(`${n} mån`, { exact: true }).click();
  await response;
  await page.waitForFunction((count) => document.querySelectorAll("div.card .recharts-bar-rectangle").length >= count, n);
}

console.log(`Datum för körningen (svensk tid): ${iso(today)}, användare: ${email}\n`);

console.log("1. Registrering och tomt läge");
await page.goto(`${WEB}/register`);
await page.getByLabel("E-post").fill(email);
await page.getByLabel("Lösenord", { exact: true }).fill(password);
await page.getByLabel("Bekräfta lösenord").fill(password);
await page.getByRole("main").getByRole("button", { name: "Skapa konto" }).click();
await page.waitForURL("**/dashboard");
await check("Ny användare ser tomt läge utan prognoskort", async () => {
  await page.getByText("Inga aktiva prenumerationer").waitFor();
  assert.equal(await page.getByRole("heading", { name: "Prognos" }).count(), 0);
});
await page.screenshot({ path: `${SHOTS}/e2e-01-tom-dashboard.png` });

console.log("\n2. Lägger till fyra prenumerationer via formuläret (alla intervall, en förfallen)");
await page.getByRole("link", { name: "Prenumerationer" }).click();
await page.getByRole("button", { name: "Lägg till din första prenumeration" }).click();
await check("Formuläret nekar ett datum utanför 2000–2100 med ett tydligt meddelande", async () => {
  const modal = page.getByRole("dialog");
  await modal.getByLabel("Namn").fill("Fel datum");
  await modal.getByLabel("Kategori").selectOption({ label: "Streaming" });
  await modal.getByLabel("Pris (kr)").fill("10");
  await modal.getByLabel("Startdatum").fill("2020-01-01");
  await modal.getByLabel("Nästa betalning").fill("2101-01-01");
  await modal.getByRole("button", { name: "Spara" }).click();
  await modal.getByText("Nästa betalning måste vara mellan 2000-01-01 och 2100-12-31.").waitFor();
  await page.screenshot({ path: `${SHOTS}/e2e-02-datumgrans.png` });
  await modal.getByRole("button", { name: "Avbryt" }).click();
  await modal.waitFor({ state: "detached" });
});
for (const [index, sub] of subs.entries()) {
  await page.getByRole("button", { name: index === 0 ? "Lägg till din första prenumeration" : "Ny prenumeration" }).click();
  const modal = page.getByRole("dialog");
  await modal.getByLabel("Namn").fill(sub.name);
  await modal.getByLabel("Kategori").selectOption({ label: sub.category });
  await modal.getByLabel("Pris (kr)").fill(String(sub.price));
  await modal.getByLabel("Betalningsintervall").selectOption({ label: sub.label });
  await modal.getByLabel("Startdatum").fill(iso(plusDays(sub.next, -200)));
  await modal.getByLabel("Nästa betalning").fill(iso(sub.next));
  await modal.getByRole("button", { name: "Spara" }).click();
  await modal.waitFor({ state: "detached" });
}
await check("Alla fyra prenumerationer syns i listan", async () => {
  for (const sub of subs) await page.locator(".card", { hasText: sub.name }).first().waitFor();
});
await page.screenshot({ path: `${SHOTS}/e2e-03-prenumerationer.png`, fullPage: true });

console.log("\n3. Prognosen på dashboarden (förväntade värden räknas oberoende i skriptet)");
await page.getByRole("link", { name: "Dashboard" }).click();
await page.getByRole("heading", { name: "Prognos" }).waitFor();
await page.locator("div.card .recharts-bar-rectangle").first().waitFor();

await check("Standard är 6 månader: summa och periodens slut stämmer", async () => {
  const exp = expectedForecast(subs, 6, today);
  assert.equal(await barCount(), 6);
  assert.equal(await forecastTotal(), exp.total);
  assert.equal(await forecastLabel(), `till och med ${monthYear(exp.last)}`);
});
await check("Webbappen ber inte om betalningslistan (stor, oanvänd) och får den inte", async () => {
  assert.ok(forecastRequests.length > 0);
  assert.ok(forecastRequests.every((url) => !url.includes("includePayments")));
  const token = await page.evaluate(() => localStorage.getItem("subtracker.token"));
  const lean = await (await fetch(`${API}/api/forecast?months=3`, { headers: { Authorization: `Bearer ${token}` } })).json();
  assert.ok(lean.months.every((m) => m.payments === null));
  assert.ok(lean.months.some((m) => m.costByCategory.length > 0));
});
await page.screenshot({ path: `${SHOTS}/e2e-04-dashboard.png`, fullPage: true });
await forecastCard().screenshot({ path: `${SHOTS}/e2e-04b-prognoskort-6.png` });

for (const months of [3, 12]) {
  await check(`Val av ${months} mån hämtar months=${months} och visar rätt summa, antal staplar och period`, async () => {
    await selectMonths(months);
    const exp = expectedForecast(subs, months, today);
    assert.equal(await barCount(), months);
    assert.equal(await forecastTotal(), exp.total);
    assert.equal(await forecastLabel(), `till och med ${monthYear(exp.last)}`);
  });
}
await forecastCard().screenshot({ path: `${SHOTS}/e2e-05-prognoskort-12.png` });

await check("Tooltip visar månad, summa och kategorier för en stapel", async () => {
  await selectMonths(6);
  const exp = expectedForecast(subs, 6, today);
  await forecastCard().locator(".recharts-bar-rectangle").nth(1).hover();
  const tooltip = forecastCard().locator(".recharts-tooltip-wrapper").last();
  await tooltip.getByText(monthYear(new Date(today.getFullYear(), today.getMonth() + 1, 1))).waitFor();
  const text = await tooltip.innerText();
  assert.ok(text.includes(subs[0].category) || text.includes(subs[1].category), `kategori saknas i tooltip: ${text}`);
  assert.equal(parseKr(text.split("\n")[0]), exp.perMonth[1]);
  await forecastCard().screenshot({ path: `${SHOTS}/e2e-06-tooltip.png` });
});

console.log("\n4. Samspel med resten av appen: 'Markera betald' ändrar prognosen");
await page.getByRole("link", { name: "Prenumerationer" }).click();
await page.locator(".card", { hasText: "Netflix" }).first().getByRole("button", { name: "Markera betald" }).click();
await page.getByText("Betalning registrerad för Netflix").waitFor();
subs[0] = { ...subs[0], next: plusMonthsClamped(subs[0].next, 1) };
await page.getByRole("link", { name: "Dashboard" }).click();
await page.getByRole("heading", { name: "Prognos" }).waitFor();
await page.locator("div.card .recharts-bar-rectangle").first().waitFor();
await check("Efter 'Markera betald' räknas Netflix från nästa månad och summan minskar", async () => {
  assert.equal(await forecastTotal(), expectedForecast(subs, 6, today).total);
});
await page.screenshot({ path: `${SHOTS}/e2e-07-efter-betalning.png`, fullPage: true });

console.log("\n5. Felhantering: serverfel och återhämtning med 'Försök igen'");
await page.route("**/api/forecast**", (route) =>
  route.fulfill({ status: 500, contentType: "application/json", body: JSON.stringify({ title: "Fel" }) }),
);
expected = /500|Failed to load resource/;
await forecastCard().getByText("3 mån", { exact: true }).click();
await check("Felet visas i kortet med en 'Försök igen'-knapp, och väljaren finns kvar", async () => {
  await forecastCard().getByText("Något gick fel. Försök igen.").waitFor();
  assert.equal(await forecastCard().getByRole("button", { name: "Försök igen" }).count(), 1);
  assert.equal(await forecastCard().getByText("6 mån", { exact: true }).count(), 1);
  await forecastCard().screenshot({ path: `${SHOTS}/e2e-08-fel.png` });
});
await page.unroute("**/api/forecast**");
await check("'Försök igen' hämtar samma period på nytt och återhämtar kortet", async () => {
  const response = page.waitForResponse((r) => r.url().includes("/api/forecast?months=3") && r.status() === 200);
  await forecastCard().getByRole("button", { name: "Försök igen" }).click();
  await response;
  await forecastCard().locator(".recharts-bar-rectangle").first().waitFor();
  assert.equal(await forecastCard().getByText("Något gick fel. Försök igen.").count(), 0);
  assert.equal(await forecastTotal(), expectedForecast(subs, 3, today).total);
});
expected = null;

console.log("\n6. Felgräns: ett renderingsfel i prognoskortet får inte tömma appen");
await page.route("**/api/forecast**", (route) =>
  route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ total: 0, months: null }) }),
);
expected = /Cannot read properties|error occurred|boundary/i;
await page.reload();
await check("Kortet ersätts av ett meddelande medan navigering och resten av dashboarden finns kvar", async () => {
  await page.getByText("Prognosen kunde inte visas. Resten av dashboarden fungerar som vanligt.").waitFor();
  assert.ok(await page.getByRole("link", { name: "Prenumerationer" }).count() > 0);
  assert.ok(await page.getByRole("heading", { name: "Dashboard" }).count() > 0);
  assert.ok(await page.getByRole("heading", { name: "Kostnad per kategori" }).count() > 0);
  await page.screenshot({ path: `${SHOTS}/e2e-09-felgrans.png`, fullPage: true });
});
await page.unroute("**/api/forecast**");
await check("'Försök igen' i felgränsen visar prognosen igen när felet är borta", async () => {
  await page.getByRole("button", { name: "Försök igen" }).click();
  await forecastCard().locator(".recharts-bar-rectangle").first().waitFor();
  assert.equal(await forecastTotal(), expectedForecast(subs, 6, today).total);
});
expected = null;

console.log("\n7. Mobil: kortare etiketter utan överlapp");
await page.setViewportSize({ width: 360, height: 800 });
await check("På 360 px visas kortare belopp (tkr) och etiketterna överlappar inte", async () => {
  await page.waitForTimeout(400);
  const boxes = await forecastCard().locator(".recharts-label-list text").evaluateAll((nodes) =>
    nodes.filter((n) => n.getBoundingClientRect().width > 0).map((n) => {
      const r = n.getBoundingClientRect();
      return { left: r.left, right: r.right, text: n.textContent };
    }));
  boxes.sort((a, b) => a.left - b.left);
  assert.ok(boxes.length >= 2, "inga synliga etiketter");
  assert.ok(boxes.every((b) => /tkr|kr/.test(b.text)));
  assert.ok(boxes.some((b) => b.text.includes("tkr")), "ingen kortare etikett (tkr) visas");
  for (let i = 1; i < boxes.length; i++) assert.ok(boxes[i].left >= boxes[i - 1].right, `etiketterna ${i - 1} och ${i} överlappar`);
  await forecastCard().screenshot({ path: `${SHOTS}/e2e-10-mobil-6.png` });
});
await selectMonths(12);
await page.waitForTimeout(300);
await forecastCard().screenshot({ path: `${SHOTS}/e2e-11-mobil-12.png` });
await check("Ingen horisontell scroll på mobil", async () => {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  assert.ok(overflow <= 1, `horisontell overflow: ${overflow}px`);
});
await page.setViewportSize({ width: 1280, height: 900 });

console.log("\n8. Utloggning");
const token = await page.evaluate(() => localStorage.getItem("subtracker.token"));
await page.getByRole("button", { name: "Logga ut" }).click();
await page.waitForURL("**/login");
await check("Efter utloggning är den gamla token ogiltig mot /api/forecast (401)", async () => {
  const response = await fetch(`${API}/api/forecast`, { headers: { Authorization: `Bearer ${token}` } });
  assert.equal(response.status, 401);
});
await check("Dashboarden kräver inloggning (omdirigeras till /login)", async () => {
  await page.goto(`${WEB}/dashboard`);
  await page.waitForURL("**/login");
});

await check("Inga oväntade fel eller varningar i webbläsarens konsol", async () => {
  assert.deepEqual(consoleErrors, []);
});

await browser.close();

const failed = results.filter((r) => !r.ok);
console.log(`\n${results.length - failed.length}/${results.length} kontroller godkända`);
process.exit(failed.length ? 1 : 0);
