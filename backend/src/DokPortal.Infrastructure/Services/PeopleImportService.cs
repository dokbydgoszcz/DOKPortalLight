using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DokPortal.Application.People;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

/// <summary>
/// Wklejona lista osób: dopasowanie po imieniu i nazwisku (bez dublowania), nadanie funkcji.
/// Kontaktów istniejących osób nie nadpisuje (uzupełnia puste), e-maili nie wysyła, kont nie zakłada.
/// </summary>
public class PeopleImportService : IPeopleImportService
{
    private const int MaxLines = 1000;
    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public PeopleImportService(AppDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    private sealed record Entry(int Line, string FirstName, string LastName, string? Email, string? Phone, int? Year, List<string> Warnings)
    {
        public int? CandidateYear { get; set; }
    }

    public async Task<PeopleImportResultDto> RunAsync(PeopleImportRequest request, bool apply, CancellationToken ct)
    {
        var skipped = new List<string>();
        var entries = Parse(request, skipped);
        if (entries.Count == 0) throw new InvalidOperationException("Lista jest pusta – wklej co najmniej jedną osobę („Nazwisko Imię” w wierszu).");
        if (entries.Count > MaxLines) throw new InvalidOperationException($"Za dużo wierszy (najwyżej {MaxLines} naraz).");

        if (request.EnrollAsCandidates && entries.Any(e => (e.Year ?? request.CandidateYear) is not (>= 1 and <= 3)))
            throw new InvalidOperationException("Podaj rok formacji (I–III) – wybierz go w formularzu albo poprzedź osoby nagłówkiem „Rok II”, „Rok III”.");
        var people = await _db.People.Include(p => p.Functions).ToListAsync(ct);
        var candidacies = await _db.Candidates.Select(c => new { c.PersonId, c.FormationYear, c.IsFormationCompleted }).ToListAsync(ct);
        var emailsInUse = new HashSet<string>(people.Where(p => p.Email != null).Select(p => p.Email!.Trim().ToLowerInvariant()));
        var now = _time.GetUtcNow().UtcDateTime;
        var note = string.IsNullOrWhiteSpace(request.FunctionNote) ? null : request.FunctionNote.Trim();
        var created = new Dictionary<string, Person>();
        var rows = new List<PeopleImportRowDto>();
        var seen = new HashSet<string>();
        int existing = 0, granted = 0, enrolled = 0;

        foreach (var entry in entries)
        {
            var key = Norm(entry.FirstName) + "|" + Norm(entry.LastName);
            if (!seen.Add(key))
            {
                entry.Warnings.Add("Ta osoba jest na liście drugi raz – pominięto.");
                rows.Add(Row(entry, false, true));
                continue;
            }
            var matches = people.Where(p => Norm(p.FirstName) + "|" + Norm(p.LastName) == key).ToList();
            Person? person = matches.Count switch
            {
                0 => null,
                1 => matches[0],
                _ => matches.FirstOrDefault(m => request.Function is { } f && m.Functions.Any(x => x.Type == f)) ?? matches.OrderBy(m => m.CreatedAtUtc).First()
            };
            if (matches.Count > 1) entry.Warnings.Add($"W bazie jest {matches.Count} osób o tym imieniu i nazwisku – wybrano jedną, sprawdź w „Osoby”.");

            var isNew = person is null;
            if (person is null)
            {
                person = new Person { Id = Guid.NewGuid(), FirstName = entry.FirstName, LastName = entry.LastName, CreatedAtUtc = now, UpdatedAtUtc = now };
                created[key] = person;
                if (apply) _db.People.Add(person);
            }
            else existing++;

            if (entry.Email is { } mail && string.IsNullOrWhiteSpace(person.Email))
            {
                if (!emailsInUse.Add(mail.ToLowerInvariant())) entry.Warnings.Add($"Adres {mail} ma już inna osoba – nie przypisano.");
                else if (apply) person.Email = mail;
            }
            else if (entry.Email is { } other && !string.Equals(other, person.Email, StringComparison.OrdinalIgnoreCase))
                entry.Warnings.Add($"Osoba ma już inny e-mail ({person.Email}) – zostawiono dotychczasowy.");
            if (entry.Phone is { } phone && string.IsNullOrWhiteSpace(person.Phone) && apply) person.Phone = phone;

            var hadFunction = request.Function is { } type && person.Functions.Any(f => f.Type == type);
            if (request.Function is { } function && !hadFunction)
            {
                granted++;
                if (apply)
                {
                    var added = new PersonFunction { Id = Guid.NewGuid(), PersonId = person.Id, Type = function, Notes = note };
                    if (isNew) person.Functions.Add(added); else _db.PersonFunctions.Add(added);
                }
            }
            if (request.EnrollAsCandidates)
            {
                var year = (entry.Year ?? request.CandidateYear)!.Value;
                var current = candidacies.FirstOrDefault(c => c.PersonId == person.Id);
                if (current is not null)
                    entry.Warnings.Add(current.IsFormationCompleted ? "Ukończyła już formację SKŚP – nie wpisano ponownie." : $"Jest już kandydatem SKŚP (rok {current.FormationYear}) – bez zmian.");
                else
                {
                    entry.CandidateYear = year;
                    enrolled++;
                    if (apply)
                    {
                        var candidate = new Candidate
                        {
                            Id = Guid.NewGuid(), PersonId = person.Id, FormationYear = year, FormationYearSinceUtc = now, CreatedAtUtc = now, UpdatedAtUtc = now
                        };
                        candidate.Events.Add(new CandidateFormationEvent
                        {
                            Id = Guid.NewGuid(), CandidateId = candidate.Id, Sequence = 1, Kind = CandidateFormationEventKind.Enrolled, ToYear = year, AtUtc = now,
                            Note = "Import listy uczniów"
                        });
                        _db.Candidates.Add(candidate);
                    }
                }
            }
            if (apply && !isNew) person.UpdatedAtUtc = now;
            rows.Add(Row(entry, isNew, hadFunction));
        }

        if (apply) await _db.SaveChangesAsync(ct);
        else _db.ChangeTracker.Clear();
        return new PeopleImportResultDto
        {
            Applied = apply, NewPeople = created.Count, ExistingPeople = existing, FunctionsGranted = granted, CandidatesEnrolled = enrolled, Rows = rows, SkippedLines = skipped
        };
    }

    private static PeopleImportRowDto Row(Entry e, bool isNew, bool hadFunction) => new()
    {
        Line = e.Line, FirstName = e.FirstName, LastName = e.LastName, Email = e.Email, Phone = e.Phone,
        NewPerson = isNew, HadFunction = hadFunction, CandidateYear = e.CandidateYear, Warnings = e.Warnings
    };

    private static List<Entry> Parse(PeopleImportRequest request, List<string> skipped)
    {
        var result = new List<Entry>();
        var lines = (request.Text ?? "").Replace("\r", "").Split('\n');
        int? section = null;
        for (var i = 0; i < lines.Length; i++)
        {
            var raw = lines[i].Trim();
            if (raw.Length == 0) continue;
            var header = Regex.Match(raw, @"^rok\s+(III|II|I|[1-3])\b\.?\s*$", RegexOptions.IgnoreCase);
            if (header.Success)
            {
                section = header.Groups[1].Value.ToUpperInvariant() switch { "I" or "1" => 1, "II" or "2" => 2, _ => 3 };
                continue;
            }
            var parts = raw.Split(new[] { '\t', ';' }, StringSplitOptions.TrimEntries);
            var namePart = Regex.Replace(parts[0], @"^\d+[.)]?\s+", "");
            // Kolumny z Excela: „1 | Kowalski | Jan | …” albo „Kowalski | Jan”.
            var rest = parts.Skip(1).ToList();
            if (Regex.IsMatch(parts[0], @"^\d+[.)]?$") && rest.Count > 0) { namePart = rest[0]; rest.RemoveAt(0); }
            if (rest.Count > 0 && !rest[0].Contains('@') && !Regex.IsMatch(rest[0], @"^[\d\s+()-]{6,}$") && !namePart.Contains(' '))
            {
                namePart = namePart + " " + rest[0];
                rest.RemoveAt(0);
            }
            var words = namePart.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Capitalize).ToList();
            // Wiersze nagłówków („Nazwisko i imię”, „Szkoła Katechistów…”) i pojedyncze słowa pomijamy.
            if (words.Count < 2 || Regex.IsMatch(namePart, @"(\bnazwisk|\bimię\b|\bimie\b|\blp\b|szkoł|szkol|katechist|zakończ|rocznik|akademick)", RegexOptions.IgnoreCase) || namePart.Any(char.IsDigit))
            {
                skipped.Add(raw);
                continue;
            }
            var warnings = new List<string>();
            if (words.Count > 2) warnings.Add("Więcej niż dwa słowa – sprawdź, które jest imieniem.");
            string first, last;
            if (request.FirstNameFirst) { first = words[0]; last = string.Join(" ", words.Skip(1)); }
            else { last = string.Join(" ", words.Take(words.Count - 1)); first = words[^1]; }
            var email = rest.FirstOrDefault(r => r.Contains('@'));
            var phone = rest.FirstOrDefault(r => Regex.IsMatch(r, @"^[\d\s+()-]{6,}$"));
            if (first.Length > 100 || last.Length > 100)
            {
                skipped.Add(raw);
                continue;
            }
            result.Add(new Entry(i + 1, first, last, email, phone, section, warnings));
        }
        return result;
    }

    private static string Capitalize(string word) =>
        word.Length > 1 && word.ToUpperInvariant() == word
            ? string.Join("-", word.Split('-').Select(p => p.Length == 0 ? p : p[0] + p[1..].ToLowerInvariant()))
            : word;

    private static string Norm(string text)
    {
        var d = text.Trim().ToLowerInvariant().Replace('ł', 'l').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in d)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return Regex.Replace(sb.ToString(), @"[^a-z0-9]+", " ").Trim();
    }
}
