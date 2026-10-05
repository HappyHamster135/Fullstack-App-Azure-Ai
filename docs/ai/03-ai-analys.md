# AI-tillfälle 2 – oberoende analys och felsökning av den granskade koden

| | |
|---|---|
| **Datum** | 2026-10-04 |
| **AI-verktyg** | Claude Code (Anthropic) |
| **Syfte** | Få en oberoende andra bedömning av prognosfunktionen *efter* att jag rättat AI-förslaget (commit `69a54cb`, kod oförändrad till `5041bb6`) |
| **Skillnad mot tillfälle 1** | Annan uppgift (granska och bevisa, ändra ingenting) och en ny agent med tomt sammanhang |

## Hur AI:n användes

En separat Claude-agent fick repot, en läsbehörighet i praktiken (ändra ingen kod, inga commits) och fick köra bygge, tester och egna skript utanför repot för att bevisa fynd.
Jag bad den **inte läsa `docs/ai/`**, så att dess bedömning inte färgas av min egen granskning.

**Körningen avbröts en gång:** containern som sessionen kör i startades om medan den första granskningsagenten arbetade, och dess arbete gick förlorat utan rapport.
Jag körde om uppgiften med exakt samma prompt. Allt nedan kommer från den andra körningen.

## Prompt (ordagrant)

```text
Jag har byggt en prognosfunktion i min webbapplikation SubTracker (ASP.NET Core 10 Web API + React/Vite). Repot ligger i /home/user/fullstack-app-azure-ai (läs README.md först). Funktionen visar vad användaren kommer att betala per månad framåt, utifrån aktiva prenumerationer.

Koden som ska granskas:
- Backend: backend/SubTracker.Api/Controllers/ForecastController.cs, Services/ForecastService.cs och IForecastService.cs, Dtos/Forecast/*, Mappings/ForecastMappings.cs, Entities/Subscription.cs (GetPaymentDates) och Entities/BillingIntervalExtensions.cs, samt registreringen i Program.cs.
- Frontend: frontend/src/api/ForecastApi.js, components/ForecastCard.jsx, components/charts/ForecastChart.jsx och hur de används i pages/DashboardPage.jsx.
- Tester: backend/SubTracker.Api.Tests.

Granska koden som en kritisk senior utvecklare och säkerhetsgranskare. Leta efter:
- buggar och felaktiga edge cases (datum, avrundning, tidszoner, tomma data)
- säkerhetsrisker (åtkomstkontroll, indatahantering, informationsläckage, resursmissbruk)
- arkitekturproblem och brott mot projektets egna mönster
- bristande felhantering
- prestandaproblem
- luckor i testerna, det vill säga beteenden som inget test skulle upptäcka om de gick sönder

Regler:
- Ändra ingen kod i repot och gör inga git-commits. Du får köra `dotnet build`/`dotnet test` och skriva egna tillfälliga skript utanför repot för att bevisa dina fynd.
- Läs inte mappen docs/ai/. Jag vill ha en oberoende bedömning.
- Rapportera bara fynd som du kan motivera. För varje fynd anger du allvarlighetsgrad (kritisk, hög, medel eller låg), fil och rad, vad som är fel, ett konkret scenario eller bevis och ett förslag på rättning.
- Skilj tydligt på fynd du har verifierat genom att köra något och sådant du bara misstänker.
- Skriv också vad du granskade och inte hittade något att anmärka på.

Svara på svenska.
```

## AI-verktygets svar (ordagrant)

