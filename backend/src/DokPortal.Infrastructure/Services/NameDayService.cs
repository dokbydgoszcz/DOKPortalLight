using DokPortal.Application.NameDays;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class NameDayService : INameDayService
{
    private readonly AppDbContext _db;

    public NameDayService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<UpcomingNameDayDto>> GetUpcomingAsync(int days, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var people = await _db.People.AsNoTracking()
            .Where(p => p.NameDayMonth != null && p.NameDayDay != null)
            .ToListAsync(ct);

        return people
            .Select(p =>
            {
                var next = NextOccurrence(p.NameDayMonth!.Value, p.NameDayDay!.Value, today);
                return new UpcomingNameDayDto
                {
                    PersonId = p.Id,
                    FullName = p.FullName,
                    NameDayMonth = p.NameDayMonth!.Value,
                    NameDayDay = p.NameDayDay!.Value,
                    DaysUntil = next.DayNumber - today.DayNumber
                };
            })
            .Where(d => d.DaysUntil <= days)
            .OrderBy(d => d.DaysUntil)
            .ThenBy(d => d.FullName)
            .ToList();
    }

    private static DateOnly NextOccurrence(int month, int day, DateOnly today)
    {
        var candidate = SafeDate(today.Year, month, day);
        return candidate < today ? SafeDate(today.Year + 1, month, day) : candidate;
    }

    private static DateOnly SafeDate(int year, int month, int day)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        return new DateOnly(year, month, Math.Min(day, daysInMonth));
    }
}
