using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DokPortal.Application.Common;

namespace DokPortal.Infrastructure.Services;

public class AzureBlobStorageService : IFileStorageService
{
    private readonly BlobContainerClient _container;

    public AzureBlobStorageService(string connectionString, string containerName)
    {
        _container = new BlobContainerClient(connectionString, containerName);
    }

    public async Task UploadAsync(string blobPath, Stream content, string contentType, CancellationToken ct)
    {
        await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);

        var blob = _container.GetBlobClient(blobPath);
        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        }, ct);
    }

    public async Task<StoredFile?> DownloadAsync(string blobPath, CancellationToken ct)
    {
        var blob = _container.GetBlobClient(blobPath);
        if (!await blob.ExistsAsync(ct)) return null;

        var result = await blob.DownloadStreamingAsync(cancellationToken: ct);
        return new StoredFile(result.Value.Content, result.Value.Details.ContentType);
    }

    public async Task DeleteAsync(string blobPath, CancellationToken ct)
    {
        var blob = _container.GetBlobClient(blobPath);
        await blob.DeleteIfExistsAsync(cancellationToken: ct);
    }
}
