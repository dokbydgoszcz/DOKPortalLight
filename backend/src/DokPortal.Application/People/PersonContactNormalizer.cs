namespace DokPortal.Application.People;

/// <summary>Sprowadza e-mail i telefon do postaci porównywalnej (do wykrywania duplikatów).</summary>
public static class PersonContactNormalizer
{
    private const int MinPhoneDigits = 6;

    public static string? NormalizeEmail(string? email)
    {
        var trimmed = email?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.ToLowerInvariant();
    }

    /// <summary>Same cyfry, bez polskiego prefiksu (0048 / 48 / +48). Zbyt krótkie numery są ignorowane.</summary>
    public static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("0048", StringComparison.Ordinal))
        {
            digits = digits[4..];
        }
        else if (digits.Length == 11 && digits.StartsWith("48", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }

        return digits.Length < MinPhoneDigits ? null : digits;
    }
}
