using DokPortal.Application.Auth;
using DokPortal.Application.Common;
using DokPortal.Infrastructure.Auth;
using DokPortal.Infrastructure.Identity;
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

    public AccountService(UserManager<AppUser> userManager, IEmailSender emailSender, PortalUrl portalUrl,
        SessionValidator? sessions = null, ILogger<AccountService>? logger = null)
    {
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
