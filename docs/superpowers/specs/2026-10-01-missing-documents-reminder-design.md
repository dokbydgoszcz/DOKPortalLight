# Przypomnienia o brakujących dokumentach — projekt

**Data:** 2026-10-01
**Status:** Zatwierdzony przez użytkownika, do implementacji

## Cel

Pierwsza wersja automatycznego systemu przypomnień e-mail dla DOK Portal Light. Wykorzystuje
istniejącą infrastrukturę Mailing/SMTP (`IEmailSender`/`SmtpEmailSender`/`NullEmailSender`) do
cotygodniowego powiadamiania o brakujących dokumentach w sprawach DOK, bez ręcznej akcji
użytkownika.

To jest pierwsza z trzech reguł przypomnień zaproponowanych przez użytkownika (brakujące
dokumenty, nadchodzące spotkania, imieniny). Pozostałe dwie mają inną logikę doboru odbiorców
i zostaną zaprojektowane osobno, jako kolejne funkcje na tym samym silniku, jeśli użytkownik
zdecyduje się je zbudować.

## Dlaczego nie prosty harmonogram w procesie API

Backend (`dokportal-api` na Azure App Service) usypia się przy bezczynności (stąd istniejący
spinner „wybudzanie serwera” przy logowaniu). Harmonogram oparty na `BackgroundService`
wewnątrz procesu API mógłby więc po prostu nie wykonać się o zaplanowanej porze. Zamiast tego
harmonogram żyje poza aplikacją: cykliczny workflow GitHub Actions wywołuje chroniony endpoint
backendu, co samo w sobie budzi serwer, jeśli akurat śpi.

## Architektura

```
GitHub Actions (cron, co tydzień)
  → POST /api/reminders/missing-documents/run
      nagłówek: X-Reminders-Key: <sekret>
  → RemindersController (bez [Authorize] — weryfikacja klucza ręcznie)
  → IReminderService.RunMissingDocumentsReminderAsync()
      → odpytuje CaseDocuments + DokCases + People + role Identity
      → IEmailSender.SendAsync(...) dla każdego odbiorcy
  → zwraca podsumowanie (liczby) jako JSON
```

### Bezpieczeństwo endpointu

- Endpoint nie wymaga zalogowanego użytkownika (wywołuje go maszyna), więc nie używa
  `[Authorize]` z JWT. Zamiast tego porównuje nagłówek `X-Reminders-Key` z wartością
  konfiguracji `Reminders:ApiKey`.
- `Reminders:ApiKey` ustawiane jako Azure App Service setting na `dokportal-api` — ten sam
  wzorzec co `Jwt:Key`, `BlobStorage:ConnectionString`, `Smtp:Password`.
- Ten sam sekret jako GitHub Actions repository secret `REMINDERS_API_KEY`, używany przez
  workflow w nagłówku żądania. Sekret ustawia i wprowadza użytkownik — nie Claude — żeby nie
  przechodził bez potrzeby przez sesję asystenta.
- Brak nagłówka lub niezgodny klucz → `401 Unauthorized`, bez szczegółów w odpowiedzi.
- Brak `Reminders:ApiKey` w konfiguracji (np. lokalnie, w Testing) → endpoint zawsze odrzuca
  (traktowane jak brak poprawnego klucza), żeby nie zostawić furtki otwartej przez pomyłkę.

## Model danych

Nowe pole na `CaseDocument`:

```csharp
public DateTime? LastReminderSentAtUtc { get; set; }
```

Wymaga migracji EF Core (`AddCaseDocumentLastReminderSentAtUtc` lub podobnie nazwana), w tym
samym stylu co poprzednie migracje tej encji — nullable, bez wartości domyślnej, automatycznie
stosowana na starcie backendu (`Database.Migrate()` w `Program.cs`, bez zmian w tym mechanizmie).

## Logika doboru i grupowania

1. Pobierz wszystkie `CaseDocument` gdzie:
   - `IsProvided == false`,
   - powiązany `DokCase` istnieje i nie jest usunięty (global soft-delete query filter już to
     załatwia automatycznie),
   - `LastReminderSentAtUtc == null` lub `LastReminderSentAtUtc <= DateTime.UtcNow.AddDays(-7)`.
2. Pogrupuj wynik po `DokCaseId` — jedna sprawa z kilkoma brakami generuje jeden e-mail do
   katechisty, nie jeden na dokument.
3. Dla każdej grupy (sprawy):
   - Jeśli `DokCase.CatechistPersonId` ma przypisany `Person.Email` (niepusty):
     - wyślij e-mail do katechisty z listą nazw brakujących dokumentów i pełnym imieniem i
       nazwiskiem podopiecznego,
     - **tylko jeśli wysyłka się powiedzie**, ustaw `LastReminderSentAtUtc = DateTime.UtcNow`
       na każdym dokumencie z tej grupy objętym tą wysyłką.
   - Jeśli katechista nie ma e-maila, lub wysyłka się nie powiedzie: **nie** stempluj — ten sam
     brak pojawi się ponownie w kolejnym uruchomieniu (codziennym/tygodniowym), dopóki katechista
     faktycznie nie dostanie wiadomości.
