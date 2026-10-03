using DokPortal.Application.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Same as the standard factory, but with an in-memory file store instead of the "not configured" one.</summary>
public class FakeStorageFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IFileStorageService)).ToList())
            {
                services.Remove(descriptor);
            }
            services.AddSingleton<IFileStorageService, InMemoryFileStorage>();
        });
    }

    private class InMemoryFileStorage : IFileStorageService
    {
        private readonly Dictionary<string, (byte[] Content, string ContentType)> _files = new();

        public async Task UploadAsync(string blobPath, Stream content, string contentType, CancellationToken ct)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct);
            _files[blobPath] = (buffer.ToArray(), contentType);
        }

        public Task<StoredFile?> DownloadAsync(string blobPath, CancellationToken ct) =>
            Task.FromResult<StoredFile?>(_files.TryGetValue(blobPath, out var file)
                ? new StoredFile(new MemoryStream(file.Content), file.ContentType)
                : null);

        public Task DeleteAsync(string blobPath, CancellationToken ct)
        {
            _files.Remove(blobPath);
            return Task.CompletedTask;
        }
    }
}
