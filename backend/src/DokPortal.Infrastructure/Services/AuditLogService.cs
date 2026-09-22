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

    public async Task<IReadOnlyList<AuditLogEntryDto>> ListAsync(CancellationToken ct)
    {
        var entries = await _db.AuditLogEntries.AsNoTracking()
            .OrderByDescending(e => e.TimestampUtc)
            .ToListAsync(ct);
        return entries.Select(ToDto).ToList();
    }

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
