using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DokPortal.Application.Common;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Identity;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Documents;

/// <summary>Wspólne dla podglądu i generowania: papier firmowy, wypełnianie pól, PDF/Word, nazwa pliku.</summary>
public sealed class LetterRenderer
{
    public const string Pdf = "pdf";
    public const string Docx = "docx";
    public const string PdfType = "application/pdf";
    public const string DocxType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private readonly AppDbContext _db;
    private readonly IFileStorageService? _storage;

    public LetterRenderer(AppDbContext db, IFileStorageService? storage)
    {
        _db = db;
        _storage = storage;
    }

    public static string NormalizeFormat(string? format) =>
        string.Equals(format, Docx, StringComparison.OrdinalIgnoreCase) || string.Equals(format, "word", StringComparison.OrdinalIgnoreCase) ? Docx : Pdf;

    public async Task<Letterhead> LetterheadAsync(CancellationToken ct) =>
        await _db.Letterheads.AsNoTracking().FirstOrDefaultAsync(l => l.Id == Letterhead.SingletonId, ct) ?? new Letterhead();

    public async Task<LetterheadContent?> LetterheadContentAsync(bool use, CancellationToken ct)
    {
        if (!use) return null;
        var head = await LetterheadAsync(ct);
        byte[]? logo = null;
        if (head.LogoBlobPath is not null && _storage is not null)
        {
            try
            {
                var stored = await _storage.DownloadAsync(head.LogoBlobPath, ct);
                if (stored is not null)
                {
                    await using var content = stored.Content;
                    using var buffer = new MemoryStream();
                    await content.CopyToAsync(buffer, ct);
                    logo = buffer.ToArray();
                }
            }
            catch (Exception)
            {
                logo = null; // pismo bez logo jest lepsze niż żadne
            }
        }
        return new LetterheadContent(head.HeaderText, head.FooterText, logo);
    }

    /// <summary>Kto wystawia pismo: imię i nazwisko powiązanej osoby, a bez niej e-mail konta.</summary>
    public async Task<string> IssuerAsync(string userId, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new { u.Email, u.PersonId }).FirstOrDefaultAsync(ct);
        if (user?.PersonId is { } personId)
        {
            var name = await _db.People.AsNoTracking().Where(p => p.Id == personId).Select(p => p.FirstName + " " + p.LastName).FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(name)) return name;
        }
        return user?.Email ?? "";
    }

    public async Task<byte[]> RenderAsync(string filledBody, bool useLetterhead, string format, CancellationToken ct)
    {
        var blocks = LetterLayout.Parse(filledBody);
        var head = await LetterheadContentAsync(useLetterhead, ct);
        return format == Docx ? LetterDocx.Render(blocks, head) : LetterPdf.Render(blocks, head);
    }

    /// <summary>„Zaświadczenie o chrzcie – Jan Kowalski” → „Zaswiadczenie-o-chrzcie_Kowalski_2026-10-16.pdf”.</summary>
    public static string FileName(string title, string? lastName, DateTime date, string format)
    {
        static string Slug(string text)
        {
            var d = text.Replace('ł', 'l').Replace('Ł', 'L').Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in d)
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
            return Regex.Replace(Regex.Replace(sb.ToString(), @"[^A-Za-z0-9]+", "-"), "^-+|-+$", "");
        }
        var parts = new[] { Slug(title), lastName is null ? "" : Slug(lastName), date.ToString("yyyy-MM-dd") }.Where(p => p.Length > 0);
        var name = string.Join("_", parts);
        return (name.Length > 120 ? name[..120] : name) + "." + format;
    }
}
