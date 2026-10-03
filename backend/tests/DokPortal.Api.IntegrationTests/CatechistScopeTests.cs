using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.CaseDocuments;
using DokPortal.Application.Common;
using DokPortal.Application.Dashboard;
using DokPortal.Application.DokCases;
using DokPortal.Application.Meetings;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Katechista widzi i zmienia tylko dane swoich podopiecznych; pozostałe role z DokCases.ViewAll widzą wszystko.</summary>
public class CatechistScopeTests : IntegrationTestBase
{
    public CatechistScopeTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private sealed record World(
        HttpClient Admin, HttpClient CatechistA, HttpClient CatechistB,
        Guid CaseA, Guid CaseB, string StudentA, string StudentB, string Marker);

    private async Task<World> SetUpAsync()
    {
        var marker = Guid.NewGuid().ToString("N")[..8];
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var studentA = $"Podopieczny-A{marker}";
        var studentB = $"Podopieczny-B{marker}";
        var (caseA, catechistA, _) = await SeedDokCaseAsync(admin, "Jan", studentA, "Anna", $"KatA{marker}");
        var (caseB, catechistB, _) = await SeedDokCaseAsync(admin, "Piotr", studentB, "Beata", $"KatB{marker}");
        var clientA = await CreateAuthenticatedClientForPersonAsync($"kat-a-{Guid.NewGuid():N}@example.org", "Sekret123!", catechistA, "KatechistaProwadzacy");
        var clientB = await CreateAuthenticatedClientForPersonAsync($"kat-b-{Guid.NewGuid():N}@example.org", "Sekret123!", catechistB, "KatechistaProwadzacy");
        return new World(admin, clientA, clientB, caseA, caseB, studentA, studentB, marker);
    }

    private static async Task<List<string>> CaseNamesAsync(HttpClient client)
    {
        var page = await client.GetFromJsonAsync<PagedResult<DokCaseDto>>("/api/dok-cases?pageSize=100", EnumJsonOptions);
        return page!.Items.Select(i => i.PersonFullName).ToList();
    }

    [Fact]
    public async Task CaseList_ShowsEachCatechistOnlyTheirOwnStudents_AndAdminEverything()
    {
        var w = await SetUpAsync();

        var namesA = await CaseNamesAsync(w.CatechistA);
        var namesB = await CaseNamesAsync(w.CatechistB);
        var namesAdmin = await CaseNamesAsync(w.Admin);

        Assert.Equal(new[] { $"Jan {w.StudentA}" }, namesA);
        Assert.Equal(new[] { $"Piotr {w.StudentB}" }, namesB);
        Assert.Contains($"Jan {w.StudentA}", namesAdmin);
        Assert.Contains($"Piotr {w.StudentB}", namesAdmin);
    }

