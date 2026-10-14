using DokPortal.Application.Auth;
using DokPortal.Application.Common;
using DokPortal.Infrastructure.Auth;
using DokPortal.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace DokPortal.Infrastructure.Services;

public class AccountService : IAccountService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly PortalUrl _portalUrl;
    private readonly SessionValidator? _sessions;
    private readonly ILogger<AccountService>? _logger;
    private readonly ITimeLimitedDataProtector _mfaProtector;

    public const string Issuer = "DOK Portal";
    /// <summary>Dostawca, pod którym Identity trzyma klucz aplikacji i kody zapasowe (AspNetUserTokens).</summary>
    private const string StoreProvider = "[AspNetUserStore]";

    public AccountService(UserManager<AppUser> userManager, IEmailSender emailSender, PortalUrl portalUrl,
        IDataProtectionProvider dataProtection, SessionValidator? sessions = null, ILogger<AccountService>? logger = null)
    {
        _mfaProtector = dataProtection.CreateProtector("DokPortal.MfaLogin").ToTimeLimitedDataProtector();
        _userManager = userManager;
        _emailSender = emailSender;
        _portalUrl = portalUrl;
        _sessions = sessions;
        _logger = logger;
    }

    private static string Errors(IdentityResult result) => string.Join(" ", result.Errors.Select(e => e.Description));

    public async Task ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException("Nie znaleziono konta.");
        if (!await _userManager.CheckPasswordAsync(user, currentPassword))
        {
            throw new InvalidOperationException("Obecne hasło jest nieprawidłowe.");
        }

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded) throw new InvalidOperationException(Errors(result));

        user.MustChangePassword = false;
        await _userManager.UpdateAsync(user);
        _sessions?.Invalidate(user.Id);
    }

    public async Task RequestPasswordResetAsync(string email, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null || user.IsDeactivated || string.IsNullOrWhiteSpace(user.Email)) return;

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var link = _portalUrl.Link($"reset-hasla?uid={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(token)}");
        var body =
            "Dzień dobry,\n\n" +
            "ktoś (zapewne Ty) poprosił o zmianę hasła do DOK Portal dla konta " + user.Email + ".\n\n" +
            "Aby ustawić nowe hasło, otwórz link (ważny 2 godziny, działa jeden raz):\n" +
            link + "\n\n" +
            "Jeśli to nie Ty – zignoruj tę wiadomość, hasło się nie zmieni.\n\n" +
            "DOK Portal";
        try
        {
            await _emailSender.SendAsync(user.Email, "Zmiana hasła w DOK Portal", body, ct);
        }
        catch (Exception ex)
        {
            // Odpowiedź ma być taka sama jak dla nieznanego adresu – błąd poczty tylko logujemy.
            _logger?.LogError(ex, "Nie udało się wysłać linku do zmiany hasła dla konta {UserId}", user.Id);
        }
    }

    // ---------- Weryfikacja dwuetapowa (aplikacja typu Microsoft/Google Authenticator) ----------

    private static string Normalize(string code) => code.Replace(" ", "").Replace("-", "").Trim();

    private async Task<AppUser> UserAsync(string userId) =>
        await _userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException("Nie znaleziono konta.");

    private Task<bool> IsValidAppCodeAsync(AppUser user, string code) =>
        _userManager.VerifyTwoFactorTokenAsync(user, _userManager.Options.Tokens.AuthenticatorTokenProvider, Normalize(code));

    public async Task<MfaStatusResponse> GetMfaStatusAsync(string userId, CancellationToken ct)
    {
        var user = await UserAsync(userId);
        return new MfaStatusResponse(user.TwoFactorEnabled, user.TwoFactorEnabled ? await _userManager.CountRecoveryCodesAsync(user) : 0);
    }

    public async Task<MfaSetupResponse> SetupMfaAsync(string userId, CancellationToken ct)
    {
        var user = await UserAsync(userId);
        if (user.TwoFactorEnabled) throw new InvalidOperationException("Weryfikacja dwuetapowa jest już włączona.");

        // Klucz zapisujemy bez zmiany znacznika bezpieczeństwa (ResetAuthenticatorKeyAsync zakończyłby bieżącą sesję).
        var key = _userManager.GenerateNewAuthenticatorKey();
        await _userManager.SetAuthenticationTokenAsync(user, StoreProvider, "AuthenticatorKey", key);
        var issuer = Uri.EscapeDataString(Issuer);
        var label = Uri.EscapeDataString(user.Email ?? user.UserName ?? user.Id);
        return new MfaSetupResponse(key, $"otpauth://totp/{issuer}:{label}?secret={key}&issuer={issuer}&digits=6&period=30");
    }

    public async Task<IReadOnlyList<string>> EnableMfaAsync(string userId, string code, CancellationToken ct)
    {
        var user = await UserAsync(userId);
        if (string.IsNullOrEmpty(await _userManager.GetAuthenticatorKeyAsync(user)))
            throw new InvalidOperationException("Najpierw zeskanuj kod QR (rozpocznij konfigurację).");
        if (!await IsValidAppCodeAsync(user, code))
            throw new InvalidOperationException("Nieprawidłowy kod. Sprawdź, czy godzina w telefonie jest ustawiona automatycznie.");

        await _userManager.SetTwoFactorEnabledAsync(user, true);
        var codes = (await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))!.ToList();
        _sessions?.Invalidate(user.Id);
        await NotifyAsync(user, "Weryfikacja dwuetapowa włączona",
            "na Twoim koncie w DOK Portal włączono weryfikację dwuetapową. Od teraz przy logowaniu, po haśle, podasz kod z aplikacji uwierzytelniającej.", ct);
        return codes;
    }

    public async Task DisableMfaAsync(string userId, string code, CancellationToken ct)
    {
        var user = await UserAsync(userId);
        if (!user.TwoFactorEnabled) throw new InvalidOperationException("Weryfikacja dwuetapowa nie jest włączona.");
        if (!await IsValidAppCodeAsync(user, code)) throw new InvalidOperationException("Nieprawidłowy kod.");

        await TurnOffMfaAsync(_userManager, user);
        _sessions?.Invalidate(user.Id);
        await NotifyAsync(user, "Weryfikacja dwuetapowa wyłączona",
            "na Twoim koncie w DOK Portal wyłączono weryfikację dwuetapową. Jeśli to nie Ty – zmień hasło i powiadom administratora.", ct);
    }

    /// <summary>Wyłącza MFA i unieważnia klucz oraz kody zapasowe (także przy resecie przez administratora).</summary>
    public static async Task TurnOffMfaAsync(UserManager<AppUser> userManager, AppUser user)
    {
        await userManager.SetTwoFactorEnabledAsync(user, false);
        await userManager.RemoveAuthenticationTokenAsync(user, StoreProvider, "AuthenticatorKey");
        await userManager.RemoveAuthenticationTokenAsync(user, StoreProvider, "RecoveryCodes");
    }

    private async Task NotifyAsync(AppUser user, string subject, string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(user.Email)) return;
        try
        {
            await _emailSender.SendAsync(user.Email, subject, $"Dzień dobry,\n\n{text}\n\nDOK Portal", ct);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Nie udało się wysłać powiadomienia o MFA dla konta {UserId}", user.Id);
        }
    }

    /// <summary>Kody zapasowe Identity mają postać „XXXXX-XXXXX”; przyjmujemy je też bez myślnika i małymi literami.</summary>
    private static string RecoveryFormat(string code)
    {
        var upper = code.ToUpperInvariant();
        return upper.Length == 10 ? $"{upper[..5]}-{upper[5..]}" : upper;
    }

    public string CreateMfaChallenge(string userId, string? securityStamp) =>
        _mfaProtector.Protect($"{userId}|{securityStamp}", TimeSpan.FromMinutes(5));

    public async Task<string?> VerifyMfaLoginAsync(string mfaToken, string code, CancellationToken ct)
    {
        string payload;
        try
        {
            payload = _mfaProtector.Unprotect(mfaToken);
        }
        catch (Exception)
        {
            return null; // bilet zły albo wygasł
        }
        var parts = payload.Split('|', 2);
        var user = await _userManager.FindByIdAsync(parts[0]);
        if (user is null || user.IsDeactivated || !user.TwoFactorEnabled || parts.Length < 2 || parts[1] != (user.SecurityStamp ?? "")) return null;
        if (await _userManager.IsLockedOutAsync(user)) return null;

        var normalized = Normalize(code);
        var ok = normalized.Length == 6 && normalized.All(char.IsDigit)
            ? await IsValidAppCodeAsync(user, normalized)
            : (await _userManager.RedeemTwoFactorRecoveryCodeAsync(user, RecoveryFormat(normalized))).Succeeded;
        if (!ok)
        {
            await _userManager.AccessFailedAsync(user);
            return null;
        }
        await _userManager.ResetAccessFailedCountAsync(user);
        return user.Id;
    }

    public async Task ResetPasswordWithTokenAsync(string userId, string token, string newPassword, CancellationToken ct)
    {
        const string invalid = "Link do zmiany hasła jest nieprawidłowy albo wygasł. Poproś o nowy („Nie pamiętam hasła”).";
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null || user.IsDeactivated) throw new InvalidOperationException(invalid);

        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken))) throw new InvalidOperationException(invalid);
            throw new InvalidOperationException(Errors(result));
        }

        user.MustChangePassword = false;
        await _userManager.UpdateAsync(user);
        await _userManager.ResetAccessFailedCountAsync(user);
        await _userManager.SetLockoutEndDateAsync(user, null);
        _sessions?.Invalidate(user.Id);
    }
}
