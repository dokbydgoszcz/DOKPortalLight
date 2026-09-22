using DokPortal.Domain.Enums;

namespace DokPortal.Application.Mailing;

public class MailingCampaignDto
{
    public required Guid Id { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public MailingGroup Group { get; init; }
    public required int RecipientCount { get; init; }
    public CampaignStatus Status { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public DateTime? SentAtUtc { get; init; }
}
