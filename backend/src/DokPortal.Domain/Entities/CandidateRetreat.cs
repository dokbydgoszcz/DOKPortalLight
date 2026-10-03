namespace DokPortal.Domain.Entities;

/// <summary>Rekolekcje kandydata w danym roku formacji (1–3): jeden rekord na rok.</summary>
public class CandidateRetreat
{
    public Guid Id { get; set; }
    public Guid CandidateId { get; set; }
    public Candidate? Candidate { get; set; }
    public int Year { get; set; }
    public bool IsCompleted { get; set; }
}
