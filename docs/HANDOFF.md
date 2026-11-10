# DOK Portal — przekazanie projektu (stan na 2026-11-10)

Dokument dla kolejnej sesji Claude. Przeczytaj go w całości przed pracą. Runbook operacyjny: `docs/operations.md`. Przegląd procesu z października (lista napraw, w większości zrobiona): `docs/przeglad-procesu-2026-10-08.md`.

## 1. Projekt

**DOK Portal** — CRM Diecezji Bydgoskiej: Szkoła Katechistów św. Pawła (SKŚP) i Diecezjalny Ośrodek Katechumenalny (DOK).
Repozytorium: `https://github.com/dokbydgoszcz/DOKPortalLight.git`, gałąź `master`. Właściciel i jedyny „klient”: Szymon, pisze zwięźle po polsku.

- Backend: ASP.NET Core 8, EF Core (Azure SQL; w testach SQLite), `backend/src/{DokPortal.Api,Application,Domain,Infrastructure}`, testy w `backend/tests/{Domain,Infrastructure,Api.Integration}.Tests` (33 + 551 + 424).
- Frontend: Angular 22.2 (zoneless, standalone), Vitest 4, `frontend/src/app/{core,features,layout,shared,testing}` (811 testów; progi pokrycia: instrukcje 97, gałęzie 94, funkcje 96, linie 98 — CI liczy ok. 0,3% niżej niż lokalnie, trzymaj zapas).
- Produkcja: frontend `https://thankful-stone-0bbe8680f.5.azurestaticapps.net` (Static Web App Free), API `https://dokportal-api.azurewebsites.net` (App Service), baza Azure SQL `sql-dokportal/DokPortalLight`, pliki w Blob `dokportalfiles`, Application Insights `appi-dokportal`. Wszystko w `rg-dokportal` (Poland Central).
- Wdrożenie: każdy push do `master` uruchamia CI backendu i frontendu, deploy API i SWA.

## 2. Zasady pracy ustalone z użytkownikiem (OBOWIĄZUJĄ)

- **Cała rozmowa po polsku**, zwięźle; użytkownik często pisze jednym zdaniem.
- Commity prosto na `master`, bez gałęzi i PR. **Push: właściciel zezwolił (2026-10) na wypychanie w tym projekcie bez pytania** — po zielonych testach. Po pushu sprawdź `gh run list` i żywe adresy.
- **Nigdy nie obsługuj sekretów** (hasła, klucze, tokeny, connection stringi). Ustawia je właściciel sam w Azure / Tally / GitHubie; podawaj mu dokładne kroki i nazwy ustawień. Nie czytaj `C:\development\ghtoken.txt`. Gdy wklei hasło na czacie — nie używaj go i poradź zmianę.
- Polecenia `az` zmieniające zasoby Azure: najpierw pokaż i uzyskaj zgodę.
- Stage’uj tylko konkretne pliki. **Nigdy nie commituj** `backend/src/DokPortal.Api/appsettings.Development.json` ani `.claude/`.
- Stopka commita: `Co-Authored-By: <aktualny model> <noreply@anthropic.com>` oraz linia `Claude-Session:` (jeśli sesja ją podaje).
- Przy każdej funkcji/poprawce dopisz wpis „Co nowego” (po polsku, na początku listy `changelog` w `frontend/src/app/features/dashboard/dashboard.component.ts`).
- TDD; **przy czerwonych testach nie commituj**. Zmiany w UI sprawdzaj na zrzucie ekranu (Playwright), nie tylko testami.
- Ręczne uruchomienie `Daily Jobs` wysyła prawdziwe e-maile — tylko na słowo właściciela.
- Odpowiadaj faktami: co sprawdzono, czego nie, co wymaga działania właściciela.

## 3. Komendy i pułapki

