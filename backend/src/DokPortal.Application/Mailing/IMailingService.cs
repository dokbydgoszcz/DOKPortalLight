using DokPortal.Domain.Enums;

namespace DokPortal.Application.Mailing;

public interface IMailingService
{
    Task<IReadOnlyList<MailingCampaignDto>> ListAsync(CancellationToken ct);
    Task<MailingCampaignDto> CreateAsync(CreateMailingCampaignRequest request, CancellationToken ct);
    /// <summary>Poprawia szkic (temat, treść, grupa, lista osób).</summary>
    /// <exception cref="InvalidOperationException">Kampania już wysłana albo nieznane osoby na liście.</exception>
    Task<MailingCampaignDto?> UpdateDraftAsync(Guid id, CreateMailingCampaignRequest request, CancellationToken ct);
    /// <summary>
    /// Wstawia kampanię do kolejki i od razu wysyła pierwszą wiadomość (żeby błąd ustawień poczty wyszedł od razu);
    /// resztę wysyła <see cref="ProcessQueueAsync"/> w tle, w granicach dobowego limitu.
    /// </summary>
    Task<MailingCampaignDto?> SendAsync(Guid id, CancellationToken ct);
    /// <summary>Wysyła oczekujące wiadomości wszystkich kampanii, dopóki pozwala dobowy limit.</summary>
    Task<MailingQueueResultDto> ProcessQueueAsync(CancellationToken ct);
    /// <summary>Stan zapisu osoby z linku w stopce (null = nieznany link).</summary>
    Task<MailingSubscriptionDto?> GetSubscriptionAsync(string token, CancellationToken ct);
    /// <summary>Wypisuje (albo zapisuje ponownie) osobę z linku w stopce.</summary>
    Task<MailingSubscriptionDto?> SetOptOutAsync(string token, bool optOut, CancellationToken ct);
    Task<bool> DeleteDraftAsync(Guid id, CancellationToken ct);
    /// <summary>Czy kampania istnieje i jest szkicem (null = nie istnieje).</summary>
    Task<bool?> IsDraftAsync(Guid id, CancellationToken ct);
    /// <summary>Wysyła jedną wiadomość testową, żeby sprawdzić ustawienia SMTP.</summary>
    /// <exception cref="InvalidOperationException">Brak konfiguracji SMTP albo serwer odrzucił wiadomość (z jego komunikatem).</exception>
    Task SendTestAsync(string toEmail, CancellationToken ct);
    /// <summary>Ilu adresatów z e-mailem ma grupa.</summary>
    Task<int> GetRecipientCountAsync(MailingGroup group, CancellationToken ct);
    /// <summary>Podgląd adresatów grupy (także osoby bez e-maila – one wiadomości nie dostaną).</summary>
    Task<IReadOnlyList<MailingRecipientDto>> GetRecipientsAsync(MailingGroup group, IReadOnlyList<Guid>? personIds, CancellationToken ct);
    Task<IReadOnlyList<MailingRecipientDto>?> GetCampaignRecipientsAsync(Guid id, CancellationToken ct);
}
