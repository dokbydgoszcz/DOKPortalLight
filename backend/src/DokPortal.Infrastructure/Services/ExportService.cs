using ClosedXML.Excel;
using DokPortal.Application.Export;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class ExportService : IExportService
{
    private readonly AppDbContext _db;

    public ExportService(AppDbContext db) => _db = db;

    public async Task<byte[]> ExportPeopleAsync(CancellationToken ct)
    {
        var people = await _db.People.AsNoTracking()
            .Include(p => p.Parish)
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .ToListAsync(ct);

        return BuildWorkbook(
            "Osoby",
            new[] { "Imię", "Nazwisko", "E-mail", "Telefon", "Data urodzenia", "Parafia", "Imieniny (DD.MM)" },
            people.Select(p => new object?[]
            {
                p.FirstName, p.LastName, p.Email, p.Phone, FormatDate(p.BirthDate), p.Parish?.Name,
                p.NameDayDay is int day && p.NameDayMonth is int month ? $"{day:00}.{month:00}" : null
            }));
    }

    private static string? FormatDate(DateOnly? date) => date?.ToString("yyyy-MM-dd");

    private static string YesNo(bool value) => value ? "Tak" : "Nie";

    private static byte[] BuildWorkbook(string sheetName, string[] headers, IEnumerable<object?[]> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);

        for (var column = 0; column < headers.Length; column++)
        {
            sheet.Cell(1, column + 1).Value = headers[column];
        }
        sheet.Row(1).Style.Font.Bold = true;

        var rowNumber = 2;
        foreach (var row in rows)
        {
            for (var column = 0; column < row.Length; column++)
            {
                switch (row[column])
                {
                    case null:
                        break;
                    case int number:
                        sheet.Cell(rowNumber, column + 1).Value = number;
                        break;
                    default:
                        sheet.Cell(rowNumber, column + 1).Value = row[column]!.ToString();
                        break;
                }
            }
            rowNumber++;
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
