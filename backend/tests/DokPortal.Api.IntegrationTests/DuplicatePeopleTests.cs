using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DokPortal.Application.People;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class DuplicatePeopleTests : IntegrationTestBase
{
    public DuplicatePeopleTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private static string Unique(string prefix) => $"{prefix}{Guid.NewGuid():N}".Substring(0, 16);

    [Fact]
    public async Task SecondPersonWithTheSameEmail_IsRejectedWithEmailTaken()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var email = $"{Unique("jan")}@example.org";
        (await admin.PostAsJsonAsync("/api/people", new { FirstName = "Jan", LastName = "Kowalski", Email = email })).EnsureSuccessStatusCode();

        var response = await admin.PostAsJsonAsync("/api/people", new { FirstName = "Drugi", LastName = "Jan", Email = email.ToUpperInvariant() });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("EmailTaken", body.RootElement.GetProperty("code").GetString());
        Assert.Contains("Jan Kowalski", body.RootElement.GetProperty("title").GetString());
        Assert.Equal("Jan Kowalski", body.RootElement.GetProperty("duplicates")[0].GetProperty("fullName").GetString());
    }

    [Fact]
    public async Task SharedPhone_ReturnsAWarning_ThatCanBeConfirmed()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var phone = $"6{Random.Shared.Next(10000000, 99999999)}";
        (await admin.PostAsJsonAsync("/api/people", new { FirstName = "Jan", LastName = "Kowalski", Phone = phone })).EnsureSuccessStatusCode();

        var warning = await admin.PostAsJsonAsync("/api/people", new { FirstName = "Maria", LastName = "Kowalska", Phone = $"+48 {phone}" });
        var confirmed = await admin.PostAsJsonAsync("/api/people", new { FirstName = "Maria", LastName = "Kowalska", Phone = $"+48 {phone}", ConfirmDuplicate = true });

        Assert.Equal(HttpStatusCode.Conflict, warning.StatusCode);
        using var body = JsonDocument.Parse(await warning.Content.ReadAsStringAsync());
        Assert.Equal("PhoneDuplicate", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("Jan Kowalski", body.RootElement.GetProperty("duplicates")[0].GetProperty("fullName").GetString());
        Assert.Equal(HttpStatusCode.Created, confirmed.StatusCode);
    }

    [Fact]
    public async Task Update_ToAnotherPersonsEmail_IsRejected_AndKeepingYourOwnIsFine()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var first = $"{Unique("a")}@example.org";
        var second = $"{Unique("b")}@example.org";
        await admin.PostAsJsonAsync("/api/people", new { FirstName = "Jan", LastName = "Kowalski", Email = first });
        var created = await (await admin.PostAsJsonAsync("/api/people", new { FirstName = "Piotr", LastName = "Nowak", Email = second }))
            .Content.ReadFromJsonAsync<PersonDto>();

        var conflict = await admin.PutAsJsonAsync($"/api/people/{created!.Id}", new { FirstName = "Piotr", LastName = "Nowak", Email = first });
        var own = await admin.PutAsJsonAsync($"/api/people/{created.Id}", new { FirstName = "Piotr", LastName = "Poprawiony", Email = second });

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
    }

    [Fact]
    public async Task EmailOfAnAccountLinkedToAnotherPerson_IsRejected_ButAnUnlinkedAccountsEmailIsNot()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var ownerId = await SeedPersonAsync(admin, "Anna", "Maj");
        var linkedLogin = $"{Unique("linked")}@example.org";
        var unlinkedLogin = $"{Unique("free")}@example.org";
        await CreateUserAndGetTokenAsync(linkedLogin, "Sekret123!", ownerId);
        await CreateUserAndGetTokenAsync(unlinkedLogin, "Sekret123!");

        var rejected = await admin.PostAsJsonAsync("/api/people", new { FirstName = "Ktoś", LastName = "Inny", Email = linkedLogin });
        var allowed = await admin.PostAsJsonAsync("/api/people", new { FirstName = "Ktoś", LastName = "Wolny", Email = unlinkedLogin });

        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
    }
}
