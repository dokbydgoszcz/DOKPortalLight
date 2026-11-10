using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DokPortal.Api.ErrorHandling;
using DokPortal.Application.Auth;
using DokPortal.Application.Users;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Poprawki z przeglądu bezpieczeństwa (2026-11): konta administratorów, hasło tymczasowe, nagłówki, komunikaty błędów.</summary>
public class SecurityHardeningTests : IntegrationTestBase
{
    public SecurityHardeningTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private static string Unique() => Guid.NewGuid().ToString("N")[..8];

    private async Task<(HttpClient Admin, HttpClient Manager, UserDto AdminAccount)> AdminAndUserManagerAsync()
    {
        var adminEmail = $"admin-{Unique()}@example.org";
        var admin = await CreateAuthenticatedClientAsync(adminEmail, "Sekret123!", "Administrator");
        var role = "Kadry " + new string(Unique().Where(char.IsLetter).DefaultIfEmpty('x').ToArray());
        (await admin.PostAsJsonAsync("/api/permissions/roles", new { Name = role })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/permissions/roles/{Uri.EscapeDataString(role)}", new { Permissions = new[] { "Users.Manage" } })).EnsureSuccessStatusCode();
        var manager = await CreateAuthenticatedClientAsync($"kadry-{Unique()}@example.org", "Sekret123!", role);
        var users = await admin.GetFromJsonAsync<List<UserDto>>("/api/users");
        return (admin, manager, users!.Single(u => u.Email == adminEmail));
    }

    [Fact]
    public async Task SomeoneWithUsersManage_CannotTakeOverAnAdministratorAccount()
    {
        var (admin, manager, adminAccount) = await AdminAndUserManagerAsync();
        var id = adminAccount.Id;
        var personId = await SeedPersonAsync(admin, "Jan", "Przejęty" + Unique());

        var attempts = new[]
        {
            await manager.PutAsJsonAsync($"/api/users/{id}/reset-password", new { NewPassword = "Przejete-123!" }),
            await manager.DeleteAsync($"/api/users/{id}/mfa"),
            await manager.PutAsJsonAsync($"/api/users/{id}/login", new { Email = $"zlodziej-{Unique()}@example.org" }),
            await manager.PutAsJsonAsync($"/api/users/{id}/person", new { PersonId = personId }),
            await manager.PutAsJsonAsync($"/api/users/{id}/active", new { IsActive = false }),
            await manager.PutAsJsonAsync($"/api/users/{id}/roles", new { Roles = new[] { "Biskup" } })
        };
        foreach (var attempt in attempts)
        {
            Assert.Equal(HttpStatusCode.BadRequest, attempt.StatusCode);
            Assert.Contains("administrator", await attempt.Content.ReadAsStringAsync());
        }

        // Stare hasło administratora dalej działa, a zwykłe konta kadry nadal obsługują.
        Assert.Equal(HttpStatusCode.OK, (await Client.PostAsJsonAsync("/api/auth/login", new { adminAccount.Email, Password = "Sekret123!" })).StatusCode);
        var regular = await (await manager.PostAsJsonAsync("/api/users", new { Email = $"zwykly-{Unique()}@example.org", Password = "Sekret123!", Roles = new[] { "Biskup" } }))
            .Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal(HttpStatusCode.NoContent, (await manager.PutAsJsonAsync($"/api/users/{regular!.Id}/reset-password", new { NewPassword = "Nowe-Haslo-123!" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.DeleteAsync($"/api/users/{regular.Id}/mfa")).StatusCode);

        // Administrator może wszystko na koncie innego administratora.
        var second = await (await admin.PostAsJsonAsync("/api/users", new { Email = $"admin2-{Unique()}@example.org", Password = "Sekret123!", Roles = new[] { "Administrator" } }))
            .Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/users/{second!.Id}/reset-password", new { NewPassword = "Nowe-Haslo-123!" })).StatusCode);
    }

    [Fact]
    public async Task ChangingTheLogin_EndsTheOldSessions()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Unique()}@example.org", "Sekret123!", "Administrator");
        var email = $"biskup-{Unique()}@example.org";
        var bishop = await CreateAuthenticatedClientAsync(email, "Sekret123!", "Biskup");
        var account = (await admin.GetFromJsonAsync<List<UserDto>>("/api/users"))!.Single(u => u.Email == email);
        Assert.Equal(HttpStatusCode.OK, (await bishop.GetAsync("/api/dashboard/summary")).StatusCode);

        (await admin.PutAsJsonAsync($"/api/users/{account.Id}/login", new { Email = $"nowy-{Unique()}@example.org" })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await bishop.GetAsync("/api/dashboard/summary")).StatusCode);
    }

