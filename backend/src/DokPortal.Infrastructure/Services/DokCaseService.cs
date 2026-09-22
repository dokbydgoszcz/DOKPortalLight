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

    public DokCaseService(AppDbContext db) => _db = db;

    public async Task<PagedResult<DokCaseDto>> SearchAsync(DokPath? path, int page, int pageSize, CancellationToken ct)
    {
        var q = _db.DokCases
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
            Items = entities.Select(ToDto).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<DokCaseDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var dokCase = await _db.DokCases
            .Include(c => c.Person).ThenInclude(p => p!.Parish)
            .Include(c => c.CatechistPerson)
            .Include(c => c.MentorPerson)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        return dokCase is null ? null : ToDto(dokCase);
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
        return (await GetByIdAsync(dokCase.Id, ct))!;
    }

    public async Task<DokCaseDto?> UpdateAsync(Guid id, UpdateDokCaseRequest request, CancellationToken ct)
    {
        var dokCase = await _db.DokCases.FirstOrDefaultAsync(c => c.Id == id, ct);
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
        return await GetByIdAsync(id, ct);
    }

    private static DokCaseDto ToDto(DokCase c) => new()
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
        CompletedAtUtc = c.CompletedAtUtc
    };
}
