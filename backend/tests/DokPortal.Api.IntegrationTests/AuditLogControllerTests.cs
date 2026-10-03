using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.AuditLog;
using DokPortal.Application.DokCases;
using DokPortal.Application.People;
using DokPortal.Application.Users;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class AuditLogControllerTests : IntegrationTestBase
{
    public AuditLogControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task ReadPastoralNotes_WhenFilteredByOtherAuthor_IsRecordedAsBlocked()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var (caseId, catechistPersonId, _) = await SeedDokCaseAsync(admin);

        var director = await CreateAuthenticatedClientAsync($"dyr-{Guid.NewGuid():N}@example.org", "Sekret123!", "DyrektorDOK");
        await director.PostAsJsonAsync($"/api/dok-cases/{caseId}/notes", new { Content = "Notatka poufna" });

        var catechist = await CreateAuthenticatedClientForPersonAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", catechistPersonId, "KatechistaProwadzacy");
        await catechist.GetAsync($"/api/dok-cases/{caseId}/notes");

        var logResponse = await admin.GetAsync("/api/audit-log");
        var entries = await logResponse.Content.ReadFromJsonAsync<List<AuditLogEntryDto>>(EnumJsonOptions);

        Assert.Contains(entries!, e => e.Action == "ReadPastoralNotes" && e.Result.ToString() == "Blocked");
    }

    [Fact]
    public async Task AssignRoles_IsRecordedAsAllowedWithTargetEmail()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var targetEmail = $"target-{Guid.NewGuid():N}@example.org";
        var createResponse = await admin.PostAsJsonAsync("/api/users", new { Email = targetEmail, Password = "Sekret123!", Roles = Array.Empty<string>() });
        var created = await createResponse.Content.ReadFromJsonAsync<UserDto>();

        await admin.PutAsJsonAsync($"/api/users/{created!.Id}/roles", new { Roles = new[] { "KatechistaProwadzacy" } });

        var logResponse = await admin.GetAsync("/api/audit-log");
        var entries = await logResponse.Content.ReadFromJsonAsync<List<AuditLogEntryDto>>(EnumJsonOptions);

        Assert.Contains(entries!, e => e.Action == "AssignUserRoles" && e.ObjectDescription == targetEmail && e.Result.ToString() == "Allowed");
    }

    [Fact]
    public async Task List_AsNonAdministrator_IsForbidden()
    {
        var katechista = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");
        var response = await katechista.GetAsync("/api/audit-log");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
