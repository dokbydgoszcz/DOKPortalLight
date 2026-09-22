# DOK Portal Light — Faza 1: Fundament — Design

Data: 2026-09-22
Status: zaakceptowany do implementacji

## Kontekst i cel

DOK Portal Light to docelowo aplikacja CRM obsługująca dwie diecezjalne
instytucje formacyjne — Szkołę Katechistów św. Pawła (SKŚP) oraz
Diecezjalny Ośrodek Katechumenalny (DOK) — na bazie wspólnego rejestru
osób i systemu RBAC, w którym jedna osoba może pełnić wiele ról i mieć
wiele powiązań instytucjonalnych jednocześnie.

Punktem wyjścia jest zaakceptowany prototyp HTML (`preview.html`)
opisujący pełny zakres funkcjonalny docelowego produktu. Ze względu na
rozmiar tego zakresu, budowa została podzielona na cztery fazy:

1. **Fundament** (ten dokument) — repo, CI/CD, baza danych, RBAC,
   szkielet Angulara, Dashboard + Baza osób.
2. Moduł SKŚP.
3. Moduł DOK.
4. Narzędzia wspólne (generator pism, mailing, imieniny, audit log).

Ten spec obejmuje **wyłącznie Fazę 1**. Kolejne fazy dostaną własne
specy budowane na tym fundamencie.

## Cele Fazy 1

- Repozytorium GitHub gotowe do łatwego wdrożenia (CI buduje i testuje,
  CD wdraża na push do `main`).
- Realna baza danych Azure SQL zarządzana przez EF Core (code-first,
  migracje), zastępująca dane demo z prototypu.
- Prawdziwe logowanie (login + hasło, JWT) i RBAC z rolami:
  Administrator, Biskup/Wikariusz, Dyrektor SKŚP, Dyrektor DOK,
  Superwizor, Katechista prowadzący — jedna osoba może mieć wiele ról
  jednocześnie.
- Wspólny rejestr osób (`Person`) będący fundamentem, na którym Fazy 2
  i 3 zbudują dane specyficzne dla SKŚP i DOK.
- Działający szkielet UI Angular odwzorowujący layout prototypu
  (sidebar, topbar, karty, tabele, modale) — bez fikcyjnych danych,
  wszystko podłączone do prawdziwego API.
- Strony gotowe funkcjonalnie w tej fazie: logowanie, Dashboard, Baza
  osób (pełne CRUD), panel administracji użytkowników/ról.

## Poza zakresem Fazy 1

- Dane i logika SKŚP (kandydaci, misje kanoniczne, formatorzy,
  parafie/giełda, budżet SKŚP) — Faza 2.
- Dane i logika DOK (podopieczni, ścieżki formacyjne, harmonogram,
  superwizje, absolwenci, budżet DOK) — Faza 3.
- Generator pism, mailing, kalendarz imienin, audit log — Faza 4.
- Realne prowizjonowanie zasobów Azure (App Service, Static Web Apps,
  Azure SQL) — użytkownik zakłada je samodzielnie i dostarcza sekrety
  do GitHub Actions; ten spec przygotowuje wyłącznie kod i workflowy.

## Architektura repozytorium

Monorepo na GitHub:

```
/backend
  /src
    DokPortal.Domain          — encje domenowe, brak zależności zewnętrznych
    DokPortal.Application     — logika aplikacyjna, DTO, interfejsy
    DokPortal.Infrastructure  — EF Core, DbContext, migracje, implementacje
    DokPortal.Api             — kontrolery, konfiguracja, Program.cs
  /tests
    DokPortal.Application.Tests
    DokPortal.Api.IntegrationTests
/frontend
  /src/app
    /core       — auth, interceptory, guardy, serwisy współdzielone
    /layout     — sidebar, topbar, shell
    /features
      /dashboard
      /people
      /admin-users
    /shared     — komponenty UI wielokrotnego użytku (karty, tabele, modale)
/.github/workflows
  backend-ci.yml
  frontend-ci.yml
  deploy.yml
/docs/superpowers/specs
```

## Model danych

### Encje w Fazie 1