- Backend: `cd backend && dotnet test`. Migracja: `dotnet ef migrations add Nazwa -p src/DokPortal.Infrastructure -s src/DokPortal.Api`. Dane przenoś SQL-em w migracji (wzorce: `ManualFormationYears`, `GrantResourcesPermissions`). Seeder wstawia domyślne uprawnienia **tylko do pustej tabeli** — nowe uprawnienie wymaga migracji danych.
- Frontend: `cd frontend && npx ng test --watch=false --coverage`, `npm run build`. Podgląd lokalny: backend 5227, frontend 4200.
- **npm**: `npm install` z nowymi wersjami Angulara wywraca się na zależnościach równorzędnych (błąd arborist „edgesOut”). Działa: `npm install --force <wszystkie pakiety @angular naraz>`, potem `rm -rf node_modules && npm ci` dla sprawdzenia. `npm audit fix` też się wywraca — nie polegaj na nim.
- Angular: wartości ustawiane w `subscribe` muszą być sygnałami (zoneless). Globalne klasy CSS `.page` (display:none) i `.doc-actions` kolidują z klasami komponentów. Błędy z odpowiedzi typu blob czytaj przez `readBlobErrors()` z `shared/http-error.ts`. W testach: `fixture.detectChanges()` przed klikaniem, `await fixture.whenStable()` dla ngModel.
- **Potwierdzenia**: nie używaj `confirm()`. Jest `ConfirmService.ask(pytanie, coPoTak, { onCancel, confirmLabel, cancelLabel, danger })` (`core/confirm`). Okno rysuje `ConfirmDialogComponent` w korzeniu aplikacji; w testach pojedynczych ekranów okna nie ma i serwis pyta przez `window.confirm` — dlatego testy nadal robią `vi.spyOn(window, 'confirm')`.
- **CSP**: `frontend/public/staticwebapp.config.json`. Nowy zewnętrzny skrypt, czcionka albo zmiana adresu API wymaga dopisania do CSP. W `angular.json` wyłączone `inlineCritical` (inaczej Angular dodaje `onload=` w `<link>`, co CSP blokuje).
- **Błędy API**: klient dostaje treść `InvalidOperationException` tylko wtedy, gdy zgłosił ją kod `DokPortal.*`. Komunikat dla użytkownika zgłaszaj więc własnym `throw new InvalidOperationException("…")`, nie licz na wyjątek biblioteki.
- EF: dziecko z gotowym Guid dodane do śledzonej kolekcji jest uznawane za istniejące — dodawaj przez `DbSet.Add`. `HasQueryFilter` na wymaganych nawigacjach robi INNER JOIN.
- Synchronizacja kopii właściciela (`C:\development\DokPortalLight`, przez most do jego komputera): `git pull --ff-only`. Git potrzebuje tam zgody na usuwanie plików; bez niej zostawia `.git/*.lock` i nieśledzone kopie nowych plików — wtedy poproś o zgodę, usuń blokady i identyczne kopie, powtórz pull.

## 4. Uprawnienia i bezpieczeństwo

- Model `Moduł.Akcja` (`Domain/Constants/Permissions.cs`, `DefaultRolePermissions.cs`), tabela `RolePermissions` edytowalna w „Uprawnienia ról”, backend `[HasPermission]`/`[HasAnyPermission]`, frontend `*appHasPermission`, `permissionGuard`. Role: Administrator, Biskup, DyrektorSKSP, DyrektorDOK, Superwizor, KatechistaProwadzacy (+ własne).
- Katechista widzi tylko swoje sprawy DOK (`ICaseScopeProvider`, wymaga konta powiązanego z osobą). Notatki duszpasterskie widzi autor (+ `PastoralNotes.ReadAll`), można je udostępnić następcy.
- Sesja: JWT 8 h ze znacznikiem bezpieczeństwa sprawdzanym na serwerze; wyłączenie konta, zmiana ról, hasła, loginu albo MFA kończy sesje. MFA (TOTP) dobrowolne, z przypomnieniem co 3. logowanie. Wylogowanie po 20 min bezczynności (frontend).
- **Przegląd bezpieczeństwa 2026-11-10** (szczegóły: `docs/operations.md`, „Ochrona dostępu”): konto administratora zmienia tylko administrator; hasło tymczasowe otwiera tylko `/api/auth` (claim `mcp`); nagłówki bezpieczeństwa i CSP; czyszczenie HTML wzorów pism (`LetterHtml.Sanitize`); CSV dziennika bez formuł; komunikaty błędów tylko własne.
- Znane, niezałatane: TipTap 2.x (GHSA-cp6q-959q-f8rh, poprawka dopiero w 3.x — duża migracja).

## 5. Co jest zrobione (wdrożone)

- **Osoby**: funkcje (Katechista/Akolita/Lektor/Proboszcz), unikalny e-mail, adres, płeć, imieniny (miesiąc słownie), zdjęcie, karta osoby ze wszystkimi powiązaniami, wyszukiwarka osób w formularzach, filtr „Bez funkcji”, „Importuj listę” (wklejona lista, nadanie funkcji, zapis na SKŚP), RODO: eksport JSON i anonimizacja.
- **Zgłoszenia z formularzy (Tally)**: webhook z podpisem → kolejka „Do przejrzenia” → nowa osoba albo aktualizacja, ze zdjęciem; dociąganie z API Tally (przycisk i krok dzienny).
- **Parafie**: rejestr z dekanatem i kontaktem, „Importuj proboszczów”, giełda zapotrzebowań; skierowanie tworzy misję i wysyła e-maile.
- **SKŚP**: kandydaci (rok ręcznie, przenoszenie, ukończenie z kontrolą braków, zatrzymanie/wznowienie), zajęcia z obecnością.
- **Katechiści**: misje kanoniczne (posłanie, odnowienie, załączniki), przypomnienie o wygasających.
- **DOK**: 5 ścieżek z etapami, edycja, historia, przerwanie/wznowienie, dokumenty, notatki, spotkania (także cykliczne co tydzień i grupowe), obecność, kalendarz iCal, absolwenci.
- **Pisma**: edytor wzorów w portalu (TipTap) z polami `{{pole}}`, `{{pole|przypadek}}` (odmiana imion i nazwisk), `{{rodzaj:on|ona}}`, `{{wpisz:…}}`; **pola podświetlane kolorami, literówki na czerwono**; papier firmowy z logo; PDF i Word; historia wydanych pism. 11 wzorów startowych „do uzupełnienia”.
- **Mailing**: kampanie do grup, kolejka w tle (400/dobę, limit Gmaila), załączniki, link „wypisz się”.
- **Powiadomienia e-mail**: nowa sprawa DOK, proboszcz po skierowaniu, przypomnienia (spotkania, braki dokumentów, imieniny, misje).
- **Pulpit**: liczniki, „Wymaga uwagi”, imieniny, spotkania, braki dokumentów, misje do odnowienia, „Co nowego” (stronicowane).
- **Inne**: superwizje, budżet SKŚP/DOK z załącznikami i eksportem, zasoby dla katechistów, dziennik zmian, eksporty do Excela, **strona „Pomoc”** (`/pomoc`, instrukcja dla katechisty, dyrektora i spraw konta — treść w `features/help/help-content.ts`; aktualizuj ją razem ze zmianami w UI), własne okna potwierdzeń, animacja po zalogowaniu (2 s), dźwięk przy usuwaniu w zasobach.

