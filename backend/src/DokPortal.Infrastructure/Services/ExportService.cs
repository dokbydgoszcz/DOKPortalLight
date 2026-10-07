using ClosedXML.Excel;
using DokPortal.Application.DokCases;
using DokPortal.Application.Export;
using DokPortal.Domain.Enums;
using DokPortal.Domain.Formation;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class ExportService : IExportService
{
    private readonly AppDbContext _db;
    private readonly ICaseScopeProvider _scope;
    private readonly TimeProvider _time;

    public ExportService(AppDbContext db, ICaseScopeProvider? scope = null, TimeProvider? time = null)
    {
        _db = db;
        _scope = scope ?? new AllCasesScopeProvider();
        _time = time ?? TimeProvider.System;
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
                c.Person?.FullName, DokStages.Label(c.Path), DokStages.Label(c.Stage), c.CatechistPerson?.FullName,
                c.MentorPerson?.FullName, FormatDate(c.LastMeetingDate), c.CompletedAtUtc?.ToString("yyyy-MM-dd")
            }));
    }

    public async Task<byte[]> ExportCandidatesAsync(CancellationToken ct)
    {
        var candidates = await _db.Candidates.AsNoTracking()
            .Include(c => c.Person)
            .Include(c => c.Retreats)
            .OrderBy(c => c.Person!.LastName).ThenBy(c => c.Person!.FirstName).ThenByDescending(c => c.FormationStartYear)
            .ToListAsync(ct);
        var today = _time.Today();

        return BuildWorkbook(
            "Kandydaci SKŚP",
            new[] { "Osoba", "Rok", "Status", "Frekwencja (%)", "Opinie zebrane", "Opinie wymagane", "Rekolekcje" },
            candidates.Select(c => new object?[]
            {
                c.Person?.FullName, Math.Min(FormationCalendar.YearOf(c.FormationStartYear, today), FormationCalendar.YearsOfFormation),
                CandidateStatusLabels[FormationCalendar.StatusOf(c.FormationStartYear, c.IsFormationStopped, today)],
                c.AttendancePercentage, c.OpinionsCollected, c.OpinionsRequired, CompletedRetreatYears(c)
            }));
    }

    private static readonly Dictionary<CandidateFormationStatus, string> CandidateStatusLabels = new()
    {
        [CandidateFormationStatus.InFormation] = "W formacji",
        [CandidateFormationStatus.Completed] = "Ukończył formację",
        [CandidateFormationStatus.Stopped] = "Formacja zatrzymana"
    };

    private static readonly string[] RomanYears = { "", "I", "II", "III" };

    /// <summary>Lata formacji z zaliczonymi rekolekcjami, np. "I, II"; "—" gdy żadnych.</summary>
    private static string CompletedRetreatYears(DokPortal.Domain.Entities.Candidate c)
    {
        var years = c.Retreats.Where(r => r.IsCompleted && r.Year is >= 1 and <= 3).OrderBy(r => r.Year).Select(r => RomanYears[r.Year]).ToList();
        return years.Count == 0 ? "—" : string.Join(", ", years);
    }

    private const string PendingStatusLabel = "Przed udzieleniem posługi";

    private static string MissionStatus(DateOnly endDate, DateOnly today) =>
        endDate < today ? "wygasła" : endDate <= today.AddDays(30) ? "wygasa" : "ważna";

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
        var pending = await new MissionService(_db, _time).GetPendingAsync(ct);
        var today = _time.Today();

        // osoby czekające na udzielenie posługi są na liście pierwsze, tak jak na ekranie
        var rows = pending
            .Select(p => new object?[] { p.PersonFullName, null, null, null, null, null, "Nie", PendingStatusLabel })
            .Concat(missions.Select(m => new object?[]
            {
                m.Person?.FullName, m.ServicePlace, FormatDate(m.MissionStartDate), FormatDate(m.MissionEndDate),
                FormatDate(m.GrantedDate), m.SupervisionGroup, YesNo(m.SentToDok), MissionStatus(m.MissionEndDate, today)
            }));

        return BuildWorkbook(
            "Katechiści",
            new[] { "Katechista", "Miejsce posługi", "Data od", "Data do", "Data udzielenia", "Grupa superwizyjna", "Posłany do DOK", "Status" },
            rows);
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
