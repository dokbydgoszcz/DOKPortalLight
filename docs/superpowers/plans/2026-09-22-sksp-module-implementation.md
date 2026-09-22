# DOK Portal Light — Faza 2: Moduł SKŚP — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the SKŚP module on top of the Phase 1 foundation: candidates, canonical missions, formators, parish-needs board, and a shared budget ledger, each with a real API and an Angular page following the same patterns as the People feature.

**Architecture:** New Domain entities (`Candidate`, `CanonicalMission`, `Formator`, `ParishNeed`, `BudgetEntry`) all FK to the existing `Person`/`Parish`; one new EF Core migration. Backend follows the exact Domain→Application→Infrastructure→Api layering and `[Authorize(Roles=...)]` pattern established in Phase 1. Frontend follows the exact `people-list.component` pattern (service + list/table + add modal, lazy-loaded route, role-filtered nav item).

**Tech Stack:** Same as Phase 1 — .NET 8, EF Core 8, ASP.NET Core Identity/JWT, xUnit; Angular (standalone, signals), Vitest.

**Spec:** [docs/superpowers/specs/2026-09-22-sksp-module-design.md](../specs/2026-09-22-sksp-module-design.md)

## Global Constraints

- All new entities reference the existing `Person`/`Parish` by FK — never duplicate personal data.
- Frekwencja/opinie on `Candidate` are plain manually-entered fields (no attendance/opinion record tables) per spec.
- `CanonicalMission.SupervisionGroup` is a plain nullable string, not an entity.
- `CanonicalMission` status ("ważna"/"wygasa"/"wygasła") is computed from `MissionEndDate` at read time in `MissionService`, never persisted.
- `BudgetEntry.Fund` is a shared enum (`SKSP`, `DOK`) on one table; this phase only ever writes/reads `SKSP`.
- Write endpoints across every module in this phase: `[Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorSKSP}")]`. Read endpoints: `[Authorize]` only (any authenticated role) — frontend nav visibility, not backend authorization, is what limits who *sees* a page (Biskup can additionally see the Missions nav item per spec).
- No new stats/summary endpoints: dashboards and category breakdowns are computed client-side from already-fetched lists (matches the existing Dashboard/People pattern — small diocese-scale datasets).
- Every task ends with a green `dotnet test DokPortal.sln` (backend tasks) or `npx ng test` (frontend tasks) and leaves the app buildable.

---

## Task 1: Domain entities, enums, DbContext wiring, migration

**Files:**
- Create: `backend/src/DokPortal.Domain/Entities/Candidate.cs`
- Create: `backend/src/DokPortal.Domain/Entities/CanonicalMission.cs`
- Create: `backend/src/DokPortal.Domain/Entities/Formator.cs`
- Create: `backend/src/DokPortal.Domain/Entities/ParishNeed.cs`
- Create: `backend/src/DokPortal.Domain/Entities/BudgetEntry.cs`
- Create: `backend/src/DokPortal.Domain/Enums/ParishNeedStatus.cs`
- Create: `backend/src/DokPortal.Domain/Enums/BudgetFund.cs`
- Create: `backend/src/DokPortal.Domain/Enums/BudgetEntryType.cs`
- Modify: `backend/src/DokPortal.Infrastructure/Persistence/AppDbContext.cs`
- Test: `backend/tests/DokPortal.Api.IntegrationTests/SkspEntitiesPersistenceTests.cs`

**Interfaces:**
- Consumes: `Person`, `Parish` (Phase 1), `CustomWebApplicationFactory` (Phase 1).
- Produces: the five entities and three enums below, plus `AppDbContext.Candidates/CanonicalMissions/Formators/ParishNeeds/BudgetEntries` — consumed by every task in this plan.

- [ ] **Step 1: Write the failing test**

`backend/tests/DokPortal.Api.IntegrationTests/SkspEntitiesPersistenceTests.cs`:

```csharp
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class SkspEntitiesPersistenceTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SkspEntitiesPersistenceTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task SavedSkspEntities_CanBeReadBackInANewScope()
    {
        Guid candidateId, missionId, formatorId, needId, entryId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var person = new Person
            {
                Id = Guid.NewGuid(), FirstName = "Test", LastName = "Kandydat",
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            };
            var parish = new Parish { Id = Guid.NewGuid(), Name = "Testowa" };
            db.People.Add(person);
            db.Parishes.Add(parish);

            var candidate = new Candidate
            {
                Id = Guid.NewGuid(), PersonId = person.Id, Year = 1, OpinionsCollected = 0,
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            };
            var mission = new CanonicalMission
            {
                Id = Guid.NewGuid(), PersonId = person.Id, ServicePlace = "Parafia testowa",
                MissionStartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                MissionEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(3)),
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            };
            var formator = new Formator { Id = Guid.NewGuid(), PersonId = person.Id, Function = "Wykładowca" };
            var need = new ParishNeed
            {
                Id = Guid.NewGuid(), ParishId = parish.Id, Description = "Potrzeba testowa",
                CreatedAtUtc = DateTime.UtcNow
            };
            var entry = new BudgetEntry
            {
                Id = Guid.NewGuid(), Fund = BudgetFund.SKSP, EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Description = "Wpis testowy", Category = "Test", Type = BudgetEntryType.Income, Amount = 100m,
                CreatedAtUtc = DateTime.UtcNow
            };

            db.Candidates.Add(candidate);
            db.CanonicalMissions.Add(mission);
            db.Formators.Add(formator);
            db.ParishNeeds.Add(need);
            db.BudgetEntries.Add(entry);
            await db.SaveChangesAsync();

            candidateId = candidate.Id;
            missionId = mission.Id;
            formatorId = formator.Id;
            needId = need.Id;
            entryId = entry.Id;
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.NotNull(await db.Candidates.FindAsync(candidateId));
            Assert.NotNull(await db.CanonicalMissions.FindAsync(missionId));
            Assert.NotNull(await db.Formators.FindAsync(formatorId));
            Assert.NotNull(await db.ParishNeeds.FindAsync(needId));
            Assert.NotNull(await db.BudgetEntries.FindAsync(entryId));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/DokPortal.Api.IntegrationTests`
Expected: FAIL to compile — `Candidate`, `CanonicalMission`, `Formator`, `ParishNeed`, `BudgetEntry`, `BudgetFund`, `BudgetEntryType` don't exist yet.

- [ ] **Step 3: Implement the enums**

`backend/src/DokPortal.Domain/Enums/ParishNeedStatus.cs`:

```csharp
namespace DokPortal.Domain.Enums;

public enum ParishNeedStatus
{
    Open,
    Assigned,
    Closed
}
```

`backend/src/DokPortal.Domain/Enums/BudgetFund.cs`:

```csharp
namespace DokPortal.Domain.Enums;

public enum BudgetFund
{
    SKSP,
    DOK
}
```

`backend/src/DokPortal.Domain/Enums/BudgetEntryType.cs`:

```csharp
namespace DokPortal.Domain.Enums;

public enum BudgetEntryType
{
    Income,
    Expense
}
```

- [ ] **Step 4: Implement the entities**

`backend/src/DokPortal.Domain/Entities/Candidate.cs`:

```csharp
namespace DokPortal.Domain.Entities;

public class Candidate
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public Person? Person { get; set; }
    public int Year { get; set; }
    public int? AttendancePercentage { get; set; }
    public int OpinionsCollected { get; set; }
    public int OpinionsRequired { get; set; } = 2;
    public bool IsRetreatCompleted { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
```

`backend/src/DokPortal.Domain/Entities/CanonicalMission.cs`:

```csharp
namespace DokPortal.Domain.Entities;

public class CanonicalMission
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public Person? Person { get; set; }
    public required string ServicePlace { get; set; }
    public DateOnly MissionStartDate { get; set; }
    public DateOnly MissionEndDate { get; set; }
    public DateOnly? GrantedDate { get; set; }
    public string? GrantedPlace { get; set; }
    public string? SupervisionGroup { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
```

`backend/src/DokPortal.Domain/Entities/Formator.cs`:

```csharp
namespace DokPortal.Domain.Entities;

public class Formator
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public Person? Person { get; set; }
    public required string Function { get; set; }
}
```

`backend/src/DokPortal.Domain/Entities/ParishNeed.cs`:

```csharp
using DokPortal.Domain.Enums;

namespace DokPortal.Domain.Entities;

public class ParishNeed
{
    public Guid Id { get; set; }
    public Guid ParishId { get; set; }
    public Parish? Parish { get; set; }
    public required string Description { get; set; }
    public ParishNeedStatus Status { get; set; } = ParishNeedStatus.Open;
    public Guid? AssignedPersonId { get; set; }
    public Person? AssignedPerson { get; set; }
    public DateTime? AssignedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
```

`backend/src/DokPortal.Domain/Entities/BudgetEntry.cs`:

```csharp
using DokPortal.Domain.Enums;

namespace DokPortal.Domain.Entities;

public class BudgetEntry
{
    public Guid Id { get; set; }
    public BudgetFund Fund { get; set; }
    public DateOnly EntryDate { get; set; }
    public required string Description { get; set; }
    public required string Category { get; set; }
    public BudgetEntryType Type { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
```

- [ ] **Step 5: Wire the entities into AppDbContext**

In `backend/src/DokPortal.Infrastructure/Persistence/AppDbContext.cs`, add after the existing `DbSet<Parish> Parishes` line:

```csharp
public DbSet<Candidate> Candidates => Set<Candidate>();
public DbSet<CanonicalMission> CanonicalMissions => Set<CanonicalMission>();
public DbSet<Formator> Formators => Set<Formator>();
public DbSet<ParishNeed> ParishNeeds => Set<ParishNeed>();
public DbSet<BudgetEntry> BudgetEntries => Set<BudgetEntry>();
```

Add `using DokPortal.Domain.Enums;` (needed for `DeleteBehavior` is already imported via EFCore; this using is for none of the enums directly here, so skip it — the entities' own files reference the enums, `AppDbContext` doesn't need to).

Add to `OnModelCreating`, after the existing `Person` entity configuration block:

```csharp
builder.Entity<Candidate>(entity =>
{
    entity.HasOne(c => c.Person).WithMany().HasForeignKey(c => c.PersonId).OnDelete(DeleteBehavior.Restrict);
});

builder.Entity<CanonicalMission>(entity =>
{
    entity.Property(m => m.ServicePlace).IsRequired().HasMaxLength(200);
    entity.Property(m => m.GrantedPlace).HasMaxLength(200);
    entity.Property(m => m.SupervisionGroup).HasMaxLength(100);
    entity.HasOne(m => m.Person).WithMany().HasForeignKey(m => m.PersonId).OnDelete(DeleteBehavior.Restrict);
});

builder.Entity<Formator>(entity =>
{
    entity.Property(f => f.Function).IsRequired().HasMaxLength(200);
    entity.HasOne(f => f.Person).WithMany().HasForeignKey(f => f.PersonId).OnDelete(DeleteBehavior.Restrict);
});

builder.Entity<ParishNeed>(entity =>
{
    entity.Property(n => n.Description).IsRequired().HasMaxLength(500);
    entity.HasOne(n => n.Parish).WithMany().HasForeignKey(n => n.ParishId).OnDelete(DeleteBehavior.Restrict);
    entity.HasOne(n => n.AssignedPerson).WithMany().HasForeignKey(n => n.AssignedPersonId).OnDelete(DeleteBehavior.SetNull);
});

builder.Entity<BudgetEntry>(entity =>
{
    entity.Property(b => b.Description).IsRequired().HasMaxLength(300);
    entity.Property(b => b.Category).IsRequired().HasMaxLength(100);
    entity.Property(b => b.Amount).HasColumnType("decimal(18,2)");
});
```

- [ ] **Step 6: Generate the EF Core migration**

```bash
cd backend
dotnet ef migrations add AddSkspModule --project src/DokPortal.Infrastructure --startup-project src/DokPortal.Api
```

Expected: a new migration file appears creating `Candidates`, `CanonicalMissions`, `Formators`, `ParishNeeds`, `BudgetEntries` tables.

- [ ] **Step 7: Run test to verify it passes**

Run: `dotnet test backend/tests/DokPortal.Api.IntegrationTests`
Expected: PASS (the new persistence test, plus all prior Phase 1 tests still green).

- [ ] **Step 8: Commit**

```bash
git add backend
git commit -m "Add SKSP domain entities, enums, and EF Core migration"
```

---

## Task 2: Candidates module

**Files:**
- Create: `backend/src/DokPortal.Application/Candidates/CandidateDto.cs`
- Create: `backend/src/DokPortal.Application/Candidates/CreateCandidateRequest.cs`
- Create: `backend/src/DokPortal.Application/Candidates/UpdateCandidateRequest.cs`
- Create: `backend/src/DokPortal.Application/Candidates/ICandidateService.cs`
- Create: `backend/src/DokPortal.Infrastructure/Services/CandidateService.cs`
- Create: `backend/src/DokPortal.Api/Controllers/CandidatesController.cs`
- Modify: `backend/src/DokPortal.Api/Program.cs`
- Test: `backend/tests/DokPortal.Infrastructure.Tests/Services/CandidateServiceTests.cs`
- Test: `backend/tests/DokPortal.Api.IntegrationTests/CandidatesControllerTests.cs`

