# Uprawnienia — część 3: ekran „Uprawnienia ról” i własne role — plan implementacji

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Administrator edytuje uprawnienia ról w macierzy oraz tworzy i usuwa własne role; lista ról pochodzi z bazy, nie ze stałej.

**Architecture:** `PermissionService` (czytający `AppDbContext.Roles` i `UserRoles`, bez `RoleManager`) dostaje tworzenie/usuwanie ról i listę ról z bazy; nowy `PermissionsController` wystawia macierz i operacje na rolach z audytem; `UsersController` dostaje `GET roles`. Frontend: serwis, komponent `permissions-matrix` (trasa `/admin/permissions`), a `users-list` pobiera role z API.

**Tech Stack:** ASP.NET Core 8, EF Core (Identity), xUnit; Angular 22 (standalone, signals), Vitest.

**Spec:** `docs/superpowers/specs/2026-10-02-permissions-design.md` (sekcja „Część 3”).

## Global Constraints

- Sześć ról systemowych (`AppRoles.All`) nie jest usuwalnych ani zmienialnych; Administrator jest niezmienny (zawsze wszystkie uprawnienia, brak kolumny w macierzy, brak wierszy w `RolePermissions`).
- Nazwa roli własnej: po `Trim` 3–50 znaków, wzorzec `^[\p{L}\d][\p{L}\d \-]*$`, unikalna po `NormalizedName` (`ToUpperInvariant`) także względem ról systemowych; nowa rola startuje bez uprawnień; brak zmiany nazwy.
- Usunięcie roli: tylko roli własnej i tylko gdy żaden użytkownik jej nie ma; usuwa też jej wiersze w `RolePermissions` i czyści cache.
- Błędy walidacji w serwisie to `InvalidOperationException` (handler zwraca 400), komunikaty po polsku.
- `PermissionsController` pod `[HasPermission(Permissions.PermissionsManage)]`; `GET api/users/roles` pod `Users.Manage`. `Permissions.Manage` nie występuje w domyślnych przydziałach (tylko Administrator).
- Audyt: `UpdateRolePermissions` (opis: rola + dodane/odebrane, max 300 znaków), `CreateRole`, `DeleteRole`.
- Bez migracji bazy (tabele `AspNetRoles`, `AspNetUserRoles`, `RolePermissions` już istnieją).
- Frontend: komunikaty błędów z backendu przez `err?.error?.title ?? 'komunikat domyślny'` (wzorzec z mailingu); usuwanie przez `confirm(...)`.
- **Commity lokalne na `master`, BEZ `git push`** (push tylko po zgodzie użytkownika). Nie commitować `backend/src/DokPortal.Api/appsettings.Development.json` ani `.claude/` (stage’ować tylko wskazane pliki).
- Stopka commita: `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.
- Komendy backendu z `C:\eu02_install\DOKPortalLight\backend`, frontendu z `...\frontend` (`npx ng test --watch=false`).

---

### Task 1: Serwis — role z bazy, tworzenie i usuwanie ról

**Files:**
- Create: `backend/src/DokPortal.Application/Permissions/RoleInfoDto.cs`
- Modify: `backend/src/DokPortal.Application/Permissions/PermissionMatrixDto.cs`, `backend/src/DokPortal.Application/Permissions/IPermissionService.cs`, `backend/src/DokPortal.Infrastructure/Services/PermissionService.cs`
- Test: `backend/tests/DokPortal.Infrastructure.Tests/Services/PermissionServiceTests.cs`

**Interfaces:**
- Produces: `RoleInfoDto { required string Name; required bool IsSystem }`; `PermissionMatrixDto.Roles : IReadOnlyList<RoleInfoDto>`; `IPermissionService.CreateRoleAsync(string name, CancellationToken ct) : Task<RoleInfoDto>`, `DeleteRoleAsync(string role, CancellationToken ct) : Task`, `ListRoleNamesAsync(CancellationToken ct) : Task<IReadOnlyList<string>>` (wszystkie role z bazy, systemowe w kolejności `AppRoles.All`, potem własne alfabetycznie; zawiera Administratora); `GetMatrixAsync`/`UpdateRolePermissionsAsync` walidują i listują role z bazy.

- [ ] **Step 1: Testy (czerwone)** — w `PermissionServiceTests.cs` dodaj `using Microsoft.AspNetCore.Identity;`, zamień helper `Create()` tak, by seedował role systemowe do `db.Roles`:

```csharp
    private static (PermissionService Service, AppDbContext Db) Create()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Roles.AddRange(AppRoles.All.Select(r => new IdentityRole(r) { NormalizedName = r.ToUpperInvariant() }));
        db.SaveChanges();
        return (new PermissionService(db, new MemoryCache(new MemoryCacheOptions())), db);
    }
