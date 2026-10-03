using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using DokPortal.Application.Attachments;
using DokPortal.Application.AuditLog;
using DokPortal.Application.Missions;
using DokPortal.Domain.Constants;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Migrations;
using DokPortal.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Lista Katechiści: osoby po formacji czekają na posłanie, które Dyrektor DOK lub SKŚP nadaje jednym kliknięciem.</summary>
public class CatechistsPendingTests : IntegrationTestBase, IClassFixture<FakeStorageFactory>
{
    public CatechistsPendingTests(CustomWebApplicationFactory unused, FakeStorageFactory factory) : base(factory)
    {
    }

    private async Task<HttpClient> UserAsync(string role) =>
        await CreateAuthenticatedClientAsync($"{role.ToLower()}-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

    /// <summary>Osoba, która ukończyła formację (rok rozpoczęcia dawno temu) i nie ma misji.</summary>
    private async Task<(Guid PersonId, string FullName)> SeedWaitingPersonAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var last = $"Czekajacy{Guid.NewGuid():N}".Substring(0, 16);
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Anna", LastName = last, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.Add(person);
        db.Candidates.Add(new Candidate
        {
            Id = Guid.NewGuid(), PersonId = person.Id, FormationStartYear = 2018, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return (person.Id, $"Anna {last}");
    }

    private static MultipartFormDataContent File(string fileName, string text = "PDF")
    {
        var content = new MultipartFormDataContent();
        var part = new ByteArrayContent(Encoding.UTF8.GetBytes(text));
        part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(part, "file", fileName);
        return content;
    }

    [Fact]
    public async Task TheDirectorOfDok_SeesTheWaitingPerson_AndGrantsTheMissionWithOneRequest()
    {
        var (personId, fullName) = await SeedWaitingPersonAsync();
        var director = await UserAsync("DyrektorDOK");

        var pending = (await director.GetFromJsonAsync<List<PendingCatechistDto>>("/api/missions/pending"))!;
        Assert.Contains(pending, p => p.PersonId == personId && p.PersonFullName == fullName);

        var response = await director.PostAsJsonAsync("/api/missions/grant", new { PersonId = personId });
        var mission = await response.Content.ReadFromJsonAsync<MissionDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(personId, mission!.PersonId);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), mission.GrantedDate);
        var after = (await director.GetFromJsonAsync<List<PendingCatechistDto>>("/api/missions/pending"))!;
        Assert.DoesNotContain(after, p => p.PersonId == personId);
    }

    [Fact]
    public async Task Granting_IsAudited()
    {
        var (personId, fullName) = await SeedWaitingPersonAsync();
        var director = await UserAsync("DyrektorSKSP");
        await director.PostAsJsonAsync("/api/missions/grant", new { PersonId = personId });
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

        var log = (await admin.GetFromJsonAsync<List<AuditLogEntryDto>>("/api/audit-log?action=GrantMission", EnumJsonOptions))!;

        Assert.Contains(log, e => e.ObjectDescription == fullName && e.Result == AuditResult.Allowed);
    }

