namespace DokPortal.Application.Common;

public record StoredFile(Stream Content, string ContentType);

public interface IFileStorageService
{
    Task UploadAsync(string blobPath, Stream content, string contentType, CancellationToken ct);
    Task<StoredFile?> DownloadAsync(string blobPath, CancellationToken ct);
    Task DeleteAsync(string blobPath, CancellationToken ct);
}
