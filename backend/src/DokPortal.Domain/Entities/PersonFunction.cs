using DokPortal.Domain.Enums;

namespace DokPortal.Domain.Entities;

/// <summary>Funkcja pełniona przez osobę (relacja osoba → funkcja); funkcja ma własne właściwości.</summary>
public class PersonFunction
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public Person? Person { get; set; }
    public FunctionType Type { get; set; }
    /// <summary>Tylko dla proboszcza: parafia, którą kieruje (pusta = czeka na przydział).</summary>
    public Guid? ParishId { get; set; }
    public Parish? Parish { get; set; }
    /// <summary>Data ustanowienia / objęcia funkcji.</summary>
    public DateOnly? InstitutedOn { get; set; }
    public string? Notes { get; set; }
}
