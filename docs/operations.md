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
| Katechista widzi puste listy (sprawy, spotkania, dokumenty) | Konto nie jest powiązane z osobą (`AppUser.PersonId`), a sprawy DOK są ograniczone do podopiecznych przypisanych do jego osoby | Administrator: ekran Użytkownicy → kolumna „Osoba” → „Powiąż” (ta sama osoba, którą wskazano jako katechistę w sprawie). Jedna osoba = jedno konto; konto można też odpiąć lub zmienić osobę. Rola bez uprawnienia „Podgląd wszystkich spraw DOK” widzi tylko swoich podopiecznych. |
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

Zapytania KQL z `az monitor app-insights query` mogą być blokowane przez firmowy proxy z przechwytywaniem TLS (host `api.applicationinsights.io`). Wtedy użyj portalu (Logs) albo metryk przez ARM: `az monitor metrics list --resource <id appi-dokportal> --metric requests/count --filter "request/resultCode eq '*'"`.

Telemetria żądań `/health*` jest odrzucana w aplikacji. Dzienny limit danych Log Analytics to ok. 0,15 GB (pilnuje darmowych 5 GB/mies.).

## Uptime i rozgrzewanie

Workflow `.github/workflows/uptime.yml` co 15 minut w dni robocze (UTC 5–19) woła `/health`. Gdy aplikacja nie odpowie po 5 próbach, run kończy się błędem i GitHub wysyła powiadomienie. Cron GitHuba bywa opóźniany, więc zimne starty są łagodzone, nie wyeliminowane. Workflow nie dotyka bazy.

## Poczta wychodząca (SMTP)

Mailing i przypomnienia wysyłają e-maile przez SMTP, gdy w ustawieniach App Service (`dokportal-api`) jest `Smtp__Host`. Bez niego aplikacja zgłasza czytelny błąd „nie jest skonfigurowane”, a reszta działa.

### Wariant A (obecnie, ustawiony 2026-10-12): Gmail `dokbydgoszcz@gmail.com`

Działa od razu, bez udziału administratora Microsoft 365. Wymaga **hasła aplikacji** (zwykłe hasło do Gmaila nie zadziała przez SMTP).

1. Konto Google → Bezpieczeństwo → włącz **weryfikację dwuetapową** (jeśli jeszcze nie jest włączona).
2. Konto Google → Bezpieczeństwo → **Hasła aplikacji** → utwórz hasło (np. nazwa „DOK Portal”) → skopiuj 16 znaków.
3. W App Service `dokportal-api` ustaw (hasło wpisuje tylko administrator, nie trafia do repozytorium ani czatu):

| Ustawienie | Wartość |
|---|---|
| `Smtp__Host` | `smtp.gmail.com` |
| `Smtp__Port` | `587` |
| `Smtp__EnableSsl` | `true` (STARTTLS) |
| `Smtp__Username` | `dokbydgoszcz@gmail.com` |
| `Smtp__FromEmail` | `dokbydgoszcz@gmail.com` |
| `Smtp__FromName` | `DOK Portal` |
| `Smtp__Password` | hasło aplikacji (16 znaków, bez spacji) |

4. Po restarcie: Mailing → „Wyślij wiadomość testową”.

Ograniczenia Gmaila: ok. 500 wiadomości na dobę dla zwykłego konta (Google Workspace: ok. 2000). Kampanie idą przez kolejkę (`MailingDeliveries`): po „Wyślij” pierwsza wiadomość wychodzi od razu (błąd ustawień poczty widać przy kliknięciu), reszta w tle, co sekundę, najwyżej `Mailing:DailyLimit` (domyślnie 400) wiadomości kampanii na 24 godziny – zapas na przypomnienia i hasła. Przerwaną wysyłkę (uśpienie F1, limit dobowy) wznawia start aplikacji i codzienny workflow `mailing-queue.yml` (6:30 UTC, `POST /api/reminders/mailing-queue/run`). Adres, który trzy razy z rzędu zwróci błąd, liczy się jako nieudany. Każda wiadomość kampanii ma stopkę z linkiem „wypisz się” (`/wypisz/<klucz>`; wypisanie wymaga kliknięcia przycisku), a osoby wypisane są pomijane – można to też zaznaczyć w formularzu osoby. Ustawienia: `Mailing__DailyLimit`, `Mailing__DelaySeconds` (Workspace: limit można podnieść do ok. 1800); nadawca zawsze będzie `dokbydgoszcz@gmail.com` (Gmail podmienia inny adres „Od”); odpowiedzi odbiorców lądują w tej skrzynce.

