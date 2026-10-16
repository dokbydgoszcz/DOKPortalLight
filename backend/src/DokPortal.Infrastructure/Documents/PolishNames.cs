using DokPortal.Domain.Enums;

namespace DokPortal.Infrastructure.Documents;

/// <summary>Przypadki gramatyczne do odmiany imion i nazwisk w pismach.</summary>
public enum GrammaticalCase { Nominative, Genitive, Dative, Accusative, Instrumental, Locative, Vocative }

/// <summary>
/// Odmiana polskich imion i nazwisk według typowych reguł (końcówki, oboczności, ruchome „e”).
/// Nazwiska kobiet zakończone spółgłoską i obce zakończenia zostają bez zmian – tak jak w polszczyźnie.
/// </summary>
public static class PolishNames
{
    private static readonly HashSet<string> MaleNamesInA = new(StringComparer.OrdinalIgnoreCase)
        { "Kuba", "Barnaba", "Bonawentura", "Kosma", "Jarema", "Juda", "Zawisza", "Saba", "Dyzma", "Bodzia" };
    private static readonly HashSet<string> FemaleNamesNotInA = new(StringComparer.OrdinalIgnoreCase)
        { "Beatrycze", "Noemi", "Miriam", "Ruth", "Rut", "Nel", "Abigail", "Karmen", "Carmen", "Ingrid", "Doris", "Dagmar", "Lili", "Inez", "Mercedes" };

    /// <summary>Nazwy przypadków używane w polach pism, np. {{osoba.imię i nazwisko|dopełniacz}}.</summary>
    public static readonly IReadOnlyDictionary<string, GrammaticalCase> CaseNames = new Dictionary<string, GrammaticalCase>(StringComparer.OrdinalIgnoreCase)
    {
        ["mianownik"] = GrammaticalCase.Nominative, ["dopełniacz"] = GrammaticalCase.Genitive, ["celownik"] = GrammaticalCase.Dative,
        ["biernik"] = GrammaticalCase.Accusative, ["narzędnik"] = GrammaticalCase.Instrumental, ["miejscownik"] = GrammaticalCase.Locative,
        ["wołacz"] = GrammaticalCase.Vocative,
        ["dopelniacz"] = GrammaticalCase.Genitive, ["narzednik"] = GrammaticalCase.Instrumental, ["wolacz"] = GrammaticalCase.Vocative,
    };

