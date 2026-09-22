using System.Net.Http.Json;
using DokPortal.Application.Supervisions;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class SupervisionsControllerTests : IntegrationTestBase
{
    public SupervisionsControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Create_ThenGetByInstitution_ReturnsCreatedSupervision()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

        var createResponse = await admin.PostAsJsonAsync("/api/supervisions", new
        {
            Institution = "DOK", GroupLabel = "Grupa A", SupervisionDate = "2026-09-30"
        });
        createResponse.EnsureSuccessStatusCode();

        var getResponse = await admin.GetAsync("/api/supervisions?institution=DOK");
        getResponse.EnsureSuccessStatusCode();
        var supervisions = await getResponse.Content.ReadFromJsonAsync<List<SupervisionDto>>(EnumJsonOptions);
        Assert.Contains(supervisions!, s => s.GroupLabel == "Grupa A");
    }
}
