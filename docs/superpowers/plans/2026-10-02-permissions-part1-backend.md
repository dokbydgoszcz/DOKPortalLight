# Uprawnienia oparte o polityki — część 1: rdzeń backendu — plan implementacji

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Zastąpić `[Authorize(Roles = ...)]` w kontrolerach nazwanymi uprawnieniami (`Moduł.Akcja`) przypisanymi rolom w bazie, z polityką ASP.NET opartą o `IPermissionService` (cache 60 s) i claimami `permission` w JWT; odczyty zaostrzyć zgodnie ze specyfikacją.

**Architecture:** Katalog uprawnień i domyślne przydziały w `Domain`; tabela `RolePermissions` + `PermissionService` (z `IMemoryCache`) w `Infrastructure`; `PermissionRequirement`/handler/`PermissionPolicyProvider`/`HasPermissionAttribute` w `Api`. Administrator ma implicit wszystkie uprawnienia i nie jest zapisany w tabeli. Budżet i notatki duszpasterskie sprawdzają uprawnienie imperatywnie przez `IAuthorizationService`.

**Tech Stack:** ASP.NET Core 8, EF Core 8 (SQL Server / InMemory / SQLite w testach), xUnit, `IMemoryCache`.

**Spec:** `docs/superpowers/specs/2026-10-02-permissions-design.md` (część 1). Odstępstwa od specyfikacji: stałe uprawnień są płaskie (`Permissions.PeopleManage` = `"People.Manage"`), bo C# nie pozwala na zagnieżdżoną klasę `Permissions.Permissions`; katalog ma 41 uprawnień (w specyfikacji błędnie napisano 43 — poprawka w Task 9).

## Global Constraints

- Wartości uprawnień mają format `Moduł.Akcja` (np. `People.Manage`); katalog (41 pozycji) jest jedynym źródłem nazw.
- Administrator zawsze ma wszystkie uprawnienia (implicit), nie jest edytowalny i nie ma wierszy w `RolePermissions`.
- Seed domyślnych przydziałów wykonuje się tylko, gdy tabela `RolePermissions` jest pusta (nie nadpisuje edycji admina).
- Cache przydziałów: jeden wpis w `IMemoryCache`, TTL 60 s, czyszczony po `UpdateRolePermissionsAsync`.
- Backend nie ufa claimom `permission` z tokenu — autoryzacja zawsze czyta role z tokenu i przydziały z `IPermissionService`.
- Otwarte dla każdego zalogowanego (zwykłe `[Authorize]`, bez uprawnienia): `GET api/people*`, `GET api/parishes`, `GET api/name-days/*`, `GET api/dashboard/*`, `api/auth/login` (anonimowo).
- Mapowanie akcji: `*Manage` = tworzenie/edycja/usuwanie (mailing: utworzenie i wysyłka; dokumenty spraw DOK: tworzenie, edycja, upload; potrzeby parafii: dodanie, przypisanie, usunięcie); `*View` = odczyty (w tym `download` dokumentów spraw); `Documents.Generate` = `POST api/documents/generate`.
- Błędy walidacji w serwisie to `InvalidOperationException` (handler zwraca 400).
- Commity i push bezpośrednio na `master`, bez PR. Nie commitować `backend/src/DokPortal.Api/appsettings.Development.json` ani `.claude/` (stage'ować tylko wskazane pliki).
- Stopka commita: `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.
- Komendy uruchamiać z `C:\eu02_install\DOKPortalLight\backend`.

---

### Task 1: Katalog uprawnień i domyślne przydziały (Domain)

**Files:**
- Create: `backend/src/DokPortal.Domain/Constants/Permissions.cs`
- Create: `backend/src/DokPortal.Domain/Constants/DefaultRolePermissions.cs`
- Test: `backend/tests/DokPortal.Domain.Tests/PermissionCatalogTests.cs`

**Interfaces:**
- Produces: `static class Permissions` (41 stałych `const string`, nazwy poniżej); `sealed record PermissionInfo(string Name, string Module, string Label)`; `static class PermissionCatalog { IReadOnlyList<PermissionInfo> All; IReadOnlySet<string> AllNames; bool IsKnown(string name) }`; `static class DefaultRolePermissions { IReadOnlyDictionary<string, string[]> Grants }` (klucz = nazwa roli ≠ Administrator).

- [ ] **Step 1: Test (czerwony)** — `PermissionCatalogTests.cs`:

```csharp
using System.Reflection;
using System.Text.RegularExpressions;
using DokPortal.Domain.Constants;
using Xunit;

namespace DokPortal.Domain.Tests;

public class PermissionCatalogTests
{
    private static readonly string[] DeclaredConstants = typeof(Permissions)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral)
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToArray();

    [Fact]
    public void Catalog_HasExpectedNumberOfUniquePermissions()
    {
        Assert.Equal(41, PermissionCatalog.All.Count);
        Assert.Equal(41, PermissionCatalog.AllNames.Count);
    }

    [Fact]
    public void Catalog_NamesFollowModuleDotActionFormat()
    {
        Assert.All(PermissionCatalog.All, p =>
        {
            Assert.Matches(new Regex(@"^[A-Za-z]+\.[A-Za-z]+$"), p.Name);
            Assert.False(string.IsNullOrWhiteSpace(p.Module));
            Assert.False(string.IsNullOrWhiteSpace(p.Label));
        });
    }

    [Fact]
    public void Catalog_ContainsExactlyTheDeclaredConstants()
    {
        Assert.Equal(DeclaredConstants.OrderBy(x => x), PermissionCatalog.AllNames.OrderBy(x => x));
    }

    [Fact]
    public void IsKnown_RecognizesCatalogNamesOnly()
    {
        Assert.True(PermissionCatalog.IsKnown(Permissions.PeopleManage));
        Assert.False(PermissionCatalog.IsKnown("People.Fly"));
    }

    [Fact]
    public void DefaultGrants_UseKnownPermissionsAndEditableRolesOnly()
    {
        foreach (var (role, permissions) in DefaultRolePermissions.Grants)
        {
            Assert.Contains(role, AppRoles.All);
            Assert.NotEqual(AppRoles.Administrator, role);
            Assert.Equal(permissions.Length, permissions.Distinct().Count());
            Assert.All(permissions, p => Assert.True(PermissionCatalog.IsKnown(p), $"{role}: nieznane uprawnienie {p}"));
        }
    }
}
```

- [ ] **Step 2: Uruchom — czerwone (nie kompiluje się)**

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test tests/DokPortal.Domain.Tests 2>&1 | grep -E "error|Powodzenie|niepowodzenie" | head -3
```

- [ ] **Step 3: Implementacja** — `Permissions.cs`:

