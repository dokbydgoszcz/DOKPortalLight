# Przypomnienia o nadchodzących spotkaniach — plan implementacji

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Codzienne, automatyczne przypomnienie e-mail do katechisty prowadzącego o spotkaniu przypisanym do jego sprawy DOK, wysyłane dzień przed spotkaniem, bez ręcznej akcji użytkownika.

**Architecture:** Rozszerzenie istniejącego silnika przypomnień zbudowanego w [2026-10-01-missing-documents-reminder.md](2026-10-01-missing-documents-reminder.md) — nowa metoda na istniejącym `IReminderService`/`ReminderService`, nowa akcja na istniejącym `RemindersController` (ta sama ochrona kluczem API, ten sam `Reminders:ApiKey`), nowy codzienny workflow GitHub Actions obok już istniejącego tygodniowego. Żaden nowy komponent architektoniczny nie powstaje — to rozszerzenie, nie nowy podsystem.

**Tech Stack:** ASP.NET Core 8, EF Core (SQL Server w produkcji, SQLite w testach integracyjnych, InMemory w testach jednostkowych), xUnit, GitHub Actions.

**Spec:** [docs/superpowers/specs/2026-10-02-upcoming-meetings-reminder-design.md](../specs/2026-10-02-upcoming-meetings-reminder-design.md)

## Global Constraints

- Commity i push idą bezpośrednio na branch `master` (bez PR) — taki jest ustalony przepływ w tym repo.
- Nigdy nie commituj `backend/src/DokPortal.Api/appsettings.Development.json` ani katalogu `.claude/`.
- Po zakończeniu funkcji dodaj wpis do `frontend/src/app/features/dashboard/dashboard.component.ts` (tablica `changelog`, nowy wpis na górze, data `YYYY-MM-DD`, język prosty/nietechniczny).
- Odbiorcą jest wyłącznie katechista prowadzący — żadnego e-maila do podopiecznego, żadnego zbiorczego digestu do Dyrektora DOK w tej funkcji.
- Spotkania bez przypisanej sprawy DOK (`DokCaseId == null`, spotkania grupowe) są zawsze pomijane.
- Okno czasowe jest sztywne: dokładnie 1 dzień przed spotkaniem (`MeetingDate == jutro`, UTC) — nie throttling okresowy jak przy dokumentach.
- `Meeting.ReminderSentAtUtc` jest stemplowane **tylko po udanej wysyłce** do katechisty — nieudana wysyłka lub brak e-maila katechisty oznacza brak stempla (spotkanie zostanie uwzględnione ponownie przy kolejnym uruchomieniu zadania następnego dnia, dopóki `MeetingDate == jutro` wciąż jest prawdą).
- Ten sam sekret `Reminders:ApiKey` / `secrets.REMINDERS_API_KEY`, który już jest skonfigurowany w Azure i GitHub dla przypomnień o brakujących dokumentach, jest reużywany — **nie potrzeba żadnego nowego sekretu**.
- Każda wysyłka e-mail (`IEmailSender.SendAsync`) w `ReminderService` jest owinięta w `try/catch` per odbiorca — błąd jednego nie przerywa pozostałych, loguje się przez `ILogger<ReminderService>.LogWarning`.

---

## Task 1: Pole `ReminderSentAtUtc` na `Meeting` + migracja EF Core

**Files:**
- Modify: `backend/src/DokPortal.Domain/Entities/Meeting.cs`
- Create: migracja EF Core (plik generowany przez narzędzie, patrz Step 2)

**Interfaces:**
- Produces: `Meeting.ReminderSentAtUtc` (`DateTime?`), używane przez `ReminderService` w Task 3.

- [ ] **Step 1: Dodaj pole do encji**

W `backend/src/DokPortal.Domain/Entities/Meeting.cs` dodaj nową właściwość na końcu klasy (pełna treść pliku po zmianie):

```csharp
namespace DokPortal.Domain.Entities;

public class Meeting : ISoftDeletable
{
    public Guid Id { get; set; }
    public Guid? DokCaseId { get; set; }
    public DokCase? DokCase { get; set; }
    public string? GroupLabel { get; set; }
    public DateOnly MeetingDate { get; set; }
    public bool? IsAttended { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    public DateTime? ReminderSentAtUtc { get; set; }
}
```

