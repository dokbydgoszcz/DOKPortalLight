using DokPortal.Domain.Enums;

namespace DokPortal.Application.People;

/// <summary>Funkcja osoby w żądaniu zapisu: ten sam typ (i parafia) = ta sama funkcja.</summary>
public class PersonFunctionInput
{
    public FunctionType Type { get; init; }
    /// <summary>Tylko dla proboszcza.</summary>
    public Guid? ParishId { get; init; }
    public DateOnly? InstitutedOn { get; init; }
    public string? Notes { get; init; }
}
