namespace DokPortal.Application.PastoralNotes;

public class PastoralNoteDto
{
    public required Guid Id { get; init; }
    public required Guid DokCaseId { get; init; }
    public required string AuthorUserId { get; init; }
    public required string Content { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}
