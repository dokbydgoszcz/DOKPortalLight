using DokPortal.Application.FormSubmissions;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Zamiast pobierać z Tally oddaje stałą treść (JPG z nagłówkiem FF D8 FF).</summary>
public sealed class FakeFormFileFetcher : IFormFileFetcher
{
    public static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4 };

    public Task<DownloadedFile> FetchAsync(FormSubmissionFile file, CancellationToken ct) =>
        Task.FromResult(new DownloadedFile(Jpeg, file.MimeType ?? "image/jpeg", file.Name));
}
