# SubTracker – prenumerationsöversikt

Fullstack-app där användaren registrerar sina digitala prenumerationer (kostnad, kategori, betalningsintervall) och får en dashboard med bl.a. total månadskostnad och kostnad per kategori.

| Del | Teknik | I Azure |
|---|---|---|
| Backend | ASP.NET Core Web API (.NET 10, controllers), EF Core Code First, ASP.NET Core Identity | App Service (Linux, Free F1) |
| Databas | SQL Server – LocalDB lokalt | Azure SQL Database (gratiserbjudandet) |
| Frontend | React (Vite, JavaScript), React-Bootstrap, React Router, Axios, Recharts | App Service (Linux, Free F1, samma plan som API:t) – Static Web Apps var inte tillåtet i studentkontots regioner |
| CI/CD | GitHub Actions – ett workflow för backend och ett för frontend | |

## Inlämning

- GitHub: <https://github.com/HappyHamster135/Fullstack-App-Azure>
- Frontend: <https://web-subtracker-jw-czaqekaghchec7fz.swedencentral-01.azurewebsites.net>
- API (Scalar): <https://app-subtracker-jw-cxgwdgd5h5bnd8f6.swedencentral-01.azurewebsites.net/scalar>

## Arkitektur

```text
Webbläsare ──► Azure App Service (React, pm2 --spa)
    │
    └── HTTPS-anrop (CORS) ──► Azure App Service (API) ──► Azure SQL Database
```

```text
├── backend/SubTracker.Api/
│   ├── Controllers/   tar emot HTTP-anrop – tunna, ingen affärslogik
│   ├── Services/      affärslogik bakom interfaces (ISubscriptionService, IAuthService …)
│   ├── Dtos/          request/response per resurs (entiteter skickas aldrig direkt)
│   ├── Entities/      Category, Subscription, Payment, AppUser
│   ├── Mappings/      entitet ↔ DTO
│   ├── Common/        ServiceResult/ServiceError – resultat från services
│   ├── Auth/          JWT-inställningar, token-validering, svenska Identity-fel
│   ├── OpenApi/       JWT-stöd i Scalar
│   ├── Data/          AppDbContext + Configurations/ (relationer och index per entitet)
│   ├── Migrations/    EF Core-migrationer
│   └── Program.cs     databas, Identity, autentisering, CORS, OpenAPI/Scalar
├── frontend/
│   ├── public/staticwebapp.config.json   React Router-stöd om frontend flyttas till Static Web Apps
│   └── src/
│       ├── api/         axios-klient och API-klasser (CrudApi → SubscriptionApi, CategoryApi …)
│       ├── auth/        AuthProvider (inloggning), useAuth, ProtectedRoute, GuestRoute
│       ├── store/       SubscriptionProvider – appens data med useReducer + Context
│       ├── components/  återanvändbara komponenter, charts/ med dashboardens diagram
│       ├── hooks/       useForm, useConfirmDialog
│       ├── pages/       en komponent per sida
│       └── utils/       validering och formatering
└── .github/workflows/  backend.yml, frontend.yml
```

## Datamodell

```text
AppUser 1 ──── * Category 1 ──── * Subscription 1 ──── * Payment
   │                                   *
   └───────────────── 1 ───────────────┘
```

| Entitet | Innehåll |
|---|---|
| `Category` | Namn och färg. Varje användare har egna kategorier och får sex standardkategorier vid registrering. |
| `Subscription` | Namn, pris, betalningsintervall (vecka/månad/kvartal/år), startdatum, nästa betalning, aktiv, anteckning. Räknar själv ut `MonthlyCost` och flyttar fram nästa betalning via `RegisterPayment`. |
| `Payment` | Belopp och datum – betalningshistorik per prenumeration. |

- **Index:** `(UserId, NextPaymentDate)` på `Subscriptions`, eftersom listan filtreras på användare och sorteras på nästa betalning. Unikt index `(UserId, Name)` på `Categories`, så att en användare inte kan ha två kategorier med samma namn.
- **Borttagning:** en kategori som används kan inte tas bort (`Restrict`, API:t svarar 409). Tas en prenumeration bort försvinner dess betalningar (`Cascade`).
- **Åtkomst:** varje fråga filtrerar på den inloggade användarens id. Försöker någon nå en annan användares data svarar API:t 404, så att det inte ens avslöjas att datan finns.

## Kom igång lokalt

Krav: .NET 10 SDK, Node.js 22+, SQL Server LocalDB (följer med Visual Studio) och `dotnet-ef` (`dotnet tool install --global dotnet-ef`).

