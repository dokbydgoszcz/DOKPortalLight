using DokPortal.Application.Common;
using DokPortal.Application.Documents;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Documents;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class LetterTemplateService : ILetterTemplateService
{
    public const long MaxLogoBytes = 2 * 1024 * 1024;
    private const string LogoPath = "letterhead/logo";

    private readonly AppDbContext _db;
    private readonly IFileStorageService? _storage;
    private readonly TimeProvider _time;
    private readonly LetterRenderer _renderer;

    public LetterTemplateService(AppDbContext db, IFileStorageService? storage = null, TimeProvider? time = null)
    {
        _db = db;
        _storage = storage;
        _time = time ?? TimeProvider.System;
        _renderer = new LetterRenderer(db, storage);
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    private Task EnsureAsync(CancellationToken ct) => LetterStarterTemplates.EnsureAsync(_db, Now, ct);

    public async Task<IReadOnlyList<LetterTemplateDto>> ListAsync(bool includeInactive, CancellationToken ct)
    {
        await EnsureAsync(ct);
        var templates = await _db.LetterTemplates.AsNoTracking().Where(t => includeInactive || t.IsActive)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Name).ToListAsync(ct);
        var usage = await _db.GeneratedDocuments.Where(d => d.LetterTemplateId != null)
            .GroupBy(d => d.LetterTemplateId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, ct);
        return templates.Select(t => ToDto(t, usage.GetValueOrDefault(t.Id))).ToList();
    }

    public async Task<LetterTemplateDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var template = await _db.LetterTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return null;
        return ToDto(template, await _db.GeneratedDocuments.CountAsync(d => d.LetterTemplateId == id, ct));
    }

    private static void Validate(SaveLetterTemplateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new InvalidOperationException("Podaj nazwę wzoru.");
        if (request.Name.Trim().Length > 200) throw new InvalidOperationException("Nazwa wzoru może mieć najwyżej 200 znaków.");
        if (string.IsNullOrWhiteSpace(request.Body) || LetterLayout.Parse(request.Body).All(b => b.Runs.All(r => r.Text.Trim().Length == 0) && b.Kind != LetterBlockKind.Rule))
            throw new InvalidOperationException("Wzór nie ma treści.");
        if (request.Body.Length > 200_000) throw new InvalidOperationException("Treść wzoru jest za długa.");
    }

    public async Task<LetterTemplateDto> CreateAsync(SaveLetterTemplateRequest request, string? userEmail, CancellationToken ct)
    {
        Validate(request);
        await EnsureAsync(ct);
        var order = await _db.LetterTemplates.Select(t => (int?)t.SortOrder).MaxAsync(ct) ?? -1;
        var template = new LetterTemplate
        {
            Id = Guid.NewGuid(), Name = request.Name.Trim(), Body = request.Body, UseLetterhead = request.UseLetterhead, IsActive = request.IsActive,
            SortOrder = order + 1, CreatedAtUtc = Now, UpdatedAtUtc = Now, UpdatedByEmail = userEmail
        };
        _db.LetterTemplates.Add(template);
        await _db.SaveChangesAsync(ct);
        return ToDto(template, 0);
    }

    public async Task<LetterTemplateDto?> UpdateAsync(Guid id, SaveLetterTemplateRequest request, string? userEmail, CancellationToken ct)
    {
        Validate(request);
        var template = await _db.LetterTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return null;
        template.Name = request.Name.Trim();
        template.Body = request.Body;
        template.UseLetterhead = request.UseLetterhead;
        template.IsActive = request.IsActive;
        template.UpdatedAtUtc = Now;
        template.UpdatedByEmail = userEmail;
        await _db.SaveChangesAsync(ct);
        return ToDto(template, await _db.GeneratedDocuments.CountAsync(d => d.LetterTemplateId == id, ct));
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var template = await _db.LetterTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return false;
        var used = await _db.GeneratedDocuments.CountAsync(d => d.LetterTemplateId == id, ct);
        if (used > 0)
            throw new InvalidOperationException($"Z tego wzoru wygenerowano już {used} pism – nie można go usunąć, ale możesz go wyłączyć (odznacz „Aktywny”).");
        _db.LetterTemplates.Remove(template);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public IReadOnlyList<LetterFieldDto> Fields() =>
        LetterFields.Catalog.Select(f => new LetterFieldDto { Key = f.Key, Label = f.Label, Group = f.Group, Example = f.Example }).ToList();

    private async Task<Letterhead> TrackedLetterheadAsync(CancellationToken ct)
    {
        await EnsureAsync(ct);
        var head = await _db.Letterheads.FirstOrDefaultAsync(l => l.Id == Letterhead.SingletonId, ct);
        if (head is null)
        {
            head = LetterStarterTemplates.DefaultLetterhead();
            _db.Letterheads.Add(head);
        }
        return head;
    }

    private static LetterheadDto ToDto(Letterhead head) => new()
    {
        HeaderText = head.HeaderText, FooterText = head.FooterText, Place = head.Place, HasLogo = head.LogoBlobPath is not null, UpdatedAtUtc = head.UpdatedAtUtc
    };

    public async Task<LetterheadDto> GetLetterheadAsync(CancellationToken ct) => ToDto(await TrackedLetterheadAsync(ct));

    private static string? Clean(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Replace("\r", "").Trim();
        if (trimmed.Length > max) throw new InvalidOperationException($"Tekst może mieć najwyżej {max} znaków.");
        return trimmed;
    }

    public async Task<LetterheadDto> SaveLetterheadAsync(SaveLetterheadRequest request, CancellationToken ct)
    {
        var head = await TrackedLetterheadAsync(ct);
        head.HeaderText = Clean(request.HeaderText, 1000);
        head.FooterText = Clean(request.FooterText, 1000);
        head.Place = Clean(request.Place, 100);
        head.UpdatedAtUtc = Now;
        await _db.SaveChangesAsync(ct);
        return ToDto(head);
    }

    public async Task<LetterheadDto> SaveLogoAsync(Stream content, string contentType, long length, CancellationToken ct)
    {
        if (_storage is null) throw new InvalidOperationException("Magazyn plików nie jest dostępny – nie można zapisać logo.");
        if (length > MaxLogoBytes) throw new InvalidOperationException("Logo jest za duże – najwyżej 2 MB.");
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var type = bytes.Length > 4 && bytes[0] == 0x89 && bytes[1] == 0x50 ? "image/png"
            : bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 ? "image/jpeg" : null;
        if (type is null) throw new InvalidOperationException("Logo musi być obrazem PNG albo JPG.");
        buffer.Position = 0;
        await _storage.UploadAsync(LogoPath, buffer, type, ct);
        var head = await TrackedLetterheadAsync(ct);
        head.LogoBlobPath = LogoPath;
        head.LogoContentType = type;
        head.UpdatedAtUtc = Now;
        await _db.SaveChangesAsync(ct);
        return ToDto(head);
    }

    public async Task<LetterheadDto> DeleteLogoAsync(CancellationToken ct)
    {
        var head = await TrackedLetterheadAsync(ct);
        if (head.LogoBlobPath is { } path && _storage is not null)
        {
            try { await _storage.DeleteAsync(path, ct); } catch (Exception) { /* plik mógł już zniknąć */ }
        }
        head.LogoBlobPath = null;
        head.LogoContentType = null;
        head.UpdatedAtUtc = Now;
        await _db.SaveChangesAsync(ct);
        return ToDto(head);
    }

    public async Task<(byte[] Bytes, string ContentType)?> GetLogoAsync(CancellationToken ct)
    {
        var head = await _renderer.LetterheadAsync(ct);
        if (head.LogoBlobPath is null || _storage is null) return null;
        var stored = await _storage.DownloadAsync(head.LogoBlobPath, ct);
        if (stored is null) return null;
        await using var content = stored.Content;
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        return (buffer.ToArray(), head.LogoContentType ?? stored.ContentType);
    }

    public async Task<LetterFile> PreviewAsync(PreviewLetterRequest request, string userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Body)) throw new InvalidOperationException("Wzór nie ma treści.");
        var format = LetterRenderer.NormalizeFormat(request.Format);
        var head = await _renderer.LetterheadAsync(ct);
        var issuer = await _renderer.IssuerAsync(userId, ct);
        Dictionary<string, string?> values;
        string? lastName = null;
        if (request.PersonId is { } personId)
        {
            try
            {
                values = await LetterFields.ResolveAsync(_db, personId, head.Place ?? "", issuer, Now, ct);
                lastName = values.GetValueOrDefault("osoba.nazwisko");
            }
            catch (KeyNotFoundException)
            {
                throw new InvalidOperationException("Nie znaleziono wybranej osoby.");
            }
        }
        else values = LetterFields.Examples(head.Place ?? "Bydgoszcz", issuer, Now);

        var inputs = request.Inputs ?? new Dictionary<string, string>();
        if (request.PersonId is null)
            foreach (var input in LetterFields.Inputs(request.Body).Where(i => !inputs.ContainsKey(i)))
                inputs[input] = $"[{input}]";
        var missing = new List<string>();
        var filled = LetterFields.Fill(request.Body, values, inputs, missing);
        var bytes = await _renderer.RenderAsync(filled, request.UseLetterhead, format, ct);
        return new LetterFile
        {
            Bytes = bytes, ContentType = format == LetterRenderer.Docx ? LetterRenderer.DocxType : LetterRenderer.PdfType,
            FileName = LetterRenderer.FileName("podglad", lastName, Now, format), MissingFields = missing
        };
    }

    private static LetterTemplateDto ToDto(LetterTemplate t, int usage) => new()
    {
        Id = t.Id, Name = t.Name, Body = t.Body, UseLetterhead = t.UseLetterhead, IsActive = t.IsActive, SortOrder = t.SortOrder,
        UpdatedAtUtc = t.UpdatedAtUtc, UpdatedByEmail = t.UpdatedByEmail,
        Inputs = LetterFields.Inputs(t.Body), UnknownFields = LetterFields.Unknown(t.Body), UsageCount = usage
    };
}
