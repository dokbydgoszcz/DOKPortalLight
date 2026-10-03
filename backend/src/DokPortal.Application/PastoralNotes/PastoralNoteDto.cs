using DokPortal.Application.Attachments;

namespace DokPortal.Application.PastoralNotes;

public class PastoralNoteDto
{
    public required Guid Id { get; init; }
    public required Guid DokCaseId { get; init; }
    public required string AuthorUserId { get; init; }
    public string? AuthorEmail { get; init; }
    public required string Content { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public IReadOnlyList<AttachmentDto> Attachments { get; init; } = Array.Empty<AttachmentDto>();
}
