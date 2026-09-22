# DOK Portal Light — Faza 3: Moduł DOK — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the DOK module: podopieczni on 5 formation paths with flexible required-document checklists, pastorally-private notes with real RODO-style server-side filtering, a meetings list, a shared Supervision module (SKŚP + DOK), an Absolwenci view, and a DOK budget page reusing Faza 2's budget ledger untouched.

**Architecture:** Same Domain→Application→Infrastructure→Api layering and `[Authorize(Roles=...)]` pattern as Faza 1-2. New entities FK to existing `Person`; enums (`DokPath`, `DokStage`, `Institution`) serialize as JSON strings via the global `JsonStringEnumConverter` registered in Faza 2 Task 6 — no manual `.ToString()` needed this time. `PastoralNotesController`'s `GET` is the one endpoint in this phase with real per-request authorization logic beyond a role check.

**Tech Stack:** Same as Phases 1-2 — .NET 8, EF Core 8, ASP.NET Core Identity/JWT, xUnit; Angular (standalone, signals), Vitest.

**Spec:** [docs/superpowers/specs/2026-09-22-dok-module-design.md](../specs/2026-09-22-dok-module-design.md)

## Global Constraints

- All new entities FK to the existing `Person` (Phase 1); `DokCase` has three separate FKs to `Person` (`PersonId`, `CatechistPersonId`, `MentorPersonId`) and each needs its own explicit `HasOne(...).WithMany().HasForeignKey(...).OnDelete(DeleteBehavior.Restrict)` — EF Core cannot infer which is which otherwise.
- `Path`/`Stage`/`Institution` are exposed as actual enum-typed DTO properties (not manually `.ToString()`'d) — the global `JsonStringEnumConverter` from Faza 2 Task 6 already handles string serialization both ways.
- `PastoralNotesController`'s `GET` filters server-side: a note is only returned to its `AuthorUserId` or to a caller with `Administrator`/`DyrektorDOK`. This is the phase's one real RODO enforcement point; everything else in this phase uses ordinary role-based write gates.
- Harmonogram is a flat list (`GET /api/meetings`), never a calendar grid.
- Budżet DOK adds **zero** backend code — it's the existing `BudgetController`/`IBudgetService` from Faza 2 called with `Fund = DOK`.
- Every task ends with a green `dotnet test DokPortal.sln` (backend) or `npx ng test` (frontend) and leaves the app buildable.

---

## Task 1: Domain entities, enums, DbContext wiring, migration

**Files:**
- Create: `backend/src/DokPortal.Domain/Enums/DokPath.cs`
- Create: `backend/src/DokPortal.Domain/Enums/DokStage.cs`
- Create: `backend/src/DokPortal.Domain/Enums/Institution.cs`
- Create: `backend/src/DokPortal.Domain/Entities/DokCase.cs`
- Create: `backend/src/DokPortal.Domain/Entities/CaseDocument.cs`
- Create: `backend/src/DokPortal.Domain/Entities/PastoralNote.cs`
- Create: `backend/src/DokPortal.Domain/Entities/Meeting.cs`
- Create: `backend/src/DokPortal.Domain/Entities/Supervision.cs`
- Modify: `backend/src/DokPortal.Infrastructure/Persistence/AppDbContext.cs`
- Test: `backend/tests/DokPortal.Api.IntegrationTests/DokEntitiesPersistenceTests.cs`

**Interfaces:**
- Consumes: `Person` (Phase 1), `CustomWebApplicationFactory` (Phase 1).
- Produces: the five entities/three enums; `AppDbContext.DokCases/CaseDocuments/PastoralNotes/Meetings/Supervisions` — consumed by every later task.

- [ ] **Step 1: Write the failing test**

`backend/tests/DokPortal.Api.IntegrationTests/DokEntitiesPersistenceTests.cs`:

```csharp
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class DokEntitiesPersistenceTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DokEntitiesPersistenceTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task SavedDokEntities_CanBeReadBackInANewScope()
    {
        Guid caseId, documentId, noteId, meetingId, supervisionId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var person = new Person
            {
                Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski",
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            };
            var catechist = new Person
            {
                Id = Guid.NewGuid(), FirstName = "Anna", LastName = "Maj",
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            };
            db.People.AddRange(person, catechist);

            var dokCase = new DokCase
            {
                Id = Guid.NewGuid(), PersonId = person.Id, Path = DokPath.Confirmation, Stage = DokStage.Formation,
                CatechistPersonId = catechist.Id, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            };
            db.DokCases.Add(dokCase);

            var document = new CaseDocument { Id = Guid.NewGuid(), DokCaseId = dokCase.Id, Name = "Metryka chrztu", CreatedAtUtc = DateTime.UtcNow };
            var note = new PastoralNote { Id = Guid.NewGuid(), DokCaseId = dokCase.Id, AuthorUserId = "user-1", Content = "Notatka testowa", CreatedAtUtc = DateTime.UtcNow };
            var meeting = new Meeting { Id = Guid.NewGuid(), DokCaseId = dokCase.Id, MeetingDate = DateOnly.FromDateTime(DateTime.UtcNow), CreatedAtUtc = DateTime.UtcNow };
            var supervision = new Supervision
            {
                Id = Guid.NewGuid(), Institution = Institution.DOK, GroupLabel = "Grupa A",
                SupervisionDate = DateOnly.FromDateTime(DateTime.UtcNow), CreatedAtUtc = DateTime.UtcNow
            };

            db.CaseDocuments.Add(document);
            db.PastoralNotes.Add(note);
            db.Meetings.Add(meeting);
            db.Supervisions.Add(supervision);
            await db.SaveChangesAsync();

            caseId = dokCase.Id;
            documentId = document.Id;
            noteId = note.Id;
            meetingId = meeting.Id;
            supervisionId = supervision.Id;
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.NotNull(await db.DokCases.FindAsync(caseId));
            Assert.NotNull(await db.CaseDocuments.FindAsync(documentId));
            Assert.NotNull(await db.PastoralNotes.FindAsync(noteId));
            Assert.NotNull(await db.Meetings.FindAsync(meetingId));
            Assert.NotNull(await db.Supervisions.FindAsync(supervisionId));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/DokPortal.Api.IntegrationTests`
Expected: FAIL to compile — none of the new types exist yet.

- [ ] **Step 3: Implement the enums**

`backend/src/DokPortal.Domain/Enums/DokPath.cs`:

```csharp
namespace DokPortal.Domain.Enums;

public enum DokPath
{
    BaptismCandidate,
    Confirmation,
    Communion,
    Conversion,
    ReturnToUnity
}
```

`backend/src/DokPortal.Domain/Enums/DokStage.cs`:

```csharp
namespace DokPortal.Domain.Enums;

public enum DokStage
{
    Application,
    Formation,
    Sacrament,
    Graduate
}
```

`backend/src/DokPortal.Domain/Enums/Institution.cs`:

```csharp
namespace DokPortal.Domain.Enums;

public enum Institution
{
    SKSP,
    DOK
}
```

- [ ] **Step 4: Implement the entities**

`backend/src/DokPortal.Domain/Entities/DokCase.cs`:

```csharp
using DokPortal.Domain.Enums;

namespace DokPortal.Domain.Entities;

public class DokCase
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public Person? Person { get; set; }
    public DokPath Path { get; set; }
    public DokStage Stage { get; set; }
    public Guid CatechistPersonId { get; set; }
    public Person? CatechistPerson { get; set; }
    public Guid? MentorPersonId { get; set; }
    public Person? MentorPerson { get; set; }
    public DateOnly? LastMeetingDate { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
```

`backend/src/DokPortal.Domain/Entities/CaseDocument.cs`:

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
}
```

`backend/src/DokPortal.Domain/Entities/PastoralNote.cs`:

```csharp
namespace DokPortal.Domain.Entities;

