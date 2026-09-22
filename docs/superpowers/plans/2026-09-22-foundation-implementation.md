# DOK Portal Light — Faza 1: Fundament — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up a working, deployable skeleton of DOK Portal Light: a monorepo with an ASP.NET Core 8 Web API (EF Core code-first over Azure SQL, JWT auth, RBAC) and an Angular frontend (login, dashboard, shared people directory, user/role admin), plus GitHub Actions CI/CD targeting Azure App Service + Azure Static Web Apps.

**Architecture:** Layered .NET backend (`Domain` → `Application` → `Infrastructure` → `Api`) using ASP.NET Core Identity for accounts/roles and a plain `Person`/`Parish` domain model as the shared registry later modules build on. Angular frontend with standalone components and signals, a global stylesheet ported 1:1 from the approved HTML prototype, and role-driven navigation (no more manual role switcher — real JWT claims drive it).

**Tech Stack:** .NET 8 (LTS), ASP.NET Core Web API + Identity + JWT Bearer, EF Core 8 (SQL Server / Azure SQL in dev+prod, SQLite/InMemory in tests), xUnit; Angular (latest stable, standalone components, signals), Karma/Jasmine; GitHub Actions.

**Spec:** [docs/superpowers/specs/2026-09-22-foundation-design.md](../specs/2026-09-22-foundation-design.md)

## Global Constraints

