# Uprawnienia oparte o polityki, edytowalne w bazie — projekt

Data: 2026-10-02. Faza 4 z planu dojrzewania systemu.

## Cel

Zastąpić ~37 zahardkodowanych `[Authorize(Roles = "...")]` w kontrolerach, zduplikowane listy ról we frontendzie (`nav-items.ts`, `export-lists.ts`) oraz `User.IsInRole(...)` w `PastoralNotesController` jednym modelem: **uprawnienia nazwane `Moduł.Akcja`, przypisywane rolom w bazie i edytowane przez Administratora na ekranie „Uprawnienia ról"**.

## Decyzje (zatwierdzone)

- Model: uprawnienia w bazie + ekran admina; katalog uprawnień w kodzie, claimy `permission` w JWT dla frontendu.
- Zakres: refaktor **z ujednoliceniem** — odczyty (GET), dziś otwarte dla każdego zalogowanego, są zaostrzone do widoczności w menu (tabela niżej); słowniki zostają otwarte.
- Administrator zawsze ma wszystkie uprawnienia, nie jest edytowalny i nie jest zapisany w tabeli (ochrona przed zablokowaniem się z systemu).
- Poza zakresem: ograniczanie wierszy (np. katechista widzi tylko swoich podopiecznych), zarządzanie rolami/użytkownikami (istnieje), zmiana modelu logowania.

## Rozbicie na trzy części (każda z osobnym planem)

1. **Rdzeń backendu** — katalog, tabela, serwis z cache, polityki, claimy w JWT, konwersja kontrolerów, testy.
2. **Frontend** — `hasPermission`, menu, przyciski eksportu, guardy tras sterowane uprawnieniami.
3. **Ekran „Uprawnienia ról"** — macierz rola × uprawnienie, zapis, audyt.

Kolejność: 1 → 2 → 3. Po części 1 system działa poprawnie na backendzie, a frontend nadal korzysta z ról (claimy `role` zostają w tokenie).

## Część 1 — rdzeń backendu

### Katalog uprawnień (`Domain`)

`Domain/Constants/Permissions.cs` — stałe `const string` w formacie `Moduł.Akcja` oraz `PermissionCatalog.All : IReadOnlyList<PermissionInfo>` (`Name`, `Module`, `Label` po polsku — etykiety trafią na ekran admina).

| Moduł | Uprawnienia |
|---|---|
| People | `People.Manage`, `People.Export` |
| Parishes | `Parishes.Manage`, `Parishes.Export` |
| Candidates | `Candidates.View`, `Candidates.Manage`, `Candidates.Export` |
| Missions | `Missions.View`, `Missions.Manage`, `Missions.Export` |
| Formators | `Formators.View`, `Formators.Manage`, `Formators.Export` |
| ParishNeeds | `ParishNeeds.View`, `ParishNeeds.Manage` |
| BudgetSksp | `BudgetSksp.View`, `BudgetSksp.Manage` |
| BudgetDok | `BudgetDok.View`, `BudgetDok.Manage` |
| DokCases | `DokCases.View`, `DokCases.Manage`, `DokCases.Export` |
| CaseDocuments | `CaseDocuments.View`, `CaseDocuments.Manage` |
| PastoralNotes | `PastoralNotes.View`, `PastoralNotes.Write`, `PastoralNotes.ReadAll` |
| Meetings | `Meetings.View`, `Meetings.Manage`, `Meetings.Export` |
| Supervisions | `Supervisions.View`, `Supervisions.Manage`, `Supervisions.Export` |
| Documents | `Documents.View`, `Documents.Generate` |
| Mailing | `Mailing.View`, `Mailing.Manage` |
| Graduates | `Graduates.View` (tylko widoczność w UI — dane pochodzą z `DokCases.View`) |
| Users | `Users.Manage` |
| AuditLog | `AuditLog.View` |
| Permissions | `Permissions.Manage` |

`Manage` = tworzenie, edycja i usuwanie (u mailingu także wysyłka; u dokumentów spraw także upload). Łącznie 41 uprawnień. W kodzie stałe są płaskie (`Permissions.PeopleManage` = `"People.Manage"`), bo C# nie pozwala na klasę zagnieżdżoną o nazwie otaczającej klasy.

### Domyślne przydziały (seed) i różnice względem dziś

Skróty: S = DyrektorSKSP, D = DyrektorDOK, V = Superwizor, K = KatechistaProwadzacy, B = Biskup. Administrator ma wszystko implicit.

