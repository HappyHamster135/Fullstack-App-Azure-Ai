# Verifiering – så kontrollerades prognosfunktionen

Skripten här är det som ligger bakom påståendena i `../02-granskning.md` och `../03-ai-analys.md`. De är verktyg för
verifiering, inte en del av appen eller lösningen, och körs inte i CI. (CI kör `dotnet test` och `npm test`.)

| Fil | Vad den gör |
|---|---|
| `oracle_check.py` | **Oberoende beräkning.** Räknar ut förväntade betalningsdatum och summor i Python, utan någon kod gemensam med C#, och jämför med det riktiga API:t för slumpade prenumerationer (månadsskiften, 29 februari, förfallna datum, alla intervall, 1–24 månader). |
| `size_check.py` | Mäter prognosens svarsstorlek (standard och med betalningslista) och visar att taket på 200 prenumerationer håller. |
| `mutation_result.txt` | Utskriften från den sista körningen av `mutation_check.py`: 52 av 55 mutanter fångas, och de tre som överlever är ekvivalenta. |
| `mutation_check.py` | **Mutationsprovning.** För in ett medvetet fel i taget i en kopia av koden (55 varianter) och kör hela testsviten. Ett test som aldrig kan fela bevisar ingenting. |
| `e2e_final.mjs` | **Helkörning i webbläsare.** Registrerar en användare, lägger till prenumerationer, kontrollerar prognosens siffror mot en egen JS-beräkning, provar 3/6/12 månader, "Markera betald", serverfel och "Försök igen", felgränsen, mobil och utloggning. |
| `label_overlap_check.mjs` | Mäter om diagrammets värdeetiketter överlappar på 320–576 px, med vanliga och tunga belopp. |
| `harness/` | Kör det riktiga API:t över HTTP mot en SQLite-fil (ingen SQL Server behövs) så att webbläsare och skript kan nå det. |
| `seed_demo.py`, `api.py` | Skapar demokontot och delar hjälpfunktioner. |

## Förutsättningar

.NET 10 SDK, Node.js 22, Python 3.9+ och en Chromium (till exempel den som Playwright hämtar). Sökvägen till Chromium anges med
`CHROMIUM_PATH` (standard `/opt/pw-browsers/chromium-1194/chrome-linux/chrome`).

## Köra allt

Från repots rot, i separata terminaler:

```bash
# 1. API:t mot SQLite på http://localhost:5077
dotnet run --project docs/ai/verifiering/harness -- 5077

# 2. Frontend mot det API:t (CORS tillåter http://localhost:5173)
cd frontend && VITE_API_URL=http://localhost:5077 npx vite --host 127.0.0.1
```

Sedan, till exempel:

```bash
cd docs/ai/verifiering
python3 oracle_check.py            # 0 avvikelser förväntas
python3 size_check.py 1000         # 200 skapas, 800 avvisas med 409
python3 seed_demo.py               # demokontot för label_overlap_check
python3 mutation_check.py          # tar ca 10 minuter, kräver bara .NET

# webbläsarskripten behöver playwright-core: npm install --no-save playwright-core
node e2e_final.mjs
node label_overlap_check.mjs
```

`mutation_check.py` kopierar `backend/` till en tillfällig mapp och rör aldrig repot. Mutanter som inte kan ändra beteende
(*ekvivalenta*) är märkta i skriptet. Att de överlever är rätt och ingen lucka i testerna.

## Begränsningar

- Allt körs mot SQLite, inte SQL Server. Frågan använder bara enkla filter som översätts likadant, men den är inte körd mot en riktig SQL Server.
- Webbläsarkörningen använder en systemfont i headless Chromium. Den är bredare än typsnitten på riktiga telefoner, så etikettmätningarna är försiktiga.
- Skripten är skrivna för den här sessionen och har inte provats i andra miljöer än den.