```

Zastąp istniejący test `GetMatrix_ReturnsCatalogEditableRolesAndGrantsIncludingEmptyOnes` wersją i dopisz nowe testy (na końcu klasy):

```csharp
    [Fact]
    public async Task GetMatrix_ReturnsCatalogEditableRolesAndGrantsIncludingEmptyOnes()
    {
        var (service, db) = Create();
        await Grant(db, AppRoles.DyrektorDOK, Permissions.DokCasesView);

        var matrix = await service.GetMatrixAsync(default);

        Assert.Equal(PermissionCatalog.All.Count, matrix.Permissions.Count);
        Assert.DoesNotContain(matrix.Roles, r => r.Name == AppRoles.Administrator);
        Assert.Equal(AppRoles.All.Length - 1, matrix.Roles.Count);
        Assert.All(matrix.Roles, r => Assert.True(r.IsSystem));
        Assert.Equal(new[] { Permissions.DokCasesView }, matrix.Grants[AppRoles.DyrektorDOK].ToArray());
        Assert.Empty(matrix.Grants[AppRoles.Biskup]);
    }

    [Fact]
    public async Task CreateRole_AddsCustomRoleWithoutPermissions_AndShowsItInMatrixAfterSystemRoles()
    {
        var (service, _) = Create();

        var created = await service.CreateRoleAsync("  Sekretariat  ", default);

        Assert.Equal("Sekretariat", created.Name);
        Assert.False(created.IsSystem);
        var matrix = await service.GetMatrixAsync(default);
        Assert.Equal("Sekretariat", matrix.Roles.Last().Name);
        Assert.False(matrix.Roles.Last().IsSystem);
        Assert.Empty(matrix.Grants["Sekretariat"]);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("   ")]
    [InlineData("Rola!")]
    [InlineData("-Rola")]
    public async Task CreateRole_RejectsInvalidNames(string name)
    {
        var (service, _) = Create();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateRoleAsync(name, default));
    }

    [Fact]
    public async Task CreateRole_RejectsTooLongName()
    {
        var (service, _) = Create();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateRoleAsync(new string('a', 51), default));
    }

    [Theory]
    [InlineData("Sekretariat")]
    [InlineData("SEKRETARIAT")]
    [InlineData("biskup")]
    [InlineData("Administrator")]
    public async Task CreateRole_RejectsDuplicatesIgnoringCase_IncludingSystemRoles(string name)
    {
        var (service, _) = Create();
        await service.CreateRoleAsync("Sekretariat", default);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateRoleAsync(name, default));
    }

    [Fact]
    public async Task UpdateRolePermissions_WorksForCustomRole()
    {
        var (service, _) = Create();
        await service.CreateRoleAsync("Sekretariat", default);

        await service.UpdateRolePermissionsAsync("Sekretariat", new[] { Permissions.CandidatesView }, default);

        var permissions = await service.GetPermissionsForRolesAsync(new[] { "Sekretariat" }, default);
        Assert.Equal(new[] { Permissions.CandidatesView }, permissions.ToArray());
    }

    [Fact]
    public async Task DeleteRole_RemovesCustomRoleAndItsGrants()
    {
        var (service, db) = Create();
        await service.CreateRoleAsync("Sekretariat", default);
        await service.UpdateRolePermissionsAsync("Sekretariat", new[] { Permissions.CandidatesView }, default);

        await service.DeleteRoleAsync("Sekretariat", default);

        Assert.False(await db.Roles.AnyAsync(r => r.Name == "Sekretariat"));
        Assert.False(await db.RolePermissions.AnyAsync(r => r.RoleName == "Sekretariat"));
        Assert.Empty(await service.GetPermissionsForRolesAsync(new[] { "Sekretariat" }, default));
        var matrix = await service.GetMatrixAsync(default);
        Assert.DoesNotContain(matrix.Roles, r => r.Name == "Sekretariat");
    }

    [Theory]
    [InlineData("Biskup")]
    [InlineData("Administrator")]
    [InlineData("NieistniejacaRola")]
    public async Task DeleteRole_RejectsSystemAndUnknownRoles(string role)
    {
        var (service, _) = Create();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteRoleAsync(role, default));
    }

    [Fact]
    public async Task DeleteRole_RejectsRoleAssignedToUsers()
    {
        var (service, db) = Create();
        var created = await service.CreateRoleAsync("Sekretariat", default);
        var roleId = (await db.Roles.SingleAsync(r => r.Name == created.Name)).Id;
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = "user-1", RoleId = roleId });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteRoleAsync("Sekretariat", default));

        Assert.Contains("1", ex.Message);
        Assert.True(await db.Roles.AnyAsync(r => r.Name == "Sekretariat"));
    }

    [Fact]
    public async Task ListRoleNames_ReturnsSystemRolesInDeclaredOrderThenCustomAlphabetically()
    {
        var (service, _) = Create();
        await service.CreateRoleAsync("Zespół", default);
        await service.CreateRoleAsync("Archiwum", default);

        var names = await service.ListRoleNamesAsync(default);

        Assert.Equal(AppRoles.All.Concat(new[] { "Archiwum", "Zespół" }), names);
    }
```

- [ ] **Step 2: Uruchom — czerwone (nie kompiluje się: brak `CreateRoleAsync`, `RoleInfoDto`)**

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test tests/DokPortal.Infrastructure.Tests --filter "FullyQualifiedName~PermissionServiceTests" 2>&1 | grep -E "error" | head -3
```

- [ ] **Step 3: Implementacja**

`RoleInfoDto.cs`:

```csharp
namespace DokPortal.Application.Permissions;

public class RoleInfoDto
{
    public required string Name { get; init; }
    public required bool IsSystem { get; init; }
}
```

W `PermissionMatrixDto.cs` zmień typ `Roles` na `required IReadOnlyList<RoleInfoDto> Roles { get; init; }`.

W `IPermissionService.cs` dopisz:

```csharp
    Task<RoleInfoDto> CreateRoleAsync(string name, CancellationToken ct);
    Task DeleteRoleAsync(string role, CancellationToken ct);
    Task<IReadOnlyList<string>> ListRoleNamesAsync(CancellationToken ct);
```

W `PermissionService.cs` dodaj `using System.Text.RegularExpressions;` i `using Microsoft.AspNetCore.Identity;`, stałą i metody; zastąp `GetMatrixAsync` i walidację ról w `UpdateRolePermissionsAsync`:

```csharp
    private static readonly Regex RoleNamePattern = new(@"^[\p{L}\d][\p{L}\d \-]*$", RegexOptions.Compiled);

    private static IReadOnlyList<string> OrderRoles(IEnumerable<string> names)
    {
        var list = names.ToList();
        return list.Where(n => AppRoles.All.Contains(n)).OrderBy(n => Array.IndexOf(AppRoles.All, n))
            .Concat(list.Where(n => !AppRoles.All.Contains(n)).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase))
            .ToList();
    }

    public async Task<IReadOnlyList<string>> ListRoleNamesAsync(CancellationToken ct)
    {
        var names = await _db.Roles.AsNoTracking().Select(r => r.Name!).ToListAsync(ct);
        return OrderRoles(names);
    }

    public async Task<PermissionMatrixDto> GetMatrixAsync(CancellationToken ct)
    {
        var map = await GetMapAsync(ct);
        var names = await ListRoleNamesAsync(ct);
        var editable = names.Where(n => n != AppRoles.Administrator).ToList();

        return new PermissionMatrixDto
        {
            Roles = editable.Select(n => new RoleInfoDto { Name = n, IsSystem = AppRoles.All.Contains(n) }).ToList(),
            Permissions = PermissionCatalog.All,
            Grants = editable.ToDictionary(
                role => role,
                role => (IReadOnlyList<string>)(map.TryGetValue(role, out var permissions)
                    ? permissions.OrderBy(p => p).ToList()
                    : new List<string>()))
        };
    }

    public async Task<RoleInfoDto> CreateRoleAsync(string name, CancellationToken ct)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length is < 3 or > 50)
        {
            throw new InvalidOperationException("Nazwa roli musi mieć od 3 do 50 znaków.");
        }
        if (!RoleNamePattern.IsMatch(trimmed))
        {
            throw new InvalidOperationException("Nazwa roli może zawierać tylko litery, cyfry, spacje i myślniki.");
        }

        var normalized = trimmed.ToUpperInvariant();
        if (await _db.Roles.AnyAsync(r => r.NormalizedName == normalized, ct))
        {
            throw new InvalidOperationException($"Rola „{trimmed}” już istnieje.");
        }

        _db.Roles.Add(new IdentityRole(trimmed) { NormalizedName = normalized });
        await _db.SaveChangesAsync(ct);
        return new RoleInfoDto { Name = trimmed, IsSystem = false };
    }

    public async Task DeleteRoleAsync(string role, CancellationToken ct)
    {
        if (AppRoles.All.Contains(role))
        {
            throw new InvalidOperationException("Roli systemowej nie można usunąć.");
        }

        var normalized = role.ToUpperInvariant();
        var entity = await _db.Roles.FirstOrDefaultAsync(r => r.NormalizedName == normalized, ct)
            ?? throw new InvalidOperationException($"Nieznana rola: {role}.");

        var assigned = await _db.UserRoles.CountAsync(ur => ur.RoleId == entity.Id, ct);
        if (assigned > 0)
        {
            throw new InvalidOperationException($"Roli „{entity.Name}” nie można usunąć — ma ją przypisaną liczba użytkowników: {assigned}.");
        }

        _db.RolePermissions.RemoveRange(await _db.RolePermissions.Where(r => r.RoleName == entity.Name).ToListAsync(ct));
        _db.Roles.Remove(entity);
        await _db.SaveChangesAsync(ct);

        _cache.Remove(CacheKey);
    }
```

