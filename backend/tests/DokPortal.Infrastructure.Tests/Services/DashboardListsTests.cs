using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

/// <summary>Pulpit wypisuje spotkania z najbliższych 7 dni i sprawy DOK z brakującymi dokumentami (nie tylko ich liczby).</summary>
public class DashboardListsTests
{
    private static readonly FixedTimeProvider Time = new(2026, 10, 7);

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Person NewPerson(string first, string last) => new()
    {
        Id = Guid.NewGuid(), FirstName = first, LastName = last, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
    };

    private static DokCase NewCase(Person person, Person catechist, DokPath path = DokPath.Confirmation) => new()
    {
        Id = Guid.NewGuid(), PersonId = person.Id, CatechistPersonId = catechist.Id, Path = path,
        Stage = path == DokPath.BaptismCandidate ? DokStage.Prekatechumenate : DokStage.Evangelization,
        CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
    };

    private static Meeting NewMeeting(DateOnly date, DokCase? dokCase = null, string? group = null, bool deleted = false) => new()
    {
        Id = Guid.NewGuid(), MeetingDate = date, DokCaseId = dokCase?.Id, GroupLabel = group, CreatedAtUtc = DateTime.UtcNow,
        DeletedAtUtc = deleted ? DateTime.UtcNow : null
    };

    private static CaseDocument NewDocument(DokCase dokCase, string name, bool provided = false) => new()
    {
        Id = Guid.NewGuid(), DokCaseId = dokCase.Id, Name = name, IsProvided = provided, CreatedAtUtc = DateTime.UtcNow
    };

    [Fact]
    public async Task UpcomingMeetings_ListsTheNextSevenDaysInOrder_LabelledByTheStudentOrTheGroup()
    {
        await using var db = CreateContext();
        var student = NewPerson("Jan", "Kowalski");
        var catechist = NewPerson("Anna", "Maj");
        var dokCase = NewCase(student, catechist);
        db.People.AddRange(student, catechist);
        db.DokCases.Add(dokCase);
        var later = NewMeeting(new DateOnly(2026, 10, 12), dokCase);
        var today = NewMeeting(new DateOnly(2026, 10, 7), group: "Grupa wtorkowa");
        db.Meetings.AddRange(
            later, today,
            NewMeeting(new DateOnly(2026, 10, 6), group: "Wczoraj"),
            NewMeeting(new DateOnly(2026, 10, 15), group: "Za późno"),
            NewMeeting(new DateOnly(2026, 10, 8), group: "Usunięte", deleted: true));
        await db.SaveChangesAsync();

        var summary = await new DashboardService(db, null, Time).GetSummaryAsync(default);

        Assert.Equal(2, summary.UpcomingMeetingsCount);
        Assert.Equal(new[] { "Grupa wtorkowa", "Jan Kowalski" }, summary.UpcomingMeetings.Select(m => m.Label));
        Assert.Equal(new[] { new DateOnly(2026, 10, 7), new DateOnly(2026, 10, 12) }, summary.UpcomingMeetings.Select(m => m.MeetingDate));
        Assert.Equal(new[] { today.Id, later.Id }, summary.UpcomingMeetings.Select(m => m.MeetingId));
    }

    [Fact]
    public async Task UpcomingMeetings_ShowsAtMostTenEarliest_WhileTheCountStaysTrue()
    {
        await using var db = CreateContext();
        for (var i = 0; i < 12; i++)
        {
            db.Meetings.Add(NewMeeting(new DateOnly(2026, 10, 7).AddDays(i % 7), group: $"Grupa {i:00}"));
        }
        await db.SaveChangesAsync();

        var summary = await new DashboardService(db, null, Time).GetSummaryAsync(default);

        Assert.Equal(12, summary.UpcomingMeetingsCount);
        Assert.Equal(10, summary.UpcomingMeetings.Count);
    }

    [Fact]
    public async Task MissingDocumentsCases_ListsEachCaseOnce_WithTheNamesOfWhatIsMissing_AlphabeticallyByPerson()
    {
        await using var db = CreateContext();
        var catechist = NewPerson("Anna", "Maj");
        var zielinski = NewPerson("Marek", "Zieliński");
        var kowalski = NewPerson("Jan", "Kowalski");
        var complete = NewPerson("Ewa", "Kompletna");
        db.People.AddRange(catechist, zielinski, kowalski, complete);
        var zielinskiCase = NewCase(zielinski, catechist, DokPath.BaptismCandidate);
        var kowalskiCase = NewCase(kowalski, catechist);
        var completeCase = NewCase(complete, catechist);
        db.DokCases.AddRange(zielinskiCase, kowalskiCase, completeCase);
        db.CaseDocuments.AddRange(
            NewDocument(zielinskiCase, "Metryka chrztu"),
            NewDocument(kowalskiCase, "Zaświadczenie"), NewDocument(kowalskiCase, "Akt urodzenia"), NewDocument(kowalskiCase, "Zgoda", provided: true),
            NewDocument(completeCase, "Komplet", provided: true));
        await db.SaveChangesAsync();

        var summary = await new DashboardService(db, null, Time).GetSummaryAsync(default);

        Assert.Equal(2, summary.MissingDocumentsCasesCount);
        Assert.Equal(new[] { "Jan Kowalski", "Marek Zieliński" }, summary.MissingDocumentsCases.Select(c => c.PersonFullName));
        var first = summary.MissingDocumentsCases[0];
        Assert.Equal(kowalskiCase.Id, first.CaseId);
        Assert.Equal("Confirmation", first.Path);
        Assert.Equal(new[] { "Akt urodzenia", "Zaświadczenie" }, first.MissingDocuments);
        Assert.Equal("BaptismCandidate", summary.MissingDocumentsCases[1].Path);
    }

    [Fact]
    public async Task MissingDocumentsCases_ShowsAtMostTwenty_WhileTheCountStaysTrue()
    {
        await using var db = CreateContext();
        var catechist = NewPerson("Anna", "Maj");
        db.People.Add(catechist);
        for (var i = 0; i < 23; i++)
        {
            var person = NewPerson("Jan", $"Nazwisko{i:00}");
            db.People.Add(person);
            var dokCase = NewCase(person, catechist);
            db.DokCases.Add(dokCase);
            db.CaseDocuments.Add(NewDocument(dokCase, "Metryka"));
        }
        await db.SaveChangesAsync();

        var summary = await new DashboardService(db, null, Time).GetSummaryAsync(default);

        Assert.Equal(23, summary.MissingDocumentsCasesCount);
        Assert.Equal(20, summary.MissingDocumentsCases.Count);
        Assert.Equal("Jan Nazwisko00", summary.MissingDocumentsCases[0].PersonFullName);
    }
}
