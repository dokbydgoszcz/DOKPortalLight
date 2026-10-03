using DokPortal.Application.Common;
using DokPortal.Domain.Enums;

namespace DokPortal.Application.Attachments;

/// <summary>Pliki dołączone do notatek i superwizji. Sprawdzenie, czy użytkownik może dotykać rekordu-właściciela, należy do wywołującego.</summary>
public interface IAttachmentService
{
    Task<IReadOnlyList<AttachmentDto>> ListAsync(AttachmentOwnerType ownerType, Guid ownerId, CancellationToken ct);

    /// <exception cref="InvalidOperationException">Niedozwolony typ, pusty lub za duży plik.</exception>
    Task<AttachmentDto> AddAsync(
        AttachmentOwnerType ownerType, Guid ownerId, Stream content, string fileName, long sizeBytes, string uploadedByUserId, CancellationToken ct);

    Task<(StoredFile File, string FileName)?> DownloadAsync(AttachmentOwnerType ownerType, Guid ownerId, Guid attachmentId, CancellationToken ct);

    Task<bool> DeleteAsync(AttachmentOwnerType ownerType, Guid ownerId, Guid attachmentId, CancellationToken ct);
}
