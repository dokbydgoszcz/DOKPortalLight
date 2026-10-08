using DokPortal.Domain.Enums;

namespace DokPortal.Domain.Entities;

public class MailingCampaign
{
    public Guid Id { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }
    public MailingGroup Group { get; set; }
    /// <summary>Ilu odbiorcom wiadomość została wysłana (przy szkicu: ilu będzie adresatów).</summary>
    public int RecipientCount { get; set; }
    /// <summary>Ilu adresatom nie udało się wysłać wiadomości (błąd serwera poczty dla tego adresu).</summary>
    public int FailedCount { get; set; }
    public CampaignStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
}
