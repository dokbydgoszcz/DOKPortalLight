# Runbook – utrzymanie DOK Portal Light na Azure

Zasoby w grupie `rg-dokportal` (Poland Central): App Service `dokportal-api` (plan F1), Azure SQL `sql-dokportal` / `DokPortalLight` (oferta darmowa), Static Web App `dokportal-web`, Blob Storage `dokportalfiles`, Application Insights `appi-dokportal` + Log Analytics `log-dokportal`.

W bashu na Windows przed poleceniami z ID zasobów: `export MSYS_NO_PATHCONV=1`.

## Co oznaczają objawy

| Objaw | Przyczyna | Co robić |
|---|---|---|
| Strona API zwraca 403 „This web app is stopped” | Przekroczony dzienny limit 60 min CPU planu F1 (`QuotaExceeded`) | Poczekać do północy UTC (limit się zeruje) albo `az webapp start`; sprawdzić zużycie (niżej). |
| 503 po dłuższej przerwie | Zimny start F1 (aplikacja uśpiona) | Ponowić po ~30 s. Workflowy przypomnień i `uptime.yml` ponawiają same. |
| Pierwsze logowanie trwa ok. minuty | Darmowa baza wstrzymana po 60 min bezczynności | Poczekać; kolejne żądania są szybkie. |
| Żądania do bazy zwracają błąd do początku miesiąca | Wyczerpany darmowy limit bazy (100 tys. vCore-s/mies.) | Sprawdzić zużycie w portalu (SQL → Free offer); rozważyć płatny plan. |
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

Za proxy z przechwytywaniem TLS `curl` może zgłaszać błąd SSL – użyj PowerShell: `(Invoke-WebRequest -UseBasicParsing <adres>).StatusCode`.

## Application Insights

Telemetria włącza się w aplikacji tylko wtedy, gdy w ustawieniach App Service jest `APPLICATIONINSIGHTS_CONNECTION_STRING` (wartość: `az monitor app-insights component show --app appi-dokportal -g rg-dokportal --query connectionString -o tsv`; nie umieszczać w repo).

Portal Azure → `appi-dokportal`:
- **Failures** – wyjątki i nieudane żądania (z podglądem stosu),
- **Performance** – najwolniejsze operacje,
- **Logs** – zapytania KQL, np. `exceptions | order by timestamp desc | take 20`.

Telemetria żądań `/health*` jest odrzucana w aplikacji. Dzienny limit danych Log Analytics to ok. 0,15 GB (pilnuje darmowych 5 GB/mies.).

## Uptime i rozgrzewanie

Workflow `.github/workflows/uptime.yml` co 15 minut w dni robocze (UTC 5–19) woła `/health`. Gdy aplikacja nie odpowie po 5 próbach, run kończy się błędem i GitHub wysyła powiadomienie. Cron GitHuba bywa opóźniany, więc zimne starty są łagodzone, nie wyeliminowane. Workflow nie dotyka bazy.
