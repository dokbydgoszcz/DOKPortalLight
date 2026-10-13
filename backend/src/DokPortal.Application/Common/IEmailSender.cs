namespace DokPortal.Application.Common;

/// <summary>Plik dołączony do wiadomości e-mail.</summary>
public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string body, CancellationToken ct);

    /// <summary>Wiadomość z załącznikami. Bez załączników – zwykła wysyłka.</summary>
    Task SendAsync(string toEmail, string subject, string body, IReadOnlyList<EmailAttachment> attachments, CancellationToken ct) =>
        attachments.Count == 0
            ? SendAsync(toEmail, subject, body, ct)
            : throw new NotSupportedException("Ten sposób wysyłki nie obsługuje załączników.");
}
