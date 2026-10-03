# Faza 6 – hosting i monitoring – plan implementacji

> **Dla wykonawców:** WYMAGANA UMIEJĘTNOŚĆ: superpowers:subagent-driven-development (zalecana) lub superpowers:executing-plans. Kroki używają składni checkboxów (`- [ ]`).

**Cel:** Dodać kontrolę gotowości (`/health/ready`), cykliczny uptime/keep-warm w GitHub Actions oraz Application Insights w backendzie – bez zmiany planów Azure i bez kosztów.

**Architektura:** Część A to kod w repo (endpoint health check, workflow `uptime.yml`). Część B to telemetria Application Insights włączana wyłącznie, gdy ustawiony jest `APPLICATIONINSIGHTS_CONNECTION_STRING` (dev/testy bez zmian), plus zasoby Azure tworzone po zgodzie użytkownika, plus runbook.

**Stos:** ASP.NET Core 8, `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`, `Microsoft.ApplicationInsights.AspNetCore`, xUnit + `WebApplicationFactory`, GitHub Actions, Azure CLI.

**Spec:** `docs/superpowers/specs/2026-10-03-hosting-monitoring-design.md`

## Ograniczenia globalne

- Koszt 0 zł; plany F1 / darmowa baza bez zmian.
- Rozgrzewanie nie może odpytywać bazy (`uptime.yml` woła wyłącznie `/health`).
- Każde polecenie `az` zmieniające konto Azure: najpierw pokazać użytkownikowi, uruchomić dopiero po zgodzie. Connection string nigdy do repo ani do czatu.
- Commity lokalne na `master`; stopka `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`; stage'ować tylko wskazane pliki (nigdy `backend/src/DokPortal.Api/appsettings.Development.json` ani `.claude/`).
- Push na `master` dopiero po wyraźnej zgodzie użytkownika.
- W bashu na Windows przed poleceniami `az` z ID zasobów: `export MSYS_NO_PATHCONV=1`.
- Komunikacja z użytkownikiem po polsku.

## Struktura plików

| Plik | Odpowiedzialność |
|---|---|
| `backend/src/DokPortal.Api/DokPortal.Api.csproj` | Pakiety: health checks EF, Application Insights |
| `backend/src/DokPortal.Api/Program.cs` | Rejestracja health checka, `/health/ready`, warunkowa telemetria AI |
| `backend/src/DokPortal.Api/Telemetry/HealthTelemetryFilter.cs` | `ITelemetryProcessor` odrzucający telemetrię żądań `/health*` |
| `backend/tests/DokPortal.Api.IntegrationTests/HealthCheckTests.cs` | Testy `/health` i `/health/ready` (200 i 503) |
| `backend/tests/DokPortal.Api.IntegrationTests/HealthTelemetryFilterTests.cs` | Testy jednostkowe filtra |
| `.github/workflows/uptime.yml` | Cykliczne wołanie `/health` z ponawianiem |
| `docs/operations.md` | Runbook |

---

### Task 1: Endpoint `GET /health/ready`

**Pliki:**
- Modyfikacja: `backend/src/DokPortal.Api/DokPortal.Api.csproj`
- Modyfikacja: `backend/src/DokPortal.Api/Program.cs`
- Test: `backend/tests/DokPortal.Api.IntegrationTests/HealthCheckTests.cs`

**Interfejsy:**
- Produkuje: `GET /health/ready` (anonimowy) → 200 `Healthy` gdy baza odpowiada, 503 `Unhealthy` w przeciwnym razie.

- [ ] **Krok 1: Przeczytaj istniejący test**

Otwórz `backend/tests/DokPortal.Api.IntegrationTests/HealthCheckTests.cs` i zachowaj jego styl (klasa, `IClassFixture<CustomWebApplicationFactory>` itp.).

- [ ] **Krok 2: Dopisz testy (czerwone)**

Dodaj w tym samym pliku (w przestrzeni nazw `DokPortal.Api.IntegrationTests`) metodę dla scenariusza zdrowego, w istniejącej klasie testowej:

```csharp
[Fact]
public async Task Ready_ReturnsOk_WhenDatabaseIsReachable()
{
    var client = _factory.CreateClient();

    var response = await client.GetAsync("/health/ready");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}
```