- [ ] **Step 2: Wygeneruj migrację EF Core**

Z katalogu `backend/src/DokPortal.Api` uruchom:

```bash
cd backend/src/DokPortal.Api
dotnet ef migrations add AddMeetingReminderSentAtUtc --project ../DokPortal.Infrastructure --startup-project .
```

Oczekiwany efekt: nowe pliki `backend/src/DokPortal.Infrastructure/Migrations/<timestamp>_AddMeetingReminderSentAtUtc.cs` i `.Designer.cs`, oraz zaktualizowany `AppDbContextModelSnapshot.cs` z dodaną kolumną `ReminderSentAtUtc` (nullable `datetime2`) na tabeli `Meetings`. Te same ostrzeżenia o globalnych query filterach co przy poprzednich migracjach tej encji mogą się pojawić — to pre-istniejące, nie dotyczą tej zmiany.

- [ ] **Step 3: Zbuduj rozwiązanie i upewnij się, że istniejące testy przechodzą**

```bash
cd backend
dotnet build
dotnet test
```

Oczekiwany wynik: 0 błędów kompilacji, wszystkie dotychczasowe testy (86) nadal przechodzą — ta zmiana jeszcze nic nie używa nowego pola.

- [ ] **Step 4: Commit**

```bash
git add backend/src/DokPortal.Domain/Entities/Meeting.cs backend/src/DokPortal.Infrastructure/Migrations/
git commit -m "Dodaj Meeting.ReminderSentAtUtc + migracja EF Core"
```

---

## Task 2: DTO wyniku i rozszerzenie `IReminderService`

**Files:**
- Create: `backend/src/DokPortal.Application/Reminders/UpcomingMeetingsReminderResultDto.cs`
- Modify: `backend/src/DokPortal.Application/Reminders/IReminderService.cs`

**Interfaces:**
- Produces: `UpcomingMeetingsReminderResultDto` z polami `MeetingsProcessed` (int), `EmailsSentToCatechists` (int), `FailedSends` (int); `IReminderService.RunUpcomingMeetingsReminderAsync(CancellationToken)` zwracające `Task<UpcomingMeetingsReminderResultDto>`. Używane przez `ReminderService` (Task 3) i `RemindersController` (Task 4).

- [ ] **Step 1: Utwórz DTO wyniku**

`backend/src/DokPortal.Application/Reminders/UpcomingMeetingsReminderResultDto.cs`:

```csharp
namespace DokPortal.Application.Reminders;

public record UpcomingMeetingsReminderResultDto
{
    public required int MeetingsProcessed { get; init; }
    public required int EmailsSentToCatechists { get; init; }
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
}
```

- [ ] **Step 3: Potwierdź czerwony wynik budowania**

```bash
cd backend
dotnet build
```

Oczekiwany wynik: błąd kompilacji `CS0535` — `ReminderService` nie implementuje `IReminderService.RunUpcomingMeetingsReminderAsync(CancellationToken)`. To oczekiwane — implementacja przychodzi w Task 3.

- [ ] **Step 4: Commit**

Ten krok commitujemy dopiero razem z Task 3, bo samodzielnie zostawia solucję w stanie nieudanej kompilacji. Przejdź od razu do Task 3.

---

## Task 3: `ReminderService.RunUpcomingMeetingsReminderAsync` (TDD)

**Files:**
- Modify: `backend/tests/DokPortal.Infrastructure.Tests/Services/ReminderServiceTests.cs`
- Modify: `backend/src/DokPortal.Infrastructure/Services/ReminderService.cs`

**Interfaces:**
- Consumes: `AppDbContext.Meetings` (`DbSet<Meeting>`, już istnieje), `Meeting.ReminderSentAtUtc` (Task 1), `UpcomingMeetingsReminderResultDto` (Task 2), `IEmailSender.SendAsync` (istniejący), `ILogger<ReminderService>` (istniejący, już wstrzyknięty w konstruktorze).
- Produces: `ReminderService.RunUpcomingMeetingsReminderAsync(CancellationToken ct)` zaimplementowane na istniejącej klasie `ReminderService`.

