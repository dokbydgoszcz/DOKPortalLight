namespace DokPortal.Domain.Entities;

/// <summary>Pozycja globalnej biblioteki zasobów dla katechistów (nie należy do żadnej sprawy ani superwizji); pliki to załączniki typu Resource.</summary>
public class LibraryResource : ISoftDeletable
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public required string CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
}
