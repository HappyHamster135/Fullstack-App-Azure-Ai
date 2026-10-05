# Granskning av AI-förslaget – prognosfunktionen

Den här filen är min egen kodgranskning av det AI-verktyget genererade i [AI-tillfälle 1](01-kodgenerering.md).
Det granskade förslaget finns orört i två commits, så att diffen mot mina rättningar går att läsa:

| Commit | Innehåll |
|---|---|
| `ab8b68c` | AI-förslag: backend (controller, service, DTO:er, mappning, `GetPaymentDates`, `AddIntervals`) |
| `00fa1c3` | AI-förslag: frontend (`ForecastApi`, `ForecastCard`, `ForecastChart`, integration i `DashboardPage`) |

## 1. Hur jag granskade

AI:n skrev själv att allt kompilerar och är testat. Det räcker inte, så jag granskade på sex sätt:

1. **Läste hela diffen rad för rad** (190 + 273 rader) och jämförde med resten av projektets mönster.
2. **Byggde och lintade** (`dotnet build`, `npm run lint`, `npm run build`) och körde de fyra baslinjetesterna.
3. **Differentialtest mot en oberoende beräkning.** Jag skrev en egen beräkning av förväntade betalningsdatum i Python, utan någon kod gemensam med C#-versionen,
   och jämförde med det riktiga API:t för 260 prenumerationer (månadsskiften, 29 februari, förfallna datum, alla fyra intervall) och fem periodlängder (1, 3, 6, 12 och 24 månader).
4. **Tester med fast klocka** genom hela kedjan (JWT → controller → service → EF Core/SQLite). Se commit `49e6664`.
5. **Mutationskontroll:** jag förstörde koden med flit på olika sätt och kontrollerade att testerna larmade. Ett test som aldrig kan fela bevisar ingenting.
6. **Mätning och körning:** svarstid och svarsstorlek mot det riktiga API:t, samt en helkörning i en riktig webbläsare (Chromium via Playwright) mot API + Vite.

## 2. Resultat per granskningsområde

| Område | Bedömning | Belägg | Åtgärd |
|---|---|---|---|
| **Fungerar tillsammans med resten av appen** | Ja | 40 tester genom hela kedjan är gröna. Differentialtestet gav **0 avvikelser** av 260 prenumerationer × 5 periodlängder. AI:ns ändring av den befintliga `NextDateAfter` bryter inte "Markera betald" (`PaymentEndpointTests`). Helkörning i webbläsare utan konsolfel. | – |
| **Följer arkitektur och ansvarsfördelning** | Till största delen. Tunn controller, service bakom interface, DTO:er, mappning och datumlogik på entiteten (som `RegisterPayment`) är rätt. Två avvikelser. | Validering låg i servicen i stället för i en DTO (F2). Klockan lästes direkt ur systemet (F1). | F1, F2 |
| **Onödig eller duplicerad logik** | Bra. AI:n återanvände `NextDateAfter` via `AddIntervals` i stället för att skriva ny datumlogik. En onödig konstruktion fanns. | `ServiceResult`/`ServiceError` användes för ett fel som en DTO hanterar bättre. Mindre likheter med `DashboardService` och `PaymentsChart` är medvetet kvar (se avsnitt 4). | F2 |
| **Indata och användardata hanteras säkert** | Delvis. Åtkomstkontrollen är rätt. Två brister kring indata. | Alla frågor filtrerar på inloggad användare. Testet för detta går sönder när jag tar bort filtret. `months` hade fel felformat (F2). Användarens datum saknar nedre gräns och styr hur mycket arbete en prenumeration kostar (F3). | F2, F3 |
| **Secrets och känslig information** | Inga. | Sökning efter `password`, `secret`, `key`, `token`, `connection string`, `bearer` och URL:er i AI:ns kod (produktion) ger bara README-texten "utan token" och en befintlig rad (`ITokenService`). Inga konfigurationsfiler ändrades. Testkoden innehåller en tydligt märkt engångsnyckel (`test-only-signing-key…`) och ett testlösenord som bara används av testvärden i minnet. | – |
| **Fel och oväntade situationer** | Bra grund, med små luckor. | Ogiltigt `months` ger 400 med `ProblemDetails`. Frontend har eget laddnings- och felläge samt skydd mot att ett sent svar skriver över ett nyare val. Luckor: felmeddelandets format (F2), en rubrik som kunde missförstås (F5). | F2, F5 |

