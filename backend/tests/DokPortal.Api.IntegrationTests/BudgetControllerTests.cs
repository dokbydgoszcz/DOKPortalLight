using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DokPortal.Application.Budget;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class BudgetControllerTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public BudgetControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Create_ThenGetByFund_ReturnsCreatedEntry()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

        var createResponse = await admin.PostAsJsonAsync("/api/budget", new
        {
            Fund = "SKSP", EntryDate = "2026-09-18", Description = "Materiały formacyjne",
            Category = "Materiały", Type = "Expense", Amount = 780m
        });
        createResponse.EnsureSuccessStatusCode();

        var getResponse = await admin.GetAsync("/api/budget?fund=SKSP");
        getResponse.EnsureSuccessStatusCode();
        var entries = await getResponse.Content.ReadFromJsonAsync<List<BudgetEntryDto>>(JsonOptions);
        Assert.Contains(entries!, e => e.Description == "Materiały formacyjne");
    }
}
