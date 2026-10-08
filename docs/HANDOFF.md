# DOK Portal — przekazanie projektu (stan na 2026-10-08, `master` @ 0863978)

Dokument dla kolejnej sesji Claude (na innym koncie lub komputerze). Przeczytaj go w całości przed pracą. Szczegóły historii są w transkrypcie sesji (jeśli został przekazany) oraz w `docs/przeglad-procesu-2026-10-08.md`, `docs/operations.md`.

## 1. Projekt

**DOK Portal** (nazwa w UI; wcześniej „DOK Portal Light”) — CRM Diecezji Bydgoskiej: Szkoła Katechistów św. Pawła (SKŚP) i Diecezjalny Ośrodek Katechumenalny (DOK).
Repozytorium: `https://github.com/dokbydgoszcz/DOKPortalLight.git`, gałąź `master`. Właściciel i jedyny „klient”: Szymon (szymon.luczak@simsmm.com), pisze zwięźle po polsku.

- Backend: ASP.NET Core 8, EF Core (Azure SQL), `backend/src/{DokPortal.Api,Application,Domain,Infrastructure}`, testy w `backend/tests/{Domain,Infrastructure,Api.Integration}.Tests`.
- Frontend: Angular 22 (zoneless, standalone), Vitest 4, `frontend/src/app/{core,features,layout,shared,testing}`.
- Hosting (budżet 0 zł): Azure App Service F1 `dokportal-api` w `rg-dokportal`, darmowy Azure SQL, Blob Storage na pliki, Static Web App dla frontendu, Application Insights `appi-dokportal`. Wdrożenie: każdy push do `master` uruchamia `.github/workflows/deploy.yml` (CI i deploy równolegle). Runbook: `docs/operations.md`.

## 2. Zasady pracy ustalone z użytkownikiem (OBOWIĄZUJĄ)

- **Cała rozmowa po polsku.**
- Commity prosto na `master`, bez gałęzi i PR. **Push tylko po wyraźnym „wypchnij/OK”** w danej sprawie; po pushu sprawdzić `gh run list`.
- **Nigdy nie obsługuj sekretów** (hasła, klucze, tokeny, connection stringi). Hasło SMTP i podobne ustawia użytkownik sam w Azure. Polecenia `az` zmieniające konto/zasoby Azure: najpierw pokaż i uzyskaj zgodę.
- Stage’uj tylko konkretne pliki. **Nigdy nie commituj** `backend/src/DokPortal.Api/appsettings.Development.json` ani `.claude/`. Po każdym commicie: `git show --stat HEAD | grep -c "appsettings\|launch"` ma dać 0.
- Stopka commita: `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>` (lub aktualny model).
- Przy każdej funkcji/poprawce dopisz wpis „Co nowego” dla testerów (po polsku, na początku listy `changelog` w `frontend/src/app/features/dashboard/dashboard.component.ts`, najnowsze pierwsze).
- Proces: krótki projekt/ustalenia → zgoda → TDD (test czerwony → implementacja → zielony) → commit. **Przy czerwonych testach nie commituj.**
- Odpowiadaj faktami: co przetestowano, czego nie (np. brak sprawdzenia w przeglądarce), co wymaga uwagi użytkownika.

## 3. Komendy i pułapki środowiska

