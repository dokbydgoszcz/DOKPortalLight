using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DokPortal.Application.Documents;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Wzory pism przez API: edycja (uprawnienie), podgląd, generowanie PDF/Word, nagłówek z logo.</summary>
public class DocumentTemplatesEndpointsTests : IntegrationTestBase, IClassFixture<FakeStorageFactory>
{
    public DocumentTemplatesEndpointsTests(CustomWebApplicationFactory unused, FakeStorageFactory factory) : base(factory)
    {
    }

    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    [Fact]
    public async Task TemplatesAreEditedWithThePermission_PreviewedAndUsedForPdfAndWord()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var catechist = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "Katechista");
        var personId = await SeedPersonAsync(admin, "Jan", $"Wzorowy{Guid.NewGuid():N}"[..14]);

        var starters = await admin.GetFromJsonAsync<List<LetterTemplateDto>>("/api/document-templates");
        Assert.True(starters!.Count >= 11);
        Assert.NotEmpty((await admin.GetFromJsonAsync<List<LetterFieldDto>>("/api/document-templates/fields"))!);
        Assert.Equal(HttpStatusCode.Forbidden, (await catechist.PostAsJsonAsync("/api/document-templates", new { Name = "x", Body = "<p>x</p>" })).StatusCode);

        var body = "<p>Dla {{osoba.imię i nazwisko}}, chrzest {{wpisz:Data chrztu}}, misja {{misja.od}}</p>";
        var created = await (await admin.PostAsJsonAsync("/api/document-templates", new { Name = "Test API", Body = body })).Content.ReadFromJsonAsync<LetterTemplateDto>();
        Assert.Equal(new[] { "Data chrztu" }, created!.Inputs);
        Assert.Equal("Test API", (await admin.GetFromJsonAsync<LetterTemplateDto>($"/api/document-templates/{created.Id}"))!.Name);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/document-templates", new { Name = "Pusty", Body = "<p></p>" })).StatusCode);

        var preview = await admin.PostAsJsonAsync("/api/document-templates/preview", new { Body = body, PersonId = personId });
        Assert.Equal("application/pdf", preview.Content.Headers.ContentType!.MediaType);
        var missing = Uri.UnescapeDataString(preview.Headers.GetValues("X-Missing-Fields").Single()).Split('|');
        Assert.Contains("Data chrztu", missing);
        Assert.Contains("Misja od", missing);

        var generated = await admin.PostAsJsonAsync("/api/documents/generate", new
        {
            LetterTemplateId = created.Id, PersonId = personId, Format = "docx", Inputs = new Dictionary<string, string> { ["Data chrztu"] = "1 maja" }
        });
        Assert.Equal(HttpStatusCode.OK, generated.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", generated.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith("Test-API_Wzorowy", generated.Content.Headers.ContentDisposition!.FileName!.Trim('"'));

        var history = await admin.GetFromJsonAsync<List<GeneratedDocumentDto>>("/api/documents", EnumJsonOptions);
        var entry = history!.First(d => d.LetterTemplateId == created.Id);
        Assert.Equal(("Test API", true), (entry.Title, entry.CanDownloadWord));
        Assert.Equal("application/pdf", (await admin.GetAsync($"/api/documents/{entry.Id}/download")).Content.Headers.ContentType!.MediaType);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            (await admin.GetAsync($"/api/documents/{entry.Id}/download?format=docx")).Content.Headers.ContentType!.MediaType);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/document-templates/{created.Id}")).StatusCode);
        var disabled = await admin.PutAsJsonAsync($"/api/document-templates/{created.Id}", new { Name = "Test API", Body = body, IsActive = false });
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
        Assert.DoesNotContain((await admin.GetFromJsonAsync<List<LetterTemplateDto>>("/api/document-templates"))!, t => t.Id == created.Id);
        Assert.Contains((await admin.GetFromJsonAsync<List<LetterTemplateDto>>("/api/document-templates?all=true"))!, t => t.Id == created.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"/api/document-templates/{Guid.NewGuid()}", new { Name = "x", Body = "<p>x</p>" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/document-templates/{Guid.NewGuid()}")).StatusCode);

        var removable = await (await admin.PostAsJsonAsync("/api/document-templates", new { Name = "Do usunięcia", Body = "<p>x</p>" })).Content.ReadFromJsonAsync<LetterTemplateDto>();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/document-templates/{removable!.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/document-templates/{removable.Id}")).StatusCode);
    }

    [Fact]
    public async Task TheLetterheadAndLogo_CanBeSetAndRemoved()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

        var saved = await (await admin.PutAsJsonAsync("/api/document-templates/letterhead", new { HeaderText = "DOK\nul. Grodzka", FooterText = "stopka", Place = "Bydgoszcz" }))
            .Content.ReadFromJsonAsync<LetterheadDto>();
        Assert.Equal("Bydgoszcz", saved!.Place);
        Assert.Equal("stopka", (await admin.GetFromJsonAsync<LetterheadDto>("/api/document-templates/letterhead"))!.FooterText);

        var file = new ByteArrayContent(Png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var upload = await admin.PutAsync("/api/document-templates/letterhead/logo", new MultipartFormDataContent { { file, "file", "logo.png" } });
        Assert.True((await upload.Content.ReadFromJsonAsync<LetterheadDto>())!.HasLogo);
        var logo = await admin.GetAsync("/api/document-templates/letterhead/logo");
        Assert.Equal(Png, await logo.Content.ReadAsByteArrayAsync());

        Assert.False((await (await admin.DeleteAsync("/api/document-templates/letterhead/logo")).Content.ReadFromJsonAsync<LetterheadDto>())!.HasLogo);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/document-templates/letterhead/logo")).StatusCode);
    }
}