- [ ] **Step 1: Dodaj nowe testy do istniejącego pliku**

Dodaj poniższe metody testowe na końcu klasy `ReminderServiceTests` w `backend/tests/DokPortal.Infrastructure.Tests/Services/ReminderServiceTests.cs`, bezpośrednio przed zamykającym nawiasem klasy (plik już ma `using DokPortal.Domain.Entities;`, który obejmuje też `Meeting` — żadne nowe `using` nie są potrzebne):

```csharp
    [Fact]
    public async Task RunUpcomingMeetingsReminderAsync_SendsReminderForMeetingTomorrowWithCase()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = NewPerson();
        var catechist = NewPerson("katechista@example.org");
        db.People.AddRange(person, catechist);
        var dokCase = NewDokCase(person.Id, catechist.Id);
        db.DokCases.Add(dokCase);
        var meeting = new Meeting
        {
            Id = Guid.NewGuid(), DokCaseId = dokCase.Id,
            MeetingDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1),
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance);

        var result = await service.RunUpcomingMeetingsReminderAsync(default);

        Assert.Equal(1, result.MeetingsProcessed);
        Assert.Equal(1, result.EmailsSentToCatechists);
        Assert.Single(emailSender.Sent);
        Assert.Equal("katechista@example.org", emailSender.Sent[0].To);

        var reloaded = await db.Meetings.AsNoTracking().SingleAsync(m => m.Id == meeting.Id);
        Assert.NotNull(reloaded.ReminderSentAtUtc);
    }

    [Fact]
    public async Task RunUpcomingMeetingsReminderAsync_SkipsMeetingNotHappeningTomorrow()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = NewPerson();
        var catechist = NewPerson("katechista@example.org");
        db.People.AddRange(person, catechist);
        var dokCase = NewDokCase(person.Id, catechist.Id);
        db.DokCases.Add(dokCase);
        db.Meetings.AddRange(
            new Meeting { Id = Guid.NewGuid(), DokCaseId = dokCase.Id, MeetingDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2), CreatedAtUtc = DateTime.UtcNow },
            new Meeting { Id = Guid.NewGuid(), DokCaseId = dokCase.Id, MeetingDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance);

        var result = await service.RunUpcomingMeetingsReminderAsync(default);

        Assert.Equal(0, result.MeetingsProcessed);
        Assert.Empty(emailSender.Sent);
    }

    [Fact]
    public async Task RunUpcomingMeetingsReminderAsync_SkipsGroupMeetingWithoutDokCase()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        db.Meetings.Add(new Meeting
        {
            Id = Guid.NewGuid(), DokCaseId = null, GroupLabel = "Spotkanie grupowe",
            MeetingDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance);

        var result = await service.RunUpcomingMeetingsReminderAsync(default);

        Assert.Equal(0, result.MeetingsProcessed);
        Assert.Empty(emailSender.Sent);
    }

    [Fact]
    public async Task RunUpcomingMeetingsReminderAsync_SkipsWhenCatechistHasNoEmail()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = NewPerson();
        var catechist = NewPerson(email: null);
        db.People.AddRange(person, catechist);
        var dokCase = NewDokCase(person.Id, catechist.Id);
        db.DokCases.Add(dokCase);
        var meeting = new Meeting
        {
            Id = Guid.NewGuid(), DokCaseId = dokCase.Id,
            MeetingDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), CreatedAtUtc = DateTime.UtcNow
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance);

        var result = await service.RunUpcomingMeetingsReminderAsync(default);

        Assert.Equal(1, result.MeetingsProcessed);
        Assert.Equal(0, result.EmailsSentToCatechists);
        var reloaded = await db.Meetings.AsNoTracking().SingleAsync(m => m.Id == meeting.Id);
        Assert.Null(reloaded.ReminderSentAtUtc);
    }

    [Fact]
    public async Task RunUpcomingMeetingsReminderAsync_DoesNotStampWhenSendFails()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = NewPerson();
        var catechist = NewPerson("katechista@example.org");
        db.People.AddRange(person, catechist);
        var dokCase = NewDokCase(person.Id, catechist.Id);
        db.DokCases.Add(dokCase);
        var meeting = new Meeting
        {
            Id = Guid.NewGuid(), DokCaseId = dokCase.Id,
            MeetingDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), CreatedAtUtc = DateTime.UtcNow
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        var service = new ReminderService(db, new ThrowingEmailSender(), NullLogger<ReminderService>.Instance);
        var result = await service.RunUpcomingMeetingsReminderAsync(default);

        Assert.Equal(0, result.EmailsSentToCatechists);
        Assert.Equal(1, result.FailedSends);
        var reloaded = await db.Meetings.AsNoTracking().SingleAsync(m => m.Id == meeting.Id);
        Assert.Null(reloaded.ReminderSentAtUtc);
    }

    [Fact]
    public async Task RunUpcomingMeetingsReminderAsync_SkipsMeetingAlreadyReminded()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = NewPerson();
        var catechist = NewPerson("katechista@example.org");
        db.People.AddRange(person, catechist);
        var dokCase = NewDokCase(person.Id, catechist.Id);
        db.DokCases.Add(dokCase);
        db.Meetings.Add(new Meeting
        {
            Id = Guid.NewGuid(), DokCaseId = dokCase.Id,
            MeetingDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), CreatedAtUtc = DateTime.UtcNow,
            ReminderSentAtUtc = DateTime.UtcNow.AddHours(-2)
        });
        await db.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance);

        var result = await service.RunUpcomingMeetingsReminderAsync(default);

        Assert.Equal(0, result.MeetingsProcessed);
        Assert.Empty(emailSender.Sent);
    }
```

