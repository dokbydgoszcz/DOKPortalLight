# Faza 7 – zakres katechisty i obecność – plan implementacji

> **Dla wykonawców:** WYMAGANA UMIEJĘTNOŚĆ: superpowers:executing-plans (inline) lub superpowers:subagent-driven-development. Kroki używają checkboxów (`- [ ]`).

**Goal:** Katechista widzi/edytuje tylko swoich podopiecznych; szybki przełącznik obecności; frekwencja na sprawie; obecność grupowa.

**Architecture:** Uprawnienie `DokCases.ViewAll` (wzorzec jak `PastoralNotes.ReadAll`) + `ICaseScopeProvider` wstrzykiwany do serwisów jako opcjonalny parametr konstruktora (domyślnie bez ograniczeń). Filtr `ForScope` na `IQueryable<DokCase>`. Migracje EF: dane (nadanie uprawnienia istniejącym rolom) i schemat (zajęcia grupowe).

**Tech Stack:** ASP.NET Core 8, EF Core (SQL Server / SQLite w testach), xUnit; Angular 22 (signals, zoneless), Vitest.

**Spec:** `docs/superpowers/specs/2026-10-03-catechist-scope-attendance-design.md`

## Ograniczenia globalne

- Commity lokalne na `master`, stopka `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`; stage'ować tylko wskazane pliki (nigdy `appsettings.Development.json`, `.claude/`).
- Push dopiero po wyraźnej zgodzie użytkownika; migracje wykonują się przy starcie aplikacji na produkcji.
- Po każdym etapie widocznym dla testerów: wpis w „Co nowego” (`frontend/src/app/features/dashboard/dashboard.component.ts`, na początku listy).
- Progi pokrycia frontendu (instrukcje 97 / gałęzie 94 / funkcje 96 / linie 98) muszą dalej przechodzić.
- Czat po polsku. Dane testowe: `DokCase` wymaga `CatechistPersonId` (wymagana relacja – bez niego `Include` zwraca puste wyniki).

## Pliki (przegląd)

Backend: `Domain/Constants/Permissions.cs`, `DefaultRolePermissions.cs`; `Application/DokCases/CaseScope.cs` (nowy), `ICaseScopeProvider.cs` (nowy); `Infrastructure/Services/CaseScopeExtensions.cs` (nowy), `AllCasesScopeProvider.cs` (nowy), `DokCaseService.cs`, `CaseDocumentService.cs`, `PastoralNoteService.cs`, `MeetingService.cs`, `DashboardService.cs`, `ExportService.cs`; `Api/Authorization/HttpCaseScopeProvider.cs` (nowy), `Program.cs`, `MeetingsController.cs`; migracje w `Infrastructure/Migrations`.
Frontend: `core/auth/permissions.ts`, `features/meetings/*`, `features/dok-cases/*`.

---

### Task 1: Uprawnienie `DokCases.ViewAll` i migracja danych

**Pliki:** `Permissions.cs`, `DefaultRolePermissions.cs`, nowa migracja, `frontend/src/app/core/auth/permissions.ts`; testy: `DokPortal.Domain.Tests` / `DokPortal.Infrastructure.Tests` (PermissionService, katalog), frontend `permissions-matrix` jeśli listuje katalog.

- [ ] **1.1** Przeczytaj `Permissions.cs` (stałe + `Catalog`) oraz `permissions.ts`; zanotuj format wpisu katalogu.
- [ ] **1.2** Napisz test (czerwony): `DefaultRolePermissions.Grants` – każda rola z `DokCases.View` poza `KatechistaProwadzacy` ma `DokCases.ViewAll`; `KatechistaProwadzacy` go nie ma; katalog zawiera `DokCases.ViewAll`; wszystkie uprawnienia z `Grants` są w katalogu (istniejący test spójności, jeśli jest).
- [ ] **1.3** Dodaj stałą `DokCasesViewAll = "DokCases.ViewAll"`, wpis katalogu („Sprawy DOK”, „Podgląd wszystkich spraw DOK i powiązanych dokumentów, notatek i spotkań”) i dopisz do ról z `DokCases.View` oprócz katechisty. Dodaj stałą w `permissions.ts`.
- [ ] **1.4** Migracja EF (data): `dotnet ef migrations add AddDokCasesViewAll --project src/DokPortal.Infrastructure --startup-project src/DokPortal.Api`; w `Up` SQL:

```sql
INSERT INTO RolePermissions (RoleName, Permission)
SELECT DISTINCT rp.RoleName, 'DokCases.ViewAll'
FROM RolePermissions rp
WHERE rp.Permission = 'DokCases.View'
  AND rp.RoleName <> 'KatechistaProwadzacy'
  AND NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleName = rp.RoleName AND x.Permission = 'DokCases.ViewAll');
```

`Down`: `DELETE FROM RolePermissions WHERE Permission = 'DokCases.ViewAll';`. Sprawdź, że migracja nie zawiera zmian schematu (sam SQL); jeśli EF doda nieoczekiwane zmiany modelu – usuń je i zbadaj przyczynę.
- [ ] **1.5** Uruchom `dotnet test` (całość) i `ng test`; commit „Uprawnienie DokCases.ViewAll i migracja danych”.

### Task 2: Zakres spraw (`CaseScope`) i serwisy

**Pliki:** `CaseScope.cs`, `ICaseScopeProvider.cs`, `CaseScopeExtensions.cs`, `AllCasesScopeProvider.cs`, `HttpCaseScopeProvider.cs`, `Program.cs`, serwisy z listy w przeglądzie; testy: `Services/CaseScopeTests.cs` (nowy) i integracyjne `CatechistScopeTests.cs` (nowy).

Kontrakt:

```csharp
namespace DokPortal.Application.DokCases;
public sealed record CaseScope(bool ViewAll, Guid? PersonId)
{
    public static readonly CaseScope All = new(true, null);
}
public interface ICaseScopeProvider { Task<CaseScope> GetAsync(CancellationToken ct); }
```

```csharp
public static class CaseScopeExtensions
{
    public static IQueryable<DokCase> ForScope(this IQueryable<DokCase> q, CaseScope scope) =>
        scope.ViewAll ? q
        : scope.PersonId is { } id ? q.Where(c => c.CatechistPersonId == id)
        : q.Where(_ => false);
}
```

`AllCasesScopeProvider.GetAsync` → `CaseScope.All`. `HttpCaseScopeProvider` (Api): `IHttpContextAccessor`, `IAuthorizationService`, `AppDbContext`; `ViewAll = (await _authorization.AuthorizeAsync(user, null, Permissions.DokCasesViewAll)).Succeeded`; `PersonId` z `_db.Users.Where(u => u.Id == sub).Select(u => u.PersonId)`; niezalogowany → `new CaseScope(false, null)`. Rejestracja: `AddHttpContextAccessor()` + `AddScoped<ICaseScopeProvider, HttpCaseScopeProvider>()`. Serwisy: opcjonalny parametr `ICaseScopeProvider? scope = null` → `_scope = scope ?? new AllCasesScopeProvider()`; DI wybiera konstruktor z zarejestrowanym dostawcą.