> # Oberoende granskning av prognosfunktionen i SubTracker
>
> Repot är orört: inga ändringar, inga commits, `git status` är rent. Jag läste inte `docs/ai/`, git-historiken eller de anteckns- och prompt-filer som låg i scratchpad. Alla körningar gjordes i kopior under `/tmp/claude-0/-home-user/b692db47-b017-5a26-94d3-6c8668b089d8/scratchpad/`.
>
> Begrepp: **Verifierat** betyder att jag körde något som visar det. **Misstanke** betyder att det bygger på kodläsning eller resonemang.
>
> ## 1. Sammanfattning
>
> - Inga kritiska eller höga fynd. Kärnan är korrekt: datumlogiken, summeringen och åtkomstkontrollen höll i allt jag provade (se avsnitt 5).
> - Jag hittade 2 medelfynd och 10 lågfynd. De två viktigaste:
>   1. Prognossvaret och antalet prenumerationer är obegränsade. Ett konto med 5 000 prenumerationer gav 833 MB toppminne i API-processen med bara 8 samtidiga anrop.
>   2. Testerna körs aldrig i CI. Mutationsprovning visar dessutom luckor, bland annat en riktig gränsbugg som inget av de 40 testen fångar.
> - Webbläsarkörningen visade tre UI-problem: diagrametiketter som krockar på telefonbredd, att hela appen blir tom vid ett renderingsfel i prognoskortet, och att man inte kan försöka igen med samma period efter ett fel.
>
> ## 2. Vad jag körde
>
> 1. Baslinje: `dotnet test` gav 40/40 gröna. `oxlint` gav 0 anmärkningar och `vite build` gick bra.
> 2. Egna probetester T1–T16 (xUnit, in-process TestServer + SQLite). Filen är `work/backend/SubTracker.Api.Tests/ReviewProbes.cs`.
> 3. Oberoende kontroll i Python:
>    - `GetPaymentDates` mot 285 892 fall (3 012 606 betalningsdatum): 0 avvikelser.
>    - Hela endpointen över HTTP mot 400 slumpscenarier (2 412 prenumerationer, 13 354 betalningar): 0 avvikelser.
> 4. Mutationsprovning: 29 medvetet felaktiga kodvarianter kördes mot de 40 ordinarie testerna. 19 fångades och 10 överlevde.
> 5. Playwright/Chromium mot ett fristående API (SQLite) och Vite dev/preview. Jag mätte nätverkstidslinjer, felscenarier, diagrametiketter och XSS-försök.
> 6. Fristående Kestrel-process: svarsstorlek, tid och toppminne (`VmHWM`).
> 7. EF Core-frågan översatt till T-SQL med SQL Server-leverantören (utan anslutning).
>
> ## 3. Sammanställning av fynd
>
> | # | Allvarlighet | Status | Område | Kort |
> |---|---|---|---|---|
> | F1 | Medel | Verifierat (Azure-effekt är misstanke) | Resurser | Obegränsat antal prenumerationer, ingen rate limiting och ett svar där 89–100 % aldrig används |
> | F2 | Medel | Verifierat | Tester/CI | CI kör aldrig testerna. 10 av 29 mutanter överlever, bland dem en riktig gränsbugg |
> | F3 | Låg | Verifierat | Tidszon | "Idag" räknas i UTC, så prognosen avviker 1–2 h per dygn |
> | F4 | Låg | Verifierat | Datum | `RegisterPayment` driver ankardagen 31 → 28, vilket prognosen säger sig undvika |
> | F5 | Låg | Verifierat | Frontend | Ingen ErrorBoundary. Ett fel i prognoskortets tooltip tömmer hela appen |
> | F6 | Låg | Verifierat | Frontend | Diagrametiketter krockar på 360–390 px |
> | F7 | Låg | Verifierat | Frontend | Kaskadladdning, inget avbrott av gamla anrop, ingen "försök igen" |
> | F8 | Låg | Verifierat | Indata | Inga gränser på datum. `RegisterPayment` ger 500 för år 9999 |
> | F9 | Låg | Verifierat | Semantik | Dashboard och prognos är oense om förfallna betalningar |
> | F10 | Låg | Verifierat/Misstanke | Sortering | Kulturberoende sortering utan slutlig tie-breaker |
> | F11 | Låg | Verifierat (kodläsning) | Arkitektur | `TimeProvider` används bara av prognosen |
> | F12 | Låg | Verifierat | Tillgänglighet | Diagrammet saknar namn och laddning aviseras inte |
>
> ## 4. Fynd i detalj
>
> ### F1. Obegränsad resursförbrukning (Medel)
> **Status:** Verifierat. Att det slår mot Azure är en misstanke.
>
> **Fil och rad:**
> - `Services/ForecastService.cs:32-36,43`
> - `Dtos/Forecast/ForecastMonthResponse.cs:8`
> - `Dtos/Forecast/ForecastPaymentResponse.cs:10`
> - `Controllers/ForecastController.cs:17-18`
> - `Services/SubscriptionService.cs:48-64`
> - `Program.cs:94-110,145-148`
> - `Controllers/AuthController.cs:16-24`
>
> **Fel:**
> - Svaret innehåller ett objekt per betalningstillfälle, med hela kategoriobjektet upprepat. Det växer alltså med prenumerationer × betalningar per månad × månader.
> - Frontend läser aldrig `months[].payments`. `ForecastCard` och `ForecastChart` använder bara `total`, `year`, `month` och `costByCategory`.
> - Det finns inget tak på antal prenumerationer, ingen rate limiting (`AddRateLimiter` saknas), ingen komprimering och ingen `CancellationToken`.
> - Registreringen är öppen utan e-postverifiering.
>
> **Bevis (körda mätningar):**
> - 1 500 × `POST /api/subscriptions` med 100 tecken långa namn gav 1 500 × 201 på 3,3 s, utan någon spärr. Därefter gav `GET /api/forecast?months=24` 33,6 MiB.
> - Veckoprenumerationer med `months=24` (in-process):
>
>   | Antal | Svar | Servertid |
>   |---|---|---|
>   | 100 | 1,4 MB | 63 ms |
>   | 1 000 | 14,7 MB | 0,3–0,4 s |
>   | 5 000 | 73 MB | 2,4–3,0 s |
>
>   Vid 5 000 allokerades 109 MB på serversidan.
> - Fristående Kestrel-process med 5 000 prenumerationer: 111 MB och 3,7 s per anrop. Toppminnet gick 154 MB → 243 MB (1 anrop) → 571 MB (4 samtidiga) → **833 MB (8 samtidiga, ca 7,5 s vardera)**.
> - Realistisk användare (24 prenumerationer, 12 månader): 52,5 kB, varav UI:t behöver 5,8 kB. 338 betalningsobjekt skickas men visas aldrig.
> - I alla mätningar är `payments` 97–100 % av svaret. Utan dem är svaret 1,6–3,3 kB oavsett antal prenumerationer.
>
> **Scenario:** Någon registrerar ett konto, skapar 5 000 veckoprenumerationer (17 s via API:t) och loopar `GET /api/forecast?months=24`.
>
> **Misstanke (ej verifierad):** README anger App Service Free F1. Enligt Azures planbeskrivning har F1 ca 1 GB minne och dagskvoter för CPU och utgående data. Då kan processen dö eller kvoten ta slut, och API:t blir otillgängligt för alla.
>
> **Rättning:**
> 1. Ta bort `Payments` ur standardsvaret eller gör det opt-in (`?details=true`). UI:t påverkas inte.
> 2. Sätt ett tak på antal prenumerationer per användare i `SubscriptionService.CreateAsync` (t.ex. 200, svara 409/400).
> 3. Lägg till `AddRateLimiter`/`UseRateLimiter` per användar-id på prognosen och på `register`/`POST subscriptions`.
> 4. Skicka `CancellationToken` genom controller, service och `ToListAsync(ct)`. Frontend ska skicka `signal` (se F7).
> 5. Valfritt: `AddResponseCompression`, och normalisera kategorin (id plus separat lista).
>
> ### F2. Testerna körs aldrig i CI, och mutationsprovning visar luckor (Medel)
> **Status:** Verifierat.
>
> **Fil och rad:**
> - `.github/workflows/backend.yml:37-44,51-52` (bara restore/build/publish av API-projektet, och deploy beror inte på något teststeg)
> - `ForecastEndpointTests.cs`
> - `Infrastructure/TestClock.cs:13-15`
> - `SubscriptionPaymentDatesTests.cs:17-37`
>
> **Fel:**
> - `grep` efter "test" i båda workflows ger inget. Ett rött test blockerar alltså aldrig deploy, och testprojektet byggs inte ens i CI.
> - README nämner inga tester och inte hur de körs. Frontend saknar testkörare helt.
>
> **Bevis (mutationsprovning, överlevande mutanter):**
>
> | Mutant | Vad inget test skyddar |
> |---|---|
> | M01 `NextPaymentDate <= lastDay` → `<` (`ForecastService.cs:29`) | En prenumeration som förfaller exakt sista dagen i perioden försvinner. Probe T14 passerar mot originalet men fallerar mot mutanten, så mutanten är inte ekvivalent. |
> | M03 `GetUtcNow()` → `GetLocalNow()` (`:22`) | Tidszon och datumgräns. `TestClock` sätter alltid 12:00 UTC och CI går i UTC. |
> | M11 `SubscriptionId` alltid 0 (`ForecastMappings.cs:9`) | JSON-testet kollar bara att egenskapen finns. |
> | M12 kategorins namn och färg ombytta (`CategoryMappings.cs:9`) | Inget test jämför kategorins namn eller färg i något svar. |
> | M13 `IntervalsBefore` hoppar aldrig över något (`BillingIntervalExtensions.cs:30-33`) | Skyddet mot CPU-förstärkning via gamla datum. Med 5 000 veckoprenumerationer med nästa=0001-01-01 gick tiden 2,1 s → 9,9 s. |
> | M26 `AddIntervals` veckor +1 dag efter 40 intervall (`BillingIntervalExtensions.cs:19`) | `Reference` i `SubscriptionPaymentDatesTests.cs:33` anropar samma `AddIntervals`, så den är inte oberoende. Påståendet om Python-verifiering i kommentaren finns inte i repot. |
> | M04, M08, M09, M10 | Harmlösa: sekundärsortering av kategorier, service-vakter som inte nås via HTTP, och `AsNoTracking`. |
>
> Fångades: ägarfiltret (M29, 9 test fallerar), `[Authorize]`, `IsActive`, förfallna betalningar, namnsortering, kategorisummor, `months`-gränserna och `from`/`to`-gränserna.
>
> **Rättning:**
> - Lägg till `dotnet test` före publish och låt deploy kräva grönt.
> - Lägg till gränstestet (återanvänder dina hjälpmetoder i `ForecastEndpointTests`):
>   ```csharp
>   [Fact]
>   public async Task Forecast_IncludesAPaymentOnTheLastDayOfThePeriod()
>   {
>       var user = await RegisterOnAsync(2027, 1, 15);
>       await user.AddSubscriptionAsync("Sista dagen", 100m, BillingInterval.Monthly, D(2027, 6, 30));
>       Assert.Equal([D(2027, 6, 30)], PaymentDates(await GetForecastAsync(user, 6), "Sista dagen"));
>   }
>   ```
> - Assertera `SubscriptionId` samt kategorins namn och färg.
> - Lägg till ett klocktest runt 23:30 UTC.
> - Gör `Reference` oberoende (egen kalenderaritmetik) eller checka in Python-kontrollen.
> - Lägg till Vitest/RTL för `ForecastCard`.
> - Beskriv testerna och hur de körs i README.
>
> ### F3. "Idag" räknas i UTC (Låg)
> **Status:** Verifierat. **Fil och rad:** `Services/ForecastService.cs:22`.
>
> **Fel:** `today` och månadsgränserna följer UTC, medan användaren och UI:t använder svensk tid. Fönstret är 1 h per dygn på vintern och 2 h på sommaren.
>
> **Bevis:**
> - Probe T1: klockan satt till 2027-01-31 23:30 UTC (00:30 den 1 feb i Stockholm). Prognosen startar på januari, räknar in betalningen 2027-01-31 och listan slutar en månad för tidigt (jan–mar i stället för feb–apr).
> - Live i webbläsaren (Europe/Stockholm) kl 01:21 den 5 okt: API-prognosen räknade in en betalning daterad 2026-10-04 (total 300 kr). Dashboardens "Kommande betalningar" i samma webbläsare visade samma betalning som "4 okt. 2026 · förfallen". Skärmdump: `e2e/shots/live-tz.png`.
>
> **Rättning:**
> ```csharp
> private static readonly TimeZoneInfo Stockholm = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");
> var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), Stockholm).DateTime);
> ```
> Använd samma klockhjälpare i `DashboardService` och `PaymentService` (se F11) och lägg till ett test vid 23:30Z på en månadsgräns.
>
> ### F4. `RegisterPayment` driver ankardagen, prognosen gör det inte (Låg)
> **Status:** Verifierat. **Fil och rad:** `Entities/Subscription.cs:35-43` mot `:45-66`, och `Entities/BillingIntervalExtensions.cs:14-15`.
>
> **Fel:** Kommentaren på `Subscription.cs:45-49` säger att datum räknas från ankaret för att undvika drift (31 → 28 → aldrig tillbaka). `RegisterPayment` stegar däremot från föregående datum och driver.
>
> **Bevis (probe T3):**
> - Prognos 2027-01-15 för nästa=2027-01-31: 01-31, 02-28, 03-31, 04-30, 05-31, 06-30.
> - Efter två "Markera betald" är `NextPaymentDate` först 2027-02-28 och sedan 2027-03-28. Prognosen blir då 03-28, 04-28, 05-28, 06-28. Ankardagen 31 är borta för gott.
> - Månadssummorna påverkas sällan, eftersom datumen hamnar i samma månad. Förfallet, "Kommande betalningar" och gränsfallet "idag" påverkas.
> - Beteendet i `RegisterPayment` verkar vara äldre kod, enligt kommentaren i `PaymentEndpointTests`. Inget test fastställer vilken regel som gäller.
>
> **Rättning:** Behåll en ankardag (`StartDate.Day` eller en ny `BillingDay`) och klampa per månad i `RegisterPayment`. Lägg till ett test som kör `RegisterPayment` två gånger över februari och jämför lagrat datum med prognosen.
>
> ### F5. Ingen ErrorBoundary: ett renderingsfel i prognoskortet tömmer hela appen (Låg)
> **Status:** Verifierat. **Fil och rad:** `components/charts/ForecastChart.jsx:35`, `main.jsx:9-19`.
>
> **Fel:** Tooltipen läser `row.categories.length` utan skydd, och ingen ErrorBoundary finns någonstans i appen.
>
> **Bevis:** Jag mockade ett 200-svar utan `costByCategory` och hovrade över en stapel. `TypeError: Cannot read properties of undefined (reading 'length')` kastades, och `#root` gick från 11 357 till **0 tecken**: vit sida, ingen navigering.
>
> **Trigger:** Det kräver ett kontraktsbrott. Det är rimligt vid versionsskillnad, eftersom frontend och backend deployas av separata workflows (misstanke).
>
> **Rättning:** Använd `row.categories ?? []`, och lägg en ErrorBoundary runt `<ForecastCard />` och runt `<Routes>`.
>
> ### F6. Diagrametiketter krockar på smala skärmar (Låg)
> **Status:** Verifierat. **Fil och rad:** `ForecastChart.jsx:17,75-78,101-108`.
>
> **Fel:** Gränsen för att dölja etiketter är fast (≥ 7 månader). Standardvyn med 6 månader visar alltid etiketter.
>
> **Bevis:** Mätt i förprognoskortets egna etiketter (bredd i px, parvis överlapp):
> - 360 px: överlapp från 1 999 kr/mån.
> - 390 px: överlapp från 12 999 kr/mån.
> - 768 px och uppåt: inga överlapp.
> - 12 månader på 992 px: inga överlapp.
>
> Skärmdump för en realistisk användare (7 prenumerationer, 3 093 kr/mån) på 360 px: `e2e/shots/s360-forecast-6m.png`. Etiketterna rinner ihop ("3 093 kr3 093 kr…"). Mätningen gjordes med systemfont i headless Chromium, som är bredare än Roboto/SF, så trösklarna kan ligga något högre på riktiga telefoner.
>
> **Rättning:** Styr etiketterna med uppmätt bredd, eller förkorta beloppen (`3,1 tkr`), eller dölj dem under `sm` och lita på tooltipen.
>
> ### F7. Kaskadladdning, inget avbrott och ingen "försök igen" (Låg)
> **Status:** Verifierat. **Fil och rad:** `pages/DashboardPage.jsx:30-32,88-90`, `components/ForecastCard.jsx:22-52,60-65`, `api/ForecastApi.js:8-13`, `api/errors.js:14-16`.
>
> **Bevis (produktionsbygge):**
> - Kaskad: `/api/forecast` startar först när `/api/dashboard` är klar (t=258 → 284 ms). Med 150 ms emulerad latens är det t=1106 → 1119 ms, och kortet syns efter 1 794 ms, ungefär en extra rundtur.
> - Inget avbrott: byte 12 → 3 månader medan 12-svaret är på väg. UI:t blir rätt (stale-skyddet fungerar), men det ersatta anropet laddade ändå ner 143 863 byte (3-månaderssvaret var 35 989).
> - Efter ett nätverksfel gav klick på redan vald period 0 nya anrop. Felet ligger kvar tills man väljer en annan period eller laddar om. Det finns ingen knapp.
> - Ett 400-svar visas som "Kontrollera de markerade fälten." trots att kortet saknar fält.
>
> **Rättning:**
> - Rendera `ForecastCard` oberoende av `summary`, eller hämta parallellt.
> - Använd `AbortController` (axios `signal`).
> - Lägg en "Försök igen"-knapp (en `reloadKey`) och ett eget felmeddelande.
> - Med F1 åtgärdat blir ett enda 12-månaderssvar som skivas klientsidigt för 3/6 enkelt.
>
> ### F8. Inga datumgränser: `RegisterPayment` ger 500 för år 9999 (Låg)
> **Status:** Verifierat. **Fil och rad:** `Dtos/Subscriptions/SubscriptionRequest.cs:20-24`, `Entities/BillingIntervalExtensions.cs:17-24`, `Entities/Subscription.cs:40`.
>
> **Bevis (probe T10, T15):**
> - `nextPaymentDate=9999-12-15` och `0001-01-01` accepteras (201).
> - Prognosen klarar bägge (200, och rätt datum för 0001-01-01).
> - `POST .../payments` på 9999-datumet ger **500** (`DateOnly.AddMonths` svämmar över).
> - I produktion blir ett oväntat undantag ett tomt 500-svar (ingen `UseExceptionHandler`/`AddProblemDetails`). Det är en kodläsning, jag körde Development.
>
> **Rättning:** Begränsa `StartDate` och `NextPaymentDate` (t.ex. 2000-01-01 till idag + 10 år) i `SubscriptionRequest.Validate`, och skydda `NextDateAfter` mot överflöd.
>
> ### F9. Dashboard och prognos är oense om förfallna betalningar (Låg, designval)
> **Status:** Verifierat (probe T2). **Fil och rad:** `ForecastService.cs:20-22,33`, `DashboardService.cs:60-65`.
>
> **Fel:** Dashboardens "Kommande betalningar" har ingen nedre gräns och listar förfallna betalningar i rött. Prognosen utelämnar den förfallna förekomsten och fortsätter cykeln. README dokumenterar det, men två kort på samma sida visar olika sanningar. Prognosen visar "Förfallen 10 dagar@2026-09-24" ingenstans.
>
> **Rättning:** Visa "Förfallet: N st / X kr" på prognoskortet, eller ge ett alternativ att räkna in förfallna betalningar i innevarande månad.
>
> ### F10. Kulturberoende sortering utan slutlig tie-breaker (Låg)
> **Status:** Verifierat för kulturen, misstanke för DB-ordningen. **Fil och rad:** `ForecastService.cs:34-35,67-68`.
>
> **Bevis (probe T7):** `OrderBy(x => x)` ger "Ägarens | apple | Apple | Åsa | Banan | Örjan | zeta | Zeta" med ICU. Med `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` ger samma kod "Apple | Banan | Zeta | apple | zeta | Ägarens | Åsa | Örjan". Ordningen på betalningar med samma datum beror alltså på värdens globaliseringsläge. Åäö hamnar inte sist, som en svensk förväntar sig.
>
> **Misstanke:** Frågan saknar `ORDER BY`, så betalningar med samma datum och namn kommer i godtycklig DB-ordning.
>
> **Rättning:** Använd en explicit jämförare (`StringComparer.Create(new CultureInfo("sv-SE"), true)`) och lägg till `.ThenBy(p => p.SubscriptionId)` och `.ThenBy(c => c.Category.Id)`.
>
> ### F11. `TimeProvider` används bara av prognosen (Låg, arkitektur)
> **Status:** Verifierat (kodläsning). **Fil och rad:** `Program.cs:94`, `DashboardService.cs:22`, `PaymentService.cs:54`, `SubscriptionService.cs:57`, `TokenService.cs:16`.
>
> **Fel:** Övriga tjänster använder `DateTime.UtcNow`. Testerna kan inte styra dashboardens eller betalningars tid, och prognosen och dashboarden kan se olika "idag".
>
> **Rättning:** Använd `TimeProvider` överallt, med den gemensamma Stockholm-hjälparen från F3.
>
> ### F12. Tillgänglighet (Låg)
> **Status:** Verifierat (DOM). **Fil och rad:** `ForecastChart.jsx:81-111`, `ForecastCard.jsx:73`.
>
> **Bevis:** SVG:n har `role="application"` och `tabindex="0"` men inget namn (inget `aria-label` och tom `<title>`). Laddning visas bara med `opacity-50`, utan `aria-busy` eller `aria-live`. Toggle-gruppen är korrekt märkt.
>
> **Rättning:** Ge diagrammet `aria-label` eller `title`/`desc`, sätt `aria-busy={isLoading}` på kortet, och överväg en dold tabell med månad och belopp.
>
> ### Småsaker (nits)
> - OpenAPI dokumenterar min/max för `months` men inte standardvärdet 6.
> - Bindningsfelet för icke-numeriskt `months` är på engelska (ramverkets standardtext), medan övrigt är svenskt.
> - Svarsfältet `date` heter `dueDate` i dashboarden.
> - `IntervalsBefore` är en publik extension. `internal` räcker.
>
> ## 5. Granskat utan anmärkning
>
> - **Åtkomstkontroll:** `[Authorize]` finns och `UserId` kommer från `sub`. Ägarfiltret ligger i frågan (`ForecastService.cs:29`), och requesten har bara `Months`, så ingen over-posting är möjlig. Kategorin kan inte tillhöra en annan användare. Att ta bort ägarfiltret (M29) fäller 9 test och att ta bort `[Authorize]` (M23) fäller 1.
> - **`months`-indata:**
>   - Range 1–24 ger 400 med svenskt meddelande.
>   - `abc`, `7.5`, `1e1`, `24.0`, `2147483648`, `-0`, `null`, `%00` och fullbreddssiffra ger 400.
>   - `+5`, `05`, ` 5` och hex `0x10` (=16) accepteras av modellbindaren. `[Range]` körs efter bindningen, så det är ofarligt.
>   - `months=5&months=50` ger 5 (första värdet gäller).
> - **SQL:** Frågan är parametriserad och översätts rent till `WHERE UserId = @userId AND IsActive = 1 AND NextPaymentDate <= @lastDay`. Den passar indexet `(UserId, NextPaymentDate)`.
> - **Datumlogiken** (`GetPaymentDates`, `IntervalsBefore`, `AddIntervals`) gav 0 avvikelser mot oberoende Python över 285 892 fall:
>   - alla ankardagar 2024–2025 (inklusive 29–31 och skottdag),
>   - ankare 0001-01-01 och 9999-12-31 (krasch uteblir),
>   - exakt de fönster appen använder.
>   Hoppoptimeringen är korrekt och motiverad, enligt F2/M13.
> - **Aggregeringen** (månadsfördelning, summor, kategorisummor och deras ordning, total) gav 0 avvikelser mot Python i 400 slumpscenarier över HTTP.
> - **Avrundning:** Prognosen räknar inte fram några belopp. Den summerar lagrade priser (`decimal(10,2)`), så ingen avrundning behövs. Jag jämförde 13 354 betalningar som `Decimal` i Python utan avvikelse.
> - **Ogiltigt intervall:** Det kan inte förgifta prognosen. `"Monthly, Weekly"` och `1` ger giltiga värden, och `7` och `"7"` ger 400 (probe T12).
> - **XSS och injektion:** HTML i kategori- och prenumerationsnamn renderas som text i tooltipen. Inget `<img>` skapades, `window.__xss` förblev `null` och inga dialoger öppnades. En färg med CSS-injektion nekas av regexen (400).
> - **Informationsläckage:** Svaret innehåller inga anteckningar eller användar-id. 500-svar blir tomma (verifierat i UI-scenariot "500 utan innehåll").
> - **Frontend:** `isCurrent`-skyddet mot sena svar fungerar (verifierat med fördröjt 12-svar). Tomt läge ("Inga betalningar väntas") och felmeddelandena fungerar. Konsolen är ren.
> - **Mönster:** Controllern är tunn. DTO:er ligger per resurs, servicen ligger bakom ett interface och returnerar DTO direkt, precis som `DashboardService`. Registreringen i `Program.cs:94,101` är rätt (Scoped service, `TimeProvider` som Singleton).
> - **OpenAPI:** 200, 400 och 401 är dokumenterade med min/max från `[Range]`.
>
> ## 6. Ej verifierat och begränsningar
>
> - Allt kördes mot SQLite och en lokal Kestrel/TestServer, inte SQL Server eller Azure. T-SQL-översättningen är verifierad offline, men frågan är inte körd mot SQL Server.
> - Azure F1-kvoterna (minne, CPU, utgående data) är inte kontrollerade, så F1:s effekt på Azure är en misstanke.
> - Om Azure-imagen kör ICU eller invariant globalisering är okänt (F10).
> - SQL Server-ordningen för betalningar med lika datum och namn (F10) är en misstanke.
> - Versionsskillnad mellan frontend och backend som utlösare för F5 är en misstanke.
> - Textbredden i F6 är mätt med en bredare systemfont än på riktiga telefoner.
> - SQL Servers hantering av pris med fler än 2 decimaler (avrundning eller trunkering) testades inte. Det ligger utanför prognosen.
>
> ## 7. Förslag på åtgärdsordning
>
> 1. CI-teststeg, gränstestet, samt assertioner för `SubscriptionId` och kategorins namn och färg (F2).
> 2. Ta bort `payments` ur standardsvaret, sätt tak på prenumerationer och lägg till rate limiting (F1).
> 3. Stockholm-tid och en gemensam klockabstraktion (F3, F11).
> 4. Tooltip-skydd, ErrorBoundary, etikettfix och "försök igen" (F5, F6, F7).
> 5. Resten.
>
> ## 8. Filer från granskningen
>
> Allt ligger under `/tmp/claude-0/-home-user/b692db47-b017-5a26-94d3-6c8668b089d8/scratchpad/`:
> - `work/backend/SubTracker.Api.Tests/ReviewProbes.cs` (T1–T16)
> - `mutate.py`, `mutate2.py`, `mutation.log` (mutationsprovning)
> - `datecheck/` och `aggcheck/` (Python-kontrollerna)
> - `e2e/*.mjs` med skärmdumpar i `e2e/shots/` (Playwright)

