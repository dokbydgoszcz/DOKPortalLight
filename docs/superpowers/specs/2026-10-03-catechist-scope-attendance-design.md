# Faza 7 – zakres katechisty i obecność – projekt

Data: 2026-10-03

## Cel

1. Katechista prowadzący widzi i edytuje **tylko swoich podopiecznych**: sprawy DOK, ich dokumenty, notatki i spotkania.
2. Obecność na spotkaniu ustawia się jednym kliknięciem na liście.
3. Na sprawie DOK widać podsumowanie frekwencji (np. 4 z 5, 80%).
4. Zajęcia grupowe mają listę uczestników z obecnością każdej osoby.

## Stan wyjściowy

- Uprawnienia są nadawane rolą (`RolePermissions`); żaden endpoint nie sprawdza, czyja jest sprawa. Każdy katechista widzi wszystko.
- Sprawa DOK ma `CatechistPersonId` (wymagane). Konto użytkownika ma opcjonalne `AppUser.PersonId` (powiązanie ustawia administrator w ekranie użytkowników).
- Spotkanie: `DokCaseId?`, `GroupLabel?`, `IsAttended?` – obecność jednym polem; zajęcia grupowe to tylko tekstowa etykieta.
- Wzorzec do naśladowania: `PastoralNotes.ReadAll` (kto ma, widzi wszystkich autorów; pozostali tylko własne).
- `DbSeeder.SeedRolePermissionsAsync` wypełnia domyślne uprawnienia tylko przy pustej tabeli – nowe uprawnienie dla istniejących ról wymaga migracji danych.

## Decyzje (zatwierdzone)

- Zakres wierszowy jako uprawnienie `DokCases.ViewAll` w macierzy ról. Domyślnie mają je wszystkie role, które dziś mają `DokCases.View`, **oprócz** `KatechistaProwadzacy`. Superwizor zachowuje pełny podgląd.
- W zakresie: szybki przełącznik obecności, podsumowanie frekwencji oraz obecność grupowa.

## Etap 1 – zakres wierszowy

**Uprawnienie:** `Permissions.DokCasesViewAll = "DokCases.ViewAll"` (katalog: „Sprawy DOK”, „Podgląd wszystkich spraw DOK i powiązanych dokumentów, notatek i spotkań”). Dodane do `DefaultRolePermissions` ról z `DokCases.View` poza katechistą. Frontend: stała w `permissions.ts`, wiersz w macierzy pojawia się z katalogu.

**Migracja danych** (EF, raz): dla każdej roli z `DokCases.View`, poza `KatechistaProwadzacy`, która nie ma jeszcze `DokCases.ViewAll` – dodać wiersz `RolePermissions`. Zachowuje obecne zachowanie ról własnych. Down: usunięcie wierszy `DokCases.ViewAll`.

**Dostawca zakresu:** `CaseScope(bool ViewAll, Guid? PersonId)` (Application) i `ICaseScopeProvider.GetAsync(ct)`. Implementacja HTTP (Api): `ViewAll` = polityka `DokCases.ViewAll` zaliczona dla użytkownika; `PersonId` czytany z bazy (`AppUser.PersonId` po `sub`), a nie z tokenu – bez opóźnienia po zmianie powiązania. Brak zalogowanego użytkownika lub brak `PersonId` bez `ViewAll` = pusty zakres (nic nie widać). Domyślna implementacja `AllCasesScopeProvider` (bez ograniczeń) jako opcjonalny parametr konstruktora serwisów, żeby istniejące testy serwisów i serwisy systemowe (przypomnienia) działały bez zmian. Rejestracja w DI: dostawca HTTP.

**Filtr:** `IQueryable<DokCase>.ForScope(CaseScope)` – `ViewAll` → bez zmian; inaczej `CatechistPersonId == PersonId`; brak `PersonId` → zawsze fałsz.

**Serwisy objęte zakresem:**
- `DokCaseService`: `SearchAsync`, `GetByIdAsync`, `UpdateAsync`, `DeleteAsync` (nieodnaleziona = niedostępna → 404). `SearchGraduatesAsync` bez zakresu (osobne uprawnienie `Graduates.View`).
- `CaseDocumentService`: wszystkie operacje sprawdzają dostęp do sprawy; poza zakresem zachowują się jak sprawa nieistniejąca (listing pusty, pozostałe `null`).
- `PastoralNoteService`: odczyt i tworzenie wymagają dostępu do sprawy (poza tym zasada „autor widzi własne” bez zmian).
- `MeetingService`: lista/odczyt/zapis/usunięcie według widoczności (Etap 3 doprecyzowuje zajęcia grupowe).
- `DashboardService`: liczniki spraw, brakujących dokumentów i spotkań liczone w zakresie; osoby, parafie, kandydaci bez zmian.
- `ExportService`: eksport spraw DOK i spotkań w zakresie.
- Bez zakresu: mailing, przypomnienia (usługi systemowe), absolwenci.