public class PastoralNote
{
    public Guid Id { get; set; }
    public Guid DokCaseId { get; set; }
    public DokCase? DokCase { get; set; }
    public required string AuthorUserId { get; set; }
    public required string Content { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
```

`backend/src/DokPortal.Domain/Entities/Meeting.cs`:

```csharp
namespace DokPortal.Domain.Entities;

public class Meeting
{
    public Guid Id { get; set; }
    public Guid? DokCaseId { get; set; }
    public DokCase? DokCase { get; set; }
    public string? GroupLabel { get; set; }
    public DateOnly MeetingDate { get; set; }
    public bool? IsAttended { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
```

`backend/src/DokPortal.Domain/Entities/Supervision.cs`:

```csharp
using DokPortal.Domain.Enums;

namespace DokPortal.Domain.Entities;

public class Supervision
{
    public Guid Id { get; set; }
    public Institution Institution { get; set; }
    public required string GroupLabel { get; set; }
    public DateOnly SupervisionDate { get; set; }
    public int? AttendeesCount { get; set; }
    public int? ExpectedCount { get; set; }
    public string? Topic { get; set; }
    public string? Conclusion { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
```

- [ ] **Step 5: Wire the entities into AppDbContext**

In `backend/src/DokPortal.Infrastructure/Persistence/AppDbContext.cs`, add after the existing SKŚP `DbSet` lines:

```csharp
public DbSet<DokCase> DokCases => Set<DokCase>();
public DbSet<CaseDocument> CaseDocuments => Set<CaseDocument>();
public DbSet<PastoralNote> PastoralNotes => Set<PastoralNote>();
public DbSet<Meeting> Meetings => Set<Meeting>();
public DbSet<Supervision> Supervisions => Set<Supervision>();
```

Add to `OnModelCreating`, after the existing SKŚP entity configuration blocks:

```csharp
builder.Entity<DokCase>(entity =>
{
    entity.HasOne(c => c.Person).WithMany().HasForeignKey(c => c.PersonId).OnDelete(DeleteBehavior.Restrict);
    entity.HasOne(c => c.CatechistPerson).WithMany().HasForeignKey(c => c.CatechistPersonId).OnDelete(DeleteBehavior.Restrict);
    entity.HasOne(c => c.MentorPerson).WithMany().HasForeignKey(c => c.MentorPersonId).OnDelete(DeleteBehavior.Restrict);
});

builder.Entity<CaseDocument>(entity =>
{
    entity.Property(d => d.Name).IsRequired().HasMaxLength(200);
    entity.HasOne(d => d.DokCase).WithMany().HasForeignKey(d => d.DokCaseId).OnDelete(DeleteBehavior.Cascade);
});

builder.Entity<PastoralNote>(entity =>
{
    entity.Property(n => n.Content).IsRequired();
    entity.HasOne(n => n.DokCase).WithMany().HasForeignKey(n => n.DokCaseId).OnDelete(DeleteBehavior.Cascade);
});

builder.Entity<Meeting>(entity =>
{
    entity.Property(m => m.GroupLabel).HasMaxLength(200);
    entity.HasOne(m => m.DokCase).WithMany().HasForeignKey(m => m.DokCaseId).OnDelete(DeleteBehavior.SetNull);
});

builder.Entity<Supervision>(entity =>
{
    entity.Property(s => s.GroupLabel).IsRequired().HasMaxLength(200);
});
```

- [ ] **Step 6: Generate the EF Core migration**

```bash
cd backend
dotnet ef migrations add AddDokModule --project src/DokPortal.Infrastructure --startup-project src/DokPortal.Api
```

- [ ] **Step 7: Run test to verify it passes**

Run: `dotnet test backend/tests/DokPortal.Api.IntegrationTests`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add backend
git commit -m "Add DOK domain entities, enums, and EF Core migration"
```

---

## Task 2: DokCases module

**Files:**
- Create: `backend/src/DokPortal.Application/DokCases/DokCaseDto.cs`
- Create: `backend/src/DokPortal.Application/DokCases/CreateDokCaseRequest.cs`
- Create: `backend/src/DokPortal.Application/DokCases/UpdateDokCaseRequest.cs`
- Create: `backend/src/DokPortal.Application/DokCases/IDokCaseService.cs`
- Create: `backend/src/DokPortal.Infrastructure/Services/DokCaseService.cs`
- Create: `backend/src/DokPortal.Api/Controllers/DokCasesController.cs`
- Modify: `backend/src/DokPortal.Api/Program.cs`
- Modify: `backend/tests/DokPortal.Api.IntegrationTests/IntegrationTestBase.cs` (adds the shared `EnumJsonOptions` helper — see deviation note after Step 6)
- Modify: `backend/tests/DokPortal.Api.IntegrationTests/BudgetControllerTests.cs` (drops its now-redundant local copy in favor of the shared one)
- Test: `backend/tests/DokPortal.Infrastructure.Tests/Services/DokCaseServiceTests.cs`
- Test: `backend/tests/DokPortal.Api.IntegrationTests/DokCasesControllerTests.cs`

**Interfaces:**
- Consumes: `DokCase`, `DokPath`, `DokStage`, `Person` (Task 1), `PagedResult<T>` (Phase 1).
- Produces: `IDokCaseService` (`SearchAsync`, `GetByIdAsync`, `CreateAsync`, `UpdateAsync`); `GET/POST /api/dok-cases`, `GET/PUT /api/dok-cases/{id}`. `UpdateAsync` stamps `CompletedAtUtc = DateTime.UtcNow` the moment `Stage` transitions to `Graduate` (and leaves it untouched on any other transition, including already-`Graduate`).

- [ ] **Step 1: Write the failing tests**

`backend/tests/DokPortal.Infrastructure.Tests/Services/DokCaseServiceTests.cs`:

```csharp
using DokPortal.Application.DokCases;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class DokCaseServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    private static async Task<(Guid personId, Guid catechistId)> SeedPeopleAsync(AppDbContext db)
    {
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var catechist = new Person { Id = Guid.NewGuid(), FirstName = "Anna", LastName = "Maj", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.AddRange(person, catechist);
        await db.SaveChangesAsync();
        return (person.Id, catechist.Id);
    }

    [Fact]
    public async Task CreateAsync_ThenSearchAsync_FiltersByPath()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var (personId, catechistId) = await SeedPeopleAsync(db);
        var service = new DokCaseService(db);

        await service.CreateAsync(new CreateDokCaseRequest
        {
            PersonId = personId, Path = DokPath.Confirmation, Stage = DokStage.Formation, CatechistPersonId = catechistId
        }, default);

        var confirmation = await service.SearchAsync(DokPath.Confirmation, 1, 20, default);
        var conversion = await service.SearchAsync(DokPath.Conversion, 1, 20, default);

        Assert.Single(confirmation.Items);
        Assert.Equal("Jan Kowalski", confirmation.Items[0].PersonFullName);
        Assert.Empty(conversion.Items);
    }

    [Fact]
    public async Task UpdateAsync_TransitioningToGraduate_StampsCompletedAtUtc()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var (personId, catechistId) = await SeedPeopleAsync(db);
        var service = new DokCaseService(db);
        var created = await service.CreateAsync(new CreateDokCaseRequest
        {
            PersonId = personId, Path = DokPath.Confirmation, Stage = DokStage.Sacrament, CatechistPersonId = catechistId
        }, default);

        var updated = await service.UpdateAsync(created.Id, new UpdateDokCaseRequest
        {
            PersonId = personId, Path = DokPath.Confirmation, Stage = DokStage.Graduate, CatechistPersonId = catechistId
        }, default);

        Assert.NotNull(updated!.CompletedAtUtc);
    }
}
```

`backend/tests/DokPortal.Api.IntegrationTests/DokCasesControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Common;
using DokPortal.Application.DokCases;
using DokPortal.Application.People;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class DokCasesControllerTests : IntegrationTestBase
{
    public DokCasesControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private async Task<Guid> CreatePersonAsync(HttpClient client, string firstName, string lastName)
    {
        var response = await client.PostAsJsonAsync("/api/people", new { FirstName = firstName, LastName = lastName });
        response.EnsureSuccessStatusCode();
        var person = await response.Content.ReadFromJsonAsync<PersonDto>();
        return person!.Id;
    }

    [Fact]
    public async Task Create_ThenGetById_ReturnsCreatedCase()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var personId = await CreatePersonAsync(admin, "Jan", "Kowalski");
        var catechistId = await CreatePersonAsync(admin, "Anna", "Maj");

        var createResponse = await admin.PostAsJsonAsync("/api/dok-cases", new
        {
            PersonId = personId, Path = "Confirmation", Stage = "Formation", CatechistPersonId = catechistId
        });

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<DokCaseDto>(EnumJsonOptions);
        Assert.NotNull(created);
        Assert.Equal("Jan Kowalski", created!.PersonFullName);

        var getResponse = await admin.GetAsync($"/api/dok-cases/{created.Id}");
        getResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Search_ByPath_ReturnsOnlyMatchingCases()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var personId = await CreatePersonAsync(admin, "Karolina", "Szymanska");
        var catechistId = await CreatePersonAsync(admin, "Marek", "Zielinski");
        await admin.PostAsJsonAsync("/api/dok-cases", new { PersonId = personId, Path = "BaptismCandidate", Stage = "Application", CatechistPersonId = catechistId });

        var response = await admin.GetAsync("/api/dok-cases?path=BaptismCandidate");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<DokCaseDto>>(EnumJsonOptions);
        Assert.NotNull(result);
        Assert.Contains(result!.Items, c => c.PersonFullName == "Karolina Szymanska");
    }

    [Fact]
    public async Task Create_WithoutRequiredRole_ReturnsForbidden()
    {
        var client = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");

        var response = await client.PostAsJsonAsync("/api/dok-cases", new
        {
            PersonId = Guid.NewGuid(), Path = "Confirmation", Stage = "Application", CatechistPersonId = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/tests/DokPortal.Infrastructure.Tests backend/tests/DokPortal.Api.IntegrationTests`
Expected: FAIL to compile — `IDokCaseService`, `DokCaseService`, `DokCasesController` don't exist yet.

- [ ] **Step 3: Implement Application contracts**

`backend/src/DokPortal.Application/DokCases/DokCaseDto.cs`:

```csharp
using DokPortal.Domain.Enums;

namespace DokPortal.Application.DokCases;

public class DokCaseDto
{
    public required Guid Id { get; init; }
    public required Guid PersonId { get; init; }
    public required string PersonFullName { get; init; }
    public string? ParishName { get; init; }
    public required DokPath Path { get; init; }
    public required DokStage Stage { get; init; }
    public required Guid CatechistPersonId { get; init; }
    public required string CatechistFullName { get; init; }
    public Guid? MentorPersonId { get; init; }
    public string? MentorFullName { get; init; }
    public DateOnly? LastMeetingDate { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
}
```

`backend/src/DokPortal.Application/DokCases/CreateDokCaseRequest.cs`:

```csharp
using DokPortal.Domain.Enums;

namespace DokPortal.Application.DokCases;

public class CreateDokCaseRequest
{
    public required Guid PersonId { get; init; }
    public required DokPath Path { get; init; }
    public required DokStage Stage { get; init; }
    public required Guid CatechistPersonId { get; init; }
    public Guid? MentorPersonId { get; init; }
}
```

`backend/src/DokPortal.Application/DokCases/UpdateDokCaseRequest.cs`:

```csharp
namespace DokPortal.Application.DokCases;

public class UpdateDokCaseRequest : CreateDokCaseRequest
{
}
```

`backend/src/DokPortal.Application/DokCases/IDokCaseService.cs`:

```csharp
using DokPortal.Application.Common;
using DokPortal.Domain.Enums;

namespace DokPortal.Application.DokCases;

public interface IDokCaseService
{
    Task<PagedResult<DokCaseDto>> SearchAsync(DokPath? path, int page, int pageSize, CancellationToken ct);
    Task<DokCaseDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<DokCaseDto> CreateAsync(CreateDokCaseRequest request, CancellationToken ct);
    Task<DokCaseDto?> UpdateAsync(Guid id, UpdateDokCaseRequest request, CancellationToken ct);
}
```

- [ ] **Step 4: Implement DokCaseService**

`backend/src/DokPortal.Infrastructure/Services/DokCaseService.cs`:

```csharp
using DokPortal.Application.Common;
using DokPortal.Application.DokCases;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class DokCaseService : IDokCaseService
{
    private readonly AppDbContext _db;

    public DokCaseService(AppDbContext db) => _db = db;

    public async Task<PagedResult<DokCaseDto>> SearchAsync(DokPath? path, int page, int pageSize, CancellationToken ct)
    {
        var q = _db.DokCases
            .Include(c => c.Person).ThenInclude(p => p!.Parish)
            .Include(c => c.CatechistPerson)
            .Include(c => c.MentorPerson)
            .AsNoTracking().AsQueryable();

        if (path.HasValue)
        {
            q = q.Where(c => c.Path == path.Value);
        }

        var total = await q.CountAsync(ct);
        var entities = await q
            .OrderBy(c => c.Person!.LastName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<DokCaseDto>
        {
            Items = entities.Select(ToDto).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<DokCaseDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var dokCase = await _db.DokCases
            .Include(c => c.Person).ThenInclude(p => p!.Parish)
            .Include(c => c.CatechistPerson)
            .Include(c => c.MentorPerson)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        return dokCase is null ? null : ToDto(dokCase);
    }

    public async Task<DokCaseDto> CreateAsync(CreateDokCaseRequest request, CancellationToken ct)
    {
        var dokCase = new DokCase
        {
            Id = Guid.NewGuid(),
            PersonId = request.PersonId,
            Path = request.Path,
            Stage = request.Stage,
            CatechistPersonId = request.CatechistPersonId,
            MentorPersonId = request.MentorPersonId,
            CompletedAtUtc = request.Stage == DokStage.Graduate ? DateTime.UtcNow : null,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _db.DokCases.Add(dokCase);
        await _db.SaveChangesAsync(ct);
        return (await GetByIdAsync(dokCase.Id, ct))!;
    }

    public async Task<DokCaseDto?> UpdateAsync(Guid id, UpdateDokCaseRequest request, CancellationToken ct)
    {
        var dokCase = await _db.DokCases.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (dokCase is null) return null;

        var isNewlyGraduate = request.Stage == DokStage.Graduate && dokCase.Stage != DokStage.Graduate;

        dokCase.PersonId = request.PersonId;
        dokCase.Path = request.Path;
        dokCase.Stage = request.Stage;
        dokCase.CatechistPersonId = request.CatechistPersonId;
        dokCase.MentorPersonId = request.MentorPersonId;
        dokCase.UpdatedAtUtc = DateTime.UtcNow;
        if (isNewlyGraduate)
        {
            dokCase.CompletedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    private static DokCaseDto ToDto(DokCase c) => new()
    {
        Id = c.Id,
        PersonId = c.PersonId,
        PersonFullName = c.Person!.FullName,
        ParishName = c.Person.Parish?.Name,
        Path = c.Path,
        Stage = c.Stage,
        CatechistPersonId = c.CatechistPersonId,
        CatechistFullName = c.CatechistPerson!.FullName,
        MentorPersonId = c.MentorPersonId,
        MentorFullName = c.MentorPerson?.FullName,
        LastMeetingDate = c.LastMeetingDate,
        CompletedAtUtc = c.CompletedAtUtc
    };
}
```

- [ ] **Step 5: Implement DokCasesController**

`backend/src/DokPortal.Api/Controllers/DokCasesController.cs`:

```csharp
using DokPortal.Application.Common;
using DokPortal.Application.DokCases;
using DokPortal.Domain.Constants;
using DokPortal.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/dok-cases")]
[Authorize]
public class DokCasesController : ControllerBase
{
    private readonly IDokCaseService _dokCaseService;

    public DokCasesController(IDokCaseService dokCaseService) => _dokCaseService = dokCaseService;

    [HttpGet]
    public async Task<ActionResult<PagedResult<DokCaseDto>>> Search(
        [FromQuery] DokPath? path, [FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken ct = default)
        => Ok(await _dokCaseService.SearchAsync(path, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DokCaseDto>> GetById(Guid id, CancellationToken ct)
    {
        var dokCase = await _dokCaseService.GetByIdAsync(id, ct);
        return dokCase is null ? NotFound() : Ok(dokCase);
    }

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorDOK}")]
    public async Task<ActionResult<DokCaseDto>> Create(CreateDokCaseRequest request, CancellationToken ct)
    {
        var created = await _dokCaseService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorDOK}")]
    public async Task<ActionResult<DokCaseDto>> Update(Guid id, UpdateDokCaseRequest request, CancellationToken ct)
    {
        var updated = await _dokCaseService.UpdateAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }
}
```

- [ ] **Step 6: Register the service**

In `backend/src/DokPortal.Api/Program.cs`, add `using DokPortal.Application.DokCases;` and register after the SKŚP registrations:

```csharp
builder.Services.AddScoped<IDokCaseService, DokCaseService>();
```

**Deviation found during execution:** `DokCaseDto.Path`/`.Stage` are the first enum-typed DTO properties read back by an integration *test* (Faza 2's `BudgetControllerTests` had the same shape but worked around it locally) — `HttpContent.ReadFromJsonAsync<T>()`'s own default options don't include a `JsonStringEnumConverter` even though the *server* has one registered globally (Faza 2 Task 6), so deserializing the create/search responses failed with `JsonException: The JSON value could not be converted to DokPortal.Domain.Enums.DokPath`. Fix: added a shared `protected static readonly JsonSerializerOptions EnumJsonOptions` to `IntegrationTestBase` (built from `JsonSerializerDefaults.Web` + `JsonStringEnumConverter`, matching Faza 2's local pattern) and pass it to every `ReadFromJsonAsync` call whose target DTO has an enum property. `BudgetControllerTests` was simplified to reuse this shared field instead of its own local copy.

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test backend/DokPortal.sln`
Expected: All tests pass.

- [ ] **Step 8: Commit**

```bash
git add backend
git commit -m "Add DokCases module with automatic graduation timestamping"
```

---

## Task 3: CaseDocuments module

**Files:**
- Create: `backend/src/DokPortal.Application/CaseDocuments/CaseDocumentDto.cs`
- Create: `backend/src/DokPortal.Application/CaseDocuments/CreateCaseDocumentRequest.cs`
- Create: `backend/src/DokPortal.Application/CaseDocuments/SetCaseDocumentProvidedRequest.cs`
- Create: `backend/src/DokPortal.Application/CaseDocuments/ICaseDocumentService.cs`
- Create: `backend/src/DokPortal.Infrastructure/Services/CaseDocumentService.cs`
- Create: `backend/src/DokPortal.Api/Controllers/CaseDocumentsController.cs`
- Modify: `backend/src/DokPortal.Api/Program.cs`
- Test: `backend/tests/DokPortal.Infrastructure.Tests/Services/CaseDocumentServiceTests.cs`
- Test: `backend/tests/DokPortal.Api.IntegrationTests/CaseDocumentsControllerTests.cs`

**Interfaces:**
- Consumes: `CaseDocument`, `DokCase` (Task 1).
- Produces: `ICaseDocumentService` (`GetForCaseAsync`, `CreateAsync`, `SetProvidedAsync`); `GET/POST /api/dok-cases/{caseId}/documents`, `PUT /api/dok-cases/{caseId}/documents/{id}`.

- [ ] **Step 1: Write the failing tests**

`backend/tests/DokPortal.Infrastructure.Tests/Services/CaseDocumentServiceTests.cs`:

```csharp
using DokPortal.Application.CaseDocuments;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class CaseDocumentServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    private static async Task<Guid> SeedCaseAsync(AppDbContext db)
    {
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var catechist = new Person { Id = Guid.NewGuid(), FirstName = "Anna", LastName = "Maj", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.AddRange(person, catechist);
        var dokCase = new DokCase
        {
            Id = Guid.NewGuid(), PersonId = person.Id, Path = DokPath.Confirmation, Stage = DokStage.Formation,
            CatechistPersonId = catechist.Id, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        db.DokCases.Add(dokCase);
        await db.SaveChangesAsync();
        return dokCase.Id;
    }

    [Fact]
    public async Task CreateAsync_ThenSetProvidedAsync_TogglesStatus()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var caseId = await SeedCaseAsync(db);
        var service = new CaseDocumentService(db);

        var created = await service.CreateAsync(caseId, new CreateCaseDocumentRequest { Name = "Metryka chrztu" }, default);
        var updated = await service.SetProvidedAsync(caseId, created.Id, true, default);

        Assert.NotNull(updated);
        Assert.True(updated!.IsProvided);
    }

    [Fact]
    public async Task GetForCaseAsync_ReturnsOnlyDocumentsForThatCase()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var caseId = await SeedCaseAsync(db);
        var otherCaseId = await SeedCaseAsync(db);
        var service = new CaseDocumentService(db);
        await service.CreateAsync(caseId, new CreateCaseDocumentRequest { Name = "Metryka chrztu" }, default);
        await service.CreateAsync(otherCaseId, new CreateCaseDocumentRequest { Name = "Inny dokument" }, default);

        var documents = await service.GetForCaseAsync(caseId, default);

        Assert.Single(documents);
        Assert.Equal("Metryka chrztu", documents[0].Name);
    }
}
```

`backend/tests/DokPortal.Api.IntegrationTests/CaseDocumentsControllerTests.cs`:

```csharp
using System.Net.Http.Json;
using DokPortal.Application.CaseDocuments;
using DokPortal.Application.DokCases;
using DokPortal.Application.People;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class CaseDocumentsControllerTests : IntegrationTestBase
{
    public CaseDocumentsControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Create_ThenTogglesProvided_ReturnsUpdatedDocument()
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

        var createResponse = await admin.PostAsJsonAsync($"/api/dok-cases/{dokCase!.Id}/documents", new { Name = "Metryka chrztu" });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CaseDocumentDto>();

        var updateResponse = await admin.PutAsJsonAsync($"/api/dok-cases/{dokCase.Id}/documents/{created!.Id}", new { IsProvided = true });
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<CaseDocumentDto>();

        Assert.True(updated!.IsProvided);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/tests/DokPortal.Infrastructure.Tests backend/tests/DokPortal.Api.IntegrationTests`
Expected: FAIL to compile.

- [ ] **Step 3: Implement Application contracts**

`backend/src/DokPortal.Application/CaseDocuments/CaseDocumentDto.cs`:

```csharp
namespace DokPortal.Application.CaseDocuments;

public class CaseDocumentDto
{
    public required Guid Id { get; init; }
    public required Guid DokCaseId { get; init; }
    public required string Name { get; init; }
    public required bool IsProvided { get; init; }
}
```

`backend/src/DokPortal.Application/CaseDocuments/CreateCaseDocumentRequest.cs`:

```csharp
namespace DokPortal.Application.CaseDocuments;

public class CreateCaseDocumentRequest
{
    public required string Name { get; init; }
}
```

`backend/src/DokPortal.Application/CaseDocuments/SetCaseDocumentProvidedRequest.cs`:

```csharp
namespace DokPortal.Application.CaseDocuments;

public class SetCaseDocumentProvidedRequest
{
    public required bool IsProvided { get; init; }
}
```

`backend/src/DokPortal.Application/CaseDocuments/ICaseDocumentService.cs`:

```csharp
namespace DokPortal.Application.CaseDocuments;

public interface ICaseDocumentService
{
    Task<IReadOnlyList<CaseDocumentDto>> GetForCaseAsync(Guid caseId, CancellationToken ct);
    Task<CaseDocumentDto> CreateAsync(Guid caseId, CreateCaseDocumentRequest request, CancellationToken ct);
    Task<CaseDocumentDto?> SetProvidedAsync(Guid caseId, Guid documentId, bool isProvided, CancellationToken ct);
}
```

- [ ] **Step 4: Implement CaseDocumentService**

`backend/src/DokPortal.Infrastructure/Services/CaseDocumentService.cs`:

```csharp
using DokPortal.Application.CaseDocuments;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class CaseDocumentService : ICaseDocumentService
{
    private readonly AppDbContext _db;

    public CaseDocumentService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<CaseDocumentDto>> GetForCaseAsync(Guid caseId, CancellationToken ct)
    {
        var documents = await _db.CaseDocuments.AsNoTracking()
            .Where(d => d.DokCaseId == caseId)
            .OrderBy(d => d.CreatedAtUtc)
            .ToListAsync(ct);
        return documents.Select(ToDto).ToList();
    }

    public async Task<CaseDocumentDto> CreateAsync(Guid caseId, CreateCaseDocumentRequest request, CancellationToken ct)
    {
        var document = new CaseDocument
        {
            Id = Guid.NewGuid(), DokCaseId = caseId, Name = request.Name, IsProvided = false, CreatedAtUtc = DateTime.UtcNow
        };
        _db.CaseDocuments.Add(document);
        await _db.SaveChangesAsync(ct);
        return ToDto(document);
    }

    public async Task<CaseDocumentDto?> SetProvidedAsync(Guid caseId, Guid documentId, bool isProvided, CancellationToken ct)
    {
        var document = await _db.CaseDocuments.FirstOrDefaultAsync(d => d.Id == documentId && d.DokCaseId == caseId, ct);
        if (document is null) return null;

        document.IsProvided = isProvided;
        await _db.SaveChangesAsync(ct);
        return ToDto(document);
    }

    private static CaseDocumentDto ToDto(CaseDocument d) => new()
    {
        Id = d.Id,
        DokCaseId = d.DokCaseId,
        Name = d.Name,
        IsProvided = d.IsProvided
    };
}
```

- [ ] **Step 5: Implement CaseDocumentsController**

`backend/src/DokPortal.Api/Controllers/CaseDocumentsController.cs`:

```csharp
using DokPortal.Application.CaseDocuments;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/dok-cases/{caseId:guid}/documents")]
[Authorize]
public class CaseDocumentsController : ControllerBase
{
    private readonly ICaseDocumentService _caseDocumentService;

    public CaseDocumentsController(ICaseDocumentService caseDocumentService) => _caseDocumentService = caseDocumentService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CaseDocumentDto>>> GetAll(Guid caseId, CancellationToken ct)
        => Ok(await _caseDocumentService.GetForCaseAsync(caseId, ct));

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorDOK},{AppRoles.KatechistaProwadzacy}")]
    public async Task<ActionResult<CaseDocumentDto>> Create(Guid caseId, CreateCaseDocumentRequest request, CancellationToken ct)
    {
        var created = await _caseDocumentService.CreateAsync(caseId, request, ct);
        return CreatedAtAction(nameof(GetAll), new { caseId }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorDOK},{AppRoles.KatechistaProwadzacy}")]
    public async Task<ActionResult<CaseDocumentDto>> SetProvided(Guid caseId, Guid id, SetCaseDocumentProvidedRequest request, CancellationToken ct)
    {
        var updated = await _caseDocumentService.SetProvidedAsync(caseId, id, request.IsProvided, ct);
        return updated is null ? NotFound() : Ok(updated);
    }
}
```

- [ ] **Step 6: Register the service**

In `backend/src/DokPortal.Api/Program.cs`, add `using DokPortal.Application.CaseDocuments;` and register:

```csharp
builder.Services.AddScoped<ICaseDocumentService, CaseDocumentService>();
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test backend/DokPortal.sln`
Expected: All tests pass.

- [ ] **Step 8: Commit**

```bash
git add backend
git commit -m "Add CaseDocuments module for per-case required-document checklists"
```

---

## Task 4: PastoralNotes module (RODO access filtering)

**Files:**
- Create: `backend/src/DokPortal.Application/PastoralNotes/PastoralNoteDto.cs`
- Create: `backend/src/DokPortal.Application/PastoralNotes/CreatePastoralNoteRequest.cs`
- Create: `backend/src/DokPortal.Application/PastoralNotes/IPastoralNoteService.cs`
- Create: `backend/src/DokPortal.Infrastructure/Services/PastoralNoteService.cs`
- Create: `backend/src/DokPortal.Api/Controllers/PastoralNotesController.cs`
- Modify: `backend/src/DokPortal.Api/Program.cs`
- Test: `backend/tests/DokPortal.Infrastructure.Tests/Services/PastoralNoteServiceTests.cs`
- Test: `backend/tests/DokPortal.Api.IntegrationTests/PastoralNotesControllerTests.cs`

**Interfaces:**
- Consumes: `PastoralNote`, `DokCase` (Task 1).
- Produces: `IPastoralNoteService.GetVisibleForCaseAsync(caseId, currentUserId, isPrivileged, ct)` — returns only notes authored by `currentUserId` unless `isPrivileged` is `true`, in which case all notes for the case are returned; `CreateAsync(caseId, authorUserId, request, ct)`. `GET/POST /api/dok-cases/{caseId}/notes`.

- [ ] **Step 1: Write the failing tests**

`backend/tests/DokPortal.Infrastructure.Tests/Services/PastoralNoteServiceTests.cs`:

```csharp
using DokPortal.Application.PastoralNotes;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class PastoralNoteServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    private static async Task<Guid> SeedCaseAsync(AppDbContext db)
    {
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var catechist = new Person { Id = Guid.NewGuid(), FirstName = "Anna", LastName = "Maj", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.AddRange(person, catechist);
        var dokCase = new DokCase
        {
            Id = Guid.NewGuid(), PersonId = person.Id, Path = DokPath.Confirmation, Stage = DokStage.Formation,
            CatechistPersonId = catechist.Id, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        db.DokCases.Add(dokCase);
        await db.SaveChangesAsync();
        return dokCase.Id;
    }

    [Fact]
    public async Task GetVisibleForCaseAsync_NonPrivilegedUser_SeesOnlyOwnNote()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var caseId = await SeedCaseAsync(db);
        var service = new PastoralNoteService(db);
        await service.CreateAsync(caseId, "author-a", new CreatePastoralNoteRequest { Content = "Notatka A" }, default);
        await service.CreateAsync(caseId, "author-b", new CreatePastoralNoteRequest { Content = "Notatka B" }, default);

        var visibleToA = await service.GetVisibleForCaseAsync(caseId, "author-a", isPrivileged: false, default);

        Assert.Single(visibleToA);
        Assert.Equal("Notatka A", visibleToA[0].Content);
    }

    [Fact]
    public async Task GetVisibleForCaseAsync_PrivilegedUser_SeesAllNotes()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var caseId = await SeedCaseAsync(db);
        var service = new PastoralNoteService(db);
        await service.CreateAsync(caseId, "author-a", new CreatePastoralNoteRequest { Content = "Notatka A" }, default);
        await service.CreateAsync(caseId, "author-b", new CreatePastoralNoteRequest { Content = "Notatka B" }, default);

        var visibleToDirector = await service.GetVisibleForCaseAsync(caseId, "director-user", isPrivileged: true, default);

        Assert.Equal(2, visibleToDirector.Count);
    }
}
```

`backend/tests/DokPortal.Api.IntegrationTests/PastoralNotesControllerTests.cs`:

```csharp
using System.Net.Http.Json;
using DokPortal.Application.DokCases;
using DokPortal.Application.PastoralNotes;
using DokPortal.Application.People;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class PastoralNotesControllerTests : IntegrationTestBase
{
    public PastoralNotesControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private async Task<Guid> CreateDokCaseAsync(HttpClient admin)
    {
        var personResponse = await admin.PostAsJsonAsync("/api/people", new { FirstName = "Jan", LastName = "Kowalski" });
        var person = await personResponse.Content.ReadFromJsonAsync<PersonDto>();
        var catechistResponse = await admin.PostAsJsonAsync("/api/people", new { FirstName = "Anna", LastName = "Maj" });
        var catechist = await catechistResponse.Content.ReadFromJsonAsync<PersonDto>();
        var caseResponse = await admin.PostAsJsonAsync("/api/dok-cases", new
        {
            PersonId = person!.Id, Path = "Confirmation", Stage = "Formation", CatechistPersonId = catechist!.Id
        });
        var dokCase = await caseResponse.Content.ReadFromJsonAsync<DokCaseDto>(EnumJsonOptions);
        return dokCase!.Id;
    }

    [Fact]
    public async Task Author_SeesOwnNote_ButNotAnotherKatechistasNote()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var caseId = await CreateDokCaseAsync(admin);

        var katechistaA = await CreateAuthenticatedClientAsync($"kat-a-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");
        var katechistaB = await CreateAuthenticatedClientAsync($"kat-b-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");

        await katechistaA.PostAsJsonAsync($"/api/dok-cases/{caseId}/notes", new { Content = "Notatka katechisty A" });

        var responseForA = await katechistaA.GetAsync($"/api/dok-cases/{caseId}/notes");
        var notesForA = await responseForA.Content.ReadFromJsonAsync<List<PastoralNoteDto>>();
        Assert.Single(notesForA!);

        var responseForB = await katechistaB.GetAsync($"/api/dok-cases/{caseId}/notes");
        var notesForB = await responseForB.Content.ReadFromJsonAsync<List<PastoralNoteDto>>();
        Assert.Empty(notesForB!);
    }

    [Fact]
    public async Task DyrektorDOK_SeesAllNotesForCase()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var caseId = await CreateDokCaseAsync(admin);

        var katechista = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");
        await katechista.PostAsJsonAsync($"/api/dok-cases/{caseId}/notes", new { Content = "Notatka poufna" });

        var director = await CreateAuthenticatedClientAsync($"dyr-{Guid.NewGuid():N}@example.org", "Sekret123!", "DyrektorDOK");
        var response = await director.GetAsync($"/api/dok-cases/{caseId}/notes");
        var notes = await response.Content.ReadFromJsonAsync<List<PastoralNoteDto>>();

        Assert.Single(notes!);
        Assert.Equal("Notatka poufna", notes![0].Content);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/tests/DokPortal.Infrastructure.Tests backend/tests/DokPortal.Api.IntegrationTests`
Expected: FAIL to compile.

- [ ] **Step 3: Implement Application contracts**

`backend/src/DokPortal.Application/PastoralNotes/PastoralNoteDto.cs`:

```csharp
namespace DokPortal.Application.PastoralNotes;

public class PastoralNoteDto
{
    public required Guid Id { get; init; }
    public required Guid DokCaseId { get; init; }
    public required string AuthorUserId { get; init; }
    public required string Content { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}
```

`backend/src/DokPortal.Application/PastoralNotes/CreatePastoralNoteRequest.cs`:

```csharp
namespace DokPortal.Application.PastoralNotes;

public class CreatePastoralNoteRequest
{
    public required string Content { get; init; }
}
```

`backend/src/DokPortal.Application/PastoralNotes/IPastoralNoteService.cs`:

```csharp
namespace DokPortal.Application.PastoralNotes;

public interface IPastoralNoteService
{
    Task<IReadOnlyList<PastoralNoteDto>> GetVisibleForCaseAsync(Guid caseId, string currentUserId, bool isPrivileged, CancellationToken ct);
    Task<PastoralNoteDto> CreateAsync(Guid caseId, string authorUserId, CreatePastoralNoteRequest request, CancellationToken ct);
}
```

- [ ] **Step 4: Implement PastoralNoteService**

`backend/src/DokPortal.Infrastructure/Services/PastoralNoteService.cs`:

```csharp
using DokPortal.Application.PastoralNotes;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class PastoralNoteService : IPastoralNoteService
{
    private readonly AppDbContext _db;

    public PastoralNoteService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<PastoralNoteDto>> GetVisibleForCaseAsync(Guid caseId, string currentUserId, bool isPrivileged, CancellationToken ct)
    {
        var q = _db.PastoralNotes.AsNoTracking().Where(n => n.DokCaseId == caseId);

        if (!isPrivileged)
        {
            q = q.Where(n => n.AuthorUserId == currentUserId);
        }

        var notes = await q.OrderByDescending(n => n.CreatedAtUtc).ToListAsync(ct);
        return notes.Select(ToDto).ToList();
    }

    public async Task<PastoralNoteDto> CreateAsync(Guid caseId, string authorUserId, CreatePastoralNoteRequest request, CancellationToken ct)
    {
        var note = new PastoralNote
        {
            Id = Guid.NewGuid(), DokCaseId = caseId, AuthorUserId = authorUserId, Content = request.Content, CreatedAtUtc = DateTime.UtcNow
        };
        _db.PastoralNotes.Add(note);
        await _db.SaveChangesAsync(ct);
        return ToDto(note);
    }

    private static PastoralNoteDto ToDto(PastoralNote n) => new()
    {
        Id = n.Id,
        DokCaseId = n.DokCaseId,
        AuthorUserId = n.AuthorUserId,
        Content = n.Content,
        CreatedAtUtc = n.CreatedAtUtc
    };
}
```

- [ ] **Step 5: Implement PastoralNotesController**

`backend/src/DokPortal.Api/Controllers/PastoralNotesController.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using DokPortal.Application.PastoralNotes;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/dok-cases/{caseId:guid}/notes")]
[Authorize]
public class PastoralNotesController : ControllerBase
{
    private readonly IPastoralNoteService _pastoralNoteService;

    public PastoralNotesController(IPastoralNoteService pastoralNoteService) => _pastoralNoteService = pastoralNoteService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PastoralNoteDto>>> GetAll(Guid caseId, CancellationToken ct)
    {
        var currentUserId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;
        var isPrivileged = User.IsInRole(AppRoles.Administrator) || User.IsInRole(AppRoles.DyrektorDOK);
        return Ok(await _pastoralNoteService.GetVisibleForCaseAsync(caseId, currentUserId, isPrivileged, ct));
    }

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorDOK},{AppRoles.KatechistaProwadzacy}")]
    public async Task<ActionResult<PastoralNoteDto>> Create(Guid caseId, CreatePastoralNoteRequest request, CancellationToken ct)
    {
        var currentUserId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;
        var created = await _pastoralNoteService.CreateAsync(caseId, currentUserId, request, ct);
        return CreatedAtAction(nameof(GetAll), new { caseId }, created);
    }
}
```

Note: `User.FindFirstValue` is the `System.Security.Claims.ClaimsPrincipal` extension method (already available via `ControllerBase.User`); add `using System.Security.Claims;` if your editor doesn't resolve it automatically via the ASP.NET Core shared framework's implicit usings.

- [ ] **Step 6: Register the service**

In `backend/src/DokPortal.Api/Program.cs`, add `using DokPortal.Application.PastoralNotes;` and register:

```csharp
builder.Services.AddScoped<IPastoralNoteService, PastoralNoteService>();
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test backend/DokPortal.sln`
Expected: All tests pass, including both RODO-filtering tests confirming katechista B cannot see katechista A's note, and DyrektorDOK sees everything.

- [ ] **Step 8: Commit**

```bash
git add backend
git commit -m "Add PastoralNotes module with real RODO-style server-side filtering"
```

---

## Task 5: Meetings module

**Files:**
- Create: `backend/src/DokPortal.Application/Meetings/MeetingDto.cs`
- Create: `backend/src/DokPortal.Application/Meetings/CreateMeetingRequest.cs`
- Create: `backend/src/DokPortal.Application/Meetings/IMeetingService.cs`
- Create: `backend/src/DokPortal.Infrastructure/Services/MeetingService.cs`
- Create: `backend/src/DokPortal.Api/Controllers/MeetingsController.cs`
- Modify: `backend/src/DokPortal.Api/Program.cs`
- Test: `backend/tests/DokPortal.Infrastructure.Tests/Services/MeetingServiceTests.cs`
- Test: `backend/tests/DokPortal.Api.IntegrationTests/MeetingsControllerTests.cs`

**Interfaces:**
- Consumes: `Meeting`, `DokCase` (Task 1).
- Produces: `IMeetingService` (`GetAllAsync`, `CreateAsync`); `GET/POST /api/meetings`.

- [ ] **Step 1: Write the failing tests**

`backend/tests/DokPortal.Infrastructure.Tests/Services/MeetingServiceTests.cs`:

```csharp
using DokPortal.Application.Meetings;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class MeetingServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task CreateAsync_GroupSession_ThenGetAllAsync_ReturnsGroupLabelWithoutCaseLabel()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var service = new MeetingService(db);

        await service.CreateAsync(new CreateMeetingRequest
        {
            GroupLabel = "DOK grupa", MeetingDate = DateOnly.FromDateTime(DateTime.UtcNow)
        }, default);

        var meetings = await service.GetAllAsync(default);

        Assert.Single(meetings);
        Assert.Equal("DOK grupa", meetings[0].GroupLabel);
        Assert.Null(meetings[0].CaseLabel);
    }

    [Fact]
    public async Task CreateAsync_IndividualMeeting_ReturnsCaseLabel()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var catechist = new Person { Id = Guid.NewGuid(), FirstName = "Anna", LastName = "Maj", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.AddRange(person, catechist);
        var dokCase = new DokCase
        {
            Id = Guid.NewGuid(), PersonId = person.Id, Path = DokPath.Confirmation, Stage = DokStage.Formation,
            CatechistPersonId = catechist.Id, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        db.DokCases.Add(dokCase);
        await db.SaveChangesAsync();
        var service = new MeetingService(db);

        await service.CreateAsync(new CreateMeetingRequest
        {
            DokCaseId = dokCase.Id, MeetingDate = DateOnly.FromDateTime(DateTime.UtcNow), IsAttended = true
        }, default);

        var meetings = await service.GetAllAsync(default);

        Assert.Single(meetings);
        Assert.Equal("Jan Kowalski", meetings[0].CaseLabel);
    }
}
```

`backend/tests/DokPortal.Api.IntegrationTests/MeetingsControllerTests.cs`:

```csharp
using System.Net.Http.Json;
using DokPortal.Application.Meetings;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class MeetingsControllerTests : IntegrationTestBase
{
    public MeetingsControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Create_ThenGetAll_ReturnsCreatedMeeting()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

        var createResponse = await admin.PostAsJsonAsync("/api/meetings", new
        {
            GroupLabel = "Grupa SKŚP II", MeetingDate = "2026-09-23"
        });
        createResponse.EnsureSuccessStatusCode();

        var getResponse = await admin.GetAsync("/api/meetings");
        getResponse.EnsureSuccessStatusCode();
        var meetings = await getResponse.Content.ReadFromJsonAsync<List<MeetingDto>>();
        Assert.Contains(meetings!, m => m.GroupLabel == "Grupa SKŚP II");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/tests/DokPortal.Infrastructure.Tests backend/tests/DokPortal.Api.IntegrationTests`
Expected: FAIL to compile.

- [ ] **Step 3: Implement Application contracts**

`backend/src/DokPortal.Application/Meetings/MeetingDto.cs`:

```csharp
namespace DokPortal.Application.Meetings;

public class MeetingDto
{
    public required Guid Id { get; init; }
    public Guid? DokCaseId { get; init; }
    public string? CaseLabel { get; init; }
    public string? GroupLabel { get; init; }
    public required DateOnly MeetingDate { get; init; }
    public bool? IsAttended { get; init; }
    public string? Notes { get; init; }
}
```

`backend/src/DokPortal.Application/Meetings/CreateMeetingRequest.cs`:

```csharp
namespace DokPortal.Application.Meetings;

public class CreateMeetingRequest
{
    public Guid? DokCaseId { get; init; }
    public string? GroupLabel { get; init; }
    public required DateOnly MeetingDate { get; init; }
    public bool? IsAttended { get; init; }
    public string? Notes { get; init; }
}
```

`backend/src/DokPortal.Application/Meetings/IMeetingService.cs`:

```csharp
namespace DokPortal.Application.Meetings;

public interface IMeetingService
{
    Task<IReadOnlyList<MeetingDto>> GetAllAsync(CancellationToken ct);
    Task<MeetingDto> CreateAsync(CreateMeetingRequest request, CancellationToken ct);
}
```

- [ ] **Step 4: Implement MeetingService**

`backend/src/DokPortal.Infrastructure/Services/MeetingService.cs`:

```csharp
using DokPortal.Application.Meetings;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class MeetingService : IMeetingService
{
    private readonly AppDbContext _db;

    public MeetingService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<MeetingDto>> GetAllAsync(CancellationToken ct)
    {
        var meetings = await _db.Meetings.Include(m => m.DokCase).ThenInclude(c => c!.Person).AsNoTracking()
            .OrderByDescending(m => m.MeetingDate)
            .ToListAsync(ct);
        return meetings.Select(ToDto).ToList();
    }

    public async Task<MeetingDto> CreateAsync(CreateMeetingRequest request, CancellationToken ct)
    {
        var meeting = new Meeting
        {
            Id = Guid.NewGuid(),
            DokCaseId = request.DokCaseId,
            GroupLabel = request.GroupLabel,
            MeetingDate = request.MeetingDate,
            IsAttended = request.IsAttended,
            Notes = request.Notes,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync(ct);

        var saved = await _db.Meetings.Include(m => m.DokCase).ThenInclude(c => c!.Person).AsNoTracking()
            .FirstAsync(m => m.Id == meeting.Id, ct);
        return ToDto(saved);
    }

    private static MeetingDto ToDto(Meeting m) => new()
    {
        Id = m.Id,
        DokCaseId = m.DokCaseId,
        CaseLabel = m.DokCase?.Person?.FullName,
        GroupLabel = m.GroupLabel,
        MeetingDate = m.MeetingDate,
        IsAttended = m.IsAttended,
        Notes = m.Notes
    };
}
```

- [ ] **Step 5: Implement MeetingsController**

`backend/src/DokPortal.Api/Controllers/MeetingsController.cs`:

```csharp
using DokPortal.Application.Meetings;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/meetings")]
[Authorize]
public class MeetingsController : ControllerBase
{
    private readonly IMeetingService _meetingService;

    public MeetingsController(IMeetingService meetingService) => _meetingService = meetingService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MeetingDto>>> GetAll(CancellationToken ct)
        => Ok(await _meetingService.GetAllAsync(ct));

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorDOK},{AppRoles.KatechistaProwadzacy}")]
    public async Task<ActionResult<MeetingDto>> Create(CreateMeetingRequest request, CancellationToken ct)
    {
        var created = await _meetingService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), created);
    }
}
```

- [ ] **Step 6: Register the service**

In `backend/src/DokPortal.Api/Program.cs`, add `using DokPortal.Application.Meetings;` and register:

```csharp
builder.Services.AddScoped<IMeetingService, MeetingService>();
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test backend/DokPortal.sln`
Expected: All tests pass.

- [ ] **Step 8: Commit**

```bash
git add backend
git commit -m "Add Meetings module (harmonogram as a list)"
```

---

## Task 6: Supervisions module

**Files:**
- Create: `backend/src/DokPortal.Application/Supervisions/SupervisionDto.cs`
- Create: `backend/src/DokPortal.Application/Supervisions/CreateSupervisionRequest.cs`
- Create: `backend/src/DokPortal.Application/Supervisions/ISupervisionService.cs`
- Create: `backend/src/DokPortal.Infrastructure/Services/SupervisionService.cs`
- Create: `backend/src/DokPortal.Api/Controllers/SupervisionsController.cs`
- Modify: `backend/src/DokPortal.Api/Program.cs`
- Test: `backend/tests/DokPortal.Infrastructure.Tests/Services/SupervisionServiceTests.cs`
- Test: `backend/tests/DokPortal.Api.IntegrationTests/SupervisionsControllerTests.cs`

**Interfaces:**
- Consumes: `Supervision`, `Institution` (Task 1).
- Produces: `ISupervisionService` (`GetAllAsync(Institution? filter, ct)`, `CreateAsync`); `GET /api/supervisions?institution=`, `POST /api/supervisions`. This closes out the backend half of Faza 3 — Budżet DOK needs no new backend code (reuses Faza 2's `BudgetController` with `Fund=DOK`).

- [ ] **Step 1: Write the failing tests**

`backend/tests/DokPortal.Infrastructure.Tests/Services/SupervisionServiceTests.cs`:

```csharp
using DokPortal.Application.Supervisions;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class SupervisionServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task CreateAsync_ThenGetAllAsync_FiltersByInstitution()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var service = new SupervisionService(db);

        await service.CreateAsync(new CreateSupervisionRequest
        {
            Institution = Institution.DOK, GroupLabel = "Grupa A", SupervisionDate = DateOnly.FromDateTime(DateTime.UtcNow)
        }, default);
        await service.CreateAsync(new CreateSupervisionRequest
        {
            Institution = Institution.SKSP, GroupLabel = "Grupa B", SupervisionDate = DateOnly.FromDateTime(DateTime.UtcNow)
        }, default);

        var dokOnly = await service.GetAllAsync(Institution.DOK, default);

        Assert.Single(dokOnly);
        Assert.Equal("Grupa A", dokOnly[0].GroupLabel);
    }
}
```

`backend/tests/DokPortal.Api.IntegrationTests/SupervisionsControllerTests.cs`:

```csharp
using System.Net.Http.Json;
using DokPortal.Application.Supervisions;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class SupervisionsControllerTests : IntegrationTestBase
{
    public SupervisionsControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Create_ThenGetByInstitution_ReturnsCreatedSupervision()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

        var createResponse = await admin.PostAsJsonAsync("/api/supervisions", new
        {
            Institution = "DOK", GroupLabel = "Grupa A", SupervisionDate = "2026-09-30"
        });
        createResponse.EnsureSuccessStatusCode();

        var getResponse = await admin.GetAsync("/api/supervisions?institution=DOK");
        getResponse.EnsureSuccessStatusCode();
        var supervisions = await getResponse.Content.ReadFromJsonAsync<List<SupervisionDto>>(EnumJsonOptions);
        Assert.Contains(supervisions!, s => s.GroupLabel == "Grupa A");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/tests/DokPortal.Infrastructure.Tests backend/tests/DokPortal.Api.IntegrationTests`
Expected: FAIL to compile.

- [ ] **Step 3: Implement Application contracts**

`backend/src/DokPortal.Application/Supervisions/SupervisionDto.cs`:

```csharp
using DokPortal.Domain.Enums;

namespace DokPortal.Application.Supervisions;

public class SupervisionDto
{
    public required Guid Id { get; init; }
    public required Institution Institution { get; init; }
    public required string GroupLabel { get; init; }
    public required DateOnly SupervisionDate { get; init; }
    public int? AttendeesCount { get; init; }
    public int? ExpectedCount { get; init; }
    public string? Topic { get; init; }
    public string? Conclusion { get; init; }
}
```

`backend/src/DokPortal.Application/Supervisions/CreateSupervisionRequest.cs`:

```csharp
using DokPortal.Domain.Enums;

namespace DokPortal.Application.Supervisions;

public class CreateSupervisionRequest
{
    public required Institution Institution { get; init; }
    public required string GroupLabel { get; init; }
    public required DateOnly SupervisionDate { get; init; }
    public int? AttendeesCount { get; init; }
    public int? ExpectedCount { get; init; }
    public string? Topic { get; init; }
    public string? Conclusion { get; init; }
}
```

`backend/src/DokPortal.Application/Supervisions/ISupervisionService.cs`:

```csharp
using DokPortal.Domain.Enums;

namespace DokPortal.Application.Supervisions;

public interface ISupervisionService
{
    Task<IReadOnlyList<SupervisionDto>> GetAllAsync(Institution? institution, CancellationToken ct);
    Task<SupervisionDto> CreateAsync(CreateSupervisionRequest request, CancellationToken ct);
}
```

- [ ] **Step 4: Implement SupervisionService**

`backend/src/DokPortal.Infrastructure/Services/SupervisionService.cs`:

```csharp
using DokPortal.Application.Supervisions;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class SupervisionService : ISupervisionService
{
    private readonly AppDbContext _db;

    public SupervisionService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<SupervisionDto>> GetAllAsync(Institution? institution, CancellationToken ct)
    {
        var q = _db.Supervisions.AsNoTracking().AsQueryable();
        if (institution.HasValue)
        {
            q = q.Where(s => s.Institution == institution.Value);
        }

        var supervisions = await q.OrderByDescending(s => s.SupervisionDate).ToListAsync(ct);
        return supervisions.Select(ToDto).ToList();
    }

    public async Task<SupervisionDto> CreateAsync(CreateSupervisionRequest request, CancellationToken ct)
    {
        var supervision = new Supervision
        {
            Id = Guid.NewGuid(),
            Institution = request.Institution,
            GroupLabel = request.GroupLabel,
            SupervisionDate = request.SupervisionDate,
            AttendeesCount = request.AttendeesCount,
            ExpectedCount = request.ExpectedCount,
            Topic = request.Topic,
            Conclusion = request.Conclusion,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Supervisions.Add(supervision);
        await _db.SaveChangesAsync(ct);
        return ToDto(supervision);
    }

    private static SupervisionDto ToDto(Supervision s) => new()
    {
        Id = s.Id,
        Institution = s.Institution,
        GroupLabel = s.GroupLabel,
        SupervisionDate = s.SupervisionDate,
        AttendeesCount = s.AttendeesCount,
        ExpectedCount = s.ExpectedCount,
        Topic = s.Topic,
        Conclusion = s.Conclusion
    };
}
```

- [ ] **Step 5: Implement SupervisionsController**

`backend/src/DokPortal.Api/Controllers/SupervisionsController.cs`:

```csharp
using DokPortal.Application.Supervisions;
using DokPortal.Domain.Constants;
using DokPortal.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/supervisions")]
[Authorize]
public class SupervisionsController : ControllerBase
{
    private readonly ISupervisionService _supervisionService;

    public SupervisionsController(ISupervisionService supervisionService) => _supervisionService = supervisionService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SupervisionDto>>> GetAll([FromQuery] Institution? institution, CancellationToken ct)
        => Ok(await _supervisionService.GetAllAsync(institution, ct));

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorDOK},{AppRoles.DyrektorSKSP},{AppRoles.Superwizor}")]
    public async Task<ActionResult<SupervisionDto>> Create(CreateSupervisionRequest request, CancellationToken ct)
    {
        var created = await _supervisionService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), created);
    }
}
```

- [ ] **Step 6: Register the service**

In `backend/src/DokPortal.Api/Program.cs`, add `using DokPortal.Application.Supervisions;` and register:

```csharp
builder.Services.AddScoped<ISupervisionService, SupervisionService>();
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test backend/DokPortal.sln`
Expected: All tests pass — this closes out the backend half of Faza 3.

- [ ] **Step 8: Commit**

```bash
git add backend
git commit -m "Add Supervisions module shared by SKSP and DOK"
```

---

## Task 7: DokCases page (Podopieczni DOK)

**Files:**
- Create: `frontend/src/app/features/dok-cases/dok-case.model.ts`
- Create: `frontend/src/app/features/dok-cases/dok-cases.service.ts`
- Test: `frontend/src/app/features/dok-cases/dok-cases.service.spec.ts`
- Create: `frontend/src/app/features/dok-cases/dok-case-form.component.ts`
- Create: `frontend/src/app/features/dok-cases/dok-case-form.component.html`
- Create: `frontend/src/app/features/dok-cases/dok-case-form.component.scss` (empty)
- Create: `frontend/src/app/features/dok-cases/dok-cases-list.component.ts`
- Create: `frontend/src/app/features/dok-cases/dok-cases-list.component.html`
- Create: `frontend/src/app/features/dok-cases/dok-cases-list.component.scss` (empty)
- Test: `frontend/src/app/features/dok-cases/dok-cases-list.component.spec.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/layout/nav-items.ts`

**Interfaces:**
- Consumes: backend `GET/POST /api/dok-cases` (Task 2), `GET /api/people` (Phase 1).
- Produces: route `/dok-cases`; nav item visible to `Administrator`/`DyrektorDOK`/`Superwizor`/`KatechistaProwadzacy`/`Biskup`.

- [ ] **Step 1: Write the failing tests**

`frontend/src/app/features/dok-cases/dok-case.model.ts`:

```typescript
export type DokPath = 'BaptismCandidate' | 'Confirmation' | 'Communion' | 'Conversion' | 'ReturnToUnity';
export type DokStage = 'Application' | 'Formation' | 'Sacrament' | 'Graduate';

