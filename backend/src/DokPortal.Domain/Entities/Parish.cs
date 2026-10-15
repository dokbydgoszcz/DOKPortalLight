namespace DokPortal.Domain.Entities;

public class Parish : ISoftDeletable
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? City { get; set; }
    /// <summary>Dekanat, np. „Bydgoszcz II – Śródmieście”.</summary>
    public string? Deanery { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
