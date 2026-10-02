using ClosedXML.Excel;
using DokPortal.Domain.Entities;
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
}
