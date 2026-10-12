using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

/// <summary>
/// Data ostatniego spotkania sprawy liczona ze spotkań (indywidualnych i zajęć grupowych z udziałem podopiecznego) do dziś włącznie.
/// Zastępuje dawne pole <c>DokCase.LastMeetingDate</c>, którego nic nie uzupełniało.
/// </summary>
internal static class LastMeetingDates
{
    public static async Task<Dictionary<Guid, DateOnly>> ForCasesAsync(AppDbContext db, IReadOnlyCollection<Guid> caseIds, DateOnly today, CancellationToken ct)
    {
        var individual = await db.Meetings.AsNoTracking()
            .Where(m => m.DokCaseId != null && caseIds.Contains(m.DokCaseId.Value) && m.MeetingDate <= today)
            .GroupBy(m => m.DokCaseId!.Value)
            .Select(g => new { CaseId = g.Key, Last = g.Max(m => m.MeetingDate) })
            .ToListAsync(ct);
        var group = await db.MeetingAttendees.AsNoTracking()
            .Where(a => caseIds.Contains(a.DokCaseId) && a.Meeting!.MeetingDate <= today)
            .GroupBy(a => a.DokCaseId)
            .Select(g => new { CaseId = g.Key, Last = g.Max(a => a.Meeting!.MeetingDate) })
            .ToListAsync(ct);

        var result = new Dictionary<Guid, DateOnly>();
        foreach (var row in individual.Concat(group))
        {
            if (!result.TryGetValue(row.CaseId, out var current) || row.Last > current) result[row.CaseId] = row.Last;
        }
        return result;
    }
}