```csharp
namespace DokPortal.Domain.Constants;

public static class Permissions
{
    public const string PeopleManage = "People.Manage";
    public const string PeopleExport = "People.Export";
    public const string ParishesManage = "Parishes.Manage";
    public const string ParishesExport = "Parishes.Export";
    public const string CandidatesView = "Candidates.View";
    public const string CandidatesManage = "Candidates.Manage";
    public const string CandidatesExport = "Candidates.Export";
    public const string MissionsView = "Missions.View";
    public const string MissionsManage = "Missions.Manage";
    public const string MissionsExport = "Missions.Export";
    public const string FormatorsView = "Formators.View";
    public const string FormatorsManage = "Formators.Manage";
    public const string FormatorsExport = "Formators.Export";
    public const string ParishNeedsView = "ParishNeeds.View";
    public const string ParishNeedsManage = "ParishNeeds.Manage";
    public const string BudgetSkspView = "BudgetSksp.View";
    public const string BudgetSkspManage = "BudgetSksp.Manage";
    public const string BudgetDokView = "BudgetDok.View";
    public const string BudgetDokManage = "BudgetDok.Manage";
    public const string DokCasesView = "DokCases.View";
    public const string DokCasesManage = "DokCases.Manage";
    public const string DokCasesExport = "DokCases.Export";
    public const string CaseDocumentsView = "CaseDocuments.View";
    public const string CaseDocumentsManage = "CaseDocuments.Manage";
    public const string PastoralNotesView = "PastoralNotes.View";
    public const string PastoralNotesWrite = "PastoralNotes.Write";
    public const string PastoralNotesReadAll = "PastoralNotes.ReadAll";
    public const string MeetingsView = "Meetings.View";
    public const string MeetingsManage = "Meetings.Manage";
    public const string MeetingsExport = "Meetings.Export";
    public const string SupervisionsView = "Supervisions.View";
    public const string SupervisionsManage = "Supervisions.Manage";
    public const string SupervisionsExport = "Supervisions.Export";
    public const string DocumentsView = "Documents.View";
    public const string DocumentsGenerate = "Documents.Generate";
    public const string MailingView = "Mailing.View";
    public const string MailingManage = "Mailing.Manage";
    public const string GraduatesView = "Graduates.View";
    public const string UsersManage = "Users.Manage";
    public const string AuditLogView = "AuditLog.View";
    public const string PermissionsManage = "Permissions.Manage";
}

public sealed record PermissionInfo(string Name, string Module, string Label);

public static class PermissionCatalog
{
    public static readonly IReadOnlyList<PermissionInfo> All = new PermissionInfo[]
    {
        new(Permissions.PeopleManage, "Osoby", "Dodawanie, edycja i usuwanie osób"),
        new(Permissions.PeopleExport, "Osoby", "Eksport osób do Excela"),
        new(Permissions.ParishesManage, "Rejestr parafii", "Dodawanie i usuwanie parafii"),
        new(Permissions.ParishesExport, "Rejestr parafii", "Eksport parafii do Excela"),
        new(Permissions.CandidatesView, "Kandydaci SKŚP", "Podgląd kandydatów"),
        new(Permissions.CandidatesManage, "Kandydaci SKŚP", "Dodawanie, edycja i usuwanie kandydatów"),
        new(Permissions.CandidatesExport, "Kandydaci SKŚP", "Eksport kandydatów do Excela"),
        new(Permissions.MissionsView, "Katechiści posłani", "Podgląd misji kanonicznych"),
        new(Permissions.MissionsManage, "Katechiści posłani", "Dodawanie, edycja i usuwanie misji"),
        new(Permissions.MissionsExport, "Katechiści posłani", "Eksport misji do Excela"),
        new(Permissions.FormatorsView, "Formatorzy", "Podgląd formatorów"),
        new(Permissions.FormatorsManage, "Formatorzy", "Dodawanie, edycja i usuwanie formatorów"),
        new(Permissions.FormatorsExport, "Formatorzy", "Eksport formatorów do Excela"),
        new(Permissions.ParishNeedsView, "Parafie i giełda", "Podgląd potrzeb parafialnych"),
        new(Permissions.ParishNeedsManage, "Parafie i giełda", "Dodawanie, przypisywanie i usuwanie potrzeb"),
        new(Permissions.BudgetSkspView, "Budżet SKŚP", "Podgląd budżetu SKŚP"),
        new(Permissions.BudgetSkspManage, "Budżet SKŚP", "Dodawanie i usuwanie wpisów budżetu SKŚP"),
        new(Permissions.BudgetDokView, "Budżet DOK", "Podgląd budżetu DOK"),
        new(Permissions.BudgetDokManage, "Budżet DOK", "Dodawanie i usuwanie wpisów budżetu DOK"),
        new(Permissions.DokCasesView, "Podopieczni DOK", "Podgląd spraw DOK"),
        new(Permissions.DokCasesManage, "Podopieczni DOK", "Dodawanie, edycja i usuwanie spraw DOK"),
        new(Permissions.DokCasesExport, "Podopieczni DOK", "Eksport spraw DOK do Excela"),
        new(Permissions.CaseDocumentsView, "Dokumenty spraw DOK", "Podgląd i pobieranie dokumentów sprawy"),
        new(Permissions.CaseDocumentsManage, "Dokumenty spraw DOK", "Dodawanie, edycja i wgrywanie dokumentów sprawy"),
        new(Permissions.PastoralNotesView, "Notatki duszpasterskie", "Podgląd własnych notatek"),
        new(Permissions.PastoralNotesWrite, "Notatki duszpasterskie", "Dodawanie notatek"),
        new(Permissions.PastoralNotesReadAll, "Notatki duszpasterskie", "Podgląd notatek wszystkich autorów"),
        new(Permissions.MeetingsView, "Spotkania", "Podgląd harmonogramu i obecności"),
        new(Permissions.MeetingsManage, "Spotkania", "Dodawanie, edycja i usuwanie spotkań"),
        new(Permissions.MeetingsExport, "Spotkania", "Eksport spotkań do Excela"),
        new(Permissions.SupervisionsView, "Superwizje", "Podgląd superwizji"),
        new(Permissions.SupervisionsManage, "Superwizje", "Dodawanie, edycja i usuwanie superwizji"),
        new(Permissions.SupervisionsExport, "Superwizje", "Eksport superwizji do Excela"),
        new(Permissions.DocumentsView, "Dokumenty i pisma", "Podgląd wygenerowanych dokumentów"),
        new(Permissions.DocumentsGenerate, "Dokumenty i pisma", "Generowanie dokumentów"),
        new(Permissions.MailingView, "Mailing", "Podgląd kampanii mailingowych"),
        new(Permissions.MailingManage, "Mailing", "Tworzenie i wysyłka kampanii"),
        new(Permissions.GraduatesView, "Absolwenci", "Dostęp do ekranu absolwentów"),
        new(Permissions.UsersManage, "Użytkownicy", "Zarządzanie użytkownikami i ich rolami"),
        new(Permissions.AuditLogView, "Dziennik audytu", "Podgląd dziennika audytu"),
        new(Permissions.PermissionsManage, "Uprawnienia", "Edycja uprawnień ról")
    };

    public static readonly IReadOnlySet<string> AllNames = All.Select(p => p.Name).ToHashSet();

    public static bool IsKnown(string name) => AllNames.Contains(name);
}
```

`DefaultRolePermissions.cs`:

```csharp
namespace DokPortal.Domain.Constants;

public static class DefaultRolePermissions
{
    public static readonly IReadOnlyDictionary<string, string[]> Grants = new Dictionary<string, string[]>
    {
        [AppRoles.Biskup] = new[]
        {
            Permissions.MissionsView, Permissions.DokCasesView, Permissions.CaseDocumentsView
        },
        [AppRoles.DyrektorSKSP] = new[]
        {
            Permissions.PeopleManage, Permissions.PeopleExport,
            Permissions.CandidatesView, Permissions.CandidatesManage, Permissions.CandidatesExport,
            Permissions.MissionsView, Permissions.MissionsManage, Permissions.MissionsExport,
            Permissions.FormatorsView, Permissions.FormatorsManage, Permissions.FormatorsExport,
            Permissions.ParishNeedsView, Permissions.ParishNeedsManage,
            Permissions.BudgetSkspView, Permissions.BudgetSkspManage,
            Permissions.SupervisionsView, Permissions.SupervisionsManage, Permissions.SupervisionsExport,
            Permissions.DocumentsView, Permissions.DocumentsGenerate,
            Permissions.MailingView, Permissions.MailingManage
        },
        [AppRoles.DyrektorDOK] = new[]
        {
            Permissions.PeopleManage, Permissions.PeopleExport,
            Permissions.DokCasesView, Permissions.DokCasesManage, Permissions.DokCasesExport,
            Permissions.CaseDocumentsView, Permissions.CaseDocumentsManage,
            Permissions.PastoralNotesView, Permissions.PastoralNotesWrite, Permissions.PastoralNotesReadAll,
            Permissions.MeetingsView, Permissions.MeetingsManage, Permissions.MeetingsExport,
            Permissions.SupervisionsView, Permissions.SupervisionsManage, Permissions.SupervisionsExport,
            Permissions.DocumentsView, Permissions.DocumentsGenerate,
            Permissions.MailingView, Permissions.MailingManage,
            Permissions.BudgetDokView, Permissions.BudgetDokManage,
            Permissions.GraduatesView
        },
        [AppRoles.Superwizor] = new[]
        {
            Permissions.DokCasesView, Permissions.CaseDocumentsView,
            Permissions.SupervisionsView, Permissions.SupervisionsManage, Permissions.SupervisionsExport
        },
        [AppRoles.KatechistaProwadzacy] = new[]
        {
            Permissions.DokCasesView, Permissions.CaseDocumentsView, Permissions.CaseDocumentsManage,
            Permissions.PastoralNotesView, Permissions.PastoralNotesWrite,
            Permissions.MeetingsView, Permissions.MeetingsManage
        }
    };
}
```

- [ ] **Step 4: Uruchom — zielone**

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test tests/DokPortal.Domain.Tests 2>&1 | grep -E "error|Powodzenie|niepowodzenie"
```
Expected: `powodzenie: 6` (1 istniejący + 5 nowych).

- [ ] **Step 5: Commit**

```bash
cd C:/eu02_install/DOKPortalLight && git add backend/src/DokPortal.Domain/Constants/Permissions.cs backend/src/DokPortal.Domain/Constants/DefaultRolePermissions.cs backend/tests/DokPortal.Domain.Tests/PermissionCatalogTests.cs
git commit -m "$(cat <<'EOF'
Dodaj katalog uprawnień i domyślne przydziały ról

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: Tabela RolePermissions, migracja i seed

**Files:**
- Create: `backend/src/DokPortal.Domain/Entities/RolePermission.cs`
- Modify: `backend/src/DokPortal.Infrastructure/Persistence/AppDbContext.cs` (DbSet + konfiguracja)
- Create (generowane): `backend/src/DokPortal.Infrastructure/Migrations/*_AddRolePermissions.cs` (+ `.Designer.cs`, zmiana `AppDbContextModelSnapshot.cs`)
- Modify: `backend/src/DokPortal.Infrastructure/Seed/DbSeeder.cs`
- Test: `backend/tests/DokPortal.Infrastructure.Tests/Seed/DbSeederPermissionsTests.cs`

**Interfaces:**
- Consumes: `DefaultRolePermissions.Grants`, `PermissionCatalog` (Task 1).
- Produces: `RolePermission { string RoleName; string Permission }`; `AppDbContext.RolePermissions : DbSet<RolePermission>`; `DbSeeder.SeedRolePermissionsAsync(AppDbContext db, CancellationToken ct = default) : Task` (wstawia domyślne przydziały tylko przy pustej tabeli; wywoływane także z `SeedAsync`).

- [ ] **Step 1: Test (czerwony)** — `DbSeederPermissionsTests.cs`:

```csharp
using DokPortal.Domain.Constants;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Seed;

public class DbSeederPermissionsTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task SeedRolePermissionsAsync_InsertsDefaultGrantsForEveryRole()
    {
        await using var db = CreateContext();

        await DbSeeder.SeedRolePermissionsAsync(db);

        var expected = DefaultRolePermissions.Grants.Sum(g => g.Value.Length);
        Assert.Equal(expected, await db.RolePermissions.CountAsync());
        Assert.True(await db.RolePermissions.AnyAsync(r => r.RoleName == AppRoles.DyrektorDOK && r.Permission == Permissions.DokCasesManage));
        Assert.False(await db.RolePermissions.AnyAsync(r => r.RoleName == AppRoles.Administrator));
    }

    [Fact]
    public async Task SeedRolePermissionsAsync_DoesNotOverwriteExistingRows()
    {
        await using var db = CreateContext();
        db.RolePermissions.Add(new RolePermission { RoleName = AppRoles.KatechistaProwadzacy, Permission = Permissions.MeetingsView });
        await db.SaveChangesAsync();

        await DbSeeder.SeedRolePermissionsAsync(db);

        Assert.Equal(1, await db.RolePermissions.CountAsync());
    }
}
```

- [ ] **Step 2: Uruchom — czerwone (nie kompiluje się)**

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test tests/DokPortal.Infrastructure.Tests --filter "FullyQualifiedName~DbSeederPermissionsTests" 2>&1 | grep -E "error" | head -2
```

- [ ] **Step 3: Implementacja**

`RolePermission.cs`:

```csharp
namespace DokPortal.Domain.Entities;

