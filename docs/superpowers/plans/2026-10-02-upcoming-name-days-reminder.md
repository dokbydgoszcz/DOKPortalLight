# Przypomnienia o nadchodzących imieninach — plan implementacji

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cotygodniowy, automatyczny e-mail do wszystkich użytkowników portalu z listą osób, które mają imieniny w najbliższych 7 dniach, bez ręcznej akcji użytkownika.

**Architecture:** Trzecie i ostatnie rozszerzenie istniejącego silnika przypomnień. `ReminderService` dostaje nową zależność konstruktora `INameDayService` (już zarejestrowaną w DI) i nową metodę, która woła istniejące `GetUpcomingAsync(7, ct)` zamiast odpytywać bazę samodzielnie. Nowa akcja na istniejącym `RemindersController` (ten sam `Reminders:ApiKey`), nowy cotygodniowy workflow GitHub Actions. **Brak migracji EF Core** — to bezstanowy digest, nie ma czego stemplować.

**Tech Stack:** ASP.NET Core 8, EF Core (SQL Server w produkcji, SQLite w testach integracyjnych, InMemory w testach jednostkowych), xUnit, GitHub Actions.

**Spec:** [docs/superpowers/specs/2026-10-02-upcoming-name-days-reminder-design.md](../specs/2026-10-02-upcoming-name-days-reminder-design.md)

## Global Constraints

- Commity i push idą bezpośrednio na branch `master` (bez PR) — taki jest ustalony przepływ w tym repo.
- Nigdy nie commituj `backend/src/DokPortal.Api/appsettings.Development.json` ani katalogu `.claude/`.
- Po zakończeniu funkcji dodaj wpis do `frontend/src/app/features/dashboard/dashboard.component.ts` (tablica `changelog`, nowy wpis na górze, data `YYYY-MM-DD`).
- Odbiorcą jest **każdy użytkownik portalu z adresem e-mail** (`_db.Users`, bez filtra roli) — zgodnie z wyborem użytkownika, nie tylko Administrator/Dyrektorzy.
- Okno czasowe jest sztywne: 7 dni, przez istniejące `INameDayService.GetUpcomingAsync(7, ct)` — żadnej nowej logiki liczenia dat.
- **Brak throttlingu i brak nowego pola w bazie** — to bezstanowy cotygodniowy digest, każde uruchomienie niezależnie pokazuje aktualny stan najbliższych 7 dni.
- Jeśli `GetUpcomingAsync(7, ct)` zwróci pustą listę, nie wysyłaj żadnego e-maila.
- Ten sam sekret `Reminders:ApiKey` / `secrets.REMINDERS_API_KEY`, który już jest skonfigurowany w Azure i GitHub, jest reużywany — **nie potrzeba żadnego nowego sekretu**.
- Każda wysyłka e-mail (`IEmailSender.SendAsync`) w `ReminderService` jest owinięta w `try/catch` per odbiorca — błąd jednego nie przerywa pozostałych, loguje się przez `ILogger<ReminderService>.LogWarning`.
- `INameDayService` jest już zarejestrowany w DI (`builder.Services.AddScoped<INameDayService, NameDayService>();` w `Program.cs`) — dodanie go jako zależności konstruktora `ReminderService` **nie wymaga żadnej zmiany w `Program.cs`**, kontener wstrzyknie go automatycznie.

---

## Task 1: DTO wyniku i rozszerzenie `IReminderService`

**Files:**
- Create: `backend/src/DokPortal.Application/Reminders/UpcomingNameDaysReminderResultDto.cs`
- Modify: `backend/src/DokPortal.Application/Reminders/IReminderService.cs`

**Interfaces:**
- Produces: `UpcomingNameDaysReminderResultDto` z polami `NameDaysFound` (int), `RecipientsNotified` (int), `FailedSends` (int); `IReminderService.RunUpcomingNameDaysReminderAsync(CancellationToken)` zwracające `Task<UpcomingNameDaysReminderResultDto>`. Używane przez `ReminderService` (Task 2) i `RemindersController` (Task 3).

- [ ] **Step 1: Utwórz DTO wyniku**

`backend/src/DokPortal.Application/Reminders/UpcomingNameDaysReminderResultDto.cs`:

```csharp
namespace DokPortal.Application.Reminders;

public record UpcomingNameDaysReminderResultDto
{
    public required int NameDaysFound { get; init; }
    public required int RecipientsNotified { get; init; }
    public required int FailedSends { get; init; }
}
```

