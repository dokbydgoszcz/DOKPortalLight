namespace DokPortal.Domain.Entities;

public interface ISoftDeletable
{
    DateTime? DeletedAtUtc { get; set; }
    string? DeletedBy { get; set; }
}
