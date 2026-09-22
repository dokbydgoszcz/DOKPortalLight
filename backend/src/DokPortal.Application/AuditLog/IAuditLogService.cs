using DokPortal.Domain.Enums;

namespace DokPortal.Application.AuditLog;

public interface IAuditLogService
{
    Task LogAsync(string userId, string userEmail, string action, string objectDescription, AuditResult result, CancellationToken ct);
    Task<IReadOnlyList<AuditLogEntryDto>> ListAsync(CancellationToken ct);
}
