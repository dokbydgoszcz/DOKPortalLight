using DokPortal.Domain.Entities;

namespace DokPortal.Infrastructure.Services;

public static class CandidateQueries
{
    /// <summary>Kandydaci w trakcie formacji (rok I–III): nie ukończyli jej i nie mają jej zatrzymanej.</summary>
    public static IQueryable<Candidate> InFormation(this IQueryable<Candidate> candidates) =>
        candidates.Where(c => !c.IsFormationStopped && !c.IsFormationCompleted);

    /// <summary>Kandydaci, którzy ukończyli formację (ręcznie odhaczone) i nie mają jej zatrzymanej.</summary>
    public static IQueryable<Candidate> Completed(this IQueryable<Candidate> candidates) =>
        candidates.Where(c => !c.IsFormationStopped && c.IsFormationCompleted);

    public static DateOnly Today(this TimeProvider time) => DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
}