- [ ] **Step 2: Rozszerz interfejs serwisu**

Zmień `backend/src/DokPortal.Application/Reminders/IReminderService.cs` na (pełna treść pliku po zmianie):

```csharp
namespace DokPortal.Application.Reminders;

public interface IReminderService
{
    Task<MissingDocumentsReminderResultDto> RunMissingDocumentsReminderAsync(CancellationToken ct);
    Task<UpcomingMeetingsReminderResultDto> RunUpcomingMeetingsReminderAsync(CancellationToken ct);
    Task<UpcomingNameDaysReminderResultDto> RunUpcomingNameDaysReminderAsync(CancellationToken ct);
}
```

- [ ] **Step 3: Potwierdź czerwony wynik budowania**

```bash
cd backend
dotnet build
```

Oczekiwany wynik: błąd kompilacji `CS0535` — `ReminderService` nie implementuje
`IReminderService.RunUpcomingNameDaysReminderAsync(CancellationToken)`. To oczekiwane —
implementacja przychodzi w Task 2. Commit odłożony do Task 2 (samodzielnie ten krok zostawia
solucję w stanie nieudanej kompilacji).

---

## Task 2: `ReminderService.RunUpcomingNameDaysReminderAsync` (TDD)

**Files:**
- Modify: `backend/src/DokPortal.Infrastructure/Services/ReminderService.cs`
- Modify: `backend/tests/DokPortal.Infrastructure.Tests/Services/ReminderServiceTests.cs`

**Interfaces:**
- Consumes: `INameDayService.GetUpcomingAsync(int days, CancellationToken ct)` (istniejący,
  zwraca `Task<IReadOnlyList<UpcomingNameDayDto>>` z polami `PersonId`, `FullName`,
  `NameDayMonth`, `NameDayDay`, `DaysUntil`), `AppDbContext.Users` (`DbSet<AppUser>`, już
  istnieje), `UpcomingNameDaysReminderResultDto` (Task 1).
- Produces: `ReminderService.RunUpcomingNameDaysReminderAsync(CancellationToken ct)`
  zaimplementowane na istniejącej klasie; `ReminderService` ma teraz 4-argumentowy konstruktor
  `ReminderService(AppDbContext db, IEmailSender emailSender, ILogger<ReminderService> logger, INameDayService nameDayService)`.

### Krok przygotowawczy: zmień konstruktor i napraw istniejące wywołania

- [ ] **Step 1: Dodaj zależność `INameDayService` do konstruktora**

W `backend/src/DokPortal.Infrastructure/Services/ReminderService.cs` dodaj import i zmień pole/konstruktor:

```csharp
using DokPortal.Application.Common;
using DokPortal.Application.NameDays;
using DokPortal.Application.Reminders;
using DokPortal.Domain.Constants;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DokPortal.Infrastructure.Services;

public class ReminderService : IReminderService
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<ReminderService> _logger;
    private readonly INameDayService _nameDayService;

    public ReminderService(AppDbContext db, IEmailSender emailSender, ILogger<ReminderService> logger, INameDayService nameDayService)
    {
        _db = db;
        _emailSender = emailSender;
        _logger = logger;
        _nameDayService = nameDayService;
    }
```

(Zostaw resztę pliku — pozostałe metody i prywatne rekordy — bez zmian na tym kroku.)

- [ ] **Step 2: Napraw wszystkie istniejące wywołania konstruktora w testach**

Ta zmiana konstruktora psuje kompilację 14 istniejących testów w
`backend/tests/DokPortal.Infrastructure.Tests/Services/ReminderServiceTests.cs` (wszystkie
wywołują stary, 3-argumentowy konstruktor). Napraw je jednym poleceniem zamiast ręcznie edytować
każdy z 14 miejsc:

```bash
cd backend
sed -i 's/new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance)/new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance, new NameDayService(db))/g' tests/DokPortal.Infrastructure.Tests/Services/ReminderServiceTests.cs
sed -i 's/new ReminderService(db, new ThrowingEmailSender(), NullLogger<ReminderService>.Instance)/new ReminderService(db, new ThrowingEmailSender(), NullLogger<ReminderService>.Instance, new NameDayService(db))/g' tests/DokPortal.Infrastructure.Tests/Services/ReminderServiceTests.cs
```