4. Niezależnie od (3), zbuduj jeden zbiorczy e-mail-podsumowanie ze wszystkimi sprawami i ich
   brakami (nawet tymi, które się nie zestemplowały) i wyślij go do każdego użytkownika z rolą
   `DyrektorDOK` (`UserManager.GetUsersInRoleAsync(AppRoles.DyrektorDOK)`, filtrowane do tych z
   niepustym `Email`). Wysyłka do Dyrektora DOK **nie wpływa** na stemplowanie
   `LastReminderSentAtUtc` — to tylko dodatkowy, zawsze wysyłany przegląd sytuacji, nie
   mechanizm throttlingu.

## Obsługa błędów

- Każda wysyłka (`IEmailSender.SendAsync`) do pojedynczego odbiorcy owinięta w `try/catch`;
  błąd jednej wysyłki (w tym wyjątek z `NullEmailSender`, gdy SMTP nie jest jeszcze
  skonfigurowany) nie przerywa przetwarzania pozostałych odbiorców. Błędy logowane przez
  `ILogger<ReminderService>` na poziomie `Warning`.
- Endpoint zawsze zwraca `200 OK` z podsumowaniem liczbowym (sprawy przetworzone, e-maile
  wysłane, błędy wysyłki) — status HTTP nie miesza się z tym, czy konkretne e-maile faktycznie
  dotarły. To celowe: dopóki SMTP nie jest skonfigurowany, GitHub Action pokaże zielony check,
  ale podsumowanie w logu joba jasno pokaże „0 wysłanych, N błędów”.
- Błędny/brakujący klucz API → `401 Unauthorized` (jedyny niestandardowy status HTTP w tym
  endponcie).

## Komponenty do zbudowania

- `IReminderService` (`Application/Common` lub nowy folder `Application/Reminders`) z metodą
  `Task<MissingDocumentsReminderResultDto> RunMissingDocumentsReminderAsync(CancellationToken ct)`.
- `MissingDocumentsReminderResultDto`: `{ CasesProcessed, EmailsSentToCatechists,
  DirectorsSummarySent (bool), FailedSends }`.
- `ReminderService : IReminderService` w `Infrastructure/Services`, zależny od `AppDbContext`,
  `IEmailSender`, `UserManager<AppUser>`, `ILogger<ReminderService>`.
- `RemindersController` w `Api/Controllers`:
  `POST /api/reminders/missing-documents/run` — bez `[Authorize]`, ręczna weryfikacja klucza
  na początku akcji, wstrzyknięty `IConfiguration` do odczytu `Reminders:ApiKey`.
- Migracja EF Core na `CaseDocument.LastReminderSentAtUtc`.
- `.github/workflows/missing-documents-reminder.yml`: `on: schedule` (cron tygodniowy) +
  `workflow_dispatch` (ręczne uruchomienie do testów), `curl` do endpointu z nagłówkiem klucza
  z `secrets.REMINDERS_API_KEY`.

## Testy

- `ReminderServiceTests` (styl jak `MailingServiceTests`, in-memory EF Core):
  - dokument z `LastReminderSentAtUtc` sprzed 3 dni → pominięty (throttle),
  - dokument z `LastReminderSentAtUtc` sprzed 8 dni lub `null` → uwzględniony,
  - dwa brakujące dokumenty w tej samej sprawie → jeden e-mail do katechisty, nie dwa,
  - katechista bez e-maila → brak wysyłki, brak stempla, ale Dyrektor DOK i tak dostaje
    podsumowanie,
  - udana wysyłka do katechisty → stempel ustawiony na obu dokumentach z grupy,
  - nieudana wysyłka (fake `IEmailSender` rzucający wyjątek) → brak stempla, wynik raportuje
    błąd, przetwarzanie pozostałych spraw kontynuuje się.
- `RemindersControllerTests` (integracyjne, styl jak `MailingControllerTests`):
  - brak nagłówka klucza → `401`,
  - zły klucz → `401`,
  - poprawny klucz → `200` i wywołanie serwisu.
- Ręczna weryfikacja end-to-end lokalnie (curl z nagłówkiem klucza), analogicznie do testów
  Dokumentów i Mailingu w tej sesji.

## Poza zakresem tej wersji

- Przypomnienia o nadchodzących spotkaniach i imieninach — osobne funkcje na tym samym wzorcu
  (endpoint + GitHub Actions cron + `IReminderService`), do zaprojektowania osobno.
- Ręczny przycisk „Wyślij przypomnienia teraz” w UI — można dodać później, jeśli potrzebny;
  dziś endpoint i tak można wywołać ręcznie (`workflow_dispatch` lub curl) do celów testowych.
- Personalizacja treści e-maila (szablony HTML, logo) — na start zwykły tekst, zgodnie z
  dzisiejszym stylem `MailingService`.