public class RolePermission
{
    public required string RoleName { get; set; }
    public required string Permission { get; set; }
}
```

W `AppDbContext.cs` dodaj obok pozostałych `DbSet`: `public DbSet<RolePermission> RolePermissions => Set<RolePermission>();`, a w `OnModelCreating` obok konfiguracji `AuditLogEntry`:

```csharp
        builder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(r => new { r.RoleName, r.Permission });
            entity.Property(r => r.RoleName).IsRequired().HasMaxLength(256);
            entity.Property(r => r.Permission).IsRequired().HasMaxLength(100);
        });
```

W `DbSeeder.cs` dodaj metodę i wywołanie na końcu `SeedAsync`:

```csharp
        await SeedRolePermissionsAsync(services.GetRequiredService<AppDbContext>());
    }

    public static async Task SeedRolePermissionsAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.RolePermissions.AnyAsync(ct)) return;

        foreach (var (role, permissions) in DefaultRolePermissions.Grants)
        {
            foreach (var permission in permissions)
            {
                db.RolePermissions.Add(new RolePermission { RoleName = role, Permission = permission });
            }
        }
        await db.SaveChangesAsync(ct);
    }
```
(wywołanie `await SeedRolePermissionsAsync(...)` wstaw jako ostatnią instrukcję `SeedAsync`, po bloku tworzenia admina; metodę `SeedRolePermissionsAsync` dopisz jako osobną metodę klasy — nawiasy klamrowe dopasuj do istniejącej struktury).

- [ ] **Step 4: Migracja**

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet ef migrations add AddRolePermissions --project src/DokPortal.Infrastructure --startup-project src/DokPortal.Api 2>&1 | tail -4
```
Expected: `Done.`; w `Migrations/` powstają pliki `*_AddRolePermissions.cs` i `.Designer.cs`. Otwórz migrację i sprawdź, że zawiera wyłącznie `CreateTable("RolePermissions", ...)` z kluczem `(RoleName, Permission)` (żadnych innych zmian schematu).

- [ ] **Step 5: Uruchom — zielone**

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test tests/DokPortal.Infrastructure.Tests --filter "FullyQualifiedName~DbSeederPermissionsTests" 2>&1 | grep -E "error|Powodzenie|niepowodzenie"
```
Expected: `powodzenie: 2`.

- [ ] **Step 6: Commit**

```bash
cd C:/eu02_install/DOKPortalLight && git add backend/src/DokPortal.Domain/Entities/RolePermission.cs backend/src/DokPortal.Infrastructure/Persistence/AppDbContext.cs backend/src/DokPortal.Infrastructure/Migrations backend/src/DokPortal.Infrastructure/Seed/DbSeeder.cs backend/tests/DokPortal.Infrastructure.Tests/Seed/DbSeederPermissionsTests.cs
git status --short
git commit -m "$(cat <<'EOF'
Dodaj tabelę RolePermissions, migrację i seed domyślnych przydziałów

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: IPermissionService i PermissionService (z cache)

**Files:**
- Create: `backend/src/DokPortal.Application/Permissions/IPermissionService.cs`
- Create: `backend/src/DokPortal.Application/Permissions/PermissionMatrixDto.cs`
- Create: `backend/src/DokPortal.Infrastructure/Services/PermissionService.cs`
- Test: `backend/tests/DokPortal.Infrastructure.Tests/Services/PermissionServiceTests.cs`

**Interfaces:**
- Consumes: `AppDbContext.RolePermissions`, `PermissionCatalog`, `PermissionInfo`, `AppRoles` (Tasks 1–2).
- Produces:
  - `IPermissionService.GetPermissionsForRolesAsync(IEnumerable<string> roles, CancellationToken ct) : Task<IReadOnlySet<string>>`
  - `IPermissionService.GetMatrixAsync(CancellationToken ct) : Task<PermissionMatrixDto>`
  - `IPermissionService.UpdateRolePermissionsAsync(string role, IReadOnlyCollection<string> permissions, CancellationToken ct) : Task` (rzuca `InvalidOperationException` dla Administratora, nieznanej roli i nieznanego uprawnienia)
  - `PermissionMatrixDto { IReadOnlyList<string> Roles; IReadOnlyList<PermissionInfo> Permissions; IReadOnlyDictionary<string, IReadOnlyList<string>> Grants }`
  - `new PermissionService(AppDbContext db, IMemoryCache cache)`

- [ ] **Step 1: Test (czerwony)** — `PermissionServiceTests.cs`:

```csharp
using DokPortal.Application.Permissions;
using DokPortal.Domain.Constants;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class PermissionServiceTests
{
    private static (PermissionService Service, AppDbContext Db) Create()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        return (new PermissionService(db, new MemoryCache(new MemoryCacheOptions())), db);
    }

    private static async Task Grant(AppDbContext db, string role, params string[] permissions)
    {
        foreach (var permission in permissions)
        {
            db.RolePermissions.Add(new RolePermission { RoleName = role, Permission = permission });
        }
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Administrator_GetsEntireCatalog_WithoutAnyRows()
    {
        var (service, _) = Create();

        var permissions = await service.GetPermissionsForRolesAsync(new[] { AppRoles.Administrator }, default);

        Assert.Equal(PermissionCatalog.AllNames.Count, permissions.Count);
    }

    [Fact]
    public async Task MultipleRoles_GetUnionOfTheirPermissions_AndUnknownRoleGetsNone()
    {
        var (service, db) = Create();
        await Grant(db, AppRoles.KatechistaProwadzacy, Permissions.MeetingsView);
        await Grant(db, AppRoles.DyrektorDOK, Permissions.MeetingsManage);

        var union = await service.GetPermissionsForRolesAsync(new[] { AppRoles.KatechistaProwadzacy, AppRoles.DyrektorDOK }, default);
        var none = await service.GetPermissionsForRolesAsync(new[] { "NieistniejacaRola" }, default);

        Assert.Equal(new[] { Permissions.MeetingsManage, Permissions.MeetingsView }, union.OrderBy(x => x));
        Assert.Empty(none);
    }

    [Fact]
    public async Task UpdateRolePermissions_ReplacesGrants_AndInvalidatesCache()
    {
        var (service, db) = Create();
        await Grant(db, AppRoles.KatechistaProwadzacy, Permissions.MeetingsView);
        var before = await service.GetPermissionsForRolesAsync(new[] { AppRoles.KatechistaProwadzacy }, default);
        Assert.Contains(Permissions.MeetingsView, before);

        await service.UpdateRolePermissionsAsync(AppRoles.KatechistaProwadzacy, new[] { Permissions.CandidatesView }, default);

        var after = await service.GetPermissionsForRolesAsync(new[] { AppRoles.KatechistaProwadzacy }, default);
        Assert.Equal(new[] { Permissions.CandidatesView }, after.ToArray());
        Assert.Equal(1, await db.RolePermissions.CountAsync());
    }

    [Fact]
    public async Task UpdateRolePermissions_WithEmptyList_RemovesAllGrantsOfTheRole()
    {
        var (service, db) = Create();
        await Grant(db, AppRoles.Superwizor, Permissions.SupervisionsView, Permissions.SupervisionsManage);

        await service.UpdateRolePermissionsAsync(AppRoles.Superwizor, Array.Empty<string>(), default);

        Assert.Empty(await service.GetPermissionsForRolesAsync(new[] { AppRoles.Superwizor }, default));
    }

    [Theory]
    [InlineData("Administrator", "People.Manage")]
    [InlineData("NieistniejacaRola", "People.Manage")]
    [InlineData("DyrektorDOK", "People.Fly")]
    public async Task UpdateRolePermissions_RejectsAdministratorUnknownRoleAndUnknownPermission(string role, string permission)
    {
        var (service, _) = Create();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateRolePermissionsAsync(role, new[] { permission }, default));
    }

    [Fact]
    public async Task GetMatrix_ReturnsCatalogEditableRolesAndGrantsIncludingEmptyOnes()
    {
        var (service, db) = Create();
        await Grant(db, AppRoles.DyrektorDOK, Permissions.DokCasesView);

        var matrix = await service.GetMatrixAsync(default);

        Assert.Equal(PermissionCatalog.All.Count, matrix.Permissions.Count);
        Assert.DoesNotContain(AppRoles.Administrator, matrix.Roles);
        Assert.Equal(AppRoles.All.Length - 1, matrix.Roles.Count);
        Assert.Equal(new[] { Permissions.DokCasesView }, matrix.Grants[AppRoles.DyrektorDOK].ToArray());
        Assert.Empty(matrix.Grants[AppRoles.Biskup]);
    }
}
```

- [ ] **Step 2: Uruchom — czerwone (nie kompiluje się)**

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test tests/DokPortal.Infrastructure.Tests --filter "FullyQualifiedName~PermissionServiceTests" 2>&1 | grep -E "error" | head -2
```

- [ ] **Step 3: Implementacja**

`Application/Permissions/PermissionMatrixDto.cs`:

```csharp
using DokPortal.Domain.Constants;

namespace DokPortal.Application.Permissions;

