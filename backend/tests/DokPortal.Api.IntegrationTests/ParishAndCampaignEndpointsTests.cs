using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Mailing;
using DokPortal.Application.Parishes;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class ParishAndCampaignEndpointsTests : IntegrationTestBase
{
    public ParishAndCampaignEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private async Task<HttpClient> AdminAsync() =>
        await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

    // --- Parafie ---

    [Fact]
    public async Task Parish_Put_UpdatesTheParish()
    {
        var admin = await AdminAsync();
        var created = await (await admin.PostAsJsonAsync("/api/parishes", new { Name = "św. Marka", City = "Bydgoszcz" }))
            .Content.ReadFromJsonAsync<ParishDto>();

        var response = await admin.PutAsJsonAsync($"/api/parishes/{created!.Id}", new { Name = "św. Marka Ewangelisty", City = "Toruń" });
        var all = await admin.GetFromJsonAsync<List<ParishDto>>("/api/parishes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = all!.Single(p => p.Id == created.Id);
        Assert.Equal("św. Marka Ewangelisty", updated.Name);
        Assert.Equal("Toruń", updated.City);
    }

    [Fact]
    public async Task Parish_Put_RejectsAnEmptyName_UnknownParish_AndMissingPermission()
    {
        var admin = await AdminAsync();
        var created = await (await admin.PostAsJsonAsync("/api/parishes", new { Name = "św. Marka" })).Content.ReadFromJsonAsync<ParishDto>();
        var katechista = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");

        var empty = await admin.PutAsJsonAsync($"/api/parishes/{created!.Id}", new { Name = "  " });
        var unknown = await admin.PutAsJsonAsync($"/api/parishes/{Guid.NewGuid()}", new { Name = "X" });
        var forbidden = await katechista.PutAsJsonAsync($"/api/parishes/{created.Id}", new { Name = "X" });

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    // --- Kampanie ---

    private static async Task<MailingCampaignDto> CreateCampaignAsync(HttpClient admin)
    {
        var response = await admin.PostAsJsonAsync("/api/mailing/campaigns", new { Subject = "Temat", Body = "Treść", Group = "DokCases" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MailingCampaignDto>(EnumJsonOptions))!;
    }

    [Fact]
    public async Task Campaign_DeleteDraft_RemovesItFromTheList()
    {
        var admin = await AdminAsync();
        var draft = await CreateCampaignAsync(admin);

        var response = await admin.DeleteAsync($"/api/mailing/campaigns/{draft.Id}");
        var list = await admin.GetFromJsonAsync<List<MailingCampaignDto>>("/api/mailing/campaigns", EnumJsonOptions);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.DoesNotContain(list!, c => c.Id == draft.Id);
    }

    [Fact]
    public async Task Campaign_Delete_RefusesSentCampaigns_ReportsUnknownOnes_AndNeedsPermission()
    {
        var admin = await AdminAsync();
        var sent = await CreateCampaignAsync(admin);
        (await admin.PostAsJsonAsync($"/api/mailing/campaigns/{sent.Id}/send", new { })).EnsureSuccessStatusCode();
        var draft = await CreateCampaignAsync(admin);
        var katechista = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");

        var refused = await admin.DeleteAsync($"/api/mailing/campaigns/{sent.Id}");
        var unknown = await admin.DeleteAsync($"/api/mailing/campaigns/{Guid.NewGuid()}");
        var forbidden = await katechista.DeleteAsync($"/api/mailing/campaigns/{draft.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }
}
