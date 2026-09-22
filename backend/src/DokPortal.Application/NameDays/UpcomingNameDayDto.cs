namespace DokPortal.Application.NameDays;

public class UpcomingNameDayDto
{
    public required Guid PersonId { get; init; }
    public required string FullName { get; init; }
    public required int NameDayMonth { get; init; }
    public required int NameDayDay { get; init; }
    public required int DaysUntil { get; init; }
}
