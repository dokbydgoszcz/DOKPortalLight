using DokPortal.Application.Candidates;
using DokPortal.Application.Common;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class CandidateService : ICandidateService
{
    private readonly AppDbContext _db;

    public CandidateService(AppDbContext db) => _db = db;

    public async Task<PagedResult<CandidateDto>> SearchAsync(int? year, int page, int pageSize, CancellationToken ct)
    {
        var q = _db.Candidates.Include(c => c.Person).ThenInclude(p => p!.Parish).AsNoTracking().AsQueryable();

        if (year.HasValue)
        {
            q = q.Where(c => c.Year == year.Value);
        }

        var total = await q.CountAsync(ct);
        var entities = await q
            .OrderBy(c => c.Year).ThenBy(c => c.Person!.LastName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<CandidateDto>
        {
            Items = entities.Select(ToDto).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CandidateDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var candidate = await _db.Candidates.Include(c => c.Person).ThenInclude(p => p!.Parish).AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        return candidate is null ? null : ToDto(candidate);
    }

    public async Task<CandidateDto> CreateAsync(CreateCandidateRequest request, CancellationToken ct)
    {
        var candidate = new Candidate
        {
            Id = Guid.NewGuid(),
            PersonId = request.PersonId,
            Year = request.Year,
            AttendancePercentage = request.AttendancePercentage,
            OpinionsCollected = request.OpinionsCollected,
            IsRetreatCompleted = request.IsRetreatCompleted,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _db.Candidates.Add(candidate);
        await _db.SaveChangesAsync(ct);
        return (await GetByIdAsync(candidate.Id, ct))!;
    }

    public async Task<CandidateDto?> UpdateAsync(Guid id, UpdateCandidateRequest request, CancellationToken ct)
    {
        var candidate = await _db.Candidates.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (candidate is null) return null;

        candidate.PersonId = request.PersonId;
        candidate.Year = request.Year;
        candidate.AttendancePercentage = request.AttendancePercentage;
        candidate.OpinionsCollected = request.OpinionsCollected;
        candidate.IsRetreatCompleted = request.IsRetreatCompleted;
        candidate.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    private static CandidateDto ToDto(Candidate c) => new()
    {
        Id = c.Id,
        PersonId = c.PersonId,
        PersonFullName = c.Person!.FullName,
        ParishName = c.Person.Parish?.Name,
        Year = c.Year,
        AttendancePercentage = c.AttendancePercentage,
        OpinionsCollected = c.OpinionsCollected,
        OpinionsRequired = c.OpinionsRequired,
        IsRetreatCompleted = c.IsRetreatCompleted
    };
}