W `UpdateRolePermissionsAsync` zamień walidację `if (!AppRoles.All.Contains(role)) throw ...` oraz późniejsze użycie `role` tak, by rola była szukana w bazie i używała kanonicznej nazwy:

```csharp
        var normalizedRole = role.ToUpperInvariant();
        var roleEntity = await _db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.NormalizedName == normalizedRole, ct)
            ?? throw new InvalidOperationException($"Nieznana rola: {role}.");
        var roleName = roleEntity.Name!;
```
(po sprawdzeniu `role == AppRoles.Administrator`, przed walidacją uprawnień) i w dalszej części metody w miejsce `role` używaj `roleName` (`Where(r => r.RoleName == roleName)` oraz `new RolePermission { RoleName = roleName, ... }`). Sprawdzenie Administratora zostaje porównaniem `role == AppRoles.Administrator` (bez zmian).

- [ ] **Step 4: Uruchom — zielone**

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test 2>&1 | grep -E "error|Powodzenie|niepowodzenie"
```
Expected: brak niepowodzeń (istniejące `UpdateRolePermissions_*` i integracyjne `PermissionAuthorizationTests` nadal przechodzą, bo role systemowe są w bazie testowej).

- [ ] **Step 5: Commit (bez push)**

```bash
cd C:/eu02_install/DOKPortalLight && git add backend/src/DokPortal.Application/Permissions backend/src/DokPortal.Infrastructure/Services/PermissionService.cs backend/tests/DokPortal.Infrastructure.Tests/Services/PermissionServiceTests.cs
git commit -m "$(cat <<'EOF'
PermissionService: role z bazy, tworzenie i usuwanie własnych ról

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: PermissionsController i `GET api/users/roles` (z audytem)

**Files:**
- Create: `backend/src/DokPortal.Application/Permissions/CreateRoleRequest.cs`, `backend/src/DokPortal.Application/Permissions/UpdateRolePermissionsRequest.cs`, `backend/src/DokPortal.Api/Controllers/PermissionsController.cs`
- Modify: `backend/src/DokPortal.Api/Controllers/UsersController.cs`
- Test: `backend/tests/DokPortal.Api.IntegrationTests/PermissionsControllerTests.cs`

**Interfaces:**
- Consumes: `IPermissionService` (Task 1: `GetMatrixAsync`, `UpdateRolePermissionsAsync`, `CreateRoleAsync`, `DeleteRoleAsync`, `ListRoleNamesAsync`), `IAuditLogService.LogAsync(userId, email, action, objectDescription, AuditResult, ct)`.
- Produces: `GET api/permissions/matrix → PermissionMatrixDto`; `PUT api/permissions/roles/{role}` body `{ "permissions": [...] }` → 204; `POST api/permissions/roles` body `{ "name": "..." }` → 201 `RoleInfoDto`; `DELETE api/permissions/roles/{role}` → 204; `GET api/users/roles → string[]`.