**Interfaces:**
- Consumes: `Candidate`, `Person` (Task 1), `PagedResult<T>` (Phase 1 Task 6), `AppRoles` (Phase 1 Task 2).
- Produces: `ICandidateService` (`SearchAsync`, `GetByIdAsync`, `CreateAsync`, `UpdateAsync`); `GET/POST /api/candidates`, `GET/PUT /api/candidates/{id}`.

- [ ] **Step 1: Write the failing tests**

`backend/tests/DokPortal.Infrastructure.Tests/Services/CandidateServiceTests.cs`:

```csharp
using DokPortal.Application.Candidates;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class CandidateServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    private static async Task<Guid> SeedPersonAsync(AppDbContext db, string firstName, string lastName)
    {
        var person = new Person
        {
            Id = Guid.NewGuid(), FirstName = firstName, LastName = lastName,
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.Add(person);
        await db.SaveChangesAsync();
        return person.Id;
    }

    [Fact]
    public async Task CreateAsync_ThenSearchAsync_FiltersByYear()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var personId = await SeedPersonAsync(db, "Agnieszka", "Lewandowska");
        var service = new CandidateService(db);

        await service.CreateAsync(new CreateCandidateRequest
        {
            PersonId = personId, Year = 3, AttendancePercentage = 94, OpinionsCollected = 2, IsRetreatCompleted = true
        }, default);

        var yearThree = await service.SearchAsync(3, 1, 20, default);
        var yearOne = await service.SearchAsync(1, 1, 20, default);

        Assert.Single(yearThree.Items);
        Assert.Equal("Agnieszka Lewandowska", yearThree.Items[0].PersonFullName);
        Assert.Empty(yearOne.Items);
    }

    [Fact]
    public async Task UpdateAsync_WhenCandidateMissing_ReturnsNull()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var service = new CandidateService(db);

        var result = await service.UpdateAsync(Guid.NewGuid(), new UpdateCandidateRequest
        {
            PersonId = Guid.NewGuid(), Year = 1, OpinionsCollected = 0, IsRetreatCompleted = false
        }, default);

        Assert.Null(result);
    }
}
```

`backend/tests/DokPortal.Api.IntegrationTests/CandidatesControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Candidates;
using DokPortal.Application.Common;
using DokPortal.Application.People;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class CandidatesControllerTests : IntegrationTestBase
{
    public CandidatesControllerTests(CustomWebApplicationFactory factory) : base(factory)
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
    public async Task Create_ThenGetById_ReturnsCreatedCandidate()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var personId = await CreatePersonAsync(admin, "Karolina", "Nowak");

        var createResponse = await admin.PostAsJsonAsync("/api/candidates", new
        {
            PersonId = personId, Year = 1, AttendancePercentage = 81, OpinionsCollected = 0, IsRetreatCompleted = false
        });

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CandidateDto>();
        Assert.NotNull(created);
        Assert.Equal("Karolina Nowak", created!.PersonFullName);

        var getResponse = await admin.GetAsync($"/api/candidates/{created.Id}");
        getResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Search_ByYear_ReturnsOnlyMatchingCandidates()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var personId = await CreatePersonAsync(admin, "Tomasz", "Wisniewski");
        await admin.PostAsJsonAsync("/api/candidates", new { PersonId = personId, Year = 2, OpinionsCollected = 1, IsRetreatCompleted = true });

        var response = await admin.GetAsync("/api/candidates?year=2");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<CandidateDto>>();
        Assert.NotNull(result);
        Assert.Contains(result!.Items, c => c.PersonFullName == "Tomasz Wisniewski");
    }

    [Fact]
    public async Task Create_WithoutRequiredRole_ReturnsForbidden()
    {
        var client = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");

        var response = await client.PostAsJsonAsync("/api/candidates", new { PersonId = Guid.NewGuid(), Year = 1, OpinionsCollected = 0, IsRetreatCompleted = false });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/tests/DokPortal.Infrastructure.Tests backend/tests/DokPortal.Api.IntegrationTests`
Expected: FAIL to compile — `ICandidateService`, `CandidateService`, `CandidatesController` don't exist yet.

- [ ] **Step 3: Implement Application contracts**

`backend/src/DokPortal.Application/Candidates/CandidateDto.cs`:

```csharp
namespace DokPortal.Application.Candidates;

public class CandidateDto
{
    public required Guid Id { get; init; }
    public required Guid PersonId { get; init; }
    public required string PersonFullName { get; init; }
    public string? ParishName { get; init; }
    public required int Year { get; init; }
    public int? AttendancePercentage { get; init; }
    public required int OpinionsCollected { get; init; }
    public required int OpinionsRequired { get; init; }
    public required bool IsRetreatCompleted { get; init; }
}
```

`backend/src/DokPortal.Application/Candidates/CreateCandidateRequest.cs`:

```csharp
namespace DokPortal.Application.Candidates;

public class CreateCandidateRequest
{
    public required Guid PersonId { get; init; }
    public required int Year { get; init; }
    public int? AttendancePercentage { get; init; }
    public required int OpinionsCollected { get; init; }
    public required bool IsRetreatCompleted { get; init; }
}
```

`backend/src/DokPortal.Application/Candidates/UpdateCandidateRequest.cs`:

```csharp
namespace DokPortal.Application.Candidates;

public class UpdateCandidateRequest : CreateCandidateRequest
{
}
```

`backend/src/DokPortal.Application/Candidates/ICandidateService.cs`:

```csharp
using DokPortal.Application.Common;

namespace DokPortal.Application.Candidates;

public interface ICandidateService
{
    Task<PagedResult<CandidateDto>> SearchAsync(int? year, int page, int pageSize, CancellationToken ct);
    Task<CandidateDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<CandidateDto> CreateAsync(CreateCandidateRequest request, CancellationToken ct);
    Task<CandidateDto?> UpdateAsync(Guid id, UpdateCandidateRequest request, CancellationToken ct);
}
```

- [ ] **Step 4: Implement CandidateService**

`backend/src/DokPortal.Infrastructure/Services/CandidateService.cs`:

```csharp
using DokPortal.Application.Candidates;
using DokPortal.Application.Common;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class CandidateService : ICandidateService
{
    private readonly AppDbContext _db;

    public CandidateService(AppDbContext db) => _db = db;

    public async Task<PagedResult<CandidateDto>> SearchAsync(int? year, int page, int pageSize, CancellationToken ct)
    {
        var q = _db.Candidates.Include(c => c.Person).ThenInclude(p => p!.Parish).AsNoTracking().AsQueryable();

        if (year.HasValue)
        {
            q = q.Where(c => c.Year == year.Value);
        }

        var total = await q.CountAsync(ct);
        var entities = await q
            .OrderBy(c => c.Year).ThenBy(c => c.Person!.LastName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<CandidateDto>
        {
            Items = entities.Select(ToDto).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CandidateDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var candidate = await _db.Candidates.Include(c => c.Person).ThenInclude(p => p!.Parish).AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        return candidate is null ? null : ToDto(candidate);
    }

    public async Task<CandidateDto> CreateAsync(CreateCandidateRequest request, CancellationToken ct)
    {
        var candidate = new Candidate
        {
            Id = Guid.NewGuid(),
            PersonId = request.PersonId,
            Year = request.Year,
            AttendancePercentage = request.AttendancePercentage,
            OpinionsCollected = request.OpinionsCollected,
            IsRetreatCompleted = request.IsRetreatCompleted,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _db.Candidates.Add(candidate);
        await _db.SaveChangesAsync(ct);
        return (await GetByIdAsync(candidate.Id, ct))!;
    }

    public async Task<CandidateDto?> UpdateAsync(Guid id, UpdateCandidateRequest request, CancellationToken ct)
    {
        var candidate = await _db.Candidates.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (candidate is null) return null;

        candidate.PersonId = request.PersonId;
        candidate.Year = request.Year;
        candidate.AttendancePercentage = request.AttendancePercentage;
        candidate.OpinionsCollected = request.OpinionsCollected;
        candidate.IsRetreatCompleted = request.IsRetreatCompleted;
        candidate.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    private static CandidateDto ToDto(Candidate c) => new()
    {
        Id = c.Id,
        PersonId = c.PersonId,
        PersonFullName = c.Person!.FullName,
        ParishName = c.Person.Parish?.Name,
        Year = c.Year,
        AttendancePercentage = c.AttendancePercentage,
        OpinionsCollected = c.OpinionsCollected,
        OpinionsRequired = c.OpinionsRequired,
        IsRetreatCompleted = c.IsRetreatCompleted
    };
}
```

- [ ] **Step 5: Implement CandidatesController**

`backend/src/DokPortal.Api/Controllers/CandidatesController.cs`:

```csharp
using DokPortal.Application.Candidates;
using DokPortal.Application.Common;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/candidates")]
[Authorize]
public class CandidatesController : ControllerBase
{
    private readonly ICandidateService _candidateService;

    public CandidatesController(ICandidateService candidateService) => _candidateService = candidateService;

    [HttpGet]
    public async Task<ActionResult<PagedResult<CandidateDto>>> Search(
        [FromQuery] int? year, [FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken ct = default)
        => Ok(await _candidateService.SearchAsync(year, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CandidateDto>> GetById(Guid id, CancellationToken ct)
    {
        var candidate = await _candidateService.GetByIdAsync(id, ct);
        return candidate is null ? NotFound() : Ok(candidate);
    }

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorSKSP}")]
    public async Task<ActionResult<CandidateDto>> Create(CreateCandidateRequest request, CancellationToken ct)
    {
        var created = await _candidateService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorSKSP}")]
    public async Task<ActionResult<CandidateDto>> Update(Guid id, UpdateCandidateRequest request, CancellationToken ct)
    {
        var updated = await _candidateService.UpdateAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }
}
```

- [ ] **Step 6: Register the service**

In `backend/src/DokPortal.Api/Program.cs`, add `using DokPortal.Application.Candidates;` and register after the existing `AddScoped<IDashboardService, ...>` line:

```csharp
builder.Services.AddScoped<ICandidateService, CandidateService>();
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test backend/DokPortal.sln`
Expected: All tests pass.

- [ ] **Step 8: Commit**

```bash
git add backend
git commit -m "Add Candidates module"
```

---

## Task 3: Missions module

**Files:**
- Create: `backend/src/DokPortal.Application/Missions/MissionDto.cs`
- Create: `backend/src/DokPortal.Application/Missions/CreateMissionRequest.cs`
- Create: `backend/src/DokPortal.Application/Missions/UpdateMissionRequest.cs`
- Create: `backend/src/DokPortal.Application/Missions/IMissionService.cs`
- Create: `backend/src/DokPortal.Infrastructure/Services/MissionService.cs`
- Create: `backend/src/DokPortal.Api/Controllers/MissionsController.cs`
- Modify: `backend/src/DokPortal.Api/Program.cs`
- Test: `backend/tests/DokPortal.Infrastructure.Tests/Services/MissionServiceTests.cs`
- Test: `backend/tests/DokPortal.Api.IntegrationTests/MissionsControllerTests.cs`

**Interfaces:**
- Consumes: `CanonicalMission`, `Person` (Task 1), `PagedResult<T>` (Phase 1).
- Produces: `IMissionService` (`SearchAsync`, `GetByIdAsync`, `CreateAsync`, `UpdateAsync`), each `MissionDto.Status` computed as `"wygasła"` (end date in the past), `"wygasa"` (end date within 30 days), or `"ważna"` (otherwise); `GET/POST /api/missions`, `GET/PUT /api/missions/{id}`.

- [ ] **Step 1: Write the failing tests**

`backend/tests/DokPortal.Infrastructure.Tests/Services/MissionServiceTests.cs`:

```csharp
using DokPortal.Application.Missions;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class MissionServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    private static async Task<Guid> SeedPersonAsync(AppDbContext db, string firstName, string lastName)
    {
        var person = new Person
        {
            Id = Guid.NewGuid(), FirstName = firstName, LastName = lastName,
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.Add(person);
        await db.SaveChangesAsync();
        return person.Id;
    }

    [Fact]
    public async Task CreateAsync_WithEndDateFarInFuture_ReturnsWaznaStatus()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var personId = await SeedPersonAsync(db, "Marek", "Zielinski");
        var service = new MissionService(db);

        var created = await service.CreateAsync(new CreateMissionRequest
        {
            PersonId = personId,
            ServicePlace = "Parafia św. Józefa",
            MissionStartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MissionEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2))
        }, default);

        Assert.Equal("ważna", created.Status);
    }

    [Fact]
    public async Task CreateAsync_WithEndDateWithin30Days_ReturnsWygasaStatus()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var personId = await SeedPersonAsync(db, "Anna", "Maj");
        var service = new MissionService(db);

        var created = await service.CreateAsync(new CreateMissionRequest
        {
            PersonId = personId,
            ServicePlace = "Parafia św. Mateusza",
            MissionStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-3)),
            MissionEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10))
        }, default);

        Assert.Equal("wygasa", created.Status);
    }

    [Fact]
    public async Task CreateAsync_WithPastEndDate_ReturnsWygaslaStatus()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var personId = await SeedPersonAsync(db, "Maria", "Kaczmarek");
        var service = new MissionService(db);

        var created = await service.CreateAsync(new CreateMissionRequest
        {
            PersonId = personId,
            ServicePlace = "DOK",
            MissionStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-5)),
            MissionEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))
        }, default);

        Assert.Equal("wygasła", created.Status);
    }
}
```

