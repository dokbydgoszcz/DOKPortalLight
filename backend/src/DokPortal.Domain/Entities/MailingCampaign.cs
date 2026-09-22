using DokPortal.Domain.Enums;

namespace DokPortal.Domain.Entities;

public class MailingCampaign
{
    public Guid Id { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }
    public MailingGroup Group { get; set; }
    public int RecipientCount { get; set; }
    public CampaignStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
}
