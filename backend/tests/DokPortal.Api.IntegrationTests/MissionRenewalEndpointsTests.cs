using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Common;
using DokPortal.Application.Missions;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Misje wygasające, „Odnów” i filtr statusu przez API (zapytania muszą działać na prawdziwej bazie, nie tylko w pamięci).</summary>
public class MissionRenewalEndpointsTests : IntegrationTestBase
{
    public MissionRenewalEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task ExpiringMissions_CanBeRenewed_AndTheListIsFilteredByStatus()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var personId = await SeedPersonAsync(admin, "Odnawiany", $"Kat{Guid.NewGuid():N}"[..10]);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var old = await (await admin.PostAsJsonAsync("/api/missions", new
        {
            PersonId = personId, ServicePlace = "Fara", MissionStartDate = today.AddYears(-1), MissionEndDate = today.AddDays(5)
        })).Content.ReadFromJsonAsync<MissionDto>();

        var expiring = await admin.GetFromJsonAsync<List<MissionDto>>("/api/missions/expiring");
        var renew = await admin.PostAsync($"/api/missions/{old!.Id}/renew", null);
        var renewed = await renew.Content.ReadFromJsonAsync<MissionDto>();
        var afterRenewal = await admin.GetFromJsonAsync<List<MissionDto>>("/api/missions/expiring");
        var current = await admin.GetFromJsonAsync<PagedResult<MissionDto>>("/api/missions?status=Current&pageSize=1000");
        var expired = await admin.GetFromJsonAsync<PagedResult<MissionDto>>("/api/missions?status=Expired&pageSize=1000");

        Assert.Contains(expiring!, m => m.Id == old.Id);
        Assert.Equal(HttpStatusCode.OK, renew.StatusCode);
        Assert.Equal(today.AddDays(6), renewed!.MissionStartDate);
        Assert.DoesNotContain(afterRenewal!, m => m.PersonId == personId);
        Assert.Contains(current!.Items, m => m.Id == renewed.Id);
        Assert.DoesNotContain(expired!.Items, m => m.PersonId == personId);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/api/missions/{old.Id}/renew", null)).StatusCode);
    }
}
