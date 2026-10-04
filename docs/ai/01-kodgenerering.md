# AI-tillfälle 1 – kodgenerering: prognosfunktion

| | |
|---|---|
| **Datum** | 2026-10-04 |
| **AI-verktyg** | Claude Code (Anthropic) |
| **Syfte** | Generera en ny funktion (prognos över kommande betalningar) till SubTracker |
| **Avgränsad del av appen** | Ny service + controller + DTO:er i backend och en ny dashboardkomponent i React |

## Hur AI:n användes

Claude Code styrde arbetet i sessionen. Själva kodgenereringen gjordes av en **separat Claude-agent med tomt sammanhang**:
den fick bara prompten nedan och tillgång till repot. Den visste alltså inte vad jag skulle granska efteråt,
och dess förslag är därför oretuscherat – inklusive sådant som jag senare ändrade.
Förslaget committades orört (se commit-listan i `docs/ai/README.md`) så att diffen mot mina senare commits visar exakt vad granskningen förändrade.

## Prompt (ordagrant)

```text
Jag vidareutvecklar min webbapplikation SubTracker (ASP.NET Core 10 Web API + React/Vite) där användare håller koll på sina digitala prenumerationer. Repot ligger i /home/user/fullstack-app-azure-ai – läs README.md först för att förstå arkitekturen.

Uppgift: bygg en prognosfunktion som visar vad användaren kommer att betala framåt i tiden.

Funktion
- Nytt API-endpoint GET /api/forecast?months=N (kräver inloggning, precis som övriga endpoints). Det räknar fram en prognos för de kommande N månaderna (standard 6) utifrån användarens aktiva prenumerationer. Nästa betalningsdatum och betalningsintervall (vecka/månad/kvartal/år) avgör vilka datum en prenumeration dras.
- Svaret ska för varje månad ge den totala förväntade kostnaden, hur den fördelas per kategori och vilka enskilda betalningar (prenumeration, belopp, datum) som ingår. Det ska också ge en total för hela perioden.
- Frontend: visa prognosen på dashboard-sidan (diagram per månad och möjlighet att välja antal månader, t.ex. 3/6/12) i samma stil som övriga dashboard-komponenter.

Krav
- Följ projektets befintliga arkitektur och konventioner: tunn controller, logik i en service bakom ett interface, DTO:er och mappningar, ServiceResult/ServiceError, svenska felmeddelanden och kommentarer i samma stil som övrig kod, samt React-komponenter och API-klass på samma sätt som idag.
- Skriv ren, läsbar kod och ändra inte befintlig funktionalitet i onödan.
- Kontrollera att det kompilerar: `dotnet build backend/SubTracker.Api/SubTracker.Api.csproj` och, i frontend/, `npm ci && npm run lint && npm run build`.
- Testerna skriver jag själv, så lägg inte till några. Gör inga git-commits och arbeta bara inne i /home/user/fullstack-app-azure-ai.

Svara på svenska med en kort sammanfattning av vad du gjort och vilka designval du gjorde.
```

## AI-verktygets svar (ordagrant)

