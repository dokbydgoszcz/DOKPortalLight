using DokPortal.Application.Common;

namespace DokPortal.Infrastructure.Services;

/// <summary>
/// Used when Smtp is not configured (local dev without SMTP, or test environments).
/// Fails only when actually used, not at startup, so the rest of the app keeps working.
/// </summary>
public class NullEmailSender : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct) =>
        throw new InvalidOperationException("Wysyłanie e-maili nie jest skonfigurowane (brak Smtp:Host).");
}
