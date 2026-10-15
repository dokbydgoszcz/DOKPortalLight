namespace DokPortal.Application.Parishes;

public class CreateParishRequest
{
    public required string Name { get; init; }
    public string? City { get; init; }
    public string? Deanery { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Website { get; init; }
    /// <summary>Proboszcz parafii (osoba); pusty = parafia bez proboszcza. Ustawia funkcję Proboszcz tej osoby na tę parafię.</summary>
    public Guid? PastorPersonId { get; init; }
}
