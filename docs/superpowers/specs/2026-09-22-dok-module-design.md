# DOK Portal Light — Faza 3: Moduł DOK — Design

Data: 2026-09-22
Status: zaakceptowany do implementacji

## Kontekst i cel

Fazy 1–2 dostarczyły wspólny rejestr osób, RBAC oraz moduł SKŚP. Faza 3
dodaje dane i logikę Diecezjalnego Ośrodka Katechumenalnego (DOK):
podopiecznych na pięciu ścieżkach formacyjnych, dokumenty wymagane,
notatki duszpasterskie z realnym egzekwowaniem RODO, harmonogram
spotkań, superwizje (wspólne dla SKŚP i DOK) oraz budżet DOK.

## Cele Fazy 3

- Rejestr podopiecznych DOK powiązanych z `Person`, z przypisaną
  ścieżką formacyjną, etapem formacji i katechistą prowadzącym.
- Elastyczna lista dokumentów wymaganych per podopieczny.
- Notatki duszpasterskie z **realną** kontrolą dostępu: widoczne tylko
  dla autora oraz ról `Administrator`/`DyrektorDOK` — to bezpośrednia
  implementacja wymogu RODO ze specyfikacji projektu, nie tylko etykieta
  w UI.
- Harmonogram spotkań (indywidualnych i grupowych) jako lista, nie
  siatka kalendarza.
- Wspólny moduł Superwizje dla SKŚP i DOK (pole `Institution`).
- Widok Absolwenci jako filtr istniejących podopiecznych (`Stage =
  Graduate`), bez nowej encji.
- Budżet DOK: zero nowej logiki backendu — filtr istniejącego
  `BudgetEntry` po `Fund = DOK`.

## Poza zakresem Fazy 3

- Prawdziwa siatka kalendarza miesięcznego — harmonogram to lista.
- Ogólny audit log (rejestr wszystkich prób dostępu, w tym
  zablokowanych) — to Faza 4. Faza 3 filtruje widoczne notatki po
  stronie API (zwraca tylko te, do których wnioskujący ma prawo), ale
  nie zapisuje osobnego rejestru prób dostępu.
- Egzekwowanie prywatności na `Supervision.Conclusion` — pole istnieje
  w UI (może być oznaczone jako prywatne wizualnie), ale bez osobnej
  kontroli dostępu backendu w tej fazie; jedyna **realna** kontrola
  RODO w Fazie 3 dotyczy `PastoralNote`.

## Model danych

Wszystkie nowe encje mają FK do `Person`/`AppUser` z Faz 1–2.

- **DokCase** — `Id`, `PersonId` (FK), `Path` (enum:
  `BaptismCandidate`, `Confirmation`, `Communion`, `Conversion`,
  `ReturnToUnity`), `Stage` (enum: `Application`, `Formation`,
  `Sacrament`, `Graduate`), `CatechistPersonId` (FK Person),
  `MentorPersonId` (Guid?, FK Person — opiekun grupy dla absolwentów),
  `LastMeetingDate` (DateOnly?), `CompletedAtUtc` (DateTime?),
  `CreatedAtUtc`, `UpdatedAtUtc`.
- **CaseDocument** — `Id`, `DokCaseId` (FK), `Name` (string),
  `IsProvided` (bool), `CreatedAtUtc`.
- **PastoralNote** — `Id`, `DokCaseId` (FK), `AuthorUserId` (string, FK
  `AspNetUsers.Id`), `Content` (string), `CreatedAtUtc`.
- **Meeting** — `Id`, `DokCaseId` (Guid?, FK — null = sesja grupowa),
  `GroupLabel` (string?, wypełniane gdy `DokCaseId` jest null),
  `MeetingDate` (DateOnly), `IsAttended` (bool?), `Notes` (string?),
  `CreatedAtUtc`.
