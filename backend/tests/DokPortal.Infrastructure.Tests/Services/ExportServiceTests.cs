using ClosedXML.Excel;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class ExportServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static IXLWorksheet OpenSheet(byte[] bytes) => new XLWorkbook(new MemoryStream(bytes)).Worksheet(1);

    private static string[] Row(IXLWorksheet sheet, int row, int columns) =>
        Enumerable.Range(1, columns).Select(c => sheet.Cell(row, c).GetString()).ToArray();

    [Fact]
    public async Task ExportPeopleAsync_WritesPolishHeadersSortedRowsAndSkipsNotesAndDeleted()
    {
        await using var db = CreateContext();
        var parish = new Parish { Id = Guid.NewGuid(), Name = "Św. Pawła" };
        db.Parishes.Add(parish);
        db.People.AddRange(
            new Person
            {
                Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski", Email = "jan@example.org", Phone = "600100200",
                BirthDate = new DateOnly(1990, 5, 17), ParishId = parish.Id, Notes = "tajna uwaga", NameDayMonth = 6, NameDayDay = 24
            },
            new Person { Id = Guid.NewGuid(), FirstName = "Anna", LastName = "Adamska" },
            new Person { Id = Guid.NewGuid(), FirstName = "Usunięty", LastName = "Zenon", DeletedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var sheet = OpenSheet(await new ExportService(db).ExportPeopleAsync(default));

        Assert.Equal(
            new[] { "Imię", "Nazwisko", "E-mail", "Telefon", "Data urodzenia", "Parafia", "Imieniny (DD.MM)" },
            Row(sheet, 1, 7));
        Assert.Equal(new[] { "Anna", "Adamska", "", "", "", "", "" }, Row(sheet, 2, 7));
        Assert.Equal(
            new[] { "Jan", "Kowalski", "jan@example.org", "600100200", "1990-05-17", "Św. Pawła", "24.06" },
            Row(sheet, 3, 7));
        Assert.Equal(3, sheet.LastRowUsed()!.RowNumber());
        Assert.DoesNotContain(sheet.CellsUsed(), c => c.GetString().Contains("tajna uwaga"));
    }

    private static Person NewPerson(string first, string last) => new() { Id = Guid.NewGuid(), FirstName = first, LastName = last };

    [Fact]
    public async Task ExportDokCasesAsync_WritesPolishLabelsAndNames()
    {
        await using var db = CreateContext();
        var person = NewPerson("Jan", "Kowalski");
        var catechist = NewPerson("Anna", "Nowak");
        var mentor = NewPerson("Piotr", "Wiśniewski");
        db.People.AddRange(person, catechist, mentor);
        db.DokCases.AddRange(
            new DokCase
            {
                Id = Guid.NewGuid(), PersonId = person.Id, CatechistPersonId = catechist.Id,
                Path = DokPath.Confirmation, Stage = DokStage.Formation, LastMeetingDate = new DateOnly(2026, 9, 30)
            },
            new DokCase
            {
                Id = Guid.NewGuid(), PersonId = mentor.Id, CatechistPersonId = catechist.Id, MentorPersonId = person.Id,
                Path = DokPath.ReturnToUnity, Stage = DokStage.Graduate, CompletedAtUtc = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc)
            },
            new DokCase
            {
                Id = Guid.NewGuid(), PersonId = person.Id, CatechistPersonId = catechist.Id,
                Path = DokPath.Communion, Stage = DokStage.Application, DeletedAtUtc = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var sheet = OpenSheet(await new ExportService(db).ExportDokCasesAsync(default));

        Assert.Equal(
            new[] { "Osoba", "Ścieżka", "Etap", "Katechista", "Opiekun (mentor)", "Data ostatniego spotkania", "Data zakończenia" },
            Row(sheet, 1, 7));
        Assert.Equal(
            new[] { "Jan Kowalski", "Bierzmowanie", "Formacja", "Anna Nowak", "", "2026-09-30", "" },
            Row(sheet, 2, 7));
        Assert.Equal(
            new[] { "Piotr Wiśniewski", "Powrót do Jedności", "Absolwent", "Anna Nowak", "Jan Kowalski", "", "2026-08-01" },
            Row(sheet, 3, 7));
        Assert.Equal(3, sheet.LastRowUsed()!.RowNumber());
    }

    [Fact]
    public async Task ExportCandidatesAsync_WritesNumbersAndYesNo()
    {
        await using var db = CreateContext();
        var person = NewPerson("Jan", "Kowalski");
        db.People.Add(person);
        db.Candidates.AddRange(
            new Candidate
            {
                Id = Guid.NewGuid(), PersonId = person.Id, FormationStartYear = 2025, AttendancePercentage = 85,
                OpinionsCollected = 1, OpinionsRequired = 2,
                Retreats =
                {
                    new CandidateRetreat { Id = Guid.NewGuid(), Year = 2, IsCompleted = true },
                    new CandidateRetreat { Id = Guid.NewGuid(), Year = 1, IsCompleted = true },
                    new CandidateRetreat { Id = Guid.NewGuid(), Year = 3, IsCompleted = false }
                }
            },
            new Candidate { Id = Guid.NewGuid(), PersonId = person.Id, FormationStartYear = 2022, OpinionsCollected = 0, OpinionsRequired = 2 },
            new Candidate { Id = Guid.NewGuid(), PersonId = person.Id, FormationStartYear = 2024, IsFormationStopped = true, FormationStopNote = "x" },
            new Candidate { Id = Guid.NewGuid(), PersonId = person.Id, FormationStartYear = 2026, DeletedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var sheet = OpenSheet(await new ExportService(db, null, new FixedTimeProvider(2026, 10, 3)).ExportCandidatesAsync(default));

        Assert.Equal(
            new[] { "Osoba", "Rok", "Status", "Frekwencja (%)", "Opinie zebrane", "Opinie wymagane", "Rekolekcje" },
            Row(sheet, 1, 7));
        Assert.Equal(new[] { "Jan Kowalski", "2", "W formacji", "85", "1", "2", "I, II" }, Row(sheet, 2, 7));
        Assert.Equal(new[] { "Jan Kowalski", "3", "Formacja zatrzymana", "", "0", "2", "—" }, Row(sheet, 3, 7));
        Assert.Equal(new[] { "Jan Kowalski", "3", "Ukończył formację", "", "0", "2", "—" }, Row(sheet, 4, 7));
        Assert.Equal(4, sheet.LastRowUsed()!.RowNumber());
    }

    [Fact]
    public async Task ExportMissionsAsync_WritesDatesAndPlaces()
    {
        await using var db = CreateContext();
        var person = NewPerson("Jan", "Kowalski");
        db.People.Add(person);
        db.CanonicalMissions.AddRange(
            new CanonicalMission
            {
                Id = Guid.NewGuid(), PersonId = person.Id, ServicePlace = "Parafia św. Jana",
                MissionStartDate = new DateOnly(2026, 1, 1), MissionEndDate = new DateOnly(2027, 1, 1),
                GrantedDate = new DateOnly(2025, 12, 20), GrantedPlace = "Bydgoszcz", SupervisionGroup = "Grupa A", SentToDok = true
            },
            new CanonicalMission
            {
                Id = Guid.NewGuid(), PersonId = person.Id, ServicePlace = "Usunięta",
                MissionStartDate = new DateOnly(2020, 1, 1), MissionEndDate = new DateOnly(2021, 1, 1), DeletedAtUtc = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var waiting = NewPerson("Anna", "Czekajaca");
        db.People.Add(waiting);
        db.Candidates.Add(new Candidate { Id = Guid.NewGuid(), PersonId = waiting.Id, FormationStartYear = 2022, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var sheet = OpenSheet(await new ExportService(db, null, new FixedTimeProvider(2026, 10, 3)).ExportMissionsAsync(default));

        Assert.Equal(
            new[] { "Katechista", "Miejsce posługi", "Data od", "Data do", "Data udzielenia", "Miejsce udzielenia", "Grupa superwizyjna", "Posłany do DOK", "Status" },
            Row(sheet, 1, 9));
        Assert.Equal(new[] { "Anna Czekajaca", "", "", "", "", "", "", "Nie", "Przed udzieleniem posługi" }, Row(sheet, 2, 9));
        Assert.Equal(
            new[] { "Jan Kowalski", "Parafia św. Jana", "2026-01-01", "2027-01-01", "2025-12-20", "Bydgoszcz", "Grupa A", "Tak", "ważna" },
            Row(sheet, 3, 9));
        Assert.Equal(3, sheet.LastRowUsed()!.RowNumber());
    }

    [Fact]
    public async Task ExportFormatorsAsync_WritesPersonAndFunction()
    {
        await using var db = CreateContext();
        var person = NewPerson("Jan", "Kowalski");
        db.People.Add(person);
        db.Formators.AddRange(
            new Formator { Id = Guid.NewGuid(), PersonId = person.Id, Function = "Wykładowca" },
            new Formator { Id = Guid.NewGuid(), PersonId = person.Id, Function = "Usunięty", DeletedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var sheet = OpenSheet(await new ExportService(db).ExportFormatorsAsync(default));

        Assert.Equal(new[] { "Osoba", "Funkcja" }, Row(sheet, 1, 2));
        Assert.Equal(new[] { "Jan Kowalski", "Wykładowca" }, Row(sheet, 2, 2));
        Assert.Equal(2, sheet.LastRowUsed()!.RowNumber());
    }

    [Fact]
    public async Task ExportSupervisionsAsync_WritesInstitutionLabelAndCounts()
    {
        await using var db = CreateContext();
        db.Supervisions.AddRange(
            new Supervision
            {
                Id = Guid.NewGuid(), Institution = Institution.SKSP, GroupLabel = "Grupa A", SupervisionDate = new DateOnly(2026, 9, 15),
                AttendeesCount = 8, ExpectedCount = 10, Topic = "Modlitwa", Conclusion = "Wnioski"
            },
            new Supervision { Id = Guid.NewGuid(), Institution = Institution.DOK, GroupLabel = "Grupa B", SupervisionDate = new DateOnly(2026, 9, 20) });
        await db.SaveChangesAsync();

        var sheet = OpenSheet(await new ExportService(db).ExportSupervisionsAsync(default));

        Assert.Equal(new[] { "Instytucja", "Grupa", "Data", "Obecnych", "Oczekiwanych", "Temat", "Wnioski" }, Row(sheet, 1, 7));
        Assert.Equal(new[] { "SKŚP", "Grupa A", "2026-09-15", "8", "10", "Modlitwa", "Wnioski" }, Row(sheet, 2, 7));
        Assert.Equal(new[] { "DOK", "Grupa B", "2026-09-20", "", "", "", "" }, Row(sheet, 3, 7));
    }

    [Fact]
    public async Task ExportMeetingsAsync_UsesCasePersonOrGroupLabelAndAttendanceText()
    {
        await using var db = CreateContext();
        var person = NewPerson("Jan", "Kowalski");
        var catechist = NewPerson("Anna", "Nowak");
        db.People.AddRange(person, catechist);
        var dokCase = new DokCase
        {
            Id = Guid.NewGuid(), PersonId = person.Id, CatechistPersonId = catechist.Id,
            Path = DokPath.Confirmation, Stage = DokStage.Formation
        };
        db.DokCases.Add(dokCase);
        db.Meetings.AddRange(
            new Meeting { Id = Guid.NewGuid(), DokCaseId = dokCase.Id, MeetingDate = new DateOnly(2026, 10, 1), IsAttended = true, Notes = "ok" },
            new Meeting { Id = Guid.NewGuid(), GroupLabel = "Grupa B", MeetingDate = new DateOnly(2026, 10, 2) },
            new Meeting { Id = Guid.NewGuid(), GroupLabel = "Grupa C", MeetingDate = new DateOnly(2026, 10, 3), IsAttended = false },
            new Meeting { Id = Guid.NewGuid(), GroupLabel = "Usunięte", MeetingDate = new DateOnly(2026, 10, 4), DeletedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var sheet = OpenSheet(await new ExportService(db).ExportMeetingsAsync(default));

        Assert.Equal(new[] { "Data", "Podopieczny / grupa", "Obecność", "Uwagi" }, Row(sheet, 1, 4));
        Assert.Equal(new[] { "2026-10-01", "Jan Kowalski", "Tak", "ok" }, Row(sheet, 2, 4));
        Assert.Equal(new[] { "2026-10-02", "Grupa B", "—", "" }, Row(sheet, 3, 4));
        Assert.Equal(new[] { "2026-10-03", "Grupa C", "Nie", "" }, Row(sheet, 4, 4));
        Assert.Equal(4, sheet.LastRowUsed()!.RowNumber());
    }

    [Fact]
    public async Task ExportParishesAsync_WritesNameAndCitySortedByName()
    {
        await using var db = CreateContext();
        db.Parishes.AddRange(
            new Parish { Id = Guid.NewGuid(), Name = "Św. Pawła", City = "Bydgoszcz" },
            new Parish { Id = Guid.NewGuid(), Name = "Matki Bożej" },
            new Parish { Id = Guid.NewGuid(), Name = "Usunięta", DeletedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var sheet = OpenSheet(await new ExportService(db).ExportParishesAsync(default));

        Assert.Equal(new[] { "Nazwa", "Miejscowość" }, Row(sheet, 1, 2));
        Assert.Equal(new[] { "Matki Bożej", "" }, Row(sheet, 2, 2));
        Assert.Equal(new[] { "Św. Pawła", "Bydgoszcz" }, Row(sheet, 3, 2));
        Assert.Equal(3, sheet.LastRowUsed()!.RowNumber());
    }
}