- [ ] **Step 1: Testy (czerwone)** — `PermissionsControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Permissions;
using DokPortal.Domain.Constants;
using DokPortal.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class PermissionsControllerTests : IntegrationTestBase
{
    public PermissionsControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private static string NewRoleName() => $"Rola {Guid.NewGuid():N}";

    private Task<HttpClient> AdminAsync() =>
        CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

    private static string RoleUrl(string role) => $"/api/permissions/roles/{Uri.EscapeDataString(role)}";

    private bool HasAudit(string action, string startsWith)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db.AuditLogEntries.Any(e => e.Action == action && e.ObjectDescription.StartsWith(startsWith));
    }

    [Fact]
    public async Task Matrix_ForAdministrator_ReturnsCatalogAndSystemRolesWithoutAdministrator()
    {
        var admin = await AdminAsync();

        var response = await admin.GetAsync("/api/permissions/matrix");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var matrix = await response.Content.ReadFromJsonAsync<PermissionMatrixDto>();
        Assert.Equal(PermissionCatalog.All.Count, matrix!.Permissions.Count);
        Assert.DoesNotContain(matrix.Roles, r => r.Name == "Administrator");
        Assert.Contains(matrix.Roles, r => r.Name == "Biskup" && r.IsSystem);
        Assert.Contains(Permissions.DokCasesManage, matrix.Grants["DyrektorDOK"]);
    }

    [Theory]
    [InlineData("DyrektorDOK")]
    [InlineData("DyrektorSKSP")]
    [InlineData("KatechistaProwadzacy")]
    public async Task AllEndpoints_AreForbidden_ForNonAdministrators(string role)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/permissions/matrix")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/permissions/roles", new { Name = "Test" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync(RoleUrl("Biskup"), new { Permissions = Array.Empty<string>() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync(RoleUrl("Biskup"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users/roles")).StatusCode);
    }

    [Fact]
    public async Task Matrix_ReturnsUnauthorized_WithoutToken()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/permissions/matrix")).StatusCode);
    }

    [Fact]
    public async Task CreateRole_ReturnsCreated_ShowsInMatrixAndUsersRoles_AndIsAudited()
    {
        var admin = await AdminAsync();
        var name = NewRoleName();

        var response = await admin.PostAsJsonAsync("/api/permissions/roles", new { Name = name });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<RoleInfoDto>();
        Assert.Equal(name, created!.Name);
        Assert.False(created.IsSystem);
        var matrix = await (await admin.GetAsync("/api/permissions/matrix")).Content.ReadFromJsonAsync<PermissionMatrixDto>();
        Assert.Contains(matrix!.Roles, r => r.Name == name && !r.IsSystem);
        Assert.Empty(matrix.Grants[name]);
        var roles = await (await admin.GetAsync("/api/users/roles")).Content.ReadFromJsonAsync<List<string>>();
        Assert.Contains(name, roles!);
        Assert.Contains("Administrator", roles!);
        Assert.True(HasAudit("CreateRole", name));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("Rola!")]
    [InlineData("Biskup")]
    public async Task CreateRole_ReturnsBadRequest_ForInvalidOrDuplicateName(string name)
    {
        var admin = await AdminAsync();

        var response = await admin.PostAsJsonAsync("/api/permissions/roles", new { Name = name });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRole_SavesGrants_ReturnsBadRequestForUnknownPermission_AndIsAudited()
    {
        var admin = await AdminAsync();
        var name = NewRoleName();
        await admin.PostAsJsonAsync("/api/permissions/roles", new { Name = name });

        var ok = await admin.PutAsJsonAsync(RoleUrl(name), new { Permissions = new[] { Permissions.CandidatesView, Permissions.MeetingsView } });
        var bad = await admin.PutAsJsonAsync(RoleUrl(name), new { Permissions = new[] { "People.Fly" } });

        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var matrix = await (await admin.GetAsync("/api/permissions/matrix")).Content.ReadFromJsonAsync<PermissionMatrixDto>();
        Assert.Equal(new[] { Permissions.CandidatesView, Permissions.MeetingsView }, matrix!.Grants[name].OrderBy(p => p).ToArray());
        Assert.True(HasAudit("UpdateRolePermissions", name));
    }

    [Fact]
    public async Task UpdateRole_ReturnsBadRequest_ForAdministratorAndUnknownRole()
    {
        var admin = await AdminAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(RoleUrl("Administrator"), new { Permissions = Array.Empty<string>() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(RoleUrl("NieistniejacaRola"), new { Permissions = Array.Empty<string>() })).StatusCode);
    }

    [Fact]
    public async Task DeleteRole_RemovesCustomRole_RejectsSystemRoleAndRoleWithUsers_AndIsAudited()
    {
        var admin = await AdminAsync();
        var removable = NewRoleName();
        var inUse = NewRoleName();
        await admin.PostAsJsonAsync("/api/permissions/roles", new { Name = removable });
        await admin.PostAsJsonAsync("/api/permissions/roles", new { Name = inUse });
        await CreateUserAndGetTokenAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", inUse);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync(RoleUrl(removable))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync(RoleUrl("Biskup"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync(RoleUrl(inUse))).StatusCode);

        var matrix = await (await admin.GetAsync("/api/permissions/matrix")).Content.ReadFromJsonAsync<PermissionMatrixDto>();
        Assert.DoesNotContain(matrix!.Roles, r => r.Name == removable);
        Assert.Contains(matrix.Roles, r => r.Name == inUse);
        Assert.True(HasAudit("DeleteRole", removable));
    }

    [Fact]
    public async Task CustomRole_GainsAccessOnNextRequest_AfterGrantingPermission()
    {
        var admin = await AdminAsync();
        var name = NewRoleName();
        await admin.PostAsJsonAsync("/api/permissions/roles", new { Name = name });
        var user = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", name);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/candidates")).StatusCode);

        await admin.PutAsJsonAsync(RoleUrl(name), new { Permissions = new[] { Permissions.CandidatesView } });

        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/candidates")).StatusCode);
    }
}
```

- [ ] **Step 2: Uruchom — czerwone (404 na endpointach)**

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test tests/DokPortal.Api.IntegrationTests --filter "FullyQualifiedName~PermissionsControllerTests" 2>&1 | grep -E "error|Powodzenie!|Niepowodzenie!" | head -3
```

- [ ] **Step 3: Implementacja**

`CreateRoleRequest.cs`:

```csharp
namespace DokPortal.Application.Permissions;

public class CreateRoleRequest
{
    public required string Name { get; init; }
}
```

`UpdateRolePermissionsRequest.cs`:

```csharp
namespace DokPortal.Application.Permissions;

public class UpdateRolePermissionsRequest
{
    public required IReadOnlyList<string> Permissions { get; init; }
}
```

`PermissionsController.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using DokPortal.Api.Authorization;
using DokPortal.Application.AuditLog;
using DokPortal.Application.Permissions;
using DokPortal.Domain.Constants;
using DokPortal.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/permissions")]
[HasPermission(Permissions.PermissionsManage)]
public class PermissionsController : ControllerBase
{
    private const int AuditDescriptionMaxLength = 300;

    private readonly IPermissionService _permissionService;
    private readonly IAuditLogService _auditLogService;

    public PermissionsController(IPermissionService permissionService, IAuditLogService auditLogService)
    {
        _permissionService = permissionService;
        _auditLogService = auditLogService;
    }

    [HttpGet("matrix")]
    public async Task<ActionResult<PermissionMatrixDto>> GetMatrix(CancellationToken ct)
        => Ok(await _permissionService.GetMatrixAsync(ct));

    [HttpPut("roles/{role}")]
    public async Task<IActionResult> UpdateRole(string role, UpdateRolePermissionsRequest request, CancellationToken ct)
    {
        var matrix = await _permissionService.GetMatrixAsync(ct);
        var before = matrix.Grants.TryGetValue(role, out var current) ? current : Array.Empty<string>();

        await _permissionService.UpdateRolePermissionsAsync(role, request.Permissions, ct);

        var added = request.Permissions.Except(before).OrderBy(p => p);
        var removed = before.Except(request.Permissions).OrderBy(p => p);
        await AuditAsync("UpdateRolePermissions",
            $"{role}: dodano [{string.Join(", ", added)}]; odebrano [{string.Join(", ", removed)}]", ct);
        return NoContent();
    }

    [HttpPost("roles")]
    public async Task<ActionResult<RoleInfoDto>> CreateRole(CreateRoleRequest request, CancellationToken ct)
    {
        var created = await _permissionService.CreateRoleAsync(request.Name, ct);
        await AuditAsync("CreateRole", created.Name, ct);
        return Created($"/api/permissions/roles/{Uri.EscapeDataString(created.Name)}", created);
    }