public class PermissionMatrixDto
{
    public required IReadOnlyList<string> Roles { get; init; }
    public required IReadOnlyList<PermissionInfo> Permissions { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> Grants { get; init; }
}
```

`Application/Permissions/IPermissionService.cs`:

```csharp
namespace DokPortal.Application.Permissions;

public interface IPermissionService
{
    Task<IReadOnlySet<string>> GetPermissionsForRolesAsync(IEnumerable<string> roles, CancellationToken ct);
    Task<PermissionMatrixDto> GetMatrixAsync(CancellationToken ct);
    Task UpdateRolePermissionsAsync(string role, IReadOnlyCollection<string> permissions, CancellationToken ct);
}
```

`Infrastructure/Services/PermissionService.cs`:

```csharp
using DokPortal.Application.Permissions;
using DokPortal.Domain.Constants;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DokPortal.Infrastructure.Services;

public class PermissionService : IPermissionService
{
    private const string CacheKey = "role-permissions-map";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public PermissionService(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<IReadOnlySet<string>> GetPermissionsForRolesAsync(IEnumerable<string> roles, CancellationToken ct)
    {
        var roleList = roles.ToList();
        if (roleList.Contains(AppRoles.Administrator))
        {
            return PermissionCatalog.AllNames;
        }

        var map = await GetMapAsync(ct);
        var result = new HashSet<string>();
        foreach (var role in roleList)
        {
            if (map.TryGetValue(role, out var permissions))
            {
                result.UnionWith(permissions);
            }
        }
        return result;
    }

    public async Task<PermissionMatrixDto> GetMatrixAsync(CancellationToken ct)
    {
        var map = await GetMapAsync(ct);
        var editableRoles = AppRoles.All.Where(r => r != AppRoles.Administrator).ToList();

        return new PermissionMatrixDto
        {
            Roles = editableRoles,
            Permissions = PermissionCatalog.All,
            Grants = editableRoles.ToDictionary(
                role => role,
                role => (IReadOnlyList<string>)(map.TryGetValue(role, out var permissions)
                    ? permissions.OrderBy(p => p).ToList()
                    : new List<string>()))
        };
    }

    public async Task UpdateRolePermissionsAsync(string role, IReadOnlyCollection<string> permissions, CancellationToken ct)
    {
        if (role == AppRoles.Administrator)
        {
            throw new InvalidOperationException("Uprawnień Administratora nie można zmieniać — zawsze ma wszystkie.");
        }
        if (!AppRoles.All.Contains(role))
        {
            throw new InvalidOperationException($"Nieznana rola: {role}.");
        }
        var unknown = permissions.FirstOrDefault(p => !PermissionCatalog.IsKnown(p));
        if (unknown is not null)
        {
            throw new InvalidOperationException($"Nieznane uprawnienie: {unknown}.");
        }

        var existing = await _db.RolePermissions.Where(r => r.RoleName == role).ToListAsync(ct);
        _db.RolePermissions.RemoveRange(existing);
        foreach (var permission in permissions.Distinct())
        {
            _db.RolePermissions.Add(new RolePermission { RoleName = role, Permission = permission });
        }
        await _db.SaveChangesAsync(ct);

        _cache.Remove(CacheKey);
    }

    private async Task<Dictionary<string, HashSet<string>>> GetMapAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(CacheKey, out Dictionary<string, HashSet<string>>? cached) && cached is not null)
        {
            return cached;
        }

        var rows = await _db.RolePermissions.AsNoTracking().ToListAsync(ct);
        var map = rows
            .GroupBy(r => r.RoleName)
            .ToDictionary(g => g.Key, g => g.Select(r => r.Permission).ToHashSet());
        _cache.Set(CacheKey, map, CacheTtl);
        return map;
    }
}
```

- [ ] **Step 4: Uruchom — zielone**

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test tests/DokPortal.Infrastructure.Tests --filter "FullyQualifiedName~PermissionServiceTests" 2>&1 | grep -E "error|Powodzenie|niepowodzenie"
```
Expected: `powodzenie: 8` (5 faktów + 3 przypadki Theory).

- [ ] **Step 5: Commit**

```bash
cd C:/eu02_install/DOKPortalLight && git add backend/src/DokPortal.Application/Permissions backend/src/DokPortal.Infrastructure/Services/PermissionService.cs backend/tests/DokPortal.Infrastructure.Tests/Services/PermissionServiceTests.cs
git commit -m "$(cat <<'EOF'
Dodaj PermissionService z cache i walidacją zmian przydziałów

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: Claimy `permission` w JWT, rejestracja DI i seed w testach integracyjnych

**Files:**
- Modify: `backend/src/DokPortal.Application/Auth/IJwtTokenGenerator.cs`
- Modify: `backend/src/DokPortal.Infrastructure/Auth/JwtTokenGenerator.cs`
- Modify: `backend/src/DokPortal.Api/Controllers/AuthController.cs`
- Modify: `backend/src/DokPortal.Api/Program.cs` (`using DokPortal.Application.Permissions;`, `AddMemoryCache`, `AddScoped<IPermissionService, PermissionService>`)
- Modify: `backend/tests/DokPortal.Api.IntegrationTests/CustomWebApplicationFactory.cs` (seed przydziałów)
- Test: `backend/tests/DokPortal.Infrastructure.Tests/Auth/JwtTokenGeneratorTests.cs`, `backend/tests/DokPortal.Api.IntegrationTests/AuthControllerTests.cs`

**Interfaces:**
- Consumes: `IPermissionService.GetPermissionsForRolesAsync`, `DbSeeder.SeedRolePermissionsAsync` (Tasks 2–3).
- Produces: `IJwtTokenGenerator.GenerateToken(string userId, string email, Guid? personId, IEnumerable<string> roles, IEnumerable<string> permissions) : string` (dodaje po jednym claimie `permission`); `IPermissionService` zarejestrowany w DI; baza testów integracyjnych ma zaseedowane domyślne przydziały.

- [ ] **Step 1: Testy (czerwone)**

W `JwtTokenGeneratorTests.cs` zmień istniejące wywołanie na 5-argumentowe i dodaj asercję oraz nowy test:

```csharp
        var token = generator.GenerateToken("user-1", "a@b.pl", personId, new[] { "Administrator" }, new[] { "People.Manage" });

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Contains(jwt.Claims, c => c.Type == "role" && c.Value == "Administrator");
        Assert.Contains(jwt.Claims, c => c.Type == "personId" && c.Value == personId.ToString());
        Assert.Contains(jwt.Claims, c => c.Type == "permission" && c.Value == "People.Manage");
```
(oraz zachowaj `var options`/`generator`/`personId` jak dotąd). Dodaj test:

```csharp
    [Fact]
    public void GenerateToken_AddsOnePermissionClaimPerPermission()
    {
        var options = new JwtOptions { Key = "unit-test-signing-key-1234567890123456", Issuer = "test", Audience = "test", ExpiryMinutes = 60 };

        var token = new JwtTokenGenerator(options).GenerateToken(
            "user-1", "a@b.pl", null, new[] { "DyrektorDOK" }, new[] { "DokCases.View", "DokCases.Manage" });

        var permissions = new JwtSecurityTokenHandler().ReadJwtToken(token).Claims
            .Where(c => c.Type == "permission").Select(c => c.Value).OrderBy(v => v).ToArray();
        Assert.Equal(new[] { "DokCases.Manage", "DokCases.View" }, permissions);
    }
```

W `AuthControllerTests.cs` dodaj (z `using System.IdentityModel.Tokens.Jwt; using System.Net.Http.Json; using DokPortal.Application.Auth; using DokPortal.Domain.Constants;` jeśli ich brak):

```csharp
    [Fact]
    public async Task Login_TokenContainsPermissionClaimsMatchingTheRole()
    {
        var email = $"dyr-{Guid.NewGuid():N}@example.org";
        await CreateUserAndGetTokenAsync(email, "Sekret123!", "DyrektorDOK");

        var response = await Client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = "Sekret123!" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();

        var permissions = new JwtSecurityTokenHandler().ReadJwtToken(body!.Token).Claims
            .Where(c => c.Type == "permission").Select(c => c.Value).ToList();
        Assert.Contains(Permissions.DokCasesManage, permissions);
        Assert.DoesNotContain(Permissions.CandidatesManage, permissions);
    }

    [Fact]
    public async Task Login_AdministratorTokenContainsEntireCatalog()
    {
        var email = $"admin-{Guid.NewGuid():N}@example.org";
        await CreateUserAndGetTokenAsync(email, "Sekret123!", "Administrator");

        var response = await Client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = "Sekret123!" });
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();

        var permissions = new JwtSecurityTokenHandler().ReadJwtToken(body!.Token).Claims
            .Where(c => c.Type == "permission").ToList();
        Assert.Equal(PermissionCatalog.All.Count, permissions.Count);
    }
```
(`CreateUserAndGetTokenAsync` samo loguje się w środku, więc drugi login w teście jest poprawny — oba przypadki sprawdzają świeży token z endpointu.)

- [ ] **Step 2: Uruchom — czerwone (nie kompiluje się: brak 5-argumentowego `GenerateToken`)**

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test 2>&1 | grep -E "error" | head -3
```

- [ ] **Step 3: Implementacja**

`IJwtTokenGenerator.cs`:

```csharp
namespace DokPortal.Application.Auth;

public interface IJwtTokenGenerator
{
    string GenerateToken(string userId, string email, Guid? personId, IEnumerable<string> roles, IEnumerable<string> permissions);
}
```

`JwtTokenGenerator.cs`: zmień sygnaturę metody na `GenerateToken(string userId, string email, Guid? personId, IEnumerable<string> roles, IEnumerable<string> permissions)` i pod linią `claims.AddRange(roles.Select(...))` dodaj:

```csharp
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
```

`AuthController.cs`: dodaj `using DokPortal.Application.Permissions;`, pole `private readonly IPermissionService _permissionService;`, parametr konstruktora `IPermissionService permissionService` (przypisz do pola) i zamień generowanie tokenu na:

```csharp
        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await _permissionService.GetPermissionsForRolesAsync(roles, ct);
        var token = _tokenGenerator.GenerateToken(user.Id, user.Email!, user.PersonId, roles, permissions);
```

`Program.cs`: dodaj `using DokPortal.Application.Permissions;` obok pozostałych `using DokPortal.Application...`, a obok `AddScoped<IAuditLogService, AuditLogService>();`:

```csharp
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IPermissionService, PermissionService>();
```

`CustomWebApplicationFactory.cs`: po pętli tworzącej role dodaj (z `using DokPortal.Infrastructure.Seed;`):

```csharp
            DbSeeder.SeedRolePermissionsAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>())
                .GetAwaiter().GetResult();
```

- [ ] **Step 4: Uruchom cały backend — zielone**

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test 2>&1 | grep -E "error|Powodzenie|niepowodzenie"
```
Expected: brak niepowodzeń (kontrolery nadal używają ról).

- [ ] **Step 5: Commit**

```bash
cd C:/eu02_install/DOKPortalLight && git add backend/src/DokPortal.Application/Auth/IJwtTokenGenerator.cs backend/src/DokPortal.Infrastructure/Auth/JwtTokenGenerator.cs backend/src/DokPortal.Api/Controllers/AuthController.cs backend/src/DokPortal.Api/Program.cs backend/tests/DokPortal.Api.IntegrationTests/CustomWebApplicationFactory.cs backend/tests/DokPortal.Infrastructure.Tests/Auth/JwtTokenGeneratorTests.cs backend/tests/DokPortal.Api.IntegrationTests/AuthControllerTests.cs
git commit -m "$(cat <<'EOF'
Dodaj claimy permission do JWT i zarejestruj PermissionService

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: Infrastruktura polityk + konwersja Osób, Parafii, Użytkowników, Audytu i Eksportu

