using DokPortal.Domain.Enums;

namespace DokPortal.Application.Mailing;

public interface IMailingService
{
    Task<IReadOnlyList<MailingCampaignDto>> ListAsync(CancellationToken ct);
    Task<MailingCampaignDto> CreateAsync(CreateMailingCampaignRequest request, CancellationToken ct);
    Task<MailingCampaignDto?> SendAsync(Guid id, CancellationToken ct);
    Task<int> GetRecipientCountAsync(MailingGroup group, CancellationToken ct);
}