export interface DokCase {
  id: string;
  personId: string;
  personFullName: string;
  parishName: string | null;
  path: DokPath;
  stage: DokStage;
  catechistPersonId: string;
  catechistFullName: string;
  mentorPersonId: string | null;
  mentorFullName: string | null;
  lastMeetingDate: string | null;
  completedAtUtc: string | null;
}

export interface DokCaseFormValue {
  personId: string;
  path: DokPath;
  stage: DokStage;
  catechistPersonId: string;
  mentorPersonId?: string;
}
```

`frontend/src/app/features/dok-cases/dok-cases.service.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it } from 'vitest';
import { DokCasesService } from './dok-cases.service';
import { environment } from '../../../environments/environment';

describe('DokCasesService', () => {
  it('sends the path as a request parameter when searching', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(DokCasesService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.search('Confirmation').subscribe();

    const req = httpMock.expectOne(
      r => r.url === `${environment.apiBaseUrl}/api/dok-cases` && r.params.get('path') === 'Confirmation'
    );
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 100 });
    httpMock.verify();
  });
});
```

`frontend/src/app/features/dok-cases/dok-cases-list.component.spec.ts`:

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { DokCasesListComponent } from './dok-cases-list.component';
import { environment } from '../../../environments/environment';

describe('DokCasesListComponent', () => {
  let fixture: ComponentFixture<DokCasesListComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [DokCasesListComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(DokCasesListComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('shows the per-path count computed from the fetched list', () => {
    fixture.detectChanges();
    const casesReq = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/dok-cases`);
    casesReq.flush({
      items: [
        { id: '1', personId: 'p1', personFullName: 'Jan Kowalski', parishName: null, path: 'Confirmation', stage: 'Formation', catechistPersonId: 'c1', catechistFullName: 'Anna Maj', mentorPersonId: null, mentorFullName: null, lastMeetingDate: null, completedAtUtc: null },
        { id: '2', personId: 'p2', personFullName: 'Piotr Malinowski', parishName: null, path: 'Conversion', stage: 'Sacrament', catechistPersonId: 'c2', catechistFullName: 'Maria Kaczmarek', mentorPersonId: null, mentorFullName: null, lastMeetingDate: null, completedAtUtc: null }
      ],
      totalCount: 2, page: 1, pageSize: 100
    });
    httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/people`).flush({ items: [], totalCount: 0, page: 1, pageSize: 200 });
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Jan Kowalski');
    expect(text).toContain('Piotr Malinowski');
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd frontend && npx ng test`
Expected: FAIL to compile.