**Files:**
- Create: `backend/src/DokPortal.Api/Authorization/PermissionRequirement.cs`
- Create: `backend/src/DokPortal.Api/Authorization/PermissionAuthorizationHandler.cs`
- Create: `backend/src/DokPortal.Api/Authorization/PermissionPolicyProvider.cs`
- Create: `backend/src/DokPortal.Api/Authorization/HasPermissionAttribute.cs`
- Modify: `backend/src/DokPortal.Api/Program.cs` (rejestracja)
- Modify: `PeopleController.cs`, `ParishesController.cs`, `UsersController.cs`, `AuditLogController.cs`, `ExportController.cs` (w `backend/src/DokPortal.Api/Controllers/`)
- Test: `backend/tests/DokPortal.Api.IntegrationTests/PermissionAuthorizationTests.cs`

**Interfaces:**
- Consumes: `IPermissionService.GetPermissionsForRolesAsync`, `PermissionCatalog.IsKnown`, stałe `Permissions.*` (Tasks 1, 3).
- Produces: `[HasPermission(string permission)]` (dziedziczy po `AuthorizeAttribute`, `Policy = permission`); `PermissionRequirement(string Permission)`; polityka dla każdej nazwy z katalogu (wymaga zalogowania). Kolejne zadania używają tylko `[HasPermission(Permissions.X)]` i `using DokPortal.Api.Authorization;`.

- [ ] **Step 1: Test (czerwony)** — `PermissionAuthorizationTests.cs`:

```csharp
using System.Net;
using DokPortal.Api.Authorization;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class PermissionAuthorizationTests : IntegrationTestBase
{
    public PermissionAuthorizationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task PolicyProvider_CreatesPolicyForKnownPermission_AndDelegatesOthers()
    {
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));

        var known = await provider.GetPolicyAsync(Permissions.PeopleManage);
        var unknown = await provider.GetPolicyAsync("NieistniejacaPolityka");

        Assert.NotNull(known);
        Assert.Contains(known!.Requirements, r => r is PermissionRequirement p && p.Permission == Permissions.PeopleManage);
        Assert.Null(unknown);
    }

    [Theory]
    [InlineData("/api/users", "Administrator", HttpStatusCode.OK)]
    [InlineData("/api/users", "DyrektorDOK", HttpStatusCode.Forbidden)]
    [InlineData("/api/audit-log", "Administrator", HttpStatusCode.OK)]
    [InlineData("/api/audit-log", "DyrektorSKSP", HttpStatusCode.Forbidden)]
    public async Task AdminOnlyEndpoints_AreGatedByPermission(string url, string role, HttpStatusCode expected)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

        var response = await client.GetAsync(url);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task AdminOnlyEndpoint_ReturnsUnauthorized_WithoutToken()
    {
        var response = await Client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("DyrektorSKSP", HttpStatusCode.Created)]
    [InlineData("DyrektorDOK", HttpStatusCode.Created)]
    [InlineData("Superwizor", HttpStatusCode.Forbidden)]
    [InlineData("Biskup", HttpStatusCode.Forbidden)]
    public async Task People_Create_RequiresManagePermission(string role, HttpStatusCode expected)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

        var response = await client.PostAsJsonAsync("/api/people", new { FirstName = "Test", LastName = "User" });

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("KatechistaProwadzacy")]
    [InlineData("Biskup")]
    public async Task People_AndParishes_Lists_AreOpenToEveryAuthenticatedUser(string role)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/people")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/parishes")).StatusCode);
    }
}
```
(dodaj `using System.Net.Http.Json;` na górze pliku).

- [ ] **Step 2: Uruchom — czerwone (nie kompiluje się: brak `DokPortal.Api.Authorization`)**

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test tests/DokPortal.Api.IntegrationTests --filter "FullyQualifiedName~PermissionAuthorizationTests" 2>&1 | grep -E "error" | head -2
```

- [ ] **Step 3: Infrastruktura** — pliki w `backend/src/DokPortal.Api/Authorization/`:

`PermissionRequirement.cs`:
```csharp
using Microsoft.AspNetCore.Authorization;

namespace DokPortal.Api.Authorization;

public class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permission) => Permission = permission;

    public string Permission { get; }
}
```

`PermissionAuthorizationHandler.cs`:
```csharp
using DokPortal.Application.Permissions;
using Microsoft.AspNetCore.Authorization;

namespace DokPortal.Api.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionService _permissionService;

    public PermissionAuthorizationHandler(IPermissionService permissionService) => _permissionService = permissionService;

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var roles = context.User.FindAll("role").Select(c => c.Value).ToList();
        if (roles.Count == 0)
        {
            return;
        }

        var permissions = await _permissionService.GetPermissionsForRolesAsync(roles, CancellationToken.None);
        if (permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}
```

`PermissionPolicyProvider.cs`:
```csharp
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace DokPortal.Api.Authorization;

public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) => _fallback = new DefaultAuthorizationPolicyProvider(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (PermissionCatalog.IsKnown(policyName))
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }
}
```

`HasPermissionAttribute.cs`:
```csharp
using Microsoft.AspNetCore.Authorization;

namespace DokPortal.Api.Authorization;

public class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission) => Policy = permission;
}
```

`Program.cs` — dodaj `using DokPortal.Api.Authorization;`, `using Microsoft.AspNetCore.Authorization;` i zaraz po `builder.Services.AddAuthorization();`:

```csharp
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
```

- [ ] **Step 4: Konwersja kontrolerów (z `backend/src/DokPortal.Api/Controllers`)**

```bash
cd C:/eu02_install/DOKPortalLight/backend/src/DokPortal.Api/Controllers
# People: zapisy → Manage (GET zostają otwarte)
sed -i 's|^\(\s*\)\[Authorize(Roles = .*)\]\s*$|\1[HasPermission(Permissions.PeopleManage)]|' PeopleController.cs
# Parishes: zapisy → Manage
sed -i 's|^\(\s*\)\[Authorize(Roles = .*)\]\s*$|\1[HasPermission(Permissions.ParishesManage)]|' ParishesController.cs
# Users / AuditLog: atrybut klasy
sed -i 's|^\[Authorize(Roles = AppRoles.Administrator)\]|[HasPermission(Permissions.UsersManage)]|' UsersController.cs
sed -i 's|^\[Authorize(Roles = AppRoles.Administrator)\]|[HasPermission(Permissions.AuditLogView)]|' AuditLogController.cs
for f in PeopleController.cs ParishesController.cs UsersController.cs AuditLogController.cs; do sed -i '1i using DokPortal.Api.Authorization;' $f; done
grep -n "HasPermission\|Authorize" PeopleController.cs ParishesController.cs UsersController.cs AuditLogController.cs
```
Sprawdź, że w `People`/`Parishes` zostaje klasowe `[Authorize]`, a trzy (people) i dwie (parishes) akcje zapisu mają `[HasPermission(...)]`; w `Users`/`AuditLog` atrybut klasy to `[HasPermission(...)]` (jeśli plik ma `using Microsoft.AspNetCore.Authorization;` pozostaje — nadal potrzebne w pozostałych kontrolerach; nieużywany `using` nie jest błędem).

Zastąp `ExportController.cs` całą zawartością:

```csharp
using System.IdentityModel.Tokens.Jwt;
using DokPortal.Api.Authorization;
using DokPortal.Application.AuditLog;
using DokPortal.Application.Export;
using DokPortal.Domain.Constants;
using DokPortal.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/export")]
[Authorize]
public class ExportController : ControllerBase
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IExportService _exportService;
    private readonly IAuditLogService _auditLogService;

    public ExportController(IExportService exportService, IAuditLogService auditLogService)
    {
        _exportService = exportService;
        _auditLogService = auditLogService;
    }

    [HttpGet("people")]
    [HasPermission(Permissions.PeopleExport)]
    public Task<IActionResult> People(CancellationToken ct) => ExportAsync("people", "osoby", _exportService.ExportPeopleAsync, ct);

    [HttpGet("dok-cases")]
    [HasPermission(Permissions.DokCasesExport)]
    public Task<IActionResult> DokCases(CancellationToken ct) => ExportAsync("dok-cases", "podopieczni-dok", _exportService.ExportDokCasesAsync, ct);

    [HttpGet("candidates")]
    [HasPermission(Permissions.CandidatesExport)]
    public Task<IActionResult> Candidates(CancellationToken ct) => ExportAsync("candidates", "kandydaci-sksp", _exportService.ExportCandidatesAsync, ct);

    [HttpGet("missions")]
    [HasPermission(Permissions.MissionsExport)]
    public Task<IActionResult> Missions(CancellationToken ct) => ExportAsync("missions", "katechisci-poslani", _exportService.ExportMissionsAsync, ct);

    [HttpGet("formators")]
    [HasPermission(Permissions.FormatorsExport)]
    public Task<IActionResult> Formators(CancellationToken ct) => ExportAsync("formators", "formatorzy", _exportService.ExportFormatorsAsync, ct);

    [HttpGet("supervisions")]
    [HasPermission(Permissions.SupervisionsExport)]
    public Task<IActionResult> Supervisions(CancellationToken ct) => ExportAsync("supervisions", "superwizje", _exportService.ExportSupervisionsAsync, ct);

    [HttpGet("meetings")]
    [HasPermission(Permissions.MeetingsExport)]
    public Task<IActionResult> Meetings(CancellationToken ct) => ExportAsync("meetings", "spotkania", _exportService.ExportMeetingsAsync, ct);

    [HttpGet("parishes")]
    [HasPermission(Permissions.ParishesExport)]
    public Task<IActionResult> Parishes(CancellationToken ct) => ExportAsync("parishes", "parafie", _exportService.ExportParishesAsync, ct);

    private async Task<IActionResult> ExportAsync(
        string listName, string fileName, Func<CancellationToken, Task<byte[]>> export, CancellationToken ct)
    {
        var bytes = await export(ct);

        await _auditLogService.LogAsync(
            User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value,
            User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value,
            "ExportData", listName, AuditResult.Allowed, ct);

        return File(bytes, XlsxContentType, $"{fileName}-{DateTime.UtcNow:yyyy-MM-dd}.xlsx");
    }
}
```

- [ ] **Step 5: Uruchom cały backend — zielone**

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test 2>&1 | grep -E "error|Powodzenie|niepowodzenie"
```
Expected: brak niepowodzeń — w tym istniejące `ExportControllerTests` (role → uprawnienia dają te same wyniki), `UsersControllerTests`, `AuditLogControllerTests`, `PeopleControllerTests`, `ParishesControllerTests`.

