using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.People;
using DokPortal.Application.Users;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Poprawka loginu konta przez administratora (np. literówka w domenie), także dla kont bez osoby.</summary>
public class ChangeLoginTests : IntegrationTestBase
{
    public ChangeLoginTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task TheLoginCanBeCorrected_AndTheLinkedPersonFollows()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var typo = $"lit-{Guid.NewGuid():N}@gmial.com";
        await CreateUserAndGetTokenAsync(typo, "Sekret123!", "DyrektorDOK");
        var user = (await admin.GetFromJsonAsync<List<UserDto>>("/api/users"))!.Single(u => u.Email == typo);
        var fixedLogin = typo.Replace("gmial", "gmail");

        var change = await admin.PutAsJsonAsync($"/api/users/{user.Id}/login", new { Email = fixedLogin });

        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        Assert.Equal(fixedLogin, (await change.Content.ReadFromJsonAsync<UserDto>())!.Email);
        Assert.Equal(HttpStatusCode.OK, (await Client.PostAsJsonAsync("/api/auth/login", new { Email = fixedLogin, Password = "Sekret123!" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PostAsJsonAsync("/api/auth/login", new { Email = typo, Password = "Sekret123!" })).StatusCode);

        var personId = await SeedPersonAsync(admin, "Ewa", "Login");
        var personLogin = $"ewa-{Guid.NewGuid():N}@example.org";
        var created = await (await admin.PostAsJsonAsync("/api/users", new { Email = personLogin, Password = "Sekret123!", PersonId = personId, Roles = new[] { "Superwizor" } }))
            .Content.ReadFromJsonAsync<UserDto>();
        var newPersonLogin = $"ewa2-{Guid.NewGuid():N}@example.org";
        (await admin.PutAsJsonAsync($"/api/users/{created!.Id}/login", new { Email = newPersonLogin })).EnsureSuccessStatusCode();
        Assert.Equal(newPersonLogin, (await admin.GetFromJsonAsync<PersonDto>($"/api/people/{personId}"))!.Email);
    }

    [Fact]
    public async Task ALoginOfAnotherAccount_OrAnInvalidAddress_IsRefused()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var a = $"a-{Guid.NewGuid():N}@example.org";
        var b = $"b-{Guid.NewGuid():N}@example.org";
        await CreateUserAndGetTokenAsync(a, "Sekret123!", "DyrektorDOK");
        await CreateUserAndGetTokenAsync(b, "Sekret123!", "DyrektorDOK");
        var userA = (await admin.GetFromJsonAsync<List<UserDto>>("/api/users"))!.Single(u => u.Email == a);
        var bishop = await CreateAuthenticatedClientAsync($"bp-{Guid.NewGuid():N}@example.org", "Sekret123!", "Biskup");

        var taken = await admin.PutAsJsonAsync($"/api/users/{userA.Id}/login", new { Email = b.ToUpperInvariant() });
        var invalid = await admin.PutAsJsonAsync($"/api/users/{userA.Id}/login", new { Email = "nie-adres" });

        Assert.Equal(HttpStatusCode.BadRequest, taken.StatusCode);
        Assert.Contains("innego konta", await taken.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bishop.PutAsJsonAsync($"/api/users/{userA.Id}/login", new { Email = "x@example.org" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync("/api/users/brak/login", new { Email = "x@example.org" })).StatusCode);
    }
}
