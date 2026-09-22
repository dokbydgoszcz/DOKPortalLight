# DOK Portal Light — Faza 4: Narzędzia wspólne — Design

Data: 2026-09-22
Status: zaakceptowany do implementacji

## Kontekst i cel

Fazy 1–3 dostarczyły fundament (osoby, RBAC, dashboard), moduł SKŚP i
moduł DOK. Faza 4 dodaje cztery narzędzia wspólne dla obu instytucji,
zamykające zakres pierwotnego prototypu: generator pism z realnym PDF,
mailing (rejestr kampanii bez wysyłki), kalendarz imienin oraz audit
log demonstrujący RODO na notatkach duszpasterskich i zmianach ról.

## Cele Fazy 4

- Generowanie prawdziwych plików PDF (biblioteka QuestPDF) dla sześciu
  szablonów pism z prototypu, z polami scalonymi z danych osoby.
- Rejestr kampanii mailingowych z grupami odbiorców liczonymi z
  rzeczywistych danych — bez realnej wysyłki e-mail.
- Pola imienin (`NameDayMonth`, `NameDayDay`) na `Person` oraz endpoint
  najbliższych imienin.
- Audit log obejmujący dostęp do notatek duszpasterskich (w tym próby
  zablokowane przez filtr RODO z Fazy 3) oraz zmiany ról użytkowników.

## Poza zakresem Fazy 4

- Rzeczywista wysyłka e-mail (SMTP) — kampanie pozostają rekordami.
- Automatyzacja/harmonogram wysyłki życzeń imieninowych — brak
  mechanizmu zadań w tle w tej fazie; funkcja realna to wyłącznie lista
  najbliższych imienin.
- Osobna encja/rola „Proboszcz" — grupa odbiorców „Proboszczowie” z
  prototypu jest pomijana, bo model danych nie ma takiej encji; zestaw
  grup ogranicza się do tych policzalnych z istniejących danych.
- Generowanie pism z głębokim scalaniem pól specyficznych dla każdego
  szablonu (np. dane misji kanonicznej, dane sprawy DOK) — wszystkie
  szablony korzystają z tego samego, uproszczonego zestawu pól osoby
  plus pole notatki dodatkowej wpisywanej ręcznie.
- Backendowy eksport CSV audit logu — eksport realizowany po stronie
  klienta z już pobranych danych.

## Model danych

### Person — nowe pola

- `NameDayMonth` (`int?`, 1–12), `NameDayDay` (`int?`, 1–31) — dodane
  do istniejącej encji `Person`, migracja `AddNameDayFields`.

### GeneratedDocument (nowa encja)

Rekord historii wygenerowanych pism (nie przechowuje samego PDF, tylko
metadane generacji):

- `Id`, `Template` (enum: `LetterToBishop`, `ConversionConsent`,
  `CanonicalMissionDecree`, `DokReferral`, `SkspCompletionCertificate`,
  `SacramentCertificate`), `PersonId` (FK `Person`),
  `GeneratedByUserId` (string, FK `AspNetUsers.Id`), `AdditionalNotes`
  (string?), `CreatedAtUtc`.

### MailingCampaign (nowa encja)

- `Id`, `Subject` (string), `Body` (string), `Group` (enum:
  `CandidatesSksp`, `Missionaries`, `DokGraduates`, `DokCases`),
  `RecipientCount` (int — zrzut liczby odbiorców w momencie
  utworzenia/wysyłki), `Status` (enum: `Draft`, `Sent`), `CreatedAtUtc`,
  `SentAtUtc` (DateTime?).

### AuditLogEntry (nowa encja)

- `Id`, `TimestampUtc`, `UserId` (string), `UserEmail` (string,
  zdenormalizowane do wyświetlania), `Action` (string, np.
  `ReadPastoralNotes`, `CreatePastoralNote`, `AssignUserRoles`),
  `ObjectDescription` (string, np. imię i nazwisko podopiecznego albo
  e-mail użytkownika docelowego), `Result` (enum: `Allowed`,
  `Blocked`).

### Migracja

Jedna nowa migracja EF Core (`AddSharedTools`) dodająca pola imienin do
`Person` oraz trzy nowe tabele powyżej.

## API i autoryzacja