    [HttpDelete("roles/{role}")]
    public async Task<IActionResult> DeleteRole(string role, CancellationToken ct)
    {
        await _permissionService.DeleteRoleAsync(role, ct);
        await AuditAsync("DeleteRole", role, ct);
        return NoContent();
    }

    private Task AuditAsync(string action, string description, CancellationToken ct)
    {
        var truncated = description.Length > AuditDescriptionMaxLength ? description[..AuditDescriptionMaxLength] : description;
        return _auditLogService.LogAsync(
            User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value,
            User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value,
            action, truncated, AuditResult.Allowed, ct);
    }
}
```

W `UsersController.cs` dodaj `using DokPortal.Application.Permissions;`, pole i parametr konstruktora:

```csharp
    private readonly IPermissionService _permissionService;

    public UsersController(IUserService userService, IAuditLogService auditLogService, IPermissionService permissionService)
    {
        _userService = userService;
        _auditLogService = auditLogService;
        _permissionService = permissionService;
    }
```
(zastępując dotychczasowy konstruktor) oraz akcję (obok `List`):

```csharp
    [HttpGet("roles")]
    public async Task<ActionResult<IReadOnlyList<string>>> ListRoles(CancellationToken ct)
        => Ok(await _permissionService.ListRoleNamesAsync(ct));
```

- [ ] **Step 4: Uruchom cały backend — zielone**

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test 2>&1 | grep -E "error|Powodzenie|niepowodzenie"
```

- [ ] **Step 5: Commit (bez push)**

```bash
cd C:/eu02_install/DOKPortalLight && git add backend/src/DokPortal.Application/Permissions backend/src/DokPortal.Api/Controllers/PermissionsController.cs backend/src/DokPortal.Api/Controllers/UsersController.cs backend/tests/DokPortal.Api.IntegrationTests/PermissionsControllerTests.cs
git commit -m "$(cat <<'EOF'
Dodaj PermissionsController (macierz, własne role) i GET api/users/roles

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Frontend — model i `PermissionsService`

**Files:**
- Create: `frontend/src/app/features/admin-permissions/permissions.model.ts`, `frontend/src/app/features/admin-permissions/permissions.service.ts`
- Test: `frontend/src/app/features/admin-permissions/permissions.service.spec.ts`

**Interfaces:**
- Produces: `RoleInfo { name; isSystem }`, `PermissionInfo { name; module; label }`, `PermissionMatrix { roles: RoleInfo[]; permissions: PermissionInfo[]; grants: Record<string, string[]> }`; `PermissionsService.getMatrix(): Observable<PermissionMatrix>`, `updateRole(role: string, permissions: string[]): Observable<void>`, `createRole(name: string): Observable<RoleInfo>`, `deleteRole(role: string): Observable<void>`.

- [ ] **Step 1: Test (czerwony)** — `permissions.service.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { PermissionsService } from './permissions.service';
import { environment } from '../../../environments/environment';

describe('PermissionsService', () => {
  let service: PermissionsService;
  let httpMock: HttpTestingController;
  const base = `${environment.apiBaseUrl}/api/permissions`;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(PermissionsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('loads the matrix', () => {
    service.getMatrix().subscribe();

    httpMock.expectOne(`${base}/matrix`).flush({ roles: [], permissions: [], grants: {} });
  });

  it('saves role permissions with the role name url-encoded', () => {
    service.updateRole('Rola testowa', ['People.Manage']).subscribe();

    const req = httpMock.expectOne(`${base}/roles/Rola%20testowa`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ permissions: ['People.Manage'] });
    req.flush(null);
  });

  it('creates and deletes roles', () => {
    service.createRole('Sekretariat').subscribe();
    const create = httpMock.expectOne(`${base}/roles`);
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toEqual({ name: 'Sekretariat' });
    create.flush({ name: 'Sekretariat', isSystem: false });

    service.deleteRole('Sekretariat').subscribe();
    const del = httpMock.expectOne(`${base}/roles/Sekretariat`);
    expect(del.request.method).toBe('DELETE');
    del.flush(null);
  });
});
```

- [ ] **Step 2: Uruchom — czerwone** (`Could not resolve "./permissions.service"`).

```bash
cd C:/eu02_install/DOKPortalLight/frontend && npx ng test --watch=false 2>&1 | grep -E "ERROR|Could not resolve|Test Files" | head -3
```

- [ ] **Step 3: Implementacja**

`permissions.model.ts`:

```ts
export interface RoleInfo {
  name: string;
  isSystem: boolean;
}

export interface PermissionInfo {
  name: string;
  module: string;
  label: string;
}

export interface PermissionMatrix {
  roles: RoleInfo[];
  permissions: PermissionInfo[];
  grants: Record<string, string[]>;
}
```

`permissions.service.ts`:

```ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { PermissionMatrix, RoleInfo } from './permissions.model';

@Injectable({ providedIn: 'root' })
export class PermissionsService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/permissions`;

  constructor(private readonly http: HttpClient) {}

  getMatrix() {
    return this.http.get<PermissionMatrix>(`${this.baseUrl}/matrix`);
  }

  updateRole(role: string, permissions: string[]) {
    return this.http.put<void>(`${this.baseUrl}/roles/${encodeURIComponent(role)}`, { permissions });
  }

  createRole(name: string) {
    return this.http.post<RoleInfo>(`${this.baseUrl}/roles`, { name });
  }

  deleteRole(role: string) {
    return this.http.delete<void>(`${this.baseUrl}/roles/${encodeURIComponent(role)}`);
  }
}
```

- [ ] **Step 4: Uruchom — zielone**

```bash
cd C:/eu02_install/DOKPortalLight/frontend && npx ng test --watch=false 2>&1 | tail -5
```

- [ ] **Step 5: Commit (bez push)**

```bash
cd C:/eu02_install/DOKPortalLight && git add frontend/src/app/features/admin-permissions
git commit -m "$(cat <<'EOF'
Frontend: model i PermissionsService dla macierzy uprawnień

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: Komponent `permissions-matrix`, trasa i pozycja menu

**Files:**
- Create: `frontend/src/app/features/admin-permissions/permissions-matrix.component.ts`, `.html`, `.scss`
- Modify: `frontend/src/app/app.routes.ts`, `frontend/src/app/layout/nav-items.ts`
- Test: `frontend/src/app/features/admin-permissions/permissions-matrix.component.spec.ts` (oraz istniejący `app.routes.spec.ts` obejmuje nową trasę)

**Interfaces:**
- Consumes: `PermissionsService` (Task 3), `ToastService.success/error`, `Permissions.PermissionsManage`, `permissionGuard`.
- Produces: `PermissionsMatrixComponent` (selektor `app-permissions-matrix`), trasa `admin/permissions`, pozycja menu „Uprawnienia ról”.

- [ ] **Step 1: Test (czerwony)** — `permissions-matrix.component.spec.ts`:

```ts
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { PermissionsMatrixComponent } from './permissions-matrix.component';
import { environment } from '../../../environments/environment';

