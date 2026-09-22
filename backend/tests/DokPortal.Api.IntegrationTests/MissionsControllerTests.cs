using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Missions;
using DokPortal.Application.People;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class MissionsControllerTests : IntegrationTestBase
{
    public MissionsControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Create_ThenGetById_ReturnsCreatedMission()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var personResponse = await admin.PostAsJsonAsync("/api/people", new { FirstName = "Marek", LastName = "Zielinski" });
        var person = await personResponse.Content.ReadFromJsonAsync<PersonDto>();

        var createResponse = await admin.PostAsJsonAsync("/api/missions", new
        {
            PersonId = person!.Id,
            ServicePlace = "Parafia św. Józefa",
            MissionStartDate = "2025-07-01",
            MissionEndDate = "2028-06-30"
        });

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<MissionDto>();
        Assert.NotNull(created);

        var getResponse = await admin.GetAsync($"/api/missions/{created!.Id}");
        getResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Create_WithoutRequiredRole_ReturnsForbidden()
    {
        var client = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");

        var response = await client.PostAsJsonAsync("/api/missions", new
        {
            PersonId = Guid.NewGuid(), ServicePlace = "Test", MissionStartDate = "2025-01-01", MissionEndDate = "2028-01-01"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
