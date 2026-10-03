using ClosedXML.Excel;
using DokPortal.Application.DokCases;
using DokPortal.Application.Export;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class ExportService : IExportService
{
    private readonly AppDbContext _db;
    private readonly ICaseScopeProvider _scope;

    public ExportService(AppDbContext db, ICaseScopeProvider? scope = null)
    {
        _db = db;
        _scope = scope ?? new AllCasesScopeProvider();
    }

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

    private static readonly Dictionary<DokPath, string> PathLabels = new()
    {
        [DokPath.BaptismCandidate] = "Kandydaci do Chrztu",
        [DokPath.Confirmation] = "Bierzmowanie",
        [DokPath.Communion] = "Stół Pański",
        [DokPath.Conversion] = "Konwersja",
        [DokPath.ReturnToUnity] = "Powrót do Jedności"
    };

    private static readonly Dictionary<DokStage, string> StageLabels = new()
    {
        [DokStage.Application] = "Zgłoszenie",
        [DokStage.Formation] = "Formacja",
        [DokStage.Sacrament] = "Sakrament",
        [DokStage.Graduate] = "Absolwent"
    };

    public async Task<byte[]> ExportDokCasesAsync(CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var cases = await _db.DokCases.ForScope(scope).AsNoTracking()
            .Include(c => c.Person).Include(c => c.CatechistPerson).Include(c => c.MentorPerson)
            .OrderBy(c => c.Person!.LastName).ThenBy(c => c.Person!.FirstName)
            .ToListAsync(ct);

        return BuildWorkbook(
            "Podopieczni DOK",
            new[] { "Osoba", "Ścieżka", "Etap", "Katechista", "Opiekun (mentor)", "Data ostatniego spotkania", "Data zakończenia" },
            cases.Select(c => new object?[]
            {
                c.Person?.FullName, PathLabels[c.Path], StageLabels[c.Stage], c.CatechistPerson?.FullName,
                c.MentorPerson?.FullName, FormatDate(c.LastMeetingDate), c.CompletedAtUtc?.ToString("yyyy-MM-dd")
            }));
    }

    public async Task<byte[]> ExportCandidatesAsync(CancellationToken ct)
    {
        var candidates = await _db.Candidates.AsNoTracking()
            .Include(c => c.Person)
            .OrderBy(c => c.Person!.LastName).ThenBy(c => c.Person!.FirstName).ThenBy(c => c.Year)
            .ToListAsync(ct);

        return BuildWorkbook(
            "Kandydaci SKŚP",
            new[] { "Osoba", "Rok", "Frekwencja (%)", "Opinie zebrane", "Opinie wymagane", "Rekolekcje" },
            candidates.Select(c => new object?[]
            {
                c.Person?.FullName, c.Year, c.AttendancePercentage, c.OpinionsCollected, c.OpinionsRequired, YesNo(c.IsRetreatCompleted)
            }));
    }

    private static readonly Dictionary<Institution, string> InstitutionLabels = new()
    {
        [Institution.SKSP] = "SKŚP",
        [Institution.DOK] = "DOK"
    };

    public async Task<byte[]> ExportMissionsAsync(CancellationToken ct)
    {
        var missions = await _db.CanonicalMissions.AsNoTracking()
            .Include(m => m.Person)
            .OrderBy(m => m.Person!.LastName).ThenBy(m => m.Person!.FirstName).ThenBy(m => m.MissionStartDate)
            .ToListAsync(ct);

        return BuildWorkbook(
            "Katechiści posłani",
            new[] { "Katechista", "Miejsce posługi", "Data od", "Data do", "Data udzielenia", "Miejsce udzielenia", "Grupa superwizyjna", "Posłany do DOK" },
            missions.Select(m => new object?[]
            {
                m.Person?.FullName, m.ServicePlace, FormatDate(m.MissionStartDate), FormatDate(m.MissionEndDate),
                FormatDate(m.GrantedDate), m.GrantedPlace, m.SupervisionGroup, YesNo(m.SentToDok)
            }));
    }

    public async Task<byte[]> ExportFormatorsAsync(CancellationToken ct)
    {
        var formators = await _db.Formators.AsNoTracking()
            .Include(f => f.Person)
            .OrderBy(f => f.Person!.LastName).ThenBy(f => f.Person!.FirstName)
            .ToListAsync(ct);

        return BuildWorkbook(
            "Formatorzy SKŚP",
            new[] { "Osoba", "Funkcja" },
            formators.Select(f => new object?[] { f.Person?.FullName, f.Function }));
    }

    public async Task<byte[]> ExportSupervisionsAsync(CancellationToken ct)
    {
        var supervisions = await _db.Supervisions.AsNoTracking()
            .OrderBy(s => s.SupervisionDate).ThenBy(s => s.GroupLabel)
            .ToListAsync(ct);

        return BuildWorkbook(
            "Superwizje",
            new[] { "Instytucja", "Grupa", "Data", "Obecnych", "Oczekiwanych", "Temat", "Wnioski" },
            supervisions.Select(s => new object?[]
            {
                InstitutionLabels[s.Institution], s.GroupLabel, FormatDate(s.SupervisionDate),
                s.AttendeesCount, s.ExpectedCount, s.Topic, s.Conclusion
            }));
    }

    public async Task<byte[]> ExportMeetingsAsync(CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var meetings = await _db.Meetings.ForScope(_db, scope).AsNoTracking()
            .Include(m => m.DokCase).ThenInclude(c => c!.Person)
            .OrderBy(m => m.MeetingDate)
            .ToListAsync(ct);

        return BuildWorkbook(
            "Spotkania",
            new[] { "Data", "Podopieczny / grupa", "Obecność", "Uwagi" },
            meetings.Select(m => new object?[]
            {
                FormatDate(m.MeetingDate),
                m.DokCase?.Person?.FullName ?? m.GroupLabel,
                m.IsAttended is bool attended ? YesNo(attended) : "—",
                m.Notes
            }));
    }

    public async Task<byte[]> ExportParishesAsync(CancellationToken ct)
    {
        var parishes = await _db.Parishes.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);

        return BuildWorkbook(
            "Parafie",
            new[] { "Nazwa", "Miejscowość" },
            parishes.Select(p => new object?[] { p.Name, p.City }));
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
