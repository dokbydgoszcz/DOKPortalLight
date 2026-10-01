# Przypomnienia o brakujących dokumentach — plan implementacji

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cotygodniowe, automatyczne przypomnienia e-mail o brakujących dokumentach w sprawach DOK — do katechisty prowadzącego (per sprawa) i zbiorczo do Dyrektorów DOK — wyzwalane przez cykliczny GitHub Actions, bez ręcznej akcji użytkownika.

**Architecture:** Nowy `IReminderService`/`ReminderService` w warstwie Infrastructure odpytuje `CaseDocuments`/`DokCases`/`People`/role Identity przez `AppDbContext` i wysyła e-maile przez już istniejący `IEmailSender`. Nowy `RemindersController` wystawia `POST /api/reminders/missing-documents/run`, chroniony nie przez JWT (bo wywołuje go maszyna), tylko przez porównanie nagłówka `X-Reminders-Key` z konfiguracją `Reminders:ApiKey` (ten sam wzorzec Azure App Service settings co `Jwt`/`BlobStorage`/`Smtp`). Nowy plik `.github/workflows/missing-documents-reminder.yml` wywołuje ten endpoint co tydzień przez `curl`, z kluczem z `secrets.REMINDERS_API_KEY`.

**Tech Stack:** ASP.NET Core 8, EF Core (SQL Server w produkcji, SQLite w testach integracyjnych, InMemory w testach jednostkowych), xUnit, GitHub Actions.

**Spec:** [docs/superpowers/specs/2026-10-01-missing-documents-reminder-design.md](../specs/2026-10-01-missing-documents-reminder-design.md)

## Global Constraints

- Commity i push idą bezpośrednio na branch `master` (bez PR) — taki jest ustalony przepływ w tym repo.
- Nigdy nie commituj `backend/src/DokPortal.Api/appsettings.Development.json` ani katalogu `.claude/`.
- Po każdej zakończonej fazie/funkcji dodaj wpis do `frontend/src/app/features/dashboard/dashboard.component.ts` (tablica `changelog`, nowe wpisy na górze, data `YYYY-MM-DD`, język prosty/nietechniczny) — to zadanie nie dotyka frontendu poza tym jednym plikiem.
- `Reminders:ApiKey` to wewnętrzny sekret współdzielony z GitHub Actions, nie dane logowania do zewnętrznego systemu — w przeciwieństwie do `BlobStorage`/`Smtp`, wolno mu mieć ustawioną (dowolną, nie-produkcyjną) wartość w `appsettings.Testing.json`, żeby dało się przetestować ścieżkę sukcesu.
- Brak `Reminders:ApiKey` w konfiguracji (produkcja bez ustawionego App Service setting, lokalny dev bez wpisu w `appsettings.Development.json`) → endpoint zawsze zwraca `401`, niezależnie od przesłanego nagłówka.
- Każda wysyłka e-mail (`IEmailSender.SendAsync`) w `ReminderService` jest owinięta w `try/catch` per odbiorca — błąd jednego nie przerywa pozostałych, loguje się przez `ILogger<ReminderService>.LogWarning`.

---

## Task 1: Pole `LastReminderSentAtUtc` na `CaseDocument` + migracja EF Core

**Files:**
- Modify: `backend/src/DokPortal.Domain/Entities/CaseDocument.cs`
- Create: migracja EF Core (plik generowany przez narzędzie, patrz Step 2)

**Interfaces:**
- Produces: `CaseDocument.LastReminderSentAtUtc` (`DateTime?`), używane przez `ReminderService` w Task 3.

- [ ] **Step 1: Dodaj pole do encji**

W `backend/src/DokPortal.Domain/Entities/CaseDocument.cs` dodaj nową właściwość na końcu klasy:

```csharp
namespace DokPortal.Domain.Entities;

public class CaseDocument
{
    public Guid Id { get; set; }
    public Guid DokCaseId { get; set; }
    public DokCase? DokCase { get; set; }
    public required string Name { get; set; }
    public bool IsProvided { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public string? BlobPath { get; set; }
    public string? OriginalFileName { get; set; }
    public string? ContentType { get; set; }
    public long? FileSizeBytes { get; set; }
    public DateTime? UploadedAtUtc { get; set; }

    public DateTime? LastReminderSentAtUtc { get; set; }
}
```

- [ ] **Step 2: Wygeneruj migrację EF Core**

Z katalogu `backend/src/DokPortal.Api` uruchom:

