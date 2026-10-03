using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DokPortal.Application.Auth;
using DokPortal.Application.AuditLog;
using DokPortal.Application.Common;
using DokPortal.Application.DokCases;
using DokPortal.Application.Users;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class UserPersonLinkTests : IntegrationTestBase
{
    public UserPersonLinkTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private static string NewEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@example.org";

    private static async Task<UserDto> CreateUserAsync(HttpClient admin, string email, params string[] roles)
    {
        var response = await admin.PostAsJsonAsync("/api/users", new { Email = email, Password = "Sekret123!", Roles = roles });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserDto>())!;
    }

    private async Task<HttpClient> LoginAsync(string email)
    {
        var login = await Client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = "Sekret123!" });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.Token;
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Admin_LinksAnExistingAccountToAPerson_AndTheListShowsIt()
    {
        var admin = await CreateAuthenticatedClientAsync(NewEmail("admin"), "Sekret123!", "Administrator");
        var user = await CreateUserAsync(admin, NewEmail("kat"), "KatechistaProwadzacy");
        var personId = await SeedPersonAsync(admin, "Anna", "Maj");

        var response = await admin.PutAsJsonAsync($"/api/users/{user.Id}/person", new { PersonId = personId });
        var users = await admin.GetFromJsonAsync<List<UserDto>>("/api/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal(personId, updated!.PersonId);
        Assert.Equal("Anna Maj", updated.PersonFullName);
        Assert.Equal("Anna Maj", users!.Single(u => u.Id == user.Id).PersonFullName);
    }

    [Fact]
    public async Task LinkingTheAccount_MakesTheCatechistSeeTheirCases()
    {
        var admin = await CreateAuthenticatedClientAsync(NewEmail("admin"), "Sekret123!", "Administrator");
        var (caseId, catechistPersonId, _) = await SeedDokCaseAsync(admin, "Jan", "Kowalski", "Anna", "Maj");
        var email = NewEmail("kat");
        var user = await CreateUserAsync(admin, email, "KatechistaProwadzacy");
        var catechist = await LoginAsync(email);

        var before = await catechist.GetFromJsonAsync<PagedResult<DokCaseDto>>("/api/dok-cases?pageSize=100", EnumJsonOptions);
        await admin.PutAsJsonAsync($"/api/users/{user.Id}/person", new { PersonId = catechistPersonId });
        var after = await catechist.GetFromJsonAsync<PagedResult<DokCaseDto>>("/api/dok-cases?pageSize=100", EnumJsonOptions);

        Assert.Empty(before!.Items);
        Assert.Equal(caseId, Assert.Single(after!.Items).Id);
    }

    [Fact]
    public async Task NullPersonId_UnlinksTheAccount()
    {
        var admin = await CreateAuthenticatedClientAsync(NewEmail("admin"), "Sekret123!", "Administrator");
        var user = await CreateUserAsync(admin, NewEmail("kat"), "KatechistaProwadzacy");
        var personId = await SeedPersonAsync(admin, "Anna", "Maj");
        await admin.PutAsJsonAsync($"/api/users/{user.Id}/person", new { PersonId = personId });

        var response = await admin.PutAsJsonAsync($"/api/users/{user.Id}/person", new { PersonId = (Guid?)null });

        var updated = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.Null(updated!.PersonId);
        Assert.Null(updated.PersonFullName);
    }

    [Fact]
    public async Task APersonCanHaveOnlyOneAccount()
    {
        var admin = await CreateAuthenticatedClientAsync(NewEmail("admin"), "Sekret123!", "Administrator");
        var first = await CreateUserAsync(admin, NewEmail("one"));
        var second = await CreateUserAsync(admin, NewEmail("two"));
        var personId = await SeedPersonAsync(admin, "Anna", "Maj");
        await admin.PutAsJsonAsync($"/api/users/{first.Id}/person", new { PersonId = personId });

        var link = await admin.PutAsJsonAsync($"/api/users/{second.Id}/person", new { PersonId = personId });
        var create = await admin.PostAsJsonAsync("/api/users", new { Email = NewEmail("three"), Password = "Sekret123!", Roles = Array.Empty<string>(), PersonId = personId });

        Assert.Equal(HttpStatusCode.BadRequest, link.StatusCode);
        Assert.Contains(first.Email, await link.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
    }

    [Fact]
    public async Task ReLinkingTheSamePerson_ToTheSameAccount_IsAllowed()
    {
        var admin = await CreateAuthenticatedClientAsync(NewEmail("admin"), "Sekret123!", "Administrator");
        var user = await CreateUserAsync(admin, NewEmail("kat"));
        var personId = await SeedPersonAsync(admin, "Anna", "Maj");
        await admin.PutAsJsonAsync($"/api/users/{user.Id}/person", new { PersonId = personId });

        var again = await admin.PutAsJsonAsync($"/api/users/{user.Id}/person", new { PersonId = personId });

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [Fact]
    public async Task UnknownPersonOrUser_AreRejected()
    {
        var admin = await CreateAuthenticatedClientAsync(NewEmail("admin"), "Sekret123!", "Administrator");
        var user = await CreateUserAsync(admin, NewEmail("kat"));

        var unknownPerson = await admin.PutAsJsonAsync($"/api/users/{user.Id}/person", new { PersonId = Guid.NewGuid() });
        var unknownUser = await admin.PutAsJsonAsync($"/api/users/{Guid.NewGuid()}/person", new { PersonId = (Guid?)null });

        Assert.Equal(HttpStatusCode.BadRequest, unknownPerson.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownUser.StatusCode);
    }

    [Fact]
    public async Task OnlyUsersManage_CanLinkAccounts_AndTheChangeIsAudited()
    {
        var admin = await CreateAuthenticatedClientAsync(NewEmail("admin"), "Sekret123!", "Administrator");
        var user = await CreateUserAsync(admin, NewEmail("kat"));
        var personId = await SeedPersonAsync(admin, "Anna", "Maj");
        var director = await CreateAuthenticatedClientAsync(NewEmail("dyr"), "Sekret123!", "DyrektorDOK");

        var forbidden = await director.PutAsJsonAsync($"/api/users/{user.Id}/person", new { PersonId = personId });
        await admin.PutAsJsonAsync($"/api/users/{user.Id}/person", new { PersonId = personId });
        var log = await admin.GetFromJsonAsync<List<AuditLogEntryDto>>("/api/audit-log", EnumJsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Contains(log!, e => e.Action == "SetUserPerson" && e.ObjectDescription == user.Email);
    }
}