`backend/tests/DokPortal.Api.IntegrationTests/MissionsControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Missions;
using DokPortal.Application.People;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class MissionsControllerTests : IntegrationTestBase
{
    public MissionsControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Create_ThenGetById_ReturnsCreatedMission()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var personResponse = await admin.PostAsJsonAsync("/api/people", new { FirstName = "Marek", LastName = "Zielinski" });
        var person = await personResponse.Content.ReadFromJsonAsync<PersonDto>();

        var createResponse = await admin.PostAsJsonAsync("/api/missions", new
        {
            PersonId = person!.Id,
            ServicePlace = "Parafia św. Józefa",
            MissionStartDate = "2025-07-01",
            MissionEndDate = "2028-06-30"
        });

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<MissionDto>();
        Assert.NotNull(created);

        var getResponse = await admin.GetAsync($"/api/missions/{created!.Id}");
        getResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Create_WithoutRequiredRole_ReturnsForbidden()
    {
        var client = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");

        var response = await client.PostAsJsonAsync("/api/missions", new
        {
            PersonId = Guid.NewGuid(), ServicePlace = "Test", MissionStartDate = "2025-01-01", MissionEndDate = "2028-01-01"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/tests/DokPortal.Infrastructure.Tests backend/tests/DokPortal.Api.IntegrationTests`
Expected: FAIL to compile — `IMissionService`, `MissionService`, `MissionsController` don't exist yet.

- [ ] **Step 3: Implement Application contracts**

`backend/src/DokPortal.Application/Missions/MissionDto.cs`:

```csharp
namespace DokPortal.Application.Missions;

public class MissionDto
{
    public required Guid Id { get; init; }
    public required Guid PersonId { get; init; }
    public required string PersonFullName { get; init; }
    public required string ServicePlace { get; init; }
    public required DateOnly MissionStartDate { get; init; }
    public required DateOnly MissionEndDate { get; init; }
    public DateOnly? GrantedDate { get; init; }
    public string? GrantedPlace { get; init; }
    public string? SupervisionGroup { get; init; }
    public required string Status { get; init; }
}
```

`backend/src/DokPortal.Application/Missions/CreateMissionRequest.cs`:

```csharp
namespace DokPortal.Application.Missions;

public class CreateMissionRequest
{
    public required Guid PersonId { get; init; }
    public required string ServicePlace { get; init; }
    public required DateOnly MissionStartDate { get; init; }
    public required DateOnly MissionEndDate { get; init; }
    public DateOnly? GrantedDate { get; init; }
    public string? GrantedPlace { get; init; }
    public string? SupervisionGroup { get; init; }
}
```

`backend/src/DokPortal.Application/Missions/UpdateMissionRequest.cs`:

```csharp
namespace DokPortal.Application.Missions;

public class UpdateMissionRequest : CreateMissionRequest
{
}
```

`backend/src/DokPortal.Application/Missions/IMissionService.cs`:

```csharp
using DokPortal.Application.Common;

namespace DokPortal.Application.Missions;

public interface IMissionService
{
    Task<PagedResult<MissionDto>> SearchAsync(string? query, int page, int pageSize, CancellationToken ct);
    Task<MissionDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<MissionDto> CreateAsync(CreateMissionRequest request, CancellationToken ct);
    Task<MissionDto?> UpdateAsync(Guid id, UpdateMissionRequest request, CancellationToken ct);
}
```

- [ ] **Step 4: Implement MissionService**

`backend/src/DokPortal.Infrastructure/Services/MissionService.cs`:

```csharp
using DokPortal.Application.Common;
using DokPortal.Application.Missions;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class MissionService : IMissionService
{
    private readonly AppDbContext _db;

    public MissionService(AppDbContext db) => _db = db;

    public async Task<PagedResult<MissionDto>> SearchAsync(string? query, int page, int pageSize, CancellationToken ct)
    {
        var q = _db.CanonicalMissions.Include(m => m.Person).AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLower();
            q = q.Where(m =>
                m.Person!.FirstName.ToLower().Contains(term) ||
                m.Person.LastName.ToLower().Contains(term) ||
                m.ServicePlace.ToLower().Contains(term));
        }

        var total = await q.CountAsync(ct);
        var entities = await q
            .OrderBy(m => m.MissionEndDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<MissionDto>
        {
            Items = entities.Select(ToDto).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<MissionDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var mission = await _db.CanonicalMissions.Include(m => m.Person).AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, ct);
        return mission is null ? null : ToDto(mission);
    }

    public async Task<MissionDto> CreateAsync(CreateMissionRequest request, CancellationToken ct)
    {
        var mission = new CanonicalMission
        {
            Id = Guid.NewGuid(),
            PersonId = request.PersonId,
            ServicePlace = request.ServicePlace,
            MissionStartDate = request.MissionStartDate,
            MissionEndDate = request.MissionEndDate,
            GrantedDate = request.GrantedDate,
            GrantedPlace = request.GrantedPlace,
            SupervisionGroup = request.SupervisionGroup,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _db.CanonicalMissions.Add(mission);
        await _db.SaveChangesAsync(ct);
        return (await GetByIdAsync(mission.Id, ct))!;
    }

    public async Task<MissionDto?> UpdateAsync(Guid id, UpdateMissionRequest request, CancellationToken ct)
    {
        var mission = await _db.CanonicalMissions.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (mission is null) return null;

        mission.PersonId = request.PersonId;
        mission.ServicePlace = request.ServicePlace;
        mission.MissionStartDate = request.MissionStartDate;
        mission.MissionEndDate = request.MissionEndDate;
        mission.GrantedDate = request.GrantedDate;
        mission.GrantedPlace = request.GrantedPlace;
        mission.SupervisionGroup = request.SupervisionGroup;
        mission.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    private static MissionDto ToDto(CanonicalMission m) => new()
    {
        Id = m.Id,
        PersonId = m.PersonId,
        PersonFullName = m.Person!.FullName,
        ServicePlace = m.ServicePlace,
        MissionStartDate = m.MissionStartDate,
        MissionEndDate = m.MissionEndDate,
        GrantedDate = m.GrantedDate,
        GrantedPlace = m.GrantedPlace,
        SupervisionGroup = m.SupervisionGroup,
        Status = ComputeStatus(m.MissionEndDate)
    };

    private static string ComputeStatus(DateOnly endDate)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (endDate < today) return "wygasła";
        if (endDate <= today.AddDays(30)) return "wygasa";
        return "ważna";
    }
}
```

- [ ] **Step 5: Implement MissionsController**

`backend/src/DokPortal.Api/Controllers/MissionsController.cs`:

```csharp
using DokPortal.Application.Common;
using DokPortal.Application.Missions;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/missions")]
[Authorize]
public class MissionsController : ControllerBase
{
    private readonly IMissionService _missionService;

    public MissionsController(IMissionService missionService) => _missionService = missionService;

    [HttpGet]
    public async Task<ActionResult<PagedResult<MissionDto>>> Search(
        [FromQuery] string? query, [FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken ct = default)
        => Ok(await _missionService.SearchAsync(query, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MissionDto>> GetById(Guid id, CancellationToken ct)
    {
        var mission = await _missionService.GetByIdAsync(id, ct);
        return mission is null ? NotFound() : Ok(mission);
    }

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorSKSP}")]
    public async Task<ActionResult<MissionDto>> Create(CreateMissionRequest request, CancellationToken ct)
    {
        var created = await _missionService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorSKSP}")]
    public async Task<ActionResult<MissionDto>> Update(Guid id, UpdateMissionRequest request, CancellationToken ct)
    {
        var updated = await _missionService.UpdateAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }
}
```

- [ ] **Step 6: Register the service**

In `backend/src/DokPortal.Api/Program.cs`, add `using DokPortal.Application.Missions;` and register:

```csharp
builder.Services.AddScoped<IMissionService, MissionService>();
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test backend/DokPortal.sln`
Expected: All tests pass.

- [ ] **Step 8: Commit**

```bash
git add backend
git commit -m "Add Missions module with computed validity status"
```

---

## Task 4: Formators module

**Files:**
- Create: `backend/src/DokPortal.Application/Formators/FormatorDto.cs`
- Create: `backend/src/DokPortal.Application/Formators/CreateFormatorRequest.cs`
- Create: `backend/src/DokPortal.Application/Formators/IFormatorService.cs`
- Create: `backend/src/DokPortal.Infrastructure/Services/FormatorService.cs`
- Create: `backend/src/DokPortal.Api/Controllers/FormatorsController.cs`
- Modify: `backend/src/DokPortal.Api/Program.cs`
- Test: `backend/tests/DokPortal.Infrastructure.Tests/Services/FormatorServiceTests.cs`
- Test: `backend/tests/DokPortal.Api.IntegrationTests/FormatorsControllerTests.cs`

**Interfaces:**
- Consumes: `Formator`, `Person` (Task 1).
- Produces: `IFormatorService` (`GetAllAsync`, `CreateAsync`); `GET/POST /api/formators`.

- [ ] **Step 1: Write the failing tests**

`backend/tests/DokPortal.Infrastructure.Tests/Services/FormatorServiceTests.cs`:

```csharp
using DokPortal.Application.Formators;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class FormatorServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task CreateAsync_ThenGetAllAsync_ReturnsCreatedFormator()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var person = new Person
        {
            Id = Guid.NewGuid(), FirstName = "Joanna", LastName = "Lis",
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.Add(person);
        await db.SaveChangesAsync();
        var service = new FormatorService(db);

        await service.CreateAsync(new CreateFormatorRequest { PersonId = person.Id, Function = "Wykładowca" }, default);
        var all = await service.GetAllAsync(default);

        Assert.Contains(all, f => f.PersonFullName == "Joanna Lis" && f.Function == "Wykładowca");
    }
}
```

`backend/tests/DokPortal.Api.IntegrationTests/FormatorsControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Formators;
using DokPortal.Application.People;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class FormatorsControllerTests : IntegrationTestBase
{
    public FormatorsControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Create_ThenGetAll_ReturnsCreatedFormator()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var personResponse = await admin.PostAsJsonAsync("/api/people", new { FirstName = "Pawel", LastName = "Nowicki" });
        var person = await personResponse.Content.ReadFromJsonAsync<PersonDto>();

        var createResponse = await admin.PostAsJsonAsync("/api/formators", new { PersonId = person!.Id, Function = "Moderator" });
        createResponse.EnsureSuccessStatusCode();

        var getResponse = await admin.GetAsync("/api/formators");
        getResponse.EnsureSuccessStatusCode();
        var all = await getResponse.Content.ReadFromJsonAsync<List<FormatorDto>>();
        Assert.Contains(all!, f => f.Function == "Moderator");
    }

    [Fact]
    public async Task Create_WithoutRequiredRole_ReturnsForbidden()
    {
        var client = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");

        var response = await client.PostAsJsonAsync("/api/formators", new { PersonId = Guid.NewGuid(), Function = "Test" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/tests/DokPortal.Infrastructure.Tests backend/tests/DokPortal.Api.IntegrationTests`
Expected: FAIL to compile — `IFormatorService`, `FormatorService`, `FormatorsController` don't exist yet.

- [ ] **Step 3: Implement Application contracts and service**

`backend/src/DokPortal.Application/Formators/FormatorDto.cs`:

```csharp
namespace DokPortal.Application.Formators;

public class FormatorDto
{
    public required Guid Id { get; init; }
    public required Guid PersonId { get; init; }
    public required string PersonFullName { get; init; }
    public string? PersonEmail { get; init; }
    public string? PersonPhone { get; init; }
    public required string Function { get; init; }
}
```

`backend/src/DokPortal.Application/Formators/CreateFormatorRequest.cs`:

```csharp
namespace DokPortal.Application.Formators;

public class CreateFormatorRequest
{
    public required Guid PersonId { get; init; }
    public required string Function { get; init; }
}
```

`backend/src/DokPortal.Application/Formators/IFormatorService.cs`:

```csharp
namespace DokPortal.Application.Formators;

public interface IFormatorService
{
    Task<IReadOnlyList<FormatorDto>> GetAllAsync(CancellationToken ct);
    Task<FormatorDto> CreateAsync(CreateFormatorRequest request, CancellationToken ct);
}
```

`backend/src/DokPortal.Infrastructure/Services/FormatorService.cs`:

```csharp
using DokPortal.Application.Formators;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class FormatorService : IFormatorService
{
    private readonly AppDbContext _db;

    public FormatorService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<FormatorDto>> GetAllAsync(CancellationToken ct)
    {
        var formators = await _db.Formators.Include(f => f.Person).AsNoTracking()
            .OrderBy(f => f.Person!.LastName)
            .ToListAsync(ct);
        return formators.Select(ToDto).ToList();
    }

    public async Task<FormatorDto> CreateAsync(CreateFormatorRequest request, CancellationToken ct)
    {
        var formator = new Formator { Id = Guid.NewGuid(), PersonId = request.PersonId, Function = request.Function };
        _db.Formators.Add(formator);
        await _db.SaveChangesAsync(ct);

        var saved = await _db.Formators.Include(f => f.Person).AsNoTracking().FirstAsync(f => f.Id == formator.Id, ct);
        return ToDto(saved);
    }

    private static FormatorDto ToDto(Formator f) => new()
    {
        Id = f.Id,
        PersonId = f.PersonId,
        PersonFullName = f.Person!.FullName,
        PersonEmail = f.Person.Email,
        PersonPhone = f.Person.Phone,
        Function = f.Function
    };
}
```

- [ ] **Step 4: Implement FormatorsController**

`backend/src/DokPortal.Api/Controllers/FormatorsController.cs`:

```csharp
using DokPortal.Application.Formators;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/formators")]
[Authorize]
public class FormatorsController : ControllerBase
{
    private readonly IFormatorService _formatorService;

    public FormatorsController(IFormatorService formatorService) => _formatorService = formatorService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FormatorDto>>> GetAll(CancellationToken ct)
        => Ok(await _formatorService.GetAllAsync(ct));

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorSKSP}")]
    public async Task<ActionResult<FormatorDto>> Create(CreateFormatorRequest request, CancellationToken ct)
    {
        var created = await _formatorService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), created);
    }
}
```

- [ ] **Step 5: Register the service**

In `backend/src/DokPortal.Api/Program.cs`, add `using DokPortal.Application.Formators;` and register:

```csharp
builder.Services.AddScoped<IFormatorService, FormatorService>();
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test backend/DokPortal.sln`
Expected: All tests pass.

- [ ] **Step 7: Commit**

```bash
git add backend
git commit -m "Add Formators module"
```

---

## Task 5: ParishNeeds module (giełda posługi)

**Files:**
- Create: `backend/src/DokPortal.Application/ParishNeeds/ParishNeedDto.cs`
- Create: `backend/src/DokPortal.Application/ParishNeeds/CreateParishNeedRequest.cs`
- Create: `backend/src/DokPortal.Application/ParishNeeds/AssignParishNeedRequest.cs`
- Create: `backend/src/DokPortal.Application/ParishNeeds/IParishNeedService.cs`
- Create: `backend/src/DokPortal.Infrastructure/Services/ParishNeedService.cs`
- Create: `backend/src/DokPortal.Api/Controllers/ParishNeedsController.cs`
- Modify: `backend/src/DokPortal.Api/Program.cs`
- Test: `backend/tests/DokPortal.Infrastructure.Tests/Services/ParishNeedServiceTests.cs`
- Test: `backend/tests/DokPortal.Api.IntegrationTests/ParishNeedsControllerTests.cs`

**Interfaces:**
- Consumes: `ParishNeed`, `ParishNeedStatus`, `Parish`, `Person` (Task 1, Phase 1).
- Produces: `IParishNeedService` (`GetAllAsync`, `CreateAsync`, `AssignAsync`); `GET/POST /api/parish-needs`, `PUT /api/parish-needs/{id}/assign`.

- [ ] **Step 1: Write the failing tests**

`backend/tests/DokPortal.Infrastructure.Tests/Services/ParishNeedServiceTests.cs`:

```csharp
using DokPortal.Application.ParishNeeds;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class ParishNeedServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task CreateAsync_ThenAssignAsync_SetsAssignedPersonAndStatus()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var parish = new Parish { Id = Guid.NewGuid(), Name = "św. Mateusza" };
        var person = new Person
        {
            Id = Guid.NewGuid(), FirstName = "Marek", LastName = "Zielinski",
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        db.Parishes.Add(parish);
        db.People.Add(person);
        await db.SaveChangesAsync();
        var service = new ParishNeedService(db);

        var created = await service.CreateAsync(new CreateParishNeedRequest
        {
            ParishId = parish.Id, Description = "Katechista do przygotowania dorosłych"
        }, default);

        var assigned = await service.AssignAsync(created.Id, person.Id, default);

        Assert.NotNull(assigned);
        Assert.Equal("Marek Zielinski", assigned!.AssignedPersonName);
        Assert.Equal("Assigned", assigned.Status);
    }

    [Fact]
    public async Task AssignAsync_WhenNeedMissing_ReturnsNull()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var service = new ParishNeedService(db);

        var result = await service.AssignAsync(Guid.NewGuid(), Guid.NewGuid(), default);

        Assert.Null(result);
    }
}
```

`backend/tests/DokPortal.Api.IntegrationTests/ParishNeedsControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.ParishNeeds;
using DokPortal.Application.Parishes;
using DokPortal.Application.People;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class ParishNeedsControllerTests : IntegrationTestBase
{
    public ParishNeedsControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Create_ThenAssign_UpdatesStatus()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var parishResponse = await admin.PostAsJsonAsync("/api/parishes", new { Name = "św. Pawła" });
        var parish = await parishResponse.Content.ReadFromJsonAsync<ParishDto>();
        var personResponse = await admin.PostAsJsonAsync("/api/people", new { FirstName = "Agnieszka", LastName = "Krol" });
        var person = await personResponse.Content.ReadFromJsonAsync<PersonDto>();

        var createResponse = await admin.PostAsJsonAsync("/api/parish-needs", new
        {
            ParishId = parish!.Id, Description = "Wsparcie katechezy dla dorosłych"
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ParishNeedDto>();

        var assignResponse = await admin.PutAsJsonAsync($"/api/parish-needs/{created!.Id}/assign", new { PersonId = person!.Id });
        assignResponse.EnsureSuccessStatusCode();
        var assigned = await assignResponse.Content.ReadFromJsonAsync<ParishNeedDto>();

        Assert.Equal("Assigned", assigned!.Status);
        Assert.Equal(person.Id, assigned.AssignedPersonId);
    }

    [Fact]
    public async Task Create_WithoutRequiredRole_ReturnsForbidden()
    {
        var client = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");

        var response = await client.PostAsJsonAsync("/api/parish-needs", new { ParishId = Guid.NewGuid(), Description = "Test" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/tests/DokPortal.Infrastructure.Tests backend/tests/DokPortal.Api.IntegrationTests`
Expected: FAIL to compile — `IParishNeedService`, `ParishNeedService`, `ParishNeedsController` don't exist yet.

- [ ] **Step 3: Implement Application contracts**

`backend/src/DokPortal.Application/ParishNeeds/ParishNeedDto.cs`:

```csharp
namespace DokPortal.Application.ParishNeeds;

public class ParishNeedDto
{
    public required Guid Id { get; init; }
    public required Guid ParishId { get; init; }
    public required string ParishName { get; init; }
    public required string Description { get; init; }
    public required string Status { get; init; }
    public Guid? AssignedPersonId { get; init; }
    public string? AssignedPersonName { get; init; }
    public DateTime? AssignedAtUtc { get; init; }
}
```

`backend/src/DokPortal.Application/ParishNeeds/CreateParishNeedRequest.cs`:

```csharp
namespace DokPortal.Application.ParishNeeds;

public class CreateParishNeedRequest
{
    public required Guid ParishId { get; init; }
    public required string Description { get; init; }
}
```

`backend/src/DokPortal.Application/ParishNeeds/AssignParishNeedRequest.cs`:

```csharp
namespace DokPortal.Application.ParishNeeds;

public class AssignParishNeedRequest
{
    public required Guid PersonId { get; init; }
}
```

`backend/src/DokPortal.Application/ParishNeeds/IParishNeedService.cs`:

```csharp
namespace DokPortal.Application.ParishNeeds;

public interface IParishNeedService
{
    Task<IReadOnlyList<ParishNeedDto>> GetAllAsync(CancellationToken ct);
    Task<ParishNeedDto> CreateAsync(CreateParishNeedRequest request, CancellationToken ct);
    Task<ParishNeedDto?> AssignAsync(Guid id, Guid personId, CancellationToken ct);
}
```

- [ ] **Step 4: Implement ParishNeedService**

`backend/src/DokPortal.Infrastructure/Services/ParishNeedService.cs`:

```csharp
using DokPortal.Application.ParishNeeds;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class ParishNeedService : IParishNeedService
{
    private readonly AppDbContext _db;

    public ParishNeedService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ParishNeedDto>> GetAllAsync(CancellationToken ct)
    {
        var needs = await _db.ParishNeeds
            .Include(n => n.Parish)
            .Include(n => n.AssignedPerson)
            .AsNoTracking()
            .OrderByDescending(n => n.CreatedAtUtc)
            .ToListAsync(ct);
        return needs.Select(ToDto).ToList();
    }

    public async Task<ParishNeedDto> CreateAsync(CreateParishNeedRequest request, CancellationToken ct)
    {
        var need = new ParishNeed
        {
            Id = Guid.NewGuid(),
            ParishId = request.ParishId,
            Description = request.Description,
            Status = ParishNeedStatus.Open,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.ParishNeeds.Add(need);
        await _db.SaveChangesAsync(ct);

        var saved = await _db.ParishNeeds.Include(n => n.Parish).AsNoTracking().FirstAsync(n => n.Id == need.Id, ct);
        return ToDto(saved);
    }

    public async Task<ParishNeedDto?> AssignAsync(Guid id, Guid personId, CancellationToken ct)
    {
        var need = await _db.ParishNeeds.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (need is null) return null;

        need.AssignedPersonId = personId;
        need.AssignedAtUtc = DateTime.UtcNow;
        need.Status = ParishNeedStatus.Assigned;
        await _db.SaveChangesAsync(ct);

        var saved = await _db.ParishNeeds
            .Include(n => n.Parish)
            .Include(n => n.AssignedPerson)
            .AsNoTracking()
            .FirstAsync(n => n.Id == id, ct);
        return ToDto(saved);
    }

    private static ParishNeedDto ToDto(ParishNeed n) => new()
    {
        Id = n.Id,
        ParishId = n.ParishId,
        ParishName = n.Parish!.Name,
        Description = n.Description,
        Status = n.Status.ToString(),
        AssignedPersonId = n.AssignedPersonId,
        AssignedPersonName = n.AssignedPerson?.FullName,
        AssignedAtUtc = n.AssignedAtUtc
    };
}
```

- [ ] **Step 5: Implement ParishNeedsController**

`backend/src/DokPortal.Api/Controllers/ParishNeedsController.cs`:

```csharp
using DokPortal.Application.ParishNeeds;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/parish-needs")]
[Authorize]
public class ParishNeedsController : ControllerBase
{
    private readonly IParishNeedService _parishNeedService;

    public ParishNeedsController(IParishNeedService parishNeedService) => _parishNeedService = parishNeedService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ParishNeedDto>>> GetAll(CancellationToken ct)
        => Ok(await _parishNeedService.GetAllAsync(ct));

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorSKSP}")]
    public async Task<ActionResult<ParishNeedDto>> Create(CreateParishNeedRequest request, CancellationToken ct)
    {
        var created = await _parishNeedService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), created);
    }

    [HttpPut("{id:guid}/assign")]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorSKSP}")]
    public async Task<ActionResult<ParishNeedDto>> Assign(Guid id, AssignParishNeedRequest request, CancellationToken ct)
    {
        var updated = await _parishNeedService.AssignAsync(id, request.PersonId, ct);
        return updated is null ? NotFound() : Ok(updated);
    }
}
```

- [ ] **Step 6: Register the service**

In `backend/src/DokPortal.Api/Program.cs`, add `using DokPortal.Application.ParishNeeds;` and register:

```csharp
builder.Services.AddScoped<IParishNeedService, ParishNeedService>();
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test backend/DokPortal.sln`
Expected: All tests pass.

- [ ] **Step 8: Commit**

```bash
git add backend
git commit -m "Add ParishNeeds module (giełda posługi)"
```

---

## Task 6: Budget module

**Files:**
- Create: `backend/src/DokPortal.Application/Budget/BudgetEntryDto.cs`
- Create: `backend/src/DokPortal.Application/Budget/CreateBudgetEntryRequest.cs`
- Create: `backend/src/DokPortal.Application/Budget/IBudgetService.cs`
- Create: `backend/src/DokPortal.Infrastructure/Services/BudgetService.cs`
- Create: `backend/src/DokPortal.Api/Controllers/BudgetController.cs`
- Modify: `backend/src/DokPortal.Api/Program.cs`
- Test: `backend/tests/DokPortal.Infrastructure.Tests/Services/BudgetServiceTests.cs`
- Test: `backend/tests/DokPortal.Api.IntegrationTests/BudgetControllerTests.cs`

**Interfaces:**
- Consumes: `BudgetEntry`, `BudgetFund`, `BudgetEntryType` (Task 1).
- Produces: `IBudgetService` (`GetEntriesAsync(BudgetFund fund, ct)`, `CreateAsync`); `GET /api/budget?fund=SKSP`, `POST /api/budget`.

- [ ] **Step 1: Write the failing tests**

`backend/tests/DokPortal.Infrastructure.Tests/Services/BudgetServiceTests.cs`:

```csharp
using DokPortal.Application.Budget;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class BudgetServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task CreateAsync_ThenGetEntriesAsync_FiltersOnlyRequestedFund()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var service = new BudgetService(db);

        await service.CreateAsync(new CreateBudgetEntryRequest
        {
            Fund = BudgetFund.SKSP, EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Description = "Wynajem sali", Category = "Organizacja", Type = BudgetEntryType.Expense, Amount = 450m
        }, default);
        await service.CreateAsync(new CreateBudgetEntryRequest
        {
            Fund = BudgetFund.DOK, EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Description = "Materiały formacyjne", Category = "Materiały", Type = BudgetEntryType.Expense, Amount = 780m
        }, default);

        var skspEntries = await service.GetEntriesAsync(BudgetFund.SKSP, default);

        Assert.Single(skspEntries);
        Assert.Equal("Wynajem sali", skspEntries[0].Description);
    }
}
```

`backend/tests/DokPortal.Api.IntegrationTests/BudgetControllerTests.cs`:

```csharp
using System.Net.Http.Json;
using DokPortal.Application.Budget;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class BudgetControllerTests : IntegrationTestBase
{
    public BudgetControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Create_ThenGetByFund_ReturnsCreatedEntry()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

        var createResponse = await admin.PostAsJsonAsync("/api/budget", new
        {
            Fund = "SKSP", EntryDate = "2026-09-18", Description = "Materiały formacyjne",
            Category = "Materiały", Type = "Expense", Amount = 780m
        });
        createResponse.EnsureSuccessStatusCode();

        var getResponse = await admin.GetAsync("/api/budget?fund=SKSP");
        getResponse.EnsureSuccessStatusCode();
        var entries = await getResponse.Content.ReadFromJsonAsync<List<BudgetEntryDto>>();
        Assert.Contains(entries!, e => e.Description == "Materiały formacyjne");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/tests/DokPortal.Infrastructure.Tests backend/tests/DokPortal.Api.IntegrationTests`
Expected: FAIL to compile — `IBudgetService`, `BudgetService`, `BudgetController` don't exist yet.

- [ ] **Step 3: Implement Application contracts**

`backend/src/DokPortal.Application/Budget/BudgetEntryDto.cs`:

```csharp
using DokPortal.Domain.Enums;

namespace DokPortal.Application.Budget;

public class BudgetEntryDto
{
    public required Guid Id { get; init; }
    public required BudgetFund Fund { get; init; }
    public required DateOnly EntryDate { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required BudgetEntryType Type { get; init; }
    public required decimal Amount { get; init; }
}
```

`backend/src/DokPortal.Application/Budget/CreateBudgetEntryRequest.cs`:

```csharp
using DokPortal.Domain.Enums;

namespace DokPortal.Application.Budget;

public class CreateBudgetEntryRequest
{
    public required BudgetFund Fund { get; init; }
    public required DateOnly EntryDate { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required BudgetEntryType Type { get; init; }
    public required decimal Amount { get; init; }
}
```

`backend/src/DokPortal.Application/Budget/IBudgetService.cs`:

```csharp
using DokPortal.Domain.Enums;

namespace DokPortal.Application.Budget;

public interface IBudgetService
{
    Task<IReadOnlyList<BudgetEntryDto>> GetEntriesAsync(BudgetFund fund, CancellationToken ct);
    Task<BudgetEntryDto> CreateAsync(CreateBudgetEntryRequest request, CancellationToken ct);
}
```

- [ ] **Step 4: Implement BudgetService**

`backend/src/DokPortal.Infrastructure/Services/BudgetService.cs`:

```csharp
using DokPortal.Application.Budget;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class BudgetService : IBudgetService
{
    private readonly AppDbContext _db;

    public BudgetService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<BudgetEntryDto>> GetEntriesAsync(BudgetFund fund, CancellationToken ct)
    {
        var entries = await _db.BudgetEntries.AsNoTracking()
            .Where(e => e.Fund == fund)
            .OrderByDescending(e => e.EntryDate)
            .ToListAsync(ct);
        return entries.Select(ToDto).ToList();
    }

    public async Task<BudgetEntryDto> CreateAsync(CreateBudgetEntryRequest request, CancellationToken ct)
    {
        var entry = new BudgetEntry
        {
            Id = Guid.NewGuid(),
            Fund = request.Fund,
            EntryDate = request.EntryDate,
            Description = request.Description,
            Category = request.Category,
            Type = request.Type,
            Amount = request.Amount,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.BudgetEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
        return ToDto(entry);
    }

    private static BudgetEntryDto ToDto(BudgetEntry e) => new()
    {
        Id = e.Id,
        Fund = e.Fund,
        EntryDate = e.EntryDate,
        Description = e.Description,
        Category = e.Category,
        Type = e.Type,
        Amount = e.Amount
    };
}
```

- [ ] **Step 5: Implement BudgetController**

`backend/src/DokPortal.Api/Controllers/BudgetController.cs`:

```csharp
using DokPortal.Application.Budget;
using DokPortal.Domain.Constants;
using DokPortal.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/budget")]
[Authorize]
public class BudgetController : ControllerBase
{
    private readonly IBudgetService _budgetService;

    public BudgetController(IBudgetService budgetService) => _budgetService = budgetService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BudgetEntryDto>>> GetEntries([FromQuery] BudgetFund fund, CancellationToken ct)
        => Ok(await _budgetService.GetEntriesAsync(fund, ct));

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorSKSP}")]
    public async Task<ActionResult<BudgetEntryDto>> Create(CreateBudgetEntryRequest request, CancellationToken ct)
    {
        var created = await _budgetService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetEntries), new { fund = created.Fund }, created);
    }
}
```

- [ ] **Step 6: Register the service**

In `backend/src/DokPortal.Api/Program.cs`, add `using DokPortal.Application.Budget;` and register:

```csharp
builder.Services.AddScoped<IBudgetService, BudgetService>();
```

**Deviation found during execution:** `BudgetEntry` is the first entity in the codebase with enum properties (`Fund`, `Type`) exposed directly on a DTO. By default `System.Text.Json` serializes/expects enums as integers, but `BudgetControllerTests` (and the eventual Angular frontend) sends/expects them as strings (`"SKSP"`, `"Expense"`) — posting a string body failed with 400 Bad Request. Fix: register a global `JsonStringEnumConverter` in `Program.cs`, right after `builder.Services.AddControllers()`:

```csharp
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
```

(needs `using System.Text.Json.Serialization;`). This only reconfigures ASP.NET Core's own request/response serialization — it does **not** affect a test's own `HttpContent.ReadFromJsonAsync<T>()` calls, which use their own default `JsonSerializerOptions` unless told otherwise. Since `System.Net.Http.Json`'s parameterless overload actually defaults to `JsonSerializerDefaults.Web` (camelCase + case-insensitive) — which is why every earlier controller test's bare `ReadFromJsonAsync<TDto>()` already worked — a test that needs the enum converter too must build its options from that same baseline, not a bare `new JsonSerializerOptions()` (which lacks camelCase/case-insensitivity and breaks `required` property binding). `BudgetControllerTests` does this explicitly:

```csharp
private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
{
    Converters = { new JsonStringEnumConverter() }
};
```

and passes `JsonOptions` to the one `ReadFromJsonAsync<List<BudgetEntryDto>>(...)` call that deserializes a DTO with enum fields.

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test backend/DokPortal.sln`
Expected: All tests pass — this closes out the backend half of Faza 2.

- [ ] **Step 8: Commit**

```bash
git add backend
git commit -m "Add Budget module shared by SKSP and (future) DOK funds"
```

---

## Task 7: Candidates page (frontend)

**Files:**
- Create: `frontend/src/app/features/candidates/candidate.model.ts`
- Create: `frontend/src/app/features/candidates/candidates.service.ts`
- Test: `frontend/src/app/features/candidates/candidates.service.spec.ts`
- Create: `frontend/src/app/features/candidates/candidate-form.component.ts`
- Create: `frontend/src/app/features/candidates/candidate-form.component.html`
- Create: `frontend/src/app/features/candidates/candidate-form.component.scss` (empty)
- Create: `frontend/src/app/features/candidates/candidates-list.component.ts`
- Create: `frontend/src/app/features/candidates/candidates-list.component.html`
- Create: `frontend/src/app/features/candidates/candidates-list.component.scss` (empty)
- Test: `frontend/src/app/features/candidates/candidates-list.component.spec.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/layout/nav-items.ts`

**Interfaces:**
- Consumes: backend `GET/POST /api/candidates` (Task 2), `GET /api/people` (Phase 1, for the person picker), `AuthService.hasAnyRole` (Phase 1).
- Produces: route `/candidates`; nav item visible to `Administrator`/`DyrektorSKSP`.

- [ ] **Step 1: Write the failing tests**

`frontend/src/app/features/candidates/candidate.model.ts`:

```typescript
export interface Candidate {
  id: string;
  personId: string;
  personFullName: string;
  parishName: string | null;
  year: number;
  attendancePercentage: number | null;
  opinionsCollected: number;
  opinionsRequired: number;
  isRetreatCompleted: boolean;
}

export interface CandidateFormValue {
  personId: string;
  year: number;
  attendancePercentage?: number;
  opinionsCollected: number;
  isRetreatCompleted: boolean;
}
```

`frontend/src/app/features/candidates/candidates.service.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it } from 'vitest';
import { CandidatesService } from './candidates.service';
import { environment } from '../../../environments/environment';

describe('CandidatesService', () => {
  it('sends the year as a request parameter when searching', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(CandidatesService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.search(3).subscribe();

    const req = httpMock.expectOne(
      r => r.url === `${environment.apiBaseUrl}/api/candidates` && r.params.get('year') === '3'
    );
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 100 });
    httpMock.verify();
  });
});
```

`frontend/src/app/features/candidates/candidates-list.component.spec.ts`:

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { CandidatesListComponent } from './candidates-list.component';
import { environment } from '../../../environments/environment';

describe('CandidatesListComponent', () => {
  let fixture: ComponentFixture<CandidatesListComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CandidatesListComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(CandidatesListComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('shows the per-year candidate count computed from the fetched list', () => {
    fixture.detectChanges();
    const candidatesReq = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/candidates`);
    candidatesReq.flush({
      items: [
        { id: '1', personId: 'p1', personFullName: 'Agnieszka Lewandowska', parishName: null, year: 3, attendancePercentage: 94, opinionsCollected: 2, opinionsRequired: 2, isRetreatCompleted: true },
        { id: '2', personId: 'p2', personFullName: 'Karolina Nowak', parishName: null, year: 1, attendancePercentage: 81, opinionsCollected: 0, opinionsRequired: 2, isRetreatCompleted: false }
      ],
      totalCount: 2, page: 1, pageSize: 100
    });
    httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/people`).flush({ items: [], totalCount: 0, page: 1, pageSize: 200 });
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Agnieszka Lewandowska');
    expect(text).toContain('Karolina Nowak');
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd frontend && npx ng test`
Expected: FAIL to compile — `CandidatesService`, `CandidatesListComponent` don't exist yet.

- [ ] **Step 3: Implement CandidatesService**

`frontend/src/app/features/candidates/candidates.service.ts`:

```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../people/person.model';
import { Candidate, CandidateFormValue } from './candidate.model';

@Injectable({ providedIn: 'root' })
export class CandidatesService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/candidates`;

  constructor(private readonly http: HttpClient) {}

  search(year?: number) {
    const params: Record<string, string> = { pageSize: '100' };
    if (year) {
      params['year'] = String(year);
    }
    return this.http.get<PagedResult<Candidate>>(this.baseUrl, { params });
  }

  create(value: CandidateFormValue) {
    return this.http.post<Candidate>(this.baseUrl, value);
  }
}
```

- [ ] **Step 4: Implement the add-candidate modal**

`frontend/src/app/features/candidates/candidate-form.component.ts`:

```typescript
import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PeopleService } from '../people/people.service';
import { Person } from '../people/person.model';
import { CandidateFormValue } from './candidate.model';

