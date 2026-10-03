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
        var q = _db.Candidates.Include(c => c.Person).ThenInclude(p => p!.Parish).Include(c => c.Retreats).AsNoTracking().AsQueryable();

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
        var candidate = await _db.Candidates.Include(c => c.Person).ThenInclude(p => p!.Parish).Include(c => c.Retreats).AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        return candidate is null ? null : ToDto(candidate);
    }

    public async Task<CandidateDto> CreateAsync(CreateCandidateRequest request, CancellationToken ct)
    {
        EnsureRetreatsAreValid(request.Retreats);
        var candidate = new Candidate
        {
            Id = Guid.NewGuid(),
            PersonId = request.PersonId,
            Year = request.Year,
            AttendancePercentage = request.AttendancePercentage,
            OpinionsCollected = request.OpinionsCollected,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        foreach (var retreat in request.Retreats)
        {
            candidate.Retreats.Add(new CandidateRetreat { Id = Guid.NewGuid(), Year = retreat.Year, IsCompleted = retreat.IsCompleted });
        }
        _db.Candidates.Add(candidate);
        await _db.SaveChangesAsync(ct);
        return (await GetByIdAsync(candidate.Id, ct))!;
    }

    public async Task<CandidateDto?> UpdateAsync(Guid id, UpdateCandidateRequest request, CancellationToken ct)
    {
        EnsureRetreatsAreValid(request.Retreats);
        var candidate = await _db.Candidates.Include(c => c.Retreats).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (candidate is null) return null;

        candidate.PersonId = request.PersonId;
        candidate.Year = request.Year;
        candidate.AttendancePercentage = request.AttendancePercentage;
        candidate.OpinionsCollected = request.OpinionsCollected;
        candidate.UpdatedAtUtc = DateTime.UtcNow;
        ReplaceRetreats(candidate, request.Retreats);

        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct)
    {
        var candidate = await _db.Candidates.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (candidate is null) return false;

        candidate.DeletedAtUtc = DateTime.UtcNow;
        candidate.DeletedBy = deletedBy;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Zastępuje rekolekcje kandydata podanym zestawem: zmienia istniejące lata, usuwa brakujące, dodaje nowe.</summary>
    private void ReplaceRetreats(Candidate candidate, IReadOnlyList<CandidateRetreatDto> requested)
    {
        var wantedYears = requested.Select(r => r.Year).ToHashSet();
        foreach (var gone in candidate.Retreats.Where(r => !wantedYears.Contains(r.Year)).ToList())
        {
            _db.CandidateRetreats.Remove(gone);
        }
        foreach (var wanted in requested)
        {
            var existing = candidate.Retreats.FirstOrDefault(r => r.Year == wanted.Year);
            if (existing is null)
            {
                _db.CandidateRetreats.Add(new CandidateRetreat { Id = Guid.NewGuid(), CandidateId = candidate.Id, Year = wanted.Year, IsCompleted = wanted.IsCompleted });
            }
            else
            {
                existing.IsCompleted = wanted.IsCompleted;
            }
        }
    }

    private static void EnsureRetreatsAreValid(IReadOnlyList<CandidateRetreatDto> retreats)
    {
        if (retreats.Any(r => r.Year is < 1 or > 3))
        {
            throw new InvalidOperationException("Rok rekolekcji musi być z zakresu 1–3.");
        }
        if (retreats.GroupBy(r => r.Year).Any(g => g.Count() > 1))
        {
            throw new InvalidOperationException("Dla jednego roku można zapisać tylko jedne rekolekcje.");
        }
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
        Retreats = c.Retreats.OrderBy(r => r.Year).Select(r => new CandidateRetreatDto { Year = r.Year, IsCompleted = r.IsCompleted }).ToList()
    };
}