- [ ] **Step 2: Uruchom testy i potwierdź czerwony wynik**

```bash
cd backend
dotnet test tests/DokPortal.Infrastructure.Tests --filter ReminderServiceTests
```

Oczekiwany wynik: błąd kompilacji — `ReminderService` wciąż nie implementuje `RunUpcomingMeetingsReminderAsync` (z Task 2, Step 3).

- [ ] **Step 3: Zaimplementuj metodę**

Dodaj do klasy `ReminderService` w `backend/src/DokPortal.Infrastructure/Services/ReminderService.cs`, obok istniejącego `MissingDocumentRow` i `RunMissingDocumentsReminderAsync` (wewnątrz klasy, po zamknięciu `RunMissingDocumentsReminderAsync` a przed zamykającym nawiasem klasy):

```csharp
    private sealed record UpcomingMeetingRow(
        Guid MeetingId,
        DateOnly MeetingDate,
        string PersonFirstName,
        string PersonLastName,
        string? CatechistEmail);

    public async Task<UpcomingMeetingsReminderResultDto> RunUpcomingMeetingsReminderAsync(CancellationToken ct)
    {
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        var rows = await (
            from meeting in _db.Meetings
            where meeting.DokCaseId != null && meeting.MeetingDate == tomorrow && meeting.ReminderSentAtUtc == null
            join dokCase in _db.DokCases on meeting.DokCaseId equals dokCase.Id
            join person in _db.People on dokCase.PersonId equals person.Id
            join catechist in _db.People on dokCase.CatechistPersonId equals catechist.Id
            select new UpcomingMeetingRow(meeting.Id, meeting.MeetingDate, person.FirstName, person.LastName, catechist.Email)
        ).ToListAsync(ct);

        var emailsSentToCatechists = 0;
        var failedSends = 0;
        var meetingIdsToStamp = new List<Guid>();

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.CatechistEmail))
            {
                continue;
            }

            var personFullName = $"{row.PersonFirstName} {row.PersonLastName}";
            var body = $"Przypomnienie: jutro ({row.MeetingDate:yyyy-MM-dd}) odbywa się spotkanie z podopiecznym {personFullName}.";
            try
            {
                await _emailSender.SendAsync(row.CatechistEmail!, "Nadchodzące spotkanie — przypomnienie", body, ct);
                emailsSentToCatechists++;
                meetingIdsToStamp.Add(row.MeetingId);
            }
            catch (Exception ex)
            {
                failedSends++;
                _logger.LogWarning(ex, "Nie udało się wysłać przypomnienia o spotkaniu {MeetingId} do katechisty", row.MeetingId);
            }
        }

        if (meetingIdsToStamp.Count > 0)
        {
            var now = DateTime.UtcNow;
            var meetingsToStamp = await _db.Meetings
                .Where(m => meetingIdsToStamp.Contains(m.Id))
                .ToListAsync(ct);
            foreach (var meeting in meetingsToStamp)
            {
                meeting.ReminderSentAtUtc = now;
            }
            await _db.SaveChangesAsync(ct);
        }

        return new UpcomingMeetingsReminderResultDto
        {
            MeetingsProcessed = rows.Count,
            EmailsSentToCatechists = emailsSentToCatechists,
            FailedSends = failedSends
        };
    }
```

