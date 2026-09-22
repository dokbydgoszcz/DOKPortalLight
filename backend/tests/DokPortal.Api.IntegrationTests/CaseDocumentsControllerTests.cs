using System.Net.Http.Json;
using DokPortal.Application.CaseDocuments;
using DokPortal.Application.DokCases;
using DokPortal.Application.People;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class CaseDocumentsControllerTests : IntegrationTestBase
{
    public CaseDocumentsControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Create_ThenTogglesProvided_ReturnsUpdatedDocument()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var personResponse = await admin.PostAsJsonAsync("/api/people", new { FirstName = "Jan", LastName = "Kowalski" });
        var person = await personResponse.Content.ReadFromJsonAsync<PersonDto>();
        var catechistResponse = await admin.PostAsJsonAsync("/api/people", new { FirstName = "Anna", LastName = "Maj" });
        var catechist = await catechistResponse.Content.ReadFromJsonAsync<PersonDto>();
        var caseResponse = await admin.PostAsJsonAsync("/api/dok-cases", new
        {
            PersonId = person!.Id, Path = "Confirmation", Stage = "Formation", CatechistPersonId = catechist!.Id
        });
        var dokCase = await caseResponse.Content.ReadFromJsonAsync<DokCaseDto>(EnumJsonOptions);

        var createResponse = await admin.PostAsJsonAsync($"/api/dok-cases/{dokCase!.Id}/documents", new { Name = "Metryka chrztu" });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CaseDocumentDto>();

        var updateResponse = await admin.PutAsJsonAsync($"/api/dok-cases/{dokCase.Id}/documents/{created!.Id}", new { IsProvided = true });
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<CaseDocumentDto>();

        Assert.True(updated!.IsProvided);
    }
}