- **Person** — `Id (Guid)`, `FirstName`, `LastName`, `Email`, `Phone`,
  `BirthDate?`, `ParishId? (FK Parish)`, `Notes?`, `CreatedAtUtc`,
  `UpdatedAtUtc`. To wspólny rekord dla obu instytucji — Fazy 2/3 będą
  dopisywać własne tabele z FK `PersonId`, nigdy nie duplikując danych
  osobowych.
- **Parish** — `Id (Guid)`, `Name`, `City?`.
- **AppUser : IdentityUser** — konto logowania (login = email, hasło
  hashowane przez Identity). Pole `PersonId? (FK Person)` łączy konto
  z rekordem osoby (część kont administracyjnych może nie mieć
  odpowiednika w `Person`, np. techniczne konto Administratora).
- **IdentityRole** (standardowe, seedowane): `Administrator`, `Biskup`,
  `DyrektorSKSP`, `DyrektorDOK`, `Superwizor`, `KatechistaProwadzacy`.
  Relacja użytkownik↔rola jest wiele-do-wielu (wbudowane w Identity) —
  to bezpośrednio realizuje wymóg wielu ról na osobę.

### Ważne rozróżnienie: role RBAC vs role biznesowe

Role Identity (`Administrator`, `Biskup`, ...) kontrolują **dostęp do
funkcji systemu** (autoryzacja API/UI). Role biznesowe widoczne w
prototypie w kolumnie „Role / powiązania" (Kandydat SKŚP, Katechista
posłany, Formator, Podopieczny DOK...) to **stan danych domenowych**,
nie autoryzacja — będą reprezentowane przez osobne tabele modułowe
dodane w Fazach 2–3 (`Candidate`, `CanonicalMission`, `DokCase`, ...),
każda z FK do `PersonId`. Widok „Role / powiązania" w Bazie osób to
zapytanie agregujące te tabele, nie kolumna w `Person`.

### Migracje

Code-first EF Core. Migracja startowa tworzy tabele Identity + Person
+ Parish. W Fazie 1 migracje są stosowane automatycznie przy starcie
API (`Database.Migrate()` w `Program.cs`) — uzasadnione skalą aplikacji
(mała, wewnętrzna); można to zmienić na oddzielny krok w CD później,
jeśli zajdzie potrzeba.

### Dane startowe (seed)

Przy pierwszym uruchomieniu na pustej bazie:
- jedno konto `Administrator` (dane logowania z konfiguracji/sekretu,
  nie zahardkodowane hasło),
- kilka przykładowych parafii,
- brak fikcyjnych osób/danych demo — baza startuje pusta poza kontem
  administratora, żeby nie mylić danych testowych z prawdziwymi.

## Autentykacja i autoryzacja

- Logowanie: `POST /api/auth/login` (email + hasło) → JWT (access
  token; bez refresh tokenu w tej fazie — uzasadnione prostotą „Light"
  i małą skalą; można dodać później, jeśli sesje 1-dniowe okażą się za
  krótkie).
- Token zawiera `sub` (UserId), role Identity użytkownika oraz opcjonalnie
  `personId`.
- Kontrolery zabezpieczone `[Authorize]` / `[Authorize(Roles = "...")]`
  zgodnie z regułami widoczności z prototypu (`applyRole()` przeniesione
  na backend jako reguły autoryzacji, nie tylko UI).
- Frontend: `AuthInterceptor` dołącza JWT do zapytań, `authGuard` chroni
  trasy, serwis `AuthService` przechowuje token (w pamięci +
  `localStorage` dla przetrwania odświeżenia strony) i dekoduje role do
  sterowania widocznością menu.

## API (zakres Fazy 1)

| Kontroler | Endpointy | Dostęp |
|---|---|---|
| AuthController | `POST /login` | anonimowy |
| PeopleController | `GET /people` (search/filter/paging), `GET /people/{id}`, `POST /people`, `PUT /people/{id}` | zalogowani; edycja: Administrator, DyrektorSKSP, DyrektorDOK |
| ParishesController | `GET /parishes`, `POST /parishes` | zalogowani; zapis: Administrator |
| UsersController | `GET /users`, `POST /users`, `PUT /users/{id}/roles` | Administrator |
| DashboardController | `GET /dashboard/summary` | zalogowani |