`NameDayService` (klasa z `DokPortal.Infrastructure.Services`, konstruktor `NameDayService(AppDbContext db)`) jest już widoczna w tym pliku testowym — `using DokPortal.Infrastructure.Services;` już tam jest (plik już go importuje dla samego `ReminderService`).

- [ ] **Step 3: Zweryfikuj, że sed objął wszystkie wywołania**

```bash
grep -c "new ReminderService(db" tests/DokPortal.Infrastructure.Tests/Services/ReminderServiceTests.cs
grep -c "new NameDayService(db)" tests/DokPortal.Infrastructure.Tests/Services/ReminderServiceTests.cs
```

Oczekiwany wynik: obie liczby równe (14) — każde wywołanie `new ReminderService(db, ...)` ma teraz towarzyszące `new NameDayService(db)`.

- [ ] **Step 4: Zbuduj i potwierdź, że jedyny błąd to brakująca implementacja metody**

```bash
dotnet build
```

Oczekiwany wynik: dalej `CS0535` — `ReminderService` nie implementuje
`IReminderService.RunUpcomingNameDaysReminderAsync(CancellationToken)` (z Task 1, Step 3). To
jedyny oczekiwany błąd na tym etapie — potwierdza, że zmiana konstruktora i naprawa `sed`-em nie
wprowadziły żadnych NOWYCH błędów kompilacji. Implementacja metody (i tym samym zielony build)
przychodzi w Step 7.

### TDD: nowa metoda

- [ ] **Step 5: Dodaj nowe testy do istniejącego pliku**

Dodaj poniższe metody testowe (i prywatną klasę pomocniczą) na końcu klasy `ReminderServiceTests`
w `backend/tests/DokPortal.Infrastructure.Tests/Services/ReminderServiceTests.cs`, bezpośrednio
przed zamykającym nawiasem klasy:

```csharp
    [Fact]
    public async Task RunUpcomingNameDaysReminderAsync_WithNoUpcomingNameDays_SendsNothing()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var user = new AppUser { Id = Guid.NewGuid().ToString(), UserName = "user@example.org", Email = "user@example.org" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance, new NameDayService(db));

        var result = await service.RunUpcomingNameDaysReminderAsync(default);

        Assert.Equal(0, result.NameDaysFound);
        Assert.Equal(0, result.RecipientsNotified);
        Assert.Empty(emailSender.Sent);
    }

    [Fact]
    public async Task RunUpcomingNameDaysReminderAsync_SendsDigestToAllUsersWithEmail()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var today = DateTime.UtcNow;
        var soon = today.AddDays(2);
        db.People.Add(new Person
        {
            Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski",
            NameDayMonth = soon.Month, NameDayDay = soon.Day,
            CreatedAtUtc = today, UpdatedAtUtc = today
        });
        var userA = new AppUser { Id = Guid.NewGuid().ToString(), UserName = "a@example.org", Email = "a@example.org" };
        var userB = new AppUser { Id = Guid.NewGuid().ToString(), UserName = "b@example.org", Email = "b@example.org" };
        db.Users.AddRange(userA, userB);
        await db.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance, new NameDayService(db));

        var result = await service.RunUpcomingNameDaysReminderAsync(default);

        Assert.Equal(1, result.NameDaysFound);
        Assert.Equal(2, result.RecipientsNotified);
        Assert.Equal(2, emailSender.Sent.Count);
        Assert.Contains(emailSender.Sent, s => s.To == "a@example.org" && s.Body.Contains("Jan Kowalski"));
        Assert.Contains(emailSender.Sent, s => s.To == "b@example.org" && s.Body.Contains("Jan Kowalski"));
    }

    [Fact]
    public async Task RunUpcomingNameDaysReminderAsync_SkipsUsersWithoutEmail()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var today = DateTime.UtcNow;
        var soon = today.AddDays(1);
        db.People.Add(new Person
        {
            Id = Guid.NewGuid(), FirstName = "Anna", LastName = "Nowak",
            NameDayMonth = soon.Month, NameDayDay = soon.Day,
            CreatedAtUtc = today, UpdatedAtUtc = today
        });
        var userWithEmail = new AppUser { Id = Guid.NewGuid().ToString(), UserName = "with@example.org", Email = "with@example.org" };
        var userWithoutEmail = new AppUser { Id = Guid.NewGuid().ToString(), UserName = "without" };
        db.Users.AddRange(userWithEmail, userWithoutEmail);
        await db.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance, new NameDayService(db));

        var result = await service.RunUpcomingNameDaysReminderAsync(default);

        Assert.Equal(1, result.RecipientsNotified);
        Assert.Single(emailSender.Sent);
        Assert.Equal("with@example.org", emailSender.Sent[0].To);
    }

    [Fact]
    public async Task RunUpcomingNameDaysReminderAsync_ContinuesAfterOneRecipientFails()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var today = DateTime.UtcNow;
        var soon = today.AddDays(1);
        db.People.Add(new Person
        {
            Id = Guid.NewGuid(), FirstName = "Piotr", LastName = "Zalewski",
            NameDayMonth = soon.Month, NameDayDay = soon.Day,
            CreatedAtUtc = today, UpdatedAtUtc = today
        });
        var okUser = new AppUser { Id = Guid.NewGuid().ToString(), UserName = "ok@example.org", Email = "ok@example.org" };
        var failingUser = new AppUser { Id = Guid.NewGuid().ToString(), UserName = "failing@example.org", Email = "failing@example.org" };
        db.Users.AddRange(okUser, failingUser);
        await db.SaveChangesAsync();

        var emailSender = new SelectivelyFailingEmailSender("failing@example.org");
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance, new NameDayService(db));

        var result = await service.RunUpcomingNameDaysReminderAsync(default);

        Assert.Equal(1, result.RecipientsNotified);
        Assert.Equal(1, result.FailedSends);
        Assert.Single(emailSender.Sent);
        Assert.Equal("ok@example.org", emailSender.Sent[0]);
    }

    private class SelectivelyFailingEmailSender : IEmailSender
    {
        private readonly string _failingEmail;
        public List<string> Sent { get; } = new();

        public SelectivelyFailingEmailSender(string failingEmail) => _failingEmail = failingEmail;

        public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct)
        {
            if (toEmail == _failingEmail)
            {
                throw new InvalidOperationException("Symulowany błąd wysyłki.");
            }
            Sent.Add(toEmail);
            return Task.CompletedTask;
        }
    }
```

