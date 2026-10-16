using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using DokPortal.Domain.Enums;
using DokPortal.Domain.Formation;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Documents;

/// <summary>Pole wstawiane do wzoru: {{klucz}}; pola z imieniem i nazwiskiem można odmienić: {{klucz|dopełniacz}}.</summary>
public sealed record LetterField(string Key, string Label, string Group, string Example, bool Declinable = false);

/// <summary>Imię i nazwisko do odmiany; <see cref="Part"/> mówi, którą część wstawić.</summary>
public sealed record LetterName(string FirstName, string LastName, PersonSex Sex, LetterNamePart Part = LetterNamePart.Full);

public enum LetterNamePart { Full, First, Last }

/// <summary>Wartości pól pisma: teksty, imiona do odmiany i płeć osoby (do {{rodzaj:…|…}}).</summary>
public sealed class LetterValues
{
    public Dictionary<string, string?> Text { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, LetterName> Names { get; } = new(StringComparer.OrdinalIgnoreCase);
    public PersonSex Sex { get; set; } = PersonSex.Male;

    public string? this[string key]
    {
        get => Text.TryGetValue(key, out var value) ? value : null;
        set => Text[key] = value;
    }

    /// <summary>Osoba (imię, nazwisko, imię i nazwisko) – wszystkie trzy pola z odmianą.</summary>
    public void AddPerson(string keyPrefix, string first, string last, PersonSex sex)
    {
        Names[$"{keyPrefix}.imię"] = new LetterName(first, last, sex, LetterNamePart.First);
        Names[$"{keyPrefix}.nazwisko"] = new LetterName(first, last, sex, LetterNamePart.Last);
        Names[$"{keyPrefix}.imię i nazwisko"] = new LetterName(first, last, sex);
    }

    public void AddName(string key, string first, string last, PersonSex? sex = null) =>
        Names[key] = new LetterName(first, last, sex ?? PolishNames.InferSex(first));

    public string? Declined(string key, GrammaticalCase grammaticalCase)
    {
        if (!Names.TryGetValue(key, out var name)) return this[key];
        return name.Part switch
        {
            LetterNamePart.First => PolishNames.FirstName(name.FirstName, name.Sex, grammaticalCase),
            LetterNamePart.Last => PolishNames.LastName(name.LastName, name.Sex, grammaticalCase),
            _ => PolishNames.FullName(name.FirstName, name.LastName, name.Sex, grammaticalCase)
        };
    }
}

/// <summary>Pola wzorów pism: katalog, odczyt wartości dla osoby i wypełnianie treści.</summary>
public static class LetterFields
{
    /// <summary>Prefiks pola wpisywanego ręcznie przy generowaniu: {{wpisz:Data chrztu}}.</summary>
    public const string InputPrefix = "wpisz:";
    /// <summary>Forma zależna od płci osoby: {{rodzaj:urodzony|urodzona}} (męska|żeńska).</summary>
    public const string GenderPrefix = "rodzaj:";
    /// <summary>Tekst w miejscu pola bez danych – do uzupełnienia odręcznie.</summary>
    public const string Blank = "……………………";

    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");
    private static readonly Regex Token = new(@"\{\{\s*([^{}]+?)\s*\}\}", RegexOptions.Compiled);

