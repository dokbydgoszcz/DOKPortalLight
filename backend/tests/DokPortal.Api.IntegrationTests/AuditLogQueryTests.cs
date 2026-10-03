using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.AuditLog;
using DokPortal.Application.Users;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class AuditLogQueryTests : IntegrationTestBase
{
    public AuditLogQueryTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    /// <summary>Tworzy wpis audytu „AssignUserRoles” z unikalnym znacznikiem w opisie obiektu (e-mail użytkownika).</summary>
    private async Task<(HttpClient Admin, string Marker)> AuditedChangeAsync()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var marker = $"znacznik{Guid.NewGuid():N}".Substring(0, 18);
        var created = await (await admin.PostAsJsonAsync("/api/users", new { Email = $"{marker}@example.org", Password = "Sekret123!", Roles = Array.Empty<string>() }))
            .Content.ReadFromJsonAsync<UserDto>();
        (await admin.PutAsJsonAsync($"/api/users/{created!.Id}/roles", new { Roles = new[] { "Biskup" } })).EnsureSuccessStatusCode();
        return (admin, marker);
    }

    [Fact]
    public async Task Search_FindsEntriesByTextInUserActionOrObject()
    {
        var (admin, marker) = await AuditedChangeAsync();

        var found = await admin.GetFromJsonAsync<List<AuditLogEntryDto>>($"/api/audit-log?search={marker.ToUpperInvariant()}", EnumJsonOptions);
        var none = await admin.GetFromJsonAsync<List<AuditLogEntryDto>>("/api/audit-log?search=takiego-tekstu-nie-ma-nigdzie", EnumJsonOptions);

        var entry = Assert.Single(found!);
        Assert.Equal("AssignUserRoles", entry.Action);
        Assert.Contains(marker, entry.ObjectDescription);
        Assert.Empty(none!);
    }

    [Fact]
    public async Task Filters_ByActionResultAndDateRange()
    {
        var (admin, marker) = await AuditedChangeAsync();
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var tomorrow = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");

        var byAction = await admin.GetFromJsonAsync<List<AuditLogEntryDto>>($"/api/audit-log?search={marker}&action=AssignUserRoles", EnumJsonOptions);
        var otherAction = await admin.GetFromJsonAsync<List<AuditLogEntryDto>>($"/api/audit-log?search={marker}&action=ResetUserPassword", EnumJsonOptions);
        var blocked = await admin.GetFromJsonAsync<List<AuditLogEntryDto>>($"/api/audit-log?search={marker}&result=Blocked", EnumJsonOptions);
        var inRange = await admin.GetFromJsonAsync<List<AuditLogEntryDto>>($"/api/audit-log?search={marker}&from={today}&to={today}", EnumJsonOptions);
        var future = await admin.GetFromJsonAsync<List<AuditLogEntryDto>>($"/api/audit-log?search={marker}&from={tomorrow}", EnumJsonOptions);

        Assert.Single(byAction!);
        Assert.Empty(otherAction!);
        Assert.Empty(blocked!);
        Assert.Single(inRange!);
        Assert.Empty(future!);
    }

    [Fact]
    public async Task Take_LimitsTheNumberOfEntries()
    {
        var (admin, _) = await AuditedChangeAsync();
        await AuditedChangeAsync();

        var limited = await admin.GetFromJsonAsync<List<AuditLogEntryDto>>("/api/audit-log?take=1", EnumJsonOptions);

        Assert.Single(limited!);
    }

    [Fact]
    public async Task Actions_ListsTheDistinctActionNames_ForAdminsOnly()
    {
        var (admin, _) = await AuditedChangeAsync();
        var director = await CreateAuthenticatedClientAsync($"dyr-{Guid.NewGuid():N}@example.org", "Sekret123!", "DyrektorDOK");

        var actions = await admin.GetFromJsonAsync<List<string>>("/api/audit-log/actions");
        var forbidden = await director.GetAsync("/api/audit-log/actions");

        Assert.Contains("AssignUserRoles", actions!);
        Assert.Equal(actions!.OrderBy(a => a, StringComparer.Ordinal).ToList(), actions);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }
}
