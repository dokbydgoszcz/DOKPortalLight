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
    public DateTime? UpdatedAtUtc { get; set; }
    /// <summary>Adresaci własnej listy (grupa <see cref="MailingGroup.Custom"/>).</summary>
    public List<MailingCampaignRecipient> Recipients { get; set; } = new();
}

/// <summary>Osoba z własnej listy kampanii.</summary>
public class MailingCampaignRecipient
{
    public Guid CampaignId { get; set; }
    public MailingCampaign? Campaign { get; set; }
    public Guid PersonId { get; set; }
    public Person? Person { get; set; }
}