- [ ] **Step 6: Uruchom testy i potwierdź czerwony wynik**

```bash
dotnet build
```

Oczekiwany wynik: błąd kompilacji `CS1061` lub podobny — `ReminderService` nie zawiera definicji
`RunUpcomingNameDaysReminderAsync` (interfejs z Task 1 deklaruje metodę, klasa jej jeszcze nie
implementuje).

- [ ] **Step 7: Zaimplementuj metodę**

Dodaj do klasy `ReminderService` w `backend/src/DokPortal.Infrastructure/Services/ReminderService.cs`,
na samym końcu klasy (po `RunUpcomingMeetingsReminderAsync`, przed zamykającym nawiasem klasy):

```csharp
    public async Task<UpcomingNameDaysReminderResultDto> RunUpcomingNameDaysReminderAsync(CancellationToken ct)
    {
        var upcoming = await _nameDayService.GetUpcomingAsync(7, ct);

        if (upcoming.Count == 0)
        {
            return new UpcomingNameDaysReminderResultDto
            {
                NameDaysFound = 0,
                RecipientsNotified = 0,
                FailedSends = 0
            };
        }

        var lines = upcoming.Select(d => $"- {d.FullName} — {d.NameDayMonth:D2}.{d.NameDayDay:D2} (za {d.DaysUntil} dni)");
        var body = "Imieniny w najbliższym tygodniu:\n" + string.Join("\n", lines);

        var recipientEmails = await _db.Users
            .Where(u => u.Email != null && u.Email != "")
            .Select(u => u.Email!)
            .Distinct()
            .ToListAsync(ct);

        var recipientsNotified = 0;
        var failedSends = 0;
        foreach (var email in recipientEmails)
        {
            try
            {
                await _emailSender.SendAsync(email, "Nadchodzące imieniny", body, ct);
                recipientsNotified++;
            }
            catch (Exception ex)
            {
                failedSends++;
                _logger.LogWarning(ex, "Nie udało się wysłać przypomnienia o imieninach do {Email}", email);
            }
        }

        return new UpcomingNameDaysReminderResultDto
        {
            NameDaysFound = upcoming.Count,
            RecipientsNotified = recipientsNotified,
            FailedSends = failedSends
        };
    }
```

