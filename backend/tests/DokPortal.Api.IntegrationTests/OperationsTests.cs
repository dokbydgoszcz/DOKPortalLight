using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DokPortal.Application.AuditLog;
using DokPortal.Application.CaseDocuments;
using DokPortal.Application.People;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Limit prób logowania i rozszerzony dziennik zmian (przegląd 2026-10-08, B6, B7).</summary>
public class OperationsTests : IntegrationTestBase, IClassFixture<FakeStorageFactory>
{
    public OperationsTests(CustomWebApplicationFactory unused, FakeStorageFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task TooManyLoginAttempts_FromOneAddress_AreRefusedFor_AMinute()
    {
        using var limited = Factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:LoginPerMinute", "3"));
        var client = limited.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.7");

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            statuses.Add((await client.PostAsJsonAsync("/api/auth/login", new { Email = "nikt@example.org", Password = "zle-haslo" })).StatusCode);
        }
        var other = limited.CreateClient();
        other.DefaultRequestHeaders.Add("X-Forwarded-For", "198.51.100.1");
        var fromElsewhere = await other.PostAsJsonAsync("/api/auth/login", new { Email = "nikt@example.org", Password = "zle-haslo" });
        var refused = await client.PostAsJsonAsync("/api/auth/login", new { Email = "nikt@example.org", Password = "zle-haslo" });

        Assert.Equal(new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests }, statuses);
        Assert.Equal(HttpStatusCode.Unauthorized, fromElsewhere.StatusCode);
        Assert.Contains("Zbyt wiele prób", await refused.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ChangesAndFileDownloads_AreWrittenToTheAuditLog()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var marker = $"Audyt{Guid.NewGuid():N}"[..13];
        var person = await (await admin.PostAsJsonAsync("/api/people", new { FirstName = "Ola", LastName = marker })).Content.ReadFromJsonAsync<PersonDto>();
        (await admin.PutAsJsonAsync($"/api/people/{person!.Id}", new { FirstName = "Olga", LastName = marker })).EnsureSuccessStatusCode();
        var (caseId, _, _) = await SeedDokCaseAsync(admin, "Jan", marker);
        var document = await (await admin.PostAsJsonAsync($"/api/dok-cases/{caseId}/documents", new { Name = "Metryka" })).Content.ReadFromJsonAsync<CaseDocumentDto>();
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(new byte[] { 1, 2, 3 });
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", $"{marker}.pdf");
        (await admin.PostAsync($"/api/dok-cases/{caseId}/documents/{document!.Id}/upload", form)).EnsureSuccessStatusCode();
        (await admin.GetAsync($"/api/dok-cases/{caseId}/documents/{document.Id}/download")).EnsureSuccessStatusCode();
        var del = await admin.DeleteAsync($"/api/people/{person.Id}");
        Assert.True(del.IsSuccessStatusCode, await del.Content.ReadAsStringAsync());
        var failed = await admin.DeleteAsync($"/api/people/{Guid.NewGuid()}");

        var entries = await admin.GetFromJsonAsync<List<AuditLogEntryDto>>($"/api/audit-log?search={marker}", EnumJsonOptions);
        var actions = entries!.Select(e => e.Action).ToList();
        var byId = await admin.GetFromJsonAsync<List<AuditLogEntryDto>>($"/api/audit-log?action=DeletePerson&search={person.Id}", EnumJsonOptions);

        Assert.Contains("CreatePerson", actions);
        Assert.Contains("UpdatePerson", actions);
        Assert.Contains("CreateDokCase", actions);
        var upload = await admin.GetFromJsonAsync<List<AuditLogEntryDto>>($"/api/audit-log?action=UploadCaseDocument&search={caseId}", EnumJsonOptions);
        Assert.StartsWith("Metryka [", Assert.Single(upload!).ObjectDescription);
        Assert.Contains("DownloadCaseDocument", actions);
        Assert.Single(byId!);
        Assert.Equal(HttpStatusCode.NotFound, failed.StatusCode);
    }
}
