using DokPortal.Application.Supervisions;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class SupervisionService : ISupervisionService
{
    private readonly AppDbContext _db;

    public SupervisionService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<SupervisionDto>> GetAllAsync(Institution? institution, CancellationToken ct)
    {
        var q = _db.Supervisions.AsNoTracking().AsQueryable();
        if (institution.HasValue)
        {
            q = q.Where(s => s.Institution == institution.Value);
        }

        var supervisions = await q.OrderByDescending(s => s.SupervisionDate).ToListAsync(ct);
        return supervisions.Select(ToDto).ToList();
    }

    public async Task<SupervisionDto> CreateAsync(CreateSupervisionRequest request, CancellationToken ct)
    {
        var supervision = new Supervision
        {
            Id = Guid.NewGuid(),
            Institution = request.Institution,
            GroupLabel = request.GroupLabel,
            SupervisionDate = request.SupervisionDate,
            AttendeesCount = request.AttendeesCount,
            ExpectedCount = request.ExpectedCount,
            Topic = request.Topic,
            Conclusion = request.Conclusion,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Supervisions.Add(supervision);
        await _db.SaveChangesAsync(ct);
        return ToDto(supervision);
    }

    private static SupervisionDto ToDto(Supervision s) => new()
    {
        Id = s.Id,
        Institution = s.Institution,
        GroupLabel = s.GroupLabel,
        SupervisionDate = s.SupervisionDate,
        AttendeesCount = s.AttendeesCount,
        ExpectedCount = s.ExpectedCount,
        Topic = s.Topic,
        Conclusion = s.Conclusion
    };
}
