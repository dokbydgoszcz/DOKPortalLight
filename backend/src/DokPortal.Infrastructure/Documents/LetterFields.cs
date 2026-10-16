using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Domain.Formation;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Documents;

/// <summary>Pole wstawiane do wzoru: {{klucz}}.</summary>
public sealed record LetterField(string Key, string Label, string Group, string Example);

/// <summary>Pola wzorów pism: katalog, odczyt wartości dla osoby i wypełnianie treści.</summary>
public static class LetterFields
{
    /// <summary>Prefiks pola wpisywanego ręcznie przy generowaniu: {{wpisz:Data chrztu}}.</summary>
    public const string InputPrefix = "wpisz:";
    /// <summary>Tekst w miejscu pola bez danych – do uzupełnienia odręcznie.</summary>
    public const string Blank = "……………………";

    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");
    private static readonly Regex Token = new(@"\{\{\s*([^{}]+?)\s*\}\}", RegexOptions.Compiled);

    public static IReadOnlyList<LetterField> Catalog { get; } = new LetterField[]
    {
        new("osoba.imię", "Imię", "Osoba", "Jan"),
        new("osoba.nazwisko", "Nazwisko", "Osoba", "Kowalski"),
        new("osoba.imię i nazwisko", "Imię i nazwisko", "Osoba", "Jan Kowalski"),
        new("osoba.data urodzenia", "Data urodzenia", "Osoba", "12.05.1990"),
        new("osoba.adres", "Adres (ulica, kod, miejscowość)", "Osoba", "ul. Długa 5, 85-001 Bydgoszcz"),
        new("osoba.ulica", "Ulica i numer", "Osoba", "ul. Długa 5"),
        new("osoba.kod pocztowy", "Kod pocztowy", "Osoba", "85-001"),
        new("osoba.miejscowość", "Miejscowość zamieszkania", "Osoba", "Bydgoszcz"),
        new("osoba.e-mail", "E-mail", "Osoba", "jan@example.org"),
        new("osoba.telefon", "Telefon", "Osoba", "600 100 200"),
        new("osoba.parafia", "Parafia (z miejscowością)", "Osoba", "św. Józefa w Bydgoszczy"),
        new("proboszcz.imię i nazwisko", "Proboszcz parafii osoby", "Parafia", "Adam Nowak"),
        new("parafia.nazwa", "Nazwa parafii", "Parafia", "św. Józefa"),
        new("parafia.miejscowość", "Miejscowość parafii", "Parafia", "Bydgoszcz"),
        new("parafia.adres", "Adres parafii", "Parafia", "ul. Kościelna 1, 85-000 Bydgoszcz"),
        new("dok.ścieżka", "Ścieżka DOK", "Sprawa DOK", "Bierzmowanie"),
        new("dok.etap", "Etap DOK", "Sprawa DOK", "Katechumenat"),
        new("dok.katechista", "Katechista prowadzący", "Sprawa DOK", "Anna Maj"),
        new("dok.mentor", "Mentor", "Sprawa DOK", "Marek Lis"),
        new("dok.data rozpoczęcia", "Data rozpoczęcia sprawy", "Sprawa DOK", "01.09.2025"),
        new("sksp.rok formacji", "Rok formacji SKŚP", "SKŚP", "III"),
        new("sksp.data ukończenia", "Data ukończenia SKŚP", "SKŚP", "20.06.2026"),
        new("misja.miejsce posługi", "Miejsce posługi (misja)", "Misja kanoniczna", "Parafia św. Józefa"),
        new("misja.od", "Misja od", "Misja kanoniczna", "01.09.2026"),
        new("misja.do", "Misja do", "Misja kanoniczna", "31.08.2029"),
        new("pismo.data", "Data wystawienia (cyfrowo)", "Pismo", "16.10.2026"),
        new("pismo.data słownie", "Data wystawienia (słownie)", "Pismo", "16 października 2026"),
        new("pismo.miejscowość", "Miejscowość wystawienia", "Pismo", "Bydgoszcz"),
        new("pismo.wystawił", "Kto wystawia", "Pismo", "Szymon Łuczak"),
        new("pismo.uwagi", "Uwagi wpisane przy generowaniu", "Pismo", "Uwagi do pisma"),
    };

