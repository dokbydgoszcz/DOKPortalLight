using System.Net.Http.Json;
using DokPortal.Application.NameDays;
using DokPortal.Application.People;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class NameDaysControllerTests : IntegrationTestBase
{
    public NameDaysControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetUpcoming_ReturnsPeopleWithNameDayFieldsSet()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // this person has no nameday fields set and must be excluded from the result
        await admin.PostAsJsonAsync("/api/people", new { FirstName = "Jan", LastName = "Kowalski" });
        await admin.PostAsJsonAsync("/api/people", new
        {
            FirstName = "Ewa", LastName = "Nowak", NameDayMonth = today.Month, NameDayDay = today.Day
        });

        var response = await admin.GetAsync("/api/name-days/upcoming?days=30");
        var upcoming = await response.Content.ReadFromJsonAsync<List<UpcomingNameDayDto>>();

        Assert.Single(upcoming!);
        Assert.Equal("Ewa Nowak", upcoming![0].FullName);
    }
}
