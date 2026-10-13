using DokPortal.Application.DokCases;
using DokPortal.Application.People;
using DokPortal.Domain.Documents;
using DokPortal.Domain.Formation;
using DokPortal.Infrastructure.Identity;
using DokPortal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class PersonOverviewService : IPersonOverviewService
{
    private readonly AppDbContext _db;
    private readonly IPersonService _people;
    private readonly ICaseScopeProvider _scope;
    private readonly UserManager<AppUser>? _userManager;
    private readonly TimeProvider _time;

    public PersonOverviewService(AppDbContext db, IPersonService people, ICaseScopeProvider? scope = null,
        UserManager<AppUser>? userManager = null, TimeProvider? time = null)
    {
        _db = db;
        _people = people;
        _scope = scope ?? new AllCasesScopeProvider();
        _userManager = userManager;
        _time = time ?? TimeProvider.System;
    }

    public async Task<PersonOverviewDto?> GetAsync(Guid personId, PersonOverviewAccess access, CancellationToken ct)
    {
        var person = await _people.GetByIdAsync(personId, ct);
        if (person is null) return null;
        var today = _time.Today();

        IReadOnlyList<OverviewCaseDto>? cases = null, led = null;
        if (access.DokCases)
        {
            var scope = await _scope.GetAsync(ct);
            var rows = await _db.DokCases.ForScope(scope).AsNoTracking()
                .Include(c => c.Person).Include(c => c.CatechistPerson)
                .Where(c => c.PersonId == personId || c.CatechistPersonId == personId || c.MentorPersonId == personId)
                .OrderByDescending(c => c.CreatedAtUtc)
                .ToListAsync(ct);
            OverviewCaseDto Map(DokPortal.Domain.Entities.DokCase c, string role) => new()
            {
                Id = c.Id, PersonFullName = c.Person!.FullName, Path = DokStages.Label(c.Path), Stage = DokStages.Label(c.Stage),
                CatechistFullName = c.CatechistPerson!.FullName, Role = role, InterruptedOn = c.InterruptedOn, CompletedAtUtc = c.CompletedAtUtc
            };
            cases = rows.Where(c => c.PersonId == personId).Select(c => Map(c, "podopieczny")).ToList();
            led = rows.Where(c => c.PersonId != personId)
                .Select(c => Map(c, c.CatechistPersonId == personId ? "katechista" : "mentor")).ToList();
        }

        OverviewCandidateDto? candidate = null;
        if (access.Candidates)
        {
            var c = await _db.Candidates.AsNoTracking().FirstOrDefaultAsync(x => x.PersonId == personId, ct);
            if (c is not null)
            {
                candidate = new OverviewCandidateDto
                {
                    Id = c.Id, FormationYear = c.FormationYear,
                    Status = c.IsFormationStopped ? "formacja zatrzymana" : c.IsFormationCompleted ? "ukończył formację" : "w formacji",
                    AttendancePercentage = c.AttendancePercentage, OpinionsCollected = c.OpinionsCollected, OpinionsRequired = c.OpinionsRequired
                };
            }
        }

        IReadOnlyList<OverviewMissionDto>? missions = null;
        if (access.Missions)
        {
            missions = (await _db.CanonicalMissions.AsNoTracking().Where(m => m.PersonId == personId)
                    .OrderByDescending(m => m.MissionEndDate).ToListAsync(ct))
                .Select(m => new OverviewMissionDto
                {
                    Id = m.Id, ServicePlace = m.ServicePlace, MissionStartDate = m.MissionStartDate, MissionEndDate = m.MissionEndDate,
                    SupervisionGroup = m.SupervisionGroup,
                    Status = m.MissionEndDate < today ? "wygasła" : m.MissionEndDate <= today.AddDays(30) ? "wygasa" : "ważna"
                }).ToList();
        }

        IReadOnlyList<OverviewAssignmentDto>? assignments = null;
        if (access.ParishNeeds)
        {
            assignments = await _db.ParishNeedAssignments.AsNoTracking().Where(a => a.PersonId == personId)
                .OrderByDescending(a => a.AssignedAtUtc)
                .Select(a => new OverviewAssignmentDto
                {
                    ParishName = a.ParishNeed!.Parish!.Name, NeedDescription = a.ParishNeed.Description, AssignedAtUtc = a.AssignedAtUtc
                }).ToListAsync(ct);
        }

        IReadOnlyList<string>? formator = null;
        if (access.Formators)
        {
            formator = await _db.Formators.AsNoTracking().Where(f => f.PersonId == personId).Select(f => f.Function).ToListAsync(ct);
        }

        IReadOnlyList<OverviewDocumentDto>? documents = null;
        if (access.Documents)
        {
            documents = (await _db.GeneratedDocuments.AsNoTracking().Where(d => d.PersonId == personId)
                    .OrderByDescending(d => d.CreatedAtUtc).ToListAsync(ct))
                .Select(d => new OverviewDocumentDto { Id = d.Id, Title = DocumentTemplates.Title(d.Template), CreatedAtUtc = d.CreatedAtUtc })
                .ToList();
        }

        OverviewSupervisionsDto? supervisions = null;
        if (access.Supervisions)
        {
            var rows = await _db.SupervisionAttendees.AsNoTracking().Where(a => a.PersonId == personId)
                .Select(a => new { a.IsPresent, a.Supervision!.SupervisionDate }).ToListAsync(ct);
            supervisions = new OverviewSupervisionsDto
            {
                Listed = rows.Count,
                Present = rows.Count(r => r.IsPresent),
                LastPresentOn = rows.Where(r => r.IsPresent).Select(r => (DateOnly?)r.SupervisionDate).Max()
            };
        }

        OverviewAccountDto? account = null;
        if (access.Users)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.PersonId == personId, ct);
            if (user is not null)
            {
                var roles = _userManager is null ? new List<string>() : (await _userManager.GetRolesAsync(user)).OrderBy(r => r).ToList();
                account = new OverviewAccountDto { Email = user.Email ?? user.UserName ?? "", Roles = roles, IsActive = !user.IsDeactivated };
            }
        }

        return new PersonOverviewDto
        {
            Person = person,
            DokCases = cases,
            CasesLed = led,
            Candidate = candidate,
            CandidateVisible = access.Candidates,
            Missions = missions,
            ParishAssignments = assignments,
            FormatorFunctions = formator,
            Documents = documents,
            Supervisions = supervisions,
            Account = account,
            AccountVisible = access.Users
        };
    }
}
