using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DokPortal.Application.FormSubmissions;

public sealed record TallyEvent(string EventType, string SubmissionId, string? FormId, string? FormName, DateTime SubmittedAtUtc, IReadOnlyList<FormSubmissionField> Fields);

/// <summary>
/// Odczyt webhooka Tally i „sprzątanie” odpowiedzi do danych osoby. Ludzie wpisują dane różnie
/// („86 005”, „85-149 Bydgoszcz”, „ul. Dabrowa”, „16.09”, „4 Maj”), więc parser poprawia to, co da się
/// poprawić pewnie – resztę osoba przeglądająca zgłoszenie poprawia ręcznie przed zapisaniem.
/// </summary>
public static partial class FormSubmissionParser
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static TallyEvent? ParseTally(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return null;
        var eventType = Str(root, "eventType") ?? "";
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object) return null;
        var submissionId = Str(data, "submissionId") ?? Str(data, "responseId");
        if (string.IsNullOrWhiteSpace(submissionId)) return null;
        var created = Str(data, "createdAt") ?? Str(root, "createdAt");
        var submittedAt = DateTime.TryParse(created, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var at)
            ? at : DateTime.UtcNow;

        var fields = new List<FormSubmissionField>();
        if (data.TryGetProperty("fields", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in list.EnumerateArray())
            {
                var type = Str(f, "type");
                // Pola ukryte i wyliczane Tally nie są odpowiedziami osoby.
                if (type is "HIDDEN_FIELDS" or "CALCULATED_FIELDS") continue;
                var label = Str(f, "label") ?? Str(f, "key") ?? "";
                fields.Add(ReadField(label.Trim(), type, f));
            }
        }
        return new TallyEvent(eventType, submissionId.Trim(), Str(data, "formId"), Str(data, "formName")?.Trim(), submittedAt, fields);
    }

    private static FormSubmissionField ReadField(string label, string? type, JsonElement f)
    {
        if (!f.TryGetProperty("value", out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return new FormSubmissionField(label, type, null);
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                return new FormSubmissionField(label, type, Blank(value.GetString()));
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                return new FormSubmissionField(label, type, value.ValueKind switch
                {
                    JsonValueKind.True => "tak",
                    JsonValueKind.False => "nie",
                    _ => value.GetRawText()
                });
            case JsonValueKind.Array:
                var files = new List<FormSubmissionFile>();
                var texts = new List<string>();
                var options = new Dictionary<string, string>();
                if (f.TryGetProperty("options", out var opts) && opts.ValueKind == JsonValueKind.Array)
                    foreach (var o in opts.EnumerateArray())
                        if (Str(o, "id") is { } id && Str(o, "text") is { } text) options[id] = text;
                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object && Str(item, "url") is { } url)
                        files.Add(new FormSubmissionFile(Str(item, "name") ?? "plik", url, Str(item, "mimeType"),
                            item.TryGetProperty("size", out var s) && s.TryGetInt64(out var size) ? size : null));
                    else if (item.ValueKind == JsonValueKind.String)
                        texts.Add(options.TryGetValue(item.GetString()!, out var t) ? t : item.GetString()!);
                    else texts.Add(item.GetRawText());
                }
                return new FormSubmissionField(label, type, texts.Count > 0 ? string.Join(", ", texts) : null, files.Count > 0 ? files : null);
            default:
                return new FormSubmissionField(label, type, value.GetRawText());
        }
    }

    public static IReadOnlyList<FormSubmissionFile> AllFiles(IEnumerable<FormSubmissionField> fields) =>
        fields.SelectMany(f => f.Files ?? Array.Empty<FormSubmissionFile>()).ToList();

    public static PersonDraft Draft(IReadOnlyList<FormSubmissionField> fields)
    {
        string? first = null, last = null, email = null, phone = null, street = null, postal = null, city = null, nameDay = null;
        foreach (var field in fields)
        {
            var label = field.Label.ToLowerInvariant();
            var text = field.Text;
            if (text is null) continue;
            if (label.Contains("imienin")) nameDay ??= text;
            else if (label.Contains("nazwisk") && (label.Contains("imi") || label.Contains("dane")))
            {
                // Jedno pole „Imię i nazwisko”.
                var parts = Collapse(text).Split(' ', 2);
                first ??= parts[0];
                if (parts.Length > 1) last ??= parts[1];
            }
            else if (label.Contains("nazwisk")) last ??= text;
            else if (label.StartsWith("imi") || label == "imię") first ??= text;
            else if (label.Contains("mail")) email ??= text;
            else if (label.Contains("telefon") || field.Type == "INPUT_PHONE_NUMBER") phone ??= text;
            else if (label.Contains("kod")) postal ??= text;
            else if (label.Contains("miasto") || label.Contains("miejscowo")) city ??= text;
            else if (label.Contains("ulica") || label.Contains("adres")) street ??= text;
        }

        var (code, postalCity) = SplitPostal(postal);
        var (streetCity, cleanStreet) = SplitStreet(street);
        var (day, month) = ParseNameDay(nameDay);
        var files = AllFiles(fields);
        var photo = files.Select((f, i) => (f, i)).FirstOrDefault(x => IsImage(x.f));
        return new PersonDraft
        {
            FirstName = NormalizeName(first),
            LastName = NormalizeName(last),
            Email = Blank(email)?.Trim().ToLowerInvariant(),
            Phone = NormalizePhone(phone),
            AddressStreet = cleanStreet,
            AddressPostalCode = code,
            AddressCity = NormalizeCity(Blank(city) ?? postalCity ?? streetCity) ?? CityFromPostalCode(code),
            NameDayDay = day,
            NameDayMonth = month,
            PhotoIndex = photo.f is null ? null : photo.i
        };
    }

    /// <summary>Kody 85-xxx to sama Bydgoszcz – formularz nie zawsze pyta o miejscowość. Inne kody obejmują kilka miejscowości.</summary>
    public static string? CityFromPostalCode(string? code) => code is not null && code.StartsWith("85-", StringComparison.Ordinal) ? "Bydgoszcz" : null;

    public static bool IsImage(FormSubmissionFile file) =>
        file.MimeType is "image/jpeg" or "image/png" or "image/webp"
        || (file.MimeType is null && Regex.IsMatch(file.Name, @"\.(jpe?g|png|webp)$", RegexOptions.IgnoreCase));

    /// <summary>Spacje, myślniki w nazwiskach dwuczłonowych, WIELKIE/małe litery → „Kowalska-Nowak”.</summary>
    public static string? NormalizeName(string? value)
    {
        var text = Blank(value);
        if (text is null) return null;
        text = Collapse(HyphenSpaces().Replace(text, "-"));
        var letters = text.Where(char.IsLetter).ToArray();
        var allUpper = letters.Length > 1 && letters.All(char.IsUpper);
        var allLower = letters.Length > 0 && letters.All(char.IsLower);
        return allUpper || allLower ? TitleCase(text) : text;
    }

    public static string? NormalizePhone(string? value)
    {
        var text = Blank(value);
        if (text is null) return null;
        var digits = new string(text.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("0048")) digits = digits[4..];
        else if (digits.Length == 11 && digits.StartsWith("48")) digits = digits[2..];
        return digits.Length == 9 ? $"{digits[..3]} {digits[3..6]} {digits[6..]}" : Collapse(text);
    }

    /// <summary>„85-149 Bydgoszcz”, „86 005”, „89300”, „85 ' 790” → (kod NN-NNN, miejscowość z reszty tekstu).</summary>
    public static (string? Code, string? City) SplitPostal(string? value)
    {
        var text = Blank(value);
        if (text is null) return (null, null);
        var match = PostalCode().Match(text);
        if (!match.Success) return (Collapse(text), null);
        var rest = Blank((text[..match.Index] + " " + text[(match.Index + match.Length)..]).Trim(' ', ',', '.', '-'));
        return ($"{match.Groups[1].Value}-{match.Groups[2].Value}", rest is null ? null : Collapse(rest));
    }

    /// <summary>
    /// Ulica z numerem: bez „ul.”, „m. 31” → „/31”, „14, 5” → „14/5”, „Chłodna, 16/6” → „Chłodna 16/6”,
    /// „Wyrzysk, ul. Wiejska 9” → (Wyrzysk, Wiejska 9). WIELKIE LITERY → zwykłe.
    /// </summary>
    public static (string? City, string? Street) SplitStreet(string? value)
    {
        var text = Blank(value);
        if (text is null) return (null, null);
        text = Collapse(text);
        string? city = null;
        var comma = text.IndexOf(',');
        if (comma > 0 && comma < text.Length - 1)
        {
            var before = text[..comma].Trim();
            var after = text[(comma + 1)..].Trim();
            if (!before.Any(char.IsDigit) && after.Length > 0 && !char.IsDigit(after[0]) && after.Any(char.IsDigit))
            {
                city = before;
                text = after;
            }
        }
        text = StreetPrefix().Replace(text, "");
        text = FlatMarker().Replace(text, "/$1");
        text = NumberComma().Replace(text, "$1/$2");
        text = NameComma().Replace(text, "$1 $2");
        text = SlashSpaces().Replace(text, "/");
        var letters = text.Where(char.IsLetter).ToArray();
        if (letters.Length > 3 && letters.All(char.IsUpper)) text = TitleCase(text);
        return (city is null ? null : NormalizeCity(city), Collapse(text));
    }

    private static string? NormalizeCity(string? value)
    {
        var text = Blank(value);
        if (text is null) return null;
        text = Collapse(text);
        var letters = text.Where(char.IsLetter).ToArray();
        return letters.Length > 0 && (letters.All(char.IsUpper) || letters.All(char.IsLower)) ? TitleCase(text) : text;
    }

    private static readonly (string Stem, int Month)[] MonthStems =
    {
        ("sty", 1), ("lut", 2), ("mar", 3), ("kwi", 4), ("maj", 5), ("cze", 6),
        ("lip", 7), ("sie", 8), ("wrz", 9), ("paź", 10), ("paz", 10), ("lis", 11), ("gru", 12)
    };

    /// <summary>„24 maja”, „4 Maj”, „27 lipiec”, „23.10.”, „16.09”, „02.02” → (dzień, miesiąc); „nie obchodzę” → brak.</summary>
    public static (int? Day, int? Month) ParseNameDay(string? value)
    {
        var text = Blank(value)?.Trim().ToLowerInvariant();
        if (text is null) return (null, null);
        int day, month;
        var numeric = NumericDate().Match(text);
        if (numeric.Success)
        {
            day = int.Parse(numeric.Groups[1].Value, CultureInfo.InvariantCulture);
            month = int.Parse(numeric.Groups[2].Value, CultureInfo.InvariantCulture);
        }
        else
        {
            var words = WordDate().Match(text);
            if (!words.Success) return (null, null);
            day = int.Parse(words.Groups[1].Value, CultureInfo.InvariantCulture);
            var stem = MonthStems.FirstOrDefault(s => words.Groups[2].Value.StartsWith(s.Stem, StringComparison.Ordinal));
            if (stem.Month == 0) return (null, null);
            month = stem.Month;
        }
        if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(2024, month)) return (null, null);
        return (day, month);
    }

    private static string TitleCase(string text)
    {
        var sb = new StringBuilder(text.Length);
        var startOfWord = true;
        foreach (var c in text)
        {
            sb.Append(startOfWord ? char.ToUpper(c, Pl) : char.ToLower(c, Pl));
            startOfWord = !char.IsLetter(c);
        }
        return sb.ToString();
    }

    private static readonly CultureInfo Pl = CultureInfo.GetCultureInfo("pl-PL");
    private static string Collapse(string text) => Spaces().Replace(text.Trim(), " ");
    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;
    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    [GeneratedRegex(@"\s+")] private static partial Regex Spaces();
    [GeneratedRegex(@"\s*-\s*")] private static partial Regex HyphenSpaces();
    [GeneratedRegex(@"(?<!\d)(\d{2})[\s\-–'’.]*(\d{3})(?!\d)")] private static partial Regex PostalCode();
    [GeneratedRegex(@"^ul\.?\s+", RegexOptions.IgnoreCase)] private static partial Regex StreetPrefix();
    [GeneratedRegex(@"\s+m\.?\s*(\d+)\s*$", RegexOptions.IgnoreCase)] private static partial Regex FlatMarker();
    [GeneratedRegex(@"(\d+[A-Za-z]?)\s*,\s*(\d+)\s*$")] private static partial Regex NumberComma();
    [GeneratedRegex(@"^([^\d,]+?)\s*,\s*(\d.*)$")] private static partial Regex NameComma();
    [GeneratedRegex(@"\s*/\s*")] private static partial Regex SlashSpaces();
    [GeneratedRegex(@"^(\d{1,2})\s*[.\-/]\s*(\d{1,2})\.?$")] private static partial Regex NumericDate();
    [GeneratedRegex(@"^(\d{1,2})\.?\s*([a-ząćęłńóśźż]+)")] private static partial Regex WordDate();
}
