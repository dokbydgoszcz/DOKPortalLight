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
}