**1. Secrets** – sparas med user-secrets, alltså utanför repot (bara första gången per dator). Connection string:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\MSSQLLocalDB;Database=SubTrackerDb;Trusted_Connection=True" --project backend/SubTracker.Api
```

JWT-nyckel (slumpas fram av Node):

```bash
dotnet user-secrets set "Jwt:Key" "$(node -e "console.log(require('crypto').randomBytes(64).toString('base64'))")" --project backend/SubTracker.Api
```

**2. API:t** – databasen skapas och migrationerna körs automatiskt vid start:

```bash
dotnet run --project backend/SubTracker.Api
```

Scalar (API-dokumentation): <https://localhost:7213/scalar>

**3. Frontend** – i en ny terminal:

```bash
cd frontend
npm install
npm run dev
```

Öppna <http://localhost:5173>. Startsidan visar om API:t och databasen svarar.

### Ändra datamodellen

```bash
dotnet ef migrations add <NamnPåÄndringen> --project backend/SubTracker.Api
```

Migrationen körs automatiskt nästa gång API:t startar – både lokalt och i Azure.

## Konfiguration och secrets

Inga lösenord, nycklar eller connection strings finns i repot. Lokal konfiguration och produktionskonfiguration hålls isär:

| Inställning | Lokalt | Produktion |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | user-secrets | App Service → Environment variables → Connection strings |
| `Jwt:Key` | user-secrets | App Service → App setting `Jwt__Key` (en egen nyckel, inte samma som lokalt) |
| `Cors:AllowedOrigins` | `appsettings.Development.json` (`http://localhost:5173`) | App Service → App setting `Cors__AllowedOrigins__0` |
| `VITE_API_URL` | `frontend/.env.development` | GitHub-variabel, byggs in av `frontend.yml` |
| Deploy av API:t | – | GitHub secret `AZURE_WEBAPP_PUBLISH_PROFILE` |
| Deploy av frontend | – | GitHub secret `AZURE_STATIC_WEB_APPS_API_TOKEN` (eller `AZURE_FRONTEND_PUBLISH_PROFILE` + variabeln `AZURE_FRONTEND_WEBAPP_NAME` om frontend körs i App Service) |

`appsettings.json` innehåller bara ofarliga standardvärden. Allt som börjar med `VITE_` hamnar i webbläsaren och får därför aldrig vara hemligt.

## Inloggning och säkerhet

- **ASP.NET Core Identity** lagrar användare och hashar lösenord. Kontot låses i 5 minuter efter 5 misslyckade inloggningar.
- **JWT (Bearer)** används eftersom frontend och API ligger på olika domäner i Azure, där cookies blockeras av många webbläsare. Token signeras med `Jwt:Key` och gäller i 60 minuter.
- **Utloggning** byter användarens security stamp i Identity. Varje token innehåller stampen och kontrolleras vid varje anrop, så gamla tokens slutar fungera direkt.
- **Skyddade routes** finns i båda lagren: `[Authorize]` i API:t och `ProtectedRoute` i React, som skickar utloggade användare till `/login`.
- **Validering** sker i båda lagren: DataAnnotations på DTO:erna och Identitys lösenordsregler i API:t, samma regler i frontend för snabb återkoppling.

| Endpoint | Kräver inloggning | Svar |
|---|---|---|
| `POST /api/auth/register` | Nej | 201, 400, 409 |
| `POST /api/auth/login` | Nej | 200, 400, 401 |
| `POST /api/auth/logout` | Ja | 204, 401 |
| `GET /api/auth/me` | Ja | 200, 401 |

## API

Alla endpoints nedan kräver inloggning (401 utan token) och rör bara den inloggade användarens data.

