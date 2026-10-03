using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.PastoralNotes;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class PastoralNotesControllerTests : IntegrationTestBase
{
    public PastoralNotesControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CaseCatechist_SeesOwnNote_ButNotNotesWrittenByOthers()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var (caseId, catechistPersonId, _) = await SeedDokCaseAsync(admin);
        var catechist = await CreateAuthenticatedClientForPersonAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", catechistPersonId, "KatechistaProwadzacy");
        var director = await CreateAuthenticatedClientAsync($"dyr-{Guid.NewGuid():N}@example.org", "Sekret123!", "DyrektorDOK");

        await catechist.PostAsJsonAsync($"/api/dok-cases/{caseId}/notes", new { Content = "Notatka katechisty" });
        await director.PostAsJsonAsync($"/api/dok-cases/{caseId}/notes", new { Content = "Notatka dyrektora" });

        var response = await catechist.GetAsync($"/api/dok-cases/{caseId}/notes");
        var notes = await response.Content.ReadFromJsonAsync<List<PastoralNoteDto>>();
        Assert.Single(notes!);
        Assert.Equal("Notatka katechisty", notes![0].Content);
    }

    [Fact]
    public async Task AnotherCatechist_CannotReadOrWriteNotesOfSomeoneElsesCase()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var (caseId, _, _) = await SeedDokCaseAsync(admin);
        var otherPersonId = await SeedPersonAsync(admin, "Beata", "Lis");
        var other = await CreateAuthenticatedClientForPersonAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", otherPersonId, "KatechistaProwadzacy");

        var read = await other.GetAsync($"/api/dok-cases/{caseId}/notes");
        var write = await other.PostAsJsonAsync($"/api/dok-cases/{caseId}/notes", new { Content = "Wtargnięcie" });

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, write.StatusCode);
    }

    [Fact]
    public async Task DyrektorDOK_SeesAllNotesForCase()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var (caseId, catechistPersonId, _) = await SeedDokCaseAsync(admin);
        var catechist = await CreateAuthenticatedClientForPersonAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", catechistPersonId, "KatechistaProwadzacy");
        await catechist.PostAsJsonAsync($"/api/dok-cases/{caseId}/notes", new { Content = "Notatka poufna" });

        var director = await CreateAuthenticatedClientAsync($"dyr-{Guid.NewGuid():N}@example.org", "Sekret123!", "DyrektorDOK");
        var response = await director.GetAsync($"/api/dok-cases/{caseId}/notes");
        var notes = await response.Content.ReadFromJsonAsync<List<PastoralNoteDto>>();

        Assert.Single(notes!);
        Assert.Equal("Notatka poufna", notes![0].Content);
    }

    [Fact]
    public async Task UnknownCase_ReturnsNotFound()
    {
        var director = await CreateAuthenticatedClientAsync($"dyr-{Guid.NewGuid():N}@example.org", "Sekret123!", "DyrektorDOK");

        var response = await director.GetAsync($"/api/dok-cases/{Guid.NewGuid()}/notes");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
