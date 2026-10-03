# Faza 6 – hosting i monitoring (budżet 0 zł) – projekt

Data: 2026-10-03

## Cel

Zmniejszyć skutki zimnych startów darmowego App Service (F1), szybko dowiadywać się o awarii oraz widzieć wyjątki i wolne żądania backendu – **bez zmiany planów Azure i bez kosztów**.

## Stan wyjściowy (zmierzony 2026-10-03)

- App Service `dokportal-api` na planie F1 (Poland Central), `alwaysOn` wyłączone.
- Azure SQL `DokPortalLight` w ofercie darmowej (`useFreeLimit`), autowstrzymanie po 60 min, limit 100 tys. vCore-sekund/mies.
- Zużycie CPU F1: 73–294 s dziennie przy limicie 3600 s (2–8%). Incydent `QuotaExceeded` był jednorazowy.
- Błędy 5xx: 1–9 dziennie – zimne starty.
- Repozytorium jest publiczne (minuty GitHub Actions bez limitu).
- `Microsoft.Insights` i `Microsoft.OperationalInsights` są w subskrypcji `NotRegistered`.
- Istnieje `GET /health` (tylko aplikacja, bez bazy). Brak Application Insights.

## Ograniczenia

- Koszt 0 zł; plany F1 / darmowa baza bez zmian.
- Rozgrzewanie **nie może** odpytywać bazy (zjadłoby darmowy limit vCore).
- Każda zmiana na koncie Azure (rejestracja providerów, tworzenie zasobów, ustawienia aplikacji) wymaga pokazania polecenia i zgody użytkownika. Connection string nie trafia do repozytorium.
- Push na `master` dopiero po zgodzie użytkownika.

## Część A – w repozytorium

1. **`GET /health/ready`** – `AddHealthChecks().AddDbContextCheck<AppDbContext>("database", tags: ["ready"])` + `MapHealthChecks("/health/ready", predicate: tag "ready")`. Anonimowy, bez danych w odpowiedzi; 200 `Healthy` lub 503 `Unhealthy`. Istniejący `/health` bez zmian. Wołany tylko ręcznie.
2. **`.github/workflows/uptime.yml`** – `schedule: "*/15 5-19 * * 1-5"` (UTC) + `workflow_dispatch`. Woła `https://dokportal-api.azurewebsites.net/health` z ponawianiem (5 prób, 30 s przerwy, ponawianie na 000/502/503/504 – tak jak w workflowach przypomnień). Po wyczerpaniu prób job kończy się błędem, a GitHub wysyła powiadomienie e-mail. Zastrzeżenie: cron GitHuba jest „best effort" i bywa opóźniany, więc to łagodzi zimne starty, ale ich nie gwarantuje usunąć.
3. Workflowy przypomnień bez zmian (mają już ponawianie).

## Część B – Azure + backend

4. **Zasoby Azure** (`rg-dokportal`, Poland Central): rejestracja providerów `Microsoft.Insights` i `Microsoft.OperationalInsights`; Log Analytics `log-dokportal` (PerGB2018, dzienny limit ~0,15 GB, retencja domyślna); Application Insights `appi-dokportal` (workspace-based). Ustawienie `APPLICATIONINSIGHTS_CONNECTION_STRING` w App Service.
5. **Backend:** pakiet `Microsoft.ApplicationInsights.AspNetCore`; `AddApplicationInsightsTelemetry()` tylko gdy connection string jest ustawiony (dev i testy bez zmian). `ITelemetryProcessor` odrzuca telemetrię żądań do ścieżek `/health*`.
6. **Runbook `docs/operations.md`:** objawy i reakcje (403 „stopped" = QuotaExceeded, 503 = zimny start, baza wstrzymana), polecenia `az` do sprawdzenia metryk CPU/żądań i restartu aplikacji, gdzie w portalu szukać wyjątków (Failures, Logs), jak działa alert z `uptime.yml`.

## Testy i weryfikacja

- Integracyjny: `/health/ready` zwraca 200 na SQLite w fabryce testowej; `/health` nadal 200.
- Jednostkowy: filtr telemetrii odrzuca `/health` i `/health/ready`, przepuszcza `/api/...`.
- Po wdrożeniu: `/health/ready` = 200 na produkcji; pierwsze żądania widoczne w Application Insights; ręczne uruchomienie `uptime.yml` kończy się sukcesem.

## Poza zakresem

Alerty metryczne Azure (możliwe drobne opłaty; rolę alertu pełni mail z `uptime.yml`), przenoszenie zadań w tle do aplikacji (na F1 aplikacja śpi), zmiana planów Azure, wpis w „Co nowego" (brak zmian widocznych dla testerów).

## Kolejność

Część A (kod + workflow + testy) → część B (kod z AI + runbook; zasoby Azure po zgodzie) → wdrożenie po zgodzie na push → weryfikacja na produkcji.
