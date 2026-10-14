namespace DokPortal.Application.Parishes;

public class CreateParishRequest
{
    public required string Name { get; init; }
    public string? City { get; init; }
    /// <summary>Proboszcz parafii (osoba); pusty = parafia bez proboszcza. Ustawia funkcję Proboszcz tej osoby na tę parafię.</summary>
    public Guid? PastorPersonId { get; init; }
}
