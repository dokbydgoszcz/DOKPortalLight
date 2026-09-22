# DOK Portal Light — Faza 2: Moduł SKŚP — Design

Data: 2026-09-22
Status: zaakceptowany do implementacji

## Kontekst i cel

Faza 1 (Fundament) dostarczyła wspólny rejestr osób (`Person`/`Parish`),
RBAC (JWT + role Identity) i szkielet Angulara. Faza 2 dodaje dane i
logikę specyficzne dla Szkoły Katechistów św. Pawła (SKŚP), zgodnie z
zakresem opisanym w prototypie (`preview.html`): kandydaci w formacji
(I–III rok), katechiści posłani (misje kanoniczne), formatorzy, giełda
posługi parafialnej oraz budżet SKŚP.

## Cele Fazy 2

- Rejestr kandydatów SKŚP powiązanych z `Person`, z rokiem formacji,
  frekwencją, liczbą zebranych opinii i statusem rekolekcji.
- Rejestr katechistów posłanych z misjami kanonicznymi (miejsce
  posługi, okres ważności, data/miejsce udzielenia, grupa
  superwizyjna) — status ważności liczony dynamicznie z dat, nigdy
  nie przechowywany.
- Rejestr formatorów SKŚP (funkcja + dane kontaktowe z `Person`).
- Giełda posługi: zapotrzebowania parafii + ręczne skierowanie
  katechisty do potrzeby.
- Budżet SKŚP: rejestr operacji przychodów/wydatków z kategoriami,
  współdzielony schemat z przyszłym budżetem DOK (Faza 3) przez pole
  `Fund`.
- Strony Angular dla wszystkich pięciu obszarów, z widocznością menu
  zgodną z oryginalną logiką `applyRole()` z prototypu.

## Poza zakresem Fazy 2

- Realne rekordy obecności/opinii z autorami i datami — frekwencja i
  liczba opinii to proste pola liczbowe wprowadzane ręcznie.
- Algorytm dopasowywania katechistów do potrzeb parafii — tylko lista
  potrzeb i ręczna akcja „Skieruj”.
- Prawdziwa encja grupy superwizyjnej — `SupervisionGroup` to string na
  misji; Faza 4 (Superwizje) może ją później zastąpić encją bez zmiany
  kontraktu zewnętrznego.
- Moduł DOK (Faza 3) i narzędzia wspólne: generator pism, mailing,
  imieniny, audit log (Faza 4).

## Model danych

Wszystkie nowe encje żyją w `DokPortal.Domain.Entities` i mają FK do
istniejących `Person`/`Parish` z Fazy 1 — żadna dana osobowa nie jest
duplikowana.

- **Candidate** — `Id`, `PersonId` (FK), `Year` (int, 1–3),
  `AttendancePercentage` (int?, 0–100), `OpinionsCollected` (int),
  `OpinionsRequired` (int, domyślnie 2), `IsRetreatCompleted` (bool),
  `CreatedAtUtc`, `UpdatedAtUtc`.
- **CanonicalMission** — `Id`, `PersonId` (FK), `ServicePlace` (string),
  `MissionStartDate` (DateOnly), `MissionEndDate` (DateOnly),
  `GrantedDate` (DateOnly?), `GrantedPlace` (string?),
  `SupervisionGroup` (string?), `CreatedAtUtc`, `UpdatedAtUtc`. Status
  („ważna" / „wygasa ≤30 dni" / „wygasła") liczony w API z
  `MissionEndDate` względem bieżącej daty, nie przechowywany.
- **Formator** — `Id`, `PersonId` (FK), `Function` (string, np.
  „Referent SKŚP").
- **ParishNeed** — `Id`, `ParishId` (FK), `Description` (string),
  `Status` (enum: `Open`, `Assigned`, `Closed`), `AssignedPersonId`
  (Guid?, FK do Person), `AssignedAtUtc` (DateTime?), `CreatedAtUtc`.
  Akcja „Skieruj" ustawia `AssignedPersonId`/`AssignedAtUtc` i zmienia
  `Status` na `Assigned`.
- **BudgetEntry** — `Id`, `Fund` (enum: `SKSP`, `DOK` — tylko `SKSP`
  używane w Fazie 2), `EntryDate` (DateOnly), `Description` (string),
  `Category` (string), `Type` (enum: `Income`, `Expense`), `Amount`
  (decimal, zawsze dodatnia — znak wynika z `Type`), `CreatedAtUtc`.

### Migracja

Jedna nowa migracja EF Core (`AddSkspModule`) dodająca powyższe pięć
tabel z kluczami obcymi do `People`/`Parishes`.

## API

| Kontroler | Endpointy | Dostęp (zapis) |
|---|---|---|
| CandidatesController | `GET/POST /api/candidates`, `GET/PUT /api/candidates/{id}` | Administrator, DyrektorSKSP |
| MissionsController | `GET/POST /api/missions`, `GET/PUT /api/missions/{id}` | Administrator, DyrektorSKSP |
| FormatorsController | `GET/POST /api/formators` | Administrator, DyrektorSKSP |
| ParishNeedsController | `GET/POST /api/parish-needs`, `PUT /api/parish-needs/{id}/assign` | Administrator, DyrektorSKSP |
| BudgetController | `GET /api/budget?fund=SKSP`, `POST /api/budget` | Administrator, DyrektorSKSP |

Wszystkie odczyty wymagają wyłącznie uwierzytelnienia (`[Authorize]`);
DTO zwracają dane scalone z `Person`/`Parish` (np. `PersonFullName`,
`ParishName`), analogicznie do `PersonDto.ParishName` z Fazy 1.

## Frontend

Pięć nowych stron pod istniejącymi (na razie nieużywanymi) tagami nav
`sksp` w prototypie: `/candidates`, `/missions`, `/formators`,
`/parish-board`, `/budget/sksp`. Każda podąża za wzorcem
`people-list.component` z Fazy 1: karty statystyk u góry, tabela z
wyszukiwaniem/filtrem, modal dodawania. Widoczność w menu bocznym:

- Administrator, DyrektorSKSP → wszystkie pięć.
- Biskup → wyłącznie „Katechiści posłani" (zgodnie z oryginalną logiką
  `applyRole()` w prototypie, która pozostawia biskupowi wgląd tylko w
  katechistów i — w przyszłości — podopiecznych DOK).
- Pozostałe role → brak widoczności pozycji SKŚP.

## Testowanie

Ten sam wzorzec co Faza 1: testy jednostkowe serwisów (xUnit,
`DokPortal.Infrastructure.Tests`, EF Core InMemory) + testy integracyjne
kontrolerów (`DokPortal.Api.IntegrationTests`, SQLite in-memory,
weryfikacja ról zapisu) + testy Vitest dla każdego nowego serwisu i
komponentu Angular.

## Ryzyka i założenia

- **Założenie:** frekwencja/opinie jako proste pola ręczne są
  akceptowalne na tym etapie; przejście na pełne rekordy obecności to
  osobna, przyszła iteracja jeśli zajdzie potrzeba.
- **Założenie:** `BudgetEntry.Fund` jako pojedyncza tabela dla SKŚP i
  DOK jest bezpieczne, bo oba budżety mają identyczną strukturę
  (data, opis, kategoria, kwota, typ) — Faza 3 doda wyłącznie filtr
  `?fund=DOK`, bez zmian schematu.
