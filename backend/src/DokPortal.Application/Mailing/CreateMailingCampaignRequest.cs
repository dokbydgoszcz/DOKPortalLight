using DokPortal.Domain.Enums;

namespace DokPortal.Application.Mailing;

public class CreateMailingCampaignRequest
{
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public MailingGroup Group { get; init; }
}
