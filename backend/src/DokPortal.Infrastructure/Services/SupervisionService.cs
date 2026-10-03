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
        var attachments = await AttachmentLookup.ForOwnersAsync(_db, AttachmentOwnerType.Supervision, supervisions.Select(s => s.Id).ToList(), ct);
        return supervisions.Select(s => ToDto(s, attachments.GetValueOrDefault(s.Id))).ToList();
    }

    public async Task<SupervisionDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var supervision = await _db.Supervisions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (supervision is null) return null;
        return ToDto(supervision, await LoadAttachmentsAsync(id, ct));
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

    public async Task<SupervisionDto?> UpdateAsync(Guid id, CreateSupervisionRequest request, CancellationToken ct)
    {
        var supervision = await _db.Supervisions.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (supervision is null) return null;

        supervision.Institution = request.Institution;
        supervision.GroupLabel = request.GroupLabel;
        supervision.SupervisionDate = request.SupervisionDate;
        supervision.AttendeesCount = request.AttendeesCount;
        supervision.ExpectedCount = request.ExpectedCount;
        supervision.Topic = request.Topic;
        supervision.Conclusion = request.Conclusion;
        await _db.SaveChangesAsync(ct);

        return ToDto(supervision, await LoadAttachmentsAsync(id, ct));
    }

    public async Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct)
    {
        var supervision = await _db.Supervisions.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (supervision is null) return false;

        supervision.DeletedAtUtc = DateTime.UtcNow;
        supervision.DeletedBy = deletedBy;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<IReadOnlyList<DokPortal.Application.Attachments.AttachmentDto>> LoadAttachmentsAsync(Guid id, CancellationToken ct) =>
        (await AttachmentLookup.ForOwnersAsync(_db, AttachmentOwnerType.Supervision, new[] { id }, ct)).GetValueOrDefault(id)
        ?? new List<DokPortal.Application.Attachments.AttachmentDto>();

    private static SupervisionDto ToDto(Supervision s, IReadOnlyList<DokPortal.Application.Attachments.AttachmentDto>? attachments = null) => new()
    {
        Id = s.Id,
        Institution = s.Institution,
        GroupLabel = s.GroupLabel,
        SupervisionDate = s.SupervisionDate,
        AttendeesCount = s.AttendeesCount,
        ExpectedCount = s.ExpectedCount,
        Topic = s.Topic,
        Conclusion = s.Conclusion,
        Attachments = attachments ?? Array.Empty<DokPortal.Application.Attachments.AttachmentDto>()
    };
}
