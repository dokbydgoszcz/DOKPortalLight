using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Supervisions;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Superwizja należy do grupy z misji (grupa superwizyjna katechisty) i ma imienną listę obecności.</summary>
public class SupervisionAttendanceTests : IntegrationTestBase
{
    public SupervisionAttendanceTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private async Task<Person> AddCatechistAsync(string last, string? group, DateOnly end)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Anna", LastName = last, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.Add(person);
        db.CanonicalMissions.Add(new CanonicalMission
        {
            Id = Guid.NewGuid(), PersonId = person.Id, ServicePlace = "Fara", SupervisionGroup = group,
            MissionStartDate = end.AddYears(-1), MissionEndDate = end, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return person;
    }

    [Fact]
    public async Task Groups_ComeFromCurrentMissions_WithTheirCatechists()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var group = $"Grupa {Guid.NewGuid():N}"[..14];
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var a = await AddCatechistAsync("Aktywna", group, today.AddMonths(6));
        var b = await AddCatechistAsync("Druga", group, today.AddMonths(2));
        await AddCatechistAsync("Wygasla", group, today.AddDays(-10));

        var groups = await admin.GetFromJsonAsync<List<SupervisionGroupDto>>("/api/supervisions/groups");

        var found = Assert.Single(groups!, g => g.Name == group);
        Assert.Equal(new[] { "Anna Aktywna", "Anna Druga" }, found.Members.Select(m => m.FullName));
        Assert.Equal(new[] { a.Id, b.Id }, found.Members.Select(m => m.PersonId));
    }

    [Fact]
    public async Task AttendanceIsRecordedPerCatechist_AndTheCountsFollowIt()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var group = $"Grupa {Guid.NewGuid():N}"[..14];
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var present = await AddCatechistAsync("Obecna", group, today.AddMonths(6));
        var absent = await AddCatechistAsync("Nieobecna", group, today.AddMonths(6));

        var create = await admin.PostAsJsonAsync("/api/supervisions", new
        {
            Institution = "DOK", GroupLabel = group, SupervisionDate = "2026-10-10", Topic = "Rozeznawanie",
            Attendance = new[] { new { PersonId = present.Id, IsPresent = true }, new { PersonId = absent.Id, IsPresent = false } }
        });
        var created = await create.Content.ReadFromJsonAsync<SupervisionDto>(EnumJsonOptions);

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(1, created!.AttendeesCount);
        Assert.Equal(2, created.ExpectedCount);
        Assert.Equal(new[] { ("Anna Nieobecna", false), ("Anna Obecna", true) },
            created.Attendance.Select(x => (x.FullName, x.IsPresent)));

        // Edycja bez listy obecności jej nie kasuje; nowa lista ją zastępuje.
        var keep = await admin.PutAsJsonAsync($"/api/supervisions/{created.Id}", new { Institution = "DOK", GroupLabel = group, SupervisionDate = "2026-10-11" });
        Assert.Equal(2, (await keep.Content.ReadFromJsonAsync<SupervisionDto>(EnumJsonOptions))!.Attendance.Count);
        var replace = await admin.PutAsJsonAsync($"/api/supervisions/{created.Id}", new
        {
            Institution = "DOK", GroupLabel = group, SupervisionDate = "2026-10-11",
            Attendance = new[] { new { PersonId = absent.Id, IsPresent = true } }
        });
        var replaced = await replace.Content.ReadFromJsonAsync<SupervisionDto>(EnumJsonOptions);
        Assert.Equal(1, replaced!.AttendeesCount);
        Assert.Equal(1, replaced.ExpectedCount);
        Assert.Equal(absent.Id, Assert.Single(replaced.Attendance).PersonId);

        var listed = (await admin.GetFromJsonAsync<List<SupervisionDto>>("/api/supervisions", EnumJsonOptions))!.Single(s => s.Id == created.Id);
        Assert.Single(listed.Attendance);
    }

    [Fact]
    public async Task UnknownPeople_AndAnEmptyGroup_AreRejected()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

        var unknown = await admin.PostAsJsonAsync("/api/supervisions", new
        {
            Institution = "DOK", GroupLabel = "Grupa", SupervisionDate = "2026-10-10",
            Attendance = new[] { new { PersonId = Guid.NewGuid(), IsPresent = true } }
        });
        var noGroup = await admin.PostAsJsonAsync("/api/supervisions", new { Institution = "DOK", GroupLabel = " ", SupervisionDate = "2026-10-10" });

        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Contains("nie ma w bazie", await unknown.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, noGroup.StatusCode);
    }
}