    [Fact]
    public async Task ATemporaryPassword_OpensOnlyThePasswordChange()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Unique()}@example.org", "Sekret123!", "Administrator");
        var email = $"nowy-{Unique()}@example.org";
        (await admin.PostAsJsonAsync("/api/users", new { Email = email, Password = "Tymczasowe-123!", Roles = new[] { "Biskup" } })).EnsureSuccessStatusCode();

        var login = await (await Client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = "Tymczasowe-123!" })).Content.ReadFromJsonAsync<LoginResponse>();
        Assert.True(login!.MustChangePassword);
        var user = Factory.CreateClient();
        user.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);

        foreach (var path in new[] { "/api/dashboard/summary", "/api/people", "/api/parishes" })
        {
            var blocked = await user.GetAsync(path);
            Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
            Assert.Contains("własne hasło", await blocked.Content.ReadAsStringAsync());
        }
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/auth/mfa")).StatusCode);

        var changed = await user.PostAsJsonAsync("/api/auth/change-password", new { CurrentPassword = "Tymczasowe-123!", NewPassword = "Moje-Wlasne-456!" });
        var session = await changed.Content.ReadFromJsonAsync<LoginResponse>();
        user.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.Token);
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/dashboard/summary")).StatusCode);
    }

    [Fact]
    public async Task EveryResponse_CarriesSecurityHeaders_AndApiDataIsNotCached()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new { Email = "nikt@example.org", Password = "x" });
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("default-src 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        Assert.False(response.Headers.Contains("Server"));

        var health = await Client.GetAsync("/health");
        Assert.Equal("nosniff", health.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Null(health.Headers.CacheControl);
    }

    [Fact]
    public async Task UnknownRoles_AreRefusedWithAClearMessage()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Unique()}@example.org", "Sekret123!", "Administrator");
        var response = await admin.PostAsJsonAsync("/api/users", new { Email = $"x-{Unique()}@example.org", Password = "Sekret123!", Roles = new[] { "Zmyślona" } });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Nieznana rola: Zmyślona", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public void LibraryErrors_DoNotLeakTheirDetails()
    {
        Assert.True(GlobalExceptionHandler.IsOwnMessage(new InvalidOperationException("nasz komunikat")));
        try
        {
            throw new InvalidOperationException("Nie znaleziono konta.");
        }
        catch (InvalidOperationException own)
        {
            Assert.True(GlobalExceptionHandler.IsOwnMessage(own));
        }
        try
        {
            _ = Array.Empty<int>().First();
        }
        catch (InvalidOperationException library)
        {
            Assert.False(GlobalExceptionHandler.IsOwnMessage(library));
        }
    }

    [Fact]
    public async Task AnAdministratorsPerson_IsNotAnonymizedByAccident()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Unique()}@example.org", "Sekret123!", "Administrator");
        var personId = await SeedPersonAsync(admin, "Adam", "Admiński" + Unique());
        var second = await (await admin.PostAsJsonAsync("/api/users", new { PersonId = personId, Email = $"admin2-{Unique()}@example.org", Password = "Sekret123!", Roles = new[] { "Administrator" } }))
            .Content.ReadFromJsonAsync<UserDto>();

        var refused = await admin.PostAsync($"/api/people/{personId}/rodo/anonymize", null);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("konto administratora", await refused.Content.ReadAsStringAsync());

        (await admin.PutAsJsonAsync($"/api/users/{second!.Id}/roles", new { Roles = new[] { "Biskup" } })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/people/{personId}/rodo/anonymize", null)).StatusCode);
    }
}