- [ ] **Step 4: Uruchom testy i potwierdź, że przechodzą**

```bash
cd backend
dotnet test tests/DokPortal.Infrastructure.Tests --filter ReminderServiceTests
```

Oczekiwany wynik: 14/14 testów przechodzi (8 istniejących dla brakujących dokumentów + 6 nowych).

- [ ] **Step 5: Uruchom pełny zestaw testów backendu**

```bash
cd backend
dotnet build
dotnet test
```

Oczekiwany wynik: 0 błędów kompilacji, 0 ostrzeżeń, wszystkie testy (86 + 6 nowych = 92) przechodzą.

- [ ] **Step 6: Commit**

```bash
git add backend/src/DokPortal.Application/Reminders/IReminderService.cs backend/src/DokPortal.Application/Reminders/UpcomingMeetingsReminderResultDto.cs backend/src/DokPortal.Infrastructure/Services/ReminderService.cs backend/tests/DokPortal.Infrastructure.Tests/Services/ReminderServiceTests.cs
git commit -m "Dodaj ReminderService.RunUpcomingMeetingsReminderAsync: przypomnienia o spotkaniach dzień wcześniej"
```

---

## Task 4: Nowa akcja na `RemindersController` + testy integracyjne

**Files:**
- Modify: `backend/src/DokPortal.Api/Controllers/RemindersController.cs`
- Modify: `backend/tests/DokPortal.Api.IntegrationTests/RemindersControllerTests.cs`

**Interfaces:**
- Consumes: `IReminderService.RunUpcomingMeetingsReminderAsync(CancellationToken)` (Task 2/3).
- Produces: `POST /api/reminders/upcoming-meetings/run` — `401` bez/ze złym nagłówkiem `X-Reminders-Key`, `200` z `UpcomingMeetingsReminderResultDto` przy poprawnym kluczu. Ten sam `Reminders:ApiKey` co istniejący endpoint `/api/reminders/missing-documents/run` — bez nowej konfiguracji.

- [ ] **Step 1: Dodaj testy integracyjne (będą czerwone, dopóki akcja nie powstanie)**

Dodaj poniższe metody testowe na końcu klasy `RemindersControllerTests` w `backend/tests/DokPortal.Api.IntegrationTests/RemindersControllerTests.cs`, przed zamykającym nawiasem klasy (plik już importuje `DokPortal.Application.Reminders`, co obejmuje też `UpcomingMeetingsReminderResultDto`):

```csharp
    [Fact]
    public async Task RunUpcomingMeetings_WithoutKeyHeader_ReturnsUnauthorized()
    {
        var response = await Client.PostAsync("/api/reminders/upcoming-meetings/run", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RunUpcomingMeetings_WithWrongKey_ReturnsUnauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/reminders/upcoming-meetings/run");
        request.Headers.Add("X-Reminders-Key", "zly-klucz");

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RunUpcomingMeetings_WithCorrectKey_ReturnsOkWithResult()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/reminders/upcoming-meetings/run");
        request.Headers.Add("X-Reminders-Key", "testing-only-reminders-key");

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UpcomingMeetingsReminderResultDto>();
        Assert.NotNull(result);
    }
```

