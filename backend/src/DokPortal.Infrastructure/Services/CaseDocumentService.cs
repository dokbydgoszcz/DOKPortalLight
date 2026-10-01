using DokPortal.Application.CaseDocuments;
using DokPortal.Application.Common;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class CaseDocumentService : ICaseDocumentService
{
    private readonly AppDbContext _db;
    private readonly IFileStorageService _fileStorageService;

    public CaseDocumentService(AppDbContext db, IFileStorageService fileStorageService)
    {
        _db = db;
        _fileStorageService = fileStorageService;
    }

    public async Task<IReadOnlyList<CaseDocumentDto>> GetForCaseAsync(Guid caseId, CancellationToken ct)
    {
        var documents = await _db.CaseDocuments.AsNoTracking()
            .Where(d => d.DokCaseId == caseId)
            .OrderBy(d => d.CreatedAtUtc)
            .ToListAsync(ct);
        return documents.Select(ToDto).ToList();
    }

    public async Task<CaseDocumentDto> CreateAsync(Guid caseId, CreateCaseDocumentRequest request, CancellationToken ct)
    {
        var document = new CaseDocument
        {
            Id = Guid.NewGuid(), DokCaseId = caseId, Name = request.Name, IsProvided = false, CreatedAtUtc = DateTime.UtcNow
        };
        _db.CaseDocuments.Add(document);
        await _db.SaveChangesAsync(ct);
        return ToDto(document);
    }

    public async Task<CaseDocumentDto?> SetProvidedAsync(Guid caseId, Guid documentId, bool isProvided, CancellationToken ct)
    {
        var document = await _db.CaseDocuments.FirstOrDefaultAsync(d => d.Id == documentId && d.DokCaseId == caseId, ct);
        if (document is null) return null;

        document.IsProvided = isProvided;
        await _db.SaveChangesAsync(ct);
        return ToDto(document);
    }

    public async Task<CaseDocumentDto?> UploadFileAsync(
        Guid caseId, Guid documentId, Stream content, string fileName, string contentType, long fileSizeBytes, CancellationToken ct)
    {
        var document = await _db.CaseDocuments.FirstOrDefaultAsync(d => d.Id == documentId && d.DokCaseId == caseId, ct);
        if (document is null) return null;

        var blobPath = $"case-documents/{caseId}/{documentId}/{fileName}";
        await _fileStorageService.UploadAsync(blobPath, content, contentType, ct);

        document.BlobPath = blobPath;
        document.OriginalFileName = fileName;
        document.ContentType = contentType;
        document.FileSizeBytes = fileSizeBytes;
        document.UploadedAtUtc = DateTime.UtcNow;
        document.IsProvided = true;
        await _db.SaveChangesAsync(ct);

        return ToDto(document);
    }

    public async Task<(StoredFile File, string FileName)?> DownloadFileAsync(Guid caseId, Guid documentId, CancellationToken ct)
    {
        var document = await _db.CaseDocuments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.DokCaseId == caseId, ct);
        if (document?.BlobPath is null || document.OriginalFileName is null) return null;

        var file = await _fileStorageService.DownloadAsync(document.BlobPath, ct);
        return file is null ? null : (file, document.OriginalFileName);
    }

    private static CaseDocumentDto ToDto(CaseDocument d) => new()
    {
        Id = d.Id,
        DokCaseId = d.DokCaseId,
        Name = d.Name,
        IsProvided = d.IsProvided,
        OriginalFileName = d.OriginalFileName,
        FileSizeBytes = d.FileSizeBytes,
        UploadedAtUtc = d.UploadedAtUtc
    };
}
