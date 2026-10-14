using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>RODO: kopia danych i anonimizacja – tylko dla administratora, na prawdziwej bazie (zapytania SQL).</summary>
public class GdprEndpointsTests : IntegrationTestBase, IClassFixture<FakeStorageFactory>
{
    public GdprEndpointsTests(CustomWebApplicationFactory unused, FakeStorageFactory factory) : base(factory)
    {
    }

    private async Task<Guid> SeedStudentAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var student = new Person { Id = Guid.NewGuid(), FirstName = "Rodo", LastName = "Kowalski", Email = $"rodo-{Guid.NewGuid():N}@example.org", Phone = "500 600 700", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var catechist = new Person { Id = Guid.NewGuid(), FirstName = "Kate", LastName = "Chista", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.AddRange(student, catechist);
        var dokCase = new DokCase { Id = Guid.NewGuid(), PersonId = student.Id, CatechistPersonId = catechist.Id, Path = DokPath.Confirmation, Stage = DokStage.Evangelization, StageSinceUtc = DateTime.UtcNow, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.DokCases.Add(dokCase);
        var group = new Meeting { Id = Guid.NewGuid(), GroupLabel = "Grupa środowa", MeetingDate = new DateOnly(2026, 10, 7), CreatedAtUtc = DateTime.UtcNow };
        group.Attendees.Add(new MeetingAttendee { Id = Guid.NewGuid(), MeetingId = group.Id, DokCaseId = dokCase.Id, IsAttended = true });
        db.Meetings.Add(group);
        db.Candidates.Add(new Candidate { Id = Guid.NewGuid(), PersonId = student.Id, FormationYear = 1, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return student.Id;
    }

    [Fact]
    public async Task AnAdministrator_ExportsThenAnonymizes_OthersCannot()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var director = await CreateAuthenticatedClientAsync($"dok-{Guid.NewGuid():N}@example.org", "Sekret123!", "DyrektorDOK");
        var id = await SeedStudentAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await director.GetAsync($"/api/people/{id}/rodo/export")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await director.PostAsync($"/api/people/{id}/rodo/anonymize", null)).StatusCode);

        var export = await admin.GetAsync($"/api/people/{id}/rodo/export");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Equal("application/json", export.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith("dane-osobowe-", export.Content.Headers.ContentDisposition!.FileNameStar ?? export.Content.Headers.ContentDisposition.FileName);
        var json = await export.Content.ReadAsStringAsync();
        Assert.Contains("500 600 700", json);
        Assert.Contains("Grupa środowa", json);
        Assert.Contains("Kate Chista", json);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/people/{Guid.NewGuid()}/rodo/export")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/people/{id}/rodo/anonymize", null)).StatusCode);
        var person = await admin.GetFromJsonAsync<JsonElement>($"/api/people/{id}");
        Assert.Equal("Osoba", person.GetProperty("firstName").GetString());
        Assert.NotEqual(JsonValueKind.Null, person.GetProperty("anonymizedAtUtc").ValueKind);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"/api/people/{id}/rodo/anonymize", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync($"/api/people/{Guid.NewGuid()}/rodo/anonymize", null)).StatusCode);
    }
}