- [ ] **Step 2: Uruchom testy integracyjne i potwierdź czerwony wynik**

```bash
cd backend
dotnet test tests/DokPortal.Api.IntegrationTests --filter RemindersControllerTests
```

Oczekiwany wynik: kompilacja się powiedzie, ale 3 nowe testy zwrócą `404 Not Found` zamiast oczekiwanego `401`/`200` — trasa `/api/reminders/upcoming-meetings/run` jeszcze nie istnieje. Istniejące 3 testy dla `/missing-documents/run` dalej przechodzą.

- [ ] **Step 3: Zaimplementuj nową akcję i wydziel wspólną weryfikację klucza**

Zmień `backend/src/DokPortal.Api/Controllers/RemindersController.cs` na (pełna treść pliku po zmianie):

```csharp
using System.Security.Cryptography;
using System.Text;
using DokPortal.Application.Reminders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/reminders")]
public class RemindersController : ControllerBase
{
    private readonly IReminderService _reminderService;
    private readonly IConfiguration _configuration;

    public RemindersController(IReminderService reminderService, IConfiguration configuration)
    {
        _reminderService = reminderService;
        _configuration = configuration;
    }

    [HttpPost("missing-documents/run")]
    public async Task<ActionResult<MissingDocumentsReminderResultDto>> RunMissingDocumentsReminder(CancellationToken ct)
    {
        if (!IsRequestAuthorized())
        {
            return Unauthorized();
        }

        var result = await _reminderService.RunMissingDocumentsReminderAsync(ct);
        return Ok(result);
    }

    [HttpPost("upcoming-meetings/run")]
    public async Task<ActionResult<UpcomingMeetingsReminderResultDto>> RunUpcomingMeetingsReminder(CancellationToken ct)
    {
        if (!IsRequestAuthorized())
        {
            return Unauthorized();
        }

        var result = await _reminderService.RunUpcomingMeetingsReminderAsync(ct);
        return Ok(result);
    }

    private bool IsRequestAuthorized()
    {
        var configuredKey = _configuration["Reminders:ApiKey"];
        return !string.IsNullOrEmpty(configuredKey) && HasValidKey(Request.Headers["X-Reminders-Key"], configuredKey);
    }

    private static bool HasValidKey(StringValues provided, string configured)
    {
        if (provided.Count != 1 || string.IsNullOrEmpty(provided[0]))
        {
            return false;
        }

        var providedBytes = Encoding.UTF8.GetBytes(provided[0]!);
        var configuredBytes = Encoding.UTF8.GetBytes(configured);
        return providedBytes.Length == configuredBytes.Length
            && CryptographicOperations.FixedTimeEquals(providedBytes, configuredBytes);
    }
}
```

- [ ] **Step 4: Uruchom testy integracyjne i potwierdź, że przechodzą**

```bash
cd backend
dotnet test tests/DokPortal.Api.IntegrationTests --filter RemindersControllerTests
```

Oczekiwany wynik: 6/6 testów przechodzi (3 istniejące + 3 nowe).

- [ ] **Step 5: Uruchom pełny zestaw testów backendu**

```bash
cd backend
dotnet build
dotnet test
```

Oczekiwany wynik: 0 błędów, 0 ostrzeżeń, wszystkie testy (92 + 3 nowe = 95) przechodzą.

- [ ] **Step 6: Commit**

```bash
git add backend/src/DokPortal.Api/Controllers/RemindersController.cs backend/tests/DokPortal.Api.IntegrationTests/RemindersControllerTests.cs
git commit -m "Dodaj endpoint POST /api/reminders/upcoming-meetings/run"
```

---

## Task 5: Codzienny GitHub Actions workflow

**Files:**
- Create: `.github/workflows/upcoming-meetings-reminder.yml`

**Interfaces:**
- Consumes: `POST /api/reminders/upcoming-meetings/run` na produkcji (`https://dokportal-api.azurewebsites.net`), nagłówek `X-Reminders-Key` z `secrets.REMINDERS_API_KEY` — **ten sam sekret, który już istnieje** z poprzedniej funkcji, nic nowego do skonfigurowania.