```bash
cd backend/src/DokPortal.Api
dotnet ef migrations add AddCaseDocumentLastReminderSentAtUtc --project ../DokPortal.Infrastructure --startup-project .
```

Oczekiwany efekt: nowe pliki `backend/src/DokPortal.Infrastructure/Migrations/<timestamp>_AddCaseDocumentLastReminderSentAtUtc.cs` i `.Designer.cs`, oraz zaktualizowany `AppDbContextModelSnapshot.cs` z dodaną kolumną `LastReminderSentAtUtc` (nullable `datetime2`) na tabeli `CaseDocuments`.

- [ ] **Step 3: Zbuduj rozwiązanie i upewnij się, że istniejące testy przechodzą**

```bash
cd backend
dotnet build
dotnet test
```

Oczekiwany wynik: 0 błędów kompilacji, wszystkie dotychczasowe testy (75) nadal przechodzą — ta zmiana jeszcze nic nie używa nowego pola.

- [ ] **Step 4: Commit**

```bash
git add backend/src/DokPortal.Domain/Entities/CaseDocument.cs backend/src/DokPortal.Infrastructure/Migrations/
git commit -m "Dodaj CaseDocument.LastReminderSentAtUtc + migracja EF Core"
```

---

## Task 2: Interfejs `IReminderService` i DTO wyniku

**Files:**
- Create: `backend/src/DokPortal.Application/Reminders/IReminderService.cs`
- Create: `backend/src/DokPortal.Application/Reminders/MissingDocumentsReminderResultDto.cs`

**Interfaces:**
- Produces: `IReminderService.RunMissingDocumentsReminderAsync(CancellationToken)` zwracające `Task<MissingDocumentsReminderResultDto>`; `MissingDocumentsReminderResultDto` z polami `CasesProcessed` (int), `EmailsSentToCatechists` (int), `DirectorsSummarySent` (bool), `FailedSends` (int). Używane przez `ReminderService` (Task 3) i `RemindersController` (Task 5).

- [ ] **Step 1: Utwórz DTO wyniku**

`backend/src/DokPortal.Application/Reminders/MissingDocumentsReminderResultDto.cs`:

```csharp
namespace DokPortal.Application.Reminders;

public record MissingDocumentsReminderResultDto
{
    public required int CasesProcessed { get; init; }
    public required int EmailsSentToCatechists { get; init; }
    public required bool DirectorsSummarySent { get; init; }
    public required int FailedSends { get; init; }
}
```

- [ ] **Step 2: Utwórz interfejs serwisu**

`backend/src/DokPortal.Application/Reminders/IReminderService.cs`:

```csharp
namespace DokPortal.Application.Reminders;

public interface IReminderService
{
    Task<MissingDocumentsReminderResultDto> RunMissingDocumentsReminderAsync(CancellationToken ct);
}
```

- [ ] **Step 3: Zbuduj rozwiązanie**

```bash
cd backend
dotnet build
```

Oczekiwany wynik: 0 błędów (nowe pliki nie mają jeszcze żadnej implementacji, więc nic ich nie używa — kompilacja powinna przejść bez zmian w innych miejscach).

- [ ] **Step 4: Commit**

```bash
git add backend/src/DokPortal.Application/Reminders/
git commit -m "Dodaj IReminderService i MissingDocumentsReminderResultDto"
```

---

## Task 3: `ReminderService` — logika wyboru, grupowania i wysyłki (TDD)

**Files:**
- Create: `backend/tests/DokPortal.Infrastructure.Tests/Services/ReminderServiceTests.cs`
- Create: `backend/src/DokPortal.Infrastructure/Services/ReminderService.cs`

**Interfaces:**
- Consumes: `AppDbContext` (DbSets `CaseDocuments`, `DokCases`, `People`, `Users`, `Roles`, `UserRoles` — z `DokPortal.Infrastructure.Persistence`), `IEmailSender.SendAsync(string toEmail, string subject, string body, CancellationToken ct)` (z `DokPortal.Application.Common`), `AppRoles.DyrektorDOK` (z `DokPortal.Domain.Constants`), `ILogger<ReminderService>`.
- Produces: `ReminderService : IReminderService`, konstruktor `ReminderService(AppDbContext db, IEmailSender emailSender, ILogger<ReminderService> logger)`.

Ten task pisany jest w stylu TDD: najpierw pełny plik testów (wszystkie scenariusze naraz, bo są ze sobą powiązane przez wspólne helpery), potem implementacja, potem zielone testy.