- [ ] **Step 8: Uruchom testy ReminderServiceTests i potwierdź, że wszystkie przechodzą**

```bash
dotnet test tests/DokPortal.Infrastructure.Tests --filter ReminderServiceTests
```

Oczekiwany wynik: 18/18 testów przechodzi (14 istniejących, niezmienionych w zachowaniu + 4 nowe).

- [ ] **Step 9: Uruchom pełny zestaw testów backendu**

```bash
dotnet build
dotnet test
```

Oczekiwany wynik: 0 błędów kompilacji, 0 ostrzeżeń, wszystkie testy (95 + 4 nowe = 99) przechodzą.

- [ ] **Step 10: Commit**

```bash
git add backend/src/DokPortal.Application/Reminders/IReminderService.cs backend/src/DokPortal.Application/Reminders/UpcomingNameDaysReminderResultDto.cs backend/src/DokPortal.Infrastructure/Services/ReminderService.cs backend/tests/DokPortal.Infrastructure.Tests/Services/ReminderServiceTests.cs
git commit -m "Dodaj ReminderService.RunUpcomingNameDaysReminderAsync: cotygodniowy digest imienin"
```

---

## Task 3: Nowa akcja na `RemindersController` + testy integracyjne

**Files:**
- Modify: `backend/src/DokPortal.Api/Controllers/RemindersController.cs`
- Modify: `backend/tests/DokPortal.Api.IntegrationTests/RemindersControllerTests.cs`

**Interfaces:**
- Consumes: `IReminderService.RunUpcomingNameDaysReminderAsync(CancellationToken)` (Task 1/2).
- Produces: `POST /api/reminders/upcoming-name-days/run` — `401` bez/ze złym nagłówkiem
  `X-Reminders-Key`, `200` z `UpcomingNameDaysReminderResultDto` przy poprawnym kluczu. Ten sam
  `Reminders:ApiKey` co pozostałe dwa endpointy — bez nowej konfiguracji.

- [ ] **Step 1: Dodaj testy integracyjne (będą czerwone, dopóki akcja nie powstanie)**

Dodaj poniższe metody testowe na końcu klasy `RemindersControllerTests` w
`backend/tests/DokPortal.Api.IntegrationTests/RemindersControllerTests.cs`, przed zamykającym
nawiasem klasy:

```csharp
    [Fact]
    public async Task RunUpcomingNameDays_WithoutKeyHeader_ReturnsUnauthorized()
    {
        var response = await Client.PostAsync("/api/reminders/upcoming-name-days/run", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RunUpcomingNameDays_WithWrongKey_ReturnsUnauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/reminders/upcoming-name-days/run");
        request.Headers.Add("X-Reminders-Key", "zly-klucz");

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RunUpcomingNameDays_WithCorrectKey_ReturnsOkWithResult()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/reminders/upcoming-name-days/run");
        request.Headers.Add("X-Reminders-Key", "testing-only-reminders-key");

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UpcomingNameDaysReminderResultDto>();
        Assert.NotNull(result);
    }
```

- [ ] **Step 2: Uruchom testy integracyjne i potwierdź czerwony wynik**

```bash
cd backend
dotnet test tests/DokPortal.Api.IntegrationTests --filter RemindersControllerTests
```

Oczekiwany wynik: kompilacja się powiedzie, ale 3 nowe testy zwrócą `404 Not Found` zamiast
oczekiwanego `401`/`200` — trasa `/api/reminders/upcoming-name-days/run` jeszcze nie istnieje.
Istniejących 6 testów (dla pozostałych dwóch endpointów) dalej przechodzi.

- [ ] **Step 3: Zaimplementuj nową akcję**

W `backend/src/DokPortal.Api/Controllers/RemindersController.cs` dodaj nową akcję zaraz po
`RunUpcomingMeetingsReminder`, przed `IsRequestAuthorized()`:

```csharp
    [HttpPost("upcoming-name-days/run")]
    public async Task<ActionResult<UpcomingNameDaysReminderResultDto>> RunUpcomingNameDaysReminder(CancellationToken ct)
    {
        if (!IsRequestAuthorized())
        {
            return Unauthorized();
        }

        var result = await _reminderService.RunUpcomingNameDaysReminderAsync(ct);
        return Ok(result);
    }
```

- [ ] **Step 4: Uruchom testy integracyjne i potwierdź, że przechodzą**