## 3. Fynd som jag rättade

### F1 – Klockan går inte att styra (arkitektur/testbarhet) – commit `49e6664`

**AI föreslog:** `var today = DateOnly.FromDateTime(DateTime.UtcNow);` inne i `ForecastService`.

**Problem:** Resultatet beror på vilken dag koden körs. Månadsskiften, skottår och förfallna betalningar går därför inte att testa deterministiskt. AI:n skrev själv i sin sammanfattning att testerna "bör räkna förväntade datum relativt dagens datum". Sådana tester ger olika utfall olika dagar, och de dagar som är intressanta (31:a, 29 februari) kommer sällan.

**Rättning:** `TimeProvider` (finns inbyggd i .NET) registreras i `Program.cs` och injiceras i servicen. Produktionsbeteendet är oförändrat. Testprojektet byter ut den mot en klocka som varje test styr (`TestClock`).

```diff
-public class ForecastService(AppDbContext db) : IForecastService
+public class ForecastService(AppDbContext db, TimeProvider timeProvider) : IForecastService
 ...
-var today = DateOnly.FromDateTime(DateTime.UtcNow);
+var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
```

**Belägg:** 23 nya tester, alla gröna mot AI:ns logik (så logiken var rätt). Mutationskontroll: jag tog bort filtret på aktiva prenumerationer, filtret på användare, "från idag"-gränsen och "räkna från ankardatumet" i tur och ordning. Testerna larmade för alla fyra.

### F2 – Valideringen följer inte projektets mönster (indata) – commit `a584ee1`

**AI föreslog:** gränserna 1–24 som konstanter i servicen, standardvärdet 6 i controllern och ett fel byggt för hand:

```csharp
["months"] = [$"Antal månader måste vara mellan {MinMonths} och {MaxMonths}."]
```

**Problem:** Projektets övriga indata valideras med DataAnnotations i en DTO, så att felen får samma form överallt. Här hamnade regeln på tre ställen, felnyckeln blev `months` i stället för `Months`, och Scalar visade inga gränser för parametern. En orimlig begäran hanterades alltså på ett annat sätt än alla andra.

**Belägg (före):**

```text
?months=25  -> 400  {"errors": {"months": ["Antal månader måste vara mellan 1 och 24."]}}
?months=abc -> 400  {"errors": {"months": ["The value 'abc' is not valid."]}}
```

Jag skrev först testerna (de förväntar projektets format) och de var **röda mot AI:ns version**: 6 av 30 föll.

**Rättning:** ny `ForecastRequest` med `[Range]` och svenskt meddelande, bunden med `[FromQuery]`. Gränser och standardvärde finns på ett ställe. Servicen returnerar `ForecastResponse` direkt (som `IDashboardService`) och skyddar bara beräkningen med en `ArgumentOutOfRangeException` mot felanrop från annan kod.

```csharp
public record ForecastRequest
{
    public const int DefaultMonths = 6;
    public const int MinMonths = 1;
    public const int MaxMonths = 24;

    [Range(MinMonths, MaxMonths, ErrorMessage = "Antal månader måste vara mellan 1 och 24.")]
    public int Months { get; init; } = DefaultMonths;
}
```

**Belägg (efter):** `{"errors": {"Months": ["Antal månader måste vara mellan 1 och 24."]}}`. OpenAPI-dokumentet anger nu `minimum: 1` och `maximum: 24`. Alla 30 tester gröna.

### F3 – Arbetet växer med hur gammalt användarens datum är (säkerhet/prestanda) – commit `1622a70`

**AI föreslog:** `GetPaymentDates` stegade från `NextPaymentDate` tills perioden nåddes.

