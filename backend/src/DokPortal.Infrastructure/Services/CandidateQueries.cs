using DokPortal.Domain.Entities;
using DokPortal.Domain.Formation;

namespace DokPortal.Infrastructure.Services;

public static class CandidateQueries
{
    /// <summary>Kandydaci, którzy dziś są w roku I–III i nie mają zatrzymanej formacji.</summary>
    public static IQueryable<Candidate> InFormation(this IQueryable<Candidate> candidates, DateOnly today)
    {
        var oldestStart = FormationCalendar.AcademicYearStart(today) - (FormationCalendar.YearsOfFormation - 1);
        return candidates.Where(c => !c.IsFormationStopped && c.FormationStartYear >= oldestStart);
    }

    /// <summary>Kandydaci, którzy ukończyli III rok (od 1 września) i nie mają zatrzymanej formacji.</summary>
    public static IQueryable<Candidate> Completed(this IQueryable<Candidate> candidates, DateOnly today)
    {
        var oldestStart = FormationCalendar.AcademicYearStart(today) - (FormationCalendar.YearsOfFormation - 1);
        return candidates.Where(c => !c.IsFormationStopped && c.FormationStartYear < oldestStart);
    }

    public static DateOnly Today(this TimeProvider time) => DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
}
