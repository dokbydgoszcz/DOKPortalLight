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
                Id = Guid.NewGuid(), PersonId = person.Id, Year = 2, AttendancePercentage = 85,
                OpinionsCollected = 1, OpinionsRequired = 2, IsRetreatCompleted = true
            },
            new Candidate { Id = Guid.NewGuid(), PersonId = person.Id, Year = 3, OpinionsCollected = 0, OpinionsRequired = 2 },
            new Candidate { Id = Guid.NewGuid(), PersonId = person.Id, Year = 1, DeletedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var sheet = OpenSheet(await new ExportService(db).ExportCandidatesAsync(default));

        Assert.Equal(
            new[] { "Osoba", "Rok", "Frekwencja (%)", "Opinie zebrane", "Opinie wymagane", "Rekolekcje" },
            Row(sheet, 1, 6));
        Assert.Equal(new[] { "Jan Kowalski", "2", "85", "1", "2", "Tak" }, Row(sheet, 2, 6));
        Assert.Equal(new[] { "Jan Kowalski", "3", "", "0", "2", "Nie" }, Row(sheet, 3, 6));
        Assert.Equal(3, sheet.LastRowUsed()!.RowNumber());
    }
}