- [ ] **Step 1: Napisz plik testów**

`backend/tests/DokPortal.Infrastructure.Tests/Services/ReminderServiceTests.cs`:

```csharp
using DokPortal.Application.Common;
using DokPortal.Domain.Constants;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Identity;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class ReminderServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    private static Person NewPerson(string? email = null) => new()
    {
        Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski", Email = email,
        CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
    };

    private static DokCase NewDokCase(Guid personId, Guid catechistPersonId) => new()
    {
        Id = Guid.NewGuid(), PersonId = personId, CatechistPersonId = catechistPersonId,
        Path = DokPath.Confirmation, Stage = DokStage.Formation,
        CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
    };

    private static CaseDocument NewMissingDocument(Guid dokCaseId, string name, DateTime? lastReminderSentAtUtc = null) => new()
    {
        Id = Guid.NewGuid(), DokCaseId = dokCaseId, Name = name, IsProvided = false,
        CreatedAtUtc = DateTime.UtcNow, LastReminderSentAtUtc = lastReminderSentAtUtc
    };

    private class RecordingEmailSender : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = new();

        public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct)
        {
            Sent.Add((toEmail, subject, body));
            return Task.CompletedTask;
        }
    }

    private class ThrowingEmailSender : IEmailSender
    {
        public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct) =>
            throw new InvalidOperationException("Wysyłanie e-maili nie jest skonfigurowane.");
    }

    [Fact]
    public async Task RunAsync_SkipsDocumentRemindedLessThanSevenDaysAgo()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = NewPerson();
        var catechist = NewPerson("katechista@example.org");
        db.People.AddRange(person, catechist);
        var dokCase = NewDokCase(person.Id, catechist.Id);
        db.DokCases.Add(dokCase);
        db.CaseDocuments.Add(NewMissingDocument(dokCase.Id, "Metryka chrztu", DateTime.UtcNow.AddDays(-3)));
        await db.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance);

        var result = await service.RunMissingDocumentsReminderAsync(default);

        Assert.Equal(0, result.CasesProcessed);
        Assert.Equal(0, result.EmailsSentToCatechists);
        Assert.Empty(emailSender.Sent);
    }

    [Fact]
    public async Task RunAsync_IncludesDocumentNeverRemindedOrOlderThanSevenDays()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = NewPerson();
        var catechist = NewPerson("katechista@example.org");
        db.People.AddRange(person, catechist);
        var dokCase = NewDokCase(person.Id, catechist.Id);
        db.DokCases.Add(dokCase);
        db.CaseDocuments.AddRange(
            NewMissingDocument(dokCase.Id, "Metryka chrztu", null),
            NewMissingDocument(dokCase.Id, "Zaświadczenie", DateTime.UtcNow.AddDays(-8)));
        await db.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance);

        var result = await service.RunMissingDocumentsReminderAsync(default);

        Assert.Equal(1, result.CasesProcessed);
        Assert.Equal(1, result.EmailsSentToCatechists);
        Assert.Single(emailSender.Sent);
        Assert.Equal("katechista@example.org", emailSender.Sent[0].To);
        Assert.Contains("Metryka chrztu", emailSender.Sent[0].Body);
        Assert.Contains("Zaświadczenie", emailSender.Sent[0].Body);
    }

    [Fact]
    public async Task RunAsync_GroupsMultipleMissingDocumentsInSameCaseIntoOneEmail()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = NewPerson();
        var catechist = NewPerson("katechista@example.org");
        db.People.AddRange(person, catechist);
        var dokCase = NewDokCase(person.Id, catechist.Id);
        db.DokCases.Add(dokCase);
        db.CaseDocuments.AddRange(
            NewMissingDocument(dokCase.Id, "Dokument A"),
            NewMissingDocument(dokCase.Id, "Dokument B"),
            NewMissingDocument(dokCase.Id, "Dokument C"));
        await db.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance);

        var result = await service.RunMissingDocumentsReminderAsync(default);

        Assert.Equal(1, result.CasesProcessed);
        Assert.Equal(1, result.EmailsSentToCatechists);
        Assert.Single(emailSender.Sent);
    }

    [Fact]
    public async Task RunAsync_StampsLastReminderSentAtUtcOnlyAfterSuccessfulCatechistEmail()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = NewPerson();
        var catechist = NewPerson("katechista@example.org");
        db.People.AddRange(person, catechist);
        var dokCase = NewDokCase(person.Id, catechist.Id);
        db.DokCases.Add(dokCase);
        var document = NewMissingDocument(dokCase.Id, "Metryka chrztu");
        db.CaseDocuments.Add(document);
        await db.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance);
        await service.RunMissingDocumentsReminderAsync(default);

        var reloaded = await db.CaseDocuments.AsNoTracking().SingleAsync(d => d.Id == document.Id);
        Assert.NotNull(reloaded.LastReminderSentAtUtc);
        Assert.True(reloaded.LastReminderSentAtUtc > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task RunAsync_DoesNotStampWhenCatechistHasNoEmail()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = NewPerson();
        var catechist = NewPerson(email: null);
        db.People.AddRange(person, catechist);
        var dokCase = NewDokCase(person.Id, catechist.Id);
        db.DokCases.Add(dokCase);
        var document = NewMissingDocument(dokCase.Id, "Metryka chrztu");
        db.CaseDocuments.Add(document);
        await db.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance);
        var result = await service.RunMissingDocumentsReminderAsync(default);

        Assert.Equal(0, result.EmailsSentToCatechists);
        var reloaded = await db.CaseDocuments.AsNoTracking().SingleAsync(d => d.Id == document.Id);
        Assert.Null(reloaded.LastReminderSentAtUtc);
    }

    [Fact]
    public async Task RunAsync_DoesNotStampWhenCatechistEmailSendFails()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = NewPerson();
        var catechist = NewPerson("katechista@example.org");
        db.People.AddRange(person, catechist);
        var dokCase = NewDokCase(person.Id, catechist.Id);
        db.DokCases.Add(dokCase);
        var document = NewMissingDocument(dokCase.Id, "Metryka chrztu");
        db.CaseDocuments.Add(document);
        await db.SaveChangesAsync();

        var service = new ReminderService(db, new ThrowingEmailSender(), NullLogger<ReminderService>.Instance);
        var result = await service.RunMissingDocumentsReminderAsync(default);

        Assert.Equal(0, result.EmailsSentToCatechists);
        Assert.Equal(1, result.FailedSends);
        var reloaded = await db.CaseDocuments.AsNoTracking().SingleAsync(d => d.Id == document.Id);
        Assert.Null(reloaded.LastReminderSentAtUtc);
    }

    [Fact]
    public async Task RunAsync_SendsDigestToDyrektorDokUsersWithEmail()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = NewPerson();
        var catechist = NewPerson("katechista@example.org");
        db.People.AddRange(person, catechist);
        var dokCase = NewDokCase(person.Id, catechist.Id);
        db.DokCases.Add(dokCase);
        db.CaseDocuments.Add(NewMissingDocument(dokCase.Id, "Metryka chrztu"));

        var role = new IdentityRole(AppRoles.DyrektorDOK) { NormalizedName = AppRoles.DyrektorDOK.ToUpperInvariant() };
        db.Roles.Add(role);
        var director = new AppUser { Id = Guid.NewGuid().ToString(), UserName = "dyrektor@example.org", Email = "dyrektor@example.org" };
        db.Users.Add(director);
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = director.Id, RoleId = role.Id });
        await db.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance);
        var result = await service.RunMissingDocumentsReminderAsync(default);

        Assert.True(result.DirectorsSummarySent);
        Assert.Contains(emailSender.Sent, s => s.To == "dyrektor@example.org");
    }

    [Fact]
    public async Task RunAsync_WithNoMissingDocuments_ReturnsZeroedResultAndSendsNoDigest()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var emailSender = new RecordingEmailSender();
        var service = new ReminderService(db, emailSender, NullLogger<ReminderService>.Instance);

        var result = await service.RunMissingDocumentsReminderAsync(default);

        Assert.Equal(0, result.CasesProcessed);
        Assert.False(result.DirectorsSummarySent);
        Assert.Empty(emailSender.Sent);
    }
}
```

