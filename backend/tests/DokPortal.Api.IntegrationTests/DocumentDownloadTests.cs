using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.AuditLog;
using DokPortal.Application.Documents;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class DocumentDownloadTests : IntegrationTestBase, IClassFixture<FakeStorageFactory>
{
    public DocumentDownloadTests(CustomWebApplicationFactory unused, FakeStorageFactory factory) : base(factory)
    {
    }

    private async Task<(HttpClient Admin, Guid DocumentId, byte[] Pdf)> GenerateAsync()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var personId = await SeedPersonAsync(admin, "Jan", $"Kowalski{Guid.NewGuid():N}".Substring(0, 14));
        var response = await admin.PostAsJsonAsync("/api/documents/generate", new { Template = "LetterToBishop", PersonId = personId, AdditionalNotes = "Uwagi" });
        response.EnsureSuccessStatusCode();
        var pdf = await response.Content.ReadAsByteArrayAsync();
        var history = await admin.GetFromJsonAsync<List<GeneratedDocumentDto>>("/api/documents", EnumJsonOptions);
        return (admin, history!.First(d => d.PersonId == personId).Id, pdf);
    }

    [Fact]
    public async Task Download_ReturnsTheSameStoredPdf_AndCountsEachDownload()
    {
        var (admin, id, pdf) = await GenerateAsync();

        var first = await admin.GetAsync($"/api/documents/{id}/download");
        var second = await admin.GetAsync($"/api/documents/{id}/download");
        var history = await admin.GetFromJsonAsync<List<GeneratedDocumentDto>>("/api/documents", EnumJsonOptions);

        Assert.Equal("application/pdf", first.Content.Headers.ContentType!.MediaType);
        Assert.Equal(pdf, await first.Content.ReadAsByteArrayAsync());
        Assert.Equal(pdf, await second.Content.ReadAsByteArrayAsync());
        var entry = history!.Single(d => d.Id == id);
        Assert.True(entry.HasStoredFile);
        Assert.Equal(3, entry.DownloadCount);
    }

    [Fact]
    public async Task Delete_RemovesTheEntry_AndIsAudited()
    {
        var (admin, id, _) = await GenerateAsync();

        var delete = await admin.DeleteAsync($"/api/documents/{id}");
        var history = await admin.GetFromJsonAsync<List<GeneratedDocumentDto>>("/api/documents", EnumJsonOptions);
        var download = await admin.GetAsync($"/api/documents/{id}/download");
        var log = await admin.GetFromJsonAsync<List<AuditLogEntryDto>>("/api/audit-log", EnumJsonOptions);

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.DoesNotContain(history!, d => d.Id == id);
        Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
        Assert.Contains(log!, e => e.Action == "DeleteGeneratedDocument");
    }

    [Fact]
    public async Task UnknownDocument_ReturnsNotFound_ForDownloadAndDelete()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/documents/{Guid.NewGuid()}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/documents/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Permissions_ViewMayDownload_ButOnlyGenerateMayDelete()
    {
        var (admin, id, _) = await GenerateAsync();
        var katechista = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");
        var director = await CreateAuthenticatedClientAsync($"dyr-{Guid.NewGuid():N}@example.org", "Sekret123!", "DyrektorDOK");

        Assert.Equal(HttpStatusCode.Forbidden, (await katechista.GetAsync($"/api/documents/{id}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await katechista.DeleteAsync($"/api/documents/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await director.GetAsync($"/api/documents/{id}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await director.DeleteAsync($"/api/documents/{id}")).StatusCode);
        Assert.NotNull(admin);
    }
}
