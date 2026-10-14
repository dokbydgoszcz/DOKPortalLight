namespace DokPortal.Application.Auth;

/// <summary>Hasła bez pośrednictwa administratora.</summary>
public interface IAccountService
{
    /// <summary>Zmienia hasło zalogowanego użytkownika i zdejmuje obowiązek zmiany; stare sesje wygasają.</summary>
    /// <exception cref="InvalidOperationException">Złe obecne hasło albo nowe nie spełnia zasad.</exception>
    Task ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken ct);

    /// <summary>Wysyła link do ustawienia nowego hasła, jeśli konto istnieje i jest aktywne. Nie zdradza, czy istnieje.</summary>
    Task RequestPasswordResetAsync(string email, CancellationToken ct);

    /// <summary>Ustawia nowe hasło jednorazowym kodem z e-maila.</summary>
    /// <exception cref="InvalidOperationException">Kod zły, użyty albo wygasł; hasło nie spełnia zasad.</exception>
    Task ResetPasswordWithTokenAsync(string userId, string token, string newPassword, CancellationToken ct);

    Task<MfaStatusResponse> GetMfaStatusAsync(string userId, CancellationToken ct);
    /// <summary>Nowy klucz dla aplikacji (MFA zaczyna działać dopiero po <see cref="EnableMfaAsync"/>).</summary>
    Task<MfaSetupResponse> SetupMfaAsync(string userId, CancellationToken ct);
    /// <summary>Włącza MFA po poprawnym kodzie; zwraca kody zapasowe.</summary>
    /// <exception cref="InvalidOperationException">Zły kod albo brak klucza.</exception>
    Task<IReadOnlyList<string>> EnableMfaAsync(string userId, string code, CancellationToken ct);
    /// <exception cref="InvalidOperationException">Zły kod albo MFA nie jest włączone.</exception>
    Task DisableMfaAsync(string userId, string code, CancellationToken ct);
    /// <summary>Krótkotrwały (5 min) bilet między hasłem a kodem.</summary>
    string CreateMfaChallenge(string userId, string? securityStamp);
    /// <summary>Sprawdza bilet i kod (z aplikacji lub zapasowy); zwraca Id konta albo null.</summary>
    Task<string?> VerifyMfaLoginAsync(string mfaToken, string code, CancellationToken ct);
}