**Problem:** `NextPaymentDate` är ett värde från användaren och `SubscriptionRequest` har ingen nedre gräns. Med ett datum långt bak i tiden gör varje prenumeration (och varje anrop) onödigt många steg. Kostnaden går att skala med antalet prenumerationer, som inte heller begränsas. Det är en lågrisk-variant av att användardata styr serverns arbete (på en gratisplan med daglig CPU-kvot är det ändå värt att stänga).

**Belägg (före):** 300 veckoprenumerationer, `months=6`, median av 5 anrop mot det riktiga API:t (SQLite, lokalt – siffrorna är riktmärken, inte exakta):

| Nästa betalning | Före | Efter |
|---|---|---|
| idag (normalt) | 94 ms | 45 ms |
| 1990-01-01 | 55 ms | 46 ms |
| **0001-01-01, veckovis** | **403 ms** | **52 ms** |
| 0001-01-01, månadsvis | 88 ms | 14,5 ms |

Det är ca 1 ms extra CPU per prenumeration och anrop i värsta fallet. Allvarligheten är låg, men rättningen är liten och gör kostnaden oberoende av datumets ålder.

**Rättning:** `IntervalsBefore` räknar ut hur många hela intervall som kan hoppas över utan att passera en betalning på eller efter periodens början, och loopen börjar där. Resonemanget (betalningar före det antalet ligger alltid före `from`) står som kommentar vid koden. Samma commit lägger `AsNoTracking()` på prognosfrågan, eftersom den är en ren läsfråga och entiteterna inte behöver spåras (det förklarar att även det normala fallet blev snabbare).

```diff
-var date = NextPaymentDate;
-for (var count = 1; date <= to; count++)
-{
-    if (date >= from) yield return date;
-    date = BillingInterval.AddIntervals(NextPaymentDate, count);
-}
+for (var count = BillingInterval.IntervalsBefore(NextPaymentDate, from); ; count++)
+{
+    var date = BillingInterval.AddIntervals(NextPaymentDate, count);
+    if (date > to) yield break;
+    if (date >= from) yield return date;
+}
```

**Belägg (efter):** `SubscriptionPaymentDatesTests` jämför den nya koden med AI:ns ursprungliga loop som referens för 4 000 slumpade prenumerationer och fönster samt äldsta möjliga datum för varje intervall. Eftersom en felaktig överhoppning skulle tyst missa betalningar prövade jag fem medvetet trasiga varianter: fyra fångades. Den femte (kvartal avrundat uppåt) är en *ekvivalent mutant*. Den hoppar bara över betalningar i månader före perioden och ändrar därför inte resultatet, så att den överlever är rätt och inget hål i testerna.

### F4 – Onödig spårning av entiteter – ingår i commit `1622a70`

Se F3. `AsNoTracking()` på en ren läsfråga. Liten förbättring, ingen risk.

### F5 – Rubriken kan missförstås (frontend) – commit `69a54cb`

**AI föreslog:** `de kommande {antal} månaderna`.

**Problem:** API:t räknar hela kalendermånader från innevarande månad och den första bara från och med idag. Sent i en månad är perioden därför nästan en månad kortare än rubriken säger, och totalen kan misstolkas.

**Rättning:** kortet visar `17 713 kr till och med mars 2027`, hämtat från sista månaden i svaret. Verifierat i webbläsare mot det riktiga API:t.

## 4. Fynd som jag medvetet *inte* rättade

Att granska är också att avgöra vad som inte ska ändras. Tabellen visar läget **efter den här granskningen**.
Tre av punkterna ändrade jag mig om när den oberoende AI-granskningen i [`03-ai-analys.md`](03-ai-analys.md) visade bevis jag inte hade, och de är markerade nedan.

