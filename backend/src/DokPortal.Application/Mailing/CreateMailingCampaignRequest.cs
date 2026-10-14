using DokPortal.Domain.Enums;

namespace DokPortal.Application.Mailing;

/// <summary>Nowa kampania albo poprawka szkicu.</summary>
public class CreateMailingCampaignRequest
{
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public MailingGroup Group { get; init; }
    /// <summary>Tylko dla grupy „Własna lista”: wybrane osoby.</summary>
    public IReadOnlyList<Guid>? PersonIds { get; init; }
}

/// <summary>Adresat w podglądzie (bez e-maila = nie dostanie wiadomości).</summary>
public class MailingRecipientDto
{
    public required Guid PersonId { get; init; }
    public required string FullName { get; init; }
    public string? Email { get; init; }
    /// <summary>Osoba wypisała się z mailingu – wiadomości nie dostanie.</summary>
    public bool OptedOut { get; init; }
}
