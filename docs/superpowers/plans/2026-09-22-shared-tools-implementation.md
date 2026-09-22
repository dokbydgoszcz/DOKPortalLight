# DOK Portal Light — Faza 4: Narzędzia wspólne — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the four shared tools that close out the prototype's scope: real PDF generation for six document templates, mailing campaign records (no real send), a nameday calendar, and an audit log that materializes Faza 3's silent RODO filter on pastoral notes plus user role changes as visible log entries.

**Architecture:** Same Domain→Application→Infrastructure→Api layering as Faza 1-3. Three new entities (`GeneratedDocument`, `MailingCampaign`, `AuditLogEntry`) FK/relate to existing `Person`/`AspNetUsers`; two new nullable fields land on the existing `Person` entity. PDF rendering is isolated inside `DocumentService` behind `IDocumentService` so QuestPDF never leaks past the Infrastructure layer. The audit log has no middleware — it's called explicitly from the two controllers the spec names.

**Tech Stack:** Same as Phases 1-3 — .NET 8, EF Core 8, ASP.NET Core Identity/JWT, xUnit; Angular (standalone, signals), Vitest. New dependency: QuestPDF (Community license) in `DokPortal.Infrastructure`.

**Spec:** [docs/superpowers/specs/2026-09-22-shared-tools-design.md](../specs/2026-09-22-shared-tools-design.md)

## Global Constraints

- All enum-typed DTO properties serialize as strings automatically via the global `JsonStringEnumConverter` from Faza 2 Task 6 — no manual `.ToString()`, and integration tests deserializing them must pass `EnumJsonOptions` (from `IntegrationTestBase`) to `ReadFromJsonAsync`.
- QuestPDF requires `QuestPDF.Settings.License = LicenseType.Community;` to be set before any PDF is generated, in every process that calls it (production and tests) — set it once in `DocumentService`'s static constructor, not in `Program.cs`, so every caller gets it for free.
- All six document templates share one code path: person fields (name, birth date, parish) + `AdditionalNotes` free text. No per-template data sources (missions, cases, candidates) are wired in — this is the explicit YAGNI simplification from the spec.
- The mailing recipient groups are exactly `CandidatesSksp`, `Missionaries`, `DokGraduates`, `DokCases` — there is no `Proboszczowie` group (no parish-priest entity exists).
- The audit log is written from exactly two call sites — `PastoralNotesController.GetAll` and `UsersController.AssignRoles` — never from a generic middleware, and `PastoralNotesController`'s existing RODO filter logic (`GetVisibleForCaseAsync`) is unchanged; only a new read-only check (`HasNotesFromOthersAsync`) is added alongside it.
- Every task ends with a green `dotnet test backend/DokPortal.sln` (backend) or `npx ng test` (frontend), and every frontend task also ends with a successful `npm run build`.

---

## Task 1: Domain additions, DbContext wiring, migration

**Files:**
- Modify: `backend/src/DokPortal.Domain/Entities/Person.cs`
- Create: `backend/src/DokPortal.Domain/Enums/DocumentTemplate.cs`
- Create: `backend/src/DokPortal.Domain/Enums/MailingGroup.cs`
- Create: `backend/src/DokPortal.Domain/Enums/CampaignStatus.cs`
- Create: `backend/src/DokPortal.Domain/Enums/AuditResult.cs`
- Create: `backend/src/DokPortal.Domain/Entities/GeneratedDocument.cs`
- Create: `backend/src/DokPortal.Domain/Entities/MailingCampaign.cs`
- Create: `backend/src/DokPortal.Domain/Entities/AuditLogEntry.cs`
- Modify: `backend/src/DokPortal.Infrastructure/Persistence/AppDbContext.cs`
- Test: `backend/tests/DokPortal.Api.IntegrationTests/SharedToolsEntitiesPersistenceTests.cs`

**Interfaces:**
- Consumes: `Person` (Phase 1), `CustomWebApplicationFactory` (Phase 1).
- Produces: `Person.NameDayMonth`/`NameDayDay` (`int?`); `GeneratedDocument`, `MailingCampaign`, `AuditLogEntry` entities; `DocumentTemplate`, `MailingGroup`, `CampaignStatus`, `AuditResult` enums; `AppDbContext.GeneratedDocuments/MailingCampaigns/AuditLogEntries` — consumed by every later task.

- [ ] **Step 1: Write the failing test**

`backend/tests/DokPortal.Api.IntegrationTests/SharedToolsEntitiesPersistenceTests.cs`:

```csharp
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class SharedToolsEntitiesPersistenceTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SharedToolsEntitiesPersistenceTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task SavedSharedToolsEntities_CanBeReadBackInANewScope()
    {
        Guid personId, documentId, campaignId, logId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var person = new Person
            {
                Id = Guid.NewGuid(), FirstName = "Ewa", LastName = "Nowak", NameDayMonth = 12, NameDayDay = 24,
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            };
            db.People.Add(person);

            var document = new GeneratedDocument
            {
                Id = Guid.NewGuid(), Template = DocumentTemplate.LetterToBishop, PersonId = person.Id,
                GeneratedByUserId = "user-1", AdditionalNotes = "Test", CreatedAtUtc = DateTime.UtcNow
            };
            var campaign = new MailingCampaign
            {
                Id = Guid.NewGuid(), Subject = "Zaproszenie", Body = "Treść", Group = MailingGroup.CandidatesSksp,
                RecipientCount = 5, Status = CampaignStatus.Draft, CreatedAtUtc = DateTime.UtcNow
            };
            var logEntry = new AuditLogEntry
            {
                Id = Guid.NewGuid(), TimestampUtc = DateTime.UtcNow, UserId = "user-1", UserEmail = "user@example.org",
                Action = "ReadPastoralNotes", ObjectDescription = "Ewa Nowak", Result = AuditResult.Blocked
            };

            db.GeneratedDocuments.Add(document);
            db.MailingCampaigns.Add(campaign);
            db.AuditLogEntries.Add(logEntry);
            await db.SaveChangesAsync();

            personId = person.Id;
            documentId = document.Id;
            campaignId = campaign.Id;
            logId = logEntry.Id;
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var person = await db.People.FindAsync(personId);
            Assert.NotNull(person);
            Assert.Equal(12, person!.NameDayMonth);
            Assert.Equal(24, person.NameDayDay);
            Assert.NotNull(await db.GeneratedDocuments.FindAsync(documentId));
            Assert.NotNull(await db.MailingCampaigns.FindAsync(campaignId));
            Assert.NotNull(await db.AuditLogEntries.FindAsync(logId));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/DokPortal.Api.IntegrationTests`
Expected: FAIL to compile — none of the new types exist yet.

- [ ] **Step 3: Add the nameday fields to Person**

In `backend/src/DokPortal.Domain/Entities/Person.cs`, add after the `Notes` property:

```csharp
    public int? NameDayMonth { get; set; }
    public int? NameDayDay { get; set; }
```

- [ ] **Step 4: Implement the enums**

`backend/src/DokPortal.Domain/Enums/DocumentTemplate.cs`:

```csharp
namespace DokPortal.Domain.Enums;

public enum DocumentTemplate
{
    LetterToBishop,
    ConversionConsent,
    CanonicalMissionDecree,
    DokReferral,
    SkspCompletionCertificate,
    SacramentCertificate
}
```

`backend/src/DokPortal.Domain/Enums/MailingGroup.cs`:

```csharp
namespace DokPortal.Domain.Enums;

public enum MailingGroup
{
    CandidatesSksp,
    Missionaries,
    DokGraduates,
    DokCases
}
```

`backend/src/DokPortal.Domain/Enums/CampaignStatus.cs`:

```csharp
namespace DokPortal.Domain.Enums;

public enum CampaignStatus
{
    Draft,
    Sent
}
```

`backend/src/DokPortal.Domain/Enums/AuditResult.cs`:

```csharp
namespace DokPortal.Domain.Enums;

public enum AuditResult
{
    Allowed,
    Blocked
}
```

- [ ] **Step 5: Implement the entities**

`backend/src/DokPortal.Domain/Entities/GeneratedDocument.cs`:

```csharp
using DokPortal.Domain.Enums;

namespace DokPortal.Domain.Entities;

public class GeneratedDocument
{
    public Guid Id { get; set; }
    public DocumentTemplate Template { get; set; }
    public Guid PersonId { get; set; }
    public Person? Person { get; set; }
    public required string GeneratedByUserId { get; set; }
    public string? AdditionalNotes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
```

`backend/src/DokPortal.Domain/Entities/MailingCampaign.cs`:

```csharp
using DokPortal.Domain.Enums;

namespace DokPortal.Domain.Entities;

public class MailingCampaign
{
    public Guid Id { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }
    public MailingGroup Group { get; set; }
    public int RecipientCount { get; set; }
    public CampaignStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
}
```

`backend/src/DokPortal.Domain/Entities/AuditLogEntry.cs`:

```csharp
using DokPortal.Domain.Enums;

namespace DokPortal.Domain.Entities;

public class AuditLogEntry
{
    public Guid Id { get; set; }
    public DateTime TimestampUtc { get; set; }
    public required string UserId { get; set; }
    public required string UserEmail { get; set; }
    public required string Action { get; set; }
    public required string ObjectDescription { get; set; }
    public AuditResult Result { get; set; }
}
```

- [ ] **Step 6: Wire the entities into AppDbContext**

In `backend/src/DokPortal.Infrastructure/Persistence/AppDbContext.cs`, add after the existing DOK `DbSet` lines:

```csharp
    public DbSet<GeneratedDocument> GeneratedDocuments => Set<GeneratedDocument>();
    public DbSet<MailingCampaign> MailingCampaigns => Set<MailingCampaign>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
```

Add to `OnModelCreating`, after the existing DOK entity configuration blocks:

```csharp
        builder.Entity<GeneratedDocument>(entity =>
        {
            entity.HasOne(d => d.Person).WithMany().HasForeignKey(d => d.PersonId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MailingCampaign>(entity =>
        {
            entity.Property(m => m.Subject).IsRequired().HasMaxLength(200);
            entity.Property(m => m.Body).IsRequired();
        });

        builder.Entity<AuditLogEntry>(entity =>
        {
            entity.Property(a => a.UserEmail).IsRequired().HasMaxLength(256);
            entity.Property(a => a.Action).IsRequired().HasMaxLength(100);
            entity.Property(a => a.ObjectDescription).IsRequired().HasMaxLength(300);
        });
```

- [ ] **Step 7: Generate the EF Core migration**

```bash
cd backend
dotnet ef migrations add AddSharedTools --project src/DokPortal.Infrastructure --startup-project src/DokPortal.Api
```

- [ ] **Step 8: Run test to verify it passes**