const base = `${environment.apiBaseUrl}/api/permissions`;

const matrix = {
  roles: [
    { name: 'Biskup', isSystem: true },
    { name: 'Sekretariat', isSystem: false }
  ],
  permissions: [
    { name: 'People.Manage', module: 'Osoby', label: 'Dodawanie, edycja i usuwanie osób' },
    { name: 'People.Export', module: 'Osoby', label: 'Eksport osób do Excela' },
    { name: 'Meetings.View', module: 'Spotkania', label: 'Podgląd harmonogramu i obecności' }
  ],
  grants: { Biskup: ['Meetings.View'], Sekretariat: [] }
};

describe('PermissionsMatrixComponent', () => {
  let fixture: ComponentFixture<PermissionsMatrixComponent>;
  let httpMock: HttpTestingController;

  function render() {
    fixture.detectChanges();
    httpMock.expectOne(`${base}/matrix`).flush(matrix);
    fixture.detectChanges();
  }

  function checkbox(role: string, permission: string): HTMLInputElement {
    return fixture.nativeElement.querySelector(`input[data-role="${role}"][data-permission="${permission}"]`);
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [PermissionsMatrixComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(PermissionsMatrixComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('renders module headers, role columns and current grants', () => {
    render();
    const text = fixture.nativeElement.textContent as string;

    expect(text).toContain('Osoby');
    expect(text).toContain('Spotkania');
    expect(text).toContain('Biskup');
    expect(text).toContain('Sekretariat');
    expect(checkbox('Biskup', 'Meetings.View').checked).toBe(true);
    expect(checkbox('Sekretariat', 'Meetings.View').checked).toBe(false);
  });

  it('offers deletion only for custom roles', () => {
    render();

    expect(fixture.nativeElement.querySelector('[data-delete-role="Sekretariat"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[data-delete-role="Biskup"]')).toBeNull();
  });

  it('enables saving after a change and sends only the changed role', () => {
    render();
    const save = () => fixture.nativeElement.querySelector('button[data-save]') as HTMLButtonElement;
    expect(save().disabled).toBe(true);

    const box = checkbox('Sekretariat', 'People.Manage');
    box.checked = true;
    box.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(save().disabled).toBe(false);

    save().click();
    const req = httpMock.expectOne(`${base}/roles/Sekretariat`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ permissions: ['People.Manage'] });
    req.flush(null);
    httpMock.expectOne(`${base}/matrix`).flush({ ...matrix, grants: { Biskup: ['Meetings.View'], Sekretariat: ['People.Manage'] } });
    httpMock.expectNone(`${base}/roles/Biskup`);
  });

  it('reverts unsaved changes', () => {
    render();
    const box = checkbox('Biskup', 'People.Export');
    box.checked = true;
    box.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('button[data-discard]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(checkbox('Biskup', 'People.Export').checked).toBe(false);
    expect((fixture.nativeElement.querySelector('button[data-save]') as HTMLButtonElement).disabled).toBe(true);
  });

  it('creates a role from the name field and reloads the matrix', () => {
    render();
    const input = fixture.nativeElement.querySelector('input[data-new-role]') as HTMLInputElement;
    input.value = 'Archiwum';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('button[data-create-role]') as HTMLButtonElement).click();

    const req = httpMock.expectOne(`${base}/roles`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'Archiwum' });
    req.flush({ name: 'Archiwum', isSystem: false });
    httpMock.expectOne(`${base}/matrix`).flush(matrix);
  });

  it('deletes a custom role after confirmation', () => {
    render();
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    (fixture.nativeElement.querySelector('[data-delete-role="Sekretariat"]') as HTMLElement).click();

    const req = httpMock.expectOne(`${base}/roles/Sekretariat`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
    httpMock.expectOne(`${base}/matrix`).flush(matrix);
  });
});
```

- [ ] **Step 2: Uruchom — czerwone** (`Could not resolve "./permissions-matrix.component"`).

```bash
cd C:/eu02_install/DOKPortalLight/frontend && npx ng test --watch=false 2>&1 | grep -E "ERROR|Could not resolve|Test Files" | head -3
```

- [ ] **Step 3: Implementacja**

`permissions-matrix.component.ts`:

```ts
import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { ToastService } from '../../core/notifications/toast.service';
import { PermissionInfo, PermissionMatrix } from './permissions.model';
import { PermissionsService } from './permissions.service';

interface ModuleGroup {
  module: string;
  permissions: PermissionInfo[];
}

function sameSet(a: string[], b: string[]): boolean {
  return a.length === b.length && a.every(x => b.includes(x));
}

function cloneGrants(matrix: PermissionMatrix): Record<string, string[]> {
  const copy: Record<string, string[]> = {};
  for (const role of matrix.roles) {
    copy[role.name] = [...(matrix.grants[role.name] ?? [])];
  }
  return copy;
}

@Component({
  selector: 'app-permissions-matrix',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './permissions-matrix.component.html',
  styleUrl: './permissions-matrix.component.scss'
})
export class PermissionsMatrixComponent implements OnInit {
  readonly matrix = signal<PermissionMatrix | null>(null);
  readonly draft = signal<Record<string, string[]>>({});
  readonly saving = signal(false);
  newRoleName = '';

  readonly modules = computed<ModuleGroup[]>(() => {
    const groups: ModuleGroup[] = [];
    for (const permission of this.matrix()?.permissions ?? []) {
      let group = groups.find(g => g.module === permission.module);
      if (!group) {
        group = { module: permission.module, permissions: [] };
        groups.push(group);
      }
      group.permissions.push(permission);
    }
    return groups;
  });

  readonly dirtyRoles = computed(() => {
    const matrix = this.matrix();
    if (!matrix) return [];
    const draft = this.draft();
    return matrix.roles.map(r => r.name).filter(name => !sameSet(matrix.grants[name] ?? [], draft[name] ?? []));
  });

  readonly hasChanges = computed(() => this.dirtyRoles().length > 0);

  constructor(
    private readonly permissionsService: PermissionsService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.permissionsService.getMatrix().subscribe({
      next: matrix => {
        this.matrix.set(matrix);
        this.draft.set(cloneGrants(matrix));
      },
      error: () => this.toast.error('Nie udało się wczytać uprawnień.')
    });
  }

  isGranted(role: string, permission: string): boolean {
    return (this.draft()[role] ?? []).includes(permission);
  }

  toggle(role: string, permission: string, checked: boolean): void {
    this.draft.update(draft => {
      const current = draft[role] ?? [];
      const next = checked ? [...new Set([...current, permission])] : current.filter(p => p !== permission);
      return { ...draft, [role]: next };
    });
  }

  discard(): void {
    const matrix = this.matrix();
    if (matrix) {
      this.draft.set(cloneGrants(matrix));
    }
  }

  save(): void {
    const dirty = this.dirtyRoles();
    if (dirty.length === 0) return;
    this.saving.set(true);
    forkJoin(dirty.map(role => this.permissionsService.updateRole(role, this.draft()[role]))).subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.success(`Zapisano uprawnienia: ${dirty.join(', ')}.`);
        this.load();
      },
      error: err => {
        this.saving.set(false);
        this.toast.error(err?.error?.title ?? 'Nie udało się zapisać uprawnień.');
        this.load();
      }
    });
  }

  createRole(): void {
    const name = this.newRoleName.trim();
    if (!name) return;
    this.permissionsService.createRole(name).subscribe({
      next: () => {
        this.newRoleName = '';
        this.toast.success(`Dodano rolę „${name}”.`);
        this.load();
      },
      error: err => this.toast.error(err?.error?.title ?? 'Nie udało się dodać roli.')
    });
  }

  deleteRole(role: string): void {
    if (!confirm(`Usunąć rolę „${role}”?`)) return;
    this.permissionsService.deleteRole(role).subscribe({
      next: () => {
        this.toast.success(`Usunięto rolę „${role}”.`);
        this.load();
      },
      error: err => this.toast.error(err?.error?.title ?? 'Nie udało się usunąć roli.')
    });
  }
}
```

`permissions-matrix.component.html`:

```html
<div class="page-heading">
  <div><h2>Uprawnienia ról</h2><p>Co może każda rola. Administrator ma zawsze wszystkie uprawnienia.</p></div>
  <div style="display:flex;gap:8px;align-items:center">
    <button class="btn ghost" data-discard [disabled]="!hasChanges()" (click)="discard()">Cofnij zmiany</button>
    <button class="btn primary" data-save [disabled]="!hasChanges() || saving()" (click)="save()">Zapisz zmiany</button>
  </div>
