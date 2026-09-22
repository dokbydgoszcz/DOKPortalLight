# DOK Portal Light

CRM dla Szkoły Katechistów św. Pawła (SKŚP) i Diecezjalnego Ośrodka
Katechumenalnego (DOK) — wspólna baza osób, RBAC, oraz moduły
specyficzne dla obu instytucji. Zobacz specyfikację w
`docs/superpowers/specs/2026-09-22-foundation-design.md` oraz plan
implementacji w `docs/superpowers/plans/2026-09-22-foundation-implementation.md`.

## Wymagania

- .NET 8 SDK
- Node.js 20+ i npm
- SQL Server LocalDB (dev na Windows) lub dostęp do instancji Azure SQL
- `dotnet-ef` w wersji 8.x (`dotnet tool install --global dotnet-ef --version 8.*`)

## Backend — uruchomienie lokalne

```bash
cd backend
dotnet restore
dotnet run --project src/DokPortal.Api
```

Migracje bazy danych i dane startowe (role RBAC, konto administratora,
przykładowe parafie) stosują się automatycznie przy starcie w trybie
Development. Domyślne dane logowania administratora znajdują się w
`appsettings.Development.json` (sekcja `SeedAdmin`) — zmień hasło po
pierwszym uruchomieniu w środowisku innym niż lokalne.

Testy: `dotnet test backend/DokPortal.sln`

## Frontend — uruchomienie lokalne

```bash
cd frontend
npm install
npm start
```

Aplikacja domyślnie łączy się z `http://localhost:5227` (patrz
`src/environments/environment.development.ts`) — dopasuj do portu, na
którym faktycznie działa lokalne API.

Testy: `npx ng test` (Vitest + jsdom, bez potrzeby instalowania przeglądarki)

## Wdrożenie (GitHub Actions → Azure)

Ten projekt wdraża się automatycznie po pushu do `main`
(`.github/workflows/deploy.yml`), pod warunkiem że wcześniej:

1. Utworzysz w Azure: **App Service** (backend, .NET 8) oraz
   **Static Web App** (frontend) i, docelowo, **Azure SQL Database**.
2. Dodasz w ustawieniach repozytorium GitHub:
   - **Secrets**: `AZURE_WEBAPP_PUBLISH_PROFILE`,
     `AZURE_STATIC_WEB_APPS_API_TOKEN`.
   - **Variables**: `AZURE_WEBAPP_NAME` (nazwa App Service).
3. Skonfigurujesz w ustawieniach App Service (Configuration →
   Application settings) rzeczywiste wartości produkcyjne:
   `ConnectionStrings__Default`, `Jwt__Key`, `Jwt__Issuer`,
   `Jwt__Audience`, `SeedAdmin__Email`, `SeedAdmin__Password`,
   `Cors__AllowedOrigins__0` (adres URL Static Web App).
4. Zaktualizujesz `frontend/src/environments/environment.ts` (wariant
   produkcyjny) na rzeczywisty adres URL wdrożonego API przed buildem
   produkcyjnym.

Bez tych kroków `backend-ci.yml` i `frontend-ci.yml` (build + testy)
nadal działają na każdy push/PR — tylko `deploy.yml` pozostanie
nieaktywny / zakończy się błędem do czasu skonfigurowania sekretów.

## Domyślne konto administratora (środowisko deweloperskie)

- E-mail: `admin@dokportal.local`
- Hasło: `ZmienMnie!123`

Zmień te wartości w `backend/src/DokPortal.Api/appsettings.Development.json`
przed udostępnieniem środowiska komukolwiek poza lokalnym deweloperem.
