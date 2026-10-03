# Faza 5 — jakość testów — projekt

Data: 2026-10-03. Faza 5 z planu dojrzewania systemu.

## Stan wyjściowy (zmierzony 2026-10-03)

- **Backend:** 81% linii (3101 linii bez migracji), 258 testów. Luki: ścieżki edycji/usuwania w serwisach Misji, Superwizji, Spotkań, Formatorów, dokumentów spraw i użytkowników (52–67%), kontrolery Formatorów i Spotkań (32%). `SmtpEmailSender` i `AzureBlobStorageService` mają 0% (zewnętrzne I/O).
- **Frontend:** 58% linii, 57% instrukcji, 60,5% gałęzi, **36,5% funkcji**, 99 testów. Testy sprawdzają głównie renderowanie list i żądania serwisów; metody zapisu, edycji, usuwania, formularze i stany błędów prawie nie są wywoływane. Najsłabsze pliki: `dok-cases-list` (32%), `users-list` (28%), formularze spraw DOK i kandydata (~3%), `people-list`, `parish-board`, `meetings-list`, `formators-list`, `supervisions-list`, logowanie.
- **CI:** Frontend CI uruchamia `ng test` bez pomiaru pokrycia (brak pakietu `@vitest/coverage-v8`); Backend CI uruchamia `dotnet test` bez pomiaru.

## Decyzje (zatwierdzone)

- Zakres: frontend i luki backendu; bez e2e (Playwright), bez testów `SmtpEmailSender`/`AzureBlobStorageService`, bez progu pokrycia dla backendu (kolektor `coverlet.collector` nie wspiera progów).
- Cel: frontend ok. 80% linii i ok. 70% funkcji; backend ok. 88% linii. Progi w CI ustawione tuż poniżej osiągniętego poziomu (zapobiegają spadkowi).

## Narzędzia

- `@vitest/coverage-v8@4.1.11` jako devDependency (wersja jak zainstalowany `vitest`).
- Opcje builder-a `@angular/build:unit-test` w `angular.json`: `coverageExclude` (pliki `*.spec.ts`, `*.model.ts`, `main.ts`, `app.config.ts`, `environments/**`, `testing/**`), `coverageReporters` (`text-summary`, `html`), `coverageThresholds` (ustawione na końcu pracy).
- Frontend CI: `npx ng test --coverage` (progi wymuszane przez builder, przekroczenie w dół kończy krok błędem).
- Backend CI: `dotnet test --collect:"XPlat Code Coverage"` (tylko raport, bez progu).

## Wzorzec testów frontendu

- Wspólna pomoc `frontend/src/app/testing/test-helpers.ts` (wyłączona z pokrycia): `api(path)`, `provideFakeAuth(granted)` (mock `AuthService` z `hasPermission` — domyślnie wszystkie uprawnienia), `setup(component, options)` zwracające `{ fixture, http, el }`, `paged(items)`, `click`, `clickByText`, `setInput`, `setSelect`, `flushAll`.
- Testy zachowania: otwarcie formularza → wypełnienie → zapis → asercja ciała i metody żądania HTTP; edycja (`PUT`); usuwanie z `confirm` (`DELETE`, brak żądania po anulowaniu); stan błędu (toast z komunikatem); blokada przycisku „Zapisz” do wyboru wymaganych pól; paginacja i szukanie; ukrycie akcji bez uprawnienia (już istnieje w `permission-wiring.spec.ts`).
- Kolejność ekranów wg ryzyka: użytkownicy (konta, role, reset hasła), sprawy DOK (notatki, dokumenty, formularz), Osoby, logowanie, Kandydaci, Misje, Formatorzy, Spotkania, Superwizje, giełda parafii, budżety, mailing, dokumenty.

## Wzorzec testów backendu

- Serwisy (InMemory): `Update*`/`Delete*` dla istniejącego i nieistniejącego rekordu, soft-delete (rekord znika z listy, `DeletedBy` ustawione).
- Kontrolery (integracyjne): `PUT`/`DELETE` happy path, 404 dla nieznanego id, 403 dla roli bez uprawnienia (dla modułów, gdzie jeszcze nie ma testu), walidacja (400).

## Kryteria sukcesu

- Frontend: linie ≥ 80%, funkcje ≥ 70% (progi w `angular.json` ustawione o 1–2 p.p. niżej), wszystkie testy zielone, `ng build` bez błędów.
- Backend: linie ≥ 88%, wszystkie testy zielone.
- CI zielone po wypchnięciu (po zgodzie użytkownika).

## Poza zakresem

E2E w przeglądarce, testy wydajnościowe, mutation testing, próg pokrycia backendu, zmiany kodu produkcyjnego poza poprawkami błędów wykrytych przez nowe testy (każdy taki błąd zgłaszam osobno).
