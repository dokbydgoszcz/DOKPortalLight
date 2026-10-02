# Przypomnienia o nadchodzących spotkaniach — projekt

**Data:** 2026-10-02
**Status:** Zatwierdzony przez użytkownika, do implementacji

## Cel

Drugi typ reguły na silniku przypomnień zbudowanym dla brakujących dokumentów
(`IReminderService`/`ReminderService`, `RemindersController` chroniony kluczem API, cykliczny
GitHub Actions). Katechista prowadzący dostaje e-mail dzień przed spotkaniem przypisanym do jego
sprawy DOK.

Ta funkcja była wcześniej zablokowana: formularz „Dodaj spotkanie” nie pozwalał przypisać sprawy
DOK do spotkania, więc niemal wszystkie rekordy `Meeting` miały `DokCaseId == null` (same
spotkania grupowe, bez konkretnego odbiorcy). Blokadę usunięto 2026-10-02 (commit `3328280`,
„Harmonogram: wybór sprawy DOK w formularzu spotkania”) — formularz pozwala teraz wybrać
podopiecznego DOK dla spotkania.

## Zakres

- **Odbiorca:** wyłącznie katechista prowadzący (`DokCase.CatechistPersonId` → `Person.Email`).
  Świadomie pominięto: e-mail do podopiecznego/kandydata i zbiorczy digest do Dyrektora DOK —
  użytkownik wybrał tylko katechistę.
- **Spotkania grupowe** (`Meeting.DokCaseId == null`) są pomijane — nie mają odbiorcy w tym
  modelu, więc reguła ich nie dotyczy. To nie jest luka do uzupełnienia, tylko naturalna
  konsekwencja zakresu.
- **Okno czasowe:** dokładnie 1 dzień przed spotkaniem — `Meeting.MeetingDate == jutro` (UTC).
  To nie jest throttling okresowy jak przy dokumentach (7 dni) — to jednorazowe przypomnienie
  przywiązane do konkretnej daty spotkania.
- **Harmonogram:** **codziennie**, nie raz w tygodniu — inaczej niż przy brakujących
  dokumentach, bo „dzień przed” wymaga sprawdzania każdego dnia, które spotkania wypadają jutro.

## Architektura

```
GitHub Actions (cron, codziennie)
  → POST /api/reminders/upcoming-meetings/run
      nagłówek: X-Reminders-Key: <ten sam sekret co /missing-documents/run>
  → RemindersController (nowa akcja w tym samym kontrolerze, ta sama weryfikacja klucza)
  → IReminderService.RunUpcomingMeetingsReminderAsync()
      → odpytuje Meetings + DokCases + People
      → IEmailSender.SendAsync(...) do katechisty, dla każdego kwalifikującego się spotkania
  → zwraca podsumowanie (liczby) jako JSON
```

Nowy endpoint żyje w **tym samym** `RemindersController` co `/missing-documents/run` i używa tej
samej metody weryfikacji klucza (`HasValidKey`) i tej samej wartości konfiguracji
`Reminders:ApiKey` — to jeden wspólny sekret dla całego silnika przypomnień, nie osobny klucz na
regułę. Nowy cykliczny workflow GitHub Actions jest w osobnym pliku (inny harmonogram: codziennie
zamiast raz w tygodniu), ale używa tego samego `secrets.REMINDERS_API_KEY`.

## Model danych

Nowe pole na `Meeting`:

```csharp
public DateTime? ReminderSentAtUtc { get; set; }
```

Wymaga migracji EF Core (`AddMeetingReminderSentAtUtc`), nullable, bez wartości domyślnej,
stosowana automatycznie na starcie backendu — ten sam mechanizm co dotychczasowe migracje.

## Logika doboru

1. Pobierz wszystkie `Meeting` gdzie:
   - `DokCaseId != null`,
   - powiązany `DokCase` istnieje i nie jest usunięty (global soft-delete query filter),
   - `MeetingDate == DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1)` (jutro, UTC),
   - `ReminderSentAtUtc == null` (jeszcze nie wysłano — to jednorazowy stempel, nie throttle
     czasowy jak przy dokumentach).
