using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.CaseDocuments;
using DokPortal.Application.DokCases;
using DokPortal.Application.PastoralNotes;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Edycja sprawy DOK z historią, poprawianie/usuwanie notatek i dokumentów sprawy przez API.</summary>
public class DokProcessEndpointsTests : IntegrationTestBase
{
    public DokProcessEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task ChangingTheStage_IsVisibleInTheHistory_WithTheAuthor()
    {
        var email = $"admin-{Guid.NewGuid():N}@example.org";
        var admin = await CreateAuthenticatedClientAsync(email, "Sekret123!", "Administrator");
        var (caseId, catechistId, studentId) = await SeedDokCaseAsync(admin);

        var update = await admin.PutAsJsonAsync($"/api/dok-cases/{caseId}", new
        {
            PersonId = studentId, Path = "Confirmation", Stage = "Graduate", CatechistPersonId = catechistId
        });
        var history = await admin.GetFromJsonAsync<List<DokCaseEventDto>>($"/api/dok-cases/{caseId}/history");

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var entry = Assert.Single(history!);
        Assert.Equal("Etap: Ewangelizacja → Absolwent", entry.Description);
        Assert.Equal(email, entry.PerformedByEmail);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/dok-cases/{Guid.NewGuid()}/history")).StatusCode);
    }

    [Fact]
    public async Task ChangingTheStudentOfACase_IsRejected()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var (caseId, catechistId, _) = await SeedDokCaseAsync(admin);
        var other = await SeedPersonAsync(admin, "Inny", "Podopieczny");

        var response = await admin.PutAsJsonAsync($"/api/dok-cases/{caseId}", new
        {
            PersonId = other, Path = "Confirmation", Stage = "Evangelization", CatechistPersonId = catechistId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("podopiecznego", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TheAuthor_EditsAndDeletesTheirNote_ButSomeoneElseCannot()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var other = await CreateAuthenticatedClientAsync($"admin2-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var (caseId, _, _) = await SeedDokCaseAsync(admin);
        var note = await (await admin.PostAsJsonAsync($"/api/dok-cases/{caseId}/notes", new { Content = "Pierwsza" }))
            .Content.ReadFromJsonAsync<PastoralNoteDto>();

        var foreignEdit = await other.PutAsJsonAsync($"/api/dok-cases/{caseId}/notes/{note!.Id}", new { Content = "x" });
        var foreignDelete = await other.DeleteAsync($"/api/dok-cases/{caseId}/notes/{note.Id}");
        var edit = await admin.PutAsJsonAsync($"/api/dok-cases/{caseId}/notes/{note.Id}", new { Content = "Poprawiona" });
        var edited = await edit.Content.ReadFromJsonAsync<PastoralNoteDto>();
        var delete = await admin.DeleteAsync($"/api/dok-cases/{caseId}/notes/{note.Id}");

        Assert.Equal(HttpStatusCode.NotFound, foreignEdit.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignDelete.StatusCode);
        Assert.Equal("Poprawiona", edited!.Content);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Empty((await admin.GetFromJsonAsync<List<PastoralNoteDto>>($"/api/dok-cases/{caseId}/notes"))!);
    }

    [Fact]
    public async Task ACaseDocument_CanBeRenamedAndDeleted()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var (caseId, _, _) = await SeedDokCaseAsync(admin);
        var document = await (await admin.PostAsJsonAsync($"/api/dok-cases/{caseId}/documents", new { Name = "Metryka" }))
            .Content.ReadFromJsonAsync<CaseDocumentDto>();

        var renamed = await (await admin.PutAsJsonAsync($"/api/dok-cases/{caseId}/documents/{document!.Id}/name", new { Name = "Metryka chrztu" }))
            .Content.ReadFromJsonAsync<CaseDocumentDto>();
        var delete = await admin.DeleteAsync($"/api/dok-cases/{caseId}/documents/{document.Id}");

        Assert.Equal("Metryka chrztu", renamed!.Name);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Empty((await admin.GetFromJsonAsync<List<CaseDocumentDto>>($"/api/dok-cases/{caseId}/documents"))!);
    }
}
