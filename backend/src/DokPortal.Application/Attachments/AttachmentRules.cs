namespace DokPortal.Application.Attachments;

/// <summary>Wspólne zasady dla plików wgrywanych do aplikacji: dozwolone rozszerzenia i limit rozmiaru (ten sam po stronie serwera i klienta).</summary>
public static class AttachmentRules
{
    public const long MaxFileBytes = 20 * 1024 * 1024;

    /// <summary>Limit całego żądania: plik plus narzut formularza multipart.</summary>
    public const long MaxRequestBytes = MaxFileBytes + 1024 * 1024;

    public const string AllowedTypesDescription = "PDF, JPG/JPEG, PNG, DOCX, DOC, TXT";

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".doc"] = "application/msword",
        [".txt"] = "text/plain; charset=utf-8"
    };

    /// <summary>Zwraca komunikat błędu albo null, gdy plik spełnia zasady.</summary>
    public static string? Validate(string fileName, long sizeBytes)
    {
        if (!ContentTypes.ContainsKey(Path.GetExtension(CleanFileName(fileName))))
        {
            return $"Niedozwolony typ pliku. Dozwolone: {AllowedTypesDescription}.";
        }
        if (sizeBytes <= 0) return "Plik jest pusty.";
        if (sizeBytes > MaxFileBytes) return "Plik jest za duży – maksymalny rozmiar to 20 MB.";
        return null;
    }

    /// <summary>Typ zawartości wynika z rozszerzenia, a nie z nagłówka podanego przez przeglądarkę.</summary>
    public static string ContentTypeFor(string fileName) =>
        ContentTypes.GetValueOrDefault(Path.GetExtension(CleanFileName(fileName)), "application/octet-stream");

    /// <summary>Zostawia samą nazwę pliku (bez katalogów, niezależnie od rodzaju separatora).</summary>
    public static string CleanFileName(string fileName) => fileName.Split('/', '\\').Last().Trim();
}