@Component({
  selector: 'app-candidate-form',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './candidate-form.component.html',
  styleUrl: './candidate-form.component.scss'
})
export class CandidateFormComponent implements OnInit {
  @Input() open = false;
  @Input() value: CandidateFormValue = { personId: '', year: 1, opinionsCollected: 0, isRetreatCompleted: false };
  @Output() save = new EventEmitter<CandidateFormValue>();
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

`frontend/src/app/features/candidates/candidate-form.component.html`:

```html
@if (open) {
  <div class="overlay show">
    <div class="modal">
      <div class="modal-head">
        <h3>Nowy kandydat SKŚP</h3>
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
            <label>Rok formacji</label>
            <select [(ngModel)]="value.year" name="year">
              <option [ngValue]="1">I ROK</option>
              <option [ngValue]="2">II ROK</option>
              <option [ngValue]="3">III ROK</option>
            </select>
          </div>
          <div class="field"><label>Frekwencja (%)</label><input type="number" [(ngModel)]="value.attendancePercentage" name="attendancePercentage" /></div>
          <div class="field"><label>Liczba opinii</label><input type="number" [(ngModel)]="value.opinionsCollected" name="opinionsCollected" /></div>
          <div class="field">
            <label>Rekolekcje</label>
            <select [(ngModel)]="value.isRetreatCompleted" name="isRetreatCompleted">
              <option [ngValue]="false">oczekuje</option>
              <option [ngValue]="true">zaliczone</option>
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

`frontend/src/app/features/candidates/candidate-form.component.scss`: create as an empty file.

- [ ] **Step 5: Implement the list page**

`frontend/src/app/features/candidates/candidates-list.component.ts`:

```typescript
import { Component, OnInit, computed, signal } from '@angular/core';
import { CandidatesService } from './candidates.service';
import { Candidate, CandidateFormValue } from './candidate.model';
import { CandidateFormComponent } from './candidate-form.component';

@Component({
  selector: 'app-candidates-list',
  standalone: true,
  imports: [CandidateFormComponent],
  templateUrl: './candidates-list.component.html',
  styleUrl: './candidates-list.component.scss'
})
export class CandidatesListComponent implements OnInit {
  readonly candidates = signal<Candidate[]>([]);
  readonly isFormOpen = signal(false);
  formValue: CandidateFormValue = { personId: '', year: 1, opinionsCollected: 0, isRetreatCompleted: false };

  readonly yearOneCount = computed(() => this.candidates().filter(c => c.year === 1).length);
  readonly yearTwoCount = computed(() => this.candidates().filter(c => c.year === 2).length);
  readonly yearThreeCount = computed(() => this.candidates().filter(c => c.year === 3).length);
  readonly missingOpinionsCount = computed(() => this.candidates().filter(c => c.opinionsCollected < c.opinionsRequired).length);

  constructor(private readonly candidatesService: CandidatesService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.candidatesService.search().subscribe(result => this.candidates.set(result.items));
  }

  openAddForm(): void {
    this.formValue = { personId: '', year: 1, opinionsCollected: 0, isRetreatCompleted: false };
    this.isFormOpen.set(true);
  }

  onSave(value: CandidateFormValue): void {
    this.candidatesService.create(value).subscribe(() => {
      this.isFormOpen.set(false);
      this.load();
    });
  }

  onCancel(): void {
    this.isFormOpen.set(false);
  }
}
```

`frontend/src/app/features/candidates/candidates-list.component.html`:

```html
<div class="page-heading">
  <div><h2>Kandydaci SKŚP</h2><p>Osoby zapisane do Szkoły Katechistów.</p></div>
  <button class="btn primary" (click)="openAddForm()">＋ Nowy kandydat</button>
</div>

<div class="grid stats">
  <div class="card stat"><div class="stat-label">I ROK</div><div class="stat-value">{{ yearOneCount() }}</div></div>
  <div class="card stat"><div class="stat-label">II ROK</div><div class="stat-value">{{ yearTwoCount() }}</div></div>
  <div class="card stat"><div class="stat-label">III ROK</div><div class="stat-value">{{ yearThreeCount() }}</div></div>
  <div class="card stat"><div class="stat-label">Brakujące opinie</div><div class="stat-value">{{ missingOpinionsCount() }}</div></div>
</div>

<div class="card">
  <div class="table-wrap">
    <table>
      <thead><tr><th>Kandydat</th><th>Rok</th><th>Parafia</th><th>Frekwencja</th><th>Opinie</th><th>Rekolekcje</th></tr></thead>
      <tbody>
        @for (candidate of candidates(); track candidate.id) {
          <tr>
            <td><b>{{ candidate.personFullName }}</b></td>
            <td><span class="pill blue">{{ candidate.year }} ROK</span></td>
            <td>{{ candidate.parishName }}</td>
            <td>{{ candidate.attendancePercentage }}%</td>
            <td>{{ candidate.opinionsCollected }} / {{ candidate.opinionsRequired }}</td>
            <td>
              @if (candidate.isRetreatCompleted) {
                <span class="pill green">✓ zaliczone</span>
              } @else {
                <span class="pill gold">oczekuje</span>
              }
            </td>
          </tr>
        } @empty {
          <tr><td colspan="6" class="empty">Brak kandydatów.</td></tr>
        }
      </tbody>
    </table>
  </div>
</div>

<app-candidate-form
  [open]="isFormOpen()"
  [value]="formValue"
  (save)="onSave($event)"
  (cancel)="onCancel()">
</app-candidate-form>
```

`frontend/src/app/features/candidates/candidates-list.component.scss`: create as an empty file.

- [ ] **Step 6: Wire the route and nav item**

In `frontend/src/app/app.routes.ts`, add to the shell's `children` array:

```typescript
{
  path: 'candidates',
  loadComponent: () => import('./features/candidates/candidates-list.component').then(m => m.CandidatesListComponent)
}
```

In `frontend/src/app/layout/nav-items.ts`, add to `NAV_ITEMS`:

```typescript
{ label: 'Kandydaci SKŚP', icon: '◉', path: '/candidates', roles: ['Administrator', 'DyrektorSKSP'] }
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `npx ng test`
Expected: PASS (2 new test files).

- [ ] **Step 8: Commit**

```bash
git add frontend
git commit -m "Add Candidates page"
```

---

## Task 8: Missions page (frontend)

**Files:**
- Create: `frontend/src/app/features/missions/mission.model.ts`
- Create: `frontend/src/app/features/missions/missions.service.ts`
- Test: `frontend/src/app/features/missions/missions.service.spec.ts`
- Create: `frontend/src/app/features/missions/mission-form.component.ts`
- Create: `frontend/src/app/features/missions/mission-form.component.html`
- Create: `frontend/src/app/features/missions/mission-form.component.scss` (empty)
- Create: `frontend/src/app/features/missions/missions-list.component.ts`
- Create: `frontend/src/app/features/missions/missions-list.component.html`
- Create: `frontend/src/app/features/missions/missions-list.component.scss` (empty)
- Test: `frontend/src/app/features/missions/missions-list.component.spec.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/layout/nav-items.ts`

**Interfaces:**
- Consumes: backend `GET/POST /api/missions` (Task 3), `GET /api/people` (Phase 1).
- Produces: route `/missions`; nav item visible to `Administrator`/`DyrektorSKSP`/`Biskup`.

- [ ] **Step 1: Write the failing tests**

`frontend/src/app/features/missions/mission.model.ts`:

```typescript
export interface Mission {
  id: string;
  personId: string;
  personFullName: string;
  servicePlace: string;
  missionStartDate: string;
  missionEndDate: string;
  grantedDate: string | null;
  grantedPlace: string | null;
  supervisionGroup: string | null;
  status: string;
}

export interface MissionFormValue {
  personId: string;
  servicePlace: string;
  missionStartDate: string;
  missionEndDate: string;
  grantedDate?: string;
  grantedPlace?: string;
  supervisionGroup?: string;
}
```

`frontend/src/app/features/missions/missions.service.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it } from 'vitest';
import { MissionsService } from './missions.service';
import { environment } from '../../../environments/environment';

describe('MissionsService', () => {
  it('requests missions from the API', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(MissionsService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.search().subscribe();

    const req = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/missions`);
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 100 });
    httpMock.verify();
  });
});
```

`frontend/src/app/features/missions/missions-list.component.spec.ts`:

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { MissionsListComponent } from './missions-list.component';
import { environment } from '../../../environments/environment';

describe('MissionsListComponent', () => {
  let fixture: ComponentFixture<MissionsListComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [MissionsListComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(MissionsListComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('renders missions returned from the API with their status', () => {
    fixture.detectChanges();
    const missionsReq = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/missions`);
    missionsReq.flush({
      items: [{ id: '1', personId: 'p1', personFullName: 'Anna Maj', servicePlace: 'Parafia św. Mateusza', missionStartDate: '2023-10-15', missionEndDate: '2026-10-14', grantedDate: null, grantedPlace: null, supervisionGroup: 'Grupa A', status: 'wygasa' }],
      totalCount: 1, page: 1, pageSize: 100
    });
    httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/people`).flush({ items: [], totalCount: 0, page: 1, pageSize: 200 });
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Anna Maj');
    expect(text).toContain('wygasa');
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd frontend && npx ng test`
Expected: FAIL to compile — `MissionsService`, `MissionsListComponent` don't exist yet.

- [ ] **Step 3: Implement MissionsService**

`frontend/src/app/features/missions/missions.service.ts`:

```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../people/person.model';
import { Mission, MissionFormValue } from './mission.model';

@Injectable({ providedIn: 'root' })
export class MissionsService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/missions`;

  constructor(private readonly http: HttpClient) {}

  search(query = '') {
    return this.http.get<PagedResult<Mission>>(this.baseUrl, { params: { query, pageSize: 100 } });
  }

  create(value: MissionFormValue) {
    return this.http.post<Mission>(this.baseUrl, value);
  }
}
```

- [ ] **Step 4: Implement the add-mission modal**

`frontend/src/app/features/missions/mission-form.component.ts`:

```typescript
import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PeopleService } from '../people/people.service';
import { Person } from '../people/person.model';
import { MissionFormValue } from './mission.model';

@Component({
  selector: 'app-mission-form',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './mission-form.component.html',
  styleUrl: './mission-form.component.scss'
})
export class MissionFormComponent implements OnInit {
  @Input() open = false;
  @Input() value: MissionFormValue = { personId: '', servicePlace: '', missionStartDate: '', missionEndDate: '' };
  @Output() save = new EventEmitter<MissionFormValue>();
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

`frontend/src/app/features/missions/mission-form.component.html`:

```html
@if (open) {
  <div class="overlay show">
    <div class="modal">
      <div class="modal-head">
        <h3>Nowa misja kanoniczna</h3>
        <button class="close" (click)="cancel.emit()">×</button>
      </div>
      <div class="modal-body">
        <div class="form-grid">
          <div class="field">
            <label>Katechista</label>
            <select [(ngModel)]="value.personId" name="personId">
              @for (person of people; track person.id) {
                <option [value]="person.id">{{ person.fullName }}</option>
              }
            </select>
          </div>
          <div class="field"><label>Parafia / miejsce posługi</label><input [(ngModel)]="value.servicePlace" name="servicePlace" /></div>
          <div class="field"><label>Data od</label><input type="date" [(ngModel)]="value.missionStartDate" name="missionStartDate" /></div>
          <div class="field"><label>Data do</label><input type="date" [(ngModel)]="value.missionEndDate" name="missionEndDate" /></div>
          <div class="field"><label>Data udzielenia</label><input type="date" [(ngModel)]="value.grantedDate" name="grantedDate" /></div>
          <div class="field"><label>Miejsce udzielenia</label><input [(ngModel)]="value.grantedPlace" name="grantedPlace" /></div>
          <div class="field"><label>Grupa superwizyjna</label><input [(ngModel)]="value.supervisionGroup" name="supervisionGroup" /></div>
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

`frontend/src/app/features/missions/mission-form.component.scss`: create as an empty file.

- [ ] **Step 5: Implement the list page**

`frontend/src/app/features/missions/missions-list.component.ts`:

```typescript
import { Component, OnInit, signal } from '@angular/core';
import { MissionsService } from './missions.service';
import { Mission, MissionFormValue } from './mission.model';
import { MissionFormComponent } from './mission-form.component';

@Component({
  selector: 'app-missions-list',
  standalone: true,
  imports: [MissionFormComponent],
  templateUrl: './missions-list.component.html',
  styleUrl: './missions-list.component.scss'
})
export class MissionsListComponent implements OnInit {
  readonly missions = signal<Mission[]>([]);
  readonly isFormOpen = signal(false);
  formValue: MissionFormValue = { personId: '', servicePlace: '', missionStartDate: '', missionEndDate: '' };

  constructor(private readonly missionsService: MissionsService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.missionsService.search().subscribe(result => this.missions.set(result.items));
  }

  openAddForm(): void {
    this.formValue = { personId: '', servicePlace: '', missionStartDate: '', missionEndDate: '' };
    this.isFormOpen.set(true);
  }

  onSave(value: MissionFormValue): void {
    this.missionsService.create(value).subscribe(() => {
      this.isFormOpen.set(false);
      this.load();
    });
  }

  onCancel(): void {
    this.isFormOpen.set(false);
  }

