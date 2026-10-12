using DokPortal.Domain.Enums;

namespace DokPortal.Domain.Entities;

/// <summary>Ślad zmiany w sprawie DOK (etap, ścieżka, katechista, mentor): kiedy, kto, z czego na co.</summary>
public class DokCaseEvent
{
    public Guid Id { get; set; }
    public Guid DokCaseId { get; set; }
    public DokCase? DokCase { get; set; }
    /// <summary>Kolejny numer zdarzenia sprawy (porządkuje zdarzenia z tą samą chwilą).</summary>
    public int Sequence { get; set; }
    public DokCaseEventKind Kind { get; set; }
    /// <summary>Poprzednia wartość czytelna dla człowieka (nazwa etapu, imię i nazwisko); pusta = brak.</summary>
    public string? FromValue { get; set; }
    public string? ToValue { get; set; }
    public DateTime AtUtc { get; set; }
    public string? PerformedByUserId { get; set; }
    public string? PerformedByEmail { get; set; }
}
