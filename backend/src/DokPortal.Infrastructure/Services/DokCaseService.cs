using DokPortal.Application.Common;
using DokPortal.Application.DokCases;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class DokCaseService : IDokCaseService
{
    private readonly AppDbContext _db;
    private readonly ICaseScopeProvider _scope;

    public DokCaseService(AppDbContext db, ICaseScopeProvider? scope = null)
    {
        _db = db;
        _scope = scope ?? new AllCasesScopeProvider();
    }

    public async Task<PagedResult<DokCaseDto>> SearchAsync(DokPath? path, int page, int pageSize, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var q = _db.DokCases.ForScope(scope)
            .Include(c => c.Person).ThenInclude(p => p!.Parish)
            .Include(c => c.CatechistPerson)
            .Include(c => c.MentorPerson)
            .AsNoTracking().AsQueryable();

        if (path.HasValue)
        {
            q = q.Where(c => c.Path == path.Value);
        }

        var total = await q.CountAsync(ct);
        var entities = await q
            .OrderBy(c => c.Person!.LastName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<DokCaseDto>
        {
            Items = await ToDtosAsync(entities, ct),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<DokCaseDto>> SearchGraduatesAsync(string? query, int page, int pageSize, CancellationToken ct)
    {
        var q = _db.DokCases
            .Include(c => c.Person).ThenInclude(p => p!.Parish)
            .Include(c => c.CatechistPerson)
            .Include(c => c.MentorPerson)
            .AsNoTracking()
            .Where(c => c.Stage == DokStage.Graduate);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLower();
            q = q.Where(c => c.Person!.FirstName.ToLower().Contains(term) || c.Person.LastName.ToLower().Contains(term));
        }

        var total = await q.CountAsync(ct);
        var entities = await q
            .OrderByDescending(c => c.CompletedAtUtc)
            .ThenBy(c => c.Person!.LastName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<DokCaseDto>
        {
            Items = await ToDtosAsync(entities, ct),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<DokCaseDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        return await LoadAsync(_db.DokCases.ForScope(scope), id, ct);
    }

    private async Task<DokCaseDto?> LoadAsync(IQueryable<DokCase> source, Guid id, CancellationToken ct)
    {
        var dokCase = await source
            .Include(c => c.Person).ThenInclude(p => p!.Parish)
            .Include(c => c.CatechistPerson)
            .Include(c => c.MentorPerson)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (dokCase is null) return null;
        return (await ToDtosAsync(new List<DokCase> { dokCase }, ct))[0];
    }

    /// <summary>Mapuje sprawy na DTO, doliczając frekwencję (jedno zapytanie zbiorcze dla całej listy).</summary>
    private async Task<List<DokCaseDto>> ToDtosAsync(IReadOnlyCollection<DokCase> entities, CancellationToken ct)
    {
        var ids = entities.Select(e => e.Id).ToList();
        // Spotkania indywidualne (obecność na samym spotkaniu) oraz zajęcia grupowe (obecność uczestnika).
        var individual = await _db.Meetings.AsNoTracking()
            .Where(m => m.DokCaseId != null && ids.Contains(m.DokCaseId.Value) && m.IsAttended != null)
            .GroupBy(m => m.DokCaseId!.Value)
            .Select(g => new { CaseId = g.Key, Recorded = g.Count(), Attended = g.Count(m => m.IsAttended == true) })
            .ToDictionaryAsync(x => x.CaseId, ct);
        var group = await _db.MeetingAttendees.AsNoTracking()
            .Where(a => ids.Contains(a.DokCaseId) && a.IsAttended != null)
            .GroupBy(a => a.DokCaseId)
            .Select(g => new { CaseId = g.Key, Recorded = g.Count(), Attended = g.Count(a => a.IsAttended == true) })
            .ToDictionaryAsync(x => x.CaseId, ct);

        return entities.Select(e =>
        {
            individual.TryGetValue(e.Id, out var i);
            group.TryGetValue(e.Id, out var g);
            return ToDto(e, (i?.Recorded ?? 0) + (g?.Recorded ?? 0), (i?.Attended ?? 0) + (g?.Attended ?? 0));
        }).ToList();
    }

    public async Task<DokCaseDto> CreateAsync(CreateDokCaseRequest request, CancellationToken ct)
    {
        var dokCase = new DokCase
        {
            Id = Guid.NewGuid(),
            PersonId = request.PersonId,
            Path = request.Path,
            Stage = request.Stage,
            CatechistPersonId = request.CatechistPersonId,
            MentorPersonId = request.MentorPersonId,
            CompletedAtUtc = request.Stage == DokStage.Graduate ? DateTime.UtcNow : null,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _db.DokCases.Add(dokCase);
        await _db.SaveChangesAsync(ct);
        return (await LoadAsync(_db.DokCases, dokCase.Id, ct))!;
    }

    public async Task<DokCaseDto?> UpdateAsync(Guid id, UpdateDokCaseRequest request, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var dokCase = await _db.DokCases.ForScope(scope).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (dokCase is null) return null;

        var isNewlyGraduate = request.Stage == DokStage.Graduate && dokCase.Stage != DokStage.Graduate;

        dokCase.PersonId = request.PersonId;
        dokCase.Path = request.Path;
        dokCase.Stage = request.Stage;
        dokCase.CatechistPersonId = request.CatechistPersonId;
        dokCase.MentorPersonId = request.MentorPersonId;
        dokCase.UpdatedAtUtc = DateTime.UtcNow;
        if (isNewlyGraduate)
        {
            dokCase.CompletedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return await LoadAsync(_db.DokCases, id, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var dokCase = await _db.DokCases.ForScope(scope).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (dokCase is null) return false;

        dokCase.DeletedAtUtc = DateTime.UtcNow;
        dokCase.DeletedBy = deletedBy;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static DokCaseDto ToDto(DokCase c, int meetingsRecorded, int meetingsAttended) => new()
    {
        Id = c.Id,
        PersonId = c.PersonId,
        PersonFullName = c.Person!.FullName,
        ParishName = c.Person.Parish?.Name,
        Path = c.Path,
        Stage = c.Stage,
        CatechistPersonId = c.CatechistPersonId,
        CatechistFullName = c.CatechistPerson!.FullName,
        MentorPersonId = c.MentorPersonId,
        MentorFullName = c.MentorPerson?.FullName,
        LastMeetingDate = c.LastMeetingDate,
        CompletedAtUtc = c.CompletedAtUtc,
        MeetingsRecorded = meetingsRecorded,
        MeetingsAttended = meetingsAttended
    };
}