- Target framework: .NET 8 LTS for the backend (Azure App Service support is well-established).
- Database: Azure SQL in dev/prod via EF Core code-first migrations; tests never touch a real Azure SQL instance (SQLite in-memory for HTTP-level integration tests, EF Core InMemory for service-level unit tests).
- Migrations are applied automatically at API startup (`Database.Migrate()`), skipped when `ASPNETCORE_ENVIRONMENT=Testing`.
- RBAC roles (ASP.NET Core Identity roles, one user can hold several): `Administrator`, `Biskup`, `DyrektorSKSP`, `DyrektorDOK`, `Superwizor`, `KatechistaProwadzacy`. These are distinct from business-domain "roles" (Kandydat SKŚP, Katechista posłany, Podopieczny DOK, …), which are out of scope for this phase and will be modeled as their own tables with a `PersonId` FK in Faza 2/3 — never add them as Identity roles.
- Auth: custom login (email + password) issuing a JWT access token; no refresh token in this phase. Role claims use the short claim type `"role"` (not `ClaimTypes.Role`'s long URI) on both the token and `TokenValidationParameters.RoleClaimType`, so the frontend can decode `role` directly.
- No fabricated demo data anywhere in Phase 1: the dashboard and lists show real API data or an explicit empty state — never hardcoded prototype numbers.
- Repository layout is a GitHub monorepo: `/backend`, `/frontend`, `/.github/workflows`, `/docs`.
- Frontend visual design is copied 1:1 from `preview.html`'s CSS (variables, sidebar/topbar/card/table/modal classes) into `frontend/src/styles.scss` — no UI component library.
- The manual role-switcher dropdown from the prototype does **not** carry over — navigation visibility and the topbar role badges are driven by the real JWT's role claims.
- CI (`backend-ci.yml`, `frontend-ci.yml`) must pass on every push/PR before the corresponding deploy job in `deploy.yml` is considered meaningful; deploy requires Azure resources + GitHub secrets the user provisions themselves (this plan does not create Azure resources).

---

## Task 1: Backend solution & project scaffold

**Files:**
- Create: `backend/DokPortal.sln`
- Create: `backend/src/DokPortal.Domain/DokPortal.Domain.csproj`
- Create: `backend/src/DokPortal.Application/DokPortal.Application.csproj`
- Create: `backend/src/DokPortal.Infrastructure/DokPortal.Infrastructure.csproj`
- Create: `backend/src/DokPortal.Api/DokPortal.Api.csproj` (+ generated `Program.cs`, `appsettings*.json`)
- Create: `backend/tests/DokPortal.Domain.Tests/DokPortal.Domain.Tests.csproj`
- Create: `backend/tests/DokPortal.Infrastructure.Tests/DokPortal.Infrastructure.Tests.csproj`
- Create: `backend/tests/DokPortal.Api.IntegrationTests/DokPortal.Api.IntegrationTests.csproj`
- Create: `.gitignore` (repo root)

**Interfaces:**
- Produces: a buildable, empty solution that Task 2 onward add real code to. No public API yet.

- [ ] **Step 1: Scaffold projects with the .NET CLI**

Run from the repo root (`C:\eu02_install\DOKPortalLight`):

```bash
mkdir -p backend/src backend/tests
cd backend

dotnet new sln -n DokPortal

dotnet new classlib -n DokPortal.Domain -o src/DokPortal.Domain -f net8.0
dotnet new classlib -n DokPortal.Application -o src/DokPortal.Application -f net8.0
dotnet new classlib -n DokPortal.Infrastructure -o src/DokPortal.Infrastructure -f net8.0
dotnet new webapi -n DokPortal.Api -o src/DokPortal.Api -f net8.0 --use-controllers

dotnet new xunit -n DokPortal.Domain.Tests -o tests/DokPortal.Domain.Tests -f net8.0
dotnet new xunit -n DokPortal.Infrastructure.Tests -o tests/DokPortal.Infrastructure.Tests -f net8.0
dotnet new xunit -n DokPortal.Api.IntegrationTests -o tests/DokPortal.Api.IntegrationTests -f net8.0

dotnet sln add src/DokPortal.Domain src/DokPortal.Application src/DokPortal.Infrastructure src/DokPortal.Api tests/DokPortal.Domain.Tests tests/DokPortal.Infrastructure.Tests tests/DokPortal.Api.IntegrationTests

dotnet add src/DokPortal.Application reference src/DokPortal.Domain
dotnet add src/DokPortal.Infrastructure reference src/DokPortal.Application
dotnet add src/DokPortal.Api reference src/DokPortal.Infrastructure

dotnet add tests/DokPortal.Domain.Tests reference src/DokPortal.Domain
dotnet add tests/DokPortal.Infrastructure.Tests reference src/DokPortal.Infrastructure
dotnet add tests/DokPortal.Api.IntegrationTests reference src/DokPortal.Api
```

Delete the sample files the templates generate that we don't want: `src/DokPortal.Domain/Class1.cs`, `src/DokPortal.Application/Class1.cs`, `src/DokPortal.Infrastructure/Class1.cs`, and the default `WeatherForecast.cs` + `Controllers/WeatherForecastController.cs` in `DokPortal.Api`.

- [ ] **Step 2: Add the root .gitignore**

Create `.gitignore` at the repo root:

```gitignore
# .NET
bin/
obj/
*.user
appsettings.*.local.json

# Node / Angular
node_modules/
dist/
.angular/

# IDE
.vs/
.vscode/
*.suo

# OS
.DS_Store
Thumbs.db
```

- [ ] **Step 3: Verify the solution builds**

Run: `dotnet build backend/DokPortal.sln`
Expected: Build succeeds (0 errors) — no tests exist yet, so `dotnet test` isn't run in this task.

- [ ] **Step 4: Commit**

```bash
git add backend .gitignore
git commit -m "Scaffold backend solution with layered projects"
```

---

## Task 2: Domain entities — Person & Parish

**Files:**
- Create: `backend/src/DokPortal.Domain/Entities/Person.cs`
- Create: `backend/src/DokPortal.Domain/Entities/Parish.cs`
- Create: `backend/src/DokPortal.Domain/Constants/AppRoles.cs`
- Test: `backend/tests/DokPortal.Domain.Tests/PersonTests.cs`

**Interfaces:**
- Produces: `DokPortal.Domain.Entities.Person` (`Id`, `FirstName`, `LastName`, `Email`, `Phone`, `BirthDate` (`DateOnly?`), `ParishId` (`Guid?`), `Parish` nav, `Notes`, `CreatedAtUtc`, `UpdatedAtUtc`, computed `FullName`); `DokPortal.Domain.Entities.Parish` (`Id`, `Name`, `City`); `DokPortal.Domain.Constants.AppRoles` (string constants for the six RBAC roles).

- [ ] **Step 1: Write the failing test**

`backend/tests/DokPortal.Domain.Tests/PersonTests.cs`:

```csharp
using DokPortal.Domain.Entities;
using Xunit;

namespace DokPortal.Domain.Tests;

public class PersonTests
{
    [Fact]
    public void FullName_CombinesFirstAndLastName()
    {
        var person = new Person
        {
            Id = Guid.NewGuid(),
            FirstName = "Anna",
            LastName = "Maj"
        };

        Assert.Equal("Anna Maj", person.FullName);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/DokPortal.Domain.Tests`
Expected: FAIL to compile — `Person` doesn't exist yet.

- [ ] **Step 3: Implement the entities**

`backend/src/DokPortal.Domain/Entities/Person.cs`:

```csharp
namespace DokPortal.Domain.Entities;

public class Person
{
    public Guid Id { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateOnly? BirthDate { get; set; }
    public Guid? ParishId { get; set; }
    public Parish? Parish { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public string FullName => $"{FirstName} {LastName}";
}
```

`backend/src/DokPortal.Domain/Entities/Parish.cs`:

```csharp
namespace DokPortal.Domain.Entities;

public class Parish
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? City { get; set; }
}
```

`backend/src/DokPortal.Domain/Constants/AppRoles.cs`:

```csharp
namespace DokPortal.Domain.Constants;

public static class AppRoles
{
    public const string Administrator = "Administrator";
    public const string Biskup = "Biskup";
    public const string DyrektorSKSP = "DyrektorSKSP";
    public const string DyrektorDOK = "DyrektorDOK";
    public const string Superwizor = "Superwizor";
    public const string KatechistaProwadzacy = "KatechistaProwadzacy";

    public static readonly string[] All =
    {
        Administrator, Biskup, DyrektorSKSP, DyrektorDOK, Superwizor, KatechistaProwadzacy
    };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/DokPortal.Domain.Tests`
Expected: PASS (1 test).

- [ ] **Step 5: Commit**

```bash
git add backend/src/DokPortal.Domain backend/tests/DokPortal.Domain.Tests
git commit -m "Add Person and Parish domain entities plus RBAC role constants"
```

---

## Task 3: Infrastructure — Identity, DbContext, minimal API host, test harness

**Files:**
- Create: `backend/src/DokPortal.Infrastructure/Identity/AppUser.cs`
- Create: `backend/src/DokPortal.Infrastructure/Persistence/AppDbContext.cs`
- Modify: `backend/src/DokPortal.Api/Program.cs` (replace generated content)
- Modify: `backend/src/DokPortal.Api/appsettings.json`, `appsettings.Development.json`
- Create: `backend/tests/DokPortal.Api.IntegrationTests/CustomWebApplicationFactory.cs`
- Test: `backend/tests/DokPortal.Api.IntegrationTests/HealthCheckTests.cs`
- Test: `backend/tests/DokPortal.Api.IntegrationTests/PersonPersistenceTests.cs`

**Interfaces:**
- Consumes: `DokPortal.Domain.Entities.Person`, `Parish` (Task 2).
- Produces: `DokPortal.Infrastructure.Identity.AppUser : IdentityUser` (adds `Guid? PersonId`); `DokPortal.Infrastructure.Persistence.AppDbContext : IdentityDbContext<AppUser>` with `DbSet<Person> People`, `DbSet<Parish> Parishes`, and a `public AppDbContext(DbContextOptions<AppDbContext> options)` constructor; `public partial class Program` in the Api project (needed for `WebApplicationFactory<Program>`); `DokPortal.Api.IntegrationTests.CustomWebApplicationFactory` (swaps EF Core to SQLite in-memory, points content root at the Api project).

- [ ] **Step 1: Add required NuGet packages**

```bash
cd backend
dotnet add src/DokPortal.Infrastructure package Microsoft.EntityFrameworkCore.SqlServer --version 8.*
dotnet add src/DokPortal.Infrastructure package Microsoft.EntityFrameworkCore.Design --version 8.*
dotnet add src/DokPortal.Infrastructure package Microsoft.AspNetCore.Identity.EntityFrameworkCore --version 8.*

dotnet add tests/DokPortal.Api.IntegrationTests package Microsoft.AspNetCore.Mvc.Testing --version 8.*
dotnet add tests/DokPortal.Api.IntegrationTests package Microsoft.Data.Sqlite --version 8.*
dotnet add tests/DokPortal.Api.IntegrationTests package Microsoft.EntityFrameworkCore.Sqlite --version 8.*

dotnet tool install --global dotnet-ef --version 8.* || dotnet tool update --global dotnet-ef --version 8.*
```

**Deviation found during execution:** `dotnet ef` requires the *startup project itself* to directly reference `Microsoft.EntityFrameworkCore.Design` — a transitive reference via `DokPortal.Infrastructure` is not enough. Also add it directly to the Api project:

```bash
dotnet add src/DokPortal.Api package Microsoft.EntityFrameworkCore.Design --version 8.*
```

- [ ] **Step 2: Write the failing tests**

`backend/tests/DokPortal.Api.IntegrationTests/CustomWebApplicationFactory.cs`:

```csharp
using DokPortal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DokPortal.Api.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseContentRoot(GetApiProjectContentRoot());

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            using var scope = services.BuildServiceProvider().CreateScope();
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        });
    }

    private static string GetApiProjectContentRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src", "DokPortal.Api")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Nie znaleziono katalogu projektu DokPortal.Api.");
        }

        return Path.Combine(directory.FullName, "src", "DokPortal.Api");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection?.Dispose();
    }
}
```

`backend/tests/DokPortal.Api.IntegrationTests/HealthCheckTests.cs`:

```csharp
using System.Net;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class HealthCheckTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public HealthCheckTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

`backend/tests/DokPortal.Api.IntegrationTests/PersonPersistenceTests.cs`:

```csharp
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class PersonPersistenceTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PersonPersistenceTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task SavedPerson_CanBeReadBackInANewScope()
    {
        var personId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.People.Add(new Person
            {
                Id = personId,
                FirstName = "Jan",
                LastName = "Kowalski",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var loaded = await db.People.FindAsync(personId);
            Assert.NotNull(loaded);
            Assert.Equal("Jan Kowalski", loaded!.FullName);
        }
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test backend/tests/DokPortal.Api.IntegrationTests`
Expected: FAIL to compile — `AppUser`, `AppDbContext`, `Program` (as a testable type) don't exist yet.

- [ ] **Step 4: Implement Identity + DbContext**

`backend/src/DokPortal.Infrastructure/Identity/AppUser.cs`:

```csharp
using Microsoft.AspNetCore.Identity;

namespace DokPortal.Infrastructure.Identity;

public class AppUser : IdentityUser
{
    public Guid? PersonId { get; set; }
}
```

`backend/src/DokPortal.Infrastructure/Persistence/AppDbContext.cs`:

```csharp
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Person> People => Set<Person>();
    public DbSet<Parish> Parishes => Set<Parish>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Parish>(entity =>
        {
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
            entity.Property(p => p.City).HasMaxLength(200);
        });

        builder.Entity<Person>(entity =>
        {
            entity.Property(p => p.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(p => p.LastName).IsRequired().HasMaxLength(100);
            entity.Property(p => p.Email).HasMaxLength(256);
            entity.Property(p => p.Phone).HasMaxLength(50);

            entity.HasOne(p => p.Parish)
                .WithMany()
                .HasForeignKey(p => p.ParishId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
```

- [ ] **Step 5: Replace Program.cs**

`backend/src/DokPortal.Api/Program.cs`:

```csharp
using DokPortal.Infrastructure.Identity;
using DokPortal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program
{
}
```

- [ ] **Step 6: Configure appsettings**

`backend/src/DokPortal.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Default": ""
  },
  "Cors": {
    "AllowedOrigins": []
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

`backend/src/DokPortal.Api/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Server=(localdb)\\mssqllocaldb;Database=DokPortalLight;Trusted_Connection=True;MultipleActiveResultSets=true"
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:4200"]
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

- [ ] **Step 7: Generate the initial EF Core migration**

```bash
cd backend
dotnet ef migrations add InitialCreate --project src/DokPortal.Infrastructure --startup-project src/DokPortal.Api
```

Expected: a `Migrations/` folder appears under `src/DokPortal.Infrastructure` with `InitialCreate` creating the Identity tables plus `People` and `Parishes`.

- [ ] **Step 8: Run tests to verify they pass**

Run: `dotnet test backend/tests/DokPortal.Api.IntegrationTests`
Expected: PASS (2 tests: `Health_ReturnsOk`, `SavedPerson_CanBeReadBackInANewScope`).

- [ ] **Step 9: Commit**

```bash
git add backend
git commit -m "Add Identity-backed AppDbContext, minimal API host, and SQLite-based integration test harness"
```

---

## Task 4: JWT authentication — login endpoint

**Files:**
- Create: `backend/src/DokPortal.Application/Auth/LoginRequest.cs`
- Create: `backend/src/DokPortal.Application/Auth/LoginResponse.cs`
- Create: `backend/src/DokPortal.Application/Auth/IJwtTokenGenerator.cs`
- Create: `backend/src/DokPortal.Infrastructure/Auth/JwtOptions.cs`
- Create: `backend/src/DokPortal.Infrastructure/Auth/JwtTokenGenerator.cs`
- Create: `backend/src/DokPortal.Api/Controllers/AuthController.cs`
- Modify: `backend/src/DokPortal.Api/Program.cs`
- Modify: `backend/src/DokPortal.Api/appsettings.json`, `appsettings.Development.json`
- Create: `backend/src/DokPortal.Api/appsettings.Testing.json`
- Create: `backend/tests/DokPortal.Infrastructure.Tests/Auth/JwtTokenGeneratorTests.cs`
- Create: `backend/tests/DokPortal.Api.IntegrationTests/IntegrationTestBase.cs`
- Test: `backend/tests/DokPortal.Api.IntegrationTests/AuthControllerTests.cs`

**Interfaces:**
- Consumes: `AppUser` (Task 3), `AppDbContext` (Task 3).
- Produces: `IJwtTokenGenerator.GenerateToken(string userId, string email, Guid? personId, IEnumerable<string> roles) : string`; `POST /api/auth/login` returning `LoginResponse { Token, ExpiresAtUtc, Roles, PersonId }`; `IntegrationTestBase` with `CreateUserAndGetTokenAsync(...)` / `CreateAuthenticatedClientAsync(...)` helpers reused by every later controller test.

- [ ] **Step 1: Add required NuGet packages**

```bash
cd backend
dotnet add src/DokPortal.Infrastructure package System.IdentityModel.Tokens.Jwt --version 8.*
dotnet add src/DokPortal.Api package Microsoft.AspNetCore.Authentication.JwtBearer --version 8.*
dotnet add tests/DokPortal.Infrastructure.Tests package System.IdentityModel.Tokens.Jwt --version 8.*
```

- [ ] **Step 2: Write the failing tests**

`backend/tests/DokPortal.Infrastructure.Tests/Auth/JwtTokenGeneratorTests.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using DokPortal.Infrastructure.Auth;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Auth;

public class JwtTokenGeneratorTests
{
    [Fact]
    public void GenerateToken_IncludesRoleAndPersonIdClaims()
    {
        var options = new JwtOptions
        {
            Key = "unit-test-signing-key-1234567890123456",
            Issuer = "test",
            Audience = "test",
            ExpiryMinutes = 60
        };
        var generator = new JwtTokenGenerator(options);
        var personId = Guid.NewGuid();

        var token = generator.GenerateToken("user-1", "a@b.pl", personId, new[] { "Administrator" });

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Contains(jwt.Claims, c => c.Type == "role" && c.Value == "Administrator");
        Assert.Contains(jwt.Claims, c => c.Type == "personId" && c.Value == personId.ToString());
    }
}
```

`backend/tests/DokPortal.Api.IntegrationTests/IntegrationTestBase.cs`:

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DokPortal.Application.Auth;
using DokPortal.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public abstract class IntegrationTestBase : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    protected readonly CustomWebApplicationFactory Factory;
    protected HttpClient Client = null!;

    protected IntegrationTestBase(CustomWebApplicationFactory factory) => Factory = factory;

    public Task InitializeAsync()
    {
        Client = Factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    protected async Task<string> CreateUserAndGetTokenAsync(string email, string password, params string[] roles)
    {
        using var scope = Factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join(";", createResult.Errors.Select(e => e.Description)));
        }
        if (roles.Length > 0)
        {
            await userManager.AddToRolesAsync(user, roles);
        }

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });
        loginResponse.EnsureSuccessStatusCode();
        var body = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.Token;
    }

    protected async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password, params string[] roles)
    {
        var token = await CreateUserAndGetTokenAsync(email, password, roles);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
```

`backend/tests/DokPortal.Api.IntegrationTests/AuthControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Auth;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class AuthControllerTests : IntegrationTestBase
{
    public AuthControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenAndRoles()
    {
        var email = $"user-{Guid.NewGuid():N}@example.org";
        const string password = "Sekret123!";
        await CreateUserAndGetTokenAsync(email, password, "Administrator");

        var response = await Client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.Contains("Administrator", body.Roles);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        var email = $"user-{Guid.NewGuid():N}@example.org";
        await CreateUserAndGetTokenAsync(email, "Sekret123!", "Administrator");

        var response = await Client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test backend/tests/DokPortal.Infrastructure.Tests backend/tests/DokPortal.Api.IntegrationTests`
Expected: FAIL to compile — `JwtOptions`, `JwtTokenGenerator`, `LoginResponse`, `/api/auth/login` don't exist yet.

- [ ] **Step 4: Implement auth types**

`backend/src/DokPortal.Application/Auth/LoginRequest.cs`:

```csharp
namespace DokPortal.Application.Auth;

public class LoginRequest
{
    public required string Email { get; init; }
    public required string Password { get; init; }
}
```

`backend/src/DokPortal.Application/Auth/LoginResponse.cs`:

```csharp
namespace DokPortal.Application.Auth;

public class LoginResponse
{
    public required string Token { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
    public required IReadOnlyList<string> Roles { get; init; }
    public Guid? PersonId { get; init; }
}
```

`backend/src/DokPortal.Application/Auth/IJwtTokenGenerator.cs`:

```csharp
namespace DokPortal.Application.Auth;

public interface IJwtTokenGenerator
{
    string GenerateToken(string userId, string email, Guid? personId, IEnumerable<string> roles);
}
```

`backend/src/DokPortal.Infrastructure/Auth/JwtOptions.cs`:

```csharp
namespace DokPortal.Infrastructure.Auth;

public class JwtOptions
{
    public required string Key { get; init; }
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required int ExpiryMinutes { get; init; }
}
```

`backend/src/DokPortal.Infrastructure/Auth/JwtTokenGenerator.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DokPortal.Application.Auth;
using Microsoft.IdentityModel.Tokens;

namespace DokPortal.Infrastructure.Auth;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtOptions _options;

    public JwtTokenGenerator(JwtOptions options) => _options = options;

    public string GenerateToken(string userId, string email, Guid? personId, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (personId.HasValue)
        {
            claims.Add(new Claim("personId", personId.Value.ToString()));
        }

        claims.AddRange(roles.Select(role => new Claim("role", role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

`backend/src/DokPortal.Api/Controllers/AuthController.cs`:

```csharp
using DokPortal.Application.Auth;
using DokPortal.Infrastructure.Auth;
using DokPortal.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly JwtOptions _jwtOptions;

    public AuthController(UserManager<AppUser> userManager, IJwtTokenGenerator tokenGenerator, JwtOptions jwtOptions)
    {
        _userManager = userManager;
        _tokenGenerator = tokenGenerator;
        _jwtOptions = jwtOptions;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new { message = "Nieprawidłowy e-mail lub hasło." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = _tokenGenerator.GenerateToken(user.Id, user.Email!, user.PersonId, roles);

        return Ok(new LoginResponse
        {
            Token = token,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes),
            Roles = roles.ToList(),
            PersonId = user.PersonId
        });
    }
}
```

- [ ] **Step 5: Wire JWT bearer auth into Program.cs**

In `backend/src/DokPortal.Api/Program.cs`, add these usings at the top:

```csharp
using DokPortal.Application.Auth;
using DokPortal.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
```

Replace `builder.Services.AddAuthentication();` with:

```csharp
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("Missing Jwt configuration section.");
builder.Services.AddSingleton(jwtOptions);
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            RoleClaimType = "role"
        };
    });
```

- [ ] **Step 6: Add Jwt config to appsettings**

Add to `backend/src/DokPortal.Api/appsettings.json` (production placeholder — real value comes from an Azure App Service setting):

```json
"Jwt": {
  "Key": "",
  "Issuer": "DokPortalLight",
  "Audience": "DokPortalLight",
  "ExpiryMinutes": 480
}
```

Add to `backend/src/DokPortal.Api/appsettings.Development.json`:

```json
"Jwt": {
  "Key": "dev-only-signing-key-change-me-please-32chars-min",
  "Issuer": "DokPortalLight",
  "Audience": "DokPortalLight",
  "ExpiryMinutes": 480
}
```

Create `backend/src/DokPortal.Api/appsettings.Testing.json`:

```json
{
  "Jwt": {
    "Key": "testing-only-signing-key-1234567890123456",
    "Issuer": "DokPortalLight",
    "Audience": "DokPortalLight",
    "ExpiryMinutes": 480
  }
}
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test backend/tests/DokPortal.Infrastructure.Tests backend/tests/DokPortal.Api.IntegrationTests`
Expected: PASS (3 tests: `GenerateToken_IncludesRoleAndPersonIdClaims`, `Login_WithValidCredentials_ReturnsTokenAndRoles`, `Login_WithInvalidPassword_ReturnsUnauthorized`).

- [ ] **Step 8: Commit**

```bash
git add backend
git commit -m "Add JWT login endpoint and shared integration test authentication helpers"
```

---

## Task 5: Database seeding — roles, admin account, sample parishes

**Files:**
- Create: `backend/src/DokPortal.Infrastructure/Seed/DbSeeder.cs`
- Modify: `backend/src/DokPortal.Api/Program.cs`
- Modify: `backend/src/DokPortal.Api/appsettings.Development.json`, `appsettings.Testing.json`
- Create: `backend/tests/DokPortal.Infrastructure.Tests/Seed/DbSeederTests.cs`

**Interfaces:**
- Consumes: `AppDbContext`, `AppUser` (Task 3), `AppRoles` (Task 2).
- Produces: `DokPortal.Infrastructure.Seed.DbSeeder.SeedAsync(IServiceProvider services) : Task` — idempotent; called once from `Program.cs` after migration.

- [ ] **Step 1: Add required NuGet package**

```bash
cd backend
dotnet add tests/DokPortal.Infrastructure.Tests package Microsoft.EntityFrameworkCore.InMemory --version 8.*
dotnet add tests/DokPortal.Infrastructure.Tests package Microsoft.AspNetCore.Identity.EntityFrameworkCore --version 8.*
```

- [ ] **Step 2: Write the failing tests**

`backend/tests/DokPortal.Infrastructure.Tests/Seed/DbSeederTests.cs`:

```csharp
using DokPortal.Infrastructure.Identity;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Seed;

public class DbSeederTests
{
    private static ServiceProvider BuildServices(string dbName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        services
            .AddIdentityCore<AppUser>(o => o.Password.RequiredLength = 8)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        var configValues = new Dictionary<string, string?>
        {
            ["SeedAdmin:Email"] = "admin@dokportal.local",
            ["SeedAdmin:Password"] = "Sekret123!"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
        services.AddSingleton<IConfiguration>(configuration);

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task SeedAsync_CreatesRolesAdminUserAndParishes()
    {
        var provider = BuildServices(Guid.NewGuid().ToString());

        await DbSeeder.SeedAsync(provider);

        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True(await roleManager.RoleExistsAsync("Administrator"));
        Assert.True(await roleManager.RoleExistsAsync("KatechistaProwadzacy"));

        var userManager = provider.GetRequiredService<UserManager<AppUser>>();
        var admin = await userManager.FindByEmailAsync("admin@dokportal.local");
        Assert.NotNull(admin);
        Assert.True(await userManager.IsInRoleAsync(admin!, "Administrator"));

        var db = provider.GetRequiredService<AppDbContext>();
        Assert.True(await db.Parishes.AnyAsync());
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent_WhenRunTwice()
    {
        var provider = BuildServices(Guid.NewGuid().ToString());

        await DbSeeder.SeedAsync(provider);
        await DbSeeder.SeedAsync(provider);

        var db = provider.GetRequiredService<AppDbContext>();
        Assert.Equal(3, await db.Parishes.CountAsync());
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test backend/tests/DokPortal.Infrastructure.Tests`
Expected: FAIL to compile — `DbSeeder` doesn't exist yet.

- [ ] **Step 4: Implement DbSeeder**

`backend/src/DokPortal.Infrastructure/Seed/DbSeeder.cs`:

```csharp
using DokPortal.Domain.Constants;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Identity;
using DokPortal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DokPortal.Infrastructure.Seed;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var configuration = services.GetRequiredService<IConfiguration>();
        var adminEmail = configuration["SeedAdmin:Email"];
        var adminPassword = configuration["SeedAdmin:Password"];

        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            var userManager = services.GetRequiredService<UserManager<AppUser>>();
            var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
            if (existingAdmin is null)
            {
                var admin = new AppUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
                var result = await userManager.CreateAsync(admin, adminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, AppRoles.Administrator);
                }
            }
        }

        var db = services.GetRequiredService<AppDbContext>();
        if (!await db.Parishes.AnyAsync())
        {
            db.Parishes.AddRange(
                new Parish { Id = Guid.NewGuid(), Name = "św. Mateusza" },
                new Parish { Id = Guid.NewGuid(), Name = "Chrystusa Króla" },
                new Parish { Id = Guid.NewGuid(), Name = "św. Józefa" });
            await db.SaveChangesAsync();
        }
    }
}
```

- [ ] **Step 5: Call the seeder from Program.cs**

In `backend/src/DokPortal.Api/Program.cs`, add `using DokPortal.Infrastructure.Seed;` and change the migration block to:

```csharp
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
    await DbSeeder.SeedAsync(scope.ServiceProvider);
}
```

- [ ] **Step 6: Add SeedAdmin config**

Add to `backend/src/DokPortal.Api/appsettings.Development.json`:

```json
"SeedAdmin": {
  "Email": "admin@dokportal.local",
  "Password": "ZmienMnie!123"
}
```

Add to `backend/src/DokPortal.Api/appsettings.Testing.json`:

```json
"SeedAdmin": {
  "Email": "admin@dokportal.local",
  "Password": "Sekret123!"
}
```

(Leave `appsettings.json`, i.e. production, without a `SeedAdmin` section — Azure App Service settings `SeedAdmin__Email` / `SeedAdmin__Password` supply it in production; without them the seeder silently skips admin creation, which is documented in Task 18's README.)

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test backend/tests/DokPortal.Infrastructure.Tests`
Expected: PASS (2 tests).

Then run the full backend suite to confirm nothing broke: `dotnet test backend/DokPortal.sln`
Expected: All tests still pass.

- [ ] **Step 8: Commit**

```bash
git add backend
git commit -m "Seed RBAC roles, an admin account, and sample parishes on startup"
```

---

## Task 6: People module (shared person registry)

**Files:**
- Create: `backend/src/DokPortal.Application/Common/PagedResult.cs`
- Create: `backend/src/DokPortal.Application/People/PersonDto.cs`
- Create: `backend/src/DokPortal.Application/People/CreatePersonRequest.cs`
- Create: `backend/src/DokPortal.Application/People/UpdatePersonRequest.cs`
- Create: `backend/src/DokPortal.Application/People/IPersonService.cs`
- Create: `backend/src/DokPortal.Infrastructure/Services/PersonService.cs`
- Create: `backend/src/DokPortal.Api/Controllers/PeopleController.cs`
- Modify: `backend/src/DokPortal.Api/Program.cs`
- Test: `backend/tests/DokPortal.Infrastructure.Tests/Services/PersonServiceTests.cs`
- Test: `backend/tests/DokPortal.Api.IntegrationTests/PeopleControllerTests.cs`

**Interfaces:**
- Consumes: `Person`, `Parish` (Task 2), `AppDbContext` (Task 3), `IntegrationTestBase` (Task 4).
- Produces: `IPersonService` (`SearchAsync`, `GetByIdAsync`, `CreateAsync`, `UpdateAsync`); REST endpoints `GET/POST /api/people`, `GET/PUT /api/people/{id}`.

- [ ] **Step 1: Write the failing unit tests**

`backend/tests/DokPortal.Infrastructure.Tests/Services/PersonServiceTests.cs`:

```csharp
using DokPortal.Application.People;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class PersonServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task CreateAsync_ThenSearchAsync_FindsPersonByPartialName()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var service = new PersonService(db);

        await service.CreateAsync(new CreatePersonRequest { FirstName = "Maria", LastName = "Kaczmarek" }, default);

        var result = await service.SearchAsync("kaczma", 1, 20, default);

        Assert.Single(result.Items);
        Assert.Equal("Maria Kaczmarek", result.Items[0].FullName);
    }

    [Fact]
    public async Task UpdateAsync_WhenPersonMissing_ReturnsNull()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var service = new PersonService(db);

        var result = await service.UpdateAsync(
            Guid.NewGuid(), new UpdatePersonRequest { FirstName = "X", LastName = "Y" }, default);

        Assert.Null(result);
    }
}
```

`backend/tests/DokPortal.Api.IntegrationTests/PeopleControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Common;
using DokPortal.Application.People;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class PeopleControllerTests : IntegrationTestBase
{
    public PeopleControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Create_ThenGetById_ReturnsCreatedPerson()
    {
        var client = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

        var createResponse = await client.PostAsJsonAsync("/api/people", new
        {
            FirstName = "Anna",
            LastName = "Maj",
            Email = "anna.maj@example.org"
        });

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<PersonDto>();
        Assert.NotNull(created);
        Assert.Equal("Anna Maj", created!.FullName);

        var getResponse = await client.GetAsync($"/api/people/{created.Id}");
        getResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Search_ByLastName_ReturnsMatchingPerson()
    {
        var client = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        await client.PostAsJsonAsync("/api/people", new { FirstName = "Jan", LastName = "Kowalski" });

        var response = await client.GetAsync("/api/people?query=Kowalski");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<PersonDto>>();
        Assert.NotNull(result);
        Assert.Contains(result!.Items, p => p.LastName == "Kowalski");
    }

    [Fact]
    public async Task Create_WithoutRequiredRole_ReturnsForbidden()
    {
        var client = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");

        var response = await client.PostAsJsonAsync("/api/people", new { FirstName = "Test", LastName = "User" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/tests/DokPortal.Infrastructure.Tests backend/tests/DokPortal.Api.IntegrationTests`
Expected: FAIL to compile — `IPersonService`, `PersonService`, `PeopleController` don't exist yet.

- [ ] **Step 3: Implement Application contracts**

`backend/src/DokPortal.Application/Common/PagedResult.cs`:

```csharp
namespace DokPortal.Application.Common;

public class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int TotalCount { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
}
```

`backend/src/DokPortal.Application/People/PersonDto.cs`:

```csharp
namespace DokPortal.Application.People;

public class PersonDto
{
    public required Guid Id { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string FullName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public DateOnly? BirthDate { get; init; }
    public Guid? ParishId { get; init; }
    public string? ParishName { get; init; }
    public string? Notes { get; init; }
}
```

`backend/src/DokPortal.Application/People/CreatePersonRequest.cs`:

```csharp
namespace DokPortal.Application.People;

public class CreatePersonRequest
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public DateOnly? BirthDate { get; init; }
    public Guid? ParishId { get; init; }
    public string? Notes { get; init; }
}
```

`backend/src/DokPortal.Application/People/UpdatePersonRequest.cs`:

```csharp
namespace DokPortal.Application.People;

public class UpdatePersonRequest : CreatePersonRequest
{
}
```

`backend/src/DokPortal.Application/People/IPersonService.cs`:

```csharp
using DokPortal.Application.Common;

namespace DokPortal.Application.People;

public interface IPersonService
{
    Task<PagedResult<PersonDto>> SearchAsync(string? query, int page, int pageSize, CancellationToken ct);
    Task<PersonDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<PersonDto> CreateAsync(CreatePersonRequest request, CancellationToken ct);
    Task<PersonDto?> UpdateAsync(Guid id, UpdatePersonRequest request, CancellationToken ct);
}
```

- [ ] **Step 4: Implement PersonService**

`backend/src/DokPortal.Infrastructure/Services/PersonService.cs`:

```csharp
using DokPortal.Application.Common;
using DokPortal.Application.People;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class PersonService : IPersonService
{
    private readonly AppDbContext _db;

    public PersonService(AppDbContext db) => _db = db;

    public async Task<PagedResult<PersonDto>> SearchAsync(string? query, int page, int pageSize, CancellationToken ct)
    {
        var q = _db.People.Include(p => p.Parish).AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLower();
            q = q.Where(p =>
                p.FirstName.ToLower().Contains(term) ||
                p.LastName.ToLower().Contains(term) ||
                (p.Email != null && p.Email.ToLower().Contains(term)) ||
                (p.Parish != null && p.Parish.Name.ToLower().Contains(term)));
        }

        var total = await q.CountAsync(ct);
        var entities = await q
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<PersonDto>
        {
            Items = entities.Select(ToDto).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PersonDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var person = await _db.People.Include(p => p.Parish).AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        return person is null ? null : ToDto(person);
    }

    public async Task<PersonDto> CreateAsync(CreatePersonRequest request, CancellationToken ct)
    {
        var person = new Person
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Phone = request.Phone,
            BirthDate = request.BirthDate,
            ParishId = request.ParishId,
            Notes = request.Notes,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _db.People.Add(person);
        await _db.SaveChangesAsync(ct);
        return (await GetByIdAsync(person.Id, ct))!;
    }

    public async Task<PersonDto?> UpdateAsync(Guid id, UpdatePersonRequest request, CancellationToken ct)
    {
        var person = await _db.People.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (person is null) return null;

        person.FirstName = request.FirstName;
        person.LastName = request.LastName;
        person.Email = request.Email;
        person.Phone = request.Phone;
        person.BirthDate = request.BirthDate;
        person.ParishId = request.ParishId;
        person.Notes = request.Notes;
        person.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    private static PersonDto ToDto(Person p) => new()
    {
        Id = p.Id,
        FirstName = p.FirstName,
        LastName = p.LastName,
        FullName = p.FullName,
        Email = p.Email,
        Phone = p.Phone,
        BirthDate = p.BirthDate,
        ParishId = p.ParishId,
        ParishName = p.Parish?.Name,
        Notes = p.Notes
    };
}
```

- [ ] **Step 5: Implement PeopleController**

`backend/src/DokPortal.Api/Controllers/PeopleController.cs`:

```csharp
using DokPortal.Application.Common;
using DokPortal.Application.People;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/people")]
[Authorize]
public class PeopleController : ControllerBase
{
    private readonly IPersonService _personService;

    public PeopleController(IPersonService personService) => _personService = personService;

    [HttpGet]
    public async Task<ActionResult<PagedResult<PersonDto>>> Search(
        [FromQuery] string? query, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _personService.SearchAsync(query, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PersonDto>> GetById(Guid id, CancellationToken ct)
    {
        var person = await _personService.GetByIdAsync(id, ct);
        return person is null ? NotFound() : Ok(person);
    }

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorSKSP},{AppRoles.DyrektorDOK}")]
    public async Task<ActionResult<PersonDto>> Create(CreatePersonRequest request, CancellationToken ct)
    {
        var created = await _personService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorSKSP},{AppRoles.DyrektorDOK}")]
    public async Task<ActionResult<PersonDto>> Update(Guid id, UpdatePersonRequest request, CancellationToken ct)
    {
        var updated = await _personService.UpdateAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }
}
```

- [ ] **Step 6: Register the service**

In `backend/src/DokPortal.Api/Program.cs`, add `using DokPortal.Application.People;` and `using DokPortal.Infrastructure.Services;`, then add after the `AddScoped<IJwtTokenGenerator, ...>` line:

```csharp
builder.Services.AddScoped<IPersonService, PersonService>();
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test backend/DokPortal.sln`
Expected: All tests pass, including the 2 new `PersonServiceTests` and 3 new `PeopleControllerTests`.

- [ ] **Step 8: Commit**

```bash
git add backend
git commit -m "Add People module: search, get, create, update with role-gated writes"
```

---

## Task 7: Parishes module

**Files:**
- Create: `backend/src/DokPortal.Application/Parishes/ParishDto.cs`
- Create: `backend/src/DokPortal.Application/Parishes/CreateParishRequest.cs`
- Create: `backend/src/DokPortal.Application/Parishes/IParishService.cs`
- Create: `backend/src/DokPortal.Infrastructure/Services/ParishService.cs`
- Create: `backend/src/DokPortal.Api/Controllers/ParishesController.cs`
- Modify: `backend/src/DokPortal.Api/Program.cs`
- Test: `backend/tests/DokPortal.Infrastructure.Tests/Services/ParishServiceTests.cs`
- Test: `backend/tests/DokPortal.Api.IntegrationTests/ParishesControllerTests.cs`

**Interfaces:**
- Consumes: `Parish` (Task 2), `AppDbContext` (Task 3).
- Produces: `IParishService` (`GetAllAsync`, `CreateAsync`); `GET/POST /api/parishes`.

- [ ] **Step 1: Write the failing tests**

`backend/tests/DokPortal.Infrastructure.Tests/Services/ParishServiceTests.cs`:

```csharp
using DokPortal.Application.Parishes;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class ParishServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task CreateAsync_ThenGetAllAsync_ReturnsCreatedParish()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var service = new ParishService(db);

        await service.CreateAsync(new CreateParishRequest { Name = "św. Pawła", City = "Bydgoszcz" }, default);
        var all = await service.GetAllAsync(default);

        Assert.Contains(all, p => p.Name == "św. Pawła" && p.City == "Bydgoszcz");
    }
}
```

`backend/tests/DokPortal.Api.IntegrationTests/ParishesControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Parishes;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class ParishesControllerTests : IntegrationTestBase
{
    public ParishesControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Create_ThenGetAll_ReturnsCreatedParish()
    {
        var client = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

        var createResponse = await client.PostAsJsonAsync("/api/parishes", new { Name = "św. Marka", City = "Bydgoszcz" });
        createResponse.EnsureSuccessStatusCode();

        var getResponse = await client.GetAsync("/api/parishes");
        getResponse.EnsureSuccessStatusCode();
        var all = await getResponse.Content.ReadFromJsonAsync<List<ParishDto>>();
        Assert.Contains(all!, p => p.Name == "św. Marka");
    }

    [Fact]
    public async Task Create_WithoutAdministratorRole_ReturnsForbidden()
    {
        var client = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");

        var response = await client.PostAsJsonAsync("/api/parishes", new { Name = "Test" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/tests/DokPortal.Infrastructure.Tests backend/tests/DokPortal.Api.IntegrationTests`
Expected: FAIL to compile — `IParishService`, `ParishService`, `ParishesController` don't exist yet.

- [ ] **Step 3: Implement Application contracts and service**

`backend/src/DokPortal.Application/Parishes/ParishDto.cs`:

```csharp
namespace DokPortal.Application.Parishes;

public class ParishDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? City { get; init; }
}
```

`backend/src/DokPortal.Application/Parishes/CreateParishRequest.cs`:

```csharp
namespace DokPortal.Application.Parishes;

public class CreateParishRequest
{
    public required string Name { get; init; }
    public string? City { get; init; }
}
```

`backend/src/DokPortal.Application/Parishes/IParishService.cs`:

```csharp
namespace DokPortal.Application.Parishes;

public interface IParishService
{
    Task<IReadOnlyList<ParishDto>> GetAllAsync(CancellationToken ct);
    Task<ParishDto> CreateAsync(CreateParishRequest request, CancellationToken ct);
}
```

`backend/src/DokPortal.Infrastructure/Services/ParishService.cs`:

```csharp
using DokPortal.Application.Parishes;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class ParishService : IParishService
{
    private readonly AppDbContext _db;

    public ParishService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ParishDto>> GetAllAsync(CancellationToken ct)
    {
        var parishes = await _db.Parishes.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);
        return parishes.Select(p => new ParishDto { Id = p.Id, Name = p.Name, City = p.City }).ToList();
    }

    public async Task<ParishDto> CreateAsync(CreateParishRequest request, CancellationToken ct)
    {
        var parish = new Parish { Id = Guid.NewGuid(), Name = request.Name, City = request.City };
        _db.Parishes.Add(parish);
        await _db.SaveChangesAsync(ct);
        return new ParishDto { Id = parish.Id, Name = parish.Name, City = parish.City };
    }
}
```

- [ ] **Step 4: Implement ParishesController**

`backend/src/DokPortal.Api/Controllers/ParishesController.cs`:

```csharp
using DokPortal.Application.Parishes;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/parishes")]
[Authorize]
public class ParishesController : ControllerBase
{
    private readonly IParishService _parishService;

    public ParishesController(IParishService parishService) => _parishService = parishService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ParishDto>>> GetAll(CancellationToken ct)
        => Ok(await _parishService.GetAllAsync(ct));

    [HttpPost]
    [Authorize(Roles = AppRoles.Administrator)]
    public async Task<ActionResult<ParishDto>> Create(CreateParishRequest request, CancellationToken ct)
    {
        var created = await _parishService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), created);
    }
}
```

- [ ] **Step 5: Register the service**

In `backend/src/DokPortal.Api/Program.cs`, add `using DokPortal.Application.Parishes;` and register:

```csharp
builder.Services.AddScoped<IParishService, ParishService>();
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test backend/DokPortal.sln`
Expected: All tests pass.

- [ ] **Step 7: Commit**

```bash
git add backend
git commit -m "Add Parishes module"
```

---

## Task 8: Users admin module (account + role management)

**Files:**
- Create: `backend/src/DokPortal.Application/Users/UserDto.cs`
- Create: `backend/src/DokPortal.Application/Users/CreateUserRequest.cs`
- Create: `backend/src/DokPortal.Application/Users/AssignRolesRequest.cs`
- Create: `backend/src/DokPortal.Application/Users/IUserService.cs`
- Create: `backend/src/DokPortal.Infrastructure/Services/UserService.cs`
- Create: `backend/src/DokPortal.Api/Controllers/UsersController.cs`
- Modify: `backend/src/DokPortal.Api/Program.cs`
- Test: `backend/tests/DokPortal.Api.IntegrationTests/UsersControllerTests.cs`

**Interfaces:**
- Consumes: `AppUser` (Task 3), `AppRoles` (Task 2).
- Produces: `IUserService` (`ListAsync`, `CreateAsync`, `AssignRolesAsync`); `GET/POST /api/users`, `PUT /api/users/{id}/roles` (all `Administrator`-only).

- [ ] **Step 1: Write the failing tests**

`backend/tests/DokPortal.Api.IntegrationTests/UsersControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Users;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class UsersControllerTests : IntegrationTestBase
{
    public UsersControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Create_ThenAssignRoles_UpdatesUserRoles()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var newUserEmail = $"nowy-{Guid.NewGuid():N}@example.org";

        var createResponse = await admin.PostAsJsonAsync("/api/users", new
        {
            Email = newUserEmail,
            Password = "Sekret123!",
            Roles = new[] { "KatechistaProwadzacy" }
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<UserDto>();

        var assignResponse = await admin.PutAsJsonAsync($"/api/users/{created!.Id}/roles", new { Roles = new[] { "Superwizor" } });
        assignResponse.EnsureSuccessStatusCode();
        var updated = await assignResponse.Content.ReadFromJsonAsync<UserDto>();

        Assert.Contains("Superwizor", updated!.Roles);
        Assert.DoesNotContain("KatechistaProwadzacy", updated.Roles);
    }

    [Fact]
    public async Task List_WithoutAdministratorRole_ReturnsForbidden()
    {
        var client = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/tests/DokPortal.Api.IntegrationTests`
Expected: FAIL to compile — `IUserService`, `UserService`, `UsersController` don't exist yet.

- [ ] **Step 3: Implement Application contracts**

`backend/src/DokPortal.Application/Users/UserDto.cs`:

```csharp
namespace DokPortal.Application.Users;

public class UserDto
{
    public required string Id { get; init; }
    public required string Email { get; init; }
    public Guid? PersonId { get; init; }
    public required IReadOnlyList<string> Roles { get; init; }
}
```

`backend/src/DokPortal.Application/Users/CreateUserRequest.cs`:

```csharp
namespace DokPortal.Application.Users;

public class CreateUserRequest
{
    public required string Email { get; init; }
    public required string Password { get; init; }
    public Guid? PersonId { get; init; }
    public required IReadOnlyList<string> Roles { get; init; }
}
```

`backend/src/DokPortal.Application/Users/AssignRolesRequest.cs`:

```csharp
namespace DokPortal.Application.Users;

public class AssignRolesRequest
{
    public required IReadOnlyList<string> Roles { get; init; }
}
```

`backend/src/DokPortal.Application/Users/IUserService.cs`:

```csharp
namespace DokPortal.Application.Users;

public interface IUserService
{
    Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct);
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct);
    Task<UserDto?> AssignRolesAsync(string userId, IReadOnlyList<string> roles, CancellationToken ct);
}
```

- [ ] **Step 4: Implement UserService**

`backend/src/DokPortal.Infrastructure/Services/UserService.cs`:

```csharp
using DokPortal.Application.Users;
using DokPortal.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly UserManager<AppUser> _userManager;

    public UserService(UserManager<AppUser> userManager) => _userManager = userManager;

    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct)
    {
        var users = await _userManager.Users.AsNoTracking().ToListAsync(ct);
        var result = new List<UserDto>(users.Count);
        foreach (var user in users)
        {
            result.Add(ToDto(user, await _userManager.GetRolesAsync(user)));
        }
        return result;
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        var user = new AppUser { UserName = request.Email, Email = request.Email, PersonId = request.PersonId };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Nie udało się utworzyć użytkownika: {errors}");
        }

        if (request.Roles.Count > 0)
        {
            await _userManager.AddToRolesAsync(user, request.Roles);
        }

        return ToDto(user, await _userManager.GetRolesAsync(user));
    }

    public async Task<UserDto?> AssignRolesAsync(string userId, IReadOnlyList<string> roles, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return null;

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
        {
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
        }
        if (roles.Count > 0)
        {
            await _userManager.AddToRolesAsync(user, roles);
        }

        return ToDto(user, await _userManager.GetRolesAsync(user));
    }

    private static UserDto ToDto(AppUser user, IList<string> roles) => new()
    {
        Id = user.Id,
        Email = user.Email!,
        PersonId = user.PersonId,
        Roles = roles.ToList()
    };
}
```

- [ ] **Step 5: Implement UsersController**

`backend/src/DokPortal.Api/Controllers/UsersController.cs`:

```csharp
using DokPortal.Application.Users;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = AppRoles.Administrator)]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService) => _userService = userService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> List(CancellationToken ct)
        => Ok(await _userService.ListAsync(ct));

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var created = await _userService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(List), created);
    }

    [HttpPut("{id}/roles")]
    public async Task<ActionResult<UserDto>> AssignRoles(string id, AssignRolesRequest request, CancellationToken ct)
    {
        var updated = await _userService.AssignRolesAsync(id, request.Roles, ct);
        return updated is null ? NotFound() : Ok(updated);
    }
}
```

- [ ] **Step 6: Register the service**

In `backend/src/DokPortal.Api/Program.cs`, add `using DokPortal.Application.Users;` and register:

```csharp
builder.Services.AddScoped<IUserService, UserService>();
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test backend/DokPortal.sln`
Expected: All tests pass.

- [ ] **Step 8: Commit**

```bash
git add backend
git commit -m "Add Users admin module for account creation and role assignment"
```

---

## Task 9: Dashboard summary module

**Files:**
- Create: `backend/src/DokPortal.Application/Dashboard/DashboardSummaryDto.cs`
- Create: `backend/src/DokPortal.Application/Dashboard/IDashboardService.cs`
- Create: `backend/src/DokPortal.Infrastructure/Services/DashboardService.cs`
- Create: `backend/src/DokPortal.Api/Controllers/DashboardController.cs`
- Modify: `backend/src/DokPortal.Api/Program.cs`
- Test: `backend/tests/DokPortal.Infrastructure.Tests/Services/DashboardServiceTests.cs`
- Test: `backend/tests/DokPortal.Api.IntegrationTests/DashboardControllerTests.cs`

**Interfaces:**
- Consumes: `AppDbContext` (Task 3), `Person`/`Parish` (Task 2).
- Produces: `IDashboardService.GetSummaryAsync` returning `DashboardSummaryDto { PeopleCount, ParishCount }`; `GET /api/dashboard/summary`.

- [ ] **Step 1: Write the failing tests**

`backend/tests/DokPortal.Infrastructure.Tests/Services/DashboardServiceTests.cs`:

```csharp
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class DashboardServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_CountsPeopleAndParishes()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);
        db.Parishes.Add(new Parish { Id = Guid.NewGuid(), Name = "św. Pawła" });
        db.People.Add(new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski" });
        await db.SaveChangesAsync();

        var service = new DashboardService(db);
        var summary = await service.GetSummaryAsync(default);

        Assert.Equal(1, summary.PeopleCount);
        Assert.Equal(1, summary.ParishCount);
    }
}
```

`backend/tests/DokPortal.Api.IntegrationTests/DashboardControllerTests.cs`:

```csharp
using System.Net.Http.Json;
using DokPortal.Application.Dashboard;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class DashboardControllerTests : IntegrationTestBase
{
    public DashboardControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetSummary_ReturnsCounts()
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");

        var response = await client.GetAsync("/api/dashboard/summary");

        response.EnsureSuccessStatusCode();
        var summary = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>();
        Assert.NotNull(summary);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/tests/DokPortal.Infrastructure.Tests backend/tests/DokPortal.Api.IntegrationTests`
Expected: FAIL to compile — `IDashboardService`, `DashboardService`, `DashboardController` don't exist yet.

- [ ] **Step 3: Implement**

`backend/src/DokPortal.Application/Dashboard/DashboardSummaryDto.cs`:

```csharp
namespace DokPortal.Application.Dashboard;

public class DashboardSummaryDto
{
    public required int PeopleCount { get; init; }
    public required int ParishCount { get; init; }
}
```

`backend/src/DokPortal.Application/Dashboard/IDashboardService.cs`:

```csharp
namespace DokPortal.Application.Dashboard;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken ct);
}
```

`backend/src/DokPortal.Infrastructure/Services/DashboardService.cs`:

```csharp
using DokPortal.Application.Dashboard;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _db;

    public DashboardService(AppDbContext db) => _db = db;

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken ct) => new()
    {
        PeopleCount = await _db.People.CountAsync(ct),
        ParishCount = await _db.Parishes.CountAsync(ct)
    };
}
```

`backend/src/DokPortal.Api/Controllers/DashboardController.cs`:

```csharp
using DokPortal.Application.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService) => _dashboardService = dashboardService;

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(CancellationToken ct)
        => Ok(await _dashboardService.GetSummaryAsync(ct));
}
```

- [ ] **Step 4: Register the service**

In `backend/src/DokPortal.Api/Program.cs`, add `using DokPortal.Application.Dashboard;` and register:

```csharp
builder.Services.AddScoped<IDashboardService, DashboardService>();
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test backend/DokPortal.sln`
Expected: All tests pass.

- [ ] **Step 6: Commit**

```bash
git add backend
git commit -m "Add Dashboard summary endpoint"
```

---

## Task 10: Backend CI workflow

**Files:**
- Create: `.github/workflows/backend-ci.yml`

**Interfaces:**
- Consumes: `backend/DokPortal.sln` (Task 1 onward).
- Produces: a GitHub Actions job that must be green before Task 18's deploy job is meaningful.

- [ ] **Step 1: Write the workflow**

`.github/workflows/backend-ci.yml`:

```yaml
name: Backend CI

on:
  push:
    branches: [main]
    paths: ["backend/**", ".github/workflows/backend-ci.yml"]
  pull_request:
    paths: ["backend/**", ".github/workflows/backend-ci.yml"]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: backend
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "8.0.x"
      - run: dotnet restore
      - run: dotnet build --no-restore --configuration Release
      - run: dotnet test --no-build --configuration Release
```

- [ ] **Step 2: Verify equivalent commands succeed locally**

This step is config, not code, so there's no red/green cycle — verify by running the same commands the workflow runs:

Run: `cd backend && dotnet restore && dotnet build --no-restore --configuration Release && dotnet test --no-build --configuration Release`
Expected: restore, build, and all tests succeed in Release configuration (not just Debug).

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/backend-ci.yml
git commit -m "Add backend CI workflow"
```

---

## Task 11: Angular scaffold + global design-system styles

**Files:**
- Create: `frontend/` (generated Angular workspace)
- Modify: `frontend/src/styles.scss`
- Create: `frontend/src/environments/environment.ts`, `environment.development.ts`

**Interfaces:**
- Produces: a buildable, testable Angular app shell that later tasks add routes/components to. Global CSS classes available app-wide: `.app`, `.sidebar`, `.topbar`, `.card`, `.pill`, `.btn`, `.table-wrap`/`table`, `.overlay`/`.modal`, `.grid.stats`, etc. (ported from `preview.html`). `environment.apiBaseUrl` is the string every later HTTP call is built from.

- [ ] **Step 1: Scaffold the Angular workspace**

From the repo root:

```bash
npx -y @angular/cli@latest new frontend --directory=frontend --routing --style=scss --strict --skip-git --package-manager=npm
cd frontend
npx ng generate environments
```

Note: this assumes a current Angular CLI (17+) generating standalone components by default and providing `ng generate environments` to scaffold `src/environments/environment.ts` + `environment.development.ts` with `angular.json` `fileReplacements` wired automatically. If the installed CLI behaves differently, adapt these steps to whatever it actually generates — the important invariant is: standalone components, a routing module/array, and two environment files with an `apiBaseUrl` field.

- [ ] **Step 2: Set the API base URL in both environment files**

`frontend/src/environments/environment.ts` (used for `ng serve` / development):

```typescript
export const environment = {
  production: false,
  apiBaseUrl: 'https://localhost:5001'
};
```

`frontend/src/environments/environment.development.ts`: same content as above (CLI may generate this as a duplicate of `environment.ts` for dev builds — keep both in sync).

Update the production variant (whichever file `fileReplacements` swaps in for `--configuration production`, typically also named `environment.ts` at build time via replacement — check `angular.json`'s `fileReplacements` entry to find the actual production file) to:

```typescript
export const environment = {
  production: true,
  apiBaseUrl: '/api-placeholder-set-in-task-18'
};
```

(Task 18's README documents overriding this with the real deployed API URL before the production build.)

- [ ] **Step 3: Replace the global stylesheet**

Replace the full contents of `frontend/src/styles.scss` with the design system from the approved prototype (`preview.html` lines 8–232): all `:root` CSS variables, `.app`/`.sidebar`/`.topbar`/`.card`/`.pill`/`.btn`/`.table-wrap`/`table`/`.overlay`/`.modal`/`.grid.stats`/`.list`/`.field`/`.toast`/`.drawer`/`.calendar`/etc. classes and their responsive `@media` rules, verbatim — omitting only the now-unused `.role-select` rule (the manual role switcher is gone per the Global Constraints).

- [ ] **Step 4: Verify the app builds and the default test passes**

Run: `cd frontend && npm run build`
Expected: production build succeeds.

Run: `npm test -- --watch=false --browsers=ChromeHeadless`
Expected: the CLI-generated `app.component.spec.ts` tests pass.

- [ ] **Step 5: Commit**

```bash
git add frontend
git commit -m "Scaffold Angular workspace with the prototype's design system as global styles"
```

---

## Task 12: Core auth (service, interceptor, guard)

**Files:**
- Create: `frontend/src/app/core/auth/auth.service.ts`
- Test: `frontend/src/app/core/auth/auth.service.spec.ts`
- Create: `frontend/src/app/core/auth/auth.interceptor.ts`
- Test: `frontend/src/app/core/auth/auth.interceptor.spec.ts`
- Create: `frontend/src/app/core/auth/auth.guard.ts`
- Modify: `frontend/src/app/app.config.ts`

**Interfaces:**
- Consumes: `environment.apiBaseUrl` (Task 11); backend `POST /api/auth/login` (Task 4).
- Produces: `AuthService` (`login(email, password): Promise<void>`, `logout(): void`, `token: string | null`, `isAuthenticated: Signal<boolean>`, `roles: Signal<string[]>`, `hasRole(role): boolean`, `hasAnyRole(roles): boolean`); `authInterceptor` (`HttpInterceptorFn`); `authGuard` (`CanActivateFn`) — all consumed by Tasks 13–17.

- [ ] **Step 1: Write the failing test for AuthService**

`frontend/src/app/core/auth/auth.service.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';
import { environment } from '../../../environments/environment';

function createFakeJwt(payload: Record<string, unknown>): string {
  const header = btoa(JSON.stringify({ alg: 'none', typ: 'JWT' }));
  const body = btoa(JSON.stringify(payload));
  return `${header}.${body}.signature`;
}

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Router, useValue: { navigateByUrl: jasmine.createSpy('navigateByUrl') } }
      ]
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('stores the token and exposes decoded roles after a successful login', async () => {
    const fakeToken = createFakeJwt({ role: 'Administrator', sub: 'user-1', email: 'a@b.pl', exp: 9999999999 });

    const loginPromise = service.login('a@b.pl', 'secret');
    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/auth/login`);
    req.flush({ token: fakeToken, expiresAtUtc: new Date().toISOString(), roles: ['Administrator'], personId: null });
    await loginPromise;

    expect(service.isAuthenticated()).toBeTrue();
    expect(service.hasRole('Administrator')).toBeTrue();
  });

  it('clears the token on logout', () => {
    localStorage.setItem('dokportal.token', createFakeJwt({ role: 'Administrator', exp: 9999999999 }));
    service = TestBed.inject(AuthService);

    service.logout();

    expect(service.isAuthenticated()).toBeFalse();
    expect(localStorage.getItem('dokportal.token')).toBeNull();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npm test -- --watch=false --browsers=ChromeHeadless`
Expected: FAIL to compile — `AuthService` doesn't exist yet.

- [ ] **Step 3: Implement AuthService**

`frontend/src/app/core/auth/auth.service.ts`:

```typescript
import { Injectable, computed, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface LoginResponse {
  token: string;
  expiresAtUtc: string;
  roles: string[];
  personId: string | null;
}

interface DecodedToken {
  sub?: string;
  email?: string;
  role?: string | string[];
  personId?: string;
  exp?: number;
}

const STORAGE_KEY = 'dokportal.token';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly tokenSignal = signal<string | null>(localStorage.getItem(STORAGE_KEY));

  readonly isAuthenticated = computed(() => this.tokenSignal() !== null);
  readonly roles = computed(() => this.decodeRoles(this.tokenSignal()));

  constructor(private readonly http: HttpClient, private readonly router: Router) {}

  get token(): string | null {
    return this.tokenSignal();
  }

  async login(email: string, password: string): Promise<void> {
    const response = await firstValueFrom(
      this.http.post<LoginResponse>(`${environment.apiBaseUrl}/api/auth/login`, { email, password })
    );
    localStorage.setItem(STORAGE_KEY, response.token);
    this.tokenSignal.set(response.token);
  }

  logout(): void {
    localStorage.removeItem(STORAGE_KEY);
    this.tokenSignal.set(null);
    this.router.navigateByUrl('/login');
  }

  hasRole(role: string): boolean {
    return this.roles().includes(role);
  }

  hasAnyRole(roles: string[]): boolean {
    return roles.some(role => this.hasRole(role));
  }

  private decodeRoles(token: string | null): string[] {
    if (!token) return [];
    const decoded = this.decodeToken(token);
    if (!decoded?.role) return [];
    return Array.isArray(decoded.role) ? decoded.role : [decoded.role];
  }

  private decodeToken(token: string): DecodedToken | null {
    try {
      const payload = token.split('.')[1];
      const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));
      return JSON.parse(json) as DecodedToken;
    } catch {
      return null;
    }
  }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `npm test -- --watch=false --browsers=ChromeHeadless`
Expected: PASS (2 tests).

- [ ] **Step 5: Write the failing test for the interceptor**

`frontend/src/app/core/auth/auth.interceptor.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;

  function setup(token: string | null) {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { token } }
      ]
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  }

  it('adds an Authorization header when a token is present', () => {
    setup('fake-token');
    http.get('/api/test').subscribe();
    const req = httpMock.expectOne('/api/test');
    expect(req.request.headers.get('Authorization')).toBe('Bearer fake-token');
  });

  it('does not add an Authorization header when there is no token', () => {
    setup(null);
    http.get('/api/test').subscribe();
    const req = httpMock.expectOne('/api/test');
    expect(req.request.headers.has('Authorization')).toBeFalse();
  });
});
```

- [ ] **Step 6: Run test to verify it fails**

Run: `npm test -- --watch=false --browsers=ChromeHeadless`
Expected: FAIL to compile — `authInterceptor` doesn't exist yet.

- [ ] **Step 7: Implement the interceptor and guard**

`frontend/src/app/core/auth/auth.interceptor.ts`:

```typescript
import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.token;

  if (!token) {
    return next(req);
  }

  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};
