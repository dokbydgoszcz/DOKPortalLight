using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using DokPortal.Application.Parishes;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

/// <summary>
/// Import spisu parafii z proboszczami. Parafie dopasowuje po miejscowości i wezwaniu, osoby po imieniu i nazwisku.
/// Nie nadpisuje kontaktów istniejących osób (uzupełnia puste) i nie wysyła e-maili.
/// </summary>
public class ParishImportService : IParishImportService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public ParishImportService(AppDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    private sealed record Line(int Row, string? Deanery, string City, string Parish, string FirstName, string LastName, string? Function,
        string? Titles, string? Order, int? Since, string? PastorPhone, string? PastorEmail,
        string? Address, string? Phone, string? Email, string? Website);

    public async Task<ParishImportResultDto> RunAsync(Stream xlsx, bool apply, CancellationToken ct)
    {
        var lines = Read(xlsx);
        var parishes = await _db.Parishes.ToListAsync(ct);
        var people = await _db.People.Include(p => p.Functions).ToListAsync(ct);
        var pastorFunctions = await _db.PersonFunctions.Where(f => f.Type == FunctionType.Pastor).ToListAsync(ct);
        var emailsInUse = new HashSet<string>(people.Where(p => p.Email != null).Select(p => p.Email!.Trim().ToLowerInvariant()));
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        var rows = new List<ParishImportRowDto>();
        var newPeople = new Dictionary<string, Person>();
        var usedParishes = new HashSet<Guid>();
        int newParishes = 0, matched = 0, set = 0, unchanged = 0, replaced = 0;
        var existingPeople = new HashSet<Guid>();

        foreach (var line in lines)
        {
            var warnings = new List<string>();

            // Parafia: dopasowanie po miejscowości i wezwaniu.
            var parish = MatchParish(parishes.Where(p => !usedParishes.Contains(p.Id)), line, out var ambiguous);
            if (ambiguous) warnings.Add("Kilka parafii w tej miejscowości pasuje podobnie – wybrano najbliższą nazwą.");
            var isNewParish = parish is null;
            if (parish is null)
            {
                parish = new Parish { Id = Guid.NewGuid(), Name = ShortName(line.Parish), City = line.City };
                parishes.Add(parish);
                if (apply) _db.Parishes.Add(parish);
                newParishes++;
            }
            else matched++;
            usedParishes.Add(parish.Id);
            if (apply)
            {
                parish.Deanery = line.Deanery ?? parish.Deanery;
                parish.Address = line.Address ?? parish.Address;
                parish.Phone = line.Phone ?? parish.Phone;
                parish.Email = line.Email ?? parish.Email;
                parish.Website = line.Website ?? parish.Website;
            }

            // Osoba: po imieniu i nazwisku; wśród kilku – ta z funkcją Proboszcz.
            var key = NameKey(line.FirstName, line.LastName);
            var candidates = people.Where(p => NameKey(p.FirstName, p.LastName) == key).ToList();
            Person? person = candidates.Count switch
            {
                0 => null,
                1 => candidates[0],
                _ => candidates.FirstOrDefault(c => pastorFunctions.Any(f => f.PersonId == c.Id)) ?? candidates.OrderBy(c => c.CreatedAtUtc).First()
            };
            if (candidates.Count > 1) warnings.Add($"W bazie jest {candidates.Count} osób o tym imieniu i nazwisku – wybrano jedną, sprawdź.");
            var isNewPerson = false;
            if (person is null && !newPeople.TryGetValue(key, out person))
            {
                isNewPerson = true;
                person = new Person
                {
                    Id = Guid.NewGuid(), FirstName = line.FirstName, LastName = line.LastName,
                    Notes = Describe(line), CreatedAtUtc = _time.GetUtcNow().UtcDateTime, UpdatedAtUtc = _time.GetUtcNow().UtcDateTime
                };
                newPeople[key] = person;
                if (apply) _db.People.Add(person);
            }
            else if (!newPeople.ContainsKey(key)) existingPeople.Add(person.Id);
            if (string.IsNullOrWhiteSpace(person.Phone) && line.PastorPhone is not null && apply) person.Phone = line.PastorPhone;
            if (string.IsNullOrWhiteSpace(person.Email) && line.PastorEmail is { } mail)
            {
                if (!emailsInUse.Add(mail.ToLowerInvariant())) warnings.Add($"Adres {mail} ma już inna osoba – nie przypisano.");
                else if (apply) person.Email = mail;
            }

            // Proboszcz parafii.
            var current = pastorFunctions.Where(f => f.ParishId == parish.Id).ToList();
            string change;
            string? previous = null;
            if (current.Any(f => f.PersonId == person.Id))
            {
                change = "unchanged";
                unchanged++;
            }
            else
            {
                if (current.Count > 0)
                {
                    var old = people.FirstOrDefault(p => p.Id == current[0].PersonId);
                    previous = old is null ? "inna osoba" : $"{old.FirstName} {old.LastName}";
                    change = "replaced";
                    replaced++;
                }
                else
                {
                    change = "set";
                    set++;
                }
                foreach (var f in current) f.ParishId = null;
                var free = pastorFunctions.FirstOrDefault(f => f.PersonId == person.Id && f.ParishId == null);
                if (free is not null)
                {
                    free.ParishId = parish.Id;
                }
                else
                {
                    free = new PersonFunction { Id = Guid.NewGuid(), PersonId = person.Id, Type = FunctionType.Pastor, ParishId = parish.Id };
                    pastorFunctions.Add(free);
                    if (apply) _db.PersonFunctions.Add(free);
                }
                if (line.Since is { } year && free.Notes is null) free.Notes = $"{(line.Function == "administrator" ? "administrator" : "proboszcz")} od {year}";
                else if (line.Function == "administrator" && free.Notes is null) free.Notes = "administrator parafii";
            }

            rows.Add(new ParishImportRowDto
            {
                Row = line.Row, Deanery = line.Deanery, City = line.City, ParishName = parish.Name, NewParish = isNewParish,
                PastorFullName = $"{person.FirstName} {person.LastName}", NewPerson = isNewPerson,
                PastorChange = change, PreviousPastor = previous, Warnings = warnings
            });
        }

        if (apply) await _db.SaveChangesAsync(ct);
        else _db.ChangeTracker.Clear();

        return new ParishImportResultDto
        {
            Applied = apply, NewParishes = newParishes, MatchedParishes = matched, NewPeople = newPeople.Count,
            ExistingPeople = existingPeople.Count, PastorsSet = set, PastorsUnchanged = unchanged, PastorsReplaced = replaced, Rows = rows
        };
    }

    private static string Describe(Line line)
    {
        var parts = new List<string> { "ks." + (line.Titles is null ? "" : " " + line.Titles) };
        if (line.Order is not null) parts.Add($"zgromadzenie {line.Order}");
        return string.Join(", ", parts) + ". Dane z importu spisu parafii.";
    }

    private static List<Line> Read(Stream xlsx)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(xlsx);
        }
        catch (Exception)
        {
            throw new InvalidOperationException("To nie jest plik Excel (.xlsx).");
        }
        using (workbook)
        {
            var sheet = workbook.Worksheets.First();
            var header = sheet.Row(1).CellsUsed().ToDictionary(c => Norm(c.GetString()), c => c.Address.ColumnNumber);
            int? Col(params string[] names) => names.Select(Norm).Where(header.ContainsKey).Select(n => (int?)header[n]).FirstOrDefault();
            var city = Col("Miejscowość");
            var parish = Col("Parafia");
            var first = Col("Imię");
            var last = Col("Nazwisko");
            if (city is null || parish is null || first is null || last is null)
                throw new InvalidOperationException("W pierwszym wierszu arkusza muszą być kolumny: Miejscowość, Parafia, Imię, Nazwisko.");
            var deanery = Col("Dekanat");
            var function = Col("Funkcja");
            var titles = Col("Tytuły");
            var order = Col("Zgromadzenie");
            var since = Col("Proboszcz od", "Od roku");
            var pastorPhone = Col("Tel. proboszcza");
            var pastorEmail = Col("E-mail proboszcza");
            var address = Col("Adres parafii");
            var phone = Col("Tel. parafii");
            var email = Col("E-mail parafii", "E-mail");
            var www = Col("WWW");

            var lines = new List<Line>();
            foreach (var row in sheet.RowsUsed().Skip(1))
            {
                string? Get(int? col) => col is { } c && row.Cell(c).GetString().Trim() is { Length: > 0 } v ? Regex.Replace(v, @"\s+", " ") : null;
                var c1 = Get(city);
                var p1 = Get(parish);
                var f1 = Get(first);
                var l1 = Get(last);
                if (c1 is null || p1 is null || f1 is null || l1 is null) continue;
                var dek = Get(deanery);
                lines.Add(new Line(row.RowNumber(), dek is null ? null : Regex.Replace(dek, "^Dekanat ", ""), c1, p1, f1, l1,
                    Get(function)?.ToLowerInvariant() is { } fn && fn.StartsWith("admin") ? "administrator" : "proboszcz", Get(titles), Get(order),
                    int.TryParse(Get(since), out var year) ? year : null,
                    Get(pastorPhone), Get(pastorEmail), Get(address), Get(phone), Get(email), Get(www)));
            }
            if (lines.Count == 0) throw new InvalidOperationException("Arkusz nie zawiera żadnej parafii z proboszczem.");
            return lines;
        }
    }

    /// <summary>„Parafia Rzymskokatolicka świętego Józefa” → „św. Józefa”.</summary>
    public static string ShortName(string name)
    {
        var s = Regex.Replace(name, @"^\s*Parafia\s+(Rzymskokatolicka\s+)?(pw\.\s*|pod wezwaniem\s+)?", "", RegexOptions.IgnoreCase);
        // „Świętego Krzyża”, „Świętej Rodziny”, „Świętej Trójcy”, „Ducha Świętego” to nazwy – zostają w całości.
        s = Regex.Replace(s, @"\b[Śś]więt(ego|ej|ych)\b\.?(?!\s+(Krzyża|Rodziny|Trójcy|Ducha)\b)", "św.");
        s = Regex.Replace(s, @"\b[Bb]łogosławionego\b", "bł.");
        s = s.Replace("Najświętszej Maryi Panny", "NMP");
        s = Regex.Replace(s, @"\s+", " ").Trim();
        return s.Length == 0 ? name.Trim() : s;
    }

    private static Parish? MatchParish(IEnumerable<Parish> parishes, Line line, out bool ambiguous)
    {
        ambiguous = false;
        var city = Norm(line.City);
        var wanted = Tokens(line.Parish);
        var scored = parishes
            .Select(p => (Parish: p, City: p.City is null ? null : Norm(p.City), Tokens: Tokens(p.Name)))
            .Where(p => p.City == city || (p.City is null && Norm(p.Parish.Name).Contains(city)))
            .Select(p => (p.Parish, Score: Jaccard(wanted, p.Tokens.Except(Tokens(line.City)).ToHashSet())))
            .Where(p => p.Score >= 0.5)
            .OrderByDescending(p => p.Score)
            .ToList();
        if (scored.Count > 1 && scored[0].Score - scored[1].Score < 0.1) ambiguous = true;
        return scored.FirstOrDefault().Parish;
    }

    private static double Jaccard(HashSet<string> a, HashSet<string> b) =>
        a.Count + b.Count == 0 ? 0 : (double)a.Intersect(b).Count() / a.Union(b).Count();

    private static readonly HashSet<string> Stop = new() { "parafia", "rzymskokatolicka", "pw", "pod", "wezwaniem", "i", "z", "a", "w", "kosciol", "garnizonowy" };
    private static readonly HashSet<string> Saint = new() { "sw", "swietego", "swietej", "swietych", "swieta", "swiety" };

    private static HashSet<string> Tokens(string text)
    {
        var s = Norm(text).Replace("nmp", " najswietszej maryi panny ").Replace("bl ", " blogoslawionego ");
        var result = new HashSet<string>();
        foreach (var w in Regex.Matches(s, "[a-z]+").Select(m => m.Value))
        {
            if (Stop.Contains(w)) continue;
            result.Add(Saint.Contains(w) ? "sw" : w.Length > 5 ? w[..5] : w);
        }
        return result;
    }

    private static string NameKey(string first, string last) => Norm(first) + "|" + Norm(last);

    private static string Norm(string text)
    {
        var d = text.Trim().ToLowerInvariant().Replace('ł', 'l').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in d)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return Regex.Replace(sb.ToString(), @"[^a-z0-9]+", " ").Trim();
    }
}
