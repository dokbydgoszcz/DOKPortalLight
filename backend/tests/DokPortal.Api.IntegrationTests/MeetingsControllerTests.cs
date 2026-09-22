using System.Net.Http.Json;
using DokPortal.Application.Meetings;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class MeetingsControllerTests : IntegrationTestBase
{
    public MeetingsControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Create_ThenGetAll_ReturnsCreatedMeeting()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

        var createResponse = await admin.PostAsJsonAsync("/api/meetings", new
        {
            GroupLabel = "Grupa SKŚP II", MeetingDate = "2026-09-23"
        });
        createResponse.EnsureSuccessStatusCode();

        var getResponse = await admin.GetAsync("/api/meetings");
        getResponse.EnsureSuccessStatusCode();
        var meetings = await getResponse.Content.ReadFromJsonAsync<List<MeetingDto>>();
        Assert.Contains(meetings!, m => m.GroupLabel == "Grupa SKŚP II");
    }
}
