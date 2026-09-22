using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.ParishNeeds;
using DokPortal.Application.Parishes;
using DokPortal.Application.People;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class ParishNeedsControllerTests : IntegrationTestBase
{
    public ParishNeedsControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Create_ThenAssign_UpdatesStatus()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var parishResponse = await admin.PostAsJsonAsync("/api/parishes", new { Name = "św. Pawła" });
        var parish = await parishResponse.Content.ReadFromJsonAsync<ParishDto>();
        var personResponse = await admin.PostAsJsonAsync("/api/people", new { FirstName = "Agnieszka", LastName = "Krol" });
        var person = await personResponse.Content.ReadFromJsonAsync<PersonDto>();

        var createResponse = await admin.PostAsJsonAsync("/api/parish-needs", new
        {
            ParishId = parish!.Id, Description = "Wsparcie katechezy dla dorosłych"
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ParishNeedDto>();

        var assignResponse = await admin.PutAsJsonAsync($"/api/parish-needs/{created!.Id}/assign", new { PersonId = person!.Id });
        assignResponse.EnsureSuccessStatusCode();
        var assigned = await assignResponse.Content.ReadFromJsonAsync<ParishNeedDto>();

        Assert.Equal("Assigned", assigned!.Status);
        Assert.Equal(person.Id, assigned.AssignedPersonId);
    }

    [Fact]
    public async Task Create_WithoutRequiredRole_ReturnsForbidden()
    {
        var client = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");

        var response = await client.PostAsJsonAsync("/api/parish-needs", new { ParishId = Guid.NewGuid(), Description = "Test" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
