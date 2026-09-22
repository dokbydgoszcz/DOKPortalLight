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

    static DocumentService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public DocumentService(AppDbContext db) => _db = db;

    public async Task<GeneratedDocumentResult?> GenerateAsync(GenerateDocumentRequest request, string generatedByUserId, CancellationToken ct)
    {
        var person = await _db.People.Include(p => p.Parish).AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PersonId, ct);
        if (person is null) return null;

        var pdfBytes = RenderPdf(TemplateTitles[request.Template], person, request.AdditionalNotes);

        var history = new GeneratedDocument
        {
            Id = Guid.NewGuid(),
            Template = request.Template,
            PersonId = person.Id,
            GeneratedByUserId = generatedByUserId,
            AdditionalNotes = request.AdditionalNotes,
            CreatedAtUtc = DateTime.UtcNow
        };
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

    private static byte[] RenderPdf(string title, Person person, string? additionalNotes)
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
                    column.Item().Text($"Data: {DateTime.UtcNow:yyyy-MM-dd}");
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
        CreatedAtUtc = d.CreatedAtUtc
    };
}