(Dostosuj nazwę pola fabryki do istniejącej klasy – jeśli to `_factory`, zostaw; w przeciwnym razie użyj tego, które klasa już ma. Dodaj `using System.Net;` jeśli go brak.)

oraz nową klasę na końcu pliku dla scenariusza awarii:

```csharp
public class HealthReadyUnhealthyTests : IClassFixture<HealthReadyUnhealthyTests.UnreachableDatabaseFactory>
{
    private readonly UnreachableDatabaseFactory _factory;

    public HealthReadyUnhealthyTests(UnreachableDatabaseFactory factory) => _factory = factory;

    [Fact]
    public async Task Ready_ReturnsServiceUnavailable_WhenDatabaseIsUnreachable()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Health_StillReturnsOk_WhenDatabaseIsUnreachable()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Standardowa fabryka, ale baza wskazuje na nieistniejący plik (tylko do odczytu).</summary>
    public class UnreachableDatabaseFactory : CustomWebApplicationFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services
                             .Where(d => d.ServiceType == typeof(Microsoft.EntityFrameworkCore.DbContextOptions<DokPortal.Infrastructure.Persistence.AppDbContext>))
                             .ToList())
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<DokPortal.Infrastructure.Persistence.AppDbContext>(options =>
                    options.UseSqlite("Data Source=/nieistniejacy-katalog-dokportal/x.db;Mode=ReadOnly"));
            });
        }
    }
}
```

Dodaj na górze pliku brakujące `using`: `Microsoft.EntityFrameworkCore;`, `Microsoft.Extensions.DependencyInjection;`.

- [ ] **Krok 3: Uruchom testy – mają się nie powieść**

```bash
cd backend && dotnet test tests/DokPortal.Api.IntegrationTests --filter "FullyQualifiedName~HealthCheckTests|FullyQualifiedName~HealthReadyUnhealthyTests"
```

Oczekiwane: `Ready_*` kończą się 404 (brak endpointu); `Health_StillReturnsOk_*` przechodzi.

- [ ] **Krok 4: Dodaj pakiet**

```bash
cd backend && dotnet add src/DokPortal.Api package Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore --version "8.*"
```

- [ ] **Krok 5: Zarejestruj health check i endpoint w `Program.cs`**

Dodaj `using Microsoft.AspNetCore.Diagnostics.HealthChecks;` do bloku `using`. Po rejestracji `AddDbContext<AppDbContext>` (linia z `UseSqlServer`) dodaj:

```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: new[] { "ready" });
```

Zaraz po `app.MapGet("/health", ...)` dodaj:

```csharp
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
```

- [ ] **Krok 6: Uruchom testy – mają przejść**

Ta sama komenda co w kroku 3. Oczekiwane: wszystkie 3 przechodzą. Jeśli `Ready_ReturnsServiceUnavailable_*` zwróci 200, sprawdź, czy opcje `AddDbContext` z fabryki bazowej nie zostały nadpisane (kolejność `ConfigureServices`) i popraw usuwanie deskryptorów.

- [ ] **Krok 7: Cały backend**

```bash
cd backend && dotnet test --configuration Release
```

Oczekiwane: wszystkie zielone.

- [ ] **Krok 8: Commit**

```bash
git add backend/src/DokPortal.Api/DokPortal.Api.csproj backend/src/DokPortal.Api/Program.cs backend/tests/DokPortal.Api.IntegrationTests/HealthCheckTests.cs
git commit -m "Dodaj endpoint /health/ready sprawdzający dostępność bazy

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Workflow `uptime.yml`

**Pliki:**
- Utwórz: `.github/workflows/uptime.yml`

**Interfejsy:**
- Konsumuje: `GET https://dokportal-api.azurewebsites.net/health` (200).

- [ ] **Krok 1: Utwórz workflow**

