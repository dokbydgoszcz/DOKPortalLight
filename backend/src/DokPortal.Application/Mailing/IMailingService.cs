using DokPortal.Domain.Enums;

namespace DokPortal.Application.Mailing;

public interface IMailingService
{
    Task<IReadOnlyList<MailingCampaignDto>> ListAsync(CancellationToken ct);
    Task<MailingCampaignDto> CreateAsync(CreateMailingCampaignRequest request, CancellationToken ct);
    Task<MailingCampaignDto?> SendAsync(Guid id, CancellationToken ct);
    Task<bool> DeleteDraftAsync(Guid id, CancellationToken ct);
    /// <summary>Wysyła jedną wiadomość testową, żeby sprawdzić ustawienia SMTP.</summary>
    /// <exception cref="InvalidOperationException">Brak konfiguracji SMTP albo serwer odrzucił wiadomość (z jego komunikatem).</exception>
    Task SendTestAsync(string toEmail, CancellationToken ct);
    Task<int> GetRecipientCountAsync(MailingGroup group, CancellationToken ct);
}