- Backend: `cd backend && dotnet test` (zatrzymaj lokalny serwer podglądu, bo blokuje DLL-e). Migracja: `dotnet ef migrations add Nazwa -p src/DokPortal.Infrastructure -s src/DokPortal.Api`, potem edytuj (dane przenoś SQL-em z publiczną stałą testowaną na SQLite; patrz `ManualFormationYears`, `GrantResourcesPermissions`). Klucze FK do tej samej tabeli: unikaj kaskad SQL Server (wiele ścieżek).
- Frontend: `cd frontend && npx ng test --watch=false --coverage` (progi pokrycia). Lokalny podgląd: `.claude/launch.json` (backend 5227, frontend 4200).
- Windows + Git Bash: **nie używaj heredoców z backslashami/cudzysłowami do skryptów** — zapisuj skrypty plikiem (Write) i uruchamiaj `python plik.py`. Pliki w repo mają mieszane końce linii; skrypty edytujące normalizuj `\r\n`→`\n` i odtwarzaj.
- Korporacyjny proxy TLS: `curl`/`az` bywają zawodne; używaj PowerShell `Invoke-WebRequest`, `MSYS_NO_PATHCONV=1` przed `az`.
- Angular testy: `fixture.detectChanges()` przed klikaniem przycisków z `[disabled]`; `await fixture.whenStable()` dla ngModel; dynamiczne `name` na polach daj jako `[attr.name]`; `clickByText` bywa trafiony w opakowanie o tym samym tekście; select z wartością początkową: użyj `[selected]` na `<option>`.
- EF: dziecko z gotowym Guid dodane do śledzonej kolekcji jest uznawane za istniejące — dodawaj przez `DbSet.Add`. `HasQueryFilter` na wymaganych nawigacjach robi INNER JOIN (ostrzeżenia 10622 przy migracjach są prawdziwe).
- F1: po wdrożeniu pierwsze żądania mogą dać 503/403 (zimny start / `QuotaExceeded`); poczekaj, nie restartuj w pętli (zjada limit CPU).

## 4. Uprawnienia

Model `Moduł.Akcja` (`backend/src/DokPortal.Domain/Constants/Permissions.cs`, `DefaultRolePermissions.cs`, `AppRoles.cs`), tabela `RolePermissions` (edytowalna w UI „Uprawnienia ról”), backend `[HasPermission]`, frontend `*appHasPermission`, `permissionGuard`. Seeder wstawia domyślne uprawnienia **tylko do pustej tabeli**, więc każde nowe uprawnienie wymaga migracji danych (wzorzec: `GrantResourcesPermissions`). Role: Administrator, Biskup, DyrektorSKSP, DyrektorDOK, Superwizor, KatechistaProwadzacy (+ własne). Katechista widzi tylko swoje sprawy DOK (zakres wierszowy `ICaseScopeProvider`, wymaga powiązania konta z osobą — ekran Użytkownicy).

## 5. Co jest zrobione (wdrożone, w `master`)

- Osoby (menu „Osoby”): **funkcje** Katechista/Akolita/Lektor/Proboszcz (`PersonFunction`; proboszcz ↔ parafia, jeden proboszcz na parafię; filtr listy po funkcji), unikalny e-mail (twarda blokada), telefon ostrzega.
- Kandydaci SKŚP: rok formacji **ręcznie** (1–3, „Przenieś”, masowo, historia zdarzeń z datą i autorem), ukończenie formacji → lista „Katechiści” jako „Przed udzieleniem posługi”, zatrzymanie formacji z notatką, rekolekcje per rok.
- Katechiści (misje kanoniczne): „Udziel posłania”, misja bez pola „miejsce udzielenia”, załączniki, **misja nadaje funkcję Katechista** (migracja uzupełniła istniejące).
- Giełda parafii: skierowanie wielu osób; **skierowanie tworzy misję (rok) i wysyła e-mail do proboszcza i katechisty z wzajemnymi danymi kontaktowymi** (błąd poczty nie cofa skierowania).
- Podopieczni DOK: 5 ścieżek z etapami zależnymi od ścieżki (+ Absolwent wszędzie), alert „etap dłużej niż rok”, filtr etapu, dokumenty/notatki/spotkania/obecność/frekwencja, zakres katechisty.
- Pulpit: liczniki (kafelki są linkami), imieniny (14 dni), lista spotkań z 7 dni, lista spraw z brakującymi dokumentami, „Wymaga uwagi”, „Co nowego”.
- Biblioteka **Zasoby dla katechistów** (uprawnienia `Resources.View/Manage`; wgrywają Superwizor i Dyrektorzy, przegląda każdy z uprawnieniem).
- Dokumenty i pisma: 11 typów (wniosek o misję, zaświadczenia o chrzcie/bierzmowaniu/Eucharystii, studium katechumenalne, rodzice chrzestni, RODO, …) — **treść wszystkich to na razie ten sam prosty układ**.
- Mailing: grupy odbiorców, szkice, wysyłka odporna na błędy pojedynczych adresów (`FailedCount`), brak ponownej wysyłki wysłanej kampanii, test wysyłki, przypomnienia cron (dokumenty, spotkania, imieniny przez GitHub Actions).
- Pliki (PDF/JPG/PNG/DOCX/DOC/TXT ≤ 20 MB) przy notatkach, superwizjach, dokumentach spraw, misjach, zasobach.
- Audit log, eksporty do Excela, monitoring (uptime, App Insights), stopka z wersją 1.0.0, krzyż jako logo/favicon.