```

`frontend/src/app/core/auth/auth.guard.ts`:

```typescript
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.isAuthenticated() ? true : router.parseUrl('/login');
};
```

- [ ] **Step 8: Register the interceptor**

`frontend/src/app/app.config.ts`:

```typescript
import { ApplicationConfig } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor]))
  ]
};
```

- [ ] **Step 9: Run tests to verify they pass**

Run: `npm test -- --watch=false --browsers=ChromeHeadless`
Expected: PASS (4 tests total: 2 AuthService + 2 interceptor).

- [ ] **Step 10: Commit**

```bash
git add frontend
git commit -m "Add AuthService, JWT interceptor, and route guard"
```

---

## Task 13: Login page

**Files:**
- Create: `frontend/src/app/features/login/login.component.ts`
- Create: `frontend/src/app/features/login/login.component.html`
- Create: `frontend/src/app/features/login/login.component.scss` (empty — styling comes from global `styles.scss`)
- Test: `frontend/src/app/features/login/login.component.spec.ts`
- Create: `frontend/src/app/app.routes.ts`

**Interfaces:**
- Consumes: `AuthService` (Task 12).
- Produces: route `/login`; establishes `frontend/src/app/app.routes.ts`, which Tasks 14–17 extend.

- [ ] **Step 1: Write the failing test**

`frontend/src/app/features/login/login.component.spec.ts`:

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { LoginComponent } from './login.component';
import { AuthService } from '../../core/auth/auth.service';

describe('LoginComponent', () => {
  let fixture: ComponentFixture<LoginComponent>;
  let authServiceSpy: jasmine.SpyObj<AuthService>;

  beforeEach(() => {
    authServiceSpy = jasmine.createSpyObj('AuthService', ['login']);

    TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [provideRouter([]), { provide: AuthService, useValue: authServiceSpy }]
    });

    fixture = TestBed.createComponent(LoginComponent);
  });

  it('navigates to /dashboard after a successful login', async () => {
    authServiceSpy.login.and.resolveTo();
    const router = TestBed.inject(Router);
    spyOn(router, 'navigateByUrl');
    const component = fixture.componentInstance;
    component.email = 'a@b.pl';
    component.password = 'secret';

    await component.submit();

    expect(authServiceSpy.login).toHaveBeenCalledWith('a@b.pl', 'secret');
    expect(router.navigateByUrl).toHaveBeenCalledWith('/dashboard');
  });

  it('shows an error message when login fails', async () => {
    authServiceSpy.login.and.rejectWith(new Error('unauthorized'));
    const component = fixture.componentInstance;

    await component.submit();

    expect(component.errorMessage()).toBe('Nieprawidłowy e-mail lub hasło.');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npm test -- --watch=false --browsers=ChromeHeadless`
