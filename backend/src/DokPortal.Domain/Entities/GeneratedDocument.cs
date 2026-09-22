using DokPortal.Domain.Enums;

namespace DokPortal.Domain.Entities;

public class GeneratedDocument
{
    public Guid Id { get; set; }
    public DocumentTemplate Template { get; set; }
    public Guid PersonId { get; set; }
    public Person? Person { get; set; }
    public required string GeneratedByUserId { get; set; }
    public string? AdditionalNotes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
