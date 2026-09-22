using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Documents;
using DokPortal.Application.People;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class DocumentsControllerTests : IntegrationTestBase
{
    public DocumentsControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Generate_AsAdministrator_ReturnsPdfAndRecordsHistory()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var personResponse = await admin.PostAsJsonAsync("/api/people", new { FirstName = "Jan", LastName = "Kowalski" });
        var person = await personResponse.Content.ReadFromJsonAsync<PersonDto>();

        var response = await admin.PostAsJsonAsync("/api/documents/generate", new
        {
            Template = "LetterToBishop", PersonId = person!.Id, AdditionalNotes = "Prośba o wydanie dekretu"
        });

        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);

        var historyResponse = await admin.GetAsync("/api/documents");
        var history = await historyResponse.Content.ReadFromJsonAsync<List<GeneratedDocumentDto>>(EnumJsonOptions);
        Assert.Single(history!);
        Assert.Equal("Jan Kowalski", history![0].PersonFullName);
    }

    [Fact]
    public async Task Generate_AsKatechista_IsForbidden()
    {
        var katechista = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");

        var response = await katechista.PostAsJsonAsync("/api/documents/generate", new
        {
            Template = "LetterToBishop", PersonId = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