```yaml
name: Uptime

on:
  schedule:
    # Co 15 minut w dni robocze, ok. 7:00–21:00 czasu polskiego (UTC 5–19).
    # Utrzymuje aplikację F1 w gotowości i alarmuje mailem z GitHuba, gdy nie odpowiada.
    # Celowo woła tylko /health (bez bazy), żeby nie zużywać darmowego limitu bazy.
    - cron: "*/15 5-19 * * 1-5"
  workflow_dispatch:

jobs:
  check:
    runs-on: ubuntu-latest
    steps:
      - name: Sprawdź /health
        run: |
          url="https://dokportal-api.azurewebsites.net/health"
          max_attempts=5
          for attempt in $(seq 1 $max_attempts); do
            http_code=$(curl -s -o /dev/null --max-time 60 -w "%{http_code}" "$url") || http_code=000
            if [ "$http_code" = "200" ]; then
              echo "OK (próba $attempt)"
              exit 0
            fi
            echo "Próba $attempt/$max_attempts: kod $http_code"
            case "$http_code" in
              000|502|503|504) ;;
              *) echo "Błąd nieprzejściowy — bez ponawiania."; exit 1 ;;
            esac
            if [ "$attempt" -lt "$max_attempts" ]; then sleep 30; fi
          done
          echo "Aplikacja niedostępna po $max_attempts próbach."
          exit 1
```

- [ ] **Krok 2: Zwaliduj składnię YAML**

```bash
python -c "import yaml,sys; yaml.safe_load(open('.github/workflows/uptime.yml', encoding='utf-8')); print('ok')"
```

Oczekiwane: `ok`. (Jeśli brak modułu `yaml`: `pip install pyyaml`.)

- [ ] **Krok 3: Sprawdź logikę skryptu lokalnie**

Przetestuj pętlę na produkcyjnym adresie (to odczyt, bez skutków ubocznych), przez PowerShell, bo `curl` w bashu ma problem z proxy:

```powershell
(Invoke-WebRequest -Uri "https://dokportal-api.azurewebsites.net/health" -UseBasicParsing -TimeoutSec 60).StatusCode
```

Oczekiwane: `200`.

- [ ] **Krok 4: Commit**

```bash
git add .github/workflows/uptime.yml
git commit -m "Dodaj workflow uptime: cykliczne sprawdzanie /health i powiadomienie o awarii

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Telemetria Application Insights w backendzie

**Pliki:**
- Utwórz: `backend/src/DokPortal.Api/Telemetry/HealthTelemetryFilter.cs`
- Modyfikacja: `backend/src/DokPortal.Api/DokPortal.Api.csproj`
- Modyfikacja: `backend/src/DokPortal.Api/Program.cs`
- Test: `backend/tests/DokPortal.Api.IntegrationTests/HealthTelemetryFilterTests.cs`

**Interfejsy:**
- Produkuje: `DokPortal.Api.Telemetry.HealthTelemetryFilter(ITelemetryProcessor next)` – `Process(ITelemetry item)` przekazuje dalej wszystko poza `RequestTelemetry`, którego `Url.AbsolutePath` zaczyna się od `/health` (porównanie bez rozróżniania wielkości liter).

- [ ] **Krok 1: Dodaj pakiet**

```bash
cd backend && dotnet add src/DokPortal.Api package Microsoft.ApplicationInsights.AspNetCore --version "2.*"
```

- [ ] **Krok 2: Napisz testy filtra (czerwone)** – utwórz `backend/tests/DokPortal.Api.IntegrationTests/HealthTelemetryFilterTests.cs`

```csharp
using DokPortal.Api.Telemetry;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace DokPortal.Api.IntegrationTests;

public class HealthTelemetryFilterTests
{
    private class RecordingProcessor : ITelemetryProcessor
    {
        public List<ITelemetry> Received { get; } = new();
        public void Process(ITelemetry item) => Received.Add(item);
    }

    private static (HealthTelemetryFilter Filter, RecordingProcessor Next) Create()
    {
        var next = new RecordingProcessor();
        return (new HealthTelemetryFilter(next), next);
    }

    private static RequestTelemetry Request(string path) => new() { Url = new Uri("https://dokportal-api.azurewebsites.net" + path) };

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    [InlineData("/HEALTH")]
    public void Process_DropsHealthRequests(string path)
    {
        var (filter, next) = Create();

        filter.Process(Request(path));

        Assert.Empty(next.Received);
    }

    [Theory]
    [InlineData("/api/people")]
    [InlineData("/api/healthy-things")]
    public void Process_PassesOtherRequests(string path)
    {
        var (filter, next) = Create();
        var item = Request(path);

        filter.Process(item);

        Assert.Single(next.Received);
        Assert.Same(item, next.Received[0]);
    }