## 6. Otwarte sprawy (kolejność ustalona)

1. **Poczta Gmail `dokbydgoszcz@gmail.com`** (zamiast zablokowanego M365: `535 5.7.139 SmtpClientAuthentication is disabled`). Kod gotowy. Potrzeba: (a) zgody użytkownika na `az webapp config appsettings set -g rg-dokportal -n dokportal-api --settings Smtp__Host=smtp.gmail.com Smtp__Port=587 Smtp__EnableSsl=true Smtp__Username=dokbydgoszcz@gmail.com Smtp__FromEmail=dokbydgoszcz@gmail.com Smtp__FromName="DOK Portal"` (poprzedzone `MSYS_NO_PATHCONV=1`), (b) hasła aplikacji Google wpisanego **przez użytkownika** w `Smtp__Password`. Potem: Mailing → „Wyślij wiadomość testową”. Limit Gmaila ok. 500/dobę. Instrukcja: `docs/operations.md`.
2. **Naprawy z przeglądu procesu** — pełna lista w `docs/przeglad-procesu-2026-10-08.md`. Rekomendowana kolejność: (1) spójność danych i usuwanie osób/parafii (blokada przy zależnościach, filtry po `Person`, spójność misja↔funkcja↔skierowanie); (2) **edycja sprawy DOK w UI** (etap, katechista, mentor — backend `PUT` istnieje, front go nie woła), pomijanie absolwentów w przypomnieniach, `ForScope` dla absolwentów; (3) dostęp/konta (`GET /api/people` otwarty dla każdego zalogowanego!, dezaktywacja konta, obsługa wygasłego tokenu, ochrona ostatniego administratora, imieniny tylko dla właściwych ról); (4) misje wygasające na pulpicie + „Odnów”; (5) operacje (runbook kopii, bramka CI przed deployem, `EnableRetryOnFailure`, rate-limit logowania, rozszerzony audit); (6) pisma — po dostarczeniu wzorów.
3. **Wzory pism** — użytkownik ma dostarczyć/zatwierdzić prawdziwe treści (do tego czasu rozważ oznaczenie „wzór do uzupełnienia”).
4. Po wdrożeniu: powiązać konta katechistów z osobami w ekranie Użytkownicy (bez tego katechista nie widzi spraw).
5. Do sprawdzenia w Azure (poza repo): HTTPS Only, `Reminders__ApiKey`, silny `Jwt__Key`, hasło `SeedAdmin__Password` (nie domyślne), retencja PITR, soft-delete Blob.

## 7. Czego NIE ma w repozytorium (trzeba odtworzyć na nowym komputerze)

- `backend/src/DokPortal.Api/appsettings.Development.json` — lokalna konfiguracja (connection string, JWT, SeedAdmin itp.). **Zawiera dane dostępowe; nie jest w paczce.** Użytkownik musi go przenieść bezpiecznie lub utworzyć od nowa (poproś go o wartości; nie wpisuj ich do czatu ani do repo).
- Poświadczenia GitHub (`gh auth login`) i Azure (`az login`) na nowym koncie/komputerze.
- `.claude/launch.json` jest w paczce (konfiguracja podglądu).
