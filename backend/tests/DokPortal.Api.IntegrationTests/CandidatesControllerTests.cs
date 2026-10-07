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
            PersonId = personId, Year = 1, AttendancePercentage = 81, OpinionsCollected = 0
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
        await admin.PostAsJsonAsync("/api/candidates", new { PersonId = personId, Year = 2, OpinionsCollected = 1 });

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

        var response = await client.PostAsJsonAsync("/api/candidates", new { PersonId = Guid.NewGuid(), Year = 1, OpinionsCollected = 0 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Retreats_AreSavedWithTheCandidate_AndReplacedOnUpdate()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var personId = await CreatePersonAsync(admin, "Ewa", "Zielinska");
        var created = await (await admin.PostAsJsonAsync("/api/candidates", new
        {
            PersonId = personId, Year = 2, OpinionsCollected = 0,
            Retreats = new[] { new { Year = 1, IsCompleted = true }, new { Year = 2, IsCompleted = false } }
        })).Content.ReadFromJsonAsync<CandidateDto>();
        Assert.Equal(2, created!.Retreats.Count);

        var updateResponse = await admin.PutAsJsonAsync($"/api/candidates/{created.Id}", new
        {
            PersonId = personId, Year = 2, OpinionsCollected = 1,
            Retreats = new[] { new { Year = 2, IsCompleted = true } }
        });
        var updated = await updateResponse.Content.ReadFromJsonAsync<CandidateDto>();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var retreat = Assert.Single(updated!.Retreats);
        Assert.Equal((2, true), (retreat.Year, retreat.IsCompleted));
        var fetched = await admin.GetFromJsonAsync<CandidateDto>($"/api/candidates/{created.Id}");
        Assert.Single(fetched!.Retreats);
    }

    [Fact]
    public async Task Retreats_WithAnInvalidYearOrTwoInOneYear_AreABadRequest()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var personId = await CreatePersonAsync(admin, "Ewa", "Zielinska");

        var badYear = await admin.PostAsJsonAsync("/api/candidates", new
        {
            PersonId = personId, Year = 1, OpinionsCollected = 0, Retreats = new[] { new { Year = 5, IsCompleted = true } }
        });
        var duplicate = await admin.PostAsJsonAsync("/api/candidates", new
        {
            PersonId = personId, Year = 1, OpinionsCollected = 0,
            Retreats = new[] { new { Year = 1, IsCompleted = true }, new { Year = 1, IsCompleted = false } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, badYear.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
    }

    [Fact]
    public async Task ANewCandidate_IsInFormationInTheChosenYear()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var personId = await CreatePersonAsync(admin, "Iga", "Lis");

        var created = await (await admin.PostAsJsonAsync("/api/candidates", new { PersonId = personId, Year = 2, OpinionsCollected = 0 }))
            .Content.ReadFromJsonAsync<CandidateDto>();

        Assert.Equal((2, "InFormation", false), (created!.Year, created.Status, created.IsFormationStopped));
    }

    [Fact]
    public async Task StoppingTheFormation_NeedsAReason_AndShowsInTheStatus()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var personId = await CreatePersonAsync(admin, "Iga", "Lis");
        var created = await (await admin.PostAsJsonAsync("/api/candidates", new { PersonId = personId, Year = 1, OpinionsCollected = 0 }))
            .Content.ReadFromJsonAsync<CandidateDto>();

        var withoutReason = await admin.PutAsJsonAsync($"/api/candidates/{created!.Id}", new { PersonId = personId, Year = 1, OpinionsCollected = 0, IsFormationStopped = true });
        var stopped = await admin.PutAsJsonAsync($"/api/candidates/{created.Id}", new
        {
            PersonId = personId, Year = 1, OpinionsCollected = 0, IsFormationStopped = true, FormationStopNote = "Zrezygnowała"
        });
        var dto = await stopped.Content.ReadFromJsonAsync<CandidateDto>();

        Assert.Equal(HttpStatusCode.BadRequest, withoutReason.StatusCode);
        Assert.Contains("powód", await withoutReason.Content.ReadAsStringAsync());
        Assert.Equal(("Stopped", "Zrezygnowała"), (dto!.Status, dto.FormationStopNote));
    }

    [Fact]
    public async Task AYearOutsideOneToThree_IsABadRequest()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var personId = await CreatePersonAsync(admin, "Iga", "Lis");

        var response = await admin.PostAsJsonAsync("/api/candidates", new { PersonId = personId, Year = 4, OpinionsCollected = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public class CandidateAdvanceEndpointTests : IntegrationTestBase
{
    public CandidateAdvanceEndpointTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private async Task<(HttpClient Admin, CandidateDto First, CandidateDto Third)> SeedAsync()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var first = await (await admin.PostAsJsonAsync("/api/candidates", new { PersonId = await SeedPersonAsync(admin, "Jan", "Pierwszy"), Year = 1, OpinionsCollected = 0 })).Content.ReadFromJsonAsync<CandidateDto>();
        var third = await (await admin.PostAsJsonAsync("/api/candidates", new { PersonId = await SeedPersonAsync(admin, "Anna", "Trzecia"), Year = 3, OpinionsCollected = 0 })).Content.ReadFromJsonAsync<CandidateDto>();
        return (admin, first!, third!);
    }

    [Fact]
    public async Task MovesTheSelectedCandidates_AndTellsHowManyAdvancedAndHowManyFinished()
    {
        var (admin, first, third) = await SeedAsync();

        var response = await admin.PostAsJsonAsync("/api/candidates/advance", new { CandidateIds = new[] { first.Id, third.Id } });
        var result = await response.Content.ReadFromJsonAsync<AdvanceCandidatesResultDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((1, 1, 0), (result!.Advanced, result.Completed, result.Skipped.Count));
        var movedFirst = await admin.GetFromJsonAsync<CandidateDto>($"/api/candidates/{first.Id}");
        var movedThird = await admin.GetFromJsonAsync<CandidateDto>($"/api/candidates/{third.Id}");
        Assert.Equal(2, movedFirst!.Year);
        Assert.Equal("Completed", movedThird!.Status);
        Assert.Equal("Advanced", movedFirst.Events[0].Kind);
        Assert.StartsWith("admin-", movedFirst.Events[0].PerformedBy);
    }

    [Fact]
    public async Task TheMoveIsAudited()
    {
        var (admin, first, _) = await SeedAsync();
        await admin.PostAsJsonAsync("/api/candidates/advance", new { CandidateIds = new[] { first.Id } });

        var log = (await admin.GetFromJsonAsync<List<DokPortal.Application.AuditLog.AuditLogEntryDto>>("/api/audit-log?action=AdvanceCandidates", EnumJsonOptions))!;

        Assert.Contains(log, e => e.ObjectDescription.Contains("przeniesiono: 1"));
    }

    [Fact]
    public async Task AnEmptySelection_IsABadRequest_AndAUserWithoutManageIsForbidden()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var catechist = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");

        var empty = await admin.PostAsJsonAsync("/api/candidates/advance", new { CandidateIds = Array.Empty<Guid>() });
        var forbidden = await catechist.PostAsJsonAsync("/api/candidates/advance", new { CandidateIds = new[] { Guid.NewGuid() } });

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Contains("co najmniej jednego", await empty.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task UnknownCandidates_AreReportedAsSkipped_NotAsAnError()
    {
        var (admin, _, _) = await SeedAsync();

        var response = await admin.PostAsJsonAsync("/api/candidates/advance", new { CandidateIds = new[] { Guid.NewGuid() } });
        var result = await response.Content.ReadFromJsonAsync<AdvanceCandidatesResultDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(result!.Skipped);
    }
}
