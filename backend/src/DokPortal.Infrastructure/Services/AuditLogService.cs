using DokPortal.Application.AuditLog;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class AuditLogService : IAuditLogService
{
    private readonly AppDbContext _db;

    public AuditLogService(AppDbContext db) => _db = db;

    public async Task LogAsync(string userId, string userEmail, string action, string objectDescription, AuditResult result, CancellationToken ct)
    {
        _db.AuditLogEntries.Add(new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            TimestampUtc = DateTime.UtcNow,
            UserId = userId,
            UserEmail = userEmail,
            Action = action,
            ObjectDescription = objectDescription,
            Result = result
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLogEntryDto>> ListAsync(AuditLogFilter filter, CancellationToken ct)
    {
        var q = _db.AuditLogEntries.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLower();
            q = q.Where(e => e.UserEmail.ToLower().Contains(term)
                || e.Action.ToLower().Contains(term)
                || e.ObjectDescription.ToLower().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            q = q.Where(e => e.Action == filter.Action);
        }
        if (filter.Result is { } result)
        {
            q = q.Where(e => e.Result == result);
        }
        if (filter.From is { } from)
        {
            var start = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            q = q.Where(e => e.TimestampUtc >= start);
        }
        if (filter.To is { } to)
        {
            var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            q = q.Where(e => e.TimestampUtc < end);
        }

        var take = Math.Clamp(filter.Take, 1, AuditLogFilter.MaxTake);
        var entries = await q.OrderByDescending(e => e.TimestampUtc).Take(take).ToListAsync(ct);
        return entries.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<string>> ListActionsAsync(CancellationToken ct) =>
        await _db.AuditLogEntries.AsNoTracking().Select(e => e.Action).Distinct().OrderBy(a => a).ToListAsync(ct);

    private static AuditLogEntryDto ToDto(AuditLogEntry e) => new()
    {
        Id = e.Id,
        TimestampUtc = e.TimestampUtc,
        UserId = e.UserId,
        UserEmail = e.UserEmail,
        Action = e.Action,
        ObjectDescription = e.ObjectDescription,
        Result = e.Result
    };
}
