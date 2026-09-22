using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Candidates;
using DokPortal.Application.Common;
using DokPortal.Application.People;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class CandidatesControllerTests : IntegrationTestBase
{
    public CandidatesControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private async Task<Guid> CreatePersonAsync(HttpClient client, string firstName, string lastName)
    {
        var response = await client.PostAsJsonAsync("/api/people", new { FirstName = firstName, LastName = lastName });
        response.EnsureSuccessStatusCode();
        var person = await response.Content.ReadFromJsonAsync<PersonDto>();
        return person!.Id;
    }

    [Fact]
    public async Task Create_ThenGetById_ReturnsCreatedCandidate()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var personId = await CreatePersonAsync(admin, "Karolina", "Nowak");

        var createResponse = await admin.PostAsJsonAsync("/api/candidates", new
        {
            PersonId = personId, Year = 1, AttendancePercentage = 81, OpinionsCollected = 0, IsRetreatCompleted = false
        });

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CandidateDto>();
        Assert.NotNull(created);
        Assert.Equal("Karolina Nowak", created!.PersonFullName);

        var getResponse = await admin.GetAsync($"/api/candidates/{created.Id}");
        getResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Search_ByYear_ReturnsOnlyMatchingCandidates()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var personId = await CreatePersonAsync(admin, "Tomasz", "Wisniewski");
        await admin.PostAsJsonAsync("/api/candidates", new { PersonId = personId, Year = 2, OpinionsCollected = 1, IsRetreatCompleted = true });

        var response = await admin.GetAsync("/api/candidates?year=2");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<CandidateDto>>();
        Assert.NotNull(result);
        Assert.Contains(result!.Items, c => c.PersonFullName == "Tomasz Wisniewski");
    }

    [Fact]
    public async Task Create_WithoutRequiredRole_ReturnsForbidden()
    {
        var client = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");

        var response = await client.PostAsJsonAsync("/api/candidates", new { PersonId = Guid.NewGuid(), Year = 1, OpinionsCollected = 0, IsRetreatCompleted = false });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
