using DokPortal.Application.Attachments;

namespace DokPortal.Application.Resources;

public class ResourceDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public IReadOnlyList<AttachmentDto> Files { get; init; } = Array.Empty<AttachmentDto>();
}