</div>

<div class="card" style="margin-bottom:16px">
  <div class="card-body" style="display:flex;gap:8px;align-items:flex-end;flex-wrap:wrap">
    <div class="field" style="margin:0">
      <label>Nowa rola</label>
      <input data-new-role [(ngModel)]="newRoleName" name="newRoleName" placeholder="np. Sekretariat" maxlength="50" />
    </div>
    <button class="btn primary" data-create-role [disabled]="!newRoleName.trim()" (click)="createRole()">＋ Dodaj rolę</button>
  </div>
</div>

@if (matrix(); as m) {
  <div class="card">
    <div class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>Uprawnienie</th>
            @for (role of m.roles; track role.name) {
              <th>
                {{ role.name }}
                @if (!role.isSystem) {
                  <span class="link" style="color:var(--danger)" [attr.data-delete-role]="role.name" (click)="deleteRole(role.name)">Usuń</span>
                }
              </th>
            }
          </tr>
        </thead>
        <tbody>
          @for (group of modules(); track group.module) {
            <tr class="module-row"><td [attr.colspan]="m.roles.length + 1"><b>{{ group.module }}</b></td></tr>
            @for (permission of group.permissions; track permission.name) {
              <tr>
                <td>{{ permission.label }}<br /><span class="small-muted">{{ permission.name }}</span></td>
                @for (role of m.roles; track role.name) {
                  <td>
                    <input type="checkbox"
                           [attr.data-role]="role.name"
                           [attr.data-permission]="permission.name"
                           [checked]="isGranted(role.name, permission.name)"
                           (change)="toggle(role.name, permission.name, $any($event.target).checked)" />
                  </td>
                }
              </tr>
            }
          }
        </tbody>
      </table>
    </div>
  </div>
}

<p class="small-muted" style="margin-top:12px">
  Zmiany obowiązują na backendzie od razu. Menu i przyciski użytkownik zobaczy po ponownym zalogowaniu.
</p>
```

`permissions-matrix.component.scss`:

```scss
.module-row td {
  background: var(--accent-soft);
}
```

W `nav-items.ts` dodaj po pozycji „Użytkownicy i role”:

```ts
  { label: 'Uprawnienia ról', icon: '⚖', path: '/admin/permissions', permission: Permissions.PermissionsManage },
```

W `app.routes.ts` dodaj po trasie `admin/users`:

```ts
      {
        path: 'admin/permissions',
        canActivate: [permissionGuard(Permissions.PermissionsManage)],
        loadComponent: () => import('./features/admin-permissions/permissions-matrix.component').then(m => m.PermissionsMatrixComponent)
      },
```

- [ ] **Step 4: Uruchom — zielone** (w tym `app.routes.spec` weryfikujący guard nowej pozycji menu)

```bash
cd C:/eu02_install/DOKPortalLight/frontend && npx ng test --watch=false 2>&1 | grep -E "ERROR|FAIL|Test Files|Tests "
```

- [ ] **Step 5: Commit (bez push)**

```bash
cd C:/eu02_install/DOKPortalLight && git add frontend/src/app/features/admin-permissions frontend/src/app/app.routes.ts frontend/src/app/layout/nav-items.ts
git commit -m "$(cat <<'EOF'
Frontend: ekran Uprawnienia ról (macierz, własne role)

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: Ekran użytkowników — lista ról z API

**Files:**
- Modify: `frontend/src/app/features/admin-users/users.service.ts`, `user.model.ts`, `users-list.component.ts`, `users-list.component.html`
- Test: `frontend/src/app/features/admin-users/users.service.spec.ts`, `frontend/src/app/features/admin-users/users-list.component.spec.ts`

**Interfaces:**
- Consumes: `GET api/users/roles → string[]` (Task 2).
- Produces: `UsersService.listRoles(): Observable<string[]>`; w `UsersListComponent` `allRoles` jest sygnałem `Signal<string[]>` (w szablonie `allRoles()`); stała `ALL_ROLES` znika z `user.model.ts`.

- [ ] **Step 1: Testy (czerwone)** — w `users.service.spec.ts` dopisz test (zachowaj istniejące importy i `describe`; dopasuj nazwy zmiennych do istniejącego pliku — jeśli plik używa `service`/`httpMock`, użyj ich):

