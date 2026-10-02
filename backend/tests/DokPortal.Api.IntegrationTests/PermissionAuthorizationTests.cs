using System.Net;
using System.Net.Http.Json;
using DokPortal.Api.Authorization;
using DokPortal.Application.Budget;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class PermissionAuthorizationTests : IntegrationTestBase
{
    public PermissionAuthorizationTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task PolicyProvider_CreatesPolicyForKnownPermission_AndDelegatesOthers()
    {
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));

        var known = await provider.GetPolicyAsync(Permissions.PeopleManage);
        var unknown = await provider.GetPolicyAsync("NieistniejacaPolityka");

        Assert.NotNull(known);
        Assert.Contains(known!.Requirements, r => r is PermissionRequirement p && p.Permission == Permissions.PeopleManage);
        Assert.Null(unknown);
    }

    [Theory]
    [InlineData("/api/users", "Administrator", HttpStatusCode.OK)]
    [InlineData("/api/users", "DyrektorDOK", HttpStatusCode.Forbidden)]
    [InlineData("/api/audit-log", "Administrator", HttpStatusCode.OK)]
    [InlineData("/api/audit-log", "DyrektorSKSP", HttpStatusCode.Forbidden)]
    public async Task AdminOnlyEndpoints_AreGatedByPermission(string url, string role, HttpStatusCode expected)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

        var response = await client.GetAsync(url);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task AdminOnlyEndpoint_ReturnsUnauthorized_WithoutToken()
    {
        var response = await Client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("DyrektorSKSP", HttpStatusCode.Created)]
    [InlineData("DyrektorDOK", HttpStatusCode.Created)]
    [InlineData("Superwizor", HttpStatusCode.Forbidden)]
    [InlineData("Biskup", HttpStatusCode.Forbidden)]
    public async Task People_Create_RequiresManagePermission(string role, HttpStatusCode expected)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

        var response = await client.PostAsJsonAsync("/api/people", new { FirstName = "Test", LastName = "User" });

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/candidates", "DyrektorSKSP", HttpStatusCode.OK)]
    [InlineData("/api/candidates", "KatechistaProwadzacy", HttpStatusCode.Forbidden)]
    [InlineData("/api/candidates", "Biskup", HttpStatusCode.Forbidden)]
    [InlineData("/api/missions", "DyrektorSKSP", HttpStatusCode.OK)]
    [InlineData("/api/missions", "Biskup", HttpStatusCode.OK)]
    [InlineData("/api/missions", "KatechistaProwadzacy", HttpStatusCode.Forbidden)]
    [InlineData("/api/formators", "DyrektorSKSP", HttpStatusCode.OK)]
    [InlineData("/api/formators", "KatechistaProwadzacy", HttpStatusCode.Forbidden)]
    [InlineData("/api/parish-needs", "DyrektorSKSP", HttpStatusCode.OK)]
    [InlineData("/api/parish-needs", "KatechistaProwadzacy", HttpStatusCode.Forbidden)]
    [InlineData("/api/parish-needs", "DyrektorDOK", HttpStatusCode.Forbidden)]
    public async Task SkspModules_Reads_RequireViewPermission(string url, string role, HttpStatusCode expected)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

        var response = await client.GetAsync(url);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/dok-cases", "DyrektorDOK", HttpStatusCode.OK)]
    [InlineData("/api/dok-cases", "Superwizor", HttpStatusCode.OK)]
    [InlineData("/api/dok-cases", "KatechistaProwadzacy", HttpStatusCode.OK)]
    [InlineData("/api/dok-cases", "Biskup", HttpStatusCode.OK)]
    [InlineData("/api/dok-cases", "DyrektorSKSP", HttpStatusCode.Forbidden)]
    [InlineData("/api/meetings", "DyrektorDOK", HttpStatusCode.OK)]
    [InlineData("/api/meetings", "KatechistaProwadzacy", HttpStatusCode.OK)]
    [InlineData("/api/meetings", "Superwizor", HttpStatusCode.Forbidden)]
    [InlineData("/api/meetings", "Biskup", HttpStatusCode.Forbidden)]
    [InlineData("/api/supervisions", "Superwizor", HttpStatusCode.OK)]
    [InlineData("/api/supervisions", "DyrektorSKSP", HttpStatusCode.OK)]
    [InlineData("/api/supervisions", "KatechistaProwadzacy", HttpStatusCode.Forbidden)]
    [InlineData("/api/supervisions", "Biskup", HttpStatusCode.Forbidden)]
    public async Task DokModules_Reads_RequireViewPermission(string url, string role, HttpStatusCode expected)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

        var response = await client.GetAsync(url);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("KatechistaProwadzacy", HttpStatusCode.OK)]
    [InlineData("DyrektorDOK", HttpStatusCode.OK)]
    [InlineData("Superwizor", HttpStatusCode.Forbidden)]
    [InlineData("Biskup", HttpStatusCode.Forbidden)]
    public async Task PastoralNotes_Read_RequiresViewPermission(string role, HttpStatusCode expected)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

        var response = await client.GetAsync($"/api/dok-cases/{Guid.NewGuid()}/notes");

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("Superwizor", HttpStatusCode.OK)]
    [InlineData("Biskup", HttpStatusCode.OK)]
    [InlineData("KatechistaProwadzacy", HttpStatusCode.OK)]
    [InlineData("DyrektorSKSP", HttpStatusCode.Forbidden)]
    public async Task CaseDocuments_Read_RequiresViewPermission(string role, HttpStatusCode expected)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

        var response = await client.GetAsync($"/api/dok-cases/{Guid.NewGuid()}/documents");

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("SKSP", "DyrektorSKSP", HttpStatusCode.OK)]
    [InlineData("SKSP", "DyrektorDOK", HttpStatusCode.Forbidden)]
    [InlineData("SKSP", "KatechistaProwadzacy", HttpStatusCode.Forbidden)]
    [InlineData("DOK", "DyrektorDOK", HttpStatusCode.OK)]
    [InlineData("DOK", "DyrektorSKSP", HttpStatusCode.Forbidden)]
    [InlineData("DOK", "KatechistaProwadzacy", HttpStatusCode.Forbidden)]
    public async Task Budget_Read_IsGatedPerFund(string fund, string role, HttpStatusCode expected)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

        var response = await client.GetAsync($"/api/budget?fund={fund}");

        Assert.Equal(expected, response.StatusCode);
    }

    private static object BudgetBody(string fund) => new
    {
        Fund = fund, EntryDate = "2026-09-18", Description = "Opis", Category = "Kategoria", Type = "Expense", Amount = 10m
    };

    [Theory]
    [InlineData("DOK", "DyrektorDOK", HttpStatusCode.Created)]
    [InlineData("DOK", "DyrektorSKSP", HttpStatusCode.Forbidden)]
    [InlineData("SKSP", "DyrektorSKSP", HttpStatusCode.Created)]
    [InlineData("SKSP", "DyrektorDOK", HttpStatusCode.Forbidden)]
    public async Task Budget_Create_IsGatedPerFund(string fund, string role, HttpStatusCode expected)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

        var response = await client.PostAsJsonAsync("/api/budget", BudgetBody(fund));

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Budget_Delete_IsGatedByFundOfTheEntry()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var created = await (await admin.PostAsJsonAsync("/api/budget", BudgetBody("DOK")))
            .Content.ReadFromJsonAsync<BudgetEntryDto>(EnumJsonOptions);
        var sksp = await CreateAuthenticatedClientAsync($"sksp-{Guid.NewGuid():N}@example.org", "Sekret123!", "DyrektorSKSP");
        var dok = await CreateAuthenticatedClientAsync($"dok-{Guid.NewGuid():N}@example.org", "Sekret123!", "DyrektorDOK");

        Assert.Equal(HttpStatusCode.Forbidden, (await sksp.DeleteAsync($"/api/budget/{created!.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await dok.DeleteAsync($"/api/budget/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await dok.DeleteAsync($"/api/budget/{Guid.NewGuid()}")).StatusCode);
    }

    [Theory]
    [InlineData("/api/documents", "DyrektorSKSP", HttpStatusCode.OK)]
    [InlineData("/api/documents", "DyrektorDOK", HttpStatusCode.OK)]
    [InlineData("/api/documents", "KatechistaProwadzacy", HttpStatusCode.Forbidden)]
    [InlineData("/api/mailing/campaigns", "DyrektorSKSP", HttpStatusCode.OK)]
    [InlineData("/api/mailing/campaigns", "DyrektorDOK", HttpStatusCode.OK)]
    [InlineData("/api/mailing/campaigns", "KatechistaProwadzacy", HttpStatusCode.Forbidden)]
    public async Task DocumentsAndMailing_Reads_RequireViewPermission(string url, string role, HttpStatusCode expected)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

        var response = await client.GetAsync(url);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("KatechistaProwadzacy")]
    [InlineData("Biskup")]
    public async Task People_AndParishes_Lists_AreOpenToEveryAuthenticatedUser(string role)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/people")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/parishes")).StatusCode);
    }
}
