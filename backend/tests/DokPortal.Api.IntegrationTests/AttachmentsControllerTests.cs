using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using DokPortal.Application.Attachments;
using DokPortal.Application.AuditLog;
using DokPortal.Application.PastoralNotes;
using DokPortal.Application.Supervisions;
using DokPortal.Domain.Enums;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Pliki dołączane do notatek duszpasterskich i superwizji oraz zasady typów i rozmiaru plików.</summary>
public class AttachmentsControllerTests : IntegrationTestBase, IClassFixture<FakeStorageFactory>
{
    public AttachmentsControllerTests(CustomWebApplicationFactory unused, FakeStorageFactory factory) : base(factory)
    {
    }

    private static MultipartFormDataContent File(string fileName, string text = "PDF-DATA", string contentType = "application/octet-stream")
    {
        var content = new MultipartFormDataContent();
        var part = new ByteArrayContent(Encoding.UTF8.GetBytes(text));
        part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(part, "file", fileName);
        return content;
    }

    private async Task<HttpClient> AdminAsync() =>
        await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

    private async Task<HttpClient> CatechistOfAsync(Guid personId) =>
        await CreateAuthenticatedClientForPersonAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", personId, "KatechistaProwadzacy");

    private async Task<HttpClient> UserAsync(string role) =>
        await CreateAuthenticatedClientAsync($"{role.ToLower()}-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

    private static async Task<PastoralNoteDto> CreateNoteAsync(HttpClient client, Guid caseId, string text = "Notatka")
    {
        var response = await client.PostAsJsonAsync($"/api/dok-cases/{caseId}/notes", new { Content = text });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PastoralNoteDto>())!;
    }