    [Fact]
    public async Task GetCase_OfSomeoneElse_ReturnsNotFound()
    {
        var w = await SetUpAsync();

        Assert.Equal(HttpStatusCode.OK, (await w.CatechistA.GetAsync($"/api/dok-cases/{w.CaseA}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await w.CatechistA.GetAsync($"/api/dok-cases/{w.CaseB}")).StatusCode);
    }

    [Fact]
    public async Task Documents_CanBeAddedAndReadOnOwnCaseOnly()
    {
        var w = await SetUpAsync();

        var created = await w.CatechistA.PostAsJsonAsync($"/api/dok-cases/{w.CaseA}/documents", new { Name = "Metryka chrztu" });
        var foreign = await w.CatechistA.PostAsJsonAsync($"/api/dok-cases/{w.CaseB}/documents", new { Name = "Wtargnięcie" });
        var ownList = await w.CatechistA.GetFromJsonAsync<List<CaseDocumentDto>>($"/api/dok-cases/{w.CaseA}/documents");
        var foreignList = await w.CatechistA.GetFromJsonAsync<List<CaseDocumentDto>>($"/api/dok-cases/{w.CaseB}/documents");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Single(ownList!);
        Assert.Empty(foreignList!);
        Assert.Empty(await w.Admin.GetFromJsonAsync<List<CaseDocumentDto>>($"/api/dok-cases/{w.CaseB}/documents") ?? new());
    }

    [Fact]
    public async Task Documents_UploadAndDownloadOfAForeignCaseAreNotFound()
    {
        var w = await SetUpAsync();
        var created = await w.CatechistB.PostAsJsonAsync($"/api/dok-cases/{w.CaseB}/documents", new { Name = "Metryka B" });
        var document = await created.Content.ReadFromJsonAsync<CaseDocumentDto>();
        using var content = new MultipartFormDataContent { { new ByteArrayContent(new byte[] { 1, 2, 3 }), "file", "m.pdf" } };

        var upload = await w.CatechistA.PostAsync($"/api/dok-cases/{w.CaseB}/documents/{document!.Id}/upload", content);
        var download = await w.CatechistA.GetAsync($"/api/dok-cases/{w.CaseB}/documents/{document.Id}/download");
        var markProvided = await w.CatechistA.PutAsJsonAsync($"/api/dok-cases/{w.CaseB}/documents/{document.Id}", new { IsProvided = true });

        Assert.Equal(HttpStatusCode.NotFound, upload.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, markProvided.StatusCode);
    }

    [Fact]
    public async Task Meetings_AreScopedToOwnCases_AndCannotBeAddedToForeignOnes()
    {
        var w = await SetUpAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        var mine = await w.CatechistA.PostAsJsonAsync("/api/meetings", new { DokCaseId = w.CaseA, MeetingDate = today });
        var foreign = await w.CatechistA.PostAsJsonAsync("/api/meetings", new { DokCaseId = w.CaseB, MeetingDate = today });
        var theirs = await w.CatechistB.PostAsJsonAsync("/api/meetings", new { DokCaseId = w.CaseB, MeetingDate = today });
        var theirsDto = await theirs.Content.ReadFromJsonAsync<MeetingDto>();

        var listA = await w.CatechistA.GetFromJsonAsync<List<MeetingDto>>("/api/meetings");
        var get = await w.CatechistA.GetAsync($"/api/meetings/{theirsDto!.Id}");
        var delete = await w.CatechistA.DeleteAsync($"/api/meetings/{theirsDto.Id}");
        var listAdmin = await w.Admin.GetFromJsonAsync<List<MeetingDto>>("/api/meetings");

        Assert.Equal(HttpStatusCode.Created, mine.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        Assert.Single(listA!);
        Assert.Equal(w.CaseA, listA![0].DokCaseId);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Contains(listAdmin!, m => m.Id == theirsDto.Id);
    }

    [Fact]
    public async Task Dashboard_CountsOnlyOwnCasesForACatechist()
    {
        var w = await SetUpAsync();

        var ownSummary = await w.CatechistA.GetFromJsonAsync<DashboardSummaryDto>("/api/dashboard/summary");
        var adminSummary = await w.Admin.GetFromJsonAsync<DashboardSummaryDto>("/api/dashboard/summary");

        Assert.Equal(1, ownSummary!.DokCasesByStage.Sum(s => s.Count));
        Assert.True(adminSummary!.DokCasesByStage.Sum(s => s.Count) >= 2);
    }

    [Fact]
    public async Task Superwizor_AndDyrektorDok_KeepFullVisibility()
    {
        var w = await SetUpAsync();
        var superwizor = await CreateAuthenticatedClientAsync($"sup-{Guid.NewGuid():N}@example.org", "Sekret123!", "Superwizor");
        var director = await CreateAuthenticatedClientAsync($"dyr-{Guid.NewGuid():N}@example.org", "Sekret123!", "DyrektorDOK");

        foreach (var client in new[] { superwizor, director })
        {
            var names = await CaseNamesAsync(client);
            Assert.Contains($"Jan {w.StudentA}", names);
            Assert.Contains($"Piotr {w.StudentB}", names);
        }
    }

    [Fact]
    public async Task AccountWithoutLinkedPerson_SeesNoCases()
    {
        var w = await SetUpAsync();
        var unlinked = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");

        var names = await CaseNamesAsync(unlinked);

        Assert.Empty(names);
        Assert.Equal(HttpStatusCode.NotFound, (await unlinked.GetAsync($"/api/dok-cases/{w.CaseA}")).StatusCode);
    }

    [Fact]
    public async Task CatechistWithoutPermission_StillGets403_BeforeAnyScopeLogic()
    {
        var w = await SetUpAsync();

        var response = await w.CatechistA.DeleteAsync($"/api/dok-cases/{w.CaseA}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
