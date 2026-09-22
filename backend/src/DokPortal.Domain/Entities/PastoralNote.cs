namespace DokPortal.Domain.Entities;

public class PastoralNote
{
    public Guid Id { get; set; }
    public Guid DokCaseId { get; set; }
    public DokCase? DokCase { get; set; }
    public required string AuthorUserId { get; set; }
    public required string Content { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