```ts
  it('loads the list of role names', () => {
    service.listRoles().subscribe(roles => expect(roles).toEqual(['Administrator', 'Sekretariat']));

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/users/roles`);
    expect(req.request.method).toBe('GET');
    req.flush(['Administrator', 'Sekretariat']);
  });
```

W `users-list.component.spec.ts` dopisz test:

```ts
  it('renders one role column per role returned by the API', () => {
    fixture.detectChanges();
    httpMock.expectOne(`${environment.apiBaseUrl}/api/users`)
      .flush([{ id: '1', email: 'admin@dokportal.local', personId: null, roles: ['Administrator'] }]);
    httpMock.expectOne(`${environment.apiBaseUrl}/api/users/roles`).flush(['Administrator', 'Sekretariat']);
    fixture.detectChanges();

    const headers = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('thead th')).map(th => th.textContent!.trim());
    expect(headers).toContain('Sekretariat');
    expect(fixture.nativeElement.querySelectorAll('tbody input[type="checkbox"]').length).toBe(2);
  });
```

- [ ] **Step 2: Uruchom — czerwone** (brak `listRoles`, brak wywołania `/api/users/roles`).

```bash
cd C:/eu02_install/DOKPortalLight/frontend && npx ng test --watch=false 2>&1 | grep -E "ERROR|FAIL|Test Files|Tests " | head -5
```

- [ ] **Step 3: Implementacja**

W `users.service.ts` dodaj metodę:

```ts
  listRoles() {
    return this.http.get<string[]>(`${this.baseUrl}/roles`);
  }
```

W `user.model.ts` usuń stałą `ALL_ROLES` (trzy pierwsze linie pliku, razem z pustą linią po nich).

W `users-list.component.ts`: zmień import na `import { AppUserAccount, CreateUserValue } from './user.model';`, zamień `readonly allRoles = ALL_ROLES;` na `readonly allRoles = signal<string[]>([]);`, a w `load()` dopisz pobranie ról (zachowując istniejące `subscribe` użytkowników):

```ts
  load(): void {
    this.usersService.list().subscribe({
      next: users => this.users.set(users),
      error: () => this.toast.error('Nie udało się wczytać listy użytkowników.')
    });
    this.usersService.listRoles().subscribe({
      next: roles => this.allRoles.set(roles),
      error: () => this.toast.error('Nie udało się wczytać listy ról.')
    });
  }
```

W `users-list.component.html` zamień obie pętle `@for (role of allRoles; track role)` na `@for (role of allRoles(); track role)` (nagłówek tabeli w linii 77 i komórki wiersza w linii 82).

- [ ] **Step 4: Uruchom — zielone**

```bash
cd C:/eu02_install/DOKPortalLight/frontend && npx ng test --watch=false 2>&1 | grep -E "ERROR|FAIL|Test Files|Tests "
```

- [ ] **Step 5: Commit (bez push)**

```bash
cd C:/eu02_install/DOKPortalLight && git add frontend/src/app/features/admin-users
git commit -m "$(cat <<'EOF'
Frontend: ekran użytkowników pobiera listę ról z API

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: Wpis „Co nowego”, weryfikacja końcowa i commit planu

**Files:**
- Modify: `frontend/src/app/features/dashboard/dashboard.component.ts`

**Interfaces:**
- Consumes: wszystkie poprzednie zadania.

- [ ] **Step 1: Wpis „Co nowego”** — na początku tablicy `changelog` w `dashboard.component.ts` dodaj:

```ts
    { date: '2026-10-03', text: 'Nowy ekran „Uprawnienia ról” (tylko Administrator): tabela, w której zaznaczasz, co może każda rola. Można też dodawać własne role (np. „Sekretariat”) i usuwać te, których nikt nie używa. Zmiany działają od razu na serwerze, a w menu i przyciskach użytkownik zobaczy je po ponownym zalogowaniu.' },
```

- [ ] **Step 2: Pełna weryfikacja**

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test 2>&1 | grep -E "error|Powodzenie|niepowodzenie"
cd C:/eu02_install/DOKPortalLight/frontend && npx ng test --watch=false 2>&1 | grep -E "ERROR|FAIL|Test Files|Tests "; npx ng build 2>&1 | grep -iE "error|Application bundle"
```
Expected: backend bez niepowodzeń, frontend wszystkie testy zielone, build kończy się „Application bundle generation complete” (jedyne ostrzeżenie to stare `login.component.scss`).

- [ ] **Step 3: Commit (bez push)**

```bash
cd C:/eu02_install/DOKPortalLight && git add frontend/src/app/features/dashboard/dashboard.component.ts docs/superpowers/plans/2026-10-03-permissions-part3-roles-screen.md
git status --short
git commit -m "$(cat <<'EOF'
Dashboard: wpis o ekranie Uprawnienia ról i własnych rolach

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
```

---

## Self-review

- **Pokrycie specyfikacji (część 3):** role z bazy + role systemowe nieusuwalne, Administrator niezmienny — Task 1; walidacja nazwy, unikalność bez względu na wielkość liter, start bez uprawnień, usuwanie tylko roli własnej bez użytkowników, czyszczenie przydziałów i cache — Task 1; `PermissionsController` z `matrix`/`PUT`/`POST`/`DELETE`, audyt `UpdateRolePermissions`/`CreateRole`/`DeleteRole`, `GET api/users/roles` — Task 2; test, że nowa rola działa w autoryzacji po nadaniu uprawnienia — Task 2; `PermissionsService`, komponent macierzy (moduły, checkboxy, Zapisz/Cofnij, dodawanie, usuwanie z `confirm`, informacja o ponownym logowaniu), trasa i menu — Tasks 3–4; lista ról w `users-list` z API — Task 5; wpis „Co nowego” — Task 6.
- **Placeholdery:** brak; kod i komendy kompletne (w Task 1 Step 3 opisane są dokładne podmiany w `UpdateRolePermissionsAsync` z fragmentem kodu).
- **Spójność typów:** `RoleInfoDto{Name,IsSystem}` ↔ `RoleInfo{name,isSystem}`; `PermissionMatrixDto.Roles : IReadOnlyList<RoleInfoDto>` ↔ `PermissionMatrix.roles`; `CreateRoleAsync/DeleteRoleAsync/ListRoleNamesAsync` użyte w kontrolerach z tymi samymi sygnaturami; endpointy `PUT roles/{role}` (body `{permissions}`) zgodne z `PermissionsService.updateRole`; atrybuty `data-*` w szablonie zgodne z selektorami w specyfikacji komponentu.