- [ ] **Step 3: Implement DokCasesService**

`frontend/src/app/features/dok-cases/dok-cases.service.ts`:

```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../people/person.model';
import { DokCase, DokCaseFormValue, DokPath } from './dok-case.model';

@Injectable({ providedIn: 'root' })
export class DokCasesService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/dok-cases`;

  constructor(private readonly http: HttpClient) {}

  search(path?: DokPath) {
    const params: Record<string, string> = { pageSize: '100' };
    if (path) {
      params['path'] = path;
    }
    return this.http.get<PagedResult<DokCase>>(this.baseUrl, { params });
  }

  create(value: DokCaseFormValue) {
    return this.http.post<DokCase>(this.baseUrl, value);
  }
}
```

- [ ] **Step 4: Implement the add-case modal**

`frontend/src/app/features/dok-cases/dok-case-form.component.ts`:

```typescript
import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PeopleService } from '../people/people.service';
import { Person } from '../people/person.model';
import { DokCaseFormValue } from './dok-case.model';

@Component({
  selector: 'app-dok-case-form',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './dok-case-form.component.html',
  styleUrl: './dok-case-form.component.scss'
})
export class DokCaseFormComponent implements OnInit {
  @Input() open = false;
  @Input() value: DokCaseFormValue = { personId: '', path: 'BaptismCandidate', stage: 'Application', catechistPersonId: '' };
  @Output() save = new EventEmitter<DokCaseFormValue>();
  @Output() cancel = new EventEmitter<void>();

