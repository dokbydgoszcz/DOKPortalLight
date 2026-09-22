using System.Net.Http.Json;
using DokPortal.Application.DokCases;
using DokPortal.Application.PastoralNotes;
using DokPortal.Application.People;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class PastoralNotesControllerTests : IntegrationTestBase
{
    public PastoralNotesControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private async Task<Guid> CreateDokCaseAsync(HttpClient admin)
    {
        var personResponse = await admin.PostAsJsonAsync("/api/people", new { FirstName = "Jan", LastName = "Kowalski" });
        var person = await personResponse.Content.ReadFromJsonAsync<PersonDto>();
        var catechistResponse = await admin.PostAsJsonAsync("/api/people", new { FirstName = "Anna", LastName = "Maj" });
        var catechist = await catechistResponse.Content.ReadFromJsonAsync<PersonDto>();
        var caseResponse = await admin.PostAsJsonAsync("/api/dok-cases", new
        {
            PersonId = person!.Id, Path = "Confirmation", Stage = "Formation", CatechistPersonId = catechist!.Id
        });
        var dokCase = await caseResponse.Content.ReadFromJsonAsync<DokCaseDto>(EnumJsonOptions);
        return dokCase!.Id;
    }

    [Fact]
    public async Task Author_SeesOwnNote_ButNotAnotherKatechistasNote()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var caseId = await CreateDokCaseAsync(admin);

        var katechistaA = await CreateAuthenticatedClientAsync($"kat-a-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");
        var katechistaB = await CreateAuthenticatedClientAsync($"kat-b-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");

        await katechistaA.PostAsJsonAsync($"/api/dok-cases/{caseId}/notes", new { Content = "Notatka katechisty A" });

        var responseForA = await katechistaA.GetAsync($"/api/dok-cases/{caseId}/notes");
        var notesForA = await responseForA.Content.ReadFromJsonAsync<List<PastoralNoteDto>>();
        Assert.Single(notesForA!);

        var responseForB = await katechistaB.GetAsync($"/api/dok-cases/{caseId}/notes");
        var notesForB = await responseForB.Content.ReadFromJsonAsync<List<PastoralNoteDto>>();
        Assert.Empty(notesForB!);
    }

    [Fact]
    public async Task DyrektorDOK_SeesAllNotesForCase()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var caseId = await CreateDokCaseAsync(admin);

        var katechista = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");
        await katechista.PostAsJsonAsync($"/api/dok-cases/{caseId}/notes", new { Content = "Notatka poufna" });

        var director = await CreateAuthenticatedClientAsync($"dyr-{Guid.NewGuid():N}@example.org", "Sekret123!", "DyrektorDOK");
        var response = await director.GetAsync($"/api/dok-cases/{caseId}/notes");
        var notes = await response.Content.ReadFromJsonAsync<List<PastoralNoteDto>>();

        Assert.Single(notes!);
        Assert.Equal("Notatka poufna", notes![0].Content);
    }
}