## Min kritiska bedömning

Jag tog inte rapporten för sann. Varje fynd verifierades med en egen körning innan jag agerade, och jag avgjorde själv vad som skulle åtgärdas, ändras eller lämnas. Commit-hashar finns i tabellen.

### Sammanfattning

- Granskaren bekräftade att **kärnan är korrekt**, och det stämmer med min egen oberoende kontroll (0 avvikelser mot en separat Python-beräkning, både före och efter rättningarna). Alla 12 fynd rörde kanterna: tester, tid, resurser, indata och frontend.
- **Alla 12 fynd gick att verifiera och höll**, med två förbehåll: Azure-effekten i F1 är en misstanke som jag inte kan pröva här, och F5 kräver ett kontraktsbrott för att utlösas.
- Jag **åtgärdade 11 helt eller delvis**. För F4 lämnade jag själva beteendet medvetet (det kräver en migration) och rättade bara den missvisande kommentaren. Jag ändrade mig om tre punkter som jag lämnat i den första granskningen.
- Jag **gjorde inte allt granskaren föreslog**, och i två fall valde jag en annan lösning än den föreslagna (F6, F10).

### Fynd för fynd

| # | AI:ns bedömning | Min verifiering | Min bedömning och åtgärd | Commit |
|---|---|---|---|---|
| F1 | Medel: obegränsad resursförbrukning | Reproducerat. Realistisk användare (24 prenumerationer, 12 mån): 54 KiB, varav UI:t behöver några KB. 1 000 försök att skapa prenumerationer gav 1 000 × 201 och ett svar på 22 MiB. Azure-effekten har jag inte kunnat pröva. | Rätt, och medel är rimligt eftersom registreringen är öppen. **Betalningslistan är nu opt-in** (4,5 KiB i stället för 54 KiB) och **taket är 200 prenumerationer** per användare (800 av 1 000 försök avvisas, värsta svar 4,4 MiB). Jag valde bort rate limiting, `CancellationToken` och komprimering: de är globala ändringar som övriga endpoints också saknar. | `5d69d13` |
| F2 | Medel: CI kör inga tester, 10 av 29 mutanter överlever | Körde om alla 29 mutanter på en kopia och fick **samma 10 överlevare**. | Rätt, och träffande: min referens i testet återanvände `AddIntervals`, och en kommentar påstod Python-verifiering utan att skriptet fanns i repot. **CI kör nu testerna före deploy**, 16 nya tester dödar nio av tio, referensen använder egen kalenderaritmetik och skripten ligger i repot. | `6915380` |
| F3 | Låg: "idag" i UTC | Fem tester röda mot koden (23:30 UTC är 00:30 svensk tid). | Rätt. I den första granskningen lämnade jag det med motiveringen att dashboarden gör likadant. Granskarens bevis att prognosen och dashboarden **syns oense för användaren** fick mig att ändra mig. Ny `SwedishTimeProvider`. | `d161168` |
| F4 | Låg: `RegisterPayment` glider 31 → 28 | Känt sedan första granskningen. | Rätt, och kommentaren på `GetPaymentDates` var missvisande. **Kommentaren är rättad.** Beteendet kvarstår: rätt åtgärd är att lagra betalningsdagen, vilket kräver en migration och är ett eget ärende. Står under "Kända begränsningar" i README. | `7b002d9` |
| F5 | Låg: ingen ErrorBoundary, hela appen blir tom | Mitt första försök att återskapa **misslyckades**: musen hamnade utanför visningsytan och tooltipen öppnades aldrig. Med en hover som scrollar elementet i vy: sidans innehåll gick från 1 452 till **0 tecken**. | Rätt, men det kräver ett kontraktsbrott (svar utan `costByCategory`), så låg är rimligt. Följden, en tom sida, är ändå dålig. **ErrorBoundary runt prognoskortet** och null-säkert diagram. | `c53c86c` |
| F6 | Låg: etiketter krockar på telefon | Bekräftat: 5 överlappande etiketter på 360 px. Med mina belopp var de trånga men läsbara. | Fyndet är rätt, men granskarens enklaste åtgärd (dölja dem) var **för trubbig**: den kastar information som oftast ryms. Jag gjorde en **annan lösning**: kortare belopp på smal skärm ("3,4 tkr"). Mätt utan överlapp på 320–576 px, även för femsiffriga belopp. | `c53c86c` |
| F7 | Låg: ingen "försök igen", inget avbrott, kaskadladdning | Bekräftat: 0 nya anrop vid omklick, och det ersatta 12-månadersanropet laddades ner klart. | **Delvis**: "Försök igen" och `AbortController` (avbrutet anrop räknas inte som fel). Kaskadladdningen lät jag vara: kortet visas bara när det finns aktiva prenumerationer, och en extra rundtur kostar mindre än en omstrukturering. | `c53c86c` |
| F8 | Låg: inga datumgränser, 500 för år 9999 | Samma sak hade jag själv verifierat i första granskningen. | Rätt. Jag hade klassat det som en separat rättning, men det hänger ihop med resursfrågan. **Datumen är begränsade till 2000–2100** i API och frontend. | `7b002d9` |
| F9 | Låg: dashboard och prognos oense om förfallna | Bekräftat (designval). | Förvirringen är verklig, beteendet avsiktligt. **Texten under diagrammet** säger nu att förfallna betalningar inte ingår. | `c53c86c` |
| F10 | Låg: kulturberoende sortering | Bekräftat med egen körning: samma kod ger `apple \| Apple \| Åsa \| Banan \| Zeta` med ICU och `Apple \| Banan \| Zeta \| apple \| Åsa` i invariant läge. Min jämförare ger `Apple \| apple \| Banan \| Zeta \| Åsa` i båda. | Rätt problem, men granskarens förslag (en `sv-SE`-jämförare) är **fortfarande olika utan ICU**. Jag valde `OrdinalIgnoreCase` och id som tie-breaker, så att ordningen blir densamma på varje server. Å, Ä och Ö hamnar efter Z. | `5d69d13` |
| F11 | Låg: `TimeProvider` bara i prognosen | Bekräftat. | Rätt. Hanteras tillsammans med F3: tre tjänster räknar "idag" på samma sätt via `GetToday()`. | `d161168` |
| F12 | Låg: diagrammet saknar namn, laddning aviseras inte | Bekräftat i DOM. | Rätt. **Diagrammet har ett namn** och innehållet markeras `aria-busy`. | `c53c86c` |