  people: Person[] = [];

  constructor(private readonly peopleService: PeopleService) {}

  ngOnInit(): void {
    this.peopleService.search('', 1, 200).subscribe(result => (this.people = result.items));
  }

  submit(): void {
    this.save.emit(this.value);
  }
}
```

`frontend/src/app/features/dok-cases/dok-case-form.component.html`:

```html
@if (open) {
  <div class="overlay show">
    <div class="modal">
      <div class="modal-head">
        <h3>Nowy podopieczny DOK</h3>
        <button class="close" (click)="cancel.emit()">×</button>
      </div>
      <div class="modal-body">
        <div class="form-grid">
          <div class="field">
            <label>Osoba</label>
            <select [(ngModel)]="value.personId" name="personId">
              @for (person of people; track person.id) {
                <option [value]="person.id">{{ person.fullName }}</option>
              }
            </select>
          </div>
          <div class="field">
            <label>Ścieżka</label>
            <select [(ngModel)]="value.path" name="path">
              <option value="BaptismCandidate">Kandydaci do Chrztu</option>
              <option value="Confirmation">Bierzmowanie</option>
              <option value="Communion">Stół Pański</option>
              <option value="Conversion">Konwersja</option>
              <option value="ReturnToUnity">Powrót do Jedności</option>
            </select>
          </div>
          <div class="field">
            <label>Etap</label>
            <select [(ngModel)]="value.stage" name="stage">
              <option value="Application">Zgłoszenie</option>
              <option value="Formation">Formacja</option>
              <option value="Sacrament">Sakrament</option>
              <option value="Graduate">Absolwent</option>
            </select>
          </div>
          <div class="field">
            <label>Katechista prowadzący</label>
            <select [(ngModel)]="value.catechistPersonId" name="catechistPersonId">
              @for (person of people; track person.id) {
                <option [value]="person.id">{{ person.fullName }}</option>
              }
            </select>
          </div>
        </div>
      </div>
      <div class="modal-foot">
        <button class="btn ghost" (click)="cancel.emit()">Anuluj</button>
        <button class="btn primary" (click)="submit()">Zapisz</button>
      </div>
    </div>
  </div>
}
```

`frontend/src/app/features/dok-cases/dok-case-form.component.scss`: create as an empty file.

- [ ] **Step 5: Implement the list page**

`frontend/src/app/features/dok-cases/dok-cases-list.component.ts`:

```typescript
import { Component, OnInit, computed, signal } from '@angular/core';
import { DokCasesService } from './dok-cases.service';
import { DokCase, DokCaseFormValue } from './dok-case.model';
import { DokCaseFormComponent } from './dok-case-form.component';