- [ ] **Step 6: Commit**

```bash
cd C:/eu02_install/DOKPortalLight && git add backend/src/DokPortal.Api/Authorization backend/src/DokPortal.Api/Program.cs backend/src/DokPortal.Api/Controllers/PeopleController.cs backend/src/DokPortal.Api/Controllers/ParishesController.cs backend/src/DokPortal.Api/Controllers/UsersController.cs backend/src/DokPortal.Api/Controllers/AuditLogController.cs backend/src/DokPortal.Api/Controllers/ExportController.cs backend/tests/DokPortal.Api.IntegrationTests/PermissionAuthorizationTests.cs
git commit -m "$(cat <<'EOF'
Dodaj polityki uprawnień i przestaw Osoby, Parafie, Użytkowników, Audyt i Eksport

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: Kontrolery SKŚP — Kandydaci, Misje, Formatorzy, Potrzeby parafii (View + Manage)

**Files:**
- Modify: `CandidatesController.cs`, `MissionsController.cs`, `FormatorsController.cs`, `ParishNeedsController.cs` (w `backend/src/DokPortal.Api/Controllers/`)
- Test: `backend/tests/DokPortal.Api.IntegrationTests/PermissionAuthorizationTests.cs` (dopisanie)

**Interfaces:**
- Consumes: `[HasPermission]` i `Permissions.*` (Task 5).
- Produces: odczyty `GET` tych czterech kontrolerów wymagają `*View`; zapisy `*Manage`.

- [ ] **Step 1: Testy (czerwone)** — dopisz do klasy `PermissionAuthorizationTests`:

```csharp
    [Theory]
    [InlineData("/api/candidates", "DyrektorSKSP", HttpStatusCode.OK)]
    [InlineData("/api/candidates", "KatechistaProwadzacy", HttpStatusCode.Forbidden)]
    [InlineData("/api/candidates", "Biskup", HttpStatusCode.Forbidden)]
    [InlineData("/api/missions", "DyrektorSKSP", HttpStatusCode.OK)]
    [InlineData("/api/missions", "Biskup", HttpStatusCode.OK)]
    [InlineData("/api/missions", "KatechistaProwadzacy", HttpStatusCode.Forbidden)]
    [InlineData("/api/formators", "DyrektorSKSP", HttpStatusCode.OK)]
    [InlineData("/api/formators", "KatechistaProwadzacy", HttpStatusCode.Forbidden)]
    [InlineData("/api/parish-needs", "DyrektorSKSP", HttpStatusCode.OK)]
    [InlineData("/api/parish-needs", "KatechistaProwadzacy", HttpStatusCode.Forbidden)]
    [InlineData("/api/parish-needs", "DyrektorDOK", HttpStatusCode.Forbidden)]
    public async Task SkspModules_Reads_RequireViewPermission(string url, string role, HttpStatusCode expected)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

        var response = await client.GetAsync(url);

        Assert.Equal(expected, response.StatusCode);
    }
```

- [ ] **Step 2: Uruchom — czerwone (GET dziś otwarte: Forbidden-przypadki zwracają 200)**

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test tests/DokPortal.Api.IntegrationTests --filter "FullyQualifiedName~SkspModules_Reads" 2>&1 | grep -E "Powodzenie|Niepowodzenie|niepowodzenie" | head -3
```

- [ ] **Step 3: Konwersja**

```bash
cd C:/eu02_install/DOKPortalLight/backend/src/DokPortal.Api/Controllers
for pair in CandidatesController:Candidates MissionsController:Missions FormatorsController:Formators ParishNeedsController:ParishNeeds; do
  f=${pair%%:*}.cs; m=${pair##*:}
  sed -i "s|^\(\s*\)\[Authorize(Roles = .*)\]\s*\$|\1[HasPermission(Permissions.${m}Manage)]|" $f
  sed -i "/^\s*\[HttpGet/{p;s/\[HttpGet.*/[HasPermission(Permissions.${m}View)]/}" $f
  sed -i '1i using DokPortal.Api.Authorization;' $f
done
grep -n "HttpGet\|HttpPost\|HttpPut\|HttpDelete\|HasPermission" CandidatesController.cs ParishNeedsController.cs
```
Sprawdź ręcznie, że każda akcja `[HttpGet]` ma bezpośrednio pod sobą `[HasPermission(Permissions.XView)]`, a `POST`/`PUT`/`DELETE` mają `[HasPermission(Permissions.XManage)]`, oraz że nie został żaden `[Authorize(Roles`.

- [ ] **Step 4: Uruchom cały backend — zielone**

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test 2>&1 | grep -E "error|Powodzenie|niepowodzenie"
```

- [ ] **Step 5: Commit**

```bash
cd C:/eu02_install/DOKPortalLight && git add backend/src/DokPortal.Api/Controllers/CandidatesController.cs backend/src/DokPortal.Api/Controllers/MissionsController.cs backend/src/DokPortal.Api/Controllers/FormatorsController.cs backend/src/DokPortal.Api/Controllers/ParishNeedsController.cs backend/tests/DokPortal.Api.IntegrationTests/PermissionAuthorizationTests.cs
git commit -m "$(cat <<'EOF'
Przestaw Kandydatów, Misje, Formatorów i Potrzeby parafii na uprawnienia View/Manage

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 7: Kontrolery DOK — Sprawy, Dokumenty spraw, Notatki, Spotkania, Superwizje

**Files:**
- Modify: `DokCasesController.cs`, `CaseDocumentsController.cs`, `MeetingsController.cs`, `SupervisionsController.cs`, `PastoralNotesController.cs` (w `backend/src/DokPortal.Api/Controllers/`)
- Test: `backend/tests/DokPortal.Api.IntegrationTests/PermissionAuthorizationTests.cs` (dopisanie)

**Interfaces:**
- Consumes: `[HasPermission]`, `Permissions.*` (Task 5); `IAuthorizationService` (ASP.NET, wstrzykiwany).
- Produces: odczyty wymagają `*View`, zapisy `*Manage`/`PastoralNotes.Write`; `PastoralNotesController.GetAll` ustala `isPrivileged` przez `Permissions.PastoralNotesReadAll`.

- [ ] **Step 1: Testy (czerwone)** — dopisz do `PermissionAuthorizationTests`:

```csharp
    [Theory]
    [InlineData("/api/dok-cases", "DyrektorDOK", HttpStatusCode.OK)]
    [InlineData("/api/dok-cases", "Superwizor", HttpStatusCode.OK)]
    [InlineData("/api/dok-cases", "KatechistaProwadzacy", HttpStatusCode.OK)]
    [InlineData("/api/dok-cases", "Biskup", HttpStatusCode.OK)]
    [InlineData("/api/dok-cases", "DyrektorSKSP", HttpStatusCode.Forbidden)]
    [InlineData("/api/meetings", "DyrektorDOK", HttpStatusCode.OK)]
    [InlineData("/api/meetings", "KatechistaProwadzacy", HttpStatusCode.OK)]
    [InlineData("/api/meetings", "Superwizor", HttpStatusCode.Forbidden)]
    [InlineData("/api/meetings", "Biskup", HttpStatusCode.Forbidden)]
    [InlineData("/api/supervisions", "Superwizor", HttpStatusCode.OK)]
    [InlineData("/api/supervisions", "DyrektorSKSP", HttpStatusCode.OK)]
    [InlineData("/api/supervisions", "KatechistaProwadzacy", HttpStatusCode.Forbidden)]
    [InlineData("/api/supervisions", "Biskup", HttpStatusCode.Forbidden)]
    public async Task DokModules_Reads_RequireViewPermission(string url, string role, HttpStatusCode expected)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

        var response = await client.GetAsync(url);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("KatechistaProwadzacy", HttpStatusCode.OK)]
    [InlineData("DyrektorDOK", HttpStatusCode.OK)]
    [InlineData("Superwizor", HttpStatusCode.Forbidden)]
    [InlineData("Biskup", HttpStatusCode.Forbidden)]
    public async Task PastoralNotes_Read_RequiresViewPermission(string role, HttpStatusCode expected)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

        var response = await client.GetAsync($"/api/dok-cases/{Guid.NewGuid()}/notes");

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("Superwizor", HttpStatusCode.OK)]
    [InlineData("Biskup", HttpStatusCode.OK)]
    [InlineData("KatechistaProwadzacy", HttpStatusCode.OK)]
    [InlineData("DyrektorSKSP", HttpStatusCode.Forbidden)]
    public async Task CaseDocuments_Read_RequiresViewPermission(string role, HttpStatusCode expected)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

        var response = await client.GetAsync($"/api/dok-cases/{Guid.NewGuid()}/documents");

        Assert.Equal(expected, response.StatusCode);
    }
```