- [ ] **2.1** Testy filtra `ForScope` (czerwone, InMemory): ViewAll → wszystkie; własne → tylko własne; cudze → pusto; `PersonId == null` bez ViewAll → pusto.
- [ ] **2.2** Zaimplementuj `CaseScope`, `ICaseScopeProvider`, `ForScope`, `AllCasesScopeProvider`; testy zielone.
- [ ] **2.3** Testy serwisów ze `StubScope` (czerwone) – dla `DokCaseService` (Search/GetById/Update/Delete: cudza sprawa → pusto/null/false), `CaseDocumentService` (GetForCase pusty, Create/SetProvided/Upload/Download → `null` dla cudzej sprawy; uwaga: `CreateAsync` dziś zwraca nie-null – zmień sygnaturę na `CaseDocumentDto?` i zaktualizuj kontroler → 404), `PastoralNoteService` (odczyt pusty, tworzenie → `null`/404 dla cudzej sprawy; zaktualizuj kontroler), `DashboardService` (liczniki w zakresie), `ExportService` (spraw DOK i spotkań w zakresie).
- [ ] **2.4** Zaimplementuj zakres w serwisach: każdy punkt wejścia pobiera `var scope = await _scope.GetAsync(ct)` i używa `_db.DokCases.ForScope(scope)`; dla zapytań po `DokCaseId` dołącz warunek `_db.DokCases.ForScope(scope).Any(c => c.Id == x.DokCaseId)` (lub `Where(x => scopedCaseIds.Contains(...))`). `MeetingService`: widoczne są spotkania z `DokCaseId` w zakresie albo (ViewAll) bez sprawy; spotkania bez sprawy dla niepełnego zakresu pojawią się dopiero z `CatechistPersonId` (Task 6) – na tym etapie niewidoczne dla nie-ViewAll; `Create/Update` spotkania z `DokCaseId` cudzej sprawy → 400 (`InvalidOperationException` mapowany na 400 przez istniejący `GlobalExceptionHandler`, jeśli tak działa – zweryfikuj; inaczej `ValidationException`).
- [ ] **2.5** Testy integracyjne `CatechistScopeTests`: dwóch katechistów z kontami powiązanymi z osobami (utwórz osobę, ustaw `AppUser.PersonId` przez `UserManager`) i osobnymi sprawami; sprawdź listę spraw, GET sprawy (404 dla cudzej), dokumenty (lista pusta/404, upload 404), notatki, spotkania, dashboard; Administrator i Superwizor widzą wszystko; konto bez `PersonId` widzi puste listy; niezalogowany 401. Zaktualizuj istniejące testy integracyjne używające roli `KatechistaProwadzacy` (powiąż konto z osobą-katechistą sprawy).
- [ ] **2.6** `dotnet test` (całość), commit „Zakres katechisty: widzi tylko swoje sprawy, dokumenty, notatki i spotkania”.
- [ ] **2.7** Frontend: wpis w „Co nowego” (np. „Katechista widzi i edytuje tylko przypisanych podopiecznych…; powiązanie konta z osobą ustawia administrator”), pozycja uprawnienia w testach macierzy (jeśli test zlicza), `ng test --coverage`, commit „Co nowego: zakres katechisty”.

### Task 3: Szybki przełącznik obecności i frekwencja

**Pliki:** `MeetingsController.cs`, `IMeetingService.cs`, `MeetingService.cs`, `DokCaseDto.cs`, `DokCaseService.cs`, frontend `meetings-list.*`, `meetings.service.ts`, `dok-case.model.ts`, `dok-cases-list.*`.

- [ ] **3.1** Testy serwisu (czerwone): `SetAttendanceAsync(Guid id, bool? isAttended, ct)` – ustawia/czyści obecność; `null` dla nieistniejącego lub spoza zakresu. Testy `DokCaseService`: `MeetingsRecorded`/`MeetingsAttended` – zliczają spotkania sprawy z ustawioną obecnością (null się nie liczy), dla strony wyników i `GetByIdAsync`, niezależnie od zakresu innych spraw.
- [ ] **3.2** Implementacja: `IMeetingService.SetAttendanceAsync`; kontroler `PUT api/meetings/{id}/attendance` (`[HasPermission(Meetings.Manage)]`, body `SetAttendanceRequest { bool? IsAttended }`); w `DokCaseDto` `MeetingsRecorded`, `MeetingsAttended` (int, `init`, domyślnie 0); `DokCaseService` wylicza zbiorczo: `GroupBy` po `DokCaseId` dla `Meetings` z `IsAttended != null` i `DokCaseId in pageIds`.
- [ ] **3.3** Testy integracyjne endpointu (200, 404 cudze, 403 bez uprawnienia, czyszczenie `null`), commit backendu.
- [ ] **3.4** Frontend (testy najpierw): `MeetingsService.setAttendance(id, value)`; w liście spotkań przyciski „Obecny”/„Nieobecny” (aktywny = klik czyści) z `*appHasPermission="'Meetings.Manage'"`, po sukcesie odświeżają wiersz, błąd → toast; kolumna „Frekwencja” w liście spraw DOK (`recorded > 0 ? attended/recorded (pct%) : '—'`). `ng test --coverage`, wpis „Co nowego”, commit.

