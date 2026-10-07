using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using DokPortal.Application.Attachments;
using DokPortal.Application.Resources;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Constants;
using DokPortal.Infrastructure.Migrations;
using DokPortal.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Globalna biblioteka zasobów dla katechistów: wgrywa Superwizor i Dyrektorzy, przegląda osoba z uprawnieniem.</summary>
public class ResourcesControllerTests : IntegrationTestBase, IClassFixture<FakeStorageFactory>
{
    public ResourcesControllerTests(CustomWebApplicationFactory unused, FakeStorageFactory factory) : base(factory)
    {
    }

    private Task<HttpClient> UserAsync(string role) =>
        CreateAuthenticatedClientAsync($"{role.ToLower()}-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

    private static MultipartFormDataContent File(string fileName, string text = "TRESC")
    {
        var content = new MultipartFormDataContent();
        var part = new ByteArrayContent(Encoding.UTF8.GetBytes(text));
        part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(part, "file", fileName);
        return content;
    }

    private static async Task<ResourceDto> CreateAsync(HttpClient client, string title = "Scenariusze spotkań")
    {
        var response = await client.PostAsJsonAsync("/api/resources", new { Title = title, Description = "Na rok formacyjny" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ResourceDto>())!;
    }

    [Theory]
    [InlineData("Superwizor")]
    [InlineData("DyrektorDOK")]
    [InlineData("DyrektorSKSP")]
    public async Task SupervisorAndDirectors_CreateAResource_UploadAFile_AndTheLeadingCatechistDownloadsIt(string role)
    {
        var uploader = await UserAsync(role);
        var resource = await CreateAsync(uploader, $"Zasób {Guid.NewGuid():N}".Substring(0, 14));

        var upload = await uploader.PostAsync($"/api/resources/{resource.Id}/attachments", File("scenariusz.pdf", "SKAN-1"));
        var attachment = (await upload.Content.ReadFromJsonAsync<AttachmentDto>())!;
        var catechist = await UserAsync("KatechistaProwadzacy");
        var listed = (await catechist.GetFromJsonAsync<List<ResourceDto>>("/api/resources"))!.Single(r => r.Id == resource.Id);
        var download = await catechist.GetAsync($"/api/resources/{resource.Id}/attachments/{attachment.Id}/download");

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        Assert.Equal("scenariusz.pdf", Assert.Single(listed.Files).FileName);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("SKAN-1", await download.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TheLeadingCatechist_CanBrowseButNotChangeTheLibrary()
    {
        var director = await UserAsync("DyrektorDOK");
        var resource = await CreateAsync(director);
        var catechist = await UserAsync("KatechistaProwadzacy");

        var create = await catechist.PostAsJsonAsync("/api/resources", new { Title = "Mój" });
        var update = await catechist.PutAsJsonAsync($"/api/resources/{resource.Id}", new { Title = "Zmiana" });
        var delete = await catechist.DeleteAsync($"/api/resources/{resource.Id}");
        var upload = await catechist.PostAsync($"/api/resources/{resource.Id}/attachments", File("a.pdf"));
        var get = await catechist.GetAsync($"/api/resources/{resource.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, upload.StatusCode);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    [Fact]
    public async Task TheBishop_HasNoAccessByDefault()
    {
        var bishop = await UserAsync("Biskup");

        Assert.Equal(HttpStatusCode.Forbidden, (await bishop.GetAsync("/api/resources")).StatusCode);
    }

    [Fact]
    public async Task Update_Delete_AndFileRemoval_Work_AndUnknownIdsAreNotFound()
    {
        var supervisor = await UserAsync("Superwizor");
        var resource = await CreateAsync(supervisor);
        var attachment = (await (await supervisor.PostAsync($"/api/resources/{resource.Id}/attachments", File("a.pdf"))).Content.ReadFromJsonAsync<AttachmentDto>())!;

        var update = await supervisor.PutAsJsonAsync($"/api/resources/{resource.Id}", new { Title = "Nowy tytuł", Description = "Opis" });
        var removeFile = await supervisor.DeleteAsync($"/api/resources/{resource.Id}/attachments/{attachment.Id}");
        var delete = await supervisor.DeleteAsync($"/api/resources/{resource.Id}");
        var gone = await supervisor.GetAsync($"/api/resources/{resource.Id}");
        var uploadToMissing = await supervisor.PostAsync($"/api/resources/{Guid.NewGuid()}/attachments", File("a.pdf"));

        Assert.Equal("Nowy tytuł", (await update.Content.ReadFromJsonAsync<ResourceDto>())!.Title);
        Assert.Equal(HttpStatusCode.NoContent, removeFile.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, uploadToMissing.StatusCode);
    }

    [Fact]
    public async Task ATitleIsRequired_AndAForbiddenFileTypeIsRefused()
    {
        var supervisor = await UserAsync("Superwizor");
        var resource = await CreateAsync(supervisor);

        var noTitle = await supervisor.PostAsJsonAsync("/api/resources", new { Title = "" });
        var exe = await supervisor.PostAsync($"/api/resources/{resource.Id}/attachments", File("wirus.exe"));

        Assert.Equal(HttpStatusCode.BadRequest, noTitle.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, exe.StatusCode);
    }

    [Fact]
    public async Task GrantSql_GivesTheResourcePermissionsToExistingRoles_OnlyOnce_AndOnlyToRolesWithStoredPermissions()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.RolePermissions.AddRange(
            new RolePermission { RoleName = "Superwizor", Permission = Permissions.DokCasesView },
            new RolePermission { RoleName = "DyrektorDOK", Permission = Permissions.ResourcesView },
            new RolePermission { RoleName = "DyrektorSKSP", Permission = Permissions.PeopleManage },
            new RolePermission { RoleName = "KatechistaProwadzacy", Permission = Permissions.DokCasesView });
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync(GrantResourcesPermissions.GrantSql);
        await db.Database.ExecuteSqlRawAsync(GrantResourcesPermissions.GrantSql);

        var granted = await db.RolePermissions.Where(p => p.Permission.StartsWith("Resources."))
            .OrderBy(p => p.RoleName).ThenBy(p => p.Permission).Select(p => p.RoleName + ":" + p.Permission).ToListAsync();
        Assert.Equal(new[]
        {
            "DyrektorDOK:Resources.Manage", "DyrektorDOK:Resources.View",
            "DyrektorSKSP:Resources.Manage", "DyrektorSKSP:Resources.View",
            "KatechistaProwadzacy:Resources.View",
            "Superwizor:Resources.Manage", "Superwizor:Resources.View"
        }, granted);

        await db.Database.ExecuteSqlRawAsync(GrantResourcesPermissions.RevokeSql);
        Assert.Empty(await db.RolePermissions.Where(p => p.Permission.StartsWith("Resources.")).ToListAsync());
    }
}
