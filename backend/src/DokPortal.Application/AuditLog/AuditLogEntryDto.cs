using DokPortal.Domain.Enums;

namespace DokPortal.Application.AuditLog;

public class AuditLogEntryDto
{
    public required Guid Id { get; init; }
    public required DateTime TimestampUtc { get; init; }
    public required string UserId { get; init; }
    public required string UserEmail { get; init; }
    public required string Action { get; init; }
    public required string ObjectDescription { get; init; }
    public AuditResult Result { get; init; }
}
