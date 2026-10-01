namespace DokPortal.Domain.Entities;

public class Parish : ISoftDeletable
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? City { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