Expected: FAIL to compile — `LoginComponent` doesn't exist yet.

- [ ] **Step 3: Implement LoginComponent**

`frontend/src/app/features/login/login.component.ts`:

```typescript
import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {
  email = '';
  password = '';
  readonly errorMessage = signal<string | null>(null);
  readonly isSubmitting = signal(false);

  constructor(private readonly auth: AuthService, private readonly router: Router) {}

  async submit(): Promise<void> {
    this.errorMessage.set(null);
    this.isSubmitting.set(true);
    try {
      await this.auth.login(this.email, this.password);
      await this.router.navigateByUrl('/dashboard');
    } catch {
      this.errorMessage.set('Nieprawidłowy e-mail lub hasło.');
    } finally {
      this.isSubmitting.set(false);
    }
  }
}
```

`frontend/src/app/features/login/login.component.html`:

```html
<div class="content" style="max-width:420px;margin:80px auto">
  <div class="card" style="padding:28px">
    <h2 style="margin:0 0 6px">DOK Portal Light</h2>
    <p class="small-muted" style="margin:0 0 20px">Zaloguj się, aby kontynuować.</p>
    <form (ngSubmit)="submit()">
      <div class="field" style="margin-bottom:12px">
        <label>E-mail</label>
        <input name="email" type="email" [(ngModel)]="email" required />
      </div>
      <div class="field" style="margin-bottom:16px">
        <label>Hasło</label>
        <input name="password" type="password" [(ngModel)]="password" required />
      </div>
      @if (errorMessage()) {
        <div class="note" style="border-color:#f2b8bf;background:#fff0f2;margin-bottom:14px">{{ errorMessage() }}</div>
      }
      <button class="btn primary" type="submit" [disabled]="isSubmitting()" style="width:100%;justify-content:center">
        {{ isSubmitting() ? 'Logowanie…' : 'Zaloguj się' }}
      </button>
    </form>
  </div>
</div>
```