`DashboardController` w Fazie 1 zwraca wyłącznie liczniki, które mają
realne źródło danych (np. liczba osób, liczba parafii). Sekcje
prototypu bez jeszcze istniejących danych („Sprawy wymagające uwagi",
„Najbliższe wydarzenia", liczniki SKŚP/DOK) renderują się jako pusty
stan z komunikatem, że dane pojawią się po wdrożeniu kolejnych faz —
zamiast pokazywać fikcyjne liczby z prototypu.

## Frontend

- Angular (najnowsza stabilna wersja), samodzielne (standalone)
  komponenty, sygnały (`signal`/`computed`) do stanu lokalnego zamiast
  NgRx — uzasadnione rozmiarem aplikacji.
- Warstwa wizualna: zmienne CSS, siatka layoutu, komponenty kart/tabel/
  modali przeniesione 1:1 z `preview.html` do globalnych stylów i
  komponentów wspólnych (`shared`), zachowując wygląd zaakceptowanego
  prototypu.
- Przełącznik roli z prototypu (`<select id="roleSelect">`) **znika** —
  zastępuje go rzeczywisty zalogowany użytkownik: topbar pokazuje jego
  role jako odznaki (pill), a widoczność pozycji menu bocznego wynika z
  ról w tokenie JWT (logika równoważna `applyRole()`, ale sterowana
  danymi, nie ręcznym wyborem).
- Strony w tej fazie: `/login`, `/dashboard`, `/people` (lista + modal
  dodawania/edycji + widok profilu), `/admin/users` (tylko
  Administrator: lista kont, przypisywanie ról).

## CI/CD

- `backend-ci.yml` — na każdy push/PR: `dotnet restore/build/test`.
  Testy integracyjne używają EF Core InMemory lub SQLite, nigdy
  prawdziwego Azure SQL.
- `frontend-ci.yml` — na każdy push/PR: `npm ci`, `ng lint`, `ng build`,
  `ng test` (Karma/Jasmine, domyślne dla Angular CLI).
- `deploy.yml` — na push do `main`: publikacja backendu do Azure App
  Service (`azure/webapps-deploy`) oraz build+wdrożenie Angulara do
  Azure Static Web Apps. Wymagane sekrety w repo GitHub (do dodania
  przez użytkownika po utworzeniu zasobów w Azure):
  `AZURE_WEBAPP_PUBLISH_PROFILE`, `AZURE_STATIC_WEB_APPS_API_TOKEN`,
  `SQL_CONNECTION_STRING` (jako App Setting backendu), `JWT_SIGNING_KEY`.
  Bez tych sekretów workflow CD jest gotowy, ale nieaktywny — zostanie
  to jasno opisane w README.

## Testowanie

- Backend: xUnit. Testy jednostkowe dla logiki aplikacyjnej (walidacja,
  mapowanie), testy integracyjne kontrolerów przez
  `WebApplicationFactory` z bazą w pamięci/SQLite.
- Frontend: testy komponentów i serwisów Karma/Jasmine (domyślne
  Angular CLI) dla logiki krytycznej (guardy, interceptor, serwis
  auth, filtrowanie/wyszukiwanie osób).
- Weryfikacja manualna: uruchomienie API + Angular lokalnie, przejście
  golden path (logowanie → dashboard → dodanie osoby → edycja →
  wylogowanie) w przeglądarce przed uznaniem fazy za zakończoną.

## Ryzyka i założenia

- **Założenie:** użytkownik samodzielnie utworzy zasoby Azure (App
  Service, Static Web App, Azure SQL) i doda sekrety do repo — bez tego
  krok CD w pipeline nie zadziała, ale build/testy (CI) będą działać od
  razu.
- **Założenie:** migracje stosowane automatycznie przy starcie API są
  akceptowalne na tym etapie (mała, wewnętrzna aplikacja); do
  rewizji, jeśli w przyszłości pojawi się potrzeba kontrolowanych okien
  wdrożeniowych.
- **Ryzyko:** brak refresh tokenu oznacza, że sesje wygasają wraz z
  access tokenem — akceptowalne dla Fazy 1, do rozważenia w kolejnych
  fazach jeśli czas życia tokenu (np. 8h) okaże się niewystarczający.