const PATH_LABELS: Record<string, string> = {
  BaptismCandidate: 'Chrzest',
  Confirmation: 'Bierzmowanie',
  Communion: 'Stół Pański',
  Conversion: 'Konwersja',
  ReturnToUnity: 'Powrót do Jedności'
};

@Component({
  selector: 'app-dok-cases-list',
  standalone: true,
  imports: [DokCaseFormComponent],
  templateUrl: './dok-cases-list.component.html',
  styleUrl: './dok-cases-list.component.scss'
})
export class DokCasesListComponent implements OnInit {
  readonly cases = signal<DokCase[]>([]);
  readonly isFormOpen = signal(false);
  formValue: DokCaseFormValue = { personId: '', path: 'BaptismCandidate', stage: 'Application', catechistPersonId: '' };

  readonly baptismCount = computed(() => this.cases().filter(c => c.path === 'BaptismCandidate').length);
  readonly confirmationCount = computed(() => this.cases().filter(c => c.path === 'Confirmation').length);
  readonly conversionCount = computed(() => this.cases().filter(c => c.path === 'Conversion' || c.path === 'ReturnToUnity').length);
  readonly communionCount = computed(() => this.cases().filter(c => c.path === 'Communion').length);

  constructor(private readonly dokCasesService: DokCasesService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.dokCasesService.search().subscribe(result => this.cases.set(result.items));
  }

  openAddForm(): void {
    this.formValue = { personId: '', path: 'BaptismCandidate', stage: 'Application', catechistPersonId: '' };
    this.isFormOpen.set(true);
  }

  onSave(value: DokCaseFormValue): void {
    this.dokCasesService.create(value).subscribe(() => {
      this.isFormOpen.set(false);
      this.load();
    });
  }

  onCancel(): void {
    this.isFormOpen.set(false);
  }

  pathLabel(path: string): string {
    return PATH_LABELS[path] ?? path;
  }
}
```

`frontend/src/app/features/dok-cases/dok-cases-list.component.html`:

```html
<div class="page-heading">
  <div><h2>Podopieczni DOK</h2><p>Pięć ścieżek formacyjnych i indywidualny obieg dokumentów.</p></div>
  <button class="btn primary" (click)="openAddForm()">＋ Nowy podopieczny</button>
</div>

<div class="grid stats">
  <div class="card stat"><div class="stat-label">Katechumeni</div><div class="stat-value">{{ baptismCount() }}</div></div>
  <div class="card stat"><div class="stat-label">Bierzmowanie</div><div class="stat-value">{{ confirmationCount() }}</div></div>
  <div class="card stat"><div class="stat-label">Konwersja / Rekonsyliacja</div><div class="stat-value">{{ conversionCount() }}</div></div>
  <div class="card stat"><div class="stat-label">Stół Pański</div><div class="stat-value">{{ communionCount() }}</div></div>
</div>

<div class="card">
  <div class="table-wrap">
    <table>
      <thead><tr><th>Podopieczny</th><th>Ścieżka</th><th>Etap</th><th>Katechista</th><th>Parafia</th></tr></thead>
      <tbody>
        @for (dokCase of cases(); track dokCase.id) {
          <tr>
            <td><b>{{ dokCase.personFullName }}</b></td>
            <td>{{ pathLabel(dokCase.path) }}</td>
            <td><span class="pill blue">{{ dokCase.stage }}</span></td>
            <td>{{ dokCase.catechistFullName }}</td>
            <td>{{ dokCase.parishName }}</td>
          </tr>
        } @empty {
          <tr><td colspan="5" class="empty">Brak podopiecznych.</td></tr>
        }
      </tbody>
    </table>
  </div>
</div>

<app-dok-case-form
  [open]="isFormOpen()"
  [value]="formValue"
  (save)="onSave($event)"
  (cancel)="onCancel()">
</app-dok-case-form>
```

`frontend/src/app/features/dok-cases/dok-cases-list.component.scss`: create as an empty file.

- [ ] **Step 6: Wire the route and nav item**

In `frontend/src/app/app.routes.ts`, add to the shell's `children` array:

```typescript
{
  path: 'dok-cases',
  loadComponent: () => import('./features/dok-cases/dok-cases-list.component').then(m => m.DokCasesListComponent)
}
```

In `frontend/src/app/layout/nav-items.ts`, add to `NAV_ITEMS`:

```typescript
{ label: 'Podopieczni DOK', icon: '◍', path: '/dok-cases', roles: ['Administrator', 'DyrektorDOK', 'Superwizor', 'KatechistaProwadzacy', 'Biskup'] }
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `npx ng test`
Expected: PASS (2 new test files).

- [ ] **Step 8: Commit**

```bash
git add frontend
git commit -m "Add DokCases page (Podopieczni DOK)"
```

---

## Task 8: Meetings page (Harmonogram)

**Files:**
- Create: `frontend/src/app/features/meetings/meeting.model.ts`
- Create: `frontend/src/app/features/meetings/meetings.service.ts`
- Test: `frontend/src/app/features/meetings/meetings.service.spec.ts`
- Create: `frontend/src/app/features/meetings/meetings-list.component.ts`
- Create: `frontend/src/app/features/meetings/meetings-list.component.html`
- Create: `frontend/src/app/features/meetings/meetings-list.component.scss` (empty)
- Test: `frontend/src/app/features/meetings/meetings-list.component.spec.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/layout/nav-items.ts`

**Interfaces:**
- Consumes: backend `GET/POST /api/meetings` (Task 5).
- Produces: route `/meetings`; nav item visible to `Administrator`/`DyrektorDOK`/`KatechistaProwadzacy`.

- [ ] **Step 1: Write the failing tests**

`frontend/src/app/features/meetings/meeting.model.ts`:

```typescript
export interface Meeting {
  id: string;
  dokCaseId: string | null;
  caseLabel: string | null;
  groupLabel: string | null;
  meetingDate: string;
  isAttended: boolean | null;
  notes: string | null;
}

export interface CreateMeetingValue {
  dokCaseId?: string;
  groupLabel?: string;
  meetingDate: string;
  isAttended?: boolean;
  notes?: string;
}
```