`frontend/src/app/features/login/login.component.scss`: create as an empty file (referenced by `styleUrl`; all visuals come from the global stylesheet).

- [ ] **Step 4: Wire the route**

`frontend/src/app/app.routes.ts`:

```typescript
import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./features/login/login.component').then(m => m.LoginComponent) },
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: '**', redirectTo: 'login' }
];
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `npm test -- --watch=false --browsers=ChromeHeadless`
Expected: PASS (2 new tests).

- [ ] **Step 6: Commit**

```bash
git add frontend
git commit -m "Add login page and initial routing"
```

---

## Task 14: Layout shell (sidebar, topbar, role-filtered navigation)

**Files:**
- Create: `frontend/src/app/layout/nav-items.ts`
- Create: `frontend/src/app/layout/shell/shell.component.ts`
- Create: `frontend/src/app/layout/shell/shell.component.html`
- Create: `frontend/src/app/layout/shell/shell.component.scss` (empty)
- Test: `frontend/src/app/layout/shell/shell.component.spec.ts`
- Modify: `frontend/src/app/app.routes.ts`

**Interfaces:**
- Consumes: `AuthService` (Task 12), `authGuard` (Task 12).
- Produces: `ShellComponent` wrapping `<router-outlet>` with a role-filtered sidebar; the `''` parent route (guarded, empty `children: []` for now — Tasks 15–17 each add one child route).

- [ ] **Step 1: Write the failing test**

`frontend/src/app/layout/shell/shell.component.spec.ts`:

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ShellComponent } from './shell.component';
import { AuthService } from '../../core/auth/auth.service';

describe('ShellComponent', () => {
  let fixture: ComponentFixture<ShellComponent>;

  function setup(roles: string[]) {
    TestBed.configureTestingModule({
      imports: [ShellComponent],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            roles: () => roles,
            hasAnyRole: (required: string[]) => required.some(r => roles.includes(r)),
            logout: jasmine.createSpy('logout')
          }
        }
      ]
    });
    fixture = TestBed.createComponent(ShellComponent);
    fixture.detectChanges();
  }

  it('hides the admin nav item for a user without the Administrator role', () => {
    setup(['KatechistaProwadzacy']);
    expect((fixture.nativeElement.textContent as string)).not.toContain('Użytkownicy i role');
  });

  it('shows the admin nav item for an Administrator', () => {
    setup(['Administrator']);
    expect((fixture.nativeElement.textContent as string)).toContain('Użytkownicy i role');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npm test -- --watch=false --browsers=ChromeHeadless`
