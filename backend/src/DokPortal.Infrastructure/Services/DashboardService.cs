using DokPortal.Application.Dashboard;
using DokPortal.Application.DokCases;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private const int UpcomingMeetingsWindowDays = 7;

    private readonly AppDbContext _db;
    private readonly ICaseScopeProvider _scope;
    private readonly TimeProvider _time;

    public DashboardService(AppDbContext db, ICaseScopeProvider? scope = null, TimeProvider? time = null)
    {
        _db = db;
        _scope = scope ?? new AllCasesScopeProvider();
        _time = time ?? TimeProvider.System;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken ct)
    {
        var today = _time.Today();
        var windowEnd = today.AddDays(UpcomingMeetingsWindowDays);

        var scope = await _scope.GetAsync(ct);
        var cases = _db.DokCases.ForScope(scope);

        var stageCounts = await cases
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
            join dokCase in cases on doc.DokCaseId equals dokCase.Id
            where !doc.IsProvided
            select doc.DokCaseId
        ).Distinct().CountAsync(ct);

        return new DashboardSummaryDto
        {
            PeopleCount = await _db.People.CountAsync(ct),
            ParishCount = await _db.Parishes.CountAsync(ct),
            DokCasesByStage = dokCasesByStage,
            MissingDocumentsCasesCount = missingDocumentsCasesCount,
            UpcomingMeetingsCount = await _db.Meetings.ForScope(_db, scope)
                .CountAsync(m => m.MeetingDate >= today && m.MeetingDate <= windowEnd, ct),
            ActiveCandidatesCount = await _db.Candidates.InFormation(today).CountAsync(ct)
        };
    }
}
