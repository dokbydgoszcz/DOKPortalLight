using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DokPortal.Application.Common;
using DokPortal.Application.People;
using DokPortal.Application.Users;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Dostęp do danych osób, konta i sesje (przegląd procesu 2026-10-08, B1, B2, B4, B8).</summary>
public class AccessControlTests : IntegrationTestBase
{
    public AccessControlTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private Task<HttpClient> AdminAsync() =>
        CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

    private HttpClient ClientWith(string token)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task CreateRoleAsync(HttpClient admin, string role, params string[] permissions)
    {
        (await admin.PostAsJsonAsync("/api/permissions/roles", new { Name = role })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/permissions/roles/{role}", new { Permissions = permissions })).EnsureSuccessStatusCode();
    }

    private static async Task<UserDto> FindUserAsync(HttpClient admin, string email) =>
        (await admin.GetFromJsonAsync<List<UserDto>>("/api/users"))!.Single(u => u.Email == email);

    // ---------- Dane osób ----------

    [Fact]
    public async Task People_AreHiddenFromRolesWithoutAccessToThem()
    {
        var admin = await AdminAsync();
        var personId = await SeedPersonAsync(admin, "Ukryta", "Osoba");
        var noPeople = $"BezOsob{Guid.NewGuid():N}"[..14];
        await CreateRoleAsync(admin, noPeople, "DokCases.View");
        var catechist = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", noPeople);

        Assert.Equal(HttpStatusCode.Forbidden, (await catechist.GetAsync("/api/people")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await catechist.GetAsync($"/api/people/{personId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await catechist.GetAsync("/api/name-days/upcoming?days=30")).StatusCode);
    }

    [Fact]
    public async Task People_AreVisibleWithPeopleView_OrToRolesThatPickPeopleForTheirWork()
    {
        var admin = await AdminAsync();
        var viewer = $"Podglad{Guid.NewGuid():N}"[..14];
        var picker = $"Wybor{Guid.NewGuid():N}"[..12];
        await CreateRoleAsync(admin, viewer, "People.View");
        await CreateRoleAsync(admin, picker, "Candidates.View", "Candidates.Manage");
        var viewerClient = await CreateAuthenticatedClientAsync($"v-{Guid.NewGuid():N}@example.org", "Sekret123!", viewer);
        var pickerClient = await CreateAuthenticatedClientAsync($"p-{Guid.NewGuid():N}@example.org", "Sekret123!", picker);

        Assert.Equal(HttpStatusCode.OK, (await viewerClient.GetAsync("/api/people")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewerClient.GetAsync("/api/name-days/upcoming?days=30")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await pickerClient.GetAsync("/api/people")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await pickerClient.GetAsync("/api/name-days/upcoming?days=30")).StatusCode);
    }

    [Fact]
    public async Task Paging_IsClampedToSaneValues()
    {
        var admin = await AdminAsync();

        var zero = await admin.GetAsync("/api/people?page=0&pageSize=0");
        var huge = await admin.GetFromJsonAsync<PagedResult<PersonDto>>("/api/people?page=-5&pageSize=100000");

        Assert.Equal(HttpStatusCode.OK, zero.StatusCode);
        Assert.Equal(1, huge!.Page);
        Assert.Equal(200, huge.PageSize);
    }

    // ---------- Konta i sesje ----------

    [Fact]
    public async Task ADeactivatedAccount_CannotLogIn_AndItsOpenSessionStops()
    {
        var admin = await AdminAsync();
        var email = $"odchodzi-{Guid.NewGuid():N}@example.org";
        var token = await CreateUserAndGetTokenAsync(email, "Sekret123!", "DyrektorDOK");
        var session = ClientWith(token);
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/dok-cases")).StatusCode);
        var user = await FindUserAsync(admin, email);

        var deactivate = await admin.PutAsJsonAsync($"/api/users/{user.Id}/active", new { IsActive = false });
        var login = await Client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = "Sekret123!" });

        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        Assert.False((await deactivate.Content.ReadFromJsonAsync<UserDto>())!.IsActive);
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/dok-cases")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Contains("nieaktywne", await login.Content.ReadAsStringAsync());

        (await admin.PutAsJsonAsync($"/api/users/{user.Id}/active", new { IsActive = true })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await Client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = "Sekret123!" })).StatusCode);
    }

    [Fact]
    public async Task ChangingRoles_EndsTheOpenSession_SoTheNewRolesApplyAtOnce()
    {
        var admin = await AdminAsync();
        var email = $"rola-{Guid.NewGuid():N}@example.org";
        var session = ClientWith(await CreateUserAndGetTokenAsync(email, "Sekret123!", "DyrektorDOK"));
        var user = await FindUserAsync(admin, email);

        (await admin.PutAsJsonAsync($"/api/users/{user.Id}/roles", new { Roles = new[] { "Superwizor" } })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/dok-cases")).StatusCode);
    }

    [Fact]
    public async Task YouCannotDeactivateYourself()
    {
        var email = $"admin-{Guid.NewGuid():N}@example.org";
        var admin = await CreateAuthenticatedClientAsync(email, "Sekret123!", "Administrator");
        var self = await FindUserAsync(admin, email);

        var response = await admin.PutAsJsonAsync($"/api/users/{self.Id}/active", new { IsActive = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("własnego", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OnlyAnAdministrator_CanGrantTheAdministratorRole()
    {
        var admin = await AdminAsync();
        var manager = $"Konta{Guid.NewGuid():N}"[..12];
        await CreateRoleAsync(admin, manager, "Users.Manage");
        var managerClient = await CreateAuthenticatedClientAsync($"m-{Guid.NewGuid():N}@example.org", "Sekret123!", manager);
        var targetEmail = $"cel-{Guid.NewGuid():N}@example.org";
        await CreateUserAndGetTokenAsync(targetEmail, "Sekret123!", "Superwizor");
        var target = await FindUserAsync(admin, targetEmail);

        var promote = await managerClient.PutAsJsonAsync($"/api/users/{target.Id}/roles", new { Roles = new[] { "Administrator" } });
        var create = await managerClient.PostAsJsonAsync("/api/users", new
        {
            Email = $"nowy-{Guid.NewGuid():N}@example.org", Password = "Sekret123!", Roles = new[] { "Administrator" }
        });
        var ordinary = await managerClient.PutAsJsonAsync($"/api/users/{target.Id}/roles", new { Roles = new[] { "DyrektorDOK" } });

        Assert.Equal(HttpStatusCode.BadRequest, promote.StatusCode);
        Assert.Contains("Administrator", await promote.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ordinary.StatusCode);
    }
}

/// <summary>Osobna baza (własna fabryka): jedynym administratorem jest konto z seedera.</summary>
public class LastAdministratorTests : IntegrationTestBase
{
    public LastAdministratorTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task TheLastActiveAdministrator_CannotLoseTheRoleOrBeDeactivated()
    {
        // W tej bazie (osobna fabryka, seeder nie działa w testach) jest dokładnie jeden administrator.
        var email = $"jedyny-{Guid.NewGuid():N}@example.org";
        var admin = await CreateAuthenticatedClientAsync(email, "Sekret123!", "Administrator");
        var self = (await admin.GetFromJsonAsync<List<UserDto>>("/api/users"))!.Single(u => u.Email == email);
        var otherEmail = $"drugi-{Guid.NewGuid():N}@example.org";
        (await admin.PostAsJsonAsync("/api/users", new { Email = otherEmail, Password = "Sekret123!", Roles = new[] { "DyrektorDOK" } })).EnsureSuccessStatusCode();

        var demote = await admin.PutAsJsonAsync($"/api/users/{self.Id}/roles", new { Roles = new[] { "DyrektorDOK" } });

        Assert.Equal(HttpStatusCode.BadRequest, demote.StatusCode);
        Assert.Contains("ostatni", await demote.Content.ReadAsStringAsync());

        // Gdy jest drugi administrator, pierwszy może już oddać rolę.
        var other = (await admin.GetFromJsonAsync<List<UserDto>>("/api/users"))!.Single(u => u.Email == otherEmail);
        (await admin.PutAsJsonAsync($"/api/users/{other.Id}/roles", new { Roles = new[] { "Administrator" } })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/users/{self.Id}/roles", new { Roles = new[] { "DyrektorDOK" } })).StatusCode);
    }
}