- [ ] **Step 2: Uruchom testy i potwierdź, że nie przechodzą kompilacji (bo `ReminderService` jeszcze nie istnieje)**

```bash
cd backend
dotnet test tests/DokPortal.Infrastructure.Tests --filter ReminderServiceTests
```

Oczekiwany wynik: błąd kompilacji „CS0246: Nie można odnaleźć typu lub nazwy przestrzeni nazw „ReminderService"”.

- [ ] **Step 3: Zaimplementuj `ReminderService`**

`backend/src/DokPortal.Infrastructure/Services/ReminderService.cs`:

```csharp
using DokPortal.Application.Common;
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

    public ReminderService(AppDbContext db, IEmailSender emailSender, ILogger<ReminderService> logger)
    {
        _db = db;
        _emailSender = emailSender;
        _logger = logger;
    }

    private sealed record MissingDocumentRow(
        Guid DocumentId,
        string DocumentName,
        Guid DokCaseId,
        string PersonFirstName,
        string PersonLastName,
        string? CatechistEmail);

    public async Task<MissingDocumentsReminderResultDto> RunMissingDocumentsReminderAsync(CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-7);

        var rows = await (
            from doc in _db.CaseDocuments
            join dokCase in _db.DokCases on doc.DokCaseId equals dokCase.Id
            join person in _db.People on dokCase.PersonId equals person.Id
            join catechist in _db.People on dokCase.CatechistPersonId equals catechist.Id
            where !doc.IsProvided && (doc.LastReminderSentAtUtc == null || doc.LastReminderSentAtUtc <= cutoff)
            select new MissingDocumentRow(doc.Id, doc.Name, dokCase.Id, person.FirstName, person.LastName, catechist.Email)
        ).ToListAsync(ct);

        var byCase = rows.GroupBy(r => r.DokCaseId).ToList();

        var emailsSentToCatechists = 0;
        var failedSends = 0;
        var documentIdsToStamp = new List<Guid>();
        var digestLines = new List<string>();

        foreach (var group in byCase)
        {
            var first = group.First();
            var personFullName = $"{first.PersonFirstName} {first.PersonLastName}";
            var documentNames = group.Select(r => r.DocumentName).ToList();
            digestLines.Add($"{personFullName}: {string.Join(", ", documentNames)}");

            if (string.IsNullOrWhiteSpace(first.CatechistEmail))
            {
                continue;
            }

            var body = $"Przypomnienie: w sprawie DOK podopiecznego {personFullName} brakuje następujących dokumentów:\n- {string.Join("\n- ", documentNames)}";
            try
            {
                await _emailSender.SendAsync(first.CatechistEmail!, "Brakujące dokumenty — przypomnienie", body, ct);
                emailsSentToCatechists++;
                documentIdsToStamp.AddRange(group.Select(r => r.DocumentId));
            }
            catch (Exception ex)
            {
                failedSends++;
                _logger.LogWarning(ex, "Nie udało się wysłać przypomnienia o brakujących dokumentach do katechisty dla sprawy {DokCaseId}", first.DokCaseId);
            }
        }

        if (documentIdsToStamp.Count > 0)
        {
            var now = DateTime.UtcNow;
            var documentsToStamp = await _db.CaseDocuments
                .Where(d => documentIdsToStamp.Contains(d.Id))
                .ToListAsync(ct);
            foreach (var document in documentsToStamp)
            {
                document.LastReminderSentAtUtc = now;
            }
            await _db.SaveChangesAsync(ct);
        }

        var directorsSummarySent = false;
        if (digestLines.Count > 0)
        {
            var directorEmails = await (
                from userRole in _db.UserRoles
                join role in _db.Roles on userRole.RoleId equals role.Id
                join user in _db.Users on userRole.UserId equals user.Id
                where role.Name == AppRoles.DyrektorDOK && user.Email != null && user.Email != ""
                select user.Email!
            ).Distinct().ToListAsync(ct);

            if (directorEmails.Count > 0)
            {
                var digestBody = "Podsumowanie brakujących dokumentów w sprawach DOK:\n\n" + string.Join("\n", digestLines);
                foreach (var email in directorEmails)
                {
                    try
                    {
                        await _emailSender.SendAsync(email, "Brakujące dokumenty — podsumowanie tygodniowe", digestBody, ct);
                        directorsSummarySent = true;
                    }
                    catch (Exception ex)
                    {
                        failedSends++;
                        _logger.LogWarning(ex, "Nie udało się wysłać podsumowania brakujących dokumentów do {Email}", email);
                    }
                }
            }
        }

        return new MissingDocumentsReminderResultDto
        {
            CasesProcessed = byCase.Count,
            EmailsSentToCatechists = emailsSentToCatechists,
            DirectorsSummarySent = directorsSummarySent,
            FailedSends = failedSends
        };
    }
}
```