| Kontroler | Endpointy | Dostęp (zapis) |
|---|---|---|
| DocumentsController | `POST /api/documents/generate` (zwraca PDF, `Content-Type: application/pdf`, zapisuje wpis historii), `GET /api/documents` (historia) | generowanie: Administrator, DyrektorSKSP, DyrektorDOK |
| MailingController | `GET/POST /api/mailing/campaigns`, `POST /api/mailing/campaigns/{id}/send` | Administrator, DyrektorSKSP, DyrektorDOK |
| NameDaysController | `GET /api/name-days/upcoming?days=30` | odczyt: każdy zalogowany |
| AuditLogController | `GET /api/audit-log` | Administrator |

Logowanie do audit logu odbywa się z dwóch istniejących miejsc, bez
nowego middleware:

- `PastoralNotesController.GetAll` — po odfiltrowaniu widocznych
  notatek (logika z Fazy 3 bez zmian): jeśli wywołujący nie ma
  uprawnień uprzywilejowanych i sprawa zawiera notatki innych autorów
  niż on, zapisywany jest wpis `Result = Blocked`; w przeciwnym razie
  `Result = Allowed`. To pierwsza realna materializacja filtra RODO z
  Fazy 3 jako widocznego zdarzenia w audit logu.
- `UsersController.AssignRoles` — każde wywołanie zapisuje wpis
  `Action = AssignUserRoles`, `ObjectDescription` = e-mail użytkownika
  docelowego, `Result = Allowed` (endpoint jest już ograniczony do
  Administratora, więc nie ma ścieżki `Blocked`).

`POST /api/mailing/campaigns/{id}/send` liczy `RecipientCount` na
podstawie grupy w momencie wysyłki (nie w momencie utworzenia), ustawia
`Status = Sent` i `SentAtUtc`; e-mail nie jest wysyłany.

`GET /api/name-days/upcoming` liczy odległość w dniach od dzisiejszej
daty do najbliższego wystąpienia miesiąc/dzień (z zawinięciem przez
koniec roku) i zwraca posortowaną listę osób z ustawionymi polami
imienin.

## Frontend

Nowe strony: `/documents` (Dokumenty i pisma — wybór szablonu i osoby
przez istniejący `PeopleService`, pole notatki dodatkowej, przycisk
generowania pobierający PDF, lista historii), `/mailing` (Mailing —
lista kampanii, formularz nowej kampanii z wyborem grupy i podglądem
liczby odbiorców, przycisk „Wyślij”), `/name-days` (Kalendarz imienin
— lista najbliższych imienin), `/audit-log` (Audit log — tabela
wpisów, przycisk „Eksportuj CSV” generujący plik po stronie klienta z
already pobranych wierszy).

Widoczność menu (`nav-items.ts`):
- Dokumenty i pisma, Mailing: Administrator, DyrektorSKSP, DyrektorDOK.
- Kalendarz imienin: brak ograniczeń (jak Dashboard/Baza osób).
- Audit log: Administrator (pierwszy realny wpis `admin-only` z
  prototypu, dotąd nieużyty).

## Testowanie

Ten sam wzorzec co Fazy 1–3: testy jednostkowe serwisów (xUnit,
`DokPortal.Infrastructure.Tests`, EF Core InMemory — w tym test
generowania PDF weryfikujący niepusty strumień bajtów i poprawny
`Content-Type`), testy integracyjne kontrolerów
(`DokPortal.Api.IntegrationTests`, SQLite in-memory) — ze szczególnym
naciskiem na test `PastoralNotesController` weryfikujący, że po
zablokowanym odczycie w bazie pojawia się wpis `AuditLogEntry` z
`Result = Blocked` — oraz testy Vitest dla nowych serwisów i
komponentów Angular.

## Ryzyka i założenia

- **Założenie:** uproszczony, jednolity zestaw pól (dane osoby +
  notatka dodatkowa) wystarcza jako demonstracja generatora pism z
  polami dynamicznymi; głębsza integracja per szablon (np. automatyczne
  pobranie danych misji kanonicznej) to potencjalne rozszerzenie poza
  zakresem „Light”.
- **Założenie:** pominięcie grupy odbiorców „Proboszczowie” nie
  ogranicza realnej użyteczności modułu mailingu, bo pozostałe cztery
  grupy pokrywają główne przypadki użycia z prototypu.
- **Założenie:** wpis audit logu dla `PastoralNotesController.GetAll`
  raz na wywołanie (a nie raz na przefiltrowaną notatkę) jest
  wystarczającą granularnością do demonstracji RODO.
