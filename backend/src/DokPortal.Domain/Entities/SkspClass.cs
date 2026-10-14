namespace DokPortal.Domain.Entities;

/// <summary>Zajęcia SKŚP dla jednego roku formacji z imienną listą obecności kandydatów.</summary>
public class SkspClass : ISoftDeletable
{
    public Guid Id { get; set; }
    /// <summary>Rok formacji 1–3, dla którego są zajęcia.</summary>
    public int Year { get; set; }
    public DateOnly ClassDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<SkspClassAttendee> Attendees { get; set; } = new();
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}

public class SkspClassAttendee
{
    public Guid Id { get; set; }
    public Guid SkspClassId { get; set; }
    public SkspClass? SkspClass { get; set; }
    public Guid CandidateId { get; set; }
    public Candidate? Candidate { get; set; }
    /// <summary>true = obecny, false = nieobecny, null = jeszcze nie zaznaczono (nie liczy się do frekwencji).</summary>
    public bool? IsPresent { get; set; }
}