`frontend/src/app/features/meetings/meetings.service.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it } from 'vitest';
import { MeetingsService } from './meetings.service';
import { environment } from '../../../environments/environment';

describe('MeetingsService', () => {
  it('requests meetings from the API', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(MeetingsService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.list().subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/meetings`);
    req.flush([]);
    httpMock.verify();
  });
});
```

`frontend/src/app/features/meetings/meetings-list.component.spec.ts`:

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { MeetingsListComponent } from './meetings-list.component';
import { environment } from '../../../environments/environment';

describe('MeetingsListComponent', () => {
  let fixture: ComponentFixture<MeetingsListComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [MeetingsListComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(MeetingsListComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('renders meetings returned from the API', () => {
    fixture.detectChanges();
    const req = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/meetings`);
    req.flush([{ id: '1', dokCaseId: null, caseLabel: null, groupLabel: 'DOK grupa', meetingDate: '2026-09-24', isAttended: null, notes: null }]);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('DOK grupa');
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd frontend && npx ng test`
Expected: FAIL to compile.

- [ ] **Step 3: Implement MeetingsService**

`frontend/src/app/features/meetings/meetings.service.ts`:

```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { CreateMeetingValue, Meeting } from './meeting.model';

@Injectable({ providedIn: 'root' })
export class MeetingsService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/meetings`;

  constructor(private readonly http: HttpClient) {}

  list() {
    return this.http.get<Meeting[]>(this.baseUrl);
  }

  create(value: CreateMeetingValue) {
    return this.http.post<Meeting>(this.baseUrl, value);
  }
}
```

- [ ] **Step 4: Implement the list page (inline add form, matching the Formatorzy page's simple single-table layout)**

`frontend/src/app/features/meetings/meetings-list.component.ts`:

```typescript
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MeetingsService } from './meetings.service';
import { CreateMeetingValue, Meeting } from './meeting.model';

@Component({
  selector: 'app-meetings-list',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './meetings-list.component.html',
  styleUrl: './meetings-list.component.scss'
})
export class MeetingsListComponent implements OnInit {
  readonly meetings = signal<Meeting[]>([]);
  readonly isFormOpen = signal(false);
  newMeeting: CreateMeetingValue = { groupLabel: '', meetingDate: '' };

  constructor(private readonly meetingsService: MeetingsService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.meetingsService.list().subscribe(meetings => this.meetings.set(meetings));
  }

  openAddForm(): void {
    this.newMeeting = { groupLabel: '', meetingDate: '' };
    this.isFormOpen.set(true);
  }

  createMeeting(): void {
    this.meetingsService.create(this.newMeeting).subscribe(() => {
      this.isFormOpen.set(false);
      this.load();
    });
  }

  cancel(): void {
    this.isFormOpen.set(false);
  }
}
```

`frontend/src/app/features/meetings/meetings-list.component.html`:

```html
<div class="page-heading">
  <div><h2>Harmonogram i obecności</h2><p>Spotkania indywidualne i zajęcia grupowe.</p></div>
  <button class="btn primary" (click)="openAddForm()">＋ Dodaj spotkanie</button>
</div>

<div class="card">
  <div class="table-wrap">
    <table>
      <thead><tr><th>Data</th><th>Podopieczny / grupa</th><th>Obecność</th><th>Notatki</th></tr></thead>
      <tbody>
        @for (meeting of meetings(); track meeting.id) {
          <tr>
            <td>{{ meeting.meetingDate }}</td>
            <td>{{ meeting.caseLabel ?? meeting.groupLabel }}</td>
            <td>
              @if (meeting.isAttended === true) {
                <span class="pill green">obecny</span>
              } @else if (meeting.isAttended === false) {
                <span class="pill red">nieobecny</span>
              }
            </td>
            <td>{{ meeting.notes }}</td>
          </tr>
        } @empty {
          <tr><td colspan="4" class="empty">Brak spotkań.</td></tr>
        }
      </tbody>
    </table>
  </div>
</div>

@if (isFormOpen()) {
  <div class="overlay show">
    <div class="modal">
      <div class="modal-head">
        <h3>Nowe spotkanie</h3>
        <button class="close" (click)="cancel()">×</button>
      </div>
      <div class="modal-body">
        <div class="form-grid">
          <div class="field"><label>Data</label><input type="date" [(ngModel)]="newMeeting.meetingDate" name="meetingDate" /></div>
          <div class="field"><label>Grupa / opis</label><input [(ngModel)]="newMeeting.groupLabel" name="groupLabel" /></div>
        </div>
      </div>
      <div class="modal-foot">
        <button class="btn ghost" (click)="cancel()">Anuluj</button>
        <button class="btn primary" (click)="createMeeting()">Zapisz</button>
      </div>
    </div>
  </div>
}
```

`frontend/src/app/features/meetings/meetings-list.component.scss`: create as an empty file.

- [ ] **Step 5: Wire the route and nav item**

In `frontend/src/app/app.routes.ts`, add to the shell's `children` array:

```typescript
{
  path: 'meetings',
  loadComponent: () => import('./features/meetings/meetings-list.component').then(m => m.MeetingsListComponent)
}
```

In `frontend/src/app/layout/nav-items.ts`, add to `NAV_ITEMS`:

```typescript
{ label: 'Harmonogram i obecności', icon: '▦', path: '/meetings', roles: ['Administrator', 'DyrektorDOK', 'KatechistaProwadzacy'] }
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `npx ng test`
Expected: PASS (2 new test files).

- [ ] **Step 7: Commit**

```bash
git add frontend
git commit -m "Add Meetings page (harmonogram as a list)"
```

---

## Task 9: Supervisions page

**Files:**
- Create: `frontend/src/app/features/supervisions/supervision.model.ts`
- Create: `frontend/src/app/features/supervisions/supervisions.service.ts`
- Test: `frontend/src/app/features/supervisions/supervisions.service.spec.ts`
- Create: `frontend/src/app/features/supervisions/supervisions-list.component.ts`
- Create: `frontend/src/app/features/supervisions/supervisions-list.component.html`
- Create: `frontend/src/app/features/supervisions/supervisions-list.component.scss` (empty)
- Test: `frontend/src/app/features/supervisions/supervisions-list.component.spec.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/layout/nav-items.ts`

**Interfaces:**
- Consumes: backend `GET /api/supervisions`, `POST /api/supervisions` (Task 6).
- Produces: route `/supervisions`; nav item visible to `Administrator`/`DyrektorDOK`/`DyrektorSKSP`/`Superwizor`.

- [ ] **Step 1: Write the failing tests**

`frontend/src/app/features/supervisions/supervision.model.ts`:

```typescript
export type Institution = 'SKSP' | 'DOK';

export interface Supervision {
  id: string;
  institution: Institution;
  groupLabel: string;
  supervisionDate: string;
  attendeesCount: number | null;
  expectedCount: number | null;
  topic: string | null;
  conclusion: string | null;
}

export interface CreateSupervisionValue {
  institution: Institution;
  groupLabel: string;
  supervisionDate: string;
  attendeesCount?: number;
  expectedCount?: number;
  topic?: string;
  conclusion?: string;
}
```

`frontend/src/app/features/supervisions/supervisions.service.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it } from 'vitest';
import { SupervisionsService } from './supervisions.service';
import { environment } from '../../../environments/environment';