### Wariant B: `noreply@bydgoskateologia.pl` (Exchange Online / Microsoft 365)

Wymaga włączenia SMTP AUTH przez administratora Microsoft 365 (patrz niżej).

Konto `noreply@bydgoskateologia.pl` (Exchange Online / Microsoft 365):

| Ustawienie | Wartość |
|---|---|
| `Smtp__Host` | `smtp.office365.com` |
| `Smtp__Port` | `587` |
| `Smtp__EnableSsl` | `true` (STARTTLS) |
| `Smtp__Username` | `noreply@bydgoskateologia.pl` |
| `Smtp__FromEmail` | `noreply@bydgoskateologia.pl` |
| `Smtp__FromName` | `DOK Portal` |
| `Smtp__Password` | hasło konta – ustawia tylko administrator, nie trafia do repozytorium ani do czatu |

Wymagania po stronie Exchange: włączony **uwierzytelniony SMTP (SMTP AUTH)** dla tej skrzynki, brak wymuszonego MFA na koncie technicznym (logowanie hasłem), nadawca zgodny ze skrzynką. Microsoft stopniowo wycofuje Basic Auth dla SMTP AUTH – gdy przestanie działać, trzeba przejść na wysyłkę przez Microsoft Graph.

Sprawdzenie: w ekranie **Mailing** przycisk „Wyślij wiadomość testową” wysyła wiadomość na adres zalogowanego użytkownika; przy błędzie pokazuje komunikat serwera poczty (np. `5.7.57 Client not authenticated` = SMTP AUTH wyłączony albo złe hasło).

## Wdrożenie i bramka testów

Push do `master` uruchamia:
- `deploy.yml` – najpierw `test-backend` (`dotnet test`), dopiero po zielonych testach `deploy-backend` (publikacja do `dokportal-api`). Czerwone testy = brak wdrożenia backendu.
- `azure-static-web-apps-…yml` – najpierw `test_frontend` (`ng test --coverage` z progami pokrycia), potem budowa i wdrożenie frontendu.

Migracje bazy wykonują się przy starcie API (do 5 prób co 15–60 s, gdy darmowa baza się budzi). Połączenie z bazą ma 60 s na nawiązanie i ponawia przejściowe błędy (do 6 razy).

Uwaga na migracje danych: część z nich zmienia dane nieodwracalnie (`Down` nie przywraca stanu sprzed migracji), m.in. `ResetDokCaseStages`, `RemoveMissionGrantedPlace`, `ManualFormationYears`, `LinkMissionsToParishesAndAssignments` (porządkuje dane po usuniętych osobach i parafiach). Przed wdrożeniem takiej migracji sprawdź, do kiedy da się odtworzyć bazę (niżej).

## Kopie zapasowe i odtwarzanie

### Baza (Azure SQL `sql-dokportal` / `DokPortalLight`)

Azure SQL robi kopie automatycznie (pełne, różnicowe i dziennika) i pozwala odtworzyć bazę na wybrany moment (PITR) w okresie przechowywania. Ile dni wstecz można cofnąć – sprawdź (polecenie tylko czyta):

```bash
az sql db show -g rg-dokportal -s sql-dokportal -n DokPortalLight --query "{najwczesniej:earliestRestoreDate, oferta:freeLimitExhaustionBehavior}" -o json
az sql db str-policy show -g rg-dokportal -s sql-dokportal -n DokPortalLight -o json
```

