using DokPortal.Domain.Enums;
using DokPortal.Domain.Formation;
using Xunit;

namespace DokPortal.Domain.Tests;

public class FormationCalendarTests
{
    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    [Theory]
    [InlineData(2026, 8, 31, 2025)]
    [InlineData(2026, 9, 1, 2026)]
    [InlineData(2026, 12, 31, 2026)]
    [InlineData(2027, 1, 1, 2026)]
    [InlineData(2027, 8, 31, 2026)]
    [InlineData(2027, 9, 1, 2027)]
    public void AcademicYearStart_ChangesOnTheFirstOfSeptember(int y, int m, int d, int expected)
    {
        Assert.Equal(expected, FormationCalendar.AcademicYearStart(D(y, m, d)));
    }

    [Theory]
    [InlineData(2026, 2026, 10, 3, 1)]
    [InlineData(2026, 2027, 8, 31, 1)]
    [InlineData(2026, 2027, 9, 1, 2)]
    [InlineData(2026, 2028, 8, 31, 2)]
    [InlineData(2026, 2028, 9, 1, 3)]
    [InlineData(2026, 2029, 8, 31, 3)]
    [InlineData(2026, 2029, 9, 1, 4)]
    public void YearOf_AdvancesEveryFirstOfSeptember(int startYear, int y, int m, int d, int expected)
    {
        Assert.Equal(expected, FormationCalendar.YearOf(startYear, D(y, m, d)));
    }

    [Fact]
    public void YearOf_IsNeverBelowOne_ForSomeoneWhoStartsInTheFuture()
    {
        Assert.Equal(1, FormationCalendar.YearOf(2027, D(2026, 10, 3)));
    }

    [Theory]
    [InlineData(1, 2026)]
    [InlineData(2, 2025)]
    [InlineData(3, 2024)]
    public void StartYearFor_GoesBackFromTheCurrentAcademicYear(int formationYear, int expectedStart)
    {
        var today = D(2026, 10, 3);

        Assert.Equal(expectedStart, FormationCalendar.StartYearFor(formationYear, today));
        Assert.Equal(formationYear, FormationCalendar.YearOf(FormationCalendar.StartYearFor(formationYear, today), today));
    }

    [Fact]
    public void StartYearFor_InSummer_RefersToTheAcademicYearThatIsStillRunning()
    {
        Assert.Equal(2025, FormationCalendar.StartYearFor(1, D(2026, 7, 15)));
    }

    [Fact]
    public void CompletedOn_IsTheFirstOfSeptemberAfterThirdYear()
    {
        Assert.Equal(D(2029, 9, 1), FormationCalendar.CompletedOn(2026));
    }

    [Theory]
    [InlineData(2026, false, 2029, 8, 31, CandidateFormationStatus.InFormation)]
    [InlineData(2026, false, 2029, 9, 1, CandidateFormationStatus.Completed)]
    [InlineData(2026, true, 2026, 10, 3, CandidateFormationStatus.Stopped)]
    [InlineData(2026, true, 2030, 1, 1, CandidateFormationStatus.Stopped)]
    public void StatusOf_StoppingWinsOverTheCalendar(int startYear, bool stopped, int y, int m, int d, CandidateFormationStatus expected)
    {
        Assert.Equal(expected, FormationCalendar.StatusOf(startYear, stopped, D(y, m, d)));
    }
}
