using DokPortal.Application.Common;
using DokPortal.Application.Documents;
using DokPortal.Domain.Documents;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Documents;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace DokPortal.Infrastructure.Services;

public class DocumentService : IDocumentService
{
    private readonly AppDbContext _db;
    private readonly IFileStorageService? _storage;
    private readonly TimeProvider _time;
    private readonly LetterRenderer _renderer;

    static DocumentService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public DocumentService(AppDbContext db, IFileStorageService? storage = null, TimeProvider? time = null)
    {
        _db = db;
        _storage = storage;
        _time = time ?? TimeProvider.System;
        _renderer = new LetterRenderer(db, storage);
    }

    public async Task<GeneratedDocumentResult?> GenerateAsync(GenerateDocumentRequest request, string generatedByUserId, CancellationToken ct)
    {
        var template = await FindTemplateAsync(request, ct);
        if (!template.IsActive) throw new InvalidOperationException($"Wzór „{template.Name}” jest wyłączony.");
        var person = await _db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.PersonId, ct);
        if (person is null) return null;

        var format = LetterRenderer.NormalizeFormat(request.Format);
        var createdAt = _time.GetUtcNow().UtcDateTime;
        var head = await _renderer.LetterheadAsync(ct);
        var values = await LetterFields.ResolveAsync(_db, person.Id, head.Place ?? "", await _renderer.IssuerAsync(generatedByUserId, ct), createdAt, ct);
        values["pismo.uwagi"] = string.IsNullOrWhiteSpace(request.AdditionalNotes) ? null : request.AdditionalNotes.Trim();
        var missing = new List<string>();
        var filled = LetterFields.Fill(template.Body, values, request.Inputs, missing);

        // Archiwum zawsze w PDF; plik dla użytkownika – w wybranym formacie.
        var pdf = await _renderer.RenderAsync(filled, template.UseLetterhead, LetterRenderer.Pdf, ct);
        var bytes = format == LetterRenderer.Docx ? await _renderer.RenderAsync(filled, template.UseLetterhead, LetterRenderer.Docx, ct) : pdf;

        var history = new GeneratedDocument
        {
            Id = Guid.NewGuid(),
            Template = template.LegacyType,
            LetterTemplateId = template.Id,
            Title = template.Name,
            RenderedBody = filled,
            UseLetterhead = template.UseLetterhead,
            PersonId = person.Id,
            GeneratedByUserId = generatedByUserId,
            AdditionalNotes = request.AdditionalNotes,
            CreatedAtUtc = createdAt,
            DownloadCount = 1
        };
        await TryStoreAsync(history, pdf, ct);
        _db.GeneratedDocuments.Add(history);
        await _db.SaveChangesAsync(ct);

        return new GeneratedDocumentResult
        {
            File = new LetterFile
            {
                Bytes = bytes, ContentType = format == LetterRenderer.Docx ? LetterRenderer.DocxType : LetterRenderer.PdfType,
                FileName = LetterRenderer.FileName(template.Name, person.LastName, createdAt, format), MissingFields = missing
            },
            History = ToDto(history, person.FullName)
        };
    }

    private async Task<LetterTemplate> FindTemplateAsync(GenerateDocumentRequest request, CancellationToken ct)
    {
        await LetterStarterTemplates.EnsureAsync(_db, _time.GetUtcNow().UtcDateTime, ct);
        LetterTemplate? template = null;
        if (request.LetterTemplateId is { } id)
            template = await _db.LetterTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        else if (request.Template is { } legacy)
            template = await _db.LetterTemplates.AsNoTracking().Where(t => t.LegacyType == legacy).OrderByDescending(t => t.IsActive).FirstOrDefaultAsync(ct);
        return template ?? throw new InvalidOperationException("Nie znaleziono wzoru pisma – wybierz wzór z listy.");
    }

    public async Task<IReadOnlyList<GeneratedDocumentDto>> GetHistoryAsync(CancellationToken ct)
    {
        var docs = await _db.GeneratedDocuments.Include(d => d.Person).AsNoTracking()
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToListAsync(ct);
        return docs.Select(d => ToDto(d, d.Person!.FullName)).ToList();
    }

    public async Task<DocumentDownload?> DownloadAsync(Guid id, CancellationToken ct, string? format = null)
    {
        var history = await _db.GeneratedDocuments.Include(d => d.Person).FirstOrDefaultAsync(d => d.Id == id, ct);
        if (history is null) return null;
        var wanted = LetterRenderer.NormalizeFormat(format);
        var title = TitleOf(history);
        var fileName = LetterRenderer.FileName(title, history.Person?.LastName, history.CreatedAtUtc, wanted);

        byte[]? bytes = null;
        var restored = false;
        if (wanted == LetterRenderer.Pdf && history.BlobPath is not null && _storage is not null)
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

        if (bytes is null && history.RenderedBody is not null)
        {
            // Treść zapisana przy generowaniu – ta sama co w oryginale.
            bytes = await _renderer.RenderAsync(history.RenderedBody, history.UseLetterhead, wanted, ct);
        }
        if (bytes is null)
        {
            if (wanted == LetterRenderer.Docx)
                throw new InvalidOperationException("Wersja Word jest dostępna tylko dla pism wygenerowanych z wzorów w portalu.");
            var person = await _db.People.Include(p => p.Parish).AsNoTracking().FirstOrDefaultAsync(p => p.Id == history.PersonId, ct)
                ?? throw new InvalidOperationException("Nie można odtworzyć pisma: osoba została usunięta.");
            bytes = RenderLegacyPdf(title, person, history.AdditionalNotes, history.CreatedAtUtc);
            restored = true;
        }

        history.DownloadCount++;
        await _db.SaveChangesAsync(ct);
        return new DocumentDownload
        {
            Bytes = bytes, FileName = fileName, Restored = restored,
            ContentType = wanted == LetterRenderer.Docx ? LetterRenderer.DocxType : LetterRenderer.PdfType
        };
    }

    private static string TitleOf(GeneratedDocument d) => d.Title ?? (d.Template is { } t ? DocumentTemplates.Title(t) : "Pismo");

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

        var blobPath = $"generated-documents/{history.Id}/pismo.pdf";
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

    /// <summary>Dawne pisma (sprzed wzorów w portalu) bez zapisanego pliku – odtwarzane w dawnym układzie.</summary>
    private static byte[] RenderLegacyPdf(string title, Person person, string? additionalNotes, DateTime date)
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
                page.Footer().AlignCenter().Text(DocumentTemplates.DraftNotice).FontSize(9).Italic();
            });
        });
        return document.GeneratePdf();
    }

    private static GeneratedDocumentDto ToDto(GeneratedDocument d, string personFullName) => new()
    {
        Id = d.Id,
        Template = d.Template,
        LetterTemplateId = d.LetterTemplateId,
        Title = TitleOf(d),
        CanDownloadWord = d.RenderedBody is not null,
        PersonId = d.PersonId,
        PersonFullName = personFullName,
        GeneratedByUserId = d.GeneratedByUserId,
        AdditionalNotes = d.AdditionalNotes,
        CreatedAtUtc = d.CreatedAtUtc,
        HasStoredFile = d.BlobPath is not null,
        DownloadCount = d.DownloadCount
    };
}