| Uprawnienie | Role | Zmiana względem dziś |
|---|---|---|
| People.Manage, People.Export | S, D | bez zmian |
| Parishes.Manage, Parishes.Export | — (tylko Admin) | bez zmian |
| Candidates.View / Manage / Export | S | **View zaostrzone** (dziś czyta każdy zalogowany) |
| Missions.View | S, B | **zaostrzone** (zgodne z menu) |
| Missions.Manage / Export | S | bez zmian |
| Formators.View / Manage / Export | S | **View zaostrzone** |
| ParishNeeds.View / Manage | S | **View zaostrzone** |
| BudgetSksp.View / Manage | S | **View zaostrzone** (dziś budżet widzi każdy zalogowany) |
| BudgetDok.View | D | **zaostrzone** |
| BudgetDok.Manage | D | **zmiana**: dziś zapisuje S (obie kasy), D nie może — przy zatwierdzeniu S traci zapis do budżetu DOK, D go zyskuje |
| DokCases.View | D, V, K, B | **zaostrzone** (S traci odczyt przez API; menu go i tak nie pokazywało) |
| DokCases.Manage / Export | D | bez zmian |
| CaseDocuments.View | D, V, K, B | **zaostrzone** |
| CaseDocuments.Manage | D, K | bez zmian |
| PastoralNotes.View | D, K | **zaostrzone** (dziś każdy; V i B nie mogą pisać notatek, więc nie mają własnych) |
| PastoralNotes.Write | D, K | bez zmian |
| PastoralNotes.ReadAll | D | zastępuje `IsInRole(Administrator) \|\| IsInRole(DyrektorDOK)`; Administrator implicit |
| Meetings.View / Manage | D, K | `View` zaostrzone |
| Meetings.Export | D | bez zmian |
| Supervisions.View / Manage / Export | S, D, V | `View` zaostrzone |
| Documents.View / Generate | S, D | `View` zaostrzone |
| Mailing.View / Manage | S, D | `View` zaostrzone |
| Graduates.View | D | bez zmian (menu) |
| Users.Manage, AuditLog.View, Permissions.Manage | — (tylko Admin) | bez zmian |

Pozostają otwarte dla każdego zalogowanego (zwykłe `[Authorize]`, bez uprawnienia): lista osób i szczegóły (`GET api/people`), lista parafii (`GET api/parishes`), imieniny, dashboard (same agregaty), `GET api/auth/*`. To słowniki potrzebne w formularzach wszystkich ról.

### Baza

- Encja `RolePermission { string RoleName; string Permission }`, klucz złożony `(RoleName, Permission)`, tabela `RolePermissions`, migracja `AddRolePermissions`.
- Seed w `DbSeeder`: jeśli tabela jest pusta, wstawia domyślne przydziały z tabeli wyżej (bez Administratora). Seed jest idempotentny i nie nadpisuje edycji admina. Uprawnienia dodane w przyszłych wersjach Administrator ma od razu (implicit), a pozostałe role dostają je dopiero z ekranu.

### Serwis (`Application` + `Infrastructure`)

`IPermissionService`:
- `Task<IReadOnlySet<string>> GetPermissionsForRolesAsync(IEnumerable<string> roles, CancellationToken ct)` — jeśli wśród ról jest Administrator, zwraca cały katalog; w przeciwnym razie sumę przydziałów ról.
- `Task<PermissionMatrixDto> GetMatrixAsync(CancellationToken ct)` — katalog + przydziały wszystkich edytowalnych ról (bez Administratora).
- `Task UpdateRolePermissionsAsync(string role, IReadOnlyCollection<string> permissions, CancellationToken ct)` — waliduje: rola istnieje w `AppRoles.All` i nie jest Administratorem, każda nazwa jest w katalogu; zastępuje przydziały roli i czyści cache.

Mapa przydziałów jest cache'owana w `IMemoryCache` (jeden wpis, TTL 60 s, czyszczony po zapisie). Backend jest autorytatywny: zmiana przydziałów działa na kolejnym żądaniu (najpóźniej po 60 s, gdyby działało wiele instancji).

### Autoryzacja (`Api`)

- `PermissionRequirement(string permission)` + `PermissionAuthorizationHandler` — czyta role z `User`, pyta `IPermissionService`.
- `PermissionPolicyProvider : IAuthorizationPolicyProvider` — tworzy politykę dla każdej nazwy z katalogu (`policy name == permission name`); pozostałe nazwy deleguje do domyślnego providera.
- `HasPermissionAttribute : AuthorizeAttribute` (`[HasPermission(Permissions.People.Manage)]`) ustawia `Policy`.
- Budżet (jeden endpoint z parametrem `fund`) sprawdza uprawnienie imperatywnie przez `IAuthorizationService.AuthorizeAsync(User, null, policyName)`, gdzie `policyName` = `BudgetSksp.*` lub `BudgetDok.*` zależnie od `fund`; przy odmowie `Forbid()`. Usuwanie wpisu odczytuje fundusz z istniejącego wpisu (nowa metoda `IBudgetService.GetFundAsync(id)`; brak wpisu → `NotFound`).
- `PastoralNotesController.GetAll` używa `IAuthorizationService` (`PastoralNotes.ReadAll`) zamiast `IsInRole`.
- `RemindersController` zostaje przy kluczu API (bez zmian).