## 6. Hosting — stan i decyzje

- 2026-10-20: darmowa baza wyczerpała miesięczny limit (wstrzymana do końca miesiąca). 2026-10-31: wygasła darmowa subskrypcja — wszystko stanęło; 2026-11-03 właściciel przeszedł na **Pay-As-You-Go** (plan wsparcia Basic). Hasło administratora SQL zresetowane 2026-10-20 (login `dokportaladmin`).
- **Stan planów do potwierdzenia u właściciela**: 2026-11-09 pytał o najtańszy plan bez usypiania; rekomendacja: App Service **B1 Linux + Always On** oraz baza **Basic 5 DTU** (razem ok. 18 USD/mies.). Nie wiadomo, czy już przełączył. Jeśli tak — można zdjąć obejścia pisane pod F1 i darmową bazę (niżej).
- Obejścia pod darmowe plany (w kodzie): migracje i seed raz na wdrożoną wersję (znacznik na dysku App Service), kolejka mailingu po restarcie tylko gdy coś czeka, jeden workflow `daily-jobs.yml` o 7:00 UTC, `uptime.yml` woła tylko `/health` (bez bazy). Opis: `docs/operations.md`, „Oszczędzanie darmowego limitu bazy”.
- Na F1 webhook Tally nie dochodzi, gdy aplikacja śpi (Tally czeka 10 s) — stąd dociąganie z API.

## 7. Otwarte sprawy

Czekają na właściciela:
1. Decyzja i przełączenie planów (sekcja 6); alert budżetowy w Azure.
2. `Tally__ApiKey` (i opcjonalnie `Tally__FormIds=LD12GG`) w ustawieniach App Service — bez tego nie działa „Pobierz brakujące z Tally”. Sekret podpisu webhooka prawdopodobnie ustawiony (webhook odpowiada 401 bez podpisu).
3. Uruchomienie na bazie skryptu ze zgłoszeniami Tally (37 odpowiedzi do 2026-11-03; plik przekazany w rozmowie) albo dociągnięcie przyciskiem po ustawieniu klucza.
4. Prawdziwa treść wzorów pism, logo i adres w nagłówku; tekst klauzuli RODO.
5. Wymagane dokumenty per ścieżka DOK (punkt z przeglądu procesu — brak danych od właściciela).
6. Import proboszczów (plik xlsx przekazany w rozmowie), test mailingu na prawdziwej skrzynce.
7. Zmiana hasła, które wkleił na czacie 2026-11-10 (jeśli było prawdziwe).

Do zrobienia po stronie kodu (zaproponowane, bez decyzji):
- Obowiązkowe MFA dla administratora i dyrektorów.
- Migracja TipTap 2 → 3.
- Absolwenci 2023–2025 jako „ukończyli formację” w SKŚP.
- Pytanie „Miejscowość” w formularzu Tally (portal zgaduje ją tylko dla kodów 85-xxx).
- Własna domena; monitoring błędów z alertami; próbne odtworzenie bazy z kopii; zewnętrzny test penetracyjny.

## 8. Czego NIE ma w repozytorium

- `backend/src/DokPortal.Api/appsettings.Development.json` — lokalna konfiguracja z danymi dostępowymi; właściciel przenosi ją sam.
- Sekrety produkcyjne (Azure App Service → Environment variables): `ConnectionStrings__Default`, `Jwt__Key`, `Smtp__*`, `Reminders__ApiKey`, `BlobStorage__ConnectionString`, `Tally__SigningSecret`, `Tally__ApiKey`, `SeedAdmin__Password`. W GitHubie: `AZURE_WEBAPP_PUBLISH_PROFILE`, token SWA, `REMINDERS_API_KEY`.