### Task 4: Obecność grupowa – model i API

**Pliki:** `Domain/Entities/Meeting.cs` (+`CatechistPersonId`, `Attendees`), `Domain/Entities/MeetingAttendee.cs` (nowy), `AppDbContext.cs`, migracja schematu, `Application/Meetings/*` (DTO, requesty, walidator), `MeetingService.cs`, `MeetingsController.cs`.

- [ ] **4.1** Testy serwisu (czerwone): zajęcia grupowe z uczestnikami zapisują się i wracają w `MeetingDto.Attendees`; walidacja „indywidualne albo grupowe” (oba → błąd); niepełny zakres: uczestnik spoza podopiecznych → błąd; właściciel = `PersonId` użytkownika; widoczność: właściciel zajęć widzi je, obcy katechista nie; `ViewAll` widzi wszystko; `SetAttendeeAttendanceAsync(meetingId, caseId, value)` ustawia obecność uczestnika, `null` dla obcych; usunięcie spotkania ukrywa je razem z uczestnikami; frekwencja z Task 3 uwzględnia wiersze uczestników.
- [ ] **4.2** Implementacja: encja `MeetingAttendee { Id, MeetingId, Meeting, DokCaseId, DokCase, IsAttended }` + unikalny indeks `(MeetingId, DokCaseId)`, kaskada po spotkaniu; `Meeting.CatechistPersonId`; `CreateMeetingRequest.Attendees` (`AttendeeRequest { DokCaseId, IsAttended? }`); `MeetingDto.Attendees` (`MeetingAttendeeDto { DokCaseId, PersonFullName, IsAttended }`); walidator FluentValidation (`DokCaseId` i `Attendees` wykluczają się; uczestnicy bez duplikatów); `UpdateAsync` synchronizuje listę uczestników; predykat widoczności zaktualizowany o `CatechistPersonId`; `PUT api/meetings/{id}/attendees/{caseId}`.
- [ ] **4.3** Migracja schematu `AddMeetingAttendees` (`dotnet ef migrations add`), przejrzyj wygenerowany kod; testy integracyjne (tworzenie grupowych, widoczność dwóch katechistów, przełącznik uczestnika 200/404/403).
- [ ] **4.4** `dotnet test` (całość), commit.

### Task 5: Obecność grupowa – frontend

**Pliki:** `meeting.model.ts`, `meetings.service.ts`, `meeting-form.component.*` (lub formularz w `meetings-list`), `meetings-list.component.*`, specyfikacje.

- [ ] **5.1** Testy najpierw: formularz – przełącznik „Zajęcia grupowe” pokazuje listę dostępnych spraw (z `DokCasesService`) z polami wyboru i wysyła `attendees`; lista – wiersz zajęć grupowych rozwija uczestników z przyciskami obecności (`PUT .../attendees/{caseId}`), indywidualne bez zmian; błędy → toast.
- [ ] **5.2** Implementacja zgodnie z wzorcami istniejących komponentów (signals; `ngModel` rejestruje kontrolki asynchronicznie – w testach `await fixture.whenStable()`).
- [ ] **5.3** `ng test --coverage` (progi), `ng build`, wpis „Co nowego”, commit.

### Task 6: Weryfikacja końcowa i wdrożenie (zgoda na push)

- [ ] **6.1** `dotnet test`, `ng test --coverage`, `ng build` – wszystko zielone.
- [ ] **6.2** Zapytaj użytkownika o zgodę na push; po zgodzie push, obserwacja `Deploy`/CI, weryfikacja produkcji: `/health`, `/health/ready` (potwierdza start po migracjach), endpointy bez tokena → 401.
- [ ] **6.3** Zaktualizuj pamięć projektu (`project_status.md`: Faza 5 i 6 pushed, Faza 7 status), skoryguj dokument `docs/operations.md` o notatkę: powiązanie konta z osobą oraz uwaga o proxy dla `az monitor app-insights query`.
