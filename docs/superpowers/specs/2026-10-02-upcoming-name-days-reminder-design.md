# Przypomnienia o nadchodzących imieninach — projekt

**Data:** 2026-10-02
**Status:** Zatwierdzony przez użytkownika, do implementacji

## Cel

Trzeci i ostatni typ reguły z pierwotnego pomysłu użytkownika (po brakujących dokumentach i
nadchodzących spotkaniach), na tym samym silniku przypomnień
(`IReminderService`/`ReminderService`, `RemindersController` chroniony kluczem API, cykliczny
GitHub Actions). Co tydzień wszyscy użytkownicy portalu z adresem e-mail dostają zbiorczy e-mail
z listą osób, które mają imieniny w najbliższych 7 dniach.

## Kluczowa różnica wobec poprzednich dwóch reguł

To przypomnienie jest „dla siebie” — dla personelu, żeby pamiętać złożyć życzenia — nie dla
konkretnego klienta/podopiecznego jak przy dokumentach czy spotkaniach. Pole imienin
(`Person.NameDayMonth`/`NameDayDay`) jest edytowalne dla **każdej** osoby w bazie (nie tylko
podopiecznych DOK czy kandydatów SKŚP), a ekran „Kalendarz imienin” jest dziś dostępny dla
każdego zalogowanego użytkownika, bez ograniczenia rolą. Nie ma więc naturalnego „jednego
opiekuna” per osoba z imieninami — użytkownik zdecydował, że odbiorcą jest **każdy użytkownik
portalu z adresem e-mail**, bez filtrowania po roli.

Druga różnica: to bezstanowy, cotygodniowy „rzut oka” na rolujące okno 7 dni, nie trwały stan
(jak brakujący dokument) ani jednorazowe wydarzenie przywiązane do konkretnej daty (jak
spotkanie). **Nie ma więc potrzeby żadnego nowego pola w bazie ani migracji EF Core, ani
throttlingu/stempla** — każde cotygodniowe uruchomienie niezależnie pokazuje aktualny stan
najbliższych 7 dni.

## Architektura

```
GitHub Actions (cron, co tydzień)
  → POST /api/reminders/upcoming-name-days/run
      nagłówek: X-Reminders-Key: <ten sam sekret co pozostałe dwa endpointy>
  → RemindersController (nowa akcja w tym samym kontrolerze, ta sama weryfikacja klucza)
  → IReminderService.RunUpcomingNameDaysReminderAsync()
      → woła już istniejące INameDayService.GetUpcomingAsync(7, ct)
      → IEmailSender.SendAsync(...) do każdego użytkownika portalu z adresem e-mail
  → zwraca podsumowanie (liczby) jako JSON
```

Nowy endpoint żyje w **tym samym** `RemindersController` co pozostałe dwa i używa tej samej
metody `IsRequestAuthorized()` i tej samej wartości konfiguracji `Reminders:ApiKey` — trzeci
endpoint na jednym wspólnym sekrecie, zero nowej konfiguracji. `ReminderService` dostaje nową
zależność konstruktora `INameDayService` (już zarejestrowany w DI jako scoped) obok istniejących
`AppDbContext`, `IEmailSender`, `ILogger<ReminderService>`.

## Logika

1. Wywołaj `_nameDayService.GetUpcomingAsync(7, ct)` — zwraca `IReadOnlyList<UpcomingNameDayDto>`
   posortowaną po `DaysUntil`.
2. Jeśli lista pusta — nic nie wysyłaj, zwróć wynik z zerami. Żadnego „brak imienin w tym
   tygodniu” maila.
3. Jeśli lista niepusta — zbuduj jeden tekst digestu, np.:
   ```
   Imieniny w najbliższym tygodniu:
   - Jan Kowalski — 03.10 (za 2 dni)
   - Anna Nowak — 07.10 (za 6 dni)
   ```
4. Pobierz wszystkich użytkowników portalu z niepustym adresem e-mail
   (`_db.Users.Where(u => u.Email != null && u.Email != "")`, bez filtra roli).
5. Wyślij digest do każdego z nich, w osobnym `try/catch` per odbiorca — błąd jednego nie
   przerywa pozostałych.

## Obsługa błędów

Identyczna jak w poprzednich dwóch regułach: błąd wysyłki do pojedynczego odbiorcy logowany przez
`ILogger<ReminderService>.LogWarning`, nie przerywa przetwarzania pozostałych odbiorców. Endpoint
zawsze zwraca `200 OK` z podsumowaniem liczbowym. Zły/brakujący klucz API → `401 Unauthorized`.

## Komponenty do zbudowania

- `UpcomingNameDaysReminderResultDto` (`Application/Reminders`):
  `{ NameDaysFound, RecipientsNotified, FailedSends }`.
- `IReminderService.RunUpcomingNameDaysReminderAsync(CancellationToken ct)` — nowa metoda na
  istniejącym interfejsie.
- `ReminderService` — nowa zależność konstruktora `INameDayService`, nowa metoda
  `RunUpcomingNameDaysReminderAsync` obok dwóch istniejących.
- `RemindersController` — nowa akcja `POST api/reminders/upcoming-name-days/run`, współdzieląca
  `IsRequestAuthorized()`.
- `.github/workflows/upcoming-name-days-reminder.yml`: `on: schedule` (cron tygodniowy, np.
  `0 7 * * 1` — ten sam dzień/godzina co przypomnienia o brakujących dokumentach, niezależny
  workflow, brak konfliktu) + `workflow_dispatch`, ta sama struktura `curl`, ten sam
  `secrets.REMINDERS_API_KEY`.
- **Brak** migracji EF Core, **brak** zmian w encjach.

## Testy

- `ReminderServiceTests` (rozszerzenie istniejącego pliku):
  - brak nadchodzących imienin → `NameDaysFound == 0`, brak wysyłki,
  - jedna lub więcej nadchodzących imienin, kilku użytkowników z e-mailem → digest wysłany do
    każdego, treść zawiera imię i nazwisko z `GetUpcomingAsync`,
  - użytkownik bez adresu e-mail → pominięty, nie liczy się jako odbiorca,
  - nieudana wysyłka do jednego użytkownika (fake `IEmailSender` rzucający wyjątek dla
    konkretnego adresu) → pozostali i tak dostają digest, wynik raportuje błąd.
- `RemindersControllerTests` (rozszerzenie istniejącego pliku): brak/zły klucz → `401` dla
  nowego endpointu; poprawny klucz → `200` i wywołanie serwisu.
- Ręczna weryfikacja end-to-end lokalnie (curl z nagłówkiem klucza, testowa osoba z imieninami
  w najbliższych 7 dniach), analogicznie do dwóch poprzednich reguł.

## Poza zakresem tej wersji

- Filtrowanie odbiorców po roli — świadomie pominięte, użytkownik wybrał „wszyscy zalogowani
  użytkownicy”.
- Codzienne/krótsze okno (np. „jutro”) — użytkownik wybrał cotygodniowe okno 7-dniowe.
- Personalizacja treści pod rolę odbiorcy (np. tylko imieniny podopiecznych danego katechisty) —
  to byłaby już inna, węższa reguła; tu świadomie jeden wspólny digest dla wszystkich.