- [ ] **Step 4: Uruchom testy i potwierdź, że przechodzą**

```bash
cd backend
dotnet test tests/DokPortal.Infrastructure.Tests --filter ReminderServiceTests
```

Oczekiwany wynik: 9/9 testów przechodzi.

- [ ] **Step 5: Uruchom pełny zestaw testów backendu**

```bash
cd backend
dotnet build
dotnet test
```

Oczekiwany wynik: 0 błędów kompilacji, 0 ostrzeżeń, wszystkie testy (75 + 9 nowych = 84) przechodzą.

- [ ] **Step 6: Commit**

```bash
git add backend/src/DokPortal.Infrastructure/Services/ReminderService.cs backend/tests/DokPortal.Infrastructure.Tests/Services/ReminderServiceTests.cs
git commit -m "Dodaj ReminderService: wybór, throttling i wysyłka przypomnień o brakujących dokumentach"
```

---

## Task 4: Rejestracja `IReminderService` w DI

**Files:**
- Modify: `backend/src/DokPortal.Api/Program.cs`

**Interfaces:**
- Consumes: `IReminderService`/`ReminderService` (Task 2 i 3).

- [ ] **Step 1: Dodaj using**

W `backend/src/DokPortal.Api/Program.cs`, w bloku `using` na górze pliku, dodaj po `using DokPortal.Application.PastoralNotes;`:

```csharp
using DokPortal.Application.Reminders;
```

- [ ] **Step 2: Zarejestruj serwis**

W tym samym pliku, zaraz po linii `builder.Services.AddScoped<IMailingService, MailingService>();`, dodaj:

```csharp
builder.Services.AddScoped<IReminderService, ReminderService>();
```

- [ ] **Step 3: Zbuduj rozwiązanie**

```bash
cd backend
dotnet build
```

Oczekiwany wynik: 0 błędów.

- [ ] **Step 4: Commit**

```bash
git add backend/src/DokPortal.Api/Program.cs
git commit -m "Zarejestruj IReminderService w kontenerze DI"
```

---

## Task 5: `RemindersController` chroniony kluczem API + testy integracyjne

**Files:**
- Create: `backend/src/DokPortal.Api/Controllers/RemindersController.cs`
- Modify: `backend/src/DokPortal.Api/appsettings.Testing.json`
- Create: `backend/tests/DokPortal.Api.IntegrationTests/RemindersControllerTests.cs`

**Interfaces:**
- Consumes: `IReminderService.RunMissingDocumentsReminderAsync(CancellationToken)` (Task 2/3), `IConfiguration["Reminders:ApiKey"]`.
- Produces: `POST /api/reminders/missing-documents/run` — `401` bez/ze złym nagłówkiem `X-Reminders-Key`, `200` z `MissingDocumentsReminderResultDto` przy poprawnym kluczu.

- [ ] **Step 1: Dodaj testowy klucz do konfiguracji Testing**

`backend/src/DokPortal.Api/appsettings.Testing.json` — dodaj sekcję `Reminders` (plik po zmianie, pełna treść):

```json
{
  "Jwt": {
    "Key": "testing-only-signing-key-1234567890123456",
    "Issuer": "DokPortalLight",
    "Audience": "DokPortalLight",
    "ExpiryMinutes": 480
  },
  "SeedAdmin": {
    "Email": "admin@dokportal.local",
    "Password": "Sekret123!"
  },
  "Reminders": {
    "ApiKey": "testing-only-reminders-key"
  }
}
```

To bezpieczne: `Reminders:ApiKey` to wewnętrzny sekret ustalany przez nas samych (nie dane logowania do zewnętrznego serwisu jak `Smtp`/`BlobStorage`), więc stała testowa wartość w konfiguracji Testing nikogo nie naraża.

- [ ] **Step 2: Napisz testy integracyjne (będą czerwone, dopóki nie powstanie kontroler)**

`backend/tests/DokPortal.Api.IntegrationTests/RemindersControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Reminders;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class RemindersControllerTests : IntegrationTestBase
{
    public RemindersControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Run_WithoutKeyHeader_ReturnsUnauthorized()
    {
        var response = await Client.PostAsync("/api/reminders/missing-documents/run", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Run_WithWrongKey_ReturnsUnauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/reminders/missing-documents/run");
        request.Headers.Add("X-Reminders-Key", "zly-klucz");

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Run_WithCorrectKey_ReturnsOkWithResult()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/reminders/missing-documents/run");
        request.Headers.Add("X-Reminders-Key", "testing-only-reminders-key");

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<MissingDocumentsReminderResultDto>();
        Assert.NotNull(result);
    }
}
```

