namespace DokPortal.Domain.Entities;

public enum MailingDeliveryStatus
{
    Pending,
    Sent,
    Failed,
    /// <summary>Adresat wypisał się z mailingu, zanim przyszła jego kolej.</summary>
    Skipped
}

/// <summary>Jedna wiadomość kampanii w kolejce wysyłki (jeden adres e-mail).</summary>
public class MailingDelivery
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public MailingCampaign? Campaign { get; set; }
    public Guid? PersonId { get; set; }
    public required string Email { get; set; }
    public MailingDeliveryStatus Status { get; set; }
    /// <summary>Ile razy próbowano wysłać (po 3 nieudanych próbach adres uznajemy za błędny).</summary>
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    /// <summary>Ostatnia próba wysłania – z niej liczymy dobowy limit skrzynki.</summary>
    public DateTime? AttemptedAtUtc { get; set; }
    /// <summary>Kolejność w kolejce.</summary>
    public int Position { get; set; }
}
