using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Common;
using DokPortal.Application.DokCases;
using DokPortal.Application.People;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class GraduatesControllerTests : IntegrationTestBase
{
    public GraduatesControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private static async Task<Guid> CreatePersonAsync(HttpClient client, string firstName, string lastName)
    {
        var response = await client.PostAsJsonAsync("/api/people", new { FirstName = firstName, LastName = lastName });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PersonDto>())!.Id;
    }

    private static async Task CreateCaseAsync(HttpClient client, string firstName, string lastName, string stage)
    {
        var personId = await CreatePersonAsync(client, firstName, lastName);
        var catechistId = await CreatePersonAsync(client, "Katechista", $"Dla{lastName}");
        var response = await client.PostAsJsonAsync("/api/dok-cases", new
        {
            PersonId = personId, Path = "Confirmation", Stage = stage, CatechistPersonId = catechistId
        });
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Get_ReturnsOnlyGraduates_AndSupportsSearch()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var marker = Guid.NewGuid().ToString("N")[..8];
        await CreateCaseAsync(admin, "Absolwent", $"Pierwszy{marker}", "Graduate");
        await CreateCaseAsync(admin, "Uczestnik", $"Formacja{marker}", "Evangelization");

        var all = await admin.GetFromJsonAsync<PagedResult<DokCaseDto>>($"/api/graduates?search={marker}", EnumJsonOptions);
        var none = await admin.GetFromJsonAsync<PagedResult<DokCaseDto>>($"/api/graduates?search=Formacja{marker}", EnumJsonOptions);

        Assert.Single(all!.Items);
        Assert.Equal($"Absolwent Pierwszy{marker}", all.Items[0].PersonFullName);
        Assert.Empty(none!.Items);
    }

    [Fact]
    public async Task Get_IsAllowedForDyrektorDok_ButForbiddenForSuperwizor()
    {
        var dyrektor = await CreateAuthenticatedClientAsync($"dyr-{Guid.NewGuid():N}@example.org", "Sekret123!", "DyrektorDOK");
        var superwizor = await CreateAuthenticatedClientAsync($"sup-{Guid.NewGuid():N}@example.org", "Sekret123!", "Superwizor");

        Assert.Equal(HttpStatusCode.OK, (await dyrektor.GetAsync("/api/graduates")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await superwizor.GetAsync("/api/graduates")).StatusCode);
    }

    [Fact]
    public async Task Get_WithoutToken_ReturnsUnauthorized()
    {
        var response = await Factory.CreateClient().GetAsync("/api/graduates");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