Småsakerna: standardvärdet 6 finns nu i OpenAPI (`DefaultValue`). De övriga (engelsk text för bindningsfel, `date` mot `dueDate`, `IntervalsBefore` som publik) lämnade jag, och bindningsfelet står i "Kända begränsningar".

### Var jag inte höll med, eller bedömde annorlunda

- **F6:** att dölja etiketterna löser kollisionen men kostar information. Jag mätte och valde kortare belopp.
- **F10:** granskarens jämförare löser inte det den pekar på, eftersom svensk sortering beror på ICU.
- **F7:** kaskadladdningen är en medveten avvägning, inte ett fel.
- **F1:** jag tog de två åtgärder som begränsar värsta fallet, inte hela listan. Rate limiting och komprimering är globala beslut för hela API:t.

### Det granskaren inte kunde veta, eller där rapporten har brister

- **Azure-effekten i F1** är en misstanke. Planens kvoter är inte kontrollerade och ingenting är mätt mot Azure.
- Rapporten är inte helt konsekvent: sammanfattningen säger att 89–100 % av svaret aldrig används, medan detaljen säger 97–100 %.
- Etikettmätningen i F6 gjordes med en bredare systemfont än riktiga telefoner har.

### Vad jag lärde mig om att använda AI för granskning

- En **oberoende** granskare med tomt sammanhang hittade sådant jag inte hade sett: tidszonen, testlucka efter testlucka och resursförstärkningen. Det hade jag svårt att få syn på själv, eftersom jag hade skrivit rättningarna.
- Det som gjorde rapporten **användbar var att varje fynd gick att köra om**. Jag kunde skilja verifierat från misstänkt, och jag hittade inget påstående som var rakt felaktigt.
- Verifiering gäller åt båda håll: mitt eget första försök att reproducera F5 misslyckades på grund av ett fel i mitt test. Hade jag då avfärdat fyndet hade jag haft fel.
- **Att köra om tester och mutanter på egen hand** var värt mer än att läsa rapporten. Rapporten påstod "10 av 29 överlever", men det var min egen körning som gjorde att jag litade på det.
- **Mina egna rättningar hade också ett hål.** Testerna ersätter klockan, så den riktiga `SwedishTimeProvider` och dess registrering i `Program.cs` kördes aldrig. Granskaren kunde inte se det (den granskade koden före rättningarna), men jag hittade det genom att tillämpa dess metod, mutationsprovning, på mina egna rättningar. Jag lade till tester (`37698f0`).
- Två AI-tillfällen räckte inte för att få allt rätt, men de hittade olika saker. Det första (min granskning) hittade fel i själva förslaget. Det andra hittade luckor i **min** granskning.

### Resultat efter båda granskningarna

| Mått | Före granskning | Efter AI-tillfälle 2 |
|---|---|---|
| Backendtester | 4 (baslinje) | **80** |
| Frontendtester | 0 (ingen testkörare) | **33** |
| Tester i CI | inga | **backend och frontend kör tester före deploy** |
| Mutationsprovning, granskarens 29 mutanter (anpassade till den ändrade koden) | 19 av 29 fångas | **29 av 29** |
| Mutationsprovning, alla 55 varianter | – | **52 av 55** (de tre övriga är ekvivalenta) |
| Oberoende beräkning mot API:t | 0 avvikelser (260 prenumerationer) | 0 avvikelser (260 prenumerationer) |
| Helkörning i webbläsare (scriptad användarresa) | – | **18 av 18 kontroller** |
| Svarsstorlek, realistisk användare, 12 mån | 54,4 KiB | **4,5 KiB** |
| Värsta svar för ett missbrukande konto | 22 MiB (1 000 prenumerationer) och växande | **0,01 MiB** (4,4 MiB med betalningslista) |
| Etiketter som överlappar på 360 px, 6 mån | 5 | **0** |
| "Idag" vid 23:30 UTC | fel dag | **rätt dag (svensk tid)** |