    /// <summary>Płeć z pierwszego imienia: imiona żeńskie kończą się zwykle na „-a” (z wyjątkami).</summary>
    public static PersonSex InferSex(string? firstName)
    {
        var first = (firstName ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        if (first.Length == 0) return PersonSex.Male;
        if (FemaleNamesNotInA.Contains(first)) return PersonSex.Female;
        if (MaleNamesInA.Contains(first)) return PersonSex.Male;
        return first.EndsWith('a') || first.EndsWith('A') ? PersonSex.Female : PersonSex.Male;
    }

    public static string FirstName(string name, PersonSex sex, GrammaticalCase c) =>
        EachWord(name, word => sex == PersonSex.Female
            ? (EndsA(word) ? FeminineNoun(word, c) : word)
            : MaleWord(word, c, isSurname: false));

    public static string LastName(string name, PersonSex sex, GrammaticalCase c) =>
        EachWord(name, word => sex == PersonSex.Female ? FemaleSurname(word, c) : MaleWord(word, c, isSurname: true));

    public static string FullName(string first, string last, PersonSex sex, GrammaticalCase c) =>
        $"{FirstName(first, sex, c)} {LastName(last, sex, c)}".Trim();

    // ---- pomocnicze ----

    private static bool EndsA(string w) => w.EndsWith('a') || w.EndsWith('A');

    /// <summary>Każde słowo osobno (także części nazwiska dwuczłonowego); wielkie litery zostają wielkie.</summary>
    private static string EachWord(string text, Func<string, string> decline)
    {
        return string.Join(" ", text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(word =>
            string.Join("-", word.Split('-').Select(part =>
            {
                if (part.Length < 2) return part;
                var upper = part.Length > 1 && part == part.ToUpperInvariant() && part != part.ToLowerInvariant();
                var normal = upper ? part[0] + part[1..].ToLowerInvariant() : part;
                var result = decline(normal);
                return upper ? result.ToUpperInvariant() : result;
            }))));
    }

    private static string FemaleSurname(string word, GrammaticalCase c)
    {
        if (!EndsA(word)) return word; // Nowak, Holc, King – nazwiska kobiet na spółgłoskę się nie odmieniają
        if (word.EndsWith("ska") || word.EndsWith("cka") || word.EndsWith("dzka") || (word.EndsWith("owa") && word.Length > 4))
        {
            var stem = word[..^1];
            return c switch
            {
                GrammaticalCase.Genitive or GrammaticalCase.Dative or GrammaticalCase.Locative => stem + (stem.EndsWith('k') ? "iej" : "ej"),
                GrammaticalCase.Accusative or GrammaticalCase.Instrumental => stem + "ą",
                _ => word
            };
        }
        return FeminineNoun(word, c, vocativeO: true);
    }

    /// <summary>Męskie imię albo nazwisko.</summary>
    private static string MaleWord(string word, GrammaticalCase c, bool isSurname)
    {
        if (c == GrammaticalCase.Nominative) return word;
        var last = char.ToLowerInvariant(word[^1]);
        if (last == 'a') return FeminineNoun(word, c, vocativeO: true);
        if (last == 'o') return isSurname ? FeminineNoun(word[..^1] + "a", c, vocativeO: true) : word;
        if (last is 'y' or 'i') return AdjectivalMale(word, c);
        if ("eéuóąęü".Contains(last)) return word;
        return MaleConsonant(word, c);
    }

    /// <summary>Jerzy, Antoni, Kowalski, Czarny – odmiana przymiotnikowa.</summary>
    private static string AdjectivalMale(string word, GrammaticalCase c)
    {
        var stem = word.EndsWith('y') ? word[..^1] : word; // Jerzy → Jerz-, Kowalski → Kowalski-
        return c switch
        {
            GrammaticalCase.Genitive or GrammaticalCase.Accusative => stem + "ego",
            GrammaticalCase.Dative => stem + "emu",
            GrammaticalCase.Instrumental or GrammaticalCase.Locative => word + "m",
            _ => word
        };
    }

    private static readonly (string From, string To)[] Softening = { ("dź", "dzi"), ("ń", "ni"), ("ś", "si"), ("ć", "ci"), ("ź", "zi") };

    /// <summary>Temat przypadków zależnych: ruchome „e” (Marek → Mark-, Paweł → Pawł-, Wróbel → Wróbl-) i zmiękczenia (ń → ni).</summary>
    private static string ObliqueStem(string word)
    {
        foreach (var (from, to) in Softening)
            if (word.EndsWith(from)) return word[..^from.Length] + to;
        if (word.Length > 3 && word.EndsWith("ek") && !IsVowel(word[^3])) return word[..^2] + "k";
        if (word.Length > 4 && word.EndsWith("ec") && !IsVowel(word[^3])) return word[..^2] + "c";
        if (word.Length > 3 && (word.EndsWith("eł") || word.EndsWith("el")) && !IsVowel(word[^3]) && word[^3] != 'i')
            return word[..^2] + word[^1];
        return word;
    }

    private static bool IsVowel(char ch) => "aeiouyąęóAEIOUYĄĘÓ".Contains(ch);

    private static string MaleConsonant(string word, GrammaticalCase c)
    {
        var stem = ObliqueStem(word);
        return c switch
        {
            GrammaticalCase.Genitive or GrammaticalCase.Accusative => stem + "a",
            GrammaticalCase.Dative => stem + "owi",
            GrammaticalCase.Instrumental => stem + (stem.EndsWith('k') || stem.EndsWith('g') ? "iem" : "em"),
            GrammaticalCase.Locative or GrammaticalCase.Vocative => MaleLocative(stem),
            _ => word
        };
    }

    /// <summary>Miejscownik (= wołacz) imion męskich: Janie, Piotrze, Robercie, Dawidzie, Pawle, Marku, Tomaszu.</summary>
    private static string MaleLocative(string stem)
    {
        string[] withU = { "ch", "sz", "cz", "rz", "dz", "dż", "ż", "k", "g", "h", "c", "j", "l", "i" };
        if (withU.Any(stem.EndsWith)) return stem + "u";
        if (stem.EndsWith("st")) return stem[..^2] + "ście";
        if (stem.EndsWith("zd")) return stem[..^2] + "ździe";
        if (stem.EndsWith("sł")) return stem[..^2] + "śle";
        if (stem.EndsWith('t')) return stem[..^1] + "cie";
        if (stem.EndsWith('d')) return stem[..^1] + "dzie";
        if (stem.EndsWith('r')) return stem[..^1] + "rze";
        if (stem.EndsWith('ł')) return stem[..^1] + "le";
        return stem + "ie";
    }

    /// <summary>Rzeczowniki na „-a” (Anna, Maria, Kasia, Monika, Kula, Kuba).</summary>
    private static string FeminineNoun(string word, GrammaticalCase c, bool vocativeO = false)
    {
        var stem = word[..^1];
        var lower = word.ToLowerInvariant();
        string Oblique(bool genitive)
        {
            if (lower.EndsWith("ia"))
            {
                // Kasia, Ania, Zosia → Kasi; Maria, Julia, Zofia → Marii
                return lower.Length >= 3 && "sczn".Contains(lower[^3]) ? stem : stem + "i";
            }
            if (lower.EndsWith("ja")) return lower.Length >= 3 && IsVowel(lower[^3]) ? word[..^2] + "i" : stem + "i"; // Maja → Mai, Alicja → Alicji
            if (lower.EndsWith("la")) return stem + "i"; // Ola → Oli, Kula → Kuli
            string[] hardY = { "cza", "sza", "rza", "ża", "ca", "dza", "dża" };
            if (hardY.Any(lower.EndsWith)) return stem + "y"; // Zawisza → Zawiszy
            if (lower.EndsWith("ka") || lower.EndsWith("ga")) return genitive ? stem + "i" : stem[..^1] + (lower.EndsWith("ka") ? "ce" : "dze");
            if (lower.EndsWith("cha")) return genitive ? stem + "y" : stem[..^2] + "sze";
            if (genitive) return stem + "y";
            if (lower.EndsWith("sta")) return stem[..^2] + "ście";
            if (lower.EndsWith("zda")) return stem[..^2] + "ździe";
            if (lower.EndsWith("sła")) return stem[..^2] + "śle";
            if (lower.EndsWith("ta")) return stem[..^1] + "cie";
            if (lower.EndsWith("da")) return stem[..^1] + "dzie";
            if (lower.EndsWith("ra")) return stem[..^1] + "rze";
            if (lower.EndsWith("ła")) return stem[..^1] + "le";
            return stem + "ie"; // Anna → Annie, Ewa → Ewie
        }
        return c switch
        {
            GrammaticalCase.Genitive => Oblique(true),
            GrammaticalCase.Dative or GrammaticalCase.Locative => Oblique(false),
            GrammaticalCase.Accusative => stem + "ę",
            GrammaticalCase.Instrumental => stem + "ą",
            GrammaticalCase.Vocative when vocativeO => word[..^1] + "o",
            GrammaticalCase.Vocative when lower.Length >= 4 && lower.EndsWith("ia") && "sczn".Contains(lower[^3]) => stem[..^1] + "iu",
            GrammaticalCase.Vocative when lower.Length <= 4 && lower.EndsWith("la") => stem + "u",
            GrammaticalCase.Vocative => stem + "o",
            _ => word
        };
    }
}