- [ ] **Step 2: Uruchom — czerwone**

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test tests/DokPortal.Api.IntegrationTests --filter "FullyQualifiedName~DokModules_Reads|FullyQualifiedName~PastoralNotes_Read|FullyQualifiedName~CaseDocuments_Read" 2>&1 | grep -E "Powodzenie|niepowodzenie" | head -3
```

- [ ] **Step 3: Konwersja prostych kontrolerów**

```bash
cd C:/eu02_install/DOKPortalLight/backend/src/DokPortal.Api/Controllers
for pair in DokCasesController:DokCases CaseDocumentsController:CaseDocuments MeetingsController:Meetings SupervisionsController:Supervisions; do
  f=${pair%%:*}.cs; m=${pair##*:}
  sed -i "s|^\(\s*\)\[Authorize(Roles = .*)\]\s*\$|\1[HasPermission(Permissions.${m}Manage)]|" $f
  sed -i "/^\s*\[HttpGet/{p;s/\[HttpGet.*/[HasPermission(Permissions.${m}View)]/}" $f
  sed -i '1i using DokPortal.Api.Authorization;' $f
done
grep -n "HttpGet\|HttpPost\|HttpPut\|HttpDelete\|HasPermission" CaseDocumentsController.cs
```
Sprawdź, że w `CaseDocumentsController` zarówno `GET` (lista) jak i `GET {id}/download` mają `CaseDocumentsView`, a `POST`, `PUT`, `POST {id}/upload` mają `CaseDocumentsManage`.

- [ ] **Step 4: PastoralNotesController** — edycje:

1. Dodaj `using DokPortal.Api.Authorization;` na górze oraz `using Microsoft.AspNetCore.Authorization;` jeśli go brak.
2. Dodaj pole i parametr konstruktora `IAuthorizationService authorization`:

```csharp
    private readonly IAuthorizationService _authorization;

    public PastoralNotesController(
        IPastoralNoteService pastoralNoteService, IDokCaseService dokCaseService,
        IAuditLogService auditLogService, IAuthorizationService authorization)
    {
        _pastoralNoteService = pastoralNoteService;
        _dokCaseService = dokCaseService;
        _auditLogService = auditLogService;
        _authorization = authorization;
    }
```
3. Nad `GetAll` (pod `[HttpGet]`) dodaj `[HasPermission(Permissions.PastoralNotesView)]`, a w `POST` zamień atrybut ról na `[HasPermission(Permissions.PastoralNotesWrite)]`.
4. W `GetAll` zamień linię z `IsInRole` na:

```csharp
        var isPrivileged = (await _authorization.AuthorizeAsync(User, null, Permissions.PastoralNotesReadAll)).Succeeded;
```

- [ ] **Step 5: Uruchom cały backend — zielone** (w tym `PastoralNotesControllerTests`: autor widzi własną, Dyrektor DOK wszystkie)

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test 2>&1 | grep -E "error|Powodzenie|niepowodzenie"
```

- [ ] **Step 6: Commit**

```bash
cd C:/eu02_install/DOKPortalLight && git add backend/src/DokPortal.Api/Controllers/DokCasesController.cs backend/src/DokPortal.Api/Controllers/CaseDocumentsController.cs backend/src/DokPortal.Api/Controllers/MeetingsController.cs backend/src/DokPortal.Api/Controllers/SupervisionsController.cs backend/src/DokPortal.Api/Controllers/PastoralNotesController.cs backend/tests/DokPortal.Api.IntegrationTests/PermissionAuthorizationTests.cs
git commit -m "$(cat <<'EOF'
Przestaw Sprawy DOK, dokumenty, notatki, Spotkania i Superwizje na uprawnienia

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 8: Dokumenty, Mailing i Budżet (uprawnienia per fundusz)

**Files:**
- Modify: `backend/src/DokPortal.Application/Budget/IBudgetService.cs` (+ `GetFundAsync`)
- Modify: `backend/src/DokPortal.Infrastructure/Services/BudgetService.cs`
- Modify: `DocumentsController.cs`, `MailingController.cs`, `BudgetController.cs` (w `backend/src/DokPortal.Api/Controllers/`)
- Test: `backend/tests/DokPortal.Api.IntegrationTests/PermissionAuthorizationTests.cs` (dopisanie), `backend/tests/DokPortal.Infrastructure.Tests/Services/BudgetServiceTests.cs` (jeśli istnieje — dopisz; w przeciwnym razie utwórz)

**Interfaces:**
- Consumes: `[HasPermission]`, `Permissions.*` (Task 5), `IAuthorizationService`.
- Produces: `IBudgetService.GetFundAsync(Guid id, CancellationToken ct) : Task<BudgetFund?>` (null gdy wpis nie istnieje lub jest usunięty).

- [ ] **Step 1: Testy (czerwone)**

Test serwisu (`BudgetServiceTests.cs`; jeśli plik już istnieje, dopisz metodę do istniejącej klasy):

```csharp
using DokPortal.Application.Budget;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class BudgetServiceFundTests
{
    [Fact]
    public async Task GetFundAsync_ReturnsFundOfExistingEntry_AndNullForMissingOrDeleted()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var service = new BudgetService(db);
        var dokEntry = await service.CreateAsync(new CreateBudgetEntryRequest
        {
            Fund = BudgetFund.DOK, EntryDate = new DateOnly(2026, 9, 18), Description = "Opis",
            Category = "Kat", Type = BudgetEntryType.Expense, Amount = 10m
        }, default);

        Assert.Equal(BudgetFund.DOK, await service.GetFundAsync(dokEntry.Id, default));
        Assert.Null(await service.GetFundAsync(Guid.NewGuid(), default));

        await service.DeleteAsync(dokEntry.Id, "user-1", default);
        Assert.Null(await service.GetFundAsync(dokEntry.Id, default));
    }
}
```

Testy HTTP — dopisz do `PermissionAuthorizationTests` (z `using DokPortal.Application.Budget;`):

```csharp
    [Theory]
    [InlineData("SKSP", "DyrektorSKSP", HttpStatusCode.OK)]
    [InlineData("SKSP", "DyrektorDOK", HttpStatusCode.Forbidden)]
    [InlineData("SKSP", "KatechistaProwadzacy", HttpStatusCode.Forbidden)]
    [InlineData("DOK", "DyrektorDOK", HttpStatusCode.OK)]
    [InlineData("DOK", "DyrektorSKSP", HttpStatusCode.Forbidden)]
    [InlineData("DOK", "KatechistaProwadzacy", HttpStatusCode.Forbidden)]
    public async Task Budget_Read_IsGatedPerFund(string fund, string role, HttpStatusCode expected)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

        var response = await client.GetAsync($"/api/budget?fund={fund}");

        Assert.Equal(expected, response.StatusCode);
    }

    private static object BudgetBody(string fund) => new
    {
        Fund = fund, EntryDate = "2026-09-18", Description = "Opis", Category = "Kategoria", Type = "Expense", Amount = 10m
    };

    [Theory]
    [InlineData("DOK", "DyrektorDOK", HttpStatusCode.Created)]
    [InlineData("DOK", "DyrektorSKSP", HttpStatusCode.Forbidden)]
    [InlineData("SKSP", "DyrektorSKSP", HttpStatusCode.Created)]
    [InlineData("SKSP", "DyrektorDOK", HttpStatusCode.Forbidden)]
    public async Task Budget_Create_IsGatedPerFund(string fund, string role, HttpStatusCode expected)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

        var response = await client.PostAsJsonAsync("/api/budget", BudgetBody(fund));

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Budget_Delete_IsGatedByFundOfTheEntry()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var created = await (await admin.PostAsJsonAsync("/api/budget", BudgetBody("DOK")))
            .Content.ReadFromJsonAsync<BudgetEntryDto>(EnumJsonOptions);
        var sksp = await CreateAuthenticatedClientAsync($"sksp-{Guid.NewGuid():N}@example.org", "Sekret123!", "DyrektorSKSP");
        var dok = await CreateAuthenticatedClientAsync($"dok-{Guid.NewGuid():N}@example.org", "Sekret123!", "DyrektorDOK");

        Assert.Equal(HttpStatusCode.Forbidden, (await sksp.DeleteAsync($"/api/budget/{created!.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await dok.DeleteAsync($"/api/budget/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await dok.DeleteAsync($"/api/budget/{Guid.NewGuid()}")).StatusCode);
    }

    [Theory]
    [InlineData("/api/documents", "DyrektorSKSP", HttpStatusCode.OK)]
    [InlineData("/api/documents", "DyrektorDOK", HttpStatusCode.OK)]
    [InlineData("/api/documents", "KatechistaProwadzacy", HttpStatusCode.Forbidden)]
    [InlineData("/api/mailing/campaigns", "DyrektorSKSP", HttpStatusCode.OK)]
    [InlineData("/api/mailing/campaigns", "DyrektorDOK", HttpStatusCode.OK)]
    [InlineData("/api/mailing/campaigns", "KatechistaProwadzacy", HttpStatusCode.Forbidden)]
    public async Task DocumentsAndMailing_Reads_RequireViewPermission(string url, string role, HttpStatusCode expected)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

        var response = await client.GetAsync(url);

        Assert.Equal(expected, response.StatusCode);
    }
```

- [ ] **Step 2: Uruchom — czerwone (nie kompiluje się: brak `GetFundAsync`)**

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test 2>&1 | grep -E "error" | head -2
```

- [ ] **Step 3: Serwis budżetu** — w `IBudgetService` dodaj `Task<BudgetFund?> GetFundAsync(Guid id, CancellationToken ct);`, w `BudgetService` dodaj:

```csharp
    public async Task<BudgetFund?> GetFundAsync(Guid id, CancellationToken ct)
    {
        var entry = await _db.BudgetEntries.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
        return entry?.Fund;
    }
```

- [ ] **Step 4: Dokumenty i Mailing**

```bash
cd C:/eu02_install/DOKPortalLight/backend/src/DokPortal.Api/Controllers
sed -i 's|^\(\s*\)\[Authorize(Roles = .*)\]\s*$|\1[HasPermission(Permissions.DocumentsGenerate)]|' DocumentsController.cs
sed -i '/^\s*\[HttpGet/{p;s/\[HttpGet.*/[HasPermission(Permissions.DocumentsView)]/}' DocumentsController.cs
sed -i 's|^\(\s*\)\[Authorize(Roles = .*)\]\s*$|\1[HasPermission(Permissions.MailingManage)]|' MailingController.cs
sed -i '/^\s*\[HttpGet/{p;s/\[HttpGet.*/[HasPermission(Permissions.MailingView)]/}' MailingController.cs
for f in DocumentsController.cs MailingController.cs; do sed -i '1i using DokPortal.Api.Authorization;' $f; done
```

- [ ] **Step 5: BudgetController** — zastąp całą zawartością:

