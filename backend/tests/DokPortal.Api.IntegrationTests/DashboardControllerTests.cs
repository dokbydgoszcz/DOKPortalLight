using System.Net.Http.Json;
using DokPortal.Application.Dashboard;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class DashboardControllerTests : IntegrationTestBase
{
    public DashboardControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetSummary_ReturnsCounts()
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");

        var response = await client.GetAsync("/api/dashboard/summary");

        response.EnsureSuccessStatusCode();
        var summary = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>();
        Assert.NotNull(summary);
    }
}