- [ ] **Step 3: Uruchom testy integracyjne i potwierdź, że są czerwone**

```bash
cd backend
dotnet test tests/DokPortal.Api.IntegrationTests --filter RemindersControllerTests
```

Oczekiwany wynik: kompilacja się powiedzie (`MissingDocumentsReminderResultDto` już istnieje z Task 2), ale wszystkie 3 testy nie przejdą w czasie wykonania — trasa `/api/reminders/missing-documents/run` jeszcze nie istnieje (kontroler nie istnieje), więc serwer zwraca `404 Not Found` zamiast oczekiwanego `401`/`200`. Potwierdź czerwony wynik przed przejściem dalej.

- [ ] **Step 4: Zaimplementuj kontroler**

`backend/src/DokPortal.Api/Controllers/RemindersController.cs`:

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
        var configuredKey = _configuration["Reminders:ApiKey"];
        if (string.IsNullOrEmpty(configuredKey) || !HasValidKey(Request.Headers["X-Reminders-Key"], configuredKey))
        {
            return Unauthorized();
        }

        var result = await _reminderService.RunMissingDocumentsReminderAsync(ct);
        return Ok(result);
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

- [ ] **Step 5: Uruchom testy integracyjne i potwierdź, że przechodzą**

```bash
cd backend
dotnet test tests/DokPortal.Api.IntegrationTests --filter RemindersControllerTests
```

Oczekiwany wynik: 3/3 testów przechodzi.

- [ ] **Step 6: Uruchom pełny zestaw testów backendu**

```bash
cd backend
dotnet build
dotnet test
```

Oczekiwany wynik: 0 błędów, 0 ostrzeżeń, wszystkie testy (84 + 3 nowe = 87) przechodzą.

- [ ] **Step 7: Commit**

```bash
git add backend/src/DokPortal.Api/Controllers/RemindersController.cs backend/src/DokPortal.Api/appsettings.Testing.json backend/tests/DokPortal.Api.IntegrationTests/RemindersControllerTests.cs
git commit -m "Dodaj RemindersController chroniony kluczem API"
```

---

## Task 6: Cykliczny GitHub Actions workflow

**Files:**
- Create: `.github/workflows/missing-documents-reminder.yml`

**Interfaces:**
- Consumes: `POST /api/reminders/missing-documents/run` na produkcji (`https://dokportal-api.azurewebsites.net`), nagłówek `X-Reminders-Key` z `secrets.REMINDERS_API_KEY`.

- [ ] **Step 1: Utwórz plik workflow**

`.github/workflows/missing-documents-reminder.yml`:

```yaml
name: Missing Documents Reminder

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
            https://dokportal-api.azurewebsites.net/api/reminders/missing-documents/run)
          http_code=$(echo "$response" | tail -n1)
          body=$(echo "$response" | sed '$d')
          echo "Odpowiedź: $body"
          if [ "$http_code" -ne 200 ]; then
            echo "Endpoint zwrócił kod $http_code"
            exit 1
          fi
```

