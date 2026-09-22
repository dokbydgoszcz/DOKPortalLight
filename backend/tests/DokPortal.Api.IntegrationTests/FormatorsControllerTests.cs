using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Formators;
using DokPortal.Application.People;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class FormatorsControllerTests : IntegrationTestBase
{
    public FormatorsControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Create_ThenGetAll_ReturnsCreatedFormator()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var personResponse = await admin.PostAsJsonAsync("/api/people", new { FirstName = "Pawel", LastName = "Nowicki" });
        var person = await personResponse.Content.ReadFromJsonAsync<PersonDto>();

        var createResponse = await admin.PostAsJsonAsync("/api/formators", new { PersonId = person!.Id, Function = "Moderator" });
        createResponse.EnsureSuccessStatusCode();

        var getResponse = await admin.GetAsync("/api/formators");
        getResponse.EnsureSuccessStatusCode();
        var all = await getResponse.Content.ReadFromJsonAsync<List<FormatorDto>>();
        Assert.Contains(all!, f => f.Function == "Moderator");
    }

    [Fact]
    public async Task Create_WithoutRequiredRole_ReturnsForbidden()
    {
        var client = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");

        var response = await client.PostAsJsonAsync("/api/formators", new { PersonId = Guid.NewGuid(), Function = "Test" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
