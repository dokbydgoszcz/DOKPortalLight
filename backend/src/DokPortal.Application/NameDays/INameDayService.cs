namespace DokPortal.Application.NameDays;

public interface INameDayService
{
    Task<IReadOnlyList<UpcomingNameDayDto>> GetUpcomingAsync(int days, CancellationToken ct);
}