2. Dla każdego kwalifikującego się spotkania (bez grupowania — jedno spotkanie = jeden e-mail,
   w przeciwieństwie do dokumentów, gdzie grupowano po sprawie):
   - Jeśli `DokCase.CatechistPersonId` ma przypisany `Person.Email` (niepusty):
     - wyślij e-mail do katechisty z datą spotkania i pełnym imieniem i nazwiskiem podopiecznego,
     - **tylko jeśli wysyłka się powiedzie**, ustaw `ReminderSentAtUtc = DateTime.UtcNow` na tym
       spotkaniu (ten sam wzorzec „stempel tylko po sukcesie” co przy dokumentach — jeśli e-mail
       się nie powiedzie, spotkanie zostanie uwzględnione ponownie następnego dnia, dopóki
       `MeetingDate == jutro` wciąż jest prawdą; gdy data minie, naturalnie przestanie się
       kwalifikować niezależnie od stempla).
   - Jeśli katechista nie ma e-maila: pomiń, nie stempluj (nic nie da się wysłać, ale nie ma to
     wpływu na inne przetwarzane spotkania).

## Obsługa błędów

Identyczna jak przy brakujących dokumentach: wysyłka do każdego katechisty w osobnym
`try/catch`, błąd jednej nie przerywa pozostałych, logowane przez `ILogger<ReminderService>` na
poziomie `Warning`. Endpoint zawsze zwraca `200 OK` z podsumowaniem liczbowym — status HTTP nie
miesza się z tym, czy e-maile faktycznie dotarły. Zły/brakujący klucz API → `401 Unauthorized`,
bez szczegółów.

## Komponenty do zbudowania

- `Meeting.ReminderSentAtUtc` (nullable `DateTime?`) + migracja EF Core.
- `UpcomingMeetingsReminderResultDto` (`Application/Reminders`):
  `{ MeetingsProcessed, EmailsSentToCatechists, FailedSends }`. Brak pola
  `DirectorsSummarySent` — nie dotyczy tej reguły (brak digestu do Dyrektora DOK w tym zakresie).
- `IReminderService.RunUpcomingMeetingsReminderAsync(CancellationToken ct)` — nowa metoda na
  istniejącym interfejsie.
- `ReminderService.RunUpcomingMeetingsReminderAsync` — nowa implementacja w istniejącej klasie,
  obok `RunMissingDocumentsReminderAsync`.
- `RemindersController` — nowa akcja `POST api/reminders/upcoming-meetings/run` w istniejącym
  kontrolerze, współdzieląca prywatną metodę `HasValidKey` z istniejącą akcją.
- `.github/workflows/upcoming-meetings-reminder.yml`: `on: schedule` (cron **codzienny**,
  np. `0 7 * * *` — ta sama godzina co cotygodniowy, ale każdego dnia) + `workflow_dispatch`,
  ta sama struktura `curl` co istniejący workflow, ten sam `secrets.REMINDERS_API_KEY`.

## Testy

- `ReminderServiceTests` (rozszerzenie istniejącego pliku, styl identyczny jak dla
  `RunMissingDocumentsReminderAsync`):
  - spotkanie jutro, z przypisaną sprawą i katechistą z e-mailem → wysłane, stempel ustawiony,
  - spotkanie pojutrze lub wczoraj → pominięte,
  - spotkanie jutro bez `DokCaseId` (grupowe) → pominięte,
  - spotkanie jutro z `DokCaseId`, katechista bez e-maila → pominięte, brak stempla,
  - spotkanie jutro, wysyłka się nie powiedzie (fake `IEmailSender` rzucający wyjątek) → brak
    stempla, wynik raportuje błąd,
  - spotkanie jutro z `ReminderSentAtUtc` już ustawionym → pominięte (nie wysyła drugi raz).
- `RemindersControllerTests` (rozszerzenie istniejącego pliku): brak/zły klucz → `401` dla nowego
  endpointu; poprawny klucz → `200` i wywołanie serwisu.
- Ręczna weryfikacja end-to-end lokalnie (curl z nagłówkiem klucza, testowe dane: sprawa DOK +
  spotkanie z datą jutrzejszą), analogicznie do weryfikacji przy dokumentach.

## Poza zakresem tej wersji

- E-mail do podopiecznego/kandydata i zbiorczy digest do Dyrektora DOK — użytkownik świadomie
  wybrał tylko katechistę.
- Przypomnienia o spotkaniach grupowych — brak modelu odbiorcy dla `GroupLabel` bez konkretnej
  osoby; osobny temat, jeśli kiedyś potrzebny.
- Przypomnienia o imieninach — trzeci, osobny typ reguły z tego samego pierwotnego pomysłu
  użytkownika, do zaprojektowania osobno.
- Konfigurowalne okno czasowe (np. „3 dni przed” jako opcja) — na start sztywne 1 dzień, zgodnie
  z wyborem użytkownika.