    public static IReadOnlyList<LetterField> Catalog { get; } = new LetterField[]
    {
        new("osoba.imię", "Imię", "Osoba", "Jan", true),
        new("osoba.nazwisko", "Nazwisko", "Osoba", "Kowalski", true),
        new("osoba.imię i nazwisko", "Imię i nazwisko", "Osoba", "Jan Kowalski", true),
        new("osoba.data urodzenia", "Data urodzenia", "Osoba", "12.05.1990"),
        new("osoba.adres", "Adres (ulica, kod, miejscowość)", "Osoba", "ul. Długa 5, 85-001 Bydgoszcz"),
        new("osoba.ulica", "Ulica i numer", "Osoba", "ul. Długa 5"),
        new("osoba.kod pocztowy", "Kod pocztowy", "Osoba", "85-001"),
        new("osoba.miejscowość", "Miejscowość zamieszkania", "Osoba", "Bydgoszcz"),
        new("osoba.e-mail", "E-mail", "Osoba", "jan@example.org"),
        new("osoba.telefon", "Telefon", "Osoba", "600 100 200"),
        new("osoba.parafia", "Parafia (z miejscowością)", "Osoba", "św. Józefa w Bydgoszczy"),
        new("proboszcz.imię i nazwisko", "Proboszcz parafii osoby", "Parafia", "Adam Nowak", true),
        new("parafia.nazwa", "Nazwa parafii", "Parafia", "św. Józefa"),
        new("parafia.miejscowość", "Miejscowość parafii", "Parafia", "Bydgoszcz"),
        new("parafia.adres", "Adres parafii", "Parafia", "ul. Kościelna 1, 85-000 Bydgoszcz"),
        new("dok.ścieżka", "Ścieżka DOK", "Sprawa DOK", "Bierzmowanie"),
        new("dok.etap", "Etap DOK", "Sprawa DOK", "Katechumenat"),
        new("dok.katechista", "Katechista prowadzący", "Sprawa DOK", "Anna Maj", true),
        new("dok.mentor", "Mentor", "Sprawa DOK", "Marek Lis", true),
        new("dok.data rozpoczęcia", "Data rozpoczęcia sprawy", "Sprawa DOK", "01.09.2025"),
        new("sksp.rok formacji", "Rok formacji SKŚP", "SKŚP", "III"),
        new("sksp.data ukończenia", "Data ukończenia SKŚP", "SKŚP", "20.06.2026"),
        new("misja.miejsce posługi", "Miejsce posługi (misja)", "Misja kanoniczna", "Parafia św. Józefa"),
        new("misja.od", "Misja od", "Misja kanoniczna", "01.09.2026"),
        new("misja.do", "Misja do", "Misja kanoniczna", "31.08.2029"),
        new("pismo.data", "Data wystawienia (cyfrowo)", "Pismo", "16.10.2026"),
        new("pismo.data słownie", "Data wystawienia (słownie)", "Pismo", "16 października 2026"),
        new("pismo.miejscowość", "Miejscowość wystawienia", "Pismo", "Bydgoszcz"),
        new("pismo.wystawił", "Kto wystawia", "Pismo", "Szymon Łuczak", true),
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
            .Where(k => !IsValid(k))
            .Distinct().ToList();

    private static readonly HashSet<string> DeclinableKeys = Catalog.Where(f => f.Declinable).Select(f => f.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static bool IsValid(string token)
    {
        if (token.StartsWith(InputPrefix, StringComparison.OrdinalIgnoreCase)) return true;
        if (token.StartsWith(GenderPrefix, StringComparison.OrdinalIgnoreCase)) return token.Contains('|');
        var (key, caseName) = SplitCase(token);
        if (!Known.Contains(key)) return false;
        return caseName is null || (DeclinableKeys.Contains(key) && PolishNames.CaseNames.ContainsKey(caseName));
    }

    private static (string Key, string? Case) SplitCase(string token)
    {
        var bar = token.IndexOf('|');
        return bar < 0 ? (token.Trim(), null) : (token[..bar].Trim(), token[(bar + 1)..].Trim());
    }

    /// <summary>Tylko znaki specjalne HTML – polskie litery zostają czytelne w zapisanej treści.</summary>
    private static string Encode(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static string StripTags(string html) => Regex.Replace(html, "<[^>]+>", "");

    /// <summary>Przykładowe wartości – do podglądu wzoru bez wybranej osoby.</summary>
    public static LetterValues Examples(string place, string issuedBy, DateTime now)
    {
        var values = new LetterValues();
        foreach (var f in Catalog) values[f.Key] = f.Example;
        values["pismo.data"] = now.ToString("dd.MM.yyyy");
        values["pismo.data słownie"] = Words(now);
        values["pismo.miejscowość"] = place;
        values["pismo.wystawił"] = issuedBy;
        values.AddPerson("osoba", "Jan", "Kowalski", PersonSex.Male);
        values.AddName("proboszcz.imię i nazwisko", "Adam", "Nowak", PersonSex.Male);
        values.AddName("dok.katechista", "Anna", "Maj");
        values.AddName("dok.mentor", "Marek", "Lis");
        AddIssuer(values, issuedBy);
        return values;
    }

    /// <summary>Wystawiający: „Imię Nazwisko” da się odmienić, adres e-mail – nie.</summary>
    private static void AddIssuer(LetterValues values, string issuedBy)
    {
        var parts = issuedBy.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && !issuedBy.Contains('@')) values.AddName("pismo.wystawił", parts[0], parts[1]);
    }

    public static string Words(DateTime date) => date.ToString("d MMMM yyyy", Polish);

    private static string Date(DateOnly? d) => d?.ToString("dd.MM.yyyy") ?? "";
    private static string Date(DateTime? d) => d?.ToString("dd.MM.yyyy") ?? "";

    /// <summary>Wartości pól dla osoby (puste, gdy brak danych).</summary>
    public static async Task<LetterValues> ResolveAsync(AppDbContext db, Guid personId, string place, string issuedBy, DateTime now, CancellationToken ct)
    {
        var person = await db.People.AsNoTracking().Include(p => p.Parish).FirstOrDefaultAsync(p => p.Id == personId, ct)
            ?? throw new KeyNotFoundException();
        var sex = person.Sex ?? PolishNames.InferSex(person.FirstName);
        var values = new LetterValues { Sex = sex };
        values.AddPerson("osoba", person.FirstName, person.LastName, sex);
        AddIssuer(values, issuedBy);
        foreach (var (key, value) in new Dictionary<string, string?>
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
        }) values[key] = value;

        if (person.ParishId is { } parishId)
        {
            var pastor = await db.PersonFunctions.AsNoTracking().Where(f => f.Type == FunctionType.Pastor && f.ParishId == parishId)
                .Select(f => new { f.Person!.FirstName, f.Person.LastName }).FirstOrDefaultAsync(ct);
            if (pastor is not null)
            {
                values["proboszcz.imię i nazwisko"] = $"{pastor.FirstName} {pastor.LastName}";
                values.AddName("proboszcz.imię i nazwisko", pastor.FirstName, pastor.LastName, PersonSex.Male);
            }
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
            if (dokCase.CatechistPerson is { } catechist) values.AddName("dok.katechista", catechist.FirstName, catechist.LastName, catechist.Sex);
            if (dokCase.MentorPerson is { } mentor) values.AddName("dok.mentor", mentor.FirstName, mentor.LastName, mentor.Sex);
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
    public static string Fill(string body, LetterValues values, IReadOnlyDictionary<string, string>? inputs, List<string> missing)
    {
        var inputValues = new Dictionary<string, string>(inputs ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
        return Token.Replace(body, m =>
        {
            var token = WebUtility.HtmlDecode(m.Groups[1].Value).Trim();
            string? value;
            string label;
            if (token.StartsWith(InputPrefix, StringComparison.OrdinalIgnoreCase))
            {
                label = token[InputPrefix.Length..].Trim();
                value = inputValues.TryGetValue(label, out var typed) ? typed : null;
            }
            else if (token.StartsWith(GenderPrefix, StringComparison.OrdinalIgnoreCase))
            {
                // {{rodzaj:urodzony|urodzona}} – forma męska|żeńska wg płci osoby
                var forms = token[GenderPrefix.Length..].Split('|');
                return Encode((values.Sex == PersonSex.Female && forms.Length > 1 ? forms[1] : forms[0]).Trim());
            }
            else
            {
                var (key, caseName) = SplitCase(token);
                label = Catalog.FirstOrDefault(f => f.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.Label ?? key;
                value = caseName is not null && PolishNames.CaseNames.TryGetValue(caseName, out var grammaticalCase)
                    ? values.Declined(key, grammaticalCase)
                    : values[key];
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