| Fynd | Bedömning | Motivering |
|---|---|---|
| **Svaret är större än vad UI:t använder.** Varje betalning bär ett helt kategoriobjekt, och frontend läser aldrig listan `payments`. Mätt: 86 KiB för 40 prenumerationer och 6 månader (172 KiB för 12). | Kvarstod här. **Åtgärdad efter AI-tillfälle 2** (`5d69d13`). | Jag bedömde storleken som rimlig för normal användning och höll fast vid kravet i prompten. Granskaren mätte ett missbruksscenario jag inte hade provat: ett konto kunde skapa obegränsat många prenumerationer, och svaret växer med prenumerationer × betalningar (22 MiB för 1 000 veckoprenumerationer). Betalningslistan är nu opt-in och antalet prenumerationer är begränsat. |
| **"Idag" är UTC**, inte svensk tid. Runt midnatt kan datumet vara fel några timmar. | Kvarstod här. **Åtgärdad efter AI-tillfälle 2** (`d161168`). | Mitt skäl var att dashboarden och `PaymentService` gör likadant. Granskaren visade att det syns för användaren: i en svensk webbläsare räknade prognosen in en betalning som dashboarden samtidigt visade som förfallen. Då är konsekvens med resten inget försvar. Alla tre tjänsterna räknar nu svensk tid. |
| **Icke-numeriska `months` (t.ex. `abc`) ger ASP.NET:s engelska standardtext.** | Kvarstår. | Det är global modellbindning som alla endpoints delar, och frontend skickar bara 3, 6 eller 12. Att ändra det globalt är en egen uppgift. |
| **`ForecastChart` liknar `PaymentsChart`** (tooltip, stapeldiagram, tomt läge). | Kvarstår. | Datan, tooltipen och etiketterna skiljer sig åt. En gemensam abstraktion med bara två användare vore för tidig. |
| **`RegisterPayment` flyttar fram datumet steg för steg**, så en betalning den 31:a blir den 28:e efter februari och förblir det. Prognosen utgår från det lagrade datumet och ärver därför glidningen. | Kvarstår, finns redan i befintlig kod. | AI:n påpekade det själv. Månadsfördelningen i prognosen påverkas inte, bara det exakta datumet. Rätt åtgärd är att lagra betalningsdagen, vilket är ett eget förändringsärende. |
| **Datumfält har ingen över- eller undergräns i valideringen.** Jag verifierade att en prenumeration med nästa betalning 9999-12-31 ger 500 vid "Markera betald" (`AddMonths` svämmar över). Prognosen hanterar samma data utan fel. | Kvarstod här. **Åtgärdad efter AI-tillfälle 2** (`7b002d9`). | Felet fanns före AI:ns ändring (`NextDateAfter` kastade likadant) och jag såg det som en separat rättning av befintlig validering. Granskaren pekade på att det hänger ihop med resursfrågan (F3 här), så datumen är nu begränsade till 2000–2100 i både API och frontend. |

## 5. Sammanfattning

- **Det AI:n gjorde bra:** rätt arkitektur på första försöket, korrekt datumlogik (0 avvikelser mot en oberoende beräkning), användarisolering, återanvändning av befintlig kod, och ett frontend-kort som passar resten av dashboarden.
- **Det jag fick rätta:** fem punkter (klocka, valideringsmönster, begränsat arbete per prenumeration, spårning, rubrik). Ingen av dem gav fel svar vid vanlig användning. De handlar om testbarhet, konsekvens med projektets mönster, robusthet mot ovanliga indata och tydlighet. Flera av dem blev tydliga först när jag testade, mätte eller körde appen: valideringens felformat syntes först i svaret, kostnaden för gamla datum först i mätningen och den missvisande rubriken först i webbläsaren.
- **Lärdom:** AI:ns egen försäkran om att koden är verifierad är inte ett skäl att hoppa över granskningen. Förslaget var bra, och just därför var det lätt att tro på. Det som hade gått förbi vid ytlig läsning var testbarhet, felformat och beteendet för ovanliga indata.

Status efter den här granskningen (före AI-tillfälle 2): **40 tester gröna**, `dotnet build` utan varningar, `npm run lint` och `npm run build` rena. Slutstatus efter båda granskningarna finns i [`03-ai-analys.md`](03-ai-analys.md).