```csharp
using System.IdentityModel.Tokens.Jwt;
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
    private readonly IAuthorizationService _authorization;

    public BudgetController(IBudgetService budgetService, IAuthorizationService authorization)
    {
        _budgetService = budgetService;
        _authorization = authorization;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BudgetEntryDto>>> GetEntries([FromQuery] BudgetFund fund, CancellationToken ct)
    {
        if (!await IsAllowedAsync(fund, manage: false)) return Forbid();

        return Ok(await _budgetService.GetEntriesAsync(fund, ct));
    }

    [HttpPost]
    public async Task<ActionResult<BudgetEntryDto>> Create(CreateBudgetEntryRequest request, CancellationToken ct)
    {
        if (!await IsAllowedAsync(request.Fund, manage: true)) return Forbid();

        var created = await _budgetService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetEntries), new { fund = created.Fund }, created);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var fund = await _budgetService.GetFundAsync(id, ct);
        if (fund is null) return NotFound();
        if (!await IsAllowedAsync(fund.Value, manage: true)) return Forbid();

        var currentUserId = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        var deleted = await _budgetService.DeleteAsync(id, currentUserId, ct);
        return deleted ? NoContent() : NotFound();
    }

    private async Task<bool> IsAllowedAsync(BudgetFund fund, bool manage)
    {
        var permission = (fund, manage) switch
        {
            (BudgetFund.SKSP, false) => Permissions.BudgetSkspView,
            (BudgetFund.SKSP, true) => Permissions.BudgetSkspManage,
            (BudgetFund.DOK, false) => Permissions.BudgetDokView,
            (BudgetFund.DOK, true) => Permissions.BudgetDokManage,
            _ => throw new InvalidOperationException("Nieznany fundusz budżetu.")
        };
        return (await _authorization.AuthorizeAsync(User, null, permission)).Succeeded;
    }
}
```

- [ ] **Step 6: Uruchom cały backend — zielone**

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test 2>&1 | grep -E "error|Powodzenie|niepowodzenie"
```

- [ ] **Step 7: Commit**

```bash
cd C:/eu02_install/DOKPortalLight && git add backend/src/DokPortal.Application/Budget/IBudgetService.cs backend/src/DokPortal.Infrastructure/Services/BudgetService.cs backend/src/DokPortal.Api/Controllers/DocumentsController.cs backend/src/DokPortal.Api/Controllers/MailingController.cs backend/src/DokPortal.Api/Controllers/BudgetController.cs backend/tests/DokPortal.Api.IntegrationTests/PermissionAuthorizationTests.cs backend/tests/DokPortal.Infrastructure.Tests/Services
git status --short
git commit -m "$(cat <<'EOF'
Przestaw Dokumenty, Mailing i Budżet (osobno SKŚP i DOK) na uprawnienia

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 9: Natychmiastowość zmian, weryfikacja końcowa, poprawka specyfikacji i push

**Files:**
- Test: `backend/tests/DokPortal.Api.IntegrationTests/PermissionAuthorizationTests.cs` (dopisanie)
- Modify: `docs/superpowers/specs/2026-10-02-permissions-design.md` (liczba uprawnień, nazwy stałych)

**Interfaces:**
- Consumes: `IPermissionService.UpdateRolePermissionsAsync` (Task 3), cały zestaw konwersji (Tasks 5–8).

- [ ] **Step 1: Test natychmiastowości zmiany (czerwony nie jest wymagany — dokumentuje zachowanie)** — dopisz do `PermissionAuthorizationTests` (z `using DokPortal.Application.Permissions; using Microsoft.Extensions.DependencyInjection;`):

```csharp
    [Fact]
    public async Task ChangingRolePermissions_TakesEffectOnNextRequest()
    {
        var client = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/candidates")).StatusCode);
        var original = DefaultRolePermissions.Grants["KatechistaProwadzacy"];

        try
        {
            using (var scope = Factory.Services.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<IPermissionService>();
                await service.UpdateRolePermissionsAsync("KatechistaProwadzacy", original.Append(Permissions.CandidatesView).ToArray(), default);
            }

            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/candidates")).StatusCode);
        }
        finally
        {
            using var scope = Factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPermissionService>();
            await service.UpdateRolePermissionsAsync("KatechistaProwadzacy", original, default);
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/candidates")).StatusCode);
    }

    [Fact]
    public void DefaultGrants_PreserveLegacyWriteAndExportAccess_ExceptIntentionalBudgetDokChange()
    {
        var legacy = new Dictionary<string, string[]>
        {
            ["DyrektorSKSP"] = new[]
            {
                Permissions.PeopleManage, Permissions.PeopleExport,
                Permissions.CandidatesManage, Permissions.CandidatesExport,
                Permissions.MissionsManage, Permissions.MissionsExport,
                Permissions.FormatorsManage, Permissions.FormatorsExport,
                Permissions.ParishNeedsManage, Permissions.BudgetSkspManage,
                Permissions.SupervisionsManage, Permissions.SupervisionsExport,
                Permissions.DocumentsGenerate, Permissions.MailingManage
            },
            ["DyrektorDOK"] = new[]
            {
                Permissions.PeopleManage, Permissions.PeopleExport,
                Permissions.DokCasesManage, Permissions.DokCasesExport,
                Permissions.CaseDocumentsManage, Permissions.PastoralNotesWrite,
                Permissions.MeetingsManage, Permissions.MeetingsExport,
                Permissions.SupervisionsManage, Permissions.SupervisionsExport,
                Permissions.DocumentsGenerate, Permissions.MailingManage
            },
            ["Superwizor"] = new[] { Permissions.SupervisionsManage, Permissions.SupervisionsExport },
            ["KatechistaProwadzacy"] = new[]
            {
                Permissions.CaseDocumentsManage, Permissions.PastoralNotesWrite, Permissions.MeetingsManage
            },
            ["Biskup"] = Array.Empty<string>()
        };

        foreach (var (role, permissions) in legacy)
        {
            Assert.All(permissions, p => Assert.Contains(p, DefaultRolePermissions.Grants[role]));
        }

        Assert.DoesNotContain(Permissions.BudgetDokManage, DefaultRolePermissions.Grants["DyrektorSKSP"]);
        Assert.Contains(Permissions.BudgetDokManage, DefaultRolePermissions.Grants["DyrektorDOK"]);
    }
```
(dodaj `using DokPortal.Domain.Constants;` jeśli brak — już jest w pliku z Task 5.)

- [ ] **Step 2: Uruchom cały backend — wszystko zielone**

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test 2>&1 | grep -E "error|Powodzenie|niepowodzenie"
```

- [ ] **Step 3: Kontrola, że nie zostały role w kontrolerach**

```bash
cd C:/eu02_install/DOKPortalLight/backend/src/DokPortal.Api/Controllers && grep -rn "Authorize(Roles\|IsInRole" . || echo "OK: brak zahardkodowanych ról"
```
Expected: `OK: brak zahardkodowanych ról`.

- [ ] **Step 4: Przegląd odczytów używanych przez frontend** — dla każdej roli sprawdź, że ekrany nie wołają `GET`-ów spoza własnego modułu (wynik analizy z planowania, do potwierdzenia grep-em):

```bash
cd C:/eu02_install/DOKPortalLight/frontend/src/app && grep -rn "Service" features/*/*.component.ts | grep "import" | grep -v "spec\|ToastService\|Notification" | sed 's/:.*import/ import/' | sort | uniq | head -40
```
Oczekiwane zależności (wszystkie pokryte): formularze Kandydatów/Misji/Formatorów/Parafii-giełdy/Dokumentów używają `people` (otwarte) i `parishes` (otwarte); Spotkania używają `dok-cases` (`DokCases.View` mają Dyrektor DOK i Katechista); Absolwenci używają `dok-cases` (Dyrektor DOK ma `View`). Jeśli grep pokaże inną zależność międzymodułową, dopisz właściwe uprawnienie roli w `DefaultRolePermissions` (i test w `DefaultGrants...`) zamiast zostawiać regresję.

- [ ] **Step 5: Poprawka specyfikacji** — w `docs/superpowers/specs/2026-10-02-permissions-design.md`: zamień „Łącznie 43 uprawnienia." na „Łącznie 41 uprawnień." oraz dopisz pod tabelą katalogu zdanie: „W kodzie stałe są płaskie (`Permissions.PeopleManage` = `"People.Manage"`), bo C# nie pozwala na klasę zagnieżdżoną o nazwie otaczającej klasy."

- [ ] **Step 6: Commit i push**

```bash
cd C:/eu02_install/DOKPortalLight && git add backend/tests/DokPortal.Api.IntegrationTests/PermissionAuthorizationTests.cs docs/superpowers/specs/2026-10-02-permissions-design.md docs/superpowers/plans/2026-10-02-permissions-part1-backend.md
git status --short
git commit -m "$(cat <<'EOF'
Zweryfikuj natychmiastowość zmian uprawnień i parytet z dawnymi rolami

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
git push origin master
```

---

## Self-review

- **Pokrycie specyfikacji (część 1):** katalog 41 uprawnień + domyślne przydziały — Task 1; tabela, migracja, seed — Task 2; `IPermissionService` (implicit Administrator, suma ról, cache 60 s, walidacja, macierz) — Task 3; claimy `permission` w JWT — Task 4; handler/provider/atrybut — Task 5; konwersja wszystkich kontrolerów (Osoby, Parafie, Użytkownicy, Audyt, Eksport — T5; SKŚP — T6; DOK + notatki z `ReadAll` — T7; Dokumenty, Mailing, Budżet per fundusz z `GetFundAsync` — T8); testy: serwis, parytet seeda, odczyty zaostrzone, natychmiastowość, JWT — T1–T9. Reminders bez zmian (klucz API).
- **Placeholdery:** brak; polecenia `sed` zawierają pełne wzorce, a po każdym jest weryfikacja przez `grep`.
- **Spójność typów:** `GetPermissionsForRolesAsync(IEnumerable<string>, CancellationToken) : Task<IReadOnlySet<string>>`, `UpdateRolePermissionsAsync(string, IReadOnlyCollection<string>, CancellationToken)`, `GenerateToken(..., roles, permissions)`, `PermissionRequirement.Permission`, `SeedRolePermissionsAsync(AppDbContext, CancellationToken)` — zgodne we wszystkich zadaniach. Nazwy stałych `*View`/`*Manage`/`*Export` zgodne z mapowaniem w Task 6–8 (`${m}View`, `${m}Manage` dla `Candidates|Missions|Formators|ParishNeeds|DokCases|CaseDocuments|Meetings|Supervisions`).