describe('SupervisionsService', () => {
  it('requests supervisions from the API', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(SupervisionsService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.list().subscribe();

    const req = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/supervisions`);
    req.flush([]);
    httpMock.verify();
  });
});
```

`frontend/src/app/features/supervisions/supervisions-list.component.spec.ts`:

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { SupervisionsListComponent } from './supervisions-list.component';
import { environment } from '../../../environments/environment';

describe('SupervisionsListComponent', () => {
  let fixture: ComponentFixture<SupervisionsListComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [SupervisionsListComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(SupervisionsListComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('renders supervisions returned from the API', () => {
    fixture.detectChanges();
    const req = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/supervisions`);
    req.flush([{ id: '1', institution: 'DOK', groupLabel: 'Grupa A', supervisionDate: '2026-09-30', attendeesCount: 8, expectedCount: 8, topic: null, conclusion: null }]);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Grupa A');
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd frontend && npx ng test`
Expected: FAIL to compile.

- [ ] **Step 3: Implement SupervisionsService**

`frontend/src/app/features/supervisions/supervisions.service.ts`:

```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { CreateSupervisionValue, Institution, Supervision } from './supervision.model';

@Injectable({ providedIn: 'root' })
export class SupervisionsService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/supervisions`;

  constructor(private readonly http: HttpClient) {}

  list(institution?: Institution) {
    const params = institution ? { institution } : {};
    return this.http.get<Supervision[]>(this.baseUrl, { params });
  }

  create(value: CreateSupervisionValue) {
    return this.http.post<Supervision>(this.baseUrl, value);
  }
}
```

- [ ] **Step 4: Implement the list page**

`frontend/src/app/features/supervisions/supervisions-list.component.ts`:

```typescript
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SupervisionsService } from './supervisions.service';
import { CreateSupervisionValue, Supervision } from './supervision.model';

@Component({
  selector: 'app-supervisions-list',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './supervisions-list.component.html',
  styleUrl: './supervisions-list.component.scss'
})
export class SupervisionsListComponent implements OnInit {
  readonly supervisions = signal<Supervision[]>([]);
  readonly isFormOpen = signal(false);
  newSupervision: CreateSupervisionValue = { institution: 'DOK', groupLabel: '', supervisionDate: '' };

  constructor(private readonly supervisionsService: SupervisionsService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.supervisionsService.list().subscribe(supervisions => this.supervisions.set(supervisions));
  }

  openAddForm(): void {
    this.newSupervision = { institution: 'DOK', groupLabel: '', supervisionDate: '' };
    this.isFormOpen.set(true);
  }

  createSupervision(): void {
    this.supervisionsService.create(this.newSupervision).subscribe(() => {
      this.isFormOpen.set(false);
      this.load();
    });
  }

  cancel(): void {
    this.isFormOpen.set(false);
  }
}
```

`frontend/src/app/features/supervisions/supervisions-list.component.html`:

```html
<div class="page-heading">
  <div><h2>Superwizje</h2><p>Frekwencja, tematyka i wnioski — wspólne dla SKŚP i DOK.</p></div>
  <button class="btn primary" (click)="openAddForm()">＋ Nowa superwizja</button>
</div>

<div class="card">
  <div class="card-body list">
    @for (supervision of supervisions(); track supervision.id) {
      <div class="list-row">
        <div class="list-main">
          <div class="list-title">{{ supervision.groupLabel }} — {{ supervision.supervisionDate }}</div>
          <div class="list-sub">{{ supervision.institution }} @if (supervision.attendeesCount !== null) { • {{ supervision.attendeesCount }}/{{ supervision.expectedCount }} obecnych }</div>
        </div>
      </div>
    } @empty {
      <div class="empty">Brak superwizji.</div>
    }
  </div>
</div>

@if (isFormOpen()) {
  <div class="overlay show">
    <div class="modal">
      <div class="modal-head">
        <h3>Nowa superwizja</h3>
        <button class="close" (click)="cancel()">×</button>
      </div>
      <div class="modal-body">
        <div class="form-grid">
          <div class="field">
            <label>Instytucja</label>
            <select [(ngModel)]="newSupervision.institution" name="institution">
              <option value="DOK">DOK</option>
              <option value="SKSP">SKŚP</option>
            </select>
          </div>
          <div class="field"><label>Grupa</label><input [(ngModel)]="newSupervision.groupLabel" name="groupLabel" /></div>
          <div class="field"><label>Data</label><input type="date" [(ngModel)]="newSupervision.supervisionDate" name="supervisionDate" /></div>
          <div class="field full"><label>Tematyka</label><textarea [(ngModel)]="newSupervision.topic" name="topic"></textarea></div>
          <div class="field full"><label>Wnioski</label><textarea [(ngModel)]="newSupervision.conclusion" name="conclusion"></textarea></div>
        </div>
      </div>
      <div class="modal-foot">
        <button class="btn ghost" (click)="cancel()">Anuluj</button>
        <button class="btn primary" (click)="createSupervision()">Zapisz</button>
      </div>
    </div>
  </div>
}
```

`frontend/src/app/features/supervisions/supervisions-list.component.scss`: create as an empty file.

- [ ] **Step 5: Wire the route and nav item**

In `frontend/src/app/app.routes.ts`, add to the shell's `children` array:

```typescript
{
  path: 'supervisions',
  loadComponent: () => import('./features/supervisions/supervisions-list.component').then(m => m.SupervisionsListComponent)
}
```

In `frontend/src/app/layout/nav-items.ts`, add to `NAV_ITEMS`:

```typescript
{ label: 'Superwizje', icon: '◌', path: '/supervisions', roles: ['Administrator', 'DyrektorDOK', 'DyrektorSKSP', 'Superwizor'] }
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `npx ng test`
Expected: PASS (2 new test files).

- [ ] **Step 7: Commit**

```bash
git add frontend
git commit -m "Add Supervisions page shared by SKSP and DOK"
```

---

## Task 10: Graduates page (Absolwenci)

**Files:**
- Create: `frontend/src/app/features/graduates/graduates-list.component.ts`
- Create: `frontend/src/app/features/graduates/graduates-list.component.html`
- Create: `frontend/src/app/features/graduates/graduates-list.component.scss` (empty)
- Test: `frontend/src/app/features/graduates/graduates-list.component.spec.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/layout/nav-items.ts`

**Interfaces:**
- Consumes: `DokCasesService` (Task 7) — requests cases and filters `stage === 'Graduate'` client-side, exactly like the Dashboard/Candidates pages compute stats from an already-fetched list. No new backend endpoint.
- Produces: route `/graduates`; nav item visible to `Administrator`/`DyrektorDOK`.

- [ ] **Step 1: Write the failing test**

`frontend/src/app/features/graduates/graduates-list.component.spec.ts`:

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { GraduatesListComponent } from './graduates-list.component';
import { environment } from '../../../environments/environment';

describe('GraduatesListComponent', () => {
  let fixture: ComponentFixture<GraduatesListComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [GraduatesListComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(GraduatesListComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('renders only cases whose stage is Graduate', () => {
    fixture.detectChanges();
    const req = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/dok-cases`);
    req.flush({
      items: [
        { id: '1', personId: 'p1', personFullName: 'Katarzyna Jankowska', parishName: 'św. Mateusza', path: 'BaptismCandidate', stage: 'Graduate', catechistPersonId: 'c1', catechistFullName: 'Joanna Lis', mentorPersonId: null, mentorFullName: null, lastMeetingDate: null, completedAtUtc: '2026-04-04T00:00:00Z' },
        { id: '2', personId: 'p2', personFullName: 'Jan Kowalski', parishName: null, path: 'Confirmation', stage: 'Formation', catechistPersonId: 'c2', catechistFullName: 'Anna Maj', mentorPersonId: null, mentorFullName: null, lastMeetingDate: null, completedAtUtc: null }
      ],
      totalCount: 2, page: 1, pageSize: 100
    });
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Katarzyna Jankowska');
    expect(text).not.toContain('Jan Kowalski');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx ng test`
Expected: FAIL to compile — `GraduatesListComponent` doesn't exist yet.

- [ ] **Step 3: Implement the component**

`frontend/src/app/features/graduates/graduates-list.component.ts`:

```typescript
import { Component, OnInit, computed, signal } from '@angular/core';
import { DokCasesService } from '../dok-cases/dok-cases.service';
import { DokCase } from '../dok-cases/dok-case.model';

@Component({
  selector: 'app-graduates-list',
  standalone: true,
  templateUrl: './graduates-list.component.html',
  styleUrl: './graduates-list.component.scss'
})
export class GraduatesListComponent implements OnInit {
  private readonly allCases = signal<DokCase[]>([]);
  readonly graduates = computed(() => this.allCases().filter(c => c.stage === 'Graduate'));

  constructor(private readonly dokCasesService: DokCasesService) {}

  ngOnInit(): void {
    this.dokCasesService.search().subscribe(result => this.allCases.set(result.items));
  }
}
```

`frontend/src/app/features/graduates/graduates-list.component.html`:

```html
<div class="page-heading">
  <div><h2>Absolwenci DOK — „Pragnę Więcej"</h2><p>Osoby, które ukończyły formację.</p></div>
</div>

<div class="card">
  <div class="table-wrap">
    <table>
      <thead><tr><th>Osoba</th><th>Ukończona ścieżka</th><th>Data ukończenia</th><th>Parafia</th><th>Opiekun grupy</th></tr></thead>
      <tbody>
        @for (graduate of graduates(); track graduate.id) {
          <tr>
            <td><b>{{ graduate.personFullName }}</b></td>
            <td>{{ graduate.path }}</td>
            <td>{{ graduate.completedAtUtc }}</td>
            <td>{{ graduate.parishName }}</td>
            <td>{{ graduate.mentorFullName }}</td>
          </tr>
        } @empty {
          <tr><td colspan="5" class="empty">Brak absolwentów.</td></tr>
        }
      </tbody>
    </table>
  </div>
</div>
```

`frontend/src/app/features/graduates/graduates-list.component.scss`: create as an empty file.

- [ ] **Step 4: Wire the route and nav item**

In `frontend/src/app/app.routes.ts`, add to the shell's `children` array:

```typescript
{
  path: 'graduates',
  loadComponent: () => import('./features/graduates/graduates-list.component').then(m => m.GraduatesListComponent)
}
```

In `frontend/src/app/layout/nav-items.ts`, add to `NAV_ITEMS`:

```typescript
{ label: 'Absolwenci', icon: '✓', path: '/graduates', roles: ['Administrator', 'DyrektorDOK'] }
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `npx ng test`
Expected: PASS (1 new test file).

- [ ] **Step 6: Commit**

```bash
git add frontend
git commit -m "Add Graduates page (Absolwenci)"
```

---

## Task 11: Budget DOK page

**Files:**
- Modify: `frontend/src/app/features/budget/budget.component.ts`
- Create: `frontend/src/app/features/budget-dok/budget-dok.component.ts`
- Create: `frontend/src/app/features/budget-dok/budget-dok.component.html`
- Create: `frontend/src/app/features/budget-dok/budget-dok.component.scss` (empty)
- Test: `frontend/src/app/features/budget-dok/budget-dok.component.spec.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/layout/nav-items.ts`

**Interfaces:**
- Consumes: existing `BudgetService` (Phase 2) — zero backend changes.
- Produces: route `/budget/dok`; nav item visible to `Administrator`/`DyrektorDOK`.

Rather than parametrize the existing SKŚP-titled `BudgetComponent`, this task adds a small sibling component for DOK with its own page heading, reusing `BudgetService` exactly as the SKŚP page does but requesting `Fund = 'DOK'`. This keeps both pages simple standalone components instead of threading a `fund`/title input through a shared one.

- [ ] **Step 1: Write the failing test**

`frontend/src/app/features/budget-dok/budget-dok.component.spec.ts`:

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { BudgetDokComponent } from './budget-dok.component';
import { environment } from '../../../environments/environment';

describe('BudgetDokComponent', () => {
  let fixture: ComponentFixture<BudgetDokComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [BudgetDokComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(BudgetDokComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('requests DOK-fund entries and computes totals', () => {
    fixture.detectChanges();
    const req = httpMock.expectOne(
      r => r.url === `${environment.apiBaseUrl}/api/budget` && r.params.get('fund') === 'DOK'
    );
    req.flush([
      { id: '1', fund: 'DOK', entryDate: '2026-09-12', description: 'Dotacja diecezjalna', category: 'Dotacja', type: 'Income', amount: 5000 },
      { id: '2', fund: 'DOK', entryDate: '2026-09-18', description: 'Materiały formacyjne', category: 'Materiały', type: 'Expense', amount: 780 }
    ]);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Dotacja diecezjalna');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx ng test`
Expected: FAIL to compile — `BudgetDokComponent` doesn't exist yet.

- [ ] **Step 3: Implement the component**

`frontend/src/app/features/budget-dok/budget-dok.component.ts`:

```typescript
import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { BudgetService } from '../budget/budget.service';
import { BudgetEntry, CreateBudgetEntryValue } from '../budget/budget-entry.model';

@Component({
  selector: 'app-budget-dok',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './budget-dok.component.html',
  styleUrl: './budget-dok.component.scss'
})
export class BudgetDokComponent implements OnInit {
  readonly entries = signal<BudgetEntry[]>([]);
  readonly isFormOpen = signal(false);
  newEntry: CreateBudgetEntryValue = {
    fund: 'DOK', entryDate: '', description: '', category: '', type: 'Expense', amount: 0
  };

  readonly totalIncome = computed(() =>
    this.entries().filter(e => e.type === 'Income').reduce((sum, e) => sum + e.amount, 0)
  );
  readonly totalExpense = computed(() =>
    this.entries().filter(e => e.type === 'Expense').reduce((sum, e) => sum + e.amount, 0)
  );
  readonly balance = computed(() => this.totalIncome() - this.totalExpense());

  constructor(private readonly budgetService: BudgetService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.budgetService.listEntries('DOK').subscribe(entries => this.entries.set(entries));
  }

  openAddForm(): void {
    this.newEntry = { fund: 'DOK', entryDate: '', description: '', category: '', type: 'Expense', amount: 0 };
    this.isFormOpen.set(true);
  }

  createEntry(): void {
    this.budgetService.create(this.newEntry).subscribe(() => {
      this.isFormOpen.set(false);
      this.load();
    });
  }

  cancel(): void {
    this.isFormOpen.set(false);
  }
}
```

`frontend/src/app/features/budget-dok/budget-dok.component.html`:

```html
<div class="page-heading">
  <div><h2>Budżet DOK</h2><p>Dedykowana ewidencja przychodów i wydatków Ośrodka.</p></div>
  <button class="btn primary" (click)="openAddForm()">＋ Dodaj operację</button>
</div>

<div class="finance-grid">
  <div class="card stat"><div class="stat-label">Przychody</div><div class="amount">{{ totalIncome() }} zł</div></div>
  <div class="card stat"><div class="stat-label">Wydatki</div><div class="amount">{{ totalExpense() }} zł</div></div>
  <div class="card stat"><div class="stat-label">Saldo</div><div class="amount" [style.color]="balance() >= 0 ? 'var(--success)' : 'var(--danger)'">{{ balance() }} zł</div></div>
</div>

<div class="card">
  <div class="table-wrap">
    <table>
      <thead><tr><th>Data</th><th>Opis</th><th>Kategoria</th><th>Kwota</th></tr></thead>
      <tbody>
        @for (entry of entries(); track entry.id) {
          <tr>
            <td>{{ entry.entryDate }}</td>
            <td>{{ entry.description }}</td>
            <td>{{ entry.category }}</td>
            <td [style.color]="entry.type === 'Income' ? 'var(--success)' : 'inherit'">
              {{ entry.type === 'Income' ? '+' : '-' }} {{ entry.amount }} zł
            </td>
          </tr>
        } @empty {
          <tr><td colspan="4" class="empty">Brak operacji.</td></tr>
        }
      </tbody>
    </table>
  </div>
</div>

@if (isFormOpen()) {
  <div class="overlay show">
    <div class="modal">
      <div class="modal-head">
        <h3>Nowa operacja</h3>
        <button class="close" (click)="cancel()">×</button>
      </div>
      <div class="modal-body">
        <div class="form-grid">
          <div class="field"><label>Data</label><input type="date" [(ngModel)]="newEntry.entryDate" name="entryDate" /></div>
          <div class="field">
            <label>Typ</label>
            <select [(ngModel)]="newEntry.type" name="type">
              <option value="Income">Przychód</option>
              <option value="Expense">Wydatek</option>
            </select>
          </div>
          <div class="field"><label>Opis</label><input [(ngModel)]="newEntry.description" name="description" /></div>
          <div class="field"><label>Kategoria</label><input [(ngModel)]="newEntry.category" name="category" /></div>
          <div class="field"><label>Kwota</label><input type="number" [(ngModel)]="newEntry.amount" name="amount" /></div>
        </div>
      </div>
      <div class="modal-foot">
        <button class="btn ghost" (click)="cancel()">Anuluj</button>
        <button class="btn primary" (click)="createEntry()">Zapisz</button>
      </div>
    </div>
  </div>
}
```

`frontend/src/app/features/budget-dok/budget-dok.component.scss`: create as an empty file.

- [ ] **Step 4: Wire the route and nav item**

In `frontend/src/app/app.routes.ts`, add to the shell's `children` array:

```typescript
{
  path: 'budget/dok',
  loadComponent: () => import('./features/budget-dok/budget-dok.component').then(m => m.BudgetDokComponent)
}
```

In `frontend/src/app/layout/nav-items.ts`, add to `NAV_ITEMS`:

```typescript
{ label: 'Budżet DOK', icon: '◈', path: '/budget/dok', roles: ['Administrator', 'DyrektorDOK'] }
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `npx ng test`
Expected: PASS (1 new test file). Then run the full frontend suite once more with no filter to confirm nothing else broke — this closes out Faza 3.

- [ ] **Step 6: Commit**

```bash
git add frontend
git commit -m "Add Budget DOK page"
```

---

## Self-Review Notes

- **Spec coverage:** DokCase/5 ścieżek/stage-track (Task 2, 7), CaseDocuments checklist (Task 3), PastoralNotes with real RODO filtering (Task 4), Meetings-as-list (Task 5, 8), shared Supervisions (Task 6, 9), Absolwenci as a `DokCase` filter (Task 10), Budżet DOK reusing Faza 2's ledger untouched (Task 11) — every Faza 3 goal from the spec maps to a task.
- **Type consistency verified:** `DokCaseDto.Path`/`Stage` (enum-typed, Task 2) match the Angular `DokPath`/`DokStage` string-literal unions (Task 7) and the JSON string values asserted in `DokCasesControllerTests`; `PastoralNoteService.GetVisibleForCaseAsync`'s `isPrivileged` parameter is computed once in `PastoralNotesController` from `User.IsInRole` and threaded straight through, matching the two authorization tests in `PastoralNoteServiceTests` and the katechista-vs-katechista / DyrektorDOK integration tests.
- **No placeholders:** every step contains complete, runnable code.