Run: `dotnet test backend/tests/DokPortal.Api.IntegrationTests`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add backend
git commit -m "Add shared-tools domain entities, enums, and EF Core migration"
git push origin master
```

---

## Task 2: NameDays module (backend)

**Files:**
- Create: `backend/src/DokPortal.Application/NameDays/UpcomingNameDayDto.cs`
- Create: `backend/src/DokPortal.Application/NameDays/INameDayService.cs`
- Create: `backend/src/DokPortal.Infrastructure/Services/NameDayService.cs`
- Create: `backend/src/DokPortal.Api/Controllers/NameDaysController.cs`
- Modify: `backend/src/DokPortal.Api/Program.cs`
- Test: `backend/tests/DokPortal.Infrastructure.Tests/Services/NameDayServiceTests.cs`
- Test: `backend/tests/DokPortal.Api.IntegrationTests/NameDaysControllerTests.cs`

**Interfaces:**
- Consumes: `Person.NameDayMonth`/`NameDayDay` (Task 1).
- Produces: `INameDayService.GetUpcomingAsync(int days, CancellationToken ct)`; `GET /api/name-days/upcoming?days=` — no later task depends on this.

- [ ] **Step 1: Write the failing unit test**

`backend/tests/DokPortal.Infrastructure.Tests/Services/NameDayServiceTests.cs`:

```csharp
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class NameDayServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task GetUpcomingAsync_ExcludesPeopleWithoutNameDayFields()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var withNameDay = new Person
        {
            Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski",
            NameDayMonth = today.Month, NameDayDay = today.Day,
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        var withoutNameDay = new Person
        {
            Id = Guid.NewGuid(), FirstName = "Anna", LastName = "Maj",
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.AddRange(withNameDay, withoutNameDay);
        await db.SaveChangesAsync();

        var service = new NameDayService(db);
        var result = await service.GetUpcomingAsync(30, default);

        Assert.Single(result);
        Assert.Equal("Jan Kowalski", result[0].FullName);
        Assert.Equal(0, result[0].DaysUntil);
    }

    [Fact]
    public async Task GetUpcomingAsync_WrapsAroundYearEnd_AndSortsByDaysUntil()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var inTwoDays = today.AddDays(2);
        var farAway = today.AddDays(200);

        db.People.AddRange(
            new Person
            {
                Id = Guid.NewGuid(), FirstName = "Piotr", LastName = "Zima",
                NameDayMonth = inTwoDays.Month, NameDayDay = inTwoDays.Day,
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            },
            new Person
            {
                Id = Guid.NewGuid(), FirstName = "Karol", LastName = "Daleki",
                NameDayMonth = farAway.Month, NameDayDay = farAway.Day,
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var service = new NameDayService(db);
        var result = await service.GetUpcomingAsync(30, default);

        Assert.Single(result);
        Assert.Equal("Piotr Zima", result[0].FullName);
        Assert.Equal(2, result[0].DaysUntil);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/DokPortal.Infrastructure.Tests`
Expected: FAIL to compile — `NameDayService` does not exist yet.

- [ ] **Step 3: Implement the DTO and interface**

`backend/src/DokPortal.Application/NameDays/UpcomingNameDayDto.cs`:

```csharp
namespace DokPortal.Application.NameDays;

public class UpcomingNameDayDto
{
    public required Guid PersonId { get; init; }
    public required string FullName { get; init; }
    public required int NameDayMonth { get; init; }
    public required int NameDayDay { get; init; }
    public required int DaysUntil { get; init; }
}
```

`backend/src/DokPortal.Application/NameDays/INameDayService.cs`:

```csharp
namespace DokPortal.Application.NameDays;

public interface INameDayService
{
    Task<IReadOnlyList<UpcomingNameDayDto>> GetUpcomingAsync(int days, CancellationToken ct);
}
```

- [ ] **Step 4: Implement the service**

`backend/src/DokPortal.Infrastructure/Services/NameDayService.cs`:

```csharp
using DokPortal.Application.NameDays;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class NameDayService : INameDayService
{
    private readonly AppDbContext _db;

    public NameDayService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<UpcomingNameDayDto>> GetUpcomingAsync(int days, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var people = await _db.People.AsNoTracking()
            .Where(p => p.NameDayMonth != null && p.NameDayDay != null)
            .ToListAsync(ct);

        return people
            .Select(p =>
            {
                var next = NextOccurrence(p.NameDayMonth!.Value, p.NameDayDay!.Value, today);
                return new UpcomingNameDayDto
                {
                    PersonId = p.Id,
                    FullName = p.FullName,
                    NameDayMonth = p.NameDayMonth!.Value,
                    NameDayDay = p.NameDayDay!.Value,
                    DaysUntil = next.DayNumber - today.DayNumber
                };
            })
            .Where(d => d.DaysUntil <= days)
            .OrderBy(d => d.DaysUntil)
            .ThenBy(d => d.FullName)
            .ToList();
    }

    private static DateOnly NextOccurrence(int month, int day, DateOnly today)
    {
        var candidate = SafeDate(today.Year, month, day);
        return candidate < today ? SafeDate(today.Year + 1, month, day) : candidate;
    }

    private static DateOnly SafeDate(int year, int month, int day)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        return new DateOnly(year, month, Math.Min(day, daysInMonth));
    }
}
```

- [ ] **Step 5: Run unit test to verify it passes**

Run: `dotnet test backend/tests/DokPortal.Infrastructure.Tests`
Expected: PASS.

- [ ] **Step 6: Write the failing integration test**

`backend/tests/DokPortal.Api.IntegrationTests/NameDaysControllerTests.cs`:

```csharp
using System.Net.Http.Json;
using DokPortal.Application.NameDays;
using DokPortal.Application.People;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class NameDaysControllerTests : IntegrationTestBase
{
    public NameDaysControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetUpcoming_ReturnsPeopleWithNameDayFieldsSet()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // this person has no nameday fields set and must be excluded from the result
        await admin.PostAsJsonAsync("/api/people", new { FirstName = "Jan", LastName = "Kowalski" });
        await admin.PostAsJsonAsync("/api/people", new
        {
            FirstName = "Ewa", LastName = "Nowak", NameDayMonth = today.Month, NameDayDay = today.Day
        });

        var response = await admin.GetAsync("/api/name-days/upcoming?days=30");
        var upcoming = await response.Content.ReadFromJsonAsync<List<UpcomingNameDayDto>>();

        Assert.Single(upcoming!);
        Assert.Equal("Ewa Nowak", upcoming![0].FullName);
    }
}
```

Note: `CreatePersonRequest`/`UpdatePersonRequest` need `NameDayMonth`/`NameDayDay` for this test's second POST to actually set the fields — add them now since `PersonService` already maps every other field 1:1.

- [ ] **Step 7: Add NameDay fields to the People request/response DTOs and service**

In `backend/src/DokPortal.Application/People/PersonDto.cs`, add after `Notes`:

```csharp
    public int? NameDayMonth { get; init; }
    public int? NameDayDay { get; init; }
```

In `backend/src/DokPortal.Application/People/CreatePersonRequest.cs`, add after `Notes`:

```csharp
    public int? NameDayMonth { get; init; }
    public int? NameDayDay { get; init; }
```

(`UpdatePersonRequest` is an empty subclass of `CreatePersonRequest` — it inherits the new properties automatically, no separate edit needed.)

In `backend/src/DokPortal.Infrastructure/Services/PersonService.cs`, set the two fields in `CreateAsync` and `UpdateAsync` alongside the existing `Notes` assignment (`person.NameDayMonth = request.NameDayMonth; person.NameDayDay = request.NameDayDay;`), and add them to `ToDto` alongside `Notes` (`NameDayMonth = p.NameDayMonth, NameDayDay = p.NameDayDay`).

- [ ] **Step 8: Run integration test to verify it fails, then implement the controller**

Run: `dotnet test backend/tests/DokPortal.Api.IntegrationTests`
Expected: FAIL — 404, `NameDaysController` does not exist yet.

`backend/src/DokPortal.Api/Controllers/NameDaysController.cs`:

```csharp
using DokPortal.Application.NameDays;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/name-days")]
[Authorize]
public class NameDaysController : ControllerBase
{
    private readonly INameDayService _nameDayService;

    public NameDaysController(INameDayService nameDayService) => _nameDayService = nameDayService;

    [HttpGet("upcoming")]
    public async Task<ActionResult<IReadOnlyList<UpcomingNameDayDto>>> GetUpcoming([FromQuery] int days, CancellationToken ct)
    {
        var window = days <= 0 ? 30 : days;
        return Ok(await _nameDayService.GetUpcomingAsync(window, ct));
    }
}
```

In `backend/src/DokPortal.Api/Program.cs`, add `using DokPortal.Application.NameDays;` near the other `using DokPortal.Application.*` lines, and `builder.Services.AddScoped<INameDayService, NameDayService>();` after the `ISupervisionService` registration.

- [ ] **Step 9: Run tests to verify they pass**

Run: `dotnet test backend/DokPortal.sln`
Expected: PASS (all suites, including the People tests already covering `Notes`).

- [ ] **Step 10: Commit**

```bash
git add backend
git commit -m "Add NameDays module: Person nameday fields and upcoming-namedays endpoint"
git push origin master
```

---

## Task 3: Document generator (QuestPDF)

**Files:**
- Modify: `backend/src/DokPortal.Infrastructure/DokPortal.Infrastructure.csproj`
- Create: `backend/src/DokPortal.Application/Documents/GeneratedDocumentDto.cs`
- Create: `backend/src/DokPortal.Application/Documents/GenerateDocumentRequest.cs`
- Create: `backend/src/DokPortal.Application/Documents/GeneratedDocumentResult.cs`
- Create: `backend/src/DokPortal.Application/Documents/IDocumentService.cs`
- Create: `backend/src/DokPortal.Infrastructure/Services/DocumentService.cs`
- Create: `backend/src/DokPortal.Api/Controllers/DocumentsController.cs`
- Modify: `backend/src/DokPortal.Api/Program.cs`
- Test: `backend/tests/DokPortal.Infrastructure.Tests/Services/DocumentServiceTests.cs`
- Test: `backend/tests/DokPortal.Api.IntegrationTests/DocumentsControllerTests.cs`

**Interfaces:**
- Consumes: `Person` (Phase 1), `DocumentTemplate` enum (Task 1).
- Produces: `IDocumentService.GenerateAsync(GenerateDocumentRequest, string generatedByUserId, CancellationToken)` returning `GeneratedDocumentResult?`; `IDocumentService.GetHistoryAsync(CancellationToken)`; `GET/POST /api/documents` — no later task depends on this.

- [ ] **Step 1: Add the QuestPDF package**

In `backend/src/DokPortal.Infrastructure/DokPortal.Infrastructure.csproj`, add to the existing `<ItemGroup>` of `PackageReference`s:

```xml
    <PackageReference Include="QuestPDF" Version="2024.*" />
```

Run: `dotnet restore backend/DokPortal.sln`
Expected: package restores successfully.

- [ ] **Step 2: Write the failing unit test**

`backend/tests/DokPortal.Infrastructure.Tests/Services/DocumentServiceTests.cs`:

```csharp
using DokPortal.Application.Documents;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class DocumentServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task GenerateAsync_WhenPersonExists_ReturnsNonEmptyPdfAndSavesHistory()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = new Person
        {
            Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski",
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.Add(person);
        await db.SaveChangesAsync();

        var service = new DocumentService(db);
        var result = await service.GenerateAsync(
            new GenerateDocumentRequest { Template = DocumentTemplate.LetterToBishop, PersonId = person.Id, AdditionalNotes = "Test" },
            "user-1", default);

        Assert.NotNull(result);
        Assert.NotEmpty(result!.PdfBytes);
        Assert.Equal(1, await db.GeneratedDocuments.CountAsync());
        Assert.Equal("Jan Kowalski", result.History.PersonFullName);
    }

    [Fact]
    public async Task GenerateAsync_WhenPersonMissing_ReturnsNull()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var service = new DocumentService(db);

        var result = await service.GenerateAsync(
            new GenerateDocumentRequest { Template = DocumentTemplate.LetterToBishop, PersonId = Guid.NewGuid() },
            "user-1", default);

        Assert.Null(result);
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test backend/tests/DokPortal.Infrastructure.Tests`
Expected: FAIL to compile — none of the new Application types or `DocumentService` exist yet.

- [ ] **Step 4: Implement the DTOs and interface**

`backend/src/DokPortal.Application/Documents/GeneratedDocumentDto.cs`:

```csharp
using DokPortal.Domain.Enums;

namespace DokPortal.Application.Documents;

public class GeneratedDocumentDto
{
    public required Guid Id { get; init; }
    public DocumentTemplate Template { get; init; }
    public required Guid PersonId { get; init; }
    public required string PersonFullName { get; init; }
    public required string GeneratedByUserId { get; init; }
    public string? AdditionalNotes { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}
```

`backend/src/DokPortal.Application/Documents/GenerateDocumentRequest.cs`:

```csharp
using DokPortal.Domain.Enums;

namespace DokPortal.Application.Documents;

public class GenerateDocumentRequest
{
    public DocumentTemplate Template { get; init; }
    public Guid PersonId { get; init; }
    public string? AdditionalNotes { get; init; }
}
```

`backend/src/DokPortal.Application/Documents/GeneratedDocumentResult.cs`:

```csharp
namespace DokPortal.Application.Documents;

public class GeneratedDocumentResult
{
    public required byte[] PdfBytes { get; init; }
    public required GeneratedDocumentDto History { get; init; }
}
```

`backend/src/DokPortal.Application/Documents/IDocumentService.cs`:

```csharp
namespace DokPortal.Application.Documents;

public interface IDocumentService
{
    Task<GeneratedDocumentResult?> GenerateAsync(GenerateDocumentRequest request, string generatedByUserId, CancellationToken ct);
    Task<IReadOnlyList<GeneratedDocumentDto>> GetHistoryAsync(CancellationToken ct);
}
```

- [ ] **Step 5: Implement the service**

`backend/src/DokPortal.Infrastructure/Services/DocumentService.cs`:

```csharp
using DokPortal.Application.Documents;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace DokPortal.Infrastructure.Services;

public class DocumentService : IDocumentService
{
    private static readonly IReadOnlyDictionary<DocumentTemplate, string> TemplateTitles = new Dictionary<DocumentTemplate, string>
    {
        [DocumentTemplate.LetterToBishop] = "Pismo do Biskupa",
        [DocumentTemplate.ConversionConsent] = "Zgoda na konwersję",
        [DocumentTemplate.CanonicalMissionDecree] = "Dekret misji kanonicznej",
        [DocumentTemplate.DokReferral] = "Skierowanie do DOK",
        [DocumentTemplate.SkspCompletionCertificate] = "Zaświadczenie ukończenia SKŚP",
        [DocumentTemplate.SacramentCertificate] = "Zaświadczenie o sakramencie"
    };

    private readonly AppDbContext _db;

    static DocumentService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public DocumentService(AppDbContext db) => _db = db;

    public async Task<GeneratedDocumentResult?> GenerateAsync(GenerateDocumentRequest request, string generatedByUserId, CancellationToken ct)
    {
        var person = await _db.People.Include(p => p.Parish).AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PersonId, ct);
        if (person is null) return null;

        var pdfBytes = RenderPdf(TemplateTitles[request.Template], person, request.AdditionalNotes);

        var history = new GeneratedDocument
        {
            Id = Guid.NewGuid(),
            Template = request.Template,
            PersonId = person.Id,
            GeneratedByUserId = generatedByUserId,
            AdditionalNotes = request.AdditionalNotes,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.GeneratedDocuments.Add(history);
        await _db.SaveChangesAsync(ct);

        return new GeneratedDocumentResult { PdfBytes = pdfBytes, History = ToDto(history, person.FullName) };
    }

    public async Task<IReadOnlyList<GeneratedDocumentDto>> GetHistoryAsync(CancellationToken ct)
    {
        var docs = await _db.GeneratedDocuments.Include(d => d.Person).AsNoTracking()
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToListAsync(ct);
        return docs.Select(d => ToDto(d, d.Person!.FullName)).ToList();
    }

    private static byte[] RenderPdf(string title, Person person, string? additionalNotes)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Header().Text(title).FontSize(20).Bold();
                page.Content().Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Text($"Data: {DateTime.UtcNow:yyyy-MM-dd}");
                    column.Item().Text($"Imię i nazwisko: {person.FullName}");
                    column.Item().Text($"Data urodzenia: {(person.BirthDate.HasValue ? person.BirthDate.Value.ToString("yyyy-MM-dd") : "brak danych")}");
                    column.Item().Text($"Parafia: {person.Parish?.Name ?? "brak danych"}");
                    if (!string.IsNullOrWhiteSpace(additionalNotes))
                    {
                        column.Item().Text($"Uwagi dodatkowe: {additionalNotes}");
                    }
                });
            });
        });
        return document.GeneratePdf();
    }

    private static GeneratedDocumentDto ToDto(GeneratedDocument d, string personFullName) => new()
    {
        Id = d.Id,
        Template = d.Template,
        PersonId = d.PersonId,
        PersonFullName = personFullName,
        GeneratedByUserId = d.GeneratedByUserId,
        AdditionalNotes = d.AdditionalNotes,
        CreatedAtUtc = d.CreatedAtUtc
    };
}
```

- [ ] **Step 6: Run unit test to verify it passes**

Run: `dotnet test backend/tests/DokPortal.Infrastructure.Tests`
Expected: PASS.

- [ ] **Step 7: Write the failing integration test**

`backend/tests/DokPortal.Api.IntegrationTests/DocumentsControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Documents;
using DokPortal.Application.People;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class DocumentsControllerTests : IntegrationTestBase
{
    public DocumentsControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Generate_AsAdministrator_ReturnsPdfAndRecordsHistory()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var personResponse = await admin.PostAsJsonAsync("/api/people", new { FirstName = "Jan", LastName = "Kowalski" });
        var person = await personResponse.Content.ReadFromJsonAsync<PersonDto>();

        var response = await admin.PostAsJsonAsync("/api/documents/generate", new
        {
            Template = "LetterToBishop", PersonId = person!.Id, AdditionalNotes = "Prośba o wydanie dekretu"
        });

        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);

        var historyResponse = await admin.GetAsync("/api/documents");
        var history = await historyResponse.Content.ReadFromJsonAsync<List<GeneratedDocumentDto>>(EnumJsonOptions);
        Assert.Single(history!);
        Assert.Equal("Jan Kowalski", history![0].PersonFullName);
    }

    [Fact]
    public async Task Generate_AsKatechista_IsForbidden()
    {
        var katechista = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");

        var response = await katechista.PostAsJsonAsync("/api/documents/generate", new
        {
            Template = "LetterToBishop", PersonId = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

- [ ] **Step 8: Run integration test to verify it fails, then implement the controller**

Run: `dotnet test backend/tests/DokPortal.Api.IntegrationTests`
Expected: FAIL — 404, `DocumentsController` does not exist yet.

`backend/src/DokPortal.Api/Controllers/DocumentsController.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using DokPortal.Application.Documents;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;

    public DocumentsController(IDocumentService documentService) => _documentService = documentService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GeneratedDocumentDto>>> GetHistory(CancellationToken ct)
        => Ok(await _documentService.GetHistoryAsync(ct));

    [HttpPost("generate")]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorSKSP},{AppRoles.DyrektorDOK}")]
    public async Task<IActionResult> Generate(GenerateDocumentRequest request, CancellationToken ct)
    {
        var userId = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        var result = await _documentService.GenerateAsync(request, userId, ct);
        if (result is null) return NotFound();
        return File(result.PdfBytes, "application/pdf", $"{result.History.Template}.pdf");
    }
}
```

In `backend/src/DokPortal.Api/Program.cs`, add `using DokPortal.Application.Documents;` near the other `using DokPortal.Application.*` lines, and `builder.Services.AddScoped<IDocumentService, DocumentService>();` after the `INameDayService` registration.

- [ ] **Step 9: Run tests to verify they pass**

Run: `dotnet test backend/DokPortal.sln`
Expected: PASS.

- [ ] **Step 10: Commit**

```bash
git add backend
git commit -m "Add document generator: QuestPDF rendering, history, and endpoints"
git push origin master
```

---

## Task 4: Mailing module (backend)

**Files:**
- Create: `backend/src/DokPortal.Application/Mailing/MailingCampaignDto.cs`
- Create: `backend/src/DokPortal.Application/Mailing/CreateMailingCampaignRequest.cs`
- Create: `backend/src/DokPortal.Application/Mailing/IMailingService.cs`
- Create: `backend/src/DokPortal.Infrastructure/Services/MailingService.cs`
- Create: `backend/src/DokPortal.Api/Controllers/MailingController.cs`
- Modify: `backend/src/DokPortal.Api/Program.cs`
- Test: `backend/tests/DokPortal.Infrastructure.Tests/Services/MailingServiceTests.cs`
- Test: `backend/tests/DokPortal.Api.IntegrationTests/MailingControllerTests.cs`

**Interfaces:**
- Consumes: `Candidate`, `CanonicalMission`, `DokCase`/`DokStage` (Phases 2-3), `MailingGroup`/`CampaignStatus` enums (Task 1).
- Produces: `IMailingService.{ListAsync, CreateAsync, SendAsync, GetRecipientCountAsync}`; `GET/POST /api/mailing/campaigns`, `POST /api/mailing/campaigns/{id}/send` — no later task depends on this.

- [ ] **Step 1: Write the failing unit test**

`backend/tests/DokPortal.Infrastructure.Tests/Services/MailingServiceTests.cs`:

```csharp
using DokPortal.Application.Mailing;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class MailingServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    private static Person NewPerson() => new()
    {
        Id = Guid.NewGuid(), FirstName = "Test", LastName = "Osoba",
        CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
    };

    [Fact]
    public async Task GetRecipientCountAsync_CountsDokGraduatesAndDokCasesSeparately()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = NewPerson();
        var catechist = NewPerson();
        db.People.AddRange(person, catechist);
        db.DokCases.AddRange(
            new DokCase { Id = Guid.NewGuid(), PersonId = person.Id, Path = DokPath.Confirmation, Stage = DokStage.Graduate, CatechistPersonId = catechist.Id, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
            new DokCase { Id = Guid.NewGuid(), PersonId = person.Id, Path = DokPath.Confirmation, Stage = DokStage.Formation, CatechistPersonId = catechist.Id, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var service = new MailingService(db);

        Assert.Equal(1, await service.GetRecipientCountAsync(MailingGroup.DokGraduates, default));
        Assert.Equal(2, await service.GetRecipientCountAsync(MailingGroup.DokCases, default));
    }

    [Fact]
    public async Task CreateAsync_SnapshotsRecipientCountAtCreationTime()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = NewPerson();
        db.People.Add(person);
        db.Candidates.Add(new Candidate { Id = Guid.NewGuid(), PersonId = person.Id, Year = 1, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var service = new MailingService(db);
        var created = await service.CreateAsync(
            new CreateMailingCampaignRequest { Subject = "Zaproszenie", Body = "Treść", Group = MailingGroup.CandidatesSksp }, default);

        Assert.Equal(1, created.RecipientCount);
        Assert.Equal(CampaignStatus.Draft, created.Status);
    }

    [Fact]
    public async Task SendAsync_FlipsStatusAndStampsSentAtUtc()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var service = new MailingService(db);
        var created = await service.CreateAsync(
            new CreateMailingCampaignRequest { Subject = "Zaproszenie", Body = "Treść", Group = MailingGroup.DokCases }, default);

        var sent = await service.SendAsync(created.Id, default);

        Assert.NotNull(sent);
        Assert.Equal(CampaignStatus.Sent, sent!.Status);
        Assert.NotNull(sent.SentAtUtc);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/DokPortal.Infrastructure.Tests`
Expected: FAIL to compile — none of the new Application types or `MailingService` exist yet.

- [ ] **Step 3: Implement the DTOs and interface**

`backend/src/DokPortal.Application/Mailing/MailingCampaignDto.cs`:

```csharp
using DokPortal.Domain.Enums;

namespace DokPortal.Application.Mailing;

public class MailingCampaignDto
{
    public required Guid Id { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public MailingGroup Group { get; init; }
    public required int RecipientCount { get; init; }
    public CampaignStatus Status { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public DateTime? SentAtUtc { get; init; }
}
```

`backend/src/DokPortal.Application/Mailing/CreateMailingCampaignRequest.cs`:

```csharp
using DokPortal.Domain.Enums;

namespace DokPortal.Application.Mailing;

public class CreateMailingCampaignRequest
{
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public MailingGroup Group { get; init; }
}
```

`backend/src/DokPortal.Application/Mailing/IMailingService.cs`:

```csharp
using DokPortal.Domain.Enums;

namespace DokPortal.Application.Mailing;

public interface IMailingService
{
    Task<IReadOnlyList<MailingCampaignDto>> ListAsync(CancellationToken ct);
    Task<MailingCampaignDto> CreateAsync(CreateMailingCampaignRequest request, CancellationToken ct);
    Task<MailingCampaignDto?> SendAsync(Guid id, CancellationToken ct);
    Task<int> GetRecipientCountAsync(MailingGroup group, CancellationToken ct);
}
```

- [ ] **Step 4: Implement the service**

`backend/src/DokPortal.Infrastructure/Services/MailingService.cs`:

```csharp
using DokPortal.Application.Mailing;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class MailingService : IMailingService
{
    private readonly AppDbContext _db;

    public MailingService(AppDbContext db) => _db = db;

    public async Task<int> GetRecipientCountAsync(MailingGroup group, CancellationToken ct) => group switch
    {
        MailingGroup.CandidatesSksp => await _db.Candidates.CountAsync(ct),
        MailingGroup.Missionaries => await _db.CanonicalMissions.Select(m => m.PersonId).Distinct().CountAsync(ct),
        MailingGroup.DokGraduates => await _db.DokCases.CountAsync(c => c.Stage == DokStage.Graduate, ct),
        MailingGroup.DokCases => await _db.DokCases.CountAsync(ct),
        _ => throw new ArgumentOutOfRangeException(nameof(group))
    };

    public async Task<IReadOnlyList<MailingCampaignDto>> ListAsync(CancellationToken ct)
    {
        var campaigns = await _db.MailingCampaigns.AsNoTracking()
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(ct);
        return campaigns.Select(ToDto).ToList();
    }

    public async Task<MailingCampaignDto> CreateAsync(CreateMailingCampaignRequest request, CancellationToken ct)
    {
        var campaign = new MailingCampaign
        {
            Id = Guid.NewGuid(),
            Subject = request.Subject,
            Body = request.Body,
            Group = request.Group,
            RecipientCount = await GetRecipientCountAsync(request.Group, ct),
            Status = CampaignStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.MailingCampaigns.Add(campaign);
        await _db.SaveChangesAsync(ct);
        return ToDto(campaign);
    }

    public async Task<MailingCampaignDto?> SendAsync(Guid id, CancellationToken ct)
    {
        var campaign = await _db.MailingCampaigns.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (campaign is null) return null;

        campaign.RecipientCount = await GetRecipientCountAsync(campaign.Group, ct);
        campaign.Status = CampaignStatus.Sent;
        campaign.SentAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(campaign);
    }

    private static MailingCampaignDto ToDto(MailingCampaign c) => new()
    {
        Id = c.Id,
        Subject = c.Subject,
        Body = c.Body,
        Group = c.Group,
        RecipientCount = c.RecipientCount,
        Status = c.Status,
        CreatedAtUtc = c.CreatedAtUtc,
        SentAtUtc = c.SentAtUtc
    };
}
```

- [ ] **Step 5: Run unit test to verify it passes**

Run: `dotnet test backend/tests/DokPortal.Infrastructure.Tests`
Expected: PASS.

- [ ] **Step 6: Write the failing integration test**

`backend/tests/DokPortal.Api.IntegrationTests/MailingControllerTests.cs`:

```csharp
using System.Net.Http.Json;
using DokPortal.Application.Mailing;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class MailingControllerTests : IntegrationTestBase
{
    public MailingControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreateThenSend_UpdatesStatusAndRecipientCount()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

        var createResponse = await admin.PostAsJsonAsync("/api/mailing/campaigns", new
        {
            Subject = "Zaproszenie na rekolekcje", Body = "Treść zaproszenia", Group = "DokCases"
        });
        var created = await createResponse.Content.ReadFromJsonAsync<MailingCampaignDto>(EnumJsonOptions);
        Assert.Equal("Draft", created!.Status.ToString());

        var sendResponse = await admin.PostAsJsonAsync($"/api/mailing/campaigns/{created.Id}/send", new { });
        var sent = await sendResponse.Content.ReadFromJsonAsync<MailingCampaignDto>(EnumJsonOptions);

        Assert.Equal("Sent", sent!.Status.ToString());
        Assert.NotNull(sent.SentAtUtc);
    }
}
```

- [ ] **Step 7: Run integration test to verify it fails, then implement the controller**

Run: `dotnet test backend/tests/DokPortal.Api.IntegrationTests`
Expected: FAIL — 404, `MailingController` does not exist yet.

`backend/src/DokPortal.Api/Controllers/MailingController.cs`:

```csharp
using DokPortal.Application.Mailing;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/mailing/campaigns")]
[Authorize]
public class MailingController : ControllerBase
{
    private readonly IMailingService _mailingService;

    public MailingController(IMailingService mailingService) => _mailingService = mailingService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MailingCampaignDto>>> List(CancellationToken ct)
        => Ok(await _mailingService.ListAsync(ct));

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorSKSP},{AppRoles.DyrektorDOK}")]
    public async Task<ActionResult<MailingCampaignDto>> Create(CreateMailingCampaignRequest request, CancellationToken ct)
        => Ok(await _mailingService.CreateAsync(request, ct));

    [HttpPost("{id:guid}/send")]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorSKSP},{AppRoles.DyrektorDOK}")]
    public async Task<ActionResult<MailingCampaignDto>> Send(Guid id, CancellationToken ct)
    {
        var sent = await _mailingService.SendAsync(id, ct);
        return sent is null ? NotFound() : Ok(sent);
    }
}
```

In `backend/src/DokPortal.Api/Program.cs`, add `using DokPortal.Application.Mailing;` near the other `using DokPortal.Application.*` lines, and `builder.Services.AddScoped<IMailingService, MailingService>();` after the `IDocumentService` registration.

- [ ] **Step 8: Run tests to verify they pass**

Run: `dotnet test backend/DokPortal.sln`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add backend
git commit -m "Add mailing module: campaign records with computed recipient counts"
git push origin master
```

---

## Task 5: Audit log module and RODO/role-change wiring

**Files:**
- Create: `backend/src/DokPortal.Application/AuditLog/AuditLogEntryDto.cs`
- Create: `backend/src/DokPortal.Application/AuditLog/IAuditLogService.cs`
- Create: `backend/src/DokPortal.Infrastructure/Services/AuditLogService.cs`
- Create: `backend/src/DokPortal.Api/Controllers/AuditLogController.cs`
- Modify: `backend/src/DokPortal.Application/PastoralNotes/IPastoralNoteService.cs`
- Modify: `backend/src/DokPortal.Infrastructure/Services/PastoralNoteService.cs`
- Modify: `backend/src/DokPortal.Api/Controllers/PastoralNotesController.cs`
- Modify: `backend/src/DokPortal.Api/Controllers/UsersController.cs`
- Modify: `backend/src/DokPortal.Api/Program.cs`
- Test: `backend/tests/DokPortal.Infrastructure.Tests/Services/AuditLogServiceTests.cs`
- Test: `backend/tests/DokPortal.Api.IntegrationTests/AuditLogControllerTests.cs`

**Interfaces:**
- Consumes: `PastoralNotesController`, `UsersController` (Phase 1/3), `AuditResult` enum (Task 1).
- Produces: `IAuditLogService.{LogAsync, ListAsync}`; `GET /api/audit-log` — no later task depends on this.

- [ ] **Step 1: Write the failing unit test**

`backend/tests/DokPortal.Infrastructure.Tests/Services/AuditLogServiceTests.cs`:

```csharp
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class AuditLogServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task LogAsync_ThenListAsync_ReturnsNewestFirst()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var service = new AuditLogService(db);

        await service.LogAsync("user-1", "user1@example.org", "ReadPastoralNotes", "Jan Kowalski", AuditResult.Blocked, default);
        await service.LogAsync("user-2", "user2@example.org", "AssignUserRoles", "target@example.org", AuditResult.Allowed, default);

        var entries = await service.ListAsync(default);

        Assert.Equal(2, entries.Count);
        Assert.Equal("AssignUserRoles", entries[0].Action);
        Assert.Equal(AuditResult.Blocked, entries[1].Result);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/DokPortal.Infrastructure.Tests`
Expected: FAIL to compile — `AuditLogService` does not exist yet.

- [ ] **Step 3: Implement the DTO, interface, and service**

`backend/src/DokPortal.Application/AuditLog/AuditLogEntryDto.cs`:

```csharp
using DokPortal.Domain.Enums;

namespace DokPortal.Application.AuditLog;

public class AuditLogEntryDto
{
    public required Guid Id { get; init; }
    public required DateTime TimestampUtc { get; init; }
    public required string UserId { get; init; }
    public required string UserEmail { get; init; }
    public required string Action { get; init; }
    public required string ObjectDescription { get; init; }
    public AuditResult Result { get; init; }
}
```

`backend/src/DokPortal.Application/AuditLog/IAuditLogService.cs`:

```csharp
using DokPortal.Domain.Enums;

namespace DokPortal.Application.AuditLog;

public interface IAuditLogService
{
    Task LogAsync(string userId, string userEmail, string action, string objectDescription, AuditResult result, CancellationToken ct);
    Task<IReadOnlyList<AuditLogEntryDto>> ListAsync(CancellationToken ct);
}
```

`backend/src/DokPortal.Infrastructure/Services/AuditLogService.cs`:

```csharp
using DokPortal.Application.AuditLog;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class AuditLogService : IAuditLogService
{
    private readonly AppDbContext _db;

    public AuditLogService(AppDbContext db) => _db = db;

    public async Task LogAsync(string userId, string userEmail, string action, string objectDescription, AuditResult result, CancellationToken ct)
    {
        _db.AuditLogEntries.Add(new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            TimestampUtc = DateTime.UtcNow,
            UserId = userId,
            UserEmail = userEmail,
            Action = action,
            ObjectDescription = objectDescription,
            Result = result
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLogEntryDto>> ListAsync(CancellationToken ct)
    {
        var entries = await _db.AuditLogEntries.AsNoTracking()
            .OrderByDescending(e => e.TimestampUtc)
            .ToListAsync(ct);
        return entries.Select(ToDto).ToList();
    }

    private static AuditLogEntryDto ToDto(AuditLogEntry e) => new()
    {
        Id = e.Id,
        TimestampUtc = e.TimestampUtc,
        UserId = e.UserId,
        UserEmail = e.UserEmail,
        Action = e.Action,
        ObjectDescription = e.ObjectDescription,
        Result = e.Result
    };
}
```

- [ ] **Step 4: Run unit test to verify it passes**

Run: `dotnet test backend/tests/DokPortal.Infrastructure.Tests`
Expected: PASS.

- [ ] **Step 5: Write the failing integration tests**

`backend/tests/DokPortal.Api.IntegrationTests/AuditLogControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.AuditLog;
using DokPortal.Application.DokCases;
using DokPortal.Application.People;
using DokPortal.Application.Users;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class AuditLogControllerTests : IntegrationTestBase
{
    public AuditLogControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task ReadPastoralNotes_WhenFilteredByOtherAuthor_IsRecordedAsBlocked()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var personResponse = await admin.PostAsJsonAsync("/api/people", new { FirstName = "Jan", LastName = "Kowalski" });
        var person = await personResponse.Content.ReadFromJsonAsync<PersonDto>();
        var catechistResponse = await admin.PostAsJsonAsync("/api/people", new { FirstName = "Anna", LastName = "Maj" });
        var catechist = await catechistResponse.Content.ReadFromJsonAsync<PersonDto>();
        var caseResponse = await admin.PostAsJsonAsync("/api/dok-cases", new
        {
            PersonId = person!.Id, Path = "Confirmation", Stage = "Formation", CatechistPersonId = catechist!.Id
        });
        var dokCase = await caseResponse.Content.ReadFromJsonAsync<DokCaseDto>(EnumJsonOptions);

        var katechistaA = await CreateAuthenticatedClientAsync($"kat-a-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");
        await katechistaA.PostAsJsonAsync($"/api/dok-cases/{dokCase!.Id}/notes", new { Content = "Notatka poufna" });

        var katechistaB = await CreateAuthenticatedClientAsync($"kat-b-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");
        await katechistaB.GetAsync($"/api/dok-cases/{dokCase.Id}/notes");

        var logResponse = await admin.GetAsync("/api/audit-log");
        var entries = await logResponse.Content.ReadFromJsonAsync<List<AuditLogEntryDto>>(EnumJsonOptions);

        Assert.Contains(entries!, e => e.Action == "ReadPastoralNotes" && e.Result.ToString() == "Blocked");
    }

    [Fact]
    public async Task AssignRoles_IsRecordedAsAllowedWithTargetEmail()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var targetEmail = $"target-{Guid.NewGuid():N}@example.org";
        var createResponse = await admin.PostAsJsonAsync("/api/users", new { Email = targetEmail, Password = "Sekret123!", Roles = Array.Empty<string>() });
        var created = await createResponse.Content.ReadFromJsonAsync<UserDto>();

        await admin.PutAsJsonAsync($"/api/users/{created!.Id}/roles", new { Roles = new[] { "KatechistaProwadzacy" } });

        var logResponse = await admin.GetAsync("/api/audit-log");
        var entries = await logResponse.Content.ReadFromJsonAsync<List<AuditLogEntryDto>>(EnumJsonOptions);

        Assert.Contains(entries!, e => e.Action == "AssignUserRoles" && e.ObjectDescription == targetEmail && e.Result.ToString() == "Allowed");
    }

    [Fact]
    public async Task List_AsNonAdministrator_IsForbidden()
    {
        var katechista = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");
        var response = await katechista.GetAsync("/api/audit-log");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

- [ ] **Step 6: Run integration tests to verify they fail**

Run: `dotnet test backend/tests/DokPortal.Api.IntegrationTests`
Expected: FAIL — 404 on `/api/audit-log`, `AuditLogController` does not exist yet.

- [ ] **Step 7: Implement the controller and register the service**

`backend/src/DokPortal.Api/Controllers/AuditLogController.cs`:

```csharp
using DokPortal.Application.AuditLog;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/audit-log")]
[Authorize(Roles = AppRoles.Administrator)]
public class AuditLogController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogController(IAuditLogService auditLogService) => _auditLogService = auditLogService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuditLogEntryDto>>> List(CancellationToken ct)
        => Ok(await _auditLogService.ListAsync(ct));
}
```

In `backend/src/DokPortal.Api/Program.cs`, add `using DokPortal.Application.AuditLog;` near the other `using DokPortal.Application.*` lines, and `builder.Services.AddScoped<IAuditLogService, AuditLogService>();` after the `IMailingService` registration.

- [ ] **Step 8: Add the hidden-notes check to PastoralNoteService**

In `backend/src/DokPortal.Application/PastoralNotes/IPastoralNoteService.cs`, add a new method to the interface:

```csharp
    Task<bool> HasNotesFromOthersAsync(Guid caseId, string currentUserId, CancellationToken ct);
```

In `backend/src/DokPortal.Infrastructure/Services/PastoralNoteService.cs`, add the implementation (the existing `GetVisibleForCaseAsync` and `CreateAsync` are unchanged):

```csharp
    public Task<bool> HasNotesFromOthersAsync(Guid caseId, string currentUserId, CancellationToken ct) =>
        _db.PastoralNotes.AnyAsync(n => n.DokCaseId == caseId && n.AuthorUserId != currentUserId, ct);
```

- [ ] **Step 9: Wire audit logging into PastoralNotesController.GetAll**

In `backend/src/DokPortal.Api/Controllers/PastoralNotesController.cs`, add `using DokPortal.Application.AuditLog;`, `using DokPortal.Application.DokCases;`, and `using DokPortal.Domain.Enums;` to the usings, inject `IAuditLogService` and `IDokCaseService` alongside `IPastoralNoteService`, add a `GetCurrentUserEmail()` helper next to `GetCurrentUserId()`, and replace the `GetAll` method:

```csharp
    private readonly IPastoralNoteService _pastoralNoteService;
    private readonly IDokCaseService _dokCaseService;
    private readonly IAuditLogService _auditLogService;

    public PastoralNotesController(IPastoralNoteService pastoralNoteService, IDokCaseService dokCaseService, IAuditLogService auditLogService)
    {
        _pastoralNoteService = pastoralNoteService;
        _dokCaseService = dokCaseService;
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PastoralNoteDto>>> GetAll(Guid caseId, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var isPrivileged = User.IsInRole(AppRoles.Administrator) || User.IsInRole(AppRoles.DyrektorDOK);
        var notes = await _pastoralNoteService.GetVisibleForCaseAsync(caseId, currentUserId, isPrivileged, ct);

        var dokCase = await _dokCaseService.GetByIdAsync(caseId, ct);
        var objectDescription = dokCase?.PersonFullName ?? $"Sprawa {caseId}";
        var isBlocked = !isPrivileged && await _pastoralNoteService.HasNotesFromOthersAsync(caseId, currentUserId, ct);
        await _auditLogService.LogAsync(
            currentUserId, GetCurrentUserEmail(), "ReadPastoralNotes", objectDescription,
            isBlocked ? AuditResult.Blocked : AuditResult.Allowed, ct);

        return Ok(notes);
    }
```

Add the email helper next to the existing `GetCurrentUserId`:

```csharp
    private string GetCurrentUserEmail() =>
        User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value;
```

- [ ] **Step 10: Wire audit logging into UsersController.AssignRoles**

In `backend/src/DokPortal.Api/Controllers/UsersController.cs`, add `using System.IdentityModel.Tokens.Jwt;` and `using DokPortal.Application.AuditLog;` to the usings, inject `IAuditLogService`, and update `AssignRoles`:

```csharp
    private readonly IUserService _userService;
    private readonly IAuditLogService _auditLogService;

    public UsersController(IUserService userService, IAuditLogService auditLogService)
    {
        _userService = userService;
        _auditLogService = auditLogService;
    }

    [HttpPut("{id}/roles")]
    public async Task<ActionResult<UserDto>> AssignRoles(string id, AssignRolesRequest request, CancellationToken ct)
    {
        var updated = await _userService.AssignRolesAsync(id, request.Roles, ct);
        if (updated is null) return NotFound();

        var currentUserId = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        var currentUserEmail = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value;
        await _auditLogService.LogAsync(currentUserId, currentUserEmail, "AssignUserRoles", updated.Email, DokPortal.Domain.Enums.AuditResult.Allowed, ct);

        return Ok(updated);
    }
```

- [ ] **Step 11: Run tests to verify they pass**

Run: `dotnet test backend/DokPortal.sln`
Expected: PASS (including the unchanged `PastoralNotesControllerTests` from Faza 3, which never assert on the audit log).

- [ ] **Step 12: Commit**

```bash
git add backend
git commit -m "Add audit log: pastoral-notes RODO filtering and role changes now produce visible entries"
git push origin master
```

---

## Task 6: Kalendarz imienin page (frontend)

**Files:**
- Create: `frontend/src/app/features/name-days/name-day.model.ts`
- Create: `frontend/src/app/features/name-days/name-days.service.ts`
- Create: `frontend/src/app/features/name-days/name-days.service.spec.ts`
- Create: `frontend/src/app/features/name-days/name-days-list.component.ts`
- Create: `frontend/src/app/features/name-days/name-days-list.component.html`
- Create: `frontend/src/app/features/name-days/name-days-list.component.scss`
- Create: `frontend/src/app/features/name-days/name-days-list.component.spec.ts`
- Modify: `frontend/src/app/features/people/person.model.ts`
- Modify: `frontend/src/app/features/people/person-form.component.html`
- Modify: `frontend/src/app/features/people/people-list.component.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/layout/nav-items.ts`

**Interfaces:**
- Consumes: `GET /api/name-days/upcoming?days=` (Task 2), `Person`/`PersonFormValue` (Phase 1).
- Produces: `UpcomingNameDay` model, `NameDaysService` — used only by this page. `Person.nameDayMonth`/`nameDayDay` are now also consumed by nothing else, but must stay in sync with the backend `PersonDto` fields added in Task 2.

- [ ] **Step 1: Write the failing service test**

`frontend/src/app/features/name-days/name-day.model.ts`:

```typescript
export interface UpcomingNameDay {
  personId: string;
  fullName: string;
  nameDayMonth: number;
  nameDayDay: number;
  daysUntil: number;
}
```

`frontend/src/app/features/name-days/name-days.service.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it } from 'vitest';
import { NameDaysService } from './name-days.service';
import { environment } from '../../../environments/environment';

describe('NameDaysService', () => {
  it('requests upcoming namedays from the API', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(NameDaysService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.upcoming(30).subscribe();

    const req = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/name-days/upcoming`);
    req.flush([]);
    httpMock.verify();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npx ng test`
Expected: FAIL — `NameDaysService` does not exist yet.

- [ ] **Step 3: Implement the service**

`frontend/src/app/features/name-days/name-days.service.ts`:

```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { UpcomingNameDay } from './name-day.model';

@Injectable({ providedIn: 'root' })
export class NameDaysService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/name-days`;

  constructor(private readonly http: HttpClient) {}

  upcoming(days = 30) {
    return this.http.get<UpcomingNameDay[]>(`${this.baseUrl}/upcoming`, { params: { days } });
  }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npx ng test`
Expected: PASS.

- [ ] **Step 5: Write the failing component test**

`frontend/src/app/features/name-days/name-days-list.component.spec.ts`:

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { NameDaysListComponent } from './name-days-list.component';
import { environment } from '../../../environments/environment';

describe('NameDaysListComponent', () => {
  let fixture: ComponentFixture<NameDaysListComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [NameDaysListComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(NameDaysListComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('renders upcoming namedays returned from the API', () => {
    fixture.detectChanges();
    const req = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/name-days/upcoming`);
    req.flush([{ personId: '1', fullName: 'Ewa Nowak', nameDayMonth: 12, nameDayDay: 24, daysUntil: 3 }]);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Ewa Nowak');
  });
});
```

- [ ] **Step 6: Run test to verify it fails, then implement the component**

Run: `npx ng test`
Expected: FAIL — `NameDaysListComponent` does not exist yet.

`frontend/src/app/features/name-days/name-days-list.component.ts`:

```typescript
import { Component, OnInit, signal } from '@angular/core';
import { NameDaysService } from './name-days.service';
import { UpcomingNameDay } from './name-day.model';

@Component({
  selector: 'app-name-days-list',
  standalone: true,
  templateUrl: './name-days-list.component.html',
  styleUrl: './name-days-list.component.scss'
})
export class NameDaysListComponent implements OnInit {
  readonly nameDays = signal<UpcomingNameDay[]>([]);

  constructor(private readonly nameDaysService: NameDaysService) {}

  ngOnInit(): void {
    this.nameDaysService.upcoming(30).subscribe(items => this.nameDays.set(items));
  }
}
```

`frontend/src/app/features/name-days/name-days-list.component.html`:

```html
<div class="page-heading">
  <div><h2>Kalendarz imienin</h2><p>Najbliższe imieniny osób w bazie (30 dni).</p></div>
</div>

<div class="card">
  <div class="table-wrap">
    <table>
      <thead><tr><th>Osoba</th><th>Data</th><th>Za ile dni</th></tr></thead>
      <tbody>
        @for (item of nameDays(); track item.personId) {
          <tr>
            <td>{{ item.fullName }}</td>
            <td>{{ item.nameDayDay }}.{{ item.nameDayMonth }}</td>
            <td>{{ item.daysUntil === 0 ? 'dziś' : item.daysUntil }}</td>
          </tr>
        } @empty {
          <tr><td colspan="3" class="empty">Brak zbliżających się imienin.</td></tr>
        }
      </tbody>
    </table>
  </div>
</div>
```

`frontend/src/app/features/name-days/name-days-list.component.scss`: leave empty (matches `BudgetDokComponent`'s empty stylesheet — page-level styles come from the shared `styles.scss`).

- [ ] **Step 7: Run test to verify it passes**

Run: `npx ng test`
Expected: PASS.

- [ ] **Step 8: Let the People form set a person's nameday**

The calendar is only useful if someone can enter the data it reads, so the People add/edit form needs the two new fields.

In `frontend/src/app/features/people/person.model.ts`, add `nameDayMonth` and `nameDayDay` to both `Person` and `PersonFormValue`:

```typescript
export interface Person {
  id: string;
  firstName: string;
  lastName: string;
  fullName: string;
  email: string | null;
  phone: string | null;
  birthDate: string | null;
  parishId: string | null;
  parishName: string | null;
  notes: string | null;
  nameDayMonth: number | null;
  nameDayDay: number | null;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface PersonFormValue {
  firstName: string;
  lastName: string;
  email?: string;
  phone?: string;
  birthDate?: string;
  parishId?: string;
  notes?: string;
  nameDayMonth?: number;
  nameDayDay?: number;
}
```

In `frontend/src/app/features/people/person-form.component.html`, add after the `Uwagi` field:

```html
          <div class="field"><label>Miesiąc imienin</label><input type="number" min="1" max="12" [(ngModel)]="value.nameDayMonth" name="nameDayMonth" /></div>
          <div class="field"><label>Dzień imienin</label><input type="number" min="1" max="31" [(ngModel)]="value.nameDayDay" name="nameDayDay" /></div>
```

In `frontend/src/app/features/people/people-list.component.ts`, add the two fields to the object built in `openEditForm`:

```typescript
  openEditForm(person: Person): void {
    this.editingId.set(person.id);
    this.formValue = {
      firstName: person.firstName,
      lastName: person.lastName,
      email: person.email ?? undefined,
      phone: person.phone ?? undefined,
      notes: person.notes ?? undefined,
      nameDayMonth: person.nameDayMonth ?? undefined,
      nameDayDay: person.nameDayDay ?? undefined
    };
    this.isFormOpen.set(true);
  }
```

- [ ] **Step 9: Run frontend tests to verify nothing broke**

Run: `npx ng test`
Expected: PASS — the existing `PeopleListComponent`/`PersonFormComponent` specs don't assert on the full field set, so they stay green.

- [ ] **Step 10: Wire the route and nav item**

In `frontend/src/app/app.routes.ts`, add after the `people` route:

```typescript
      {
        path: 'name-days',
        loadComponent: () => import('./features/name-days/name-days-list.component').then(m => m.NameDaysListComponent)
      },
```

In `frontend/src/app/layout/nav-items.ts`, add after the `Baza osób` entry:

```typescript
  { label: 'Kalendarz imienin', icon: '✿', path: '/name-days', roles: [] },
```

- [ ] **Step 11: Build and verify**

Run: `npm run build`
Expected: builds successfully.

- [ ] **Step 12: Commit**

```bash
git add frontend
git commit -m "Add Kalendarz imienin page and nameday fields on the People form"
git push origin master
```

---

## Task 7: Dokumenty i pisma page (frontend)

**Files:**
- Create: `frontend/src/app/features/documents/generated-document.model.ts`
- Create: `frontend/src/app/features/documents/documents.service.ts`
- Create: `frontend/src/app/features/documents/documents.service.spec.ts`
- Create: `frontend/src/app/features/documents/documents.component.ts`
- Create: `frontend/src/app/features/documents/documents.component.html`
- Create: `frontend/src/app/features/documents/documents.component.scss`
- Create: `frontend/src/app/features/documents/documents.component.spec.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/layout/nav-items.ts`

**Interfaces:**
- Consumes: `GET/POST /api/documents` (Task 3), `PeopleService` (Phase 1).
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Write the failing service test**

`frontend/src/app/features/documents/generated-document.model.ts`:

```typescript
export type DocumentTemplate =
  | 'LetterToBishop'
  | 'ConversionConsent'
  | 'CanonicalMissionDecree'
  | 'DokReferral'
  | 'SkspCompletionCertificate'
  | 'SacramentCertificate';

export interface GeneratedDocument {
  id: string;
  template: DocumentTemplate;
  personId: string;
  personFullName: string;
  generatedByUserId: string;
  additionalNotes: string | null;
  createdAtUtc: string;
}

export interface GenerateDocumentValue {
  template: DocumentTemplate;
  personId: string;
  additionalNotes?: string;
}

export const DOCUMENT_TEMPLATE_LABELS: Record<DocumentTemplate, string> = {
  LetterToBishop: 'Pismo do Biskupa',
  ConversionConsent: 'Zgoda na konwersję',
  CanonicalMissionDecree: 'Dekret misji kanonicznej',
  DokReferral: 'Skierowanie do DOK',
  SkspCompletionCertificate: 'Zaświadczenie ukończenia SKŚP',
  SacramentCertificate: 'Zaświadczenie o sakramencie'
};
```

`frontend/src/app/features/documents/documents.service.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect } from 'vitest';
import { DocumentsService } from './documents.service';
import { environment } from '../../../environments/environment';

describe('DocumentsService', () => {
  it('requests document history from the API', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(DocumentsService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.history().subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/documents`);
    req.flush([]);
    httpMock.verify();
  });

  it('posts a generate request expecting a blob response', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(DocumentsService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.generate({ template: 'LetterToBishop', personId: 'p1' }).subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/documents/generate`);
    expect(req.request.responseType).toBe('blob');
    req.flush(new Blob());
    httpMock.verify();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npx ng test`
Expected: FAIL — `DocumentsService` does not exist yet.

- [ ] **Step 3: Implement the service**

`frontend/src/app/features/documents/documents.service.ts`:

```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { GeneratedDocument, GenerateDocumentValue } from './generated-document.model';

@Injectable({ providedIn: 'root' })
export class DocumentsService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/documents`;

  constructor(private readonly http: HttpClient) {}

  history() {
    return this.http.get<GeneratedDocument[]>(this.baseUrl);
  }

  generate(value: GenerateDocumentValue) {
    return this.http.post(`${this.baseUrl}/generate`, value, { responseType: 'blob' });
  }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npx ng test`
Expected: PASS.

- [ ] **Step 5: Write the failing component test**

`frontend/src/app/features/documents/documents.component.spec.ts`:

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { DocumentsComponent } from './documents.component';
import { environment } from '../../../environments/environment';

describe('DocumentsComponent', () => {
  let fixture: ComponentFixture<DocumentsComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [DocumentsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(DocumentsComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('renders document history returned from the API', () => {
    fixture.detectChanges();
    httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/documents`).flush([
      { id: '1', template: 'LetterToBishop', personId: 'p1', personFullName: 'Jan Kowalski', generatedByUserId: 'u1', additionalNotes: null, createdAtUtc: '2026-09-22T00:00:00Z' }
    ]);
    httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/people`).flush({ items: [], totalCount: 0, page: 1, pageSize: 200 });
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Jan Kowalski');
  });
});
```

- [ ] **Step 6: Run test to verify it fails, then implement the component**

Run: `npx ng test`
Expected: FAIL — `DocumentsComponent` does not exist yet.

`frontend/src/app/features/documents/documents.component.ts`:

```typescript
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DocumentsService } from './documents.service';
import { DOCUMENT_TEMPLATE_LABELS, DocumentTemplate, GeneratedDocument, GenerateDocumentValue } from './generated-document.model';
import { PeopleService } from '../people/people.service';
import { Person } from '../people/person.model';

@Component({
  selector: 'app-documents',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './documents.component.html',
  styleUrl: './documents.component.scss'
})
export class DocumentsComponent implements OnInit {
  readonly history = signal<GeneratedDocument[]>([]);
  readonly templateLabels = DOCUMENT_TEMPLATE_LABELS;
  readonly templates = Object.keys(DOCUMENT_TEMPLATE_LABELS) as DocumentTemplate[];
  people: Person[] = [];
  form: GenerateDocumentValue = { template: 'LetterToBishop', personId: '', additionalNotes: '' };

  constructor(
    private readonly documentsService: DocumentsService,
    private readonly peopleService: PeopleService
  ) {}

  ngOnInit(): void {
    this.load();
    this.peopleService.search('', 1, 200).subscribe(result => (this.people = result.items));
  }

  load(): void {
    this.documentsService.history().subscribe(items => this.history.set(items));
  }

  generate(): void {
    this.documentsService.generate(this.form).subscribe(blob => {
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${this.form.template}.pdf`;
      link.click();
      window.URL.revokeObjectURL(url);
      this.load();
    });
  }
}
```

`frontend/src/app/features/documents/documents.component.html`:

```html
<div class="page-heading">
  <div><h2>Dokumenty i pisma</h2><p>Generator pism z polami scalonymi z profilu osoby.</p></div>
</div>

<div class="card">
  <div class="card-body">
    <div class="form-grid">
      <div class="field">
        <label>Szablon</label>
        <select [(ngModel)]="form.template" name="template">
          @for (template of templates; track template) {
            <option [value]="template">{{ templateLabels[template] }}</option>
          }
        </select>
      </div>
      <div class="field">
        <label>Osoba</label>
        <select [(ngModel)]="form.personId" name="personId">
          <option value="">— wybierz —</option>
          @for (person of people; track person.id) {
            <option [value]="person.id">{{ person.fullName }}</option>
          }
        </select>
      </div>
      <div class="field full"><label>Uwagi dodatkowe</label><textarea [(ngModel)]="form.additionalNotes" name="additionalNotes"></textarea></div>
    </div>
    <button class="btn primary" [disabled]="!form.personId" (click)="generate()">Generuj PDF</button>
  </div>
</div>

<div class="card">
  <div class="table-wrap">
    <table>
      <thead><tr><th>Data</th><th>Szablon</th><th>Osoba</th></tr></thead>
      <tbody>
        @for (doc of history(); track doc.id) {
          <tr>
            <td>{{ doc.createdAtUtc | date: 'yyyy-MM-dd' }}</td>
            <td>{{ templateLabels[doc.template] }}</td>
            <td>{{ doc.personFullName }}</td>
          </tr>
        } @empty {
          <tr><td colspan="3" class="empty">Brak wygenerowanych dokumentów.</td></tr>
        }
      </tbody>
    </table>
  </div>
</div>
```

`frontend/src/app/features/documents/documents.component.scss`: leave empty.

- [ ] **Step 7: Run test to verify it passes**

Run: `npx ng test`
Expected: PASS.

- [ ] **Step 8: Wire the route and nav item**

In `frontend/src/app/app.routes.ts`, add after the `name-days` route:

```typescript
      {
        path: 'documents',
        loadComponent: () => import('./features/documents/documents.component').then(m => m.DocumentsComponent)
      },
```

In `frontend/src/app/layout/nav-items.ts`, add after the `Kalendarz imienin` entry:

```typescript
  { label: 'Dokumenty i pisma', icon: '✎', path: '/documents', roles: ['Administrator', 'DyrektorSKSP', 'DyrektorDOK'] },
```

- [ ] **Step 9: Build and verify**

Run: `npm run build`
Expected: builds successfully.

- [ ] **Step 10: Commit**

```bash
git add frontend
git commit -m "Add Dokumenty i pisma page with PDF download"
git push origin master
```

---

## Task 8: Mailing page (frontend)

**Files:**
- Create: `frontend/src/app/features/mailing/mailing-campaign.model.ts`
- Create: `frontend/src/app/features/mailing/mailing.service.ts`
- Create: `frontend/src/app/features/mailing/mailing.service.spec.ts`
- Create: `frontend/src/app/features/mailing/mailing.component.ts`
- Create: `frontend/src/app/features/mailing/mailing.component.html`
- Create: `frontend/src/app/features/mailing/mailing.component.scss`
- Create: `frontend/src/app/features/mailing/mailing.component.spec.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/layout/nav-items.ts`

**Interfaces:**
- Consumes: `GET/POST /api/mailing/campaigns`, `POST /api/mailing/campaigns/{id}/send` (Task 4).
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Write the failing service test**

`frontend/src/app/features/mailing/mailing-campaign.model.ts`:

```typescript
export type MailingGroup = 'CandidatesSksp' | 'Missionaries' | 'DokGraduates' | 'DokCases';
export type CampaignStatus = 'Draft' | 'Sent';

export interface MailingCampaign {
  id: string;
  subject: string;
  body: string;
  group: MailingGroup;
  recipientCount: number;
  status: CampaignStatus;
  createdAtUtc: string;
  sentAtUtc: string | null;
}

export interface CreateMailingCampaignValue {
  subject: string;
  body: string;
  group: MailingGroup;
}

export const MAILING_GROUP_LABELS: Record<MailingGroup, string> = {
  CandidatesSksp: 'Kandydaci SKŚP',
  Missionaries: 'Katechiści posłani',
  DokGraduates: 'Absolwenci DOK',
  DokCases: 'Podopieczni DOK'
};
```

`frontend/src/app/features/mailing/mailing.service.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it } from 'vitest';
import { MailingService } from './mailing.service';
import { environment } from '../../../environments/environment';

describe('MailingService', () => {
  it('requests campaigns from the API', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(MailingService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.list().subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/mailing/campaigns`);
    req.flush([]);
    httpMock.verify();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npx ng test`
Expected: FAIL — `MailingService` does not exist yet.

- [ ] **Step 3: Implement the service**

`frontend/src/app/features/mailing/mailing.service.ts`:

```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { CreateMailingCampaignValue, MailingCampaign } from './mailing-campaign.model';

@Injectable({ providedIn: 'root' })
export class MailingService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/mailing/campaigns`;

  constructor(private readonly http: HttpClient) {}

  list() {
    return this.http.get<MailingCampaign[]>(this.baseUrl);
  }

  create(value: CreateMailingCampaignValue) {
    return this.http.post<MailingCampaign>(this.baseUrl, value);
  }

  send(id: string) {
    return this.http.post<MailingCampaign>(`${this.baseUrl}/${id}/send`, {});
  }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npx ng test`
Expected: PASS.

- [ ] **Step 5: Write the failing component test**

`frontend/src/app/features/mailing/mailing.component.spec.ts`:

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { MailingComponent } from './mailing.component';
import { environment } from '../../../environments/environment';

describe('MailingComponent', () => {
  let fixture: ComponentFixture<MailingComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [MailingComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(MailingComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('renders campaigns returned from the API', () => {
    fixture.detectChanges();
    const req = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/mailing/campaigns`);
    req.flush([{ id: '1', subject: 'Zaproszenie', body: 'Treść', group: 'DokCases', recipientCount: 12, status: 'Draft', createdAtUtc: '2026-09-22T00:00:00Z', sentAtUtc: null }]);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Zaproszenie');
  });
});
```

- [ ] **Step 6: Run test to verify it fails, then implement the component**

Run: `npx ng test`
Expected: FAIL — `MailingComponent` does not exist yet.

`frontend/src/app/features/mailing/mailing.component.ts`:

```typescript
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MailingService } from './mailing.service';
import { CreateMailingCampaignValue, MAILING_GROUP_LABELS, MailingCampaign, MailingGroup } from './mailing-campaign.model';

@Component({
  selector: 'app-mailing',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './mailing.component.html',
  styleUrl: './mailing.component.scss'
})
export class MailingComponent implements OnInit {
  readonly campaigns = signal<MailingCampaign[]>([]);
  readonly isFormOpen = signal(false);
  readonly groupLabels = MAILING_GROUP_LABELS;
  readonly groups = Object.keys(MAILING_GROUP_LABELS) as MailingGroup[];
  newCampaign: CreateMailingCampaignValue = { subject: '', body: '', group: 'CandidatesSksp' };

  constructor(private readonly mailingService: MailingService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.mailingService.list().subscribe(campaigns => this.campaigns.set(campaigns));
  }

  openAddForm(): void {
    this.newCampaign = { subject: '', body: '', group: 'CandidatesSksp' };
    this.isFormOpen.set(true);
  }

  createCampaign(): void {
    this.mailingService.create(this.newCampaign).subscribe(() => {
      this.isFormOpen.set(false);
      this.load();
    });
  }

  sendCampaign(campaign: MailingCampaign): void {
    this.mailingService.send(campaign.id).subscribe(() => this.load());
  }

  cancel(): void {
    this.isFormOpen.set(false);
  }
}
```

`frontend/src/app/features/mailing/mailing.component.html`:

```html
<div class="page-heading">
  <div><h2>Mailing</h2><p>Rejestr kampanii do grup odbiorców (bez wysyłki e-mail).</p></div>
  <button class="btn primary" (click)="openAddForm()">＋ Nowa kampania</button>
</div>

<div class="card">
  <div class="table-wrap">
    <table>
      <thead><tr><th>Temat</th><th>Grupa</th><th>Odbiorcy</th><th>Status</th><th></th></tr></thead>
      <tbody>
        @for (campaign of campaigns(); track campaign.id) {
          <tr>
            <td>{{ campaign.subject }}</td>
            <td>{{ groupLabels[campaign.group] }}</td>
            <td>{{ campaign.recipientCount }}</td>
            <td>
              @if (campaign.status === 'Sent') {
                <span class="pill green">Wysłano</span>
              } @else {
                <span class="pill">Szkic</span>
              }
            </td>
            <td>
              @if (campaign.status === 'Draft') {
                <button class="btn primary small" (click)="sendCampaign(campaign)">Wyślij</button>
              }
            </td>
          </tr>
        } @empty {
          <tr><td colspan="5" class="empty">Brak kampanii.</td></tr>
        }
      </tbody>
    </table>
  </div>
</div>

@if (isFormOpen()) {
  <div class="overlay show">
    <div class="modal">
      <div class="modal-head">
        <h3>Nowa kampania</h3>
        <button class="close" (click)="cancel()">×</button>
      </div>
      <div class="modal-body">
        <div class="form-grid">
          <div class="field"><label>Temat</label><input [(ngModel)]="newCampaign.subject" name="subject" /></div>
          <div class="field">
            <label>Grupa odbiorców</label>
            <select [(ngModel)]="newCampaign.group" name="group">
              @for (group of groups; track group) {
                <option [value]="group">{{ groupLabels[group] }}</option>
              }
            </select>
          </div>
          <div class="field full"><label>Treść</label><textarea [(ngModel)]="newCampaign.body" name="body"></textarea></div>
        </div>
      </div>
      <div class="modal-foot">
        <button class="btn ghost" (click)="cancel()">Anuluj</button>
        <button class="btn primary" (click)="createCampaign()">Zapisz</button>
      </div>
    </div>
  </div>
}
```

`frontend/src/app/features/mailing/mailing.component.scss`: leave empty.

- [ ] **Step 7: Run test to verify it passes**

Run: `npx ng test`
Expected: PASS.

- [ ] **Step 8: Wire the route and nav item**

In `frontend/src/app/app.routes.ts`, add after the `documents` route:

```typescript
      {
        path: 'mailing',
        loadComponent: () => import('./features/mailing/mailing.component').then(m => m.MailingComponent)
      },
```

In `frontend/src/app/layout/nav-items.ts`, add after the `Dokumenty i pisma` entry:

```typescript
  { label: 'Mailing', icon: '✉', path: '/mailing', roles: ['Administrator', 'DyrektorSKSP', 'DyrektorDOK'] },
```

- [ ] **Step 9: Build and verify**

Run: `npm run build`
Expected: builds successfully.

- [ ] **Step 10: Commit**

```bash
git add frontend
git commit -m "Add Mailing page"
git push origin master
```

---

## Task 9: Audit log page (frontend)

**Files:**
- Create: `frontend/src/app/features/audit-log/audit-log-entry.model.ts`
- Create: `frontend/src/app/features/audit-log/audit-log.service.ts`
- Create: `frontend/src/app/features/audit-log/audit-log.service.spec.ts`
- Create: `frontend/src/app/features/audit-log/audit-log.component.ts`
- Create: `frontend/src/app/features/audit-log/audit-log.component.html`
- Create: `frontend/src/app/features/audit-log/audit-log.component.scss`
- Create: `frontend/src/app/features/audit-log/audit-log.component.spec.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/layout/nav-items.ts`

**Interfaces:**
- Consumes: `GET /api/audit-log` (Task 5).
- Produces: nothing consumed by later tasks — this is the final task of Faza 4.

- [ ] **Step 1: Write the failing service test**

`frontend/src/app/features/audit-log/audit-log-entry.model.ts`:

```typescript
export type AuditResult = 'Allowed' | 'Blocked';

export interface AuditLogEntry {
  id: string;
  timestampUtc: string;
  userId: string;
  userEmail: string;
  action: string;
  objectDescription: string;
  result: AuditResult;
}
```

`frontend/src/app/features/audit-log/audit-log.service.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it } from 'vitest';
import { AuditLogService } from './audit-log.service';
import { environment } from '../../../environments/environment';

describe('AuditLogService', () => {
  it('requests audit log entries from the API', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(AuditLogService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.list().subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/audit-log`);
    req.flush([]);
    httpMock.verify();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `npx ng test`
Expected: FAIL — `AuditLogService` does not exist yet.

- [ ] **Step 3: Implement the service**

`frontend/src/app/features/audit-log/audit-log.service.ts`:

```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AuditLogEntry } from './audit-log-entry.model';

@Injectable({ providedIn: 'root' })
export class AuditLogService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/audit-log`;

  constructor(private readonly http: HttpClient) {}

  list() {
    return this.http.get<AuditLogEntry[]>(this.baseUrl);
  }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npx ng test`
Expected: PASS.

- [ ] **Step 5: Write the failing component test**

`frontend/src/app/features/audit-log/audit-log.component.spec.ts`:

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { AuditLogComponent } from './audit-log.component';
import { environment } from '../../../environments/environment';

describe('AuditLogComponent', () => {
  let fixture: ComponentFixture<AuditLogComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [AuditLogComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(AuditLogComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('renders audit log entries returned from the API', () => {
    fixture.detectChanges();
    const req = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/audit-log`);
    req.flush([{ id: '1', timestampUtc: '2026-09-22T10:00:00Z', userId: 'u1', userEmail: 'kat@example.org', action: 'ReadPastoralNotes', objectDescription: 'Jan Kowalski', result: 'Blocked' }]);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('kat@example.org');
    expect(text).toContain('Blocked');
  });
});
```

- [ ] **Step 6: Run test to verify it fails, then implement the component**

Run: `npx ng test`
Expected: FAIL — `AuditLogComponent` does not exist yet.

`frontend/src/app/features/audit-log/audit-log.component.ts`:

```typescript
import { Component, OnInit, signal } from '@angular/core';
import { AuditLogService } from './audit-log.service';
import { AuditLogEntry } from './audit-log-entry.model';

@Component({
  selector: 'app-audit-log',
  standalone: true,
  templateUrl: './audit-log.component.html',
  styleUrl: './audit-log.component.scss'
})
export class AuditLogComponent implements OnInit {
  readonly entries = signal<AuditLogEntry[]>([]);

  constructor(private readonly auditLogService: AuditLogService) {}

  ngOnInit(): void {
    this.auditLogService.list().subscribe(entries => this.entries.set(entries));
  }

  exportCsv(): void {
    const header = 'Data,Uzytkownik,Akcja,Obiekt,Wynik';
    const rows = this.entries().map(e => [e.timestampUtc, e.userEmail, e.action, e.objectDescription, e.result].join(','));
    const csv = [header, ...rows].join('\n');
    const blob = new Blob([csv], { type: 'text/csv' });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'audit-log.csv';
    link.click();
    window.URL.revokeObjectURL(url);
  }
}
```

`frontend/src/app/features/audit-log/audit-log.component.html`:

```html
<div class="page-heading">
  <div><h2>Audit log</h2><p>Dostęp do notatek duszpasterskich i zmiany ról użytkowników.</p></div>
  <button class="btn ghost" (click)="exportCsv()">Eksportuj CSV</button>
</div>

<div class="card">
  <div class="table-wrap">
    <table>
      <thead><tr><th>Data</th><th>Użytkownik</th><th>Akcja</th><th>Obiekt</th><th>Wynik</th></tr></thead>
      <tbody>
        @for (entry of entries(); track entry.id) {
          <tr>
            <td>{{ entry.timestampUtc | date: 'yyyy-MM-dd HH:mm' }}</td>
            <td>{{ entry.userEmail }}</td>
            <td>{{ entry.action }}</td>
            <td>{{ entry.objectDescription }}</td>
            <td>
              @if (entry.result === 'Blocked') {
                <span class="pill red">Zablokowano</span>
              } @else {
                <span class="pill green">Dozwolono</span>
              }
            </td>
          </tr>
        } @empty {
          <tr><td colspan="5" class="empty">Brak wpisów.</td></tr>
        }
      </tbody>
    </table>
  </div>
</div>
```

`frontend/src/app/features/audit-log/audit-log.component.scss`: leave empty.

- [ ] **Step 7: Run test to verify it passes**

Run: `npx ng test`
Expected: PASS.

- [ ] **Step 8: Wire the route and nav item**

In `frontend/src/app/app.routes.ts`, add after the `admin/users` route:

```typescript
      {
        path: 'audit-log',
        loadComponent: () => import('./features/audit-log/audit-log.component').then(m => m.AuditLogComponent)
      }
```

(Note: this is now the last entry in `children`, so it must NOT have a trailing comma — move the comma from the previous `admin/users` entry onto this one instead.)

In `frontend/src/app/layout/nav-items.ts`, add after the `Użytkownicy i role` entry:

```typescript
  { label: 'Audit log', icon: '⛨', path: '/audit-log', roles: ['Administrator'] }
```

(Note: this is now the last entry in the array — move the trailing comma accordingly.)

- [ ] **Step 9: Build and verify**

Run: `npm run build`
Expected: builds successfully.

- [ ] **Step 10: Full-suite verification**

Run: `dotnet test backend/DokPortal.sln`
Run: `npx ng test`
Expected: both green — this closes out Faza 4 and the full originally-agreed scope.

- [ ] **Step 11: Commit**

```bash
git add frontend
git commit -m "Add Audit log page with client-side CSV export"
git push origin master
```

---

## Self-Review Notes

**Spec coverage:** All four Faza 4 tools from the spec are covered — document generator (Task 3), mailing (Task 4), nameday calendar (Task 1 fields + Task 2 endpoint), audit log (Task 5) — plus their frontend pages (Tasks 6-9). The two named audit-log call sites (`PastoralNotesController.GetAll`, `UsersController.AssignRoles`) are both wired in Task 5, with a test proving the RODO-blocked scenario surfaces as a real log entry, matching the spec's core motivation. Task 6 additionally closes a gap the spec's Frontend section didn't call out explicitly: without input fields on the People form, nobody could ever set a person's nameday, so the calendar would always be empty — Task 6 Step 8 adds those fields.

**Placeholder scan:** No TBD/TODO markers; every step has runnable code, not a description of code.

**Type consistency:** `DocumentTemplate`/`MailingGroup`/`CampaignStatus`/`AuditResult` enum names and `GeneratedDocumentDto`/`MailingCampaignDto`/`AuditLogEntryDto`/`UpcomingNameDayDto` property names are defined once in Task 1-5 and reused verbatim in every later task and every frontend model (`generated-document.model.ts`, `mailing-campaign.model.ts`, `audit-log-entry.model.ts`, `name-day.model.ts`) that mirrors them.
