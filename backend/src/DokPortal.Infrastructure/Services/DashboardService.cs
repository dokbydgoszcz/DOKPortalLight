using DokPortal.Application.Dashboard;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private const int UpcomingMeetingsWindowDays = 7;

    private readonly AppDbContext _db;

    public DashboardService(AppDbContext db) => _db = db;

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var windowEnd = today.AddDays(UpcomingMeetingsWindowDays);

        var stageCounts = await _db.DokCases
            .GroupBy(c => c.Stage)
            .Select(g => new { Stage = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var dokCasesByStage = Enum.GetValues<DokStage>()
            .Select(stage => new DokStageCountDto
            {
                Stage = stage.ToString(),
                Count = stageCounts.FirstOrDefault(s => s.Stage == stage)?.Count ?? 0
            })
            .ToList();

        var missingDocumentsCasesCount = await (
            from doc in _db.CaseDocuments
            join dokCase in _db.DokCases on doc.DokCaseId equals dokCase.Id
            where !doc.IsProvided
            select doc.DokCaseId
        ).Distinct().CountAsync(ct);

        return new DashboardSummaryDto
        {
            PeopleCount = await _db.People.CountAsync(ct),
            ParishCount = await _db.Parishes.CountAsync(ct),
            DokCasesByStage = dokCasesByStage,
            MissingDocumentsCasesCount = missingDocumentsCasesCount,
            UpcomingMeetingsCount = await _db.Meetings
                .CountAsync(m => m.MeetingDate >= today && m.MeetingDate <= windowEnd, ct),
            ActiveCandidatesCount = await _db.Candidates.CountAsync(ct)
        };
    }
}