> Prognosfunktionen är byggd, backend och frontend. Allt kompilerar och lintar rent. Jag har inte gjort några commits och inte lagt till några tester. Jag har bara ändrat i /home/user/fullstack-app-azure-ai. Det andra repot, /home/user/Fullstack-App-Azure, är orört.
>
> **Kontroller**
> - `dotnet build` av hela lösningen med --no-incremental ger 0 varningar och 0 fel.
> - `npm ci && npm run lint && npm run build` går igenom, och Prettier-kontrollen av ändrade frontendfiler är ren.
> - Era 4 befintliga tester passerar.
> - Jag skrev tillfälligt ca 32 verifieringstester mot API:t och körde en helkörning i Chromium med Playwright (API:t mot SQLite, Vite och seedad demodata). Jag kontrollerade också att frågan översätts för SQL Server. Alla tillfälliga filer är borttagna.
>
> **Backend** (/home/user/fullstack-app-azure-ai/backend/SubTracker.Api/)
> - Nya filer:
>   - `Controllers/ForecastController.cs`: tunn, `[Authorize]`, `months` har standard 6.
>   - `Services/IForecastService.cs` och `Services/ForecastService.cs`.
>   - `Dtos/Forecast/` med `ForecastResponse`, `ForecastMonthResponse`, `ForecastCategoryResponse` och `ForecastPaymentResponse`.
>   - `Mappings/ForecastMappings.cs`.
> - Ändrade filer:
>   - `Entities/BillingIntervalExtensions.cs`: ny `AddIntervals`; `NextDateAfter` delegerar dit med oförändrat beteende.
>   - `Entities/Subscription.cs`: ny `GetPaymentDates(from, to)`.
>   - `Program.cs`: DI-registrering.
> - Svarsform: `{ total, months: [{ year, month, total, costByCategory: [{ category, total }], payments: [{ subscriptionId, name, amount, date, category }] }] }`. Alla N månader finns alltid med (tomma månader har tomma listor). Betalningar är sorterade på datum och sedan namn, kategorier på total fallande.
>
> **Designval**
> 1. **Perioden.** `months=N` ger N hela kalendermånader med början i innevarande månad. Bara betalningar från och med idag räknas, så första månaden visar det som återstår.
> 2. **Förfallna betalningar ingår inte.** Ett nästa betalningsdatum före idag hoppas över, men intervallet följs vidare från det datumet. Det beror på att appen inte flyttar fram datumet själv, och dragningen oftast redan har skett. Dashboardens "Kommande betalningar" visar dem fortfarande som förfallna.
> 3. **Bara aktiva prenumerationer, och belopp är fullt pris på betalningsdagen.** Det är faktiska dragningar, inte månadskostnad. En årsprenumeration syns därför bara i sin betalmånad. Priserna har högst 2 decimaler och ingen division sker, så ingen avrundning behövs.
> 4. **Inget datumglidande vid månadsskifte.** Varje datum räknas som nästa betalningsdatum plus k intervall, inte från föregående datum. 31 jan blir 28 feb, 31 mar, 30 apr, och 29 feb 2028 går tillbaka till 29 feb 2032. Logiken är ren och går att enhetstesta utan databas.
> 5. **Validering.** `months` måste vara 1–24, kontrolleras i servicen med `ServiceError.Validation` och ger 400 med "Antal månader måste vara mellan 1 och 24." Det skyddar mot tunga beräkningar även om servicen anropas på annat håll.
> 6. **En enda databasfråga.** Filter på användare, aktiv och nästa betalning ≤ periodens slut ligger i databasen (passar indexet `(UserId, NextPaymentDate)`). Resten räknas i minnet, så det fungerar likadant på SQL Server och SQLite.
>
> **Frontend** (/home/user/fullstack-app-azure-ai/frontend/src/)
> - Nya filer:
>   - `api/ForecastApi.js`.
>   - `components/ForecastCard.jsx`: val 3/6/12 mån med `ToggleButtonGroup`, total för perioden, egen laddning och eget felläge, samt skydd mot att ett sent svar skriver över ett nyare val.
>   - `components/charts/ForecastChart.jsx`: stapeldiagram i samma stil som `PaymentsChart`, med tooltip som visar månad, total och kategorifördelning.
> - Ändrade filer: `pages/DashboardPage.jsx` (kortet sitter efter "Kommande betalningar" och visas bara när det finns aktiva prenumerationer) och `utils/format.js` (ny `formatMonthYear`).
> - Vid 12 månader döljs beloppen ovanför staplarna under lg-bredd för att undvika överlapp. Då visas de bara i tooltipen.
> - README.md har fått en rad i API-tabellen.
>
> **Bra att veta när du skriver och granskar testerna**
> - "Idag" är UTC, som i dashboarden. Hur många betalningar en månadsprenumeration får i perioden beror på dagens datum, så testerna bör räkna förväntade datum relativt dagens datum.
> - `months=abc`, `7.5` och tomt värde ger ASP.NET:s engelska standardmeddelande, med nyckeln `months`. Befintliga endpoints beter sig likadant vid felaktiga värden, t.ex. `price: "abc"`, så jag ändrade ingen global konfiguration.
> - Befintligt beteende som jag lät vara: `RegisterPayment` flyttar fram datumet steg för steg, så 31:a blir 28:e och stannar där när man registrerar betalningar. Prenumerationsdatum saknar också över- och undergräns i valideringen.

## Levererade filer (oförändrat förslag)

| Fil | Roll |
|---|---|
| `backend/SubTracker.Api/Controllers/ForecastController.cs` | Ny controller, `GET /api/forecast?months=N` |
| `backend/SubTracker.Api/Services/IForecastService.cs`, `ForecastService.cs` | Ny service med validering, databasfråga och beräkning |
| `backend/SubTracker.Api/Dtos/Forecast/*` | Fyra response-DTO:er |
| `backend/SubTracker.Api/Mappings/ForecastMappings.cs` | Prenumeration → `ForecastPaymentResponse` |
| `backend/SubTracker.Api/Entities/Subscription.cs` | Ny metod `GetPaymentDates(from, to)` |
| `backend/SubTracker.Api/Entities/BillingIntervalExtensions.cs` | Ny `AddIntervals`, `NextDateAfter` delegerar dit |
| `backend/SubTracker.Api/Program.cs` | DI-registrering av servicen |
| `frontend/src/api/ForecastApi.js` | API-klass |
| `frontend/src/components/ForecastCard.jsx` | Kort med val av antal månader, laddning och felhantering |
| `frontend/src/components/charts/ForecastChart.jsx` | Stapeldiagram med tooltip |
| `frontend/src/pages/DashboardPage.jsx`, `frontend/src/utils/format.js` | Integration på dashboarden, ny datumformatering |

## Min första bedömning (före den egentliga granskningen)

Jag litar inte på förslaget bara för att AI:n själv säger att det kompilerar och är testat. Det jag kontrollerade direkt:

- Hela lösningen byggs utan varningar (`dotnet build --no-incremental`), `npm run lint` och `npm run build` går igenom och mina fyra baslinjetester är fortsatt gröna.
- Förslaget ser **bra ut vid första anblick**: tunn controller, logiken bakom ett interface, DTO:er och mappning, ingen entitet skickas ut, svenska meddelanden, `[Authorize]` och filtrering på inloggad användare. Det återanvänder till och med `NextDateAfter` i stället för att skriva ny datumlogik.
- Det betyder inte att det är *rätt*. Beräkningen bygger på datumregler (månadsskiften, skottdag, förfallna datum) och på användarstyrd data, och det är just sådant som är lätt att få fel men svårt att se vid läsning. Granskningen i [`02-granskning.md`](02-granskning.md) går därför igenom koden mot en checklista och bevisar påståenden med tester i stället för att bara läsa.

Frågor som jag tog med mig till granskningen:

1. Går beräkningen att testa deterministiskt när "idag" läses från systemklockan inne i servicen?
2. Hur mycket arbete kan en enskild prenumeration orsaka? Datumen kommer från användaren och är obegränsade.
3. Valideras `months` på samma sätt som övriga indata i projektet, och ser felen likadana ut?
4. Läser koden mer data än den behöver, och spåras entiteter i onödan för en ren läsfråga?
5. Gör förfallna prenumerationer och första månaden (som bara räknas från och med idag) att siffrorna kan missförstås?
