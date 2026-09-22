using DokPortal.Domain.Enums;

namespace DokPortal.Domain.Entities;

public class AuditLogEntry
{
    public Guid Id { get; set; }
    public DateTime TimestampUtc { get; set; }
    public required string UserId { get; set; }
    public required string UserEmail { get; set; }
    public required string Action { get; set; }
    public required string ObjectDescription { get; set; }
    public AuditResult Result { get; set; }
}
