namespace DokPortal.Application.Candidates;

/// <summary>Warunki ukończenia formacji SKŚP: rekolekcje każdego roku, komplet opinii i frekwencja.</summary>
public static class CandidateCompletion
{
    public const int FormationYears = 3;
    /// <summary>Minimalna frekwencja na zajęciach (w procentach).</summary>
    public const int MinAttendancePercentage = 80;

    private static readonly string[] Roman = { "", "I", "II", "III" };

    /// <summary>Lista braków (pusta = wszystkie warunki spełnione).</summary>
    public static IReadOnlyList<string> Gaps(IEnumerable<CandidateRetreatDto> retreats, int opinionsCollected, int opinionsRequired, int? attendancePercentage)
    {
        var gaps = new List<string>();
        var done = retreats.Where(r => r.IsCompleted).Select(r => r.Year).ToHashSet();
        var missing = Enumerable.Range(1, FormationYears).Where(y => !done.Contains(y)).ToList();
        if (missing.Count > 0)
            gaps.Add($"rekolekcje niezaliczone: rok {string.Join(", ", missing.Select(y => Roman[y]))}");
        if (opinionsCollected < opinionsRequired)
            gaps.Add($"opinie: {opinionsCollected} z {opinionsRequired}");
        if (attendancePercentage is null)
            gaps.Add("brak frekwencji");
        else if (attendancePercentage < MinAttendancePercentage)
            gaps.Add($"frekwencja {attendancePercentage}% (wymagane {MinAttendancePercentage}%)");
        return gaps;
    }

    /// <summary>Frekwencja: z list obecności zajęć (obecny / zaznaczone), a gdy ich nie ma – wpisana ręcznie.</summary>
    public static int? EffectiveAttendance(int? manual, int attended, int recorded) =>
        recorded > 0 ? (int)Math.Round(100.0 * attended / recorded, MidpointRounding.AwayFromZero) : manual;

    public static string Describe(IReadOnlyList<string> gaps) => string.Join("; ", gaps);
}