| Endpoint | Beskrivning | Svar |
|---|---|---|
| `GET /api/subscriptions` | Alla prenumerationer, sorterade på nästa betalning | 200 |
| `GET /api/subscriptions/{id}` | En prenumeration | 200, 404 |
| `POST /api/subscriptions` | Skapa. Högst 200 prenumerationer per användare. Start- och betalningsdatum måste vara mellan 2000-01-01 och 2100-12-31. | 201, 400, 409 |
| `PUT /api/subscriptions/{id}` | Uppdatera (samma datumgränser) | 200, 400, 404 |
| `DELETE /api/subscriptions/{id}` | Ta bort (inklusive betalningar) | 204, 404 |
| `GET /api/subscriptions/{id}/payments` | Betalningshistorik | 200, 404 |
| `POST /api/subscriptions/{id}/payments` | Registrera betalning – flyttar fram nästa betalningsdatum. Tom body `{}` ger pris och dagens datum. | 201, 400, 404 |
| `DELETE /api/subscriptions/{id}/payments/{paymentId}` | Ta bort betalning | 204, 404 |
| `GET /api/categories` | Alla kategorier | 200 |
| `GET /api/categories/{id}` | En kategori | 200, 404 |
| `POST /api/categories` | Skapa | 201, 400, 409 |
| `PUT /api/categories/{id}` | Uppdatera | 200, 400, 404, 409 |
| `DELETE /api/categories/{id}` | Ta bort – nekas om kategorin används | 204, 404, 409 |
| `GET /api/dashboard` | Sammanställning: total kostnad per månad/år, kostnad per kategori, betalningar inom 30 dagar och betalt per månad (senaste sex) | 200 |
| `GET /api/forecast?months=N&includePayments=true` | Prognos över kommande betalningar för aktiva prenumerationer: total för perioden samt per månad summa och kostnad per kategori. `N` är 1–24 hela kalendermånader från och med innevarande månad (standard 6). Datumen räknas från nästa betalningsdatum och intervall, och bara betalningar från och med idag ingår (förfallna ingår inte). Med `includePayments=true` följer även varje enskild betalning (prenumeration, belopp, datum, kategori) med. Listan är avstängd som standard eftersom den är nästan hela svaret och webbappen inte använder den. "Idag" räknas i svensk tid. | 200, 400 |

## Tester

```bash
dotnet test
```

`backend/SubTracker.Api.Tests` (xUnit) startar hela API:t i minnet med `WebApplicationFactory` och byter SQL Server mot SQLite i minnet, så inga databasserver behövs. Testerna gör riktiga HTTP-anrop genom JWT, controllers, services och EF Core.

- SQL Server-migrationerna hoppas över i testerna (`Database:MigrateOnStartup=false`, standard är `true`). Schemat skapas av EF Core från modellen.
- `TestClock` ersätter klockan och går i svensk tid, så att månadsskiften, skottår, sommartid och tiden runt midnatt testas deterministiskt.
- Datumlogiken testas mot en referens med egen kalenderaritmetik. En oberoende Python-kontroll och körningen i webbläsare finns i `docs/ai/verifiering/`.

Frontend har Vitest och Testing Library (`cd frontend && npm test`). Testerna täcker prognoskortets laddning, felläge med "Försök igen", avbrutna anrop när perioden byts, felgränsen och valideringsreglerna för datum. Diagrammet testas i webbläsare, eftersom jsdom saknar storlekar för Recharts.

## CI/CD

- **`backend.yml`** – vid ändringar i `backend/`: restore → build → **test** → publish → deploy till App Service. Ett rött test stoppar jobbet, så deploy körs aldrig med trasig kod.
- **`frontend.yml`** – vid ändringar i `frontend/`: `npm ci` → lint → **test** → build → deploy till Static Web Apps (eller App Service, om variabeln `AZURE_FRONTEND_WEBAPP_NAME` är satt).

Pull requests byggs men deployas inte. Deploy-stegen hoppas över tills GitHub-variablerna `AZURE_WEBAPP_NAME` och `VITE_API_URL` är satta.

## Kända begränsningar

Sådant som är kartlagt men medvetet inte åtgärdat. Skälen finns i `docs/ai/`.

- **`RegisterPayment` flyttar fram datumet från föregående datum.** En betalning den 31:a blir den 28:e efter en februari och förblir det. Prognosen räknar rätt inom sig, men utgår från det lagrade datumet. Rätt åtgärd är att lagra betalningsdagen, vilket kräver en migration.
- **Icke-numeriska frågeparametrar** (t.ex. `months=abc`) ger ASP.NET Cores engelska standardtext. Det är global modellbindning som alla endpoints delar.
- **Ingen rate limiting, svarskomprimering eller `CancellationToken`** i API:t. Resursförbrukningen begränsas i stället av taket på 200 prenumerationer per användare och av att betalningslistan i prognosen är avstängd som standard.
- **Testerna körs mot SQLite.** Prognosfrågan använder bara enkla filter (användare, aktiv, datum) som översätts likadant till SQL Server, men den är inte körd mot en riktig SQL Server i testerna.
- **Svensk tid förutsätter tidszonsdatabasen.** Saknas `Europe/Stockholm` på servern räknas datum i UTC och en varning loggas vid start.