Odtworzenie **zawsze tworzy nową bazę** obok obecnej (obecna zostaje nietknięta). Nowa baza zwykle **nie jest objęta darmową ofertą** – to koszt, więc decyzja należy do właściciela. Wzór (najpierw pokaż polecenie i uzyskaj zgodę):

```bash
az sql db restore -g rg-dokportal -s sql-dokportal -n DokPortalLight \
  --dest-name DokPortalLight-odtworzona --time "2026-10-10T08:00:00Z"
```

Po sprawdzeniu danych w odtworzonej bazie można: (a) skopiować z niej tylko brakujące rekordy, albo (b) przełączyć aplikację na odtworzoną bazę (zmiana connection stringa w App Service – robi właściciel, to sekret), a starą usunąć.

Kopia „na półkę” (plik `.bacpac`, np. przed dużą zmianą) – eksport do Blob Storage wymaga loginu i hasła administratora SQL, więc wykonuje go właściciel (portal: baza → **Export**, albo `az sql db export`). Plik trzymaj w kontenerze innym niż pliki aplikacji.

### Pliki (Blob Storage `dokportalfiles`)

Dokumenty spraw, załączniki, pisma i zasoby leżą w Blob Storage. Sprawdź, czy jest włączone miękkie usuwanie (odzyskanie skasowanego pliku):

```bash
az storage account blob-service-properties show --account-name dokportalfiles -g rg-dokportal \
  --query "{usuwanie:deleteRetentionPolicy, kontenery:containerDeleteRetentionPolicy, wersje:isVersioningEnabled}" -o json
```

Zalecenie (zmienia konfigurację konta – tylko za zgodą właściciela; przechowywane usunięte pliki zajmują miejsce):

```bash
az storage account blob-service-properties update --account-name dokportalfiles -g rg-dokportal \
  --enable-delete-retention true --delete-retention-days 14 \
  --enable-container-delete-retention true --container-delete-retention-days 14
```

Odzyskanie usuniętego pliku: portal → konto `dokportalfiles` → kontener → „Pokaż usunięte obiekty” → **Cofnij usunięcie**.

### Próba odtworzenia

Raz na kwartał warto sprawdzić, że odtworzenie działa: `earliestRestoreDate` jest sprzed co najmniej kilku dni, a w Blob Storage widać miękko usunięte pliki. Pełne odtworzenie bazy robić tylko, gdy właściciel zgodzi się na koszt.

## Ochrona dostępu

- **Logowanie**: najwyżej 10 prób na minutę z jednego adresu (potem 429 „Zbyt wiele prób…”), a po 5 błędnych hasłach konto jest blokowane na 15 minut. `/health/ready` (budzi bazę) – najwyżej 6 wywołań na minutę. Limity zmienisz ustawieniami `RateLimiting__LoginPerMinute` i `RateLimiting__HealthReadyPerMinute`.
- **Sesje**: przy każdym żądaniu API sprawdza (z pamięcią 60 s), czy konto istnieje, nie jest wyłączone i czy token pasuje do jego znacznika bezpieczeństwa. Wyłączenie konta, zmiana ról albo hasła kończy otwarte sesje tego konta.
- **Odejście katechisty**: Użytkownicy i role → „Wyłącz konto”. Jego sprawy DOK przekaż innemu katechiście (Podopieczni DOK → Edytuj → Katechista prowadzący); nowy katechista dostanie e-mail.
- **Dziennik zmian**: zapisuje m.in. tworzenie, zmiany i usuwanie osób, spraw, kandydatów, misji, parafii, zapotrzebowań, spotkań, formatorów, superwizji, zasobów; wgrywanie i pobieranie plików; generowanie i pobieranie pism; wysyłkę kampanii; zmiany kont. Opis wpisu to nazwa obiektu i jego identyfikatory, np. `Metryka [caseId=…, id=…]`.