    [Fact]
    public void Process_PassesNonRequestTelemetryAndRequestsWithoutUrl()
    {
        var (filter, next) = Create();

        filter.Process(new TraceTelemetry("komunikat"));
        filter.Process(new RequestTelemetry());

        Assert.Equal(2, next.Received.Count);
    }
}
```

Uwaga: `/api/healthy-things` ma zostać przepuszczone – filtr dopasowuje dokładnie `/health` lub prefiks `/health/`, nie samo `/health…`.

- [ ] **Krok 3: Uruchom – ma się nie skompilować/nie przejść**

```bash
cd backend && dotnet test tests/DokPortal.Api.IntegrationTests --filter "FullyQualifiedName~HealthTelemetryFilterTests"
```

Oczekiwane: błąd kompilacji (brak `HealthTelemetryFilter`).

- [ ] **Krok 4: Zaimplementuj filtr** – utwórz `backend/src/DokPortal.Api/Telemetry/HealthTelemetryFilter.cs`

```csharp
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace DokPortal.Api.Telemetry;

/// <summary>Odrzuca telemetrię żądań health checków (/health, /health/ready), żeby nie zaśmiecała Application Insights.</summary>
public class HealthTelemetryFilter : ITelemetryProcessor
{
    private readonly ITelemetryProcessor _next;

    public HealthTelemetryFilter(ITelemetryProcessor next) => _next = next;

    public void Process(ITelemetry item)
    {
        if (item is RequestTelemetry { Url: not null } request && IsHealthPath(request.Url.AbsolutePath))
        {
            return;
        }

        _next.Process(item);
    }

