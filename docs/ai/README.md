# AI-verktyg i systemutveckling – dokumentation

Den här mappen innehåller loggen över hur AI användes för att utveckla och granska en ny del av SubTracker, prognosfunktionen.
Den sammanfattande rapporten är PDF-filen [`../Dokumentation-AI-verktyg.pdf`](../Dokumentation-AI-verktyg.pdf).

## Vem gjorde vad

Texten är skriven i jag-form. Arbetet har utförts av **Claude Code** (Anthropic) på mitt uppdrag, i en molnsession med tillgång till repot,
kommandoraden och en webbläsare:

- **Huvudsessionen** (Claude Code) styrde arbetet och gjorde granskningen, rättningarna, testerna, mätningarna och dokumentationen.
- **AI-tillfälle 1, kodgenereringen,** gjordes av en separat Claude-agent med tomt sammanhang. Den fick bara prompten i [`01-kodgenerering.md`](01-kodgenerering.md) och tillgång till repot.
- **AI-tillfälle 2, den oberoende analysen,** gjordes av en annan separat Claude-agent med tomt sammanhang. Den fick bara läsa koden och köra tester och egna skript, inte ändra något i repot. Prompten och rapporten står ordagrant i [`03-ai-analys.md`](03-ai-analys.md).

Att agenterna var separata var avsiktligt: förslaget är då oretuscherat, och den andra bedömningen är inte färgad av min egen granskning.
Där texten säger "jag" om granskning och bedömning avses alltså arbetet som Claude Code utförde under mitt uppdrag.

## Uppgiftens krav och var de uppfylls

| Krav | Var |
|---|---|
| Välja en avgränsad del att utveckla med AI | Prognosfunktionen: ny controller, service, DTO:er och en dashboardkomponent. Se `01-kodgenerering.md` |
| Minst ett tillfälle där AI genererar kod | AI-tillfälle 1: [`01-kodgenerering.md`](01-kodgenerering.md) |
| Minst ett tillfälle där AI analyserar, felsöker eller förbättrar kod | AI-tillfälle 2: [`03-ai-analys.md`](03-ai-analys.md) |
| Dokumentera prompts, AI:ns svar och egna bedömningar | Ordagrant i `01` och `03`, bedömningar i `02` och `03` |
| Granska AI-genererad kod: fungerar med resten, arkitektur, duplicerad logik, säkerhet, secrets, felhantering | Checklista med belägg i [`02-granskning.md`](02-granskning.md) |
| Rätta problemen och visa före/efter | Fynd med kodutdrag i `02`, tabell per fynd och före/efter-mått i `03`, commit-listan nedan |
| Granska också AI:ns andra resultat kritiskt | "Min kritiska bedömning" i `03`: verifiering, avvikelser, vad rapporten inte kunde veta |
| Testa att lösningen fungerar med resten av appen | 80 backendtester, 33 frontendtester, CI och en scriptad körning i webbläsare ([`verifiering/`](verifiering/)) |
| Separata, meningsfulla commits | Listan nedan |

## Filer

| Fil | Innehåll |
|---|---|
| [`01-kodgenerering.md`](01-kodgenerering.md) | AI-tillfälle 1: prompten, AI:ns svar ordagrant, vad som levererades och min första bedömning |
| [`02-granskning.md`](02-granskning.md) | Min granskning av AI:ns förslag: metod, checklista med sex områden, fem rättade fynd med före/efter och fynd som lämnades |
| [`03-ai-analys.md`](03-ai-analys.md) | AI-tillfälle 2: prompten, rapporten ordagrant (12 fynd) och min kritiska bedömning av varje fynd |
| [`verifiering/`](verifiering/) | Skripten bakom mätningarna: oberoende beräkning, mutationsprovning, webbläsarkörning |

## Commits som hör till uppgiften

Allt ligger i det här repot på grenen `claude/blissful-edison-7izj8g`, ovanpå `a936831` (inlämningen av föregående uppgift).
Commit-hasharna ändras om historiken skrivs om, så grenen ska slås ihop med en vanlig merge-commit, aldrig med squash eller rebase.

| Commit | Roll | Beskrivning |
|---|---|---|
| `f0b2903` | Förberedelse | Testprojekt med integrationstester mot SQLite |
| `ab8b68c` | AI-tillfälle 1 – förslaget, orört | AI-förslag (ogranskat): prognos – backend |
| `00fa1c3` | AI-tillfälle 1 – förslaget, orört | AI-förslag (ogranskat): prognos – frontend |
| `a818df7` | Dokumentation | AI-logg 1: prompt och svar för kodgenereringen |
| `49e6664` | Egen granskning av AI-koden | Granskning 1: injicerbar klocka i prognosen + tester av beteendet |
| `a584ee1` | Egen granskning av AI-koden | Granskning 2: validera months som övrig indata (ForecastRequest) |
| `1622a70` | Egen granskning av AI-koden | Granskning 3: begränsa beräkningen av betalningsdatum + AsNoTracking |
| `69a54cb` | Egen granskning av AI-koden | Granskning 4: ange prognosperiodens slut i stället för "N månader" |
| `5041bb6` | Dokumentation | Granskningsrapport: fynd, belägg och rättningar för AI-förslaget |
| `f2d51d7` | Dokumentation | AI-logg 2: prompt och oberoende granskningsrapport (ordagrant) |
| `6915380` | Åtgärd efter AI-tillfälle 2 | Analys 2 (F2): CI kör testerna + testluckor som mutationsprovningen hittade |
| `d161168` | Åtgärd efter AI-tillfälle 2 | Analys 2 (F3, F11): "idag" räknas i svensk tid i prognos, dashboard och betalningar |
| `5d69d13` | Åtgärd efter AI-tillfälle 2 | Analys 2 (F1, F10): begränsa prognosens svarsstorlek, tak på prenumerationer, deterministisk sortering |
| `7b002d9` | Åtgärd efter AI-tillfälle 2 | Analys 2 (F8, F4): datumgränser för prenumerationer, ärlig kommentar och kända begränsningar |
| `c53c86c` | Åtgärd efter AI-tillfälle 2 | Analys 2 (F5, F6, F7, F9, F12): robustare och tydligare prognoskort + frontendtester |
| `37698f0` | Åtgärd efter AI-tillfälle 2 | Tester av den riktiga klockan: SwedishTimeProvider och registreringen i Program.cs |
| `d66fed8` | Verifiering | Verifieringsskript: oberoende beräkning, mutationsprovning, helkörning i webbläsare |
| `81b0789` | Dokumentation | AI-logg 2: min kritiska bedömning av granskningsrapporten |

Commits efter den här listan (index och PDF) finns med i `inlamning.txt` i inlämningen.