```bash
dotnet test tests/DokPortal.Api.IntegrationTests --filter RemindersControllerTests
```

Oczekiwany wynik: 9/9 testów przechodzi (6 istniejących + 3 nowe).

- [ ] **Step 5: Uruchom pełny zestaw testów backendu**

```bash
dotnet build
dotnet test
```

Oczekiwany wynik: 0 błędów, 0 ostrzeżeń, wszystkie testy (99 + 3 nowe = 102) przechodzą.

- [ ] **Step 6: Commit**

```bash
git add backend/src/DokPortal.Api/Controllers/RemindersController.cs backend/tests/DokPortal.Api.IntegrationTests/RemindersControllerTests.cs
git commit -m "Dodaj endpoint POST /api/reminders/upcoming-name-days/run"
```

---

## Task 4: Cotygodniowy GitHub Actions workflow

**Files:**
- Create: `.github/workflows/upcoming-name-days-reminder.yml`

**Interfaces:**
- Consumes: `POST /api/reminders/upcoming-name-days/run` na produkcji
  (`https://dokportal-api.azurewebsites.net`), nagłówek `X-Reminders-Key` z
  `secrets.REMINDERS_API_KEY` — **ten sam sekret, który już istnieje**, nic nowego do
  skonfigurowania.

- [ ] **Step 1: Utwórz plik workflow**

`.github/workflows/upcoming-name-days-reminder.yml`:

```yaml
name: Upcoming Name Days Reminder

on:
  schedule:
    - cron: "0 7 * * 1"
  workflow_dispatch:

jobs:
  run-reminder:
    runs-on: ubuntu-latest
    steps:
      - name: Wywołaj endpoint przypomnień
        run: |
          response=$(curl -s -w "\n%{http_code}" -X POST \
            -H "X-Reminders-Key: ${{ secrets.REMINDERS_API_KEY }}" \
            https://dokportal-api.azurewebsites.net/api/reminders/upcoming-name-days/run)
          http_code=$(echo "$response" | tail -n1)
          body=$(echo "$response" | sed '$d')
          echo "Odpowiedź: $body"
          if [ "$http_code" -ne 200 ]; then
            echo "Endpoint zwrócił kod $http_code"
            exit 1
          fi
```

