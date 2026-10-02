using System.Net;
using DokPortal.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class ExportControllerTests : IntegrationTestBase
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public ExportControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Theory]
    [InlineData("people", "Administrator")]
    [InlineData("people", "DyrektorSKSP")]
    [InlineData("people", "DyrektorDOK")]
    [InlineData("dok-cases", "Administrator")]
    [InlineData("dok-cases", "DyrektorDOK")]
    [InlineData("candidates", "Administrator")]
    [InlineData("candidates", "DyrektorSKSP")]
    [InlineData("missions", "Administrator")]
    [InlineData("missions", "DyrektorSKSP")]
    [InlineData("formators", "Administrator")]
    [InlineData("formators", "DyrektorSKSP")]
    [InlineData("supervisions", "Administrator")]
    [InlineData("supervisions", "DyrektorDOK")]
    [InlineData("supervisions", "DyrektorSKSP")]
    [InlineData("supervisions", "Superwizor")]
    [InlineData("meetings", "Administrator")]
    [InlineData("meetings", "DyrektorDOK")]
    [InlineData("parishes", "Administrator")]
    public async Task Export_ReturnsXlsx_ForAllowedRole(string list, string role)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

        var response = await client.GetAsync($"/api/export/{list}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(XlsxContentType, response.Content.Headers.ContentType!.MediaType);
        Assert.NotEmpty(await response.Content.ReadAsByteArrayAsync());
    }

    [Theory]
    [InlineData("people")]
    [InlineData("dok-cases")]
    [InlineData("candidates")]
    [InlineData("missions")]
    [InlineData("formators")]
    [InlineData("supervisions")]
    [InlineData("meetings")]
    [InlineData("parishes")]
    public async Task Export_ReturnsForbidden_ForCatechist(string list)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");

        var response = await client.GetAsync($"/api/export/{list}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("dok-cases", "DyrektorSKSP")]
    [InlineData("candidates", "DyrektorDOK")]
    [InlineData("meetings", "DyrektorSKSP")]
    [InlineData("parishes", "DyrektorDOK")]
    public async Task Export_ReturnsForbidden_ForRoleOutsideListPolicy(string list, string role)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

        var response = await client.GetAsync($"/api/export/{list}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Export_ReturnsUnauthorized_WithoutLogin()
    {
        var response = await Client.GetAsync("/api/export/people");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Export_WritesAuditEntry()
    {
        var email = $"user-{Guid.NewGuid():N}@example.org";
        var client = await CreateAuthenticatedClientAsync(email, "Sekret123!", "Administrator");

        var response = await client.GetAsync("/api/export/parishes");
        response.EnsureSuccessStatusCode();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Contains(db.AuditLogEntries, e => e.UserEmail == email && e.Action == "ExportData" && e.ObjectDescription == "parishes");
    }
}