    private static async Task<SupervisionDto> CreateSupervisionAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/supervisions", new
        {
            Institution = "DOK", GroupLabel = $"Grupa {Guid.NewGuid():N}".Substring(0, 12), SupervisionDate = "2026-10-01"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SupervisionDto>(EnumJsonOptions))!;
    }

    // ------------------------------------------------------------------ pastoral notes

    [Fact]
    public async Task NoteAuthor_UploadsListsDownloadsAndDeletesAnAttachment()
    {
        var admin = await AdminAsync();
        var (caseId, catechistPersonId, _) = await SeedDokCaseAsync(admin);
        var catechist = await CatechistOfAsync(catechistPersonId);
        var note = await CreateNoteAsync(catechist, caseId);

        var upload = await catechist.PostAsync($"/api/dok-cases/{caseId}/notes/{note.Id}/attachments", File("kindle.pdf", "SKAN-1", "application/x-whatever"));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var attachment = (await upload.Content.ReadFromJsonAsync<AttachmentDto>())!;
        Assert.Equal("kindle.pdf", attachment.FileName);
        Assert.Equal("application/pdf", attachment.ContentType);

        var notes = (await catechist.GetFromJsonAsync<List<PastoralNoteDto>>($"/api/dok-cases/{caseId}/notes"))!;
        Assert.Equal("kindle.pdf", notes.Single(n => n.Id == note.Id).Attachments.Single().FileName);

        var download = await catechist.GetAsync($"/api/dok-cases/{caseId}/notes/{note.Id}/attachments/{attachment.Id}/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType!.MediaType);
        Assert.Equal("kindle.pdf", download.Content.Headers.ContentDisposition!.FileNameStar ?? download.Content.Headers.ContentDisposition.FileName);
        Assert.Equal("SKAN-1", await download.Content.ReadAsStringAsync());

        var delete = await catechist.DeleteAsync($"/api/dok-cases/{caseId}/notes/{note.Id}/attachments/{attachment.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var after = (await catechist.GetFromJsonAsync<List<PastoralNoteDto>>($"/api/dok-cases/{caseId}/notes"))!;
        Assert.Empty(after.Single(n => n.Id == note.Id).Attachments);
        var gone = await catechist.GetAsync($"/api/dok-cases/{caseId}/notes/{note.Id}/attachments/{attachment.Id}/download");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Theory]
    [InlineData("zdjecie.JPG")]
    [InlineData("zrzut.png")]
    [InlineData("pismo.docx")]
    [InlineData("pismo.doc")]
    [InlineData("notatka.txt")]
    public async Task NoteAttachment_AcceptsTheAllowedFileTypes(string fileName)
    {
        var admin = await AdminAsync();
        var (caseId, catechistPersonId, _) = await SeedDokCaseAsync(admin);
        var catechist = await CatechistOfAsync(catechistPersonId);
        var note = await CreateNoteAsync(catechist, caseId);

        var upload = await catechist.PostAsync($"/api/dok-cases/{caseId}/notes/{note.Id}/attachments", File(fileName));

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
    }

    [Fact]
    public async Task NoteAttachment_RejectsAForbiddenTypeAndAnEmptyFile_WithAClearMessage()
    {
        var admin = await AdminAsync();
        var (caseId, catechistPersonId, _) = await SeedDokCaseAsync(admin);
        var catechist = await CatechistOfAsync(catechistPersonId);
        var note = await CreateNoteAsync(catechist, caseId);
        var url = $"/api/dok-cases/{caseId}/notes/{note.Id}/attachments";

        var exe = await catechist.PostAsync(url, File("program.exe"));
        var empty = await catechist.PostAsync(url, File("pusty.pdf", ""));

        Assert.Equal(HttpStatusCode.BadRequest, exe.StatusCode);
        Assert.Contains("Niedozwolony typ pliku", await exe.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Contains("Plik jest pusty", await empty.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task NoteAttachment_OfAnotherAuthorsNote_IsHiddenFromTheCatechist_AndTheAttemptIsAudited()
    {
        var admin = await AdminAsync();
        var (caseId, catechistPersonId, _) = await SeedDokCaseAsync(admin);
        var catechist = await CatechistOfAsync(catechistPersonId);
        var director = await UserAsync("DyrektorDOK");
        var directorsNote = await CreateNoteAsync(director, caseId, "Notatka dyrektora");
        var directorsFile = (await (await director.PostAsync($"/api/dok-cases/{caseId}/notes/{directorsNote.Id}/attachments", File("poufne.pdf")))
            .Content.ReadFromJsonAsync<AttachmentDto>())!;
        var url = $"/api/dok-cases/{caseId}/notes/{directorsNote.Id}/attachments";

        var download = await catechist.GetAsync($"{url}/{directorsFile.Id}/download");
        var upload = await catechist.PostAsync(url, File("wtargniecie.pdf"));
        var delete = await catechist.DeleteAsync($"{url}/{directorsFile.Id}");

        Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, upload.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        var stillThere = (await director.GetFromJsonAsync<List<PastoralNoteDto>>($"/api/dok-cases/{caseId}/notes"))!;
        Assert.Single(stillThere.Single(n => n.Id == directorsNote.Id).Attachments);
        var log = (await admin.GetFromJsonAsync<List<AuditLogEntryDto>>("/api/audit-log?action=DownloadPastoralNoteAttachment", EnumJsonOptions))!;
        Assert.Contains(log, e => e.Result == AuditResult.Blocked);
    }

    [Fact]
    public async Task DirectorWithReadAll_DownloadsACatechistsAttachment_AndTheDownloadIsAudited()
    {
        var admin = await AdminAsync();
        var (caseId, catechistPersonId, _) = await SeedDokCaseAsync(admin);
        var catechist = await CatechistOfAsync(catechistPersonId);
        var note = await CreateNoteAsync(catechist, caseId);
        var file = (await (await catechist.PostAsync($"/api/dok-cases/{caseId}/notes/{note.Id}/attachments", File("scan.pdf", "TRESC")))
            .Content.ReadFromJsonAsync<AttachmentDto>())!;
        var director = await UserAsync("DyrektorDOK");

        var download = await director.GetAsync($"/api/dok-cases/{caseId}/notes/{note.Id}/attachments/{file.Id}/download");

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("TRESC", await download.Content.ReadAsStringAsync());
        var log = (await admin.GetFromJsonAsync<List<AuditLogEntryDto>>("/api/audit-log?action=DownloadPastoralNoteAttachment", EnumJsonOptions))!;
        Assert.Contains(log, e => e.Result == AuditResult.Allowed && e.ObjectDescription.Contains("Kowalski"));
    }

    [Fact]
    public async Task NoteAttachment_OfACaseOutsideTheCatechistsScope_IsNotFound()
    {
        var admin = await AdminAsync();
        var (caseId, _, _) = await SeedDokCaseAsync(admin);
        var note = await CreateNoteAsync(admin, caseId);
        var otherPersonId = await SeedPersonAsync(admin, "Beata", "Lis");
        var outsider = await CatechistOfAsync(otherPersonId);
        var file = (await (await admin.PostAsync($"/api/dok-cases/{caseId}/notes/{note.Id}/attachments", File("a.pdf")))
            .Content.ReadFromJsonAsync<AttachmentDto>())!;
        var url = $"/api/dok-cases/{caseId}/notes/{note.Id}/attachments";

        Assert.Equal(HttpStatusCode.NotFound, (await outsider.PostAsync(url, File("b.pdf"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"{url}/{file.Id}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.DeleteAsync($"{url}/{file.Id}")).StatusCode);
    }

    [Fact]
    public async Task NoteAttachment_ForAnUnknownNoteOrFile_IsNotFound()
    {
        var admin = await AdminAsync();
        var (caseId, _, _) = await SeedDokCaseAsync(admin);
        var note = await CreateNoteAsync(admin, caseId);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync($"/api/dok-cases/{caseId}/notes/{Guid.NewGuid()}/attachments", File("a.pdf"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/dok-cases/{caseId}/notes/{note.Id}/attachments/{Guid.NewGuid()}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/dok-cases/{caseId}/notes/{note.Id}/attachments/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task NoteAttachment_WithoutPastoralNotesPermissions_IsForbidden()
    {
        var admin = await AdminAsync();
        var (caseId, _, _) = await SeedDokCaseAsync(admin);
        var note = await CreateNoteAsync(admin, caseId);
        var supervisor = await UserAsync("Superwizor");
        var url = $"/api/dok-cases/{caseId}/notes/{note.Id}/attachments";

        Assert.Equal(HttpStatusCode.Forbidden, (await supervisor.PostAsync(url, File("a.pdf"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await supervisor.GetAsync($"{url}/{Guid.NewGuid()}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await supervisor.DeleteAsync($"{url}/{Guid.NewGuid()}")).StatusCode);
    }

    // ------------------------------------------------------------------ supervisions

    [Fact]
    public async Task Supervision_AttachmentLifecycle_UploadListDownloadDelete()
    {
        var supervisor = await UserAsync("Superwizor");
        var supervision = await CreateSupervisionAsync(supervisor);

        var upload = await supervisor.PostAsync($"/api/supervisions/{supervision.Id}/attachments", File("protokol.docx", "PROTOKOL"));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var attachment = (await upload.Content.ReadFromJsonAsync<AttachmentDto>())!;

        var fetched = (await supervisor.GetFromJsonAsync<SupervisionDto>($"/api/supervisions/{supervision.Id}", EnumJsonOptions))!;
        Assert.Equal("protokol.docx", fetched.Attachments.Single().FileName);
        var list = (await supervisor.GetFromJsonAsync<List<SupervisionDto>>("/api/supervisions", EnumJsonOptions))!;
        Assert.Single(list.Single(s => s.Id == supervision.Id).Attachments);

        var download = await supervisor.GetAsync($"/api/supervisions/{supervision.Id}/attachments/{attachment.Id}/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("PROTOKOL", await download.Content.ReadAsStringAsync());

        var delete = await supervisor.DeleteAsync($"/api/supervisions/{supervision.Id}/attachments/{attachment.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var after = (await supervisor.GetFromJsonAsync<SupervisionDto>($"/api/supervisions/{supervision.Id}", EnumJsonOptions))!;
        Assert.Empty(after.Attachments);
    }

    [Fact]
    public async Task Supervision_RejectsAForbiddenTypeAndATooBigFile()
    {
        var supervisor = await UserAsync("Superwizor");
        var supervision = await CreateSupervisionAsync(supervisor);
        var url = $"/api/supervisions/{supervision.Id}/attachments";

        var exe = await supervisor.PostAsync(url, File("program.exe"));
        var tooBig = new MultipartFormDataContent();
        var big = new ByteArrayContent(new byte[(int)AttachmentRules.MaxFileBytes + 1]);
        tooBig.Add(big, "file", "duzy.pdf");
        var huge = await supervisor.PostAsync(url, tooBig);

        Assert.Equal(HttpStatusCode.BadRequest, exe.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, huge.StatusCode);
        Assert.Contains("20 MB", await huge.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Supervision_AttachmentsAreForbiddenWithoutThePermissions_AndNotFoundForUnknownIds()
    {
        var supervisor = await UserAsync("Superwizor");
        var supervision = await CreateSupervisionAsync(supervisor);
        var bishop = await UserAsync("Biskup");
        var url = $"/api/supervisions/{supervision.Id}/attachments";

        Assert.Equal(HttpStatusCode.Forbidden, (await bishop.PostAsync(url, File("a.pdf"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bishop.GetAsync($"{url}/{Guid.NewGuid()}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bishop.DeleteAsync($"{url}/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await supervisor.PostAsync($"/api/supervisions/{Guid.NewGuid()}/attachments", File("a.pdf"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await supervisor.GetAsync($"{url}/{Guid.NewGuid()}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await supervisor.DeleteAsync($"{url}/{Guid.NewGuid()}")).StatusCode);
    }

    // ------------------------------------------------------------------ case documents

    [Fact]
    public async Task CaseDocumentUpload_FollowsTheSameTypeRules()
    {
        var admin = await AdminAsync();
        var (caseId, _, _) = await SeedDokCaseAsync(admin);
        var created = await admin.PostAsJsonAsync($"/api/dok-cases/{caseId}/documents", new { Name = "Metryka chrztu" });
        var documentId = (await created.Content.ReadFromJsonAsync<DokPortal.Application.CaseDocuments.CaseDocumentDto>())!.Id;
        var url = $"/api/dok-cases/{caseId}/documents/{documentId}/upload";

        var bad = await admin.PostAsync(url, File("arkusz.xlsx"));
        var good = await admin.PostAsync(url, File("metryka.png"));

        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Contains("Niedozwolony typ pliku", await bad.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, good.StatusCode);
    }
}