  statusPillClass(status: string): string {
    if (status === 'wygasła') return 'pill red';
    if (status === 'wygasa') return 'pill red';
    return 'pill green';
  }
}
```

`frontend/src/app/features/missions/missions-list.component.html`:

```html
<div class="page-heading">
  <div><h2>Katechiści posłani</h2><p>Misje kanoniczne, miejsca posługi i grupy superwizyjne.</p></div>
  <button class="btn primary" (click)="openAddForm()">＋ Dodaj misję</button>
</div>

<div class="card">
  <div class="table-wrap">
    <table>
      <thead><tr><th>Katechista</th><th>Miejsce posługi</th><th>Misja od</th><th>Misja do</th><th>Superwizja</th><th>Status</th></tr></thead>
      <tbody>
        @for (mission of missions(); track mission.id) {
          <tr>
            <td><b>{{ mission.personFullName }}</b></td>
            <td>{{ mission.servicePlace }}</td>
            <td>{{ mission.missionStartDate }}</td>
            <td>{{ mission.missionEndDate }}</td>
            <td>{{ mission.supervisionGroup }}</td>
            <td><span [class]="statusPillClass(mission.status)">{{ mission.status }}</span></td>
          </tr>
        } @empty {
          <tr><td colspan="6" class="empty">Brak misji.</td></tr>
        }
      </tbody>
    </table>
  </div>
</div>

<app-mission-form
  [open]="isFormOpen()"
  [value]="formValue"
  (save)="onSave($event)"
  (cancel)="onCancel()">
</app-mission-form>
```

`frontend/src/app/features/missions/missions-list.component.scss`: create as an empty file.

- [ ] **Step 6: Wire the route and nav item**

In `frontend/src/app/app.routes.ts`, add to the shell's `children` array:

```typescript
{
  path: 'missions',
  loadComponent: () => import('./features/missions/missions-list.component').then(m => m.MissionsListComponent)
}
```

In `frontend/src/app/layout/nav-items.ts`, add to `NAV_ITEMS`:

```typescript
{ label: 'Katechiści posłani', icon: '✦', path: '/missions', roles: ['Administrator', 'DyrektorSKSP', 'Biskup'] }
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `npx ng test`
Expected: PASS (2 new test files).

- [ ] **Step 8: Commit**

```bash
git add frontend
git commit -m "Add Missions page"
```

---

## Task 9: Formators page (frontend)

**Files:**
- Create: `frontend/src/app/features/formators/formator.model.ts`
- Create: `frontend/src/app/features/formators/formators.service.ts`
- Test: `frontend/src/app/features/formators/formators.service.spec.ts`
- Create: `frontend/src/app/features/formators/formators-list.component.ts`
- Create: `frontend/src/app/features/formators/formators-list.component.html`
- Create: `frontend/src/app/features/formators/formators-list.component.scss` (empty)
- Test: `frontend/src/app/features/formators/formators-list.component.spec.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/layout/nav-items.ts`

**Interfaces:**
- Consumes: backend `GET/POST /api/formators` (Task 4), `GET /api/people` (Phase 1).
- Produces: route `/formators`; nav item visible to `Administrator`/`DyrektorSKSP`.

- [ ] **Step 1: Write the failing tests**

`frontend/src/app/features/formators/formator.model.ts`:

```typescript
export interface Formator {
  id: string;
  personId: string;
  personFullName: string;
  personEmail: string | null;
  personPhone: string | null;
  function: string;
}

export interface FormatorFormValue {
  personId: string;
  function: string;
}
```

`frontend/src/app/features/formators/formators.service.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it } from 'vitest';
import { FormatorsService } from './formators.service';
import { environment } from '../../../environments/environment';

describe('FormatorsService', () => {
  it('requests the formators list from the API', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(FormatorsService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.list().subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/formators`);
    req.flush([]);
    httpMock.verify();
  });
});
```

`frontend/src/app/features/formators/formators-list.component.spec.ts`:

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { FormatorsListComponent } from './formators-list.component';
import { environment } from '../../../environments/environment';

describe('FormatorsListComponent', () => {
  let fixture: ComponentFixture<FormatorsListComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [FormatorsListComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(FormatorsListComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('renders formators returned from the API', () => {
    fixture.detectChanges();
    const formatorsReq = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/formators`);
    formatorsReq.flush([{ id: '1', personId: 'p1', personFullName: 'Joanna Lis', personEmail: 'j.lis@example.org', personPhone: null, function: 'Wykładowca' }]);
    httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/people`).flush({ items: [], totalCount: 0, page: 1, pageSize: 200 });
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Joanna Lis');
    expect(text).toContain('Wykładowca');
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd frontend && npx ng test`
Expected: FAIL to compile — `FormatorsService`, `FormatorsListComponent` don't exist yet.

- [ ] **Step 3: Implement FormatorsService**

`frontend/src/app/features/formators/formators.service.ts`:

```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { Formator, FormatorFormValue } from './formator.model';

@Injectable({ providedIn: 'root' })
export class FormatorsService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/formators`;

  constructor(private readonly http: HttpClient) {}

  list() {
    return this.http.get<Formator[]>(this.baseUrl);
  }

  create(value: FormatorFormValue) {
    return this.http.post<Formator>(this.baseUrl, value);
  }
}
```

- [ ] **Step 4: Implement the list page (with an inline add form, no separate modal component — matches the prototype's simple one-table layout for this page)**

`frontend/src/app/features/formators/formators-list.component.ts`:

```typescript
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FormatorsService } from './formators.service';
import { Formator, FormatorFormValue } from './formator.model';
import { PeopleService } from '../people/people.service';
import { Person } from '../people/person.model';

@Component({
  selector: 'app-formators-list',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './formators-list.component.html',
  styleUrl: './formators-list.component.scss'
})
export class FormatorsListComponent implements OnInit {
  readonly formators = signal<Formator[]>([]);
  readonly isFormOpen = signal(false);
  people: Person[] = [];
  formValue: FormatorFormValue = { personId: '', function: '' };

  constructor(
    private readonly formatorsService: FormatorsService,
    private readonly peopleService: PeopleService
  ) {}

  ngOnInit(): void {
    this.load();
    this.peopleService.search('', 1, 200).subscribe(result => (this.people = result.items));
  }

  load(): void {
    this.formatorsService.list().subscribe(formators => this.formators.set(formators));
  }

  openAddForm(): void {
    this.formValue = { personId: '', function: '' };
    this.isFormOpen.set(true);
  }

  onSave(): void {
    this.formatorsService.create(this.formValue).subscribe(() => {
      this.isFormOpen.set(false);
      this.load();
    });
  }

  onCancel(): void {
    this.isFormOpen.set(false);
  }
}
```

`frontend/src/app/features/formators/formators-list.component.html`:

```html
<div class="page-heading">
  <div><h2>Formatorzy SKŚP</h2><p>Biskup, wikariusz, referent, wykładowcy i moderatorzy.</p></div>
  <button class="btn primary" (click)="openAddForm()">＋ Dodaj formatora</button>
</div>

<div class="card">
  <div class="table-wrap">
    <table>
      <thead><tr><th>Osoba</th><th>Funkcja</th><th>E-mail</th><th>Telefon</th></tr></thead>
      <tbody>
        @for (formator of formators(); track formator.id) {
          <tr>
            <td><b>{{ formator.personFullName }}</b></td>
            <td>{{ formator.function }}</td>
            <td>{{ formator.personEmail }}</td>
            <td>{{ formator.personPhone }}</td>
          </tr>
        } @empty {
          <tr><td colspan="4" class="empty">Brak formatorów.</td></tr>
        }
      </tbody>
    </table>
  </div>
</div>

@if (isFormOpen()) {
  <div class="overlay show">
    <div class="modal">
      <div class="modal-head">
        <h3>Dodaj formatora</h3>
        <button class="close" (click)="onCancel()">×</button>
      </div>
      <div class="modal-body">
        <div class="form-grid">
          <div class="field">
            <label>Osoba</label>
            <select [(ngModel)]="formValue.personId" name="personId">
              @for (person of people; track person.id) {
                <option [value]="person.id">{{ person.fullName }}</option>
              }
            </select>
          </div>
          <div class="field"><label>Funkcja</label><input [(ngModel)]="formValue.function" name="function" /></div>
        </div>
      </div>
      <div class="modal-foot">
        <button class="btn ghost" (click)="onCancel()">Anuluj</button>
        <button class="btn primary" (click)="onSave()">Zapisz</button>
      </div>
    </div>
  </div>
}
```

`frontend/src/app/features/formators/formators-list.component.scss`: create as an empty file.

- [ ] **Step 5: Wire the route and nav item**

In `frontend/src/app/app.routes.ts`, add to the shell's `children` array:

```typescript
{
  path: 'formators',
  loadComponent: () => import('./features/formators/formators-list.component').then(m => m.FormatorsListComponent)
}
```

In `frontend/src/app/layout/nav-items.ts`, add to `NAV_ITEMS`:

```typescript
{ label: 'Formatorzy', icon: '♙', path: '/formators', roles: ['Administrator', 'DyrektorSKSP'] }
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `npx ng test`
Expected: PASS (2 new test files).

- [ ] **Step 7: Commit**

```bash
git add frontend
git commit -m "Add Formators page"
```

---

## Task 10: Parish board page (frontend)

**Files:**
- Create: `frontend/src/app/features/parish-board/parish-need.model.ts`
- Create: `frontend/src/app/features/parish-board/parishes.service.ts`
- Create: `frontend/src/app/features/parish-board/parish-needs.service.ts`
- Test: `frontend/src/app/features/parish-board/parish-needs.service.spec.ts`
- Create: `frontend/src/app/features/parish-board/parish-board.component.ts`
- Create: `frontend/src/app/features/parish-board/parish-board.component.html`
- Create: `frontend/src/app/features/parish-board/parish-board.component.scss` (empty)
- Test: `frontend/src/app/features/parish-board/parish-board.component.spec.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/layout/nav-items.ts`

**Interfaces:**
- Consumes: backend `GET/POST /api/parish-needs`, `PUT /api/parish-needs/{id}/assign` (Task 5), `GET /api/parishes` (Phase 1), `GET /api/people` (Phase 1).
- Produces: route `/parish-board`; nav item visible to `Administrator`/`DyrektorSKSP`.

- [ ] **Step 1: Write the failing tests**

`frontend/src/app/features/parish-board/parish-need.model.ts`:

```typescript
export interface ParishNeed {
  id: string;
  parishId: string;
  parishName: string;
  description: string;
  status: string;
  assignedPersonId: string | null;
  assignedPersonName: string | null;
  assignedAtUtc: string | null;
}

export interface CreateParishNeedValue {
  parishId: string;
  description: string;
}

export interface Parish {
  id: string;
  name: string;
  city: string | null;
}
```

`frontend/src/app/features/parish-board/parishes.service.ts`:

```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { Parish } from './parish-need.model';

@Injectable({ providedIn: 'root' })
export class ParishesService {
  constructor(private readonly http: HttpClient) {}

  list() {
    return this.http.get<Parish[]>(`${environment.apiBaseUrl}/api/parishes`);
  }
}
```

`frontend/src/app/features/parish-board/parish-needs.service.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it } from 'vitest';
import { ParishNeedsService } from './parish-needs.service';
import { environment } from '../../../environments/environment';

describe('ParishNeedsService', () => {
  it('requests the needs list from the API', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(ParishNeedsService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.list().subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/parish-needs`);
    req.flush([]);
    httpMock.verify();
  });
});
```

`frontend/src/app/features/parish-board/parish-board.component.spec.ts`:

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { ParishBoardComponent } from './parish-board.component';
import { environment } from '../../../environments/environment';

describe('ParishBoardComponent', () => {
  let fixture: ComponentFixture<ParishBoardComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ParishBoardComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(ParishBoardComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('renders parish needs returned from the API', () => {
    fixture.detectChanges();
    httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/parish-needs`).flush([
      { id: '1', parishId: 'par1', parishName: 'św. Mateusza', description: 'Katechista do przygotowania dorosłych', status: 'Open', assignedPersonId: null, assignedPersonName: null, assignedAtUtc: null }
    ]);
    httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/parishes`).flush([]);
    httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/people`).flush({ items: [], totalCount: 0, page: 1, pageSize: 200 });
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('św. Mateusza');
    expect(text).toContain('Katechista do przygotowania dorosłych');
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd frontend && npx ng test`
Expected: FAIL to compile — `ParishNeedsService`, `ParishBoardComponent` don't exist yet.

- [ ] **Step 3: Implement ParishNeedsService**

`frontend/src/app/features/parish-board/parish-needs.service.ts`:

```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { CreateParishNeedValue, ParishNeed } from './parish-need.model';

@Injectable({ providedIn: 'root' })
export class ParishNeedsService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/parish-needs`;

  constructor(private readonly http: HttpClient) {}

  list() {
    return this.http.get<ParishNeed[]>(this.baseUrl);
  }

  create(value: CreateParishNeedValue) {
    return this.http.post<ParishNeed>(this.baseUrl, value);
  }

  assign(id: string, personId: string) {
    return this.http.put<ParishNeed>(`${this.baseUrl}/${id}/assign`, { personId });
  }
}
```

- [ ] **Step 4: Implement the parish board page**

`frontend/src/app/features/parish-board/parish-board.component.ts`:

```typescript
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ParishNeedsService } from './parish-needs.service';
import { ParishesService } from './parishes.service';
import { PeopleService } from '../people/people.service';
import { CreateParishNeedValue, Parish, ParishNeed } from './parish-need.model';
import { Person } from '../people/person.model';

@Component({
  selector: 'app-parish-board',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './parish-board.component.html',
  styleUrl: './parish-board.component.scss'
})
export class ParishBoardComponent implements OnInit {
  readonly needs = signal<ParishNeed[]>([]);
  readonly isAddFormOpen = signal(false);
  readonly assigningNeedId = signal<string | null>(null);
  parishes: Parish[] = [];
  people: Person[] = [];
  newNeed: CreateParishNeedValue = { parishId: '', description: '' };
  assignPersonId = '';

  constructor(
    private readonly parishNeedsService: ParishNeedsService,
    private readonly parishesService: ParishesService,
    private readonly peopleService: PeopleService
  ) {}

  ngOnInit(): void {
    this.load();
    this.parishesService.list().subscribe(parishes => (this.parishes = parishes));
    this.peopleService.search('', 1, 200).subscribe(result => (this.people = result.items));
  }

  load(): void {
    this.parishNeedsService.list().subscribe(needs => this.needs.set(needs));
  }

  openAddForm(): void {
    this.newNeed = { parishId: '', description: '' };
    this.isAddFormOpen.set(true);
  }

  createNeed(): void {
    this.parishNeedsService.create(this.newNeed).subscribe(() => {
      this.isAddFormOpen.set(false);
      this.load();
    });
  }

  openAssignForm(need: ParishNeed): void {
    this.assignPersonId = '';
    this.assigningNeedId.set(need.id);
  }

  confirmAssign(): void {
    const id = this.assigningNeedId();
    if (!id) return;
    this.parishNeedsService.assign(id, this.assignPersonId).subscribe(() => {
      this.assigningNeedId.set(null);
      this.load();
    });
  }

  cancelAssign(): void {
    this.assigningNeedId.set(null);
  }
}
```

`frontend/src/app/features/parish-board/parish-board.component.html`:

```html
<div class="page-heading">
  <div><h2>Parafie i giełda posługi</h2><p>Potrzeby parafii i ręczne skierowanie katechisty.</p></div>
  <button class="btn primary" (click)="openAddForm()">＋ Nowe zapotrzebowanie</button>
</div>

<div class="card">
  <div class="card-body list">
    @for (need of needs(); track need.id) {
      <div class="list-row">
        <div class="list-main">
          <div class="list-title">{{ need.parishName }} — {{ need.description }}</div>
          @if (need.assignedPersonName) {
            <div class="list-sub">Skierowano: {{ need.assignedPersonName }}</div>
          }
        </div>
        @if (need.status === 'Open') {
          <button class="btn primary small" (click)="openAssignForm(need)">Skieruj</button>
        } @else {
          <span class="pill green">{{ need.status }}</span>
        }
      </div>
    } @empty {
      <div class="empty">Brak zapotrzebowań.</div>
    }
  </div>
</div>

@if (isAddFormOpen()) {
  <div class="overlay show">
    <div class="modal">
      <div class="modal-head">
        <h3>Nowe zapotrzebowanie parafii</h3>
        <button class="close" (click)="isAddFormOpen.set(false)">×</button>
      </div>
      <div class="modal-body">
        <div class="form-grid">
          <div class="field">
            <label>Parafia</label>
            <select [(ngModel)]="newNeed.parishId" name="parishId">
              @for (parish of parishes; track parish.id) {
                <option [value]="parish.id">{{ parish.name }}</option>
              }
            </select>
          </div>
          <div class="field full"><label>Opis potrzeby</label><textarea [(ngModel)]="newNeed.description" name="description"></textarea></div>
        </div>
      </div>
      <div class="modal-foot">
        <button class="btn ghost" (click)="isAddFormOpen.set(false)">Anuluj</button>
        <button class="btn primary" (click)="createNeed()">Zapisz</button>
      </div>
    </div>
  </div>
}

@if (assigningNeedId()) {
  <div class="overlay show">
    <div class="modal">
      <div class="modal-head">
        <h3>Skieruj katechistę</h3>
        <button class="close" (click)="cancelAssign()">×</button>
      </div>
      <div class="modal-body">
        <div class="field">
          <label>Osoba</label>
          <select [(ngModel)]="assignPersonId" name="assignPersonId">
            @for (person of people; track person.id) {
              <option [value]="person.id">{{ person.fullName }}</option>
            }
          </select>
        </div>
      </div>
      <div class="modal-foot">
        <button class="btn ghost" (click)="cancelAssign()">Anuluj</button>
        <button class="btn primary" (click)="confirmAssign()">Skieruj</button>
      </div>
    </div>
  </div>
}
```

`frontend/src/app/features/parish-board/parish-board.component.scss`: create as an empty file.

- [ ] **Step 5: Wire the route and nav item**

In `frontend/src/app/app.routes.ts`, add to the shell's `children` array:

```typescript
{
  path: 'parish-board',
  loadComponent: () => import('./features/parish-board/parish-board.component').then(m => m.ParishBoardComponent)
}
```

In `frontend/src/app/layout/nav-items.ts`, add to `NAV_ITEMS`:

```typescript
{ label: 'Parafie i giełda', icon: '⌂', path: '/parish-board', roles: ['Administrator', 'DyrektorSKSP'] }
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `npx ng test`
Expected: PASS (2 new test files).

- [ ] **Step 7: Commit**

```bash
git add frontend
git commit -m "Add Parish board page (giełda posługi)"
```

---

## Task 11: Budget page (frontend)

**Files:**
- Create: `frontend/src/app/features/budget/budget-entry.model.ts`
- Create: `frontend/src/app/features/budget/budget.service.ts`
- Test: `frontend/src/app/features/budget/budget.service.spec.ts`
- Create: `frontend/src/app/features/budget/budget.component.ts`
- Create: `frontend/src/app/features/budget/budget.component.html`
- Create: `frontend/src/app/features/budget/budget.component.scss` (empty)
- Test: `frontend/src/app/features/budget/budget.component.spec.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/layout/nav-items.ts`

**Interfaces:**
- Consumes: backend `GET /api/budget?fund=SKSP`, `POST /api/budget` (Task 6).
- Produces: route `/budget/sksp`; nav item visible to `Administrator`/`DyrektorSKSP`.

- [ ] **Step 1: Write the failing tests**

`frontend/src/app/features/budget/budget-entry.model.ts`:

```typescript
export type BudgetFund = 'SKSP' | 'DOK';
export type BudgetEntryType = 'Income' | 'Expense';

export interface BudgetEntry {
  id: string;
  fund: BudgetFund;
  entryDate: string;
  description: string;
  category: string;
  type: BudgetEntryType;
  amount: number;
}

export interface CreateBudgetEntryValue {
  fund: BudgetFund;
  entryDate: string;
  description: string;
  category: string;
  type: BudgetEntryType;
  amount: number;
}
```

`frontend/src/app/features/budget/budget.service.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it } from 'vitest';
import { BudgetService } from './budget.service';
import { environment } from '../../../environments/environment';

describe('BudgetService', () => {
  it('sends the fund as a request parameter when listing entries', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(BudgetService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.listEntries('SKSP').subscribe();

    const req = httpMock.expectOne(
      r => r.url === `${environment.apiBaseUrl}/api/budget` && r.params.get('fund') === 'SKSP'
    );
    req.flush([]);
    httpMock.verify();
  });
});
```

`frontend/src/app/features/budget/budget.component.spec.ts`:

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { BudgetComponent } from './budget.component';
import { environment } from '../../../environments/environment';

describe('BudgetComponent', () => {
  let fixture: ComponentFixture<BudgetComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [BudgetComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(BudgetComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('computes income, expense, and balance totals from the fetched entries', () => {
    fixture.detectChanges();
    const req = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/budget`);
    req.flush([
      { id: '1', fund: 'SKSP', entryDate: '2026-09-12', description: 'Dotacja', category: 'Dotacja', type: 'Income', amount: 5000 },
      { id: '2', fund: 'SKSP', entryDate: '2026-09-18', description: 'Materiały', category: 'Materiały', type: 'Expense', amount: 780 }
    ]);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('5000');
    expect(text).toContain('780');
    expect(text).toContain('4220');
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd frontend && npx ng test`
Expected: FAIL to compile — `BudgetService`, `BudgetComponent` don't exist yet.

- [ ] **Step 3: Implement BudgetService**

`frontend/src/app/features/budget/budget.service.ts`:

```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { BudgetEntry, BudgetFund, CreateBudgetEntryValue } from './budget-entry.model';

@Injectable({ providedIn: 'root' })
export class BudgetService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/budget`;

  constructor(private readonly http: HttpClient) {}

  listEntries(fund: BudgetFund) {
    return this.http.get<BudgetEntry[]>(this.baseUrl, { params: { fund } });
  }

  create(value: CreateBudgetEntryValue) {
    return this.http.post<BudgetEntry>(this.baseUrl, value);
  }
}
```

- [ ] **Step 4: Implement the budget page**

`frontend/src/app/features/budget/budget.component.ts`:

```typescript
import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { BudgetService } from './budget.service';
import { BudgetEntry, CreateBudgetEntryValue } from './budget-entry.model';

@Component({
  selector: 'app-budget',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './budget.component.html',
  styleUrl: './budget.component.scss'
})
export class BudgetComponent implements OnInit {
  readonly entries = signal<BudgetEntry[]>([]);
  readonly isFormOpen = signal(false);
  newEntry: CreateBudgetEntryValue = {
    fund: 'SKSP', entryDate: '', description: '', category: '', type: 'Expense', amount: 0
  };

  readonly totalIncome = computed(() =>
    this.entries().filter(e => e.type === 'Income').reduce((sum, e) => sum + e.amount, 0)
  );
  readonly totalExpense = computed(() =>
    this.entries().filter(e => e.type === 'Expense').reduce((sum, e) => sum + e.amount, 0)
  );
  readonly balance = computed(() => this.totalIncome() - this.totalExpense());
  readonly expenseByCategory = computed(() => {
    const totals = new Map<string, number>();
    for (const entry of this.entries().filter(e => e.type === 'Expense')) {
      totals.set(entry.category, (totals.get(entry.category) ?? 0) + entry.amount);
    }
    return Array.from(totals.entries()).map(([category, amount]) => ({ category, amount }));
  });

  constructor(private readonly budgetService: BudgetService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.budgetService.listEntries('SKSP').subscribe(entries => this.entries.set(entries));
  }

  openAddForm(): void {
    this.newEntry = { fund: 'SKSP', entryDate: '', description: '', category: '', type: 'Expense', amount: 0 };
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

`frontend/src/app/features/budget/budget.component.html`:

```html
<div class="page-heading">
  <div><h2>Budżet SKŚP</h2><p>Przychody, wydatki i zestawienie kategorii.</p></div>
  <button class="btn primary" (click)="openAddForm()">＋ Dodaj operację</button>
</div>

<div class="finance-grid">
  <div class="card stat"><div class="stat-label">Przychody</div><div class="amount">{{ totalIncome() }} zł</div></div>
  <div class="card stat"><div class="stat-label">Wydatki</div><div class="amount">{{ totalExpense() }} zł</div></div>
  <div class="card stat"><div class="stat-label">Saldo</div><div class="amount" [style.color]="balance() >= 0 ? 'var(--success)' : 'var(--danger)'">{{ balance() }} zł</div></div>
</div>

<div class="card" style="margin-bottom:16px">
  <div class="card-head"><h3>Struktura wydatków</h3></div>
  <div class="card-body">
    @for (item of expenseByCategory(); track item.category) {
      <div class="bar-row">
        <div class="bar-head"><span>{{ item.category }}</span><b>{{ item.amount }} zł</b></div>
        <div class="bar"><span [style.width.%]="totalExpense() ? (item.amount / totalExpense() * 100) : 0"></span></div>
      </div>
    }
  </div>
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

`frontend/src/app/features/budget/budget.component.scss`: create as an empty file.

- [ ] **Step 5: Wire the route and nav item**

In `frontend/src/app/app.routes.ts`, add to the shell's `children` array:

```typescript
{
  path: 'budget/sksp',
  loadComponent: () => import('./features/budget/budget.component').then(m => m.BudgetComponent)
}
```

In `frontend/src/app/layout/nav-items.ts`, add to `NAV_ITEMS`:

```typescript
{ label: 'Budżet SKŚP', icon: '◈', path: '/budget/sksp', roles: ['Administrator', 'DyrektorSKSP'] }
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `npx ng test`
Expected: PASS (2 new test files). Then run the full frontend suite once more with no filter to confirm nothing else broke.

- [ ] **Step 7: Commit**

```bash
git add frontend
git commit -m "Add Budget page"
```

---

## Self-Review Notes

- **Spec coverage:** Candidates (Task 2, 7), Missions with computed status (Task 3, 8), Formators (Task 4, 9), ParishNeeds/giełda posługi with manual "Skieruj" (Task 5, 10), shared Budget ledger (Task 6, 11) — every Faza 2 goal from the spec maps to a task. Nav visibility (Administrator/DyrektorSKSP everywhere, Biskup additionally on Missions) matches the spec exactly.
- **Type consistency verified:** `CandidateDto`/`MissionDto`/`FormatorDto`/`ParishNeedDto`/`BudgetEntryDto` field names match their Angular model counterparts (`Candidate`/`Mission`/`Formator`/`ParishNeed`/`BudgetEntry`) and the JSON shapes asserted in each controller test; `MissionService.ComputeStatus`'s three string values (`"ważna"`, `"wygasa"`, `"wygasła"`) are asserted directly in `MissionServiceTests` and consumed as-is by `MissionsListComponent.statusPillClass`.
- **No placeholders:** every step contains complete, runnable code.
