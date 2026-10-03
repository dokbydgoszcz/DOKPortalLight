using DokPortal.Domain.Enums;

namespace DokPortal.Domain.Formation;

/// <summary>
/// Rok formacji kandydata wynika z daty: rok szkolny zaczyna się 1 września, więc kandydat awansuje sam, bez żadnego zadania w tle.
/// Kandydat pamięta tylko rok kalendarzowy września, w którym zaczął I rok.
/// </summary>
public static class FormationCalendar
{
    public const int YearsOfFormation = 3;

    /// <summary>Rok kalendarzowy września, od którego trwa bieżący rok szkolny (do 31 sierpnia nadal poprzedni).</summary>
    public static int AcademicYearStart(DateOnly date) => date.Month >= 9 ? date.Year : date.Year - 1;

    /// <summary>Który rok formacji trwa: 1 przed rozpoczęciem, powyżej 3 po ukończeniu.</summary>
    public static int YearOf(int startYear, DateOnly today) => Math.Max(1, AcademicYearStart(today) - startYear + 1);

    /// <summary>Rok rozpoczęcia dla kandydata, który dziś jest w danym roku formacji.</summary>
    public static int StartYearFor(int formationYear, DateOnly today) => AcademicYearStart(today) - (formationYear - 1);

    /// <summary>Dzień, od którego formacja jest ukończona (1 września po III roku).</summary>
    public static DateOnly CompletedOn(int startYear) => new(startYear + YearsOfFormation, 9, 1);

    public static CandidateFormationStatus StatusOf(int startYear, bool isStopped, DateOnly today)
    {
        if (isStopped) return CandidateFormationStatus.Stopped;
        return YearOf(startYear, today) > YearsOfFormation ? CandidateFormationStatus.Completed : CandidateFormationStatus.InFormation;
    }
}