Expected: FAIL to compile — `ShellComponent` doesn't exist yet.

- [ ] **Step 3: Implement**

`frontend/src/app/layout/nav-items.ts`:

```typescript
export interface NavItem {
  label: string;
  icon: string;
  path: string;
  roles: string[];
}

export const NAV_ITEMS: NavItem[] = [
  { label: 'Dashboard', icon: '◫', path: '/dashboard', roles: [] },
  { label: 'Baza osób', icon: '◎', path: '/people', roles: [] },
  { label: 'Użytkownicy i role', icon: '⚙', path: '/admin/users', roles: ['Administrator'] }
];
```

`frontend/src/app/layout/shell/shell.component.ts`:

```typescript
import { Component, computed } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { NAV_ITEMS } from '../nav-items';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss'
})
export class ShellComponent {
  readonly visibleNavItems = computed(() =>
    NAV_ITEMS.filter(item => item.roles.length === 0 || this.auth.hasAnyRole(item.roles))
  );

  constructor(readonly auth: AuthService) {}

  logout(): void {
    this.auth.logout();
  }
}
```

`frontend/src/app/layout/shell/shell.component.html`:

```html
<div class="app">
  <aside class="sidebar">
    <div class="brand">
      <div class="logo">SK</div>
      <div><b>DOK Portal Light</b><span>Diecezja</span></div>
    </div>
    <div class="nav-title">Menu</div>
    @for (item of visibleNavItems(); track item.path) {
      <a class="nav-item" [routerLink]="item.path" routerLinkActive="active">
        <span class="nav-ico">{{ item.icon }}</span> {{ item.label }}
      </a>
    }
  </aside>
  <main class="main">
    <header class="topbar">
      <div class="top-left"><div class="page-title">DOK Portal Light</div></div>
      <div class="top-actions">
        @for (role of auth.roles(); track role) {
          <span class="pill blue">{{ role }}</span>
        }
        <button class="btn ghost small" (click)="logout()">Wyloguj</button>
      </div>
    </header>
    <div class="content">
      <router-outlet></router-outlet>
    </div>
  </main>
</div>
```

`frontend/src/app/layout/shell/shell.component.scss`: create as an empty file.

- [ ] **Step 4: Wire the guarded parent route**

`frontend/src/app/app.routes.ts`:

```typescript
import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { ShellComponent } from './layout/shell/shell.component';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./features/login/login.component').then(m => m.LoginComponent) },
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    children: []
  },
  { path: '**', redirectTo: 'login' }
];
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `npm test -- --watch=false --browsers=ChromeHeadless`
Expected: PASS (2 new tests).

- [ ] **Step 6: Commit**

```bash
git add frontend
git commit -m "Add layout shell with role-filtered navigation"
```

---

## Task 15: Dashboard page

**Files:**
- Create: `frontend/src/app/features/dashboard/dashboard.model.ts`
- Create: `frontend/src/app/features/dashboard/dashboard.service.ts`
- Test: `frontend/src/app/features/dashboard/dashboard.service.spec.ts`
- Create: `frontend/src/app/features/dashboard/dashboard.component.ts`
- Create: `frontend/src/app/features/dashboard/dashboard.component.html`
- Create: `frontend/src/app/features/dashboard/dashboard.component.scss` (empty)
- Test: `frontend/src/app/features/dashboard/dashboard.component.spec.ts`
- Modify: `frontend/src/app/app.routes.ts`

**Interfaces:**
- Consumes: backend `GET /api/dashboard/summary` (Task 9).
- Produces: route `/dashboard`, the shell's default redirect target.

- [ ] **Step 1: Write the failing tests**

`frontend/src/app/features/dashboard/dashboard.model.ts`:

```typescript
export interface DashboardSummary {
  peopleCount: number;
  parishCount: number;
}
```

`frontend/src/app/features/dashboard/dashboard.service.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DashboardService } from './dashboard.service';
import { environment } from '../../../environments/environment';