    [Fact]
    public async Task Granting_ToSomeoneWhoDoesNotWait_OrTwice_IsABadRequest()
    {
        var (personId, _) = await SeedWaitingPersonAsync();
        var director = await UserAsync("DyrektorDOK");
        await director.PostAsJsonAsync("/api/missions/grant", new { PersonId = personId });

        var twice = await director.PostAsJsonAsync("/api/missions/grant", new { PersonId = personId });
        var stranger = await director.PostAsJsonAsync("/api/missions/grant", new { PersonId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.BadRequest, twice.StatusCode);
        Assert.Contains("nie czeka", await twice.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, stranger.StatusCode);
    }

    [Fact]
    public async Task OnlyThoseWithManage_CanGrant_ViewersSeeTheList_AndOthersAreForbidden()
    {
        var (personId, _) = await SeedWaitingPersonAsync();
        var bishop = await UserAsync("Biskup");
        var catechist = await UserAsync("KatechistaProwadzacy");

        Assert.Equal(HttpStatusCode.OK, (await bishop.GetAsync("/api/missions/pending")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bishop.PostAsJsonAsync("/api/missions/grant", new { PersonId = personId })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await catechist.GetAsync("/api/missions/pending")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await catechist.PostAsJsonAsync("/api/missions/grant", new { PersonId = personId })).StatusCode);
    }

    [Fact]
    public async Task TheDirectorOfDok_HasTheMissionPermissionsByDefault()
    {
        var director = await UserAsync("DyrektorDOK");

        Assert.Equal(HttpStatusCode.OK, (await director.GetAsync("/api/missions")).StatusCode);
        Assert.True(DefaultRolePermissions.Grants[AppRoles.DyrektorDOK].Contains(Permissions.MissionsManage));
    }

    [Fact]
    public async Task TheGrantDocument_CanBeAttachedToTheMission_AndDownloadedByViewers()
    {
        var (personId, _) = await SeedWaitingPersonAsync();
        var director = await UserAsync("DyrektorDOK");
        var mission = (await (await director.PostAsJsonAsync("/api/missions/grant", new { PersonId = personId })).Content.ReadFromJsonAsync<MissionDto>())!;
        var bishop = await UserAsync("Biskup");
        var url = $"/api/missions/{mission.Id}/attachments";

        var upload = await director.PostAsync(url, File("poslanie.pdf", "DOKUMENT"));
        var attachment = (await upload.Content.ReadFromJsonAsync<AttachmentDto>())!;
        var fetched = (await director.GetFromJsonAsync<MissionDto>($"/api/missions/{mission.Id}"))!;
        var download = await bishop.GetAsync($"{url}/{attachment.Id}/download");
        var bishopUpload = await bishop.PostAsync(url, File("x.pdf"));
        var delete = await director.DeleteAsync($"{url}/{attachment.Id}");

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        Assert.Equal("poslanie.pdf", fetched.Attachments.Single().FileName);
        Assert.Equal("DOKUMENT", await download.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden, bishopUpload.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await director.PostAsync($"/api/missions/{Guid.NewGuid()}/attachments", File("a.pdf"))).StatusCode);
    }

    [Fact]
    public async Task GrantSql_GivesTheMissionPermissionsToAnExistingDyrektorDokRole_OnlyOnce()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.RolePermissions.AddRange(
            new RolePermission { RoleName = "DyrektorDOK", Permission = Permissions.DokCasesView },
            new RolePermission { RoleName = "DyrektorDOK", Permission = Permissions.MissionsView },
            new RolePermission { RoleName = "Superwizor", Permission = Permissions.DokCasesView });
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync(GrantMissionsToDyrektorDok.GrantSql);
        await db.Database.ExecuteSqlRawAsync(GrantMissionsToDyrektorDok.GrantSql);

        var granted = await db.RolePermissions.Where(p => p.Permission.StartsWith("Missions.")).OrderBy(p => p.RoleName).ThenBy(p => p.Permission)
            .Select(p => p.RoleName + ":" + p.Permission).ToListAsync();
        Assert.Equal(new[] { "DyrektorDOK:Missions.Export", "DyrektorDOK:Missions.Manage", "DyrektorDOK:Missions.View" }, granted);

        await db.Database.ExecuteSqlRawAsync(GrantMissionsToDyrektorDok.RevokeSql);
        Assert.Empty(await db.RolePermissions.Where(p => p.Permission.StartsWith("Missions.")).ToListAsync());
    }

    [Fact]
    public async Task GrantSql_DoesNothing_WhenTheRoleHasNoStoredPermissions()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        await db.Database.ExecuteSqlRawAsync(GrantMissionsToDyrektorDok.GrantSql);

        Assert.Empty(await db.RolePermissions.ToListAsync());
    }
}