    private static readonly HashSet<string> Known = Catalog.Select(f => f.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Pola {{wpisz:…}} we wzorze, w kolejności pierwszego wystąpienia.</summary>
    public static IReadOnlyList<string> Inputs(string body) =>
        Token.Matches(WebUtility.HtmlDecode(StripTags(body))).Select(m => m.Groups[1].Value.Trim())
            .Where(k => k.StartsWith(InputPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(k => k[InputPrefix.Length..].Trim()).Where(k => k.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Pola, których portal nie zna (literówki), np. {{osoba.imie}}.</summary>
    public static IReadOnlyList<string> Unknown(string body) =>
        Token.Matches(WebUtility.HtmlDecode(StripTags(body))).Select(m => m.Groups[1].Value.Trim())
            .Where(k => !k.StartsWith(InputPrefix, StringComparison.OrdinalIgnoreCase) && !Known.Contains(k))
            .Distinct().ToList();

    /// <summary>Tylko znaki specjalne HTML – polskie litery zostają czytelne w zapisanej treści.</summary>
    private static string Encode(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static string StripTags(string html) => Regex.Replace(html, "<[^>]+>", "");

    /// <summary>Przykładowe wartości – do podglądu wzoru bez wybranej osoby.</summary>
    public static Dictionary<string, string?> Examples(string place, string issuedBy, DateTime now)
    {
        var values = Catalog.ToDictionary(f => f.Key, f => (string?)f.Example, StringComparer.OrdinalIgnoreCase);
        values["pismo.data"] = now.ToString("dd.MM.yyyy");
        values["pismo.data słownie"] = Words(now);
        values["pismo.miejscowość"] = place;
        values["pismo.wystawił"] = issuedBy;
        return values;
    }

    public static string Words(DateTime date) => date.ToString("d MMMM yyyy", Polish);

    private static string Date(DateOnly? d) => d?.ToString("dd.MM.yyyy") ?? "";
    private static string Date(DateTime? d) => d?.ToString("dd.MM.yyyy") ?? "";

    /// <summary>Wartości pól dla osoby (puste, gdy brak danych).</summary>
    public static async Task<Dictionary<string, string?>> ResolveAsync(AppDbContext db, Guid personId, string place, string issuedBy, DateTime now, CancellationToken ct)
    {
        var person = await db.People.AsNoTracking().Include(p => p.Parish).FirstOrDefaultAsync(p => p.Id == personId, ct)
            ?? throw new KeyNotFoundException();
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["osoba.imię"] = person.FirstName,
            ["osoba.nazwisko"] = person.LastName,
            ["osoba.imię i nazwisko"] = person.FullName,
            ["osoba.data urodzenia"] = Date(person.BirthDate),
            ["osoba.ulica"] = person.AddressStreet,
            ["osoba.kod pocztowy"] = person.AddressPostalCode,
            ["osoba.miejscowość"] = person.AddressCity,
            ["osoba.adres"] = Join(", ", person.AddressStreet, Join(" ", person.AddressPostalCode, person.AddressCity)),
            ["osoba.e-mail"] = person.Email,
            ["osoba.telefon"] = person.Phone,
            ["osoba.parafia"] = person.Parish is null ? null : person.Parish.City is { Length: > 0 } c ? $"{person.Parish.Name} ({c})" : person.Parish.Name,
            ["parafia.nazwa"] = person.Parish?.Name,
            ["parafia.miejscowość"] = person.Parish?.City,
            ["parafia.adres"] = person.Parish?.Address,
            ["pismo.data"] = now.ToString("dd.MM.yyyy"),
            ["pismo.data słownie"] = Words(now),
            ["pismo.miejscowość"] = place,
            ["pismo.wystawił"] = issuedBy,
        };

        if (person.ParishId is { } parishId)
        {
            var pastor = await db.PersonFunctions.AsNoTracking().Where(f => f.Type == FunctionType.Pastor && f.ParishId == parishId)
                .Select(f => f.Person!.FirstName + " " + f.Person.LastName).FirstOrDefaultAsync(ct);
            values["proboszcz.imię i nazwisko"] = pastor;
        }

        // Sprawa DOK: najpierw otwarta, potem najnowsza.
        var dokCase = await db.DokCases.AsNoTracking().Include(c => c.CatechistPerson).Include(c => c.MentorPerson)
            .Where(c => c.PersonId == personId)
            .OrderBy(c => c.CompletedAtUtc != null || c.InterruptedOn != null).ThenByDescending(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
        if (dokCase is not null)
        {
            values["dok.ścieżka"] = DokStages.Label(dokCase.Path);
            values["dok.etap"] = DokStages.Label(dokCase.Stage);
            values["dok.katechista"] = dokCase.CatechistPerson?.FullName;
            values["dok.mentor"] = dokCase.MentorPerson?.FullName;
            values["dok.data rozpoczęcia"] = Date(dokCase.CreatedAtUtc);
        }

        var candidate = await db.Candidates.AsNoTracking().Include(c => c.Events).Where(c => c.PersonId == personId)
            .OrderByDescending(c => c.CreatedAtUtc).FirstOrDefaultAsync(ct);
        if (candidate is not null)
        {
            values["sksp.rok formacji"] = candidate.FormationYear switch { 1 => "I", 2 => "II", 3 => "III", var y => y.ToString() };
            if (candidate.IsFormationCompleted)
                values["sksp.data ukończenia"] = Date(candidate.Events.Where(e => e.Kind == CandidateFormationEventKind.Completed).OrderByDescending(e => e.AtUtc).Select(e => (DateTime?)e.AtUtc).FirstOrDefault());
        }

        var mission = await db.CanonicalMissions.AsNoTracking().Where(m => m.PersonId == personId)
            .OrderByDescending(m => m.MissionEndDate).FirstOrDefaultAsync(ct);
        if (mission is not null)
        {
            values["misja.miejsce posługi"] = mission.ServicePlace;
            values["misja.od"] = Date(mission.MissionStartDate);
            values["misja.do"] = Date(mission.MissionEndDate);
        }
        return values;
    }

    private static string? Join(string separator, params string?[] parts)
    {
        var joined = string.Join(separator, parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        return joined.Length == 0 ? null : joined;
    }

    /// <summary>
    /// Wstawia wartości w treść (HTML): pola znane z <paramref name="values"/>, pola {{wpisz:…}} z <paramref name="inputs"/>.
    /// Pole bez wartości dostaje kropki do wypełnienia; jego nazwa trafia do <paramref name="missing"/>.
    /// </summary>
    public static string Fill(string body, IReadOnlyDictionary<string, string?> values, IReadOnlyDictionary<string, string>? inputs, List<string> missing)
    {
        var inputValues = new Dictionary<string, string>(inputs ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
        return Token.Replace(body, m =>
        {
            var key = WebUtility.HtmlDecode(m.Groups[1].Value).Trim();
            string? value;
            string label;
            if (key.StartsWith(InputPrefix, StringComparison.OrdinalIgnoreCase))
            {
                label = key[InputPrefix.Length..].Trim();
                value = inputValues.TryGetValue(label, out var typed) ? typed : null;
            }
            else
            {
                label = Catalog.FirstOrDefault(f => f.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.Label ?? key;
                value = values.TryGetValue(key, out var known) ? known : null;
            }
            if (string.IsNullOrWhiteSpace(value))
            {
                if (!missing.Contains(label)) missing.Add(label);
                return Blank;
            }
            return Encode(value.Trim()).Replace("\n", "<br>");
        });
    }
}
