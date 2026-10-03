using DokPortal.Application.Common;
using DokPortal.Application.Documents;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace DokPortal.Infrastructure.Services;

public class DocumentService : IDocumentService
{
    private static readonly IReadOnlyDictionary<DocumentTemplate, string> TemplateTitles = new Dictionary<DocumentTemplate, string>
    {
        [DocumentTemplate.LetterToBishop] = "Pismo do Biskupa",
        [DocumentTemplate.ConversionConsent] = "Zgoda na konwersję",
        [DocumentTemplate.CanonicalMissionDecree] = "Dekret misji kanonicznej",
        [DocumentTemplate.DokReferral] = "Skierowanie do DOK",
        [DocumentTemplate.SkspCompletionCertificate] = "Zaświadczenie ukończenia SKŚP",
        [DocumentTemplate.SacramentCertificate] = "Zaświadczenie o sakramencie"
    };

    private readonly AppDbContext _db;
    private readonly IFileStorageService? _storage;

    static DocumentService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public DocumentService(AppDbContext db, IFileStorageService? storage = null)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<GeneratedDocumentResult?> GenerateAsync(GenerateDocumentRequest request, string generatedByUserId, CancellationToken ct)
    {
        var person = await _db.People.Include(p => p.Parish).AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PersonId, ct);
        if (person is null) return null;

        var createdAt = DateTime.UtcNow;
        var pdfBytes = RenderPdf(TemplateTitles[request.Template], person, request.AdditionalNotes, createdAt);

        var history = new GeneratedDocument
        {
            Id = Guid.NewGuid(),
            Template = request.Template,
            PersonId = person.Id,
            GeneratedByUserId = generatedByUserId,
            AdditionalNotes = request.AdditionalNotes,
            CreatedAtUtc = createdAt,
            DownloadCount = 1
        };
        await TryStoreAsync(history, pdfBytes, ct);
        _db.GeneratedDocuments.Add(history);
        await _db.SaveChangesAsync(ct);

        return new GeneratedDocumentResult { PdfBytes = pdfBytes, History = ToDto(history, person.FullName) };
    }

    public async Task<IReadOnlyList<GeneratedDocumentDto>> GetHistoryAsync(CancellationToken ct)
    {
        var docs = await _db.GeneratedDocuments.Include(d => d.Person).AsNoTracking()
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToListAsync(ct);
        return docs.Select(d => ToDto(d, d.Person!.FullName)).ToList();
    }

    public async Task<DocumentDownload?> DownloadAsync(Guid id, CancellationToken ct)
    {
        var history = await _db.GeneratedDocuments.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (history is null) return null;

        var fileName = $"{history.Template}.pdf";
        byte[]? bytes = null;
        if (history.BlobPath is not null && _storage is not null)
        {
            var stored = await _storage.DownloadAsync(history.BlobPath, ct);
            if (stored is not null)
            {
                await using var content = stored.Content;
                using var buffer = new MemoryStream();
                await content.CopyToAsync(buffer, ct);
                bytes = buffer.ToArray();
            }
        }

        var restored = bytes is null;
        if (restored)
        {
            var person = await _db.People.Include(p => p.Parish).AsNoTracking().FirstOrDefaultAsync(p => p.Id == history.PersonId, ct)
                ?? throw new InvalidOperationException("Nie można odtworzyć pisma: osoba została usunięta.");
            bytes = RenderPdf(TemplateTitles[history.Template], person, history.AdditionalNotes, history.CreatedAtUtc);
        }

        history.DownloadCount++;
        await _db.SaveChangesAsync(ct);
        return new DocumentDownload { PdfBytes = bytes!, FileName = fileName, Restored = restored };
    }

    public async Task<GeneratedDocumentDto?> DeleteAsync(Guid id, CancellationToken ct)
    {
        var history = await _db.GeneratedDocuments.Include(d => d.Person).FirstOrDefaultAsync(d => d.Id == id, ct);
        if (history is null) return null;

        var dto = ToDto(history, history.Person!.FullName);
        _db.GeneratedDocuments.Remove(history);
        await _db.SaveChangesAsync(ct);

        if (history.BlobPath is not null && _storage is not null)
        {
            try
            {
                await _storage.DeleteAsync(history.BlobPath, ct);
            }
            catch (Exception)
            {
                // Wpis już usunięty – osierocony plik nie powinien blokować użytkownika.
            }
        }
        return dto;
    }

    /// <summary>Zapis egzemplarza PDF jest dodatkiem: gdy magazyn zawiedzie, pismo i tak trafia do użytkownika.</summary>
    private async Task TryStoreAsync(GeneratedDocument history, byte[] pdfBytes, CancellationToken ct)
    {
        if (_storage is null) return;

        var blobPath = $"generated-documents/{history.Id}/{history.Template}.pdf";
        try
        {
            using var stream = new MemoryStream(pdfBytes);
            await _storage.UploadAsync(blobPath, stream, "application/pdf", ct);
            history.BlobPath = blobPath;
            history.FileSizeBytes = pdfBytes.Length;
        }
        catch (Exception)
        {
            history.BlobPath = null;
            history.FileSizeBytes = null;
        }
    }

    private static byte[] RenderPdf(string title, Person person, string? additionalNotes, DateTime date)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Header().Text(title).FontSize(20).Bold();
                page.Content().Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Text($"Data: {date:yyyy-MM-dd}");
                    column.Item().Text($"Imię i nazwisko: {person.FullName}");
                    column.Item().Text($"Data urodzenia: {(person.BirthDate.HasValue ? person.BirthDate.Value.ToString("yyyy-MM-dd") : "brak danych")}");
                    column.Item().Text($"Parafia: {person.Parish?.Name ?? "brak danych"}");
                    if (!string.IsNullOrWhiteSpace(additionalNotes))
                    {
                        column.Item().Text($"Uwagi dodatkowe: {additionalNotes}");
                    }
                });
            });
        });
        return document.GeneratePdf();
    }

    private static GeneratedDocumentDto ToDto(GeneratedDocument d, string personFullName) => new()
    {
        Id = d.Id,
        Template = d.Template,
        PersonId = d.PersonId,
        PersonFullName = personFullName,
        GeneratedByUserId = d.GeneratedByUserId,
        AdditionalNotes = d.AdditionalNotes,
        CreatedAtUtc = d.CreatedAtUtc,
        HasStoredFile = d.BlobPath is not null,
        DownloadCount = d.DownloadCount
    };
}
