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
