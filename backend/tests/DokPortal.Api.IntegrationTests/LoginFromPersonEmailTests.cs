using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Auth;
using DokPortal.Application.People;
using DokPortal.Application.Users;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Login konta to e-mail osoby: nie podaje się go drugi raz, a zmiana e-maila osoby zmienia login.</summary>
public class LoginFromPersonEmailTests : IntegrationTestBase
{
    public LoginFromPersonEmailTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private Task<HttpClient> AdminAsync() =>
        CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

    private static string NewEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@example.org";

    private static async Task<PersonDto> PersonAsync(HttpClient admin, string? email)
    {
        var response = await admin.PostAsJsonAsync("/api/people", new { FirstName = "Kuba", LastName = "Adekow", Email = email });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PersonDto>())!;
    }

    [Fact]
    public async Task TheAccountOfAPersonWithAnEmail_UsesThatEmailAsItsLogin_EvenIfAnotherOneWasTyped()
    {
        var admin = await AdminAsync();
        var email = NewEmail("kuba");
        var person = await PersonAsync(admin, email);

        var withoutEmail = await admin.PostAsJsonAsync("/api/users", new { PersonId = person.Id, Password = "Sekret123!", Roles = new[] { "KatechistaProwadzacy" } });
        var created = await withoutEmail.Content.ReadFromJsonAsync<UserDto>();

        Assert.Equal(HttpStatusCode.Created, withoutEmail.StatusCode);
        Assert.Equal(email, created!.Email);
        Assert.Equal(HttpStatusCode.OK, (await Client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = "Sekret123!" })).StatusCode);

        var other = await PersonAsync(admin, NewEmail("ewa"));
        var typed = await (await admin.PostAsJsonAsync("/api/users", new { Email = NewEmail("inny"), PersonId = other.Id, Password = "Sekret123!", Roles = Array.Empty<string>() }))
            .Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal(other.Email, typed!.Email);
    }

    [Fact]
    public async Task ForAPersonWithoutAnEmail_TheTypedEmailBecomesTheLogin_AndThePersonsEmail()
    {
        var admin = await AdminAsync();
        var person = await PersonAsync(admin, null);
        var email = NewEmail("bez");

        var missing = await admin.PostAsJsonAsync("/api/users", new { PersonId = person.Id, Password = "Sekret123!", Roles = Array.Empty<string>() });
        var created = await admin.PostAsJsonAsync("/api/users", new { Email = email, PersonId = person.Id, Password = "Sekret123!", Roles = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains("e-mail", await missing.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(email, (await admin.GetFromJsonAsync<PersonDto>($"/api/people/{person.Id}"))!.Email);
    }

    [Fact]
    public async Task AnAccountWithoutAPerson_StillNeedsAnEmail()
    {
        var admin = await AdminAsync();

        var response = await admin.PostAsJsonAsync("/api/users", new { Password = "Sekret123!", Roles = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangingThePersonsEmail_ChangesTheLogin()
    {
        var admin = await AdminAsync();
        var oldEmail = NewEmail("stary");
        var newEmail = NewEmail("nowy");
        var person = await PersonAsync(admin, oldEmail);
        (await admin.PostAsJsonAsync("/api/users", new { PersonId = person.Id, Password = "Sekret123!", Roles = Array.Empty<string>() })).EnsureSuccessStatusCode();

        (await admin.PutAsJsonAsync($"/api/people/{person.Id}", new { FirstName = "Kuba", LastName = "Adekow", Email = newEmail })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PostAsJsonAsync("/api/auth/login", new { Email = oldEmail, Password = "Sekret123!" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.PostAsJsonAsync("/api/auth/login", new { Email = newEmail, Password = "Sekret123!" })).StatusCode);
        Assert.Contains((await admin.GetFromJsonAsync<List<UserDto>>("/api/users"))!, u => u.PersonId == person.Id && u.Email == newEmail);
    }

    [Fact]
    public async Task APersonCannotTakeTheEmailOfAnotherAccount()
    {
        var admin = await AdminAsync();
        var taken = NewEmail("zajety");
        await CreateUserAndGetTokenAsync(taken, "Sekret123!");
        var person = await PersonAsync(admin, NewEmail("kuba"));

        var response = await admin.PutAsJsonAsync($"/api/people/{person.Id}", new { FirstName = "Kuba", LastName = "Adekow", Email = taken });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("konta", await response.Content.ReadAsStringAsync());
    }
}