- **Supervision** — `Id`, `Institution` (enum: `SKSP`, `DOK`),
  `GroupLabel` (string), `SupervisionDate` (DateOnly),
  `AttendeesCount` (int?), `ExpectedCount` (int?), `Topic` (string?),
  `Conclusion` (string?), `CreatedAtUtc`.

### Migracja

Jedna nowa migracja EF Core (`AddDokModule`) dodająca powyższe pięć
tabel.

## API i autoryzacja

| Kontroler | Endpointy | Dostęp (zapis) |
|---|---|---|
| DokCasesController | `GET/POST /api/dok-cases`, `GET/PUT /api/dok-cases/{id}` | Administrator, DyrektorDOK |
| CaseDocumentsController | `GET/POST /api/dok-cases/{caseId}/documents`, `PUT /api/dok-cases/{caseId}/documents/{id}` (przełącza `IsProvided`) | Administrator, DyrektorDOK, KatechistaProwadzacy |
| PastoralNotesController | `GET/POST /api/dok-cases/{caseId}/notes` | zapis: Administrator, DyrektorDOK, KatechistaProwadzacy |
| MeetingsController | `GET/POST /api/meetings` | Administrator, DyrektorDOK, KatechistaProwadzacy |
| SupervisionsController | `GET /api/supervisions?institution=`, `POST /api/supervisions` | Administrator, DyrektorDOK, DyrektorSKSP, Superwizor |

`GET /api/dok-cases/{caseId}/notes` filtruje wynik po stronie serwera:
zwraca tylko notatki, których `AuthorUserId` odpowiada wywołującemu,
chyba że wywołujący ma rolę `Administrator` lub `DyrektorDOK` — wtedy
widzi wszystkie notatki danego podopiecznego. To jedyne miejsce w
Fazie 3 z rzeczywistą logiką RODO (nie tylko etykieta w UI).

## Frontend

Nowe strony: `/dok-cases` (Podopieczni DOK, ze statystykami per
ścieżka i modalem profilu ze stage-trackiem, dokumentami i notatką),
`/meetings` (Harmonogram — lista), `/supervisions` (Superwizje —
wspólne dla SKŚP/DOK), `/graduates` (Absolwenci — filtr `DokCase` po
`Stage = Graduate`), `/budget/dok` (Budżet DOK — ten sam komponent
budżetu co SKŚP, sparametryzowany `Fund = DOK`).

Widoczność menu:
- Podopieczni DOK: Administrator, DyrektorDOK, Superwizor,
  KatechistaProwadzacy, Biskup.
- Harmonogram i obecności: Administrator, DyrektorDOK,
  KatechistaProwadzacy.
- Superwizje: Administrator, DyrektorDOK, DyrektorSKSP, Superwizor
  (rozszerzone względem prototypu, bo moduł jest teraz wspólny).
- Absolwenci, Budżet DOK: Administrator, DyrektorDOK.

## Testowanie

Ten sam wzorzec co Fazy 1–2: testy jednostkowe serwisów (xUnit,
`DokPortal.Infrastructure.Tests`, EF Core InMemory), testy
integracyjne kontrolerów (`DokPortal.Api.IntegrationTests`, SQLite
in-memory) — ze szczególnym naciskiem na testy `PastoralNotesController`
weryfikujące, że katechista A nie widzi notatki katechisty B, a
Dyrektor DOK widzi obie — oraz testy Vitest dla nowych serwisów i
komponentów Angular.

## Ryzyka i założenia

- **Założenie:** filtrowanie notatek po stronie serwera (zamiast
  jawnego 403 + osobnego rejestru prób dostępu) wystarcza jako
  demonstracja RODO w tej fazie; pełny audit log z rejestrem
  zablokowanych prób to Faza 4.
- **Założenie:** rozszerzenie widoczności „Superwizje" o
  `DyrektorSKSP` (którego prototyp by nie pokazał, bo strona była
  wcześniej wyłącznie w sekcji DOK) jest bezpieczne, bo moduł jest już
  koncepcyjnie wspólny dla obu instytucji.
