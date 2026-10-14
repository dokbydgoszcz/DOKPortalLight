namespace DokPortal.Domain.Enums;

public enum CampaignStatus
{
    Draft,
    Sent,
    /// <summary>Kampania w kolejce: wiadomości wychodzą partiami w tle (limit dobowy skrzynki, np. Gmaila).</summary>
    Sending
}
