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
        var personResponse = await admin.PostAsJsonAsync("/api/people", new { FirstName = "Jan", LastName = "Kowalski" });
        var person = await personResponse.Content.ReadFromJsonAsync<PersonDto>();
        var catechistResponse = await admin.PostAsJsonAsync("/api/people", new { FirstName = "Anna", LastName = "Maj" });
        var catechist = await catechistResponse.Content.ReadFromJsonAsync<PersonDto>();
        var caseResponse = await admin.PostAsJsonAsync("/api/dok-cases", new
        {
            PersonId = person!.Id, Path = "Confirmation", Stage = "Formation", CatechistPersonId = catechist!.Id
        });
        var dokCase = await caseResponse.Content.ReadFromJsonAsync<DokCaseDto>(EnumJsonOptions);

        var katechistaA = await CreateAuthenticatedClientAsync($"kat-a-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");
        await katechistaA.PostAsJsonAsync($"/api/dok-cases/{dokCase!.Id}/notes", new { Content = "Notatka poufna" });

        var katechistaB = await CreateAuthenticatedClientAsync($"kat-b-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");
        await katechistaB.GetAsync($"/api/dok-cases/{dokCase.Id}/notes");

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
