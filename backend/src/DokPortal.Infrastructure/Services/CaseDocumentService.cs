using DokPortal.Application.CaseDocuments;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class CaseDocumentService : ICaseDocumentService
{
    private readonly AppDbContext _db;

    public CaseDocumentService(AppDbContext db) => _db = db;

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

    private static CaseDocumentDto ToDto(CaseDocument d) => new()
    {
        Id = d.Id,
        DokCaseId = d.DokCaseId,
        Name = d.Name,
        IsProvided = d.IsProvided
    };
}
