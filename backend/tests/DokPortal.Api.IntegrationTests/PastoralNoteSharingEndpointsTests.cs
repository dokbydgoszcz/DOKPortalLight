using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.PastoralNotes;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Poprzedni katechista udostępnia swoje notatki następcy – także gdy sam już nie widzi sprawy.</summary>
public class PastoralNoteSharingEndpointsTests : IntegrationTestBase
{
    public PastoralNoteSharingEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task ThePreviousCatechist_SharesTheirNotesWithTheSuccessor_AndCanRevokeIt()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var (caseId, previousId, studentId) = await SeedDokCaseAsync(admin, "Jan", $"Pod{Guid.NewGuid():N}"[..10]);
        var nextId = await SeedPersonAsync(admin, "Beata", "Nastepczyni");
        var strangerId = await SeedPersonAsync(admin, "Obcy", "Katechista");
        var previous = await CreateAuthenticatedClientForPersonAsync($"prev-{Guid.NewGuid():N}@example.org", "Sekret123!", previousId, "KatechistaProwadzacy");
        var next = await CreateAuthenticatedClientForPersonAsync($"next-{Guid.NewGuid():N}@example.org", "Sekret123!", nextId, "KatechistaProwadzacy");
        var stranger = await CreateAuthenticatedClientForPersonAsync($"obcy-{Guid.NewGuid():N}@example.org", "Sekret123!", strangerId, "KatechistaProwadzacy");
        (await previous.PostAsJsonAsync($"/api/dok-cases/{caseId}/notes", new { Content = "Ważna rozmowa" })).EnsureSuccessStatusCode();

        (await admin.PutAsJsonAsync($"/api/dok-cases/{caseId}", new
        {
            PersonId = studentId, Path = "Confirmation", Stage = "Evangelization", CatechistPersonId = nextId
        })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NotFound, (await previous.GetAsync($"/api/dok-cases/{caseId}")).StatusCode);
        Assert.Empty((await next.GetFromJsonAsync<List<PastoralNoteDto>>($"/api/dok-cases/{caseId}/notes"))!);
        var handedOver = await previous.GetFromJsonAsync<List<HandedOverCaseDto>>("/api/pastoral-notes/handed-over");
        Assert.Equal("Beata Nastepczyni", Assert.Single(handedOver!).CurrentCatechistFullName);

        var share = await previous.PostAsync($"/api/dok-cases/{caseId}/notes/share", null);
        var strangerShare = await stranger.PostAsync($"/api/dok-cases/{caseId}/notes/share", null);
        var seenByNext = await next.GetFromJsonAsync<List<PastoralNoteDto>>($"/api/dok-cases/{caseId}/notes");

        Assert.Equal(HttpStatusCode.OK, share.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, strangerShare.StatusCode);
        Assert.Equal("Ważna rozmowa", Assert.Single(seenByNext!).Content);

        var revoke = await previous.DeleteAsync($"/api/dok-cases/{caseId}/notes/share/{nextId}");
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        Assert.Empty((await next.GetFromJsonAsync<List<PastoralNoteDto>>($"/api/dok-cases/{caseId}/notes"))!);
    }
}