Uwaga: `cron: "0 7 * * 1"` oznacza poniedziałek 07:00 UTC — ten sam dzień i godzina co cotygodniowy
workflow dla brakujących dokumentów. Niezależny workflow, bez konfliktu — GitHub Actions
uruchamia oba równolegle bez problemu. `workflow_dispatch` pozwala uruchomić ręcznie do testów.

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/upcoming-name-days-reminder.yml
git commit -m "Dodaj cotygodniowy workflow przypomnień o nadchodzących imieninach"
```

---

## Task 5: Changelog na Dashboardzie

**Files:**
- Modify: `frontend/src/app/features/dashboard/dashboard.component.ts`

**Interfaces:** brak — czysto tekstowa zmiana w tablicy `changelog`.

- [ ] **Step 1: Dodaj wpis na górze tablicy `changelog`**

W `frontend/src/app/features/dashboard/dashboard.component.ts`, w tablicy `changelog`, dodaj
nowy element jako pierwszy:

```typescript
{ date: '2026-10-02', text: 'Automatyczne przypomnienia o imieninach — raz w tygodniu każdy użytkownik portalu dostaje e-mail z listą osób mających imieniny w najbliższych 7 dniach.' },
```

- [ ] **Step 2: Zbuduj frontend**

```bash
cd frontend
npm run build -- --configuration production
```

Oczekiwany wynik: build kończy się sukcesem (ten sam znany warning budżetu
`login.component.scss`, niezwiązany z tą zmianą).

- [ ] **Step 3: Commit**

```bash
git add frontend/src/app/features/dashboard/dashboard.component.ts
git commit -m "Dashboard: wpis o przypomnieniach o nadchodzących imieninach"
```

---

## Task 6: Weryfikacja end-to-end i push

**Files:** brak nowych/zmienianych — tylko weryfikacja.

**Interfaces:** brak.

- [ ] **Step 1: Uruchom lokalny backend z tymczasowym kluczem**

Dodaj tymczasowo do `backend/src/DokPortal.Api/appsettings.Development.json` sekcję (ten plik
nigdy nie jest commitowany):

```json
"Reminders": { "ApiKey": "lokalny-klucz-testowy" }
```

Uruchom backend:

```bash
cd backend/src/DokPortal.Api
dotnet run --urls http://localhost:5227
```

- [ ] **Step 2: Zweryfikuj bez danych testowych**

```bash
curl -s -X POST -H "X-Reminders-Key: lokalny-klucz-testowy" http://localhost:5227/api/reminders/upcoming-name-days/run
```

Oczekiwany wynik: `{"nameDaysFound":0,"recipientsNotified":0,"failedSends":0}` (lub inne liczby,
jeśli lokalna baza ma już jakieś osoby z imieninami w najbliższych 7 dniach z wcześniejszych
sesji — to też poprawny wynik).

- [ ] **Step 3: Przetestuj z prawdziwymi danymi testowymi**

Utwórz przez API testową Osobę z `nameDayMonth`/`nameDayDay` ustawionymi na datę w ciągu
najbliższych 7 dni. Uruchom endpoint ponownie — oczekiwany wynik: `nameDaysFound: 1`,
`recipientsNotified: 0`, `failedSends: co najmniej 1` (bo lokalnie `Smtp:Host` nie jest
skonfigurowany, `NullEmailSender` rzuci wyjątek dla każdego użytkownika z e-mailem w lokalnej
bazie — dokładnie jak przy weryfikacji dwóch poprzednich reguł). Posprzątaj testową Osobę przez
`DELETE`.

Po testach usuń sekcję `Reminders` z `appsettings.Development.json` z powrotem.

- [ ] **Step 4: Push na master**

```bash
cd /c/eu02_install/DOKPortalLight
git push origin master
```

Poczekaj na zielone CI (Backend CI, Deploy, Frontend CI, Azure Static Web Apps CI/CD). Ta zmiana
**nie zawiera migracji** — sprawdź `/health` na produkcji po wdrożeniu tylko dla potwierdzenia, że
backend wystartował poprawnie, nie w celu weryfikacji migracji (jej nie ma).

- [ ] **Step 5: Zweryfikuj endpoint na produkcji**

```bash
curl -s --ssl-no-revoke -o /dev/null -w "Status: %{http_code}\n" -X POST https://dokportal-api.azurewebsites.net/api/reminders/upcoming-name-days/run
```

Oczekiwany wynik: `401` (brak klucza w żądaniu — potwierdza, że endpoint żyje i jest chroniony).

- [ ] **Step 6: Ręcznie uruchom nowy workflow i potwierdź sukces**

```bash
gh workflow run upcoming-name-days-reminder.yml
```

Poczekaj kilka sekund, potem:

```bash
gh run list --workflow=upcoming-name-days-reminder.yml --limit 1
```

Oczekiwany wynik: `completed success`. Sprawdź log joba (`gh run view <id> --log`), żeby
potwierdzić, że odpowiedź endpointu to `200` z realnym podsumowaniem.

---

## Self-Review Notes

- **Pokrycie spec:** architektura — rozszerzenie istniejącego kontrolera/serwisu, reużycie
  `INameDayService` (Task 2, 3), logika — pusta lista = brak wysyłki, digest do wszystkich
  użytkowników z e-mailem, brak throttlingu/migracji (Task 2), obsługa błędów per-odbiorca
  (Task 2), reużycie istniejącego klucza API (Task 3, 4, 6), testy jednostkowe i integracyjne
  (Task 2, 3), changelog (Task 5), weryfikacja end-to-end (Task 6) — wszystkie sekcje spec mają
  odpowiadające zadanie.
- **Spójność typów:** `UpcomingNameDaysReminderResultDto` ma identyczny kształt w Task 1
  (definicja), Task 2 (konstrukcja w `ReminderService`) i Task 3 (deserializacja w teście
  integracyjnym). `IReminderService.RunUpcomingNameDaysReminderAsync(CancellationToken ct)` ma tę
  samą sygnaturę w Task 1 (interfejs) i Task 2 (implementacja). Konstruktor `ReminderService`
  konsekwentnie ma 4 parametry od Task 2 dalej — wszystkie 18 wywołań w pliku testowym (14
  naprawionych przez `sed` + 4 nowe pisane od razu z 4 argumentami) są spójne.
- **Poza zakresem (zgodnie ze spec):** filtrowanie odbiorców po roli, krótsze okno czasowe,
  personalizacja treści pod rolę — świadomie nieuwzględnione w tym planie.
