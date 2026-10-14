using DokPortal.Application.Common;

namespace DokPortal.Application.People;

/// <summary>Zdjęcie profilowe osoby (JPG, PNG lub WEBP, do 5 MB).</summary>
public interface IPersonPhotoService
{
    public const long MaxBytes = 5 * 1024 * 1024;

    /// <summary>Zapisuje (lub podmienia) zdjęcie; false = brak osoby.</summary>
    /// <exception cref="InvalidOperationException">To nie jest obraz JPG/PNG/WEBP albo plik jest za duży lub pusty.</exception>
    Task<bool> SaveAsync(Guid personId, Stream content, long sizeBytes, CancellationToken ct);
    Task<StoredFile?> GetAsync(Guid personId, CancellationToken ct);
    Task<bool> DeleteAsync(Guid personId, CancellationToken ct);
}
