using DokPortal.Application.Common;

namespace DokPortal.Infrastructure.Services;

/// <summary>
/// Used when BlobStorage is not configured (local dev without Azure, or test environments).
/// Fails only when actually used, not at startup, so the rest of the app keeps working.
/// </summary>
public class NullFileStorageService : IFileStorageService
{
    public Task UploadAsync(string blobPath, Stream content, string contentType, CancellationToken ct) =>
        throw new InvalidOperationException("Przechowywanie plików nie jest skonfigurowane (brak BlobStorage:ConnectionString).");

    public Task<StoredFile?> DownloadAsync(string blobPath, CancellationToken ct) =>
        throw new InvalidOperationException("Przechowywanie plików nie jest skonfigurowane (brak BlobStorage:ConnectionString).");

    public Task DeleteAsync(string blobPath, CancellationToken ct) =>
        throw new InvalidOperationException("Przechowywanie plików nie jest skonfigurowane (brak BlobStorage:ConnectionString).");
}