### JWT

`AuthController.Login` po rolach wylicza uprawnienia przez `IPermissionService` i dodaje po jednym claimie `permission` na uprawnienie (obok istniejących claimów `role`). Claimy służą tylko UI — backend nie ufa im przy autoryzacji, tylko tabeli.

### Testy części 1

- `PermissionServiceTests` (InMemory): Administrator dostaje cały katalog; suma ról; zapis zastępuje przydziały i czyści cache; odrzuca Administratora, nieznaną rolę i nieznane uprawnienie.
- Test parytetu seeda: tabela oczekiwanych domyślnych przydziałów (powyższa) kontra faktyczny seed.
- Testy integracyjne istniejących kontrolerów: zachowują się bez zmian dla zapisów; testy odczytów aktualizowane zgodnie z zaostrzeniem (nowe przypadki: katechista dostaje 403 na `GET api/candidates`, `api/budget?fund=SKSP`, `api/formators`, `api/missions`, `api/parish-needs`; dyrektor DOK ma 200 na `api/budget?fund=DOK` i 403 na `fund=SKSP`).
- Test, że po `UpdateRolePermissionsAsync` zmiana działa od razu w żądaniu HTTP.
- Test JWT: token zawiera claimy `permission` zgodne z rolą.

## Część 2 — frontend

- `AuthService`: `permissions` (z claimów `permission`; pojedynczy claim jest stringiem, wiele to tablica — obsłużyć oba kształty), `hasPermission(p)`, `hasAnyPermission(ps)`.
- `nav-items.ts`: pole `permission?: string` zamiast `roles: string[]`; pozycje bez `permission` widoczne dla wszystkich zalogowanych (Dashboard, Baza osób, Kalendarz imienin). Mapowanie: Dokumenty → `Documents.View`; Mailing → `Mailing.View`; Kandydaci → `Candidates.View`; Katechiści posłani → `Missions.View`; Formatorzy → `Formators.View`; Parafie i giełda → `ParishNeeds.View`; Rejestr parafii → `Parishes.Manage`; Budżet SKŚP → `BudgetSksp.View`; Podopieczni DOK → `DokCases.View`; Harmonogram → `Meetings.View`; Superwizje → `Supervisions.View`; Absolwenci → `Graduates.View`; Budżet DOK → `BudgetDok.View`; Użytkownicy i role → `Users.Manage`; Audit log → `AuditLog.View`.
- `export-lists.ts`: `roles` → `permission` (`People.Export` itd.); `ExportButtonComponent` używa `hasPermission`.
- `permissionGuard(permission)` na trasach w `app.routes.ts` (przekierowanie na `/dashboard` przy braku uprawnienia).
- Przyciski „Dodaj/Edytuj/Usuń" na listach ukryte bez odpowiedniego `*.Manage`.
- Wpis w „Co nowego".

## Część 3 — ekran „Uprawnienia ról"

- Backend: `PermissionsController` (`api/permissions`), oba endpointy z `[HasPermission(Permissions.PermissionsManage)]`: `GET matrix` (`{ roles: string[], permissions: [{ name, module, label }], grants: { [rola]: string[] } }`) oraz `PUT roles/{role}` (`{ permissions: string[] }`); zapis loguje do audytu akcję `UpdateRolePermissions` z opisem roli i różnicą (dodane/odebrane).
- Frontend: komponent `permissions-matrix`, trasa `/admin/permissions` (`Permissions.Manage`), pozycja „Uprawnienia ról" w menu. Tabela: wiersze = uprawnienia pogrupowane po module, kolumny = role (bez Administratora), checkboxy, przycisk „Zapisz" per rola lub dla całości, toast po zapisie, informacja, że zmiany w menu użytkownika widać po ponownym zalogowaniu.
- Testy: kontroler (200 dla Admina, 403 dla innych, walidacja nieznanej roli/uprawnienia, wpis audytu), spec komponentu.

## Ryzyka i uwagi

- Zaostrzenie odczytów może odciąć dane, których formularze jakiejś roli używają jako słownika. Z analizy: formularze Osób/Kandydatów/Misji/Formatorów używają listy osób i parafii (otwarte); formularz Spotkań używa spraw DOK (`DokCases.View` mają D i K). Przed wdrożeniem części 1 sprawdzić każdy ekran na wywołania `GET` spoza własnego modułu roli.
- Dashboard pokazuje agregaty DOK/SKŚP każdej roli — świadomie zostaje otwarty (same liczby).
- Tokeny wydane przed wdrożeniem nie mają claimów `permission` — frontend (część 2) musi poradzić sobie z ich brakiem (wylogowanie przy braku claimów lub fallback), więc części 1 i 2 wdrażamy razem lub 2 tuż po 1.
- Backend dodatkowo zachowuje claimy `role`, więc rollback części 2 jest bezpieczny.