    private static bool IsHealthPath(string path) =>
        path.Equals("/health", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/health/", StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Krok 5: Uruchom testy filtra – mają przejść**

Ta sama komenda co w kroku 3. Oczekiwane: wszystkie zielone.

- [ ] **Krok 6: Warunkowo włącz telemetrię w `Program.cs`**

Zaraz po `var builder = WebApplication.CreateBuilder(args);` dodaj:

```csharp
if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddApplicationInsightsTelemetry();
    builder.Services.AddApplicationInsightsTelemetryProcessor<DokPortal.Api.Telemetry.HealthTelemetryFilter>();
}
```

- [ ] **Krok 7: Cały backend**

```bash
cd backend && dotnet test --configuration Release
```

Oczekiwane: wszystkie zielone (w testach connection string nie jest ustawiony, więc telemetria się nie włącza).

- [ ] **Krok 8: Commit**

```bash
git add backend/src/DokPortal.Api/DokPortal.Api.csproj backend/src/DokPortal.Api/Program.cs backend/src/DokPortal.Api/Telemetry/HealthTelemetryFilter.cs backend/tests/DokPortal.Api.IntegrationTests/HealthTelemetryFilterTests.cs
git commit -m "Dodaj telemetrię Application Insights (włączaną przez connection string) i filtr health checków

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Zasoby Azure (wymaga zgody użytkownika na każde polecenie)

**Pliki:** brak zmian w repo (zmiany na koncie Azure).

**Interfejsy:**
- Produkuje: ustawienie `APPLICATIONINSIGHTS_CONNECTION_STRING` w App Service `dokportal-api`; zasoby `log-dokportal`, `appi-dokportal` w `rg-dokportal`.

Dla każdego kroku: pokaż użytkownikowi polecenie, poczekaj na „tak", uruchom. Ustaw `export MSYS_NO_PATHCONV=1`.

- [ ] **Krok 1: Sprawdź (tylko odczyt) obsługę regionu**

```bash
az provider show -n Microsoft.OperationalInsights --query "resourceTypes[?resourceType=='workspaces'].locations | [0]" -o tsv
az provider show -n Microsoft.Insights --query "resourceTypes[?resourceType=='components'].locations | [0]" -o tsv
```

Oczekiwane: lista zawiera `Poland Central`. Jeśli nie – zatrzymaj się i zapytaj użytkownika o region (np. `West Europe`).

- [ ] **Krok 2: Zarejestruj providery (zgoda!)**

```bash
az provider register -n Microsoft.OperationalInsights --wait
az provider register -n Microsoft.Insights --wait
```

Weryfikacja: `az provider show -n Microsoft.Insights --query registrationState -o tsv` → `Registered` (to samo dla `Microsoft.OperationalInsights`).

- [ ] **Krok 3: Utwórz Log Analytics z dziennym limitem (zgoda!)**

```bash
az monitor log-analytics workspace create -g rg-dokportal -n log-dokportal -l polandcentral --sku PerGB2018 --quota 0.15
```

- [ ] **Krok 4: Utwórz Application Insights (zgoda!)**

```bash
WS_ID=$(az monitor log-analytics workspace show -g rg-dokportal -n log-dokportal --query id -o tsv)
az monitor app-insights component create --app appi-dokportal -g rg-dokportal -l polandcentral --kind web --application-type web --workspace "$WS_ID"
```

Jeśli `az` zaproponuje instalację rozszerzenia `application-insights` – zaakceptuj (zgoda użytkownika wcześniej).

- [ ] **Krok 5: Ustaw connection string w aplikacji (zgoda! – restartuje aplikację)**

Wartość nie może pojawić się w czacie ani w repo:

```bash
CS=$(az monitor app-insights component show --app appi-dokportal -g rg-dokportal --query connectionString -o tsv)
az webapp config appsettings set -g rg-dokportal -n dokportal-api --settings "APPLICATIONINSIGHTS_CONNECTION_STRING=$CS" --query "[?name=='APPLICATIONINSIGHTS_CONNECTION_STRING'].name" -o tsv
```

Oczekiwane: wypisana tylko nazwa ustawienia.

- [ ] **Krok 6: Sprawdź, że aplikacja nadal działa**

```powershell
(Invoke-WebRequest -Uri "https://dokportal-api.azurewebsites.net/health" -UseBasicParsing -TimeoutSec 120).StatusCode
```

Oczekiwane: `200` (pierwsze wywołanie po restarcie może potrwać).

Brak commitu (zmiany poza repo).

---

### Task 5: Runbook `docs/operations.md`

**Pliki:**
- Utwórz: `docs/operations.md`

- [ ] **Krok 1: Utwórz plik**

````markdown
# Runbook – utrzymanie DOK Portal Light na Azure

Zasoby w grupie `rg-dokportal` (Poland Central): App Service `dokportal-api` (plan F1), Azure SQL `sql-dokportal` / `DokPortalLight` (oferta darmowa), Static Web App `dokportal-web`, Blob Storage `dokportalfiles`, Application Insights `appi-dokportal` + Log Analytics `log-dokportal`.

W bashu na Windows przed poleceniami z ID zasobów: `export MSYS_NO_PATHCONV=1`.

## Co oznaczają objawy

| Objaw | Przyczyna | Co robić |
|---|---|---|
| Strona API zwraca 403 „This web app is stopped” | Przekroczony dzienny limit 60 min CPU planu F1 (`QuotaExceeded`) | Poczekać do północy UTC (limit się zeruje) albo `az webapp start`; sprawdzić zużycie (niżej). |
| 503 po dłuższej przerwie | Zimny start F1 (aplikacja uśpiona) | Ponowić po ~30 s. Workflowy przypomnień i `uptime.yml` ponawiają same. |
| Pierwsze logowanie trwa ok. minuty | Darmowa baza wstrzymana po 60 min bezczynności | Poczekać; kolejne żądania są szybkie. |
| Wszystkie żądania do bazy zwracają błąd do początku miesiąca | Wyczerpany darmowy limit bazy (100 tys. vCore-s/mies.) | Sprawdzić zużycie w portalu (SQL → Free offer); rozważyć płatny plan. |
| Mail „Uptime: run failed” z GitHuba | `/health` nie odpowiedział 200 po 5 próbach | Sprawdzić stan aplikacji (niżej), potem Application Insights. |

## Polecenia diagnostyczne

Stan aplikacji:

```bash
az webapp show -g rg-dokportal -n dokportal-api --query "{state:state,availability:availabilityState}" -o json
```

Zużycie CPU i żądania z ostatnich 14 dni (CPU w sekundach, limit 3600/dzień):

```bash
RID=$(az webapp show -g rg-dokportal -n dokportal-api --query id -o tsv)
az monitor metrics list --resource "$RID" --metric CpuTime Requests Http5xx --interval P1D --offset 14d --aggregation Total -o table
```

Restart / start:

```bash
az webapp restart -g rg-dokportal -n dokportal-api
az webapp start -g rg-dokportal -n dokportal-api
```

Sprawdzenie bazy (budzi ją i zużywa darmowy limit – nie wołać automatycznie):

```bash
curl -s -o /dev/null -w "%{http_code}\n" https://dokportal-api.azurewebsites.net/health/ready
```

(Za proxy z przechwytywaniem TLS użyj PowerShell: `Invoke-WebRequest -UseBasicParsing`.)

## Application Insights

Portal Azure → `appi-dokportal`:
- **Failures** – wyjątki i nieudane żądania (z podglądem stosu),
- **Performance** – najwolniejsze operacje,
- **Logs** – zapytania KQL, np. `exceptions | order by timestamp desc | take 20`.

Telemetria żądań `/health*` jest odrzucana w aplikacji. Dzienny limit danych Log Analytics to ok. 0,15 GB (pilnuje darmowych 5 GB/mies.).

## Uptime i rozgrzewanie

Workflow `.github/workflows/uptime.yml` co 15 minut w dni robocze (UTC 5–19) woła `/health`. Gdy aplikacja nie odpowie po 5 próbach, run kończy się błędem i GitHub wysyła powiadomienie. Cron GitHuba bywa opóźniany, więc zimne starty są łagodzone, nie wyeliminowane. Workflow nie dotyka bazy.
````

- [ ] **Krok 2: Commit**

```bash
git add docs/operations.md
git commit -m "Dodaj runbook utrzymania aplikacji na Azure

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Wdrożenie i weryfikacja na produkcji (wymaga zgody na push)

**Pliki:** brak.

- [ ] **Krok 1: Pełna weryfikacja lokalna**

```bash
cd backend && dotnet test --configuration Release
```

Oczekiwane: wszystko zielone.

- [ ] **Krok 2: Zapytaj użytkownika o zgodę na push**

Wypisz commity do wypchnięcia (`git log origin/master..HEAD --oneline`) i poczekaj na „wypchnij".

- [ ] **Krok 3: Push i obserwacja wdrożenia**

```bash
git push origin master
gh run list --branch master --limit 6
```

Poczekaj na zakończenie `Deploy` i `Backend CI` (`gh run watch <id>` lub kolejne `gh run list`).

- [ ] **Krok 4: Weryfikacja na produkcji**

```powershell
(Invoke-WebRequest -Uri "https://dokportal-api.azurewebsites.net/health" -UseBasicParsing -TimeoutSec 120).StatusCode
(Invoke-WebRequest -Uri "https://dokportal-api.azurewebsites.net/health/ready" -UseBasicParsing -TimeoutSec 180).StatusCode
```

Oczekiwane: `200` i `200` (drugie może potrwać do ~1 min, jeśli baza jest wstrzymana).

- [ ] **Krok 5: Uruchom `uptime.yml` ręcznie**

```bash
gh workflow run uptime.yml
gh run list --workflow uptime.yml --limit 1
```

Oczekiwane: run kończy się sukcesem.

- [ ] **Krok 6: Telemetria w Application Insights**

Wywołaj kilka żądań (np. `Invoke-WebRequest` na `https://dokportal-api.azurewebsites.net/api/auth/login` z błędnymi danymi → 400/401), odczekaj 2–5 minut, następnie:

```bash
az monitor app-insights query --app appi-dokportal -g rg-dokportal --analytics-query "requests | summarize count() by name, resultCode" -o table
```

Oczekiwane: widoczne żądania do `/api/...`, brak wpisów dla `/health`. Jeśli pusto – poczekaj kilka minut (opóźnienie ingestii) i sprawdź `APPLICATIONINSIGHTS_CONNECTION_STRING` w ustawieniach aplikacji.

- [ ] **Krok 7: Zaktualizuj pamięć projektu**

Dopisz do `project_status.md` status Fazy 6 (health/ready, uptime.yml, Application Insights, runbook; aktualizacja wpisu Fazy 5 na „pushed 2026-10-03").

---

## Samokontrola względem specyfikacji

- Pkt 1 (`/health/ready`) → Task 1. Pkt 2 (`uptime.yml`) → Task 2. Pkt 3 (przypomnienia bez zmian) → brak zadania (celowo).
- Pkt 4 (zasoby Azure) → Task 4. Pkt 5 (backend AI + filtr) → Task 3. Pkt 6 (runbook) → Task 5.
- Testy: integracyjny `/health/ready` + filtr → Task 1, 3; weryfikacja po wdrożeniu → Task 6.
- Kolejność ze specyfikacji (A → B → wdrożenie po zgodzie) zachowana; Task 4 (Azure) można wykonać przed Task 6, aby telemetria działała od pierwszego wdrożenia.
