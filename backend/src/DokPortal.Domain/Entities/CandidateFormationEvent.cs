using DokPortal.Domain.Enums;

namespace DokPortal.Domain.Entities;

/// <summary>Ślad zmiany roku formacji: kiedy, przez kogo i z którego roku na który.</summary>
public class CandidateFormationEvent
{
    public Guid Id { get; set; }
    public Guid CandidateId { get; set; }
    public Candidate? Candidate { get; set; }
    /// <summary>Kolejny numer zdarzenia kandydata (porządkuje zdarzenia z tą samą chwilą).</summary>
    public int Sequence { get; set; }
    public CandidateFormationEventKind Kind { get; set; }
    public int? FromYear { get; set; }
    /// <summary>Pusty przy ukończeniu formacji.</summary>
    public int? ToYear { get; set; }
    public DateTime AtUtc { get; set; }
    public string? PerformedByUserId { get; set; }
    public string? PerformedByEmail { get; set; }
}
