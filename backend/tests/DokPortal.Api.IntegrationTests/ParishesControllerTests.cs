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