**Konto bez powiązanej osoby:** katechista bez `PersonId` widzi puste listy. Dokumentacja (runbook/odpowiedź na zgłoszenia): powiązanie konta z osobą ustawia administrator w ekranie użytkowników.

**Tworzenie spraw:** bez dodatkowego sprawdzenia zakresu (uprawnienie `DokCases.Manage` mają role zarządzające).

## Etap 2 – szybki przełącznik i frekwencja

**API:** `PUT /api/meetings/{id}/attendance` z `{ "isAttended": true | false | null }` (uprawnienie `Meetings.Manage`, w zakresie) → `MeetingDto`; 404 dla niewidocznego lub nieistniejącego.

**Frekwencja na sprawie:** `DokCaseDto` dostaje `MeetingsRecorded` i `MeetingsAttended` (int, domyślnie 0). Liczone zbiorczo dla spraw bieżącej strony: spotkania indywidualne sprawy z ustawioną obecnością oraz (po Etapie 3) wiersze uczestników zajęć grupowych. Procent wylicza frontend.

**Frontend:** lista spotkań – przyciski „Obecny / Nieobecny” (klik w aktywny = wyczyść) widoczne z `Meetings.Manage`; lista spraw DOK – kolumna „Frekwencja” („4/5 (80%)”, „—” gdy brak zapisanych spotkań).

## Etap 3 – obecność grupowa

**Model:** `Meeting.CatechistPersonId` (`Guid?`, właściciel zajęć grupowych) oraz tabela `MeetingAttendee` (`Id`, `MeetingId`, `DokCaseId`, `IsAttended?`, unikalny indeks `MeetingId + DokCaseId`, usuwanie kaskadowe po spotkaniu). Migracja EF.

**Reguły:**
- Spotkanie jest albo indywidualne (`DokCaseId`), albo grupowe (lista uczestników), nie oba naraz – walidacja żądania (400). Spotkanie bez sprawy i bez uczestników (stare zajęcia „tylko etykieta”) pozostaje dozwolone.
- Widoczność: `ViewAll` lub sprawa spotkania należy do użytkownika lub `Meeting.CatechistPersonId == PersonId`.
- Tworzenie zajęć grupowych: bez `ViewAll` właścicielem jest `PersonId` użytkownika, a wszyscy uczestnicy muszą być jego podopiecznymi (inaczej 400); z `ViewAll` właściciel = `PersonId` użytkownika, o ile ma.
- `CreateMeetingRequest`: `Attendees` (`DokCaseId`, `IsAttended?`); `MeetingDto`: `Attendees` (`DokCaseId`, `PersonFullName`, `IsAttended?`).
- `PUT /api/meetings/{id}/attendees/{caseId}` z `{ "isAttended": ... }` – przełącznik obecności uczestnika (w zakresie, 404 dla obcych).

**Frontend:** formularz spotkania – przełącznik „Zajęcia grupowe” z wyborem uczestników (lista dostępnych spraw z polem wyboru); lista spotkań – wiersz zajęć grupowych rozwija uczestników z przyciskami obecności.

## Testy

- Serwisy: filtr zakresu (ViewAll, własne, cudze, brak `PersonId`), każdy serwis z punktu „Serwisy objęte zakresem”, migracja danych (nowe uprawnienie nadane właściwym rolom), frekwencja, przełączniki, obecność grupowa (walidacje, widoczność, kaskada).
- Integracyjne: dwóch katechistów z osobnymi podopiecznymi – każdy widzi i edytuje tylko swoje (sprawy, dokumenty, upload/download, notatki, spotkania, przełączniki); Administrator i Superwizor widzą wszystko; konto bez `PersonId` widzi puste listy; brak dostępu = 404.
- Frontend: przełącznik obecności, kolumna frekwencji, formularz zajęć grupowych, pozycja w macierzy uprawnień.
- Istniejące testy z rolą katechisty wymagają powiązania konta z osobą – aktualizowane razem z zakresem.

## Poza zakresem

Przypisywanie superwizora do spraw; raporty frekwencji zbiorcze; powiadomienia o nieobecnościach; zakres wierszowy dla mailingu i spisu absolwentów.

## Kolejność i wdrożenie

Etap 1 → Etap 2 → Etap 3, każdy osobnym commitem lokalnym z testami. Push i wdrożenie po zgodzie użytkownika (migracje wykonują się przy starcie aplikacji). Wpis w „Co nowego” po każdym etapie widocznym dla testerów.
