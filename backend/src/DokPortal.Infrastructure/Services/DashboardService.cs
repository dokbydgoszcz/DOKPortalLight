using DokPortal.Application.Dashboard;
using DokPortal.Application.DokCases;
using DokPortal.Domain.Enums;
using DokPortal.Domain.Formation;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private const int UpcomingMeetingsWindowDays = 7;
    private const int StalledCasesShown = 20;
    private const int MeetingsShown = 10;
    private const int MissingDocumentsCasesShown = 20;

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

        var dokCasesByStage = DokStages.All
            .Select(stage => new DokStageCountDto
            {
                Stage = stage.ToString(),
                Count = stageCounts.FirstOrDefault(s => s.Stage == stage)?.Count ?? 0
            })
            .ToList();

        // Sprawa na jednym etapie dłużej niż rok (absolwent nie ma już etapu do przejścia).
        var now = _time.GetUtcNow().UtcDateTime;
        var stalledSince = now.AddYears(-1);
        var stalledQuery = cases.Where(c => c.Stage != DokStage.Graduate && c.StageSinceUtc < stalledSince);
        var stalledCount = await stalledQuery.CountAsync(ct);
        var stalledEntities = await stalledQuery.Include(c => c.Person)
            .OrderBy(c => c.StageSinceUtc).ThenBy(c => c.Person!.LastName)
            .Take(StalledCasesShown)
            .AsNoTracking()
            .ToListAsync(ct);
        var stalledCases = stalledEntities.Select(c => new StalledCaseDto
        {
            CaseId = c.Id,
            PersonFullName = c.Person!.FullName,
            Path = c.Path.ToString(),
            Stage = c.Stage.ToString(),
            StageSinceUtc = c.StageSinceUtc,
            MonthsOnStage = FullMonthsBetween(c.StageSinceUtc, now)
        }).ToList();

        var missingRows = await (
            from doc in _db.CaseDocuments
            join dokCase in cases on doc.DokCaseId equals dokCase.Id
            where !doc.IsProvided
            select new { doc.DokCaseId, doc.Name }
        ).ToListAsync(ct);
        var missingByCase = missingRows.GroupBy(r => r.DokCaseId).ToDictionary(g => g.Key, g => g.Select(r => r.Name).OrderBy(n => n).ToList());
        var missingCaseIds = missingByCase.Keys.ToList();
        var missingCases = (await cases.Where(c => missingCaseIds.Contains(c.Id)).Include(c => c.Person).AsNoTracking().ToListAsync(ct))
            .OrderBy(c => c.Person!.LastName).ThenBy(c => c.Person!.FirstName)
            .Take(MissingDocumentsCasesShown)
            .Select(c => new MissingDocumentsCaseDto
            {
                CaseId = c.Id,
                PersonFullName = c.Person!.FullName,
                Path = c.Path.ToString(),
                MissingDocuments = missingByCase[c.Id]
            }).ToList();

        var meetingsInWindow = _db.Meetings.ForScope(_db, scope).Where(m => m.MeetingDate >= today && m.MeetingDate <= windowEnd);
        var meetingEntities = await meetingsInWindow
            .Include(m => m.DokCase).ThenInclude(c => c!.Person)
            .OrderBy(m => m.MeetingDate).ThenBy(m => m.CreatedAtUtc)
            .Take(MeetingsShown)
            .AsNoTracking()
            .ToListAsync(ct);
        var upcomingMeetings = meetingEntities.Select(m => new UpcomingMeetingDto
        {
            MeetingId = m.Id,
            MeetingDate = m.MeetingDate,
            Label = m.DokCase?.Person?.FullName ?? m.GroupLabel ?? "Spotkanie"
        }).ToList();

        return new DashboardSummaryDto
        {
            PeopleCount = await _db.People.CountAsync(ct),
            ParishCount = await _db.Parishes.CountAsync(ct),
            DokCasesByStage = dokCasesByStage,
            MissingDocumentsCasesCount = missingByCase.Count,
            MissingDocumentsCases = missingCases,
            UpcomingMeetingsCount = await meetingsInWindow.CountAsync(ct),
            UpcomingMeetings = upcomingMeetings,
            ActiveCandidatesCount = await _db.Candidates.InFormation().CountAsync(ct),
            StalledCases = stalledCases,
            StalledCasesCount = stalledCount
        };
    }

    private static int FullMonthsBetween(DateTime from, DateTime to)
    {
        var months = (to.Year - from.Year) * 12 + to.Month - from.Month;
        return to.Day < from.Day ? months - 1 : months;
    }
}