describe('DashboardService', () => {
  it('requests the summary from the API', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(DashboardService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.getSummary().subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/dashboard/summary`);
    req.flush({ peopleCount: 1, parishCount: 1 });
    httpMock.verify();
  });
});
```

`frontend/src/app/features/dashboard/dashboard.component.spec.ts`:

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DashboardComponent } from './dashboard.component';
import { environment } from '../../../environments/environment';

describe('DashboardComponent', () => {
  let fixture: ComponentFixture<DashboardComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(DashboardComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('renders the people count returned by the API', () => {
    fixture.detectChanges();
    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/dashboard/summary`);
    req.flush({ peopleCount: 12, parishCount: 3 });
    fixture.detectChanges();

    expect((fixture.nativeElement.textContent as string)).toContain('12');
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd frontend && npm test -- --watch=false --browsers=ChromeHeadless`
Expected: FAIL to compile — `DashboardService`, `DashboardComponent` don't exist yet.

- [ ] **Step 3: Implement**

`frontend/src/app/features/dashboard/dashboard.service.ts`:

```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { DashboardSummary } from './dashboard.model';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  constructor(private readonly http: HttpClient) {}

  getSummary() {
    return this.http.get<DashboardSummary>(`${environment.apiBaseUrl}/api/dashboard/summary`);
  }
}
```

`frontend/src/app/features/dashboard/dashboard.component.ts`:

```typescript
import { Component, OnInit, signal } from '@angular/core';
import { DashboardService } from './dashboard.service';
import { DashboardSummary } from './dashboard.model';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit {
  readonly summary = signal<DashboardSummary | null>(null);

  constructor(private readonly dashboardService: DashboardService) {}

  ngOnInit(): void {
    this.dashboardService.getSummary().subscribe(summary => this.summary.set(summary));
  }
}
```

`frontend/src/app/features/dashboard/dashboard.component.html`:

```html
<div class="hero">
  <div>
    <h1>Dzień dobry 👋</h1>
    <p>Wspólne centrum obsługi Szkoły Katechistów św. Pawła i Diecezjalnego Ośrodka Katechumenalnego.</p>
  </div>
</div>

<div class="grid stats">
  <div class="card stat">
    <div class="stat-label">Osoby w bazie</div>
    <div class="stat-value">{{ summary()?.peopleCount ?? '—' }}</div>
  </div>
  <div class="card stat">
    <div class="stat-label">Parafie</div>
    <div class="stat-value">{{ summary()?.parishCount ?? '—' }}</div>
  </div>
</div>

<div class="card">
  <div class="card-body empty">
    Statystyki SKŚP, DOK oraz sprawy wymagające uwagi pojawią się po wdrożeniu kolejnych faz.
  </div>
</div>
```

`frontend/src/app/features/dashboard/dashboard.component.scss`: create as an empty file.

- [ ] **Step 4: Wire the route and default redirect**

In `frontend/src/app/app.routes.ts`, change the shell's `children: []` to:

```typescript
children: [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  {
    path: 'dashboard',
    loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent)
  }
]
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `npm test -- --watch=false --browsers=ChromeHeadless`
Expected: PASS (2 new tests).

- [ ] **Step 6: Commit**

```bash
git add frontend
git commit -m "Add Dashboard page wired to the real summary endpoint"
```

---

## Task 16: People feature (list, search, add/edit modal)

**Files:**
- Create: `frontend/src/app/features/people/person.model.ts`
- Create: `frontend/src/app/features/people/people.service.ts`
- Test: `frontend/src/app/features/people/people.service.spec.ts`
- Create: `frontend/src/app/features/people/person-form.component.ts`
- Create: `frontend/src/app/features/people/person-form.component.html`
- Create: `frontend/src/app/features/people/person-form.component.scss` (empty)
- Create: `frontend/src/app/features/people/people-list.component.ts`
- Create: `frontend/src/app/features/people/people-list.component.html`
- Create: `frontend/src/app/features/people/people-list.component.scss` (empty)
- Test: `frontend/src/app/features/people/people-list.component.spec.ts`
- Modify: `frontend/src/app/app.routes.ts`

**Interfaces:**
- Consumes: backend `GET/POST /api/people`, `PUT /api/people/{id}` (Task 6).
- Produces: route `/people`.

- [ ] **Step 1: Write the failing tests**

`frontend/src/app/features/people/person.model.ts`:

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
}
```

`frontend/src/app/features/people/people.service.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PeopleService } from './people.service';
import { environment } from '../../../environments/environment';

describe('PeopleService', () => {
  it('sends the query as a request parameter when searching', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(PeopleService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.search('Kowalski').subscribe();

    const req = httpMock.expectOne(
      r => r.url === `${environment.apiBaseUrl}/api/people` && r.params.get('query') === 'Kowalski'
    );
    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 20 });
    httpMock.verify();
  });
});
```

`frontend/src/app/features/people/people-list.component.spec.ts`:

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PeopleListComponent } from './people-list.component';
import { environment } from '../../../environments/environment';

describe('PeopleListComponent', () => {
  let fixture: ComponentFixture<PeopleListComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [PeopleListComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(PeopleListComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('renders people returned from the search endpoint', () => {
    fixture.detectChanges();
    const req = httpMock.expectOne(r => r.url === `${environment.apiBaseUrl}/api/people`);
    req.flush({
      items: [{ id: '1', firstName: 'Anna', lastName: 'Maj', fullName: 'Anna Maj', email: null, phone: null, birthDate: null, parishId: null, parishName: null, notes: null }],
      totalCount: 1,
      page: 1,
      pageSize: 20
    });
    fixture.detectChanges();

    expect((fixture.nativeElement.textContent as string)).toContain('Anna Maj');
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd frontend && npm test -- --watch=false --browsers=ChromeHeadless`
Expected: FAIL to compile — `PeopleService`, `PeopleListComponent` don't exist yet.

- [ ] **Step 3: Implement PeopleService**

`frontend/src/app/features/people/people.service.ts`:

```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { PagedResult, Person, PersonFormValue } from './person.model';

@Injectable({ providedIn: 'root' })
export class PeopleService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/people`;

  constructor(private readonly http: HttpClient) {}

  search(query: string, page = 1, pageSize = 20) {
    return this.http.get<PagedResult<Person>>(this.baseUrl, { params: { query, page, pageSize } });
  }

  getById(id: string) {
    return this.http.get<Person>(`${this.baseUrl}/${id}`);
  }

  create(value: PersonFormValue) {
    return this.http.post<Person>(this.baseUrl, value);
  }

  update(id: string, value: PersonFormValue) {
    return this.http.put<Person>(`${this.baseUrl}/${id}`, value);
  }
}
```

- [ ] **Step 4: Implement the add/edit modal**

`frontend/src/app/features/people/person-form.component.ts`:

```typescript
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PersonFormValue } from './person.model';

@Component({
  selector: 'app-person-form',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './person-form.component.html',
  styleUrl: './person-form.component.scss'
})
export class PersonFormComponent {
  @Input() open = false;
  @Input() value: PersonFormValue = { firstName: '', lastName: '' };
  @Output() save = new EventEmitter<PersonFormValue>();
  @Output() cancel = new EventEmitter<void>();

  submit(): void {
    this.save.emit(this.value);
  }
}
```

`frontend/src/app/features/people/person-form.component.html`:

```html
@if (open) {
  <div class="overlay show">
    <div class="modal">
      <div class="modal-head">
        <h3>Dodaj / edytuj osobę</h3>
        <button class="close" (click)="cancel.emit()">×</button>
      </div>
      <div class="modal-body">
        <div class="form-grid">
          <div class="field"><label>Imię</label><input [(ngModel)]="value.firstName" name="firstName" /></div>
          <div class="field"><label>Nazwisko</label><input [(ngModel)]="value.lastName" name="lastName" /></div>
          <div class="field"><label>E-mail</label><input [(ngModel)]="value.email" name="email" /></div>
          <div class="field"><label>Telefon</label><input [(ngModel)]="value.phone" name="phone" /></div>
          <div class="field full"><label>Uwagi</label><textarea [(ngModel)]="value.notes" name="notes"></textarea></div>
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

`frontend/src/app/features/people/person-form.component.scss`: create as an empty file.

- [ ] **Step 5: Implement the list page**

`frontend/src/app/features/people/people-list.component.ts`:

```typescript
import { Component, OnInit, signal } from '@angular/core';
import { PeopleService } from './people.service';
import { Person, PersonFormValue } from './person.model';
import { PersonFormComponent } from './person-form.component';

@Component({
  selector: 'app-people-list',
  standalone: true,
  imports: [PersonFormComponent],
  templateUrl: './people-list.component.html',
  styleUrl: './people-list.component.scss'
})
export class PeopleListComponent implements OnInit {
  readonly people = signal<Person[]>([]);
  readonly query = signal('');
  readonly isFormOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  formValue: PersonFormValue = { firstName: '', lastName: '' };

  constructor(private readonly peopleService: PeopleService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.peopleService.search(this.query()).subscribe(result => this.people.set(result.items));
  }

  onSearch(value: string): void {
    this.query.set(value);
    this.load();
  }

  openAddForm(): void {
    this.editingId.set(null);
    this.formValue = { firstName: '', lastName: '' };
    this.isFormOpen.set(true);
  }

  openEditForm(person: Person): void {
    this.editingId.set(person.id);
    this.formValue = {
      firstName: person.firstName,
      lastName: person.lastName,
      email: person.email ?? undefined,
      phone: person.phone ?? undefined,
      notes: person.notes ?? undefined
    };
    this.isFormOpen.set(true);
  }

  onSave(value: PersonFormValue): void {
    const id = this.editingId();
    const request$ = id ? this.peopleService.update(id, value) : this.peopleService.create(value);
    request$.subscribe(() => {
      this.isFormOpen.set(false);
      this.load();
    });
  }

  onCancel(): void {
    this.isFormOpen.set(false);
  }
}
```

`frontend/src/app/features/people/people-list.component.html`:

```html
<div class="page-heading">
  <div><h2>Baza osób</h2><p>Jeden profil osoby, wiele ról i powiązań.</p></div>
  <button class="btn primary" (click)="openAddForm()">＋ Dodaj osobę</button>
</div>

<div class="card">
  <div class="card-head">
    <div class="search">
      <input placeholder="Szukaj…" [value]="query()" (input)="onSearch($any($event.target).value)" />
    </div>
  </div>
  <div class="table-wrap">
    <table>
      <thead><tr><th>Osoba</th><th>Kontakt</th><th>Parafia</th><th></th></tr></thead>
      <tbody>
        @for (person of people(); track person.id) {
          <tr>
            <td><b>{{ person.fullName }}</b></td>
            <td>{{ person.email }}<br /><span class="small-muted">{{ person.phone }}</span></td>
            <td>{{ person.parishName }}</td>
            <td><span class="link" (click)="openEditForm(person)">Edytuj</span></td>
          </tr>
        } @empty {
          <tr><td colspan="4" class="empty">Brak wyników.</td></tr>
        }
      </tbody>
    </table>
  </div>
</div>

<app-person-form
  [open]="isFormOpen()"
  [value]="formValue"
  (save)="onSave($event)"
  (cancel)="onCancel()">
</app-person-form>
```

`frontend/src/app/features/people/people-list.component.scss`: create as an empty file.

- [ ] **Step 6: Wire the route**

In `frontend/src/app/app.routes.ts`, add to the shell's `children` array:

```typescript
{ path: 'people', loadComponent: () => import('./features/people/people-list.component').then(m => m.PeopleListComponent) }
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `npm test -- --watch=false --browsers=ChromeHeadless`
Expected: PASS (2 new tests).

- [ ] **Step 8: Commit**

```bash
git add frontend
git commit -m "Add People feature: search, list, add/edit modal"
```

---

## Task 17: Admin Users feature + frontend CI workflow

**Files:**
- Create: `frontend/src/app/features/admin-users/user.model.ts`
- Create: `frontend/src/app/features/admin-users/users.service.ts`
- Test: `frontend/src/app/features/admin-users/users.service.spec.ts`
- Create: `frontend/src/app/features/admin-users/users-list.component.ts`
- Create: `frontend/src/app/features/admin-users/users-list.component.html`
- Create: `frontend/src/app/features/admin-users/users-list.component.scss` (empty)
- Test: `frontend/src/app/features/admin-users/users-list.component.spec.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Create: `.github/workflows/frontend-ci.yml`

**Interfaces:**
- Consumes: backend `GET/POST /api/users`, `PUT /api/users/{id}/roles` (Task 8).
- Produces: route `/admin/users`; a GitHub Actions job that must be green before Task 18's deploy job is meaningful.

- [ ] **Step 1: Write the failing tests**

`frontend/src/app/features/admin-users/user.model.ts`:

```typescript
export const ALL_ROLES = [
  'Administrator', 'Biskup', 'DyrektorSKSP', 'DyrektorDOK', 'Superwizor', 'KatechistaProwadzacy'
] as const;

export interface AppUserAccount {
  id: string;
  email: string;
  personId: string | null;
  roles: string[];
}

export interface CreateUserValue {
  email: string;
  password: string;
  roles: string[];
}
```

`frontend/src/app/features/admin-users/users.service.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { UsersService } from './users.service';
import { environment } from '../../../environments/environment';

describe('UsersService', () => {
  it('requests the user list from the API', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(UsersService);
    const httpMock = TestBed.inject(HttpTestingController);

    service.list().subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/users`);
    req.flush([]);
    httpMock.verify();
  });
});
```

`frontend/src/app/features/admin-users/users-list.component.spec.ts`:

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { UsersListComponent } from './users-list.component';
import { environment } from '../../../environments/environment';

describe('UsersListComponent', () => {
  let fixture: ComponentFixture<UsersListComponent>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [UsersListComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    fixture = TestBed.createComponent(UsersListComponent);
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('renders emails returned from the API', () => {
    fixture.detectChanges();
    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/users`);
    req.flush([{ id: '1', email: 'admin@dokportal.local', personId: null, roles: ['Administrator'] }]);
    fixture.detectChanges();

    expect((fixture.nativeElement.textContent as string)).toContain('admin@dokportal.local');
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd frontend && npm test -- --watch=false --browsers=ChromeHeadless`
Expected: FAIL to compile — `UsersService`, `UsersListComponent` don't exist yet.

- [ ] **Step 3: Implement UsersService**

`frontend/src/app/features/admin-users/users.service.ts`:

```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AppUserAccount, CreateUserValue } from './user.model';

@Injectable({ providedIn: 'root' })
export class UsersService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/users`;

  constructor(private readonly http: HttpClient) {}

  list() {
    return this.http.get<AppUserAccount[]>(this.baseUrl);
  }

  create(value: CreateUserValue) {
    return this.http.post<AppUserAccount>(this.baseUrl, value);
  }

  assignRoles(id: string, roles: string[]) {
    return this.http.put<AppUserAccount>(`${this.baseUrl}/${id}/roles`, { roles });
  }
}
```

- [ ] **Step 4: Implement UsersListComponent**

`frontend/src/app/features/admin-users/users-list.component.ts`:

```typescript
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { UsersService } from './users.service';
import { ALL_ROLES, AppUserAccount, CreateUserValue } from './user.model';

@Component({
  selector: 'app-users-list',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './users-list.component.html',
  styleUrl: './users-list.component.scss'
})
export class UsersListComponent implements OnInit {
  readonly users = signal<AppUserAccount[]>([]);
  readonly allRoles = ALL_ROLES;
  newUser: CreateUserValue = { email: '', password: '', roles: [] };

  constructor(private readonly usersService: UsersService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.usersService.list().subscribe(users => this.users.set(users));
  }

  createUser(): void {
    this.usersService.create(this.newUser).subscribe(() => {
      this.newUser = { email: '', password: '', roles: [] };
      this.load();
    });
  }

  toggleRole(user: AppUserAccount, role: string, checked: boolean): void {
    const roles = checked ? [...user.roles, role] : user.roles.filter(r => r !== role);
    this.usersService.assignRoles(user.id, roles).subscribe(() => this.load());
  }
}
```

`frontend/src/app/features/admin-users/users-list.component.html`:

```html
<div class="page-heading">
  <div><h2>Użytkownicy i role</h2><p>Zarządzanie kontami i przypisaniem ról RBAC.</p></div>
</div>

<div class="card" style="margin-bottom:16px">
  <div class="card-body form-grid">
    <div class="field"><label>E-mail</label><input [(ngModel)]="newUser.email" name="email" /></div>
    <div class="field"><label>Hasło</label><input type="password" [(ngModel)]="newUser.password" name="password" /></div>
    <div class="field full">
      <button class="btn primary" (click)="createUser()">＋ Utwórz konto</button>
    </div>
  </div>
</div>

<div class="card">
  <div class="table-wrap">
    <table>
      <thead><tr><th>E-mail</th>@for (role of allRoles; track role) {<th>{{ role }}</th>}</tr></thead>
      <tbody>
        @for (user of users(); track user.id) {
          <tr>
            <td>{{ user.email }}</td>
            @for (role of allRoles; track role) {
              <td>
                <input type="checkbox" [checked]="user.roles.includes(role)"
                       (change)="toggleRole(user, role, $any($event.target).checked)" />
              </td>
            }
          </tr>
        }
      </tbody>
    </table>
  </div>
</div>
```

`frontend/src/app/features/admin-users/users-list.component.scss`: create as an empty file.

- [ ] **Step 5: Wire the route**

In `frontend/src/app/app.routes.ts`, add to the shell's `children` array:

```typescript
{ path: 'admin/users', loadComponent: () => import('./features/admin-users/users-list.component').then(m => m.UsersListComponent) }
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `npm test -- --watch=false --browsers=ChromeHeadless`
Expected: PASS (2 new tests). Then run the full frontend suite: `npm test -- --watch=false --browsers=ChromeHeadless` (no filter) to confirm nothing else broke.

- [ ] **Step 7: Add the frontend CI workflow**

`.github/workflows/frontend-ci.yml`:

```yaml
name: Frontend CI

on:
  push:
    branches: [main]
    paths: ["frontend/**", ".github/workflows/frontend-ci.yml"]
  pull_request:
    paths: ["frontend/**", ".github/workflows/frontend-ci.yml"]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: frontend
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: "20"
          cache: "npm"
          cache-dependency-path: frontend/package-lock.json
      - run: npm ci
      - run: npm run build
      - run: npm test -- --watch=false --browsers=ChromeHeadless
```

Verify by running the same commands locally: `cd frontend && npm ci && npm run build && npm test -- --watch=false --browsers=ChromeHeadless`.

- [ ] **Step 8: Commit**

```bash
git add frontend .github/workflows/frontend-ci.yml
git commit -m "Add Admin Users feature and frontend CI workflow"
```

---

## Task 18: Deploy workflow + README

**Files:**
- Create: `.github/workflows/deploy.yml`
- Create: `README.md` (repo root)

**Interfaces:**
- Consumes: `backend/src/DokPortal.Api` (Task 3+), `frontend` (Task 11+).
- Produces: a deploy pipeline that activates once the user supplies Azure resources + GitHub secrets; documentation for local setup and required secrets.

- [ ] **Step 1: Write the deploy workflow**

`.github/workflows/deploy.yml`:

```yaml
name: Deploy

on:
  push:
    branches: [main]

jobs:
  deploy-backend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "8.0.x"
      - run: dotnet publish backend/src/DokPortal.Api/DokPortal.Api.csproj -c Release -o publish/api
      - uses: azure/webapps-deploy@v3
        with:
          app-name: ${{ vars.AZURE_WEBAPP_NAME }}
          publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE }}
          package: publish/api

  deploy-frontend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: "20"
          cache: "npm"
          cache-dependency-path: frontend/package-lock.json
      - run: npm ci
        working-directory: frontend
      - run: npm run build -- --configuration production
        working-directory: frontend
      - uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}
          repo_token: ${{ secrets.GITHUB_TOKEN }}
          action: upload
          app_location: frontend/dist/frontend/browser
          skip_app_build: true
```

Note: `app_location`'s path (`frontend/dist/frontend/browser`) assumes the Angular CLI's current default output layout (`dist/<project-name>/browser`). After Task 11 scaffolds the app, check the actual `outputPath` in `frontend/angular.json` and adjust this path if it differs.

- [ ] **Step 2: Write the README**

`README.md` (repo root):

```markdown
# DOK Portal Light

CRM dla Szkoły Katechistów św. Pawła (SKŚP) i Diecezjalnego Ośrodka
Katechumenalnego (DOK) — wspólna baza osób, RBAC, oraz moduły
specyficzne dla obu instytucji. Zobacz specyfikację w
`docs/superpowers/specs/2026-09-22-foundation-design.md`.

## Wymagania

- .NET 8 SDK
- Node.js 20+ i npm
- SQL Server LocalDB (dev na Windows) lub dostęp do instancji Azure SQL
- `dotnet-ef` (`dotnet tool install --global dotnet-ef`)

## Backend — uruchomienie lokalne

```bash
cd backend
dotnet restore
dotnet run --project src/DokPortal.Api
```

Migracje bazy danych i dane startowe (role RBAC, konto administratora,
przykładowe parafie) stosują się automatycznie przy starcie w trybie
Development. Domyślne dane logowania administratora w
`appsettings.Development.json` (`SeedAdmin` section) — zmień hasło po
pierwszym uruchomieniu w środowisku innym niż lokalne.

Testy: `dotnet test backend/DokPortal.sln`

## Frontend — uruchomienie lokalne

```bash
cd frontend
npm install
npm start
```

Aplikacja domyślnie łączy się z `https://localhost:5001` (patrz
`src/environments/environment.ts`) — dopasuj do portu, na którym
faktycznie działa lokalne API.

Testy: `npm test -- --watch=false --browsers=ChromeHeadless`

## Wdrożenie (GitHub Actions → Azure)

Ten projekt wdraża się automatycznie po pushu do `main`
(`.github/workflows/deploy.yml`), pod warunkiem że wcześniej:

1. Utworzysz w Azure: **App Service** (backend, .NET 8) oraz
   **Static Web App** (frontend) i, docelowo, **Azure SQL Database**.
2. Dodasz w ustawieniach repozytorium GitHub:
   - **Secrets**: `AZURE_WEBAPP_PUBLISH_PROFILE`,
     `AZURE_STATIC_WEB_APPS_API_TOKEN`.
   - **Variables**: `AZURE_WEBAPP_NAME` (nazwa App Service).
3. Skonfigurujesz w ustawieniach App Service (Configuration →
   Application settings) rzeczywiste wartości produkcyjne:
   `ConnectionStrings__Default`, `Jwt__Key`, `Jwt__Issuer`,
   `Jwt__Audience`, `SeedAdmin__Email`, `SeedAdmin__Password`,
   `Cors__AllowedOrigins__0` (adres URL Static Web App).
4. Zaktualizujesz `frontend/src/environments/environment.ts` (wariant
   produkcyjny) na rzeczywisty adres URL wdrożonego API przed buildem
   produkcyjnym.

Bez tych kroków `backend-ci.yml` i `frontend-ci.yml` (build + testy)
nadal działają na każdy push/PR — tylko `deploy.yml` pozostanie
nieaktywny / zakończy się błędem do czasu skonfigurowania sekretów.
```

- [ ] **Step 3: Verify the backend publish command succeeds locally**

Run: `dotnet publish backend/src/DokPortal.Api/DokPortal.Api.csproj -c Release -o /tmp/dokportal-publish-check`
Expected: publish succeeds, producing a deployable output folder.

- [ ] **Step 4: Verify the frontend production build succeeds locally**

Run: `cd frontend && npm run build -- --configuration production`
Expected: build succeeds; note the actual output folder path and confirm it matches `app_location` in `deploy.yml` (adjust the workflow if it differs, per Step 1's note).

- [ ] **Step 5: Commit**

```bash
git add .github/workflows/deploy.yml README.md
git commit -m "Add deploy workflow and setup/deployment README"
```

---

## Self-Review Notes

- **Spec coverage:** repo/CI (Tasks 1, 10, 17, 18), Azure SQL + EF Core migrations (Tasks 3, 5), RBAC with multi-role Identity users (Tasks 3–5, 8), shared Person registry (Tasks 2, 6), Angular shell mirroring the prototype minus the manual role switcher (Tasks 11, 14), Dashboard + Baza osób with real data only (Tasks 15, 16), Users admin panel (Task 8, 17), deploy workflow + required-secrets documentation (Task 18) — all Phase 1 goals from the spec map to a task.
- **Type consistency verified:** `PersonDto`/`CreatePersonRequest`/`UpdatePersonRequest` (Task 6) match the JSON shape asserted in `PeopleControllerTests` and the Angular `Person`/`PersonFormValue` models (Task 16); `UserDto`/`CreateUserRequest`/`AssignRolesRequest` (Task 8) match `UsersControllerTests` and the Angular `AppUserAccount`/`CreateUserValue` models (Task 17); the JWT `"role"` claim type is consistent across `JwtTokenGenerator` (Task 4), `TokenValidationParameters.RoleClaimType` (Task 4), and the Angular `AuthService` decoder (Task 12).
- **No placeholders:** every step above contains complete, runnable code — no "TBD" or "add appropriate handling" steps remain.