- [ ] **Step 1: Utwórz plik workflow**

`.github/workflows/upcoming-meetings-reminder.yml`:

```yaml
name: Upcoming Meetings Reminder

on:
  schedule:
    - cron: "0 7 * * *"
  workflow_dispatch:

jobs:
  run-reminder:
    runs-on: ubuntu-latest
    steps:
      - name: Wywołaj endpoint przypomnień
        run: |
          response=$(curl -s -w "\n%{http_code}" -X POST \
            -H "X-Reminders-Key: ${{ secrets.REMINDERS_API_KEY }}" \
            https://dokportal-api.azurewebsites.net/api/reminders/upcoming-meetings/run)
          http_code=$(echo "$response" | tail -n1)
          body=$(echo "$response" | sed '$d')
          echo "Odpowiedź: $body"
          if [ "$http_code" -ne 200 ]; then
            echo "Endpoint zwrócił kod $http_code"
            exit 1
          fi
```

Uwaga: `cron: "0 7 * * *"` oznacza codziennie o 07:00 UTC — ta sama godzina co cotygodniowy workflow dla brakujących dokumentów, ale każdego dnia zamiast tylko w poniedziałek. `workflow_dispatch` pozwala uruchomić ręcznie z zakładki Actions, przydatne do testów.

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/upcoming-meetings-reminder.yml
git commit -m "Dodaj codzienny workflow przypomnień o nadchodzących spotkaniach"
```

Ten workflow zadziała od razu po wdrożeniu — `Reminders:ApiKey` i `REMINDERS_API_KEY` są już skonfigurowane z poprzedniej funkcji, żadnego dodatkowego ręcznego kroku od użytkownika nie potrzeba.

---

## Task 6: Changelog na Dashboardzie

**Files:**
- Modify: `frontend/src/app/features/dashboard/dashboard.component.ts`

**Interfaces:** brak — czysto tekstowa zmiana w tablicy `changelog`.

- [ ] **Step 1: Dodaj wpis na górze tablicy `changelog`**

W `frontend/src/app/features/dashboard/dashboard.component.ts`, w tablicy `changelog`, dodaj nowy element jako pierwszy (przed dotychczasowym pierwszym wpisem):

```typescript
{ date: '2026-10-02', text: 'Automatyczne przypomnienia o spotkaniach — katechista prowadzący dostaje e-mail dzień przed spotkaniem przypisanym do jego sprawy DOK.' },
```

- [ ] **Step 2: Zbuduj frontend**

```bash
cd frontend
npm run build -- --configuration production
```

Oczekiwany wynik: build kończy się sukcesem (ten sam znany warning budżetu `login.component.scss`, niezwiązany z tą zmianą).

- [ ] **Step 3: Commit**

```bash
git add frontend/src/app/features/dashboard/dashboard.component.ts
git commit -m "Dashboard: wpis o przypomnieniach o nadchodzących spotkaniach"
```

---

## Task 7: Weryfikacja end-to-end i push

**Files:** brak nowych/zmienianych — tylko weryfikacja.

**Interfaces:** brak.

- [ ] **Step 1: Uruchom lokalny backend z tymczasowym kluczem i zweryfikuj ręcznie**

Dodaj tymczasowo do `backend/src/DokPortal.Api/appsettings.Development.json` sekcję (ten plik nigdy nie jest commitowany):

```json
"Reminders": { "ApiKey": "lokalny-klucz-testowy" }
```

Uruchom backend:

```bash
cd backend/src/DokPortal.Api
dotnet run --urls http://localhost:5227
```

W drugim terminalu, bez żadnych spotkań jutro w lokalnej bazie — oczekiwany `200` z zerowym podsumowaniem:

```bash
curl -s -X POST -H "X-Reminders-Key: lokalny-klucz-testowy" http://localhost:5227/api/reminders/upcoming-meetings/run
```

Oczekiwany wynik: `{"meetingsProcessed":0,"emailsSentToCatechists":0,"failedSends":0}` (lub inne liczby, jeśli w lokalnej bazie są już jakieś dane testowe z wcześniejszych sesji — to też jest poprawny wynik).

- [ ] **Step 2: Przetestuj z prawdziwymi danymi testowymi (opcjonalnie, zalecane)**

Utwórz przez API testową Osobę (podopiecznego), testową Osobę (katechistę z adresem e-mail), testową Sprawę DOK łączącą obie, oraz testowe Spotkanie z `meetingDate` ustawionym na jutrzejszą datę i tym `dokCaseId`. Uruchom endpoint ponownie — oczekiwany wynik: `meetingsProcessed: 1`, `emailsSentToCatechists: 0`, `failedSends: 1` (bo lokalnie `Smtp:Host` nie jest skonfigurowany, więc `NullEmailSender` rzuci wyjątek — to oczekiwane, dokładnie jak przy weryfikacji brakujących dokumentów). Uruchom endpoint jeszcze raz i potwierdź te same liczby (brak stempla po nieudanej wysyłce, więc spotkanie wciąż się kwalifikuje). Posprzątaj testowe dane przez `DELETE` na utworzonych zasobach.

Po testach usuń sekcję `Reminders` z `appsettings.Development.json` z powrotem.

- [ ] **Step 3: Push na master**

```bash
cd /c/eu02_install/DOKPortalLight
git push origin master
```

Poczekaj na zielone CI (Backend CI, Deploy, Frontend CI, Azure Static Web Apps CI/CD) i sprawdź `/health` na produkcji po wdrożeniu migracji z Task 1 — tak samo jak przy poprzednich migracjach w tej sesji.

- [ ] **Step 4: Zweryfikuj endpoint na produkcji**

```bash
curl -s --ssl-no-revoke -o /dev/null -w "Status: %{http_code}\n" -X POST https://dokportal-api.azurewebsites.net/api/reminders/upcoming-meetings/run
```

Oczekiwany wynik: `401` (brak klucza w żądaniu — to potwierdza, że endpoint żyje i jest chroniony; nie trzeba znać prawdziwego `Reminders:ApiKey`, żeby to sprawdzić).

- [ ] **Step 5: Ręcznie uruchom nowy workflow i potwierdź sukces**

```bash
gh workflow run upcoming-meetings-reminder.yml
```

Poczekaj kilka sekund, potem:

```bash
gh run list --workflow=upcoming-meetings-reminder.yml --limit 1
```

Oczekiwany wynik: `completed success`. Sprawdź log joba (`gh run view <id> --log`), żeby potwierdzić, że odpowiedź endpointu to `200` z realnym podsumowaniem — dokładnie jak przy weryfikacji poprzedniego workflow.

---

## Self-Review Notes

- **Pokrycie spec:** architektura — rozszerzenie istniejącego kontrolera/serwisu (Task 3, 4), model danych (Task 1), logika doboru — jutro + DokCaseId != null + brak stempla (Task 3), obsługa błędów per-odbiorca (Task 3), reużycie istniejącego klucza API bez nowej konfiguracji (Task 4, 5, 7), testy jednostkowe i integracyjne (Task 3, 4), changelog (Task 6), weryfikacja end-to-end (Task 7) — wszystkie sekcje spec mają odpowiadające zadanie.
- **Spójność typów:** `UpcomingMeetingsReminderResultDto` ma identyczny kształt w Task 2 (definicja), Task 3 (konstrukcja w `ReminderService`) i Task 4 (deserializacja w teście integracyjnym). `IReminderService.RunUpcomingMeetingsReminderAsync(CancellationToken ct)` ma tę samą sygnaturę w Task 2 (interfejs) i Task 3 (implementacja). `RemindersController` zachowuje istniejące zachowanie `/missing-documents/run` (wydzielenie `IsRequestAuthorized()` to czysty refaktor, bez zmiany logiki — testy z Task 5 poprzedniego planu dalej przechodzą).
- **Poza zakresem (zgodnie ze spec):** e-mail do podopiecznego, zbiorczy digest do Dyrektora DOK, przypomnienia o spotkaniach grupowych, konfigurowalne okno czasowe — świadomie nieuwzględnione w tym planie.