Uwaga: `cron: "0 7 * * 1"` oznacza poniedziałek 07:00 UTC (czyli 08:00 lub 09:00 czasu polskiego, zależnie od czasu letniego) — raz w tygodniu, zgodnie ze specyfikacją. `workflow_dispatch` pozwala uruchomić ręcznie z zakładki Actions na GitHubie, przydatne do testów.

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/missing-documents-reminder.yml
git commit -m "Dodaj cykliczny workflow przypomnień o brakujących dokumentach"
```

Ten plik sam w sobie nic nie zrobi, dopóki `REMINDERS_API_KEY` nie zostanie ustawiony jako GitHub Actions secret, a `Reminders:ApiKey` jako Azure App Service setting na `dokportal-api` — to ręczny krok użytkownika opisany w Task 8.

---

## Task 7: Changelog na Dashboardzie

**Files:**
- Modify: `frontend/src/app/features/dashboard/dashboard.component.ts`

**Interfaces:**
- Brak — czysto tekstowa zmiana w tablicy `changelog`.

- [ ] **Step 1: Dodaj wpis na górze tablicy `changelog`**

W `frontend/src/app/features/dashboard/dashboard.component.ts`, w tablicy `changelog`, dodaj nowy element jako pierwszy (przed dotychczasowym pierwszym wpisem o Mailingu):

```typescript
{ date: '2026-10-01', text: 'Automatyczne przypomnienia o brakujących dokumentach — raz w tygodniu katechista i Dyrektor DOK dostają e-mail o niedostarczonych dokumentach w swoich sprawach DOK.' },
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
git commit -m "Dashboard: wpis o przypomnieniach o brakujących dokumentach"
```

---

## Task 8: Weryfikacja end-to-end, push i konfiguracja produkcyjna

**Files:** brak nowych/zmienianych — tylko weryfikacja i instrukcje dla użytkownika.

**Interfaces:** brak.

- [ ] **Step 1: Uruchom lokalny backend i zweryfikuj endpoint ręcznie**

```bash
cd backend/src/DokPortal.Api
dotnet run --urls http://localhost:5227
```

W drugim terminalu (backend bez `Reminders:ApiKey` w `appsettings.Development.json` — oczekiwany 401):

```bash
curl -s -o /dev/null -w "%{http_code}\n" -X POST http://localhost:5227/api/reminders/missing-documents/run
```

Oczekiwany wynik: `401`.

- [ ] **Step 2: Przetestuj ścieżkę sukcesu lokalnie z tymczasowym kluczem**

Dodaj tymczasowo do `backend/src/DokPortal.Api/appsettings.Development.json` sekcję:

```json
"Reminders": { "ApiKey": "lokalny-klucz-testowy" }
```

Zrestartuj backend, potem:

```bash
curl -s -X POST -H "X-Reminders-Key: lokalny-klucz-testowy" http://localhost:5227/api/reminders/missing-documents/run
```

Oczekiwany wynik: `200` z JSON-em `{"casesProcessed":0,"emailsSentToCatechists":0,"directorsSummarySent":false,"failedSends":0}` (zakładając pustą lokalną bazę braków dokumentów — jeśli są jakieś testowe dane z wcześniejszych sesji, liczby będą inne, co też jest poprawnym wynikiem).

Po teście **usuń** sekcję `Reminders` z `appsettings.Development.json` z powrotem (ten plik i tak nigdy nie jest commitowany, ale zostawienie w nim przypadkowego sekretu jest złą praktyką).

- [ ] **Step 3: Push na master**

```bash
cd /c/eu02_install/DOKPortalLight
git push origin master
```

Poczekaj na zielone CI (Backend CI, Deploy, Frontend CI, Azure Static Web Apps CI/CD) i sprawdź `/health` na produkcji po wdrożeniu migracji z Task 1 — tak samo jak przy poprzednich migracjach w tej sesji.

- [ ] **Step 4: Przekaż użytkownikowi dwa ręczne kroki konfiguracyjne**

Te dwa kroki musi wykonać użytkownik (Szymon), nie Claude — to ustalanie nowych sekretów w systemach, do których dostęp powinien nadawać właściciel:

1. W Azure Portal → App Service `dokportal-api` → Configuration → Application settings: dodać `Reminders:ApiKey` z nową, losową wartością (np. wygenerowaną przez `openssl rand -hex 32`).
2. W repo GitHub → Settings → Secrets and variables → Actions: dodać repository secret `REMINDERS_API_KEY` z **dokładnie tą samą** wartością co w kroku 1.

Dopóki te dwa kroki nie zostaną wykonane, cykliczny workflow będzie kończyć się błędem (serwer zwróci `401`, bo `Reminders:ApiKey` nie będzie ustawiony w Azure) — to oczekiwane i bezpieczne zachowanie, analogiczne do stanu Mailingu przed podaniem danych SMTP.

---

## Self-Review Notes

- **Pokrycie spec:** architektura (Task 5+6), model danych (Task 1), logika doboru/throttlingu/grupowania (Task 3), obsługa błędów per-odbiorca (Task 3), bezpieczeństwo endpointu (Task 5), testy jednostkowe i integracyjne (Task 3, 5), changelog (Task 7), konfiguracja produkcyjna (Task 8) — wszystkie sekcje spec mają odpowiadające zadanie.
- **Spójność typów:** `MissingDocumentsReminderResultDto` ma identyczny kształt w Task 2 (definicja), Task 3 (konstrukcja w `ReminderService`) i Task 5 (deserializacja w teście integracyjnym). `IReminderService.RunMissingDocumentsReminderAsync(CancellationToken ct)` ma tę samą sygnaturę wszędzie.
- **Poza zakresem (zgodnie ze spec):** przypomnienia o spotkaniach i imieninach, ręczny przycisk w UI, szablony HTML e-maili — świadomie nieuwzględnione w tym planie.
