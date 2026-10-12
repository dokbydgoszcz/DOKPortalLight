using DokPortal.Application.PastoralNotes;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class PastoralNoteService : IPastoralNoteService
{
    private readonly AppDbContext _db;

    public PastoralNoteService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<PastoralNoteDto>> GetVisibleForCaseAsync(Guid caseId, string currentUserId, bool isPrivileged, CancellationToken ct)
    {
        var q = _db.PastoralNotes.AsNoTracking().Where(n => n.DokCaseId == caseId);

        if (!isPrivileged)
        {
            q = q.Where(n => n.AuthorUserId == currentUserId);
        }

        var notes = await q.OrderByDescending(n => n.CreatedAtUtc).ToListAsync(ct);
        var authorIds = notes.Select(n => n.AuthorUserId).Distinct().ToList();
        var authorEmails = await _db.Users
            .Where(u => authorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email, ct);

        var attachments = await AttachmentLookup.ForOwnersAsync(_db, AttachmentOwnerType.PastoralNote, notes.Select(n => n.Id).ToList(), ct);

        return notes.Select(n => ToDto(n, authorEmails.GetValueOrDefault(n.AuthorUserId), attachments.GetValueOrDefault(n.Id))).ToList();
    }

    public async Task<PastoralNoteDto> CreateAsync(Guid caseId, string authorUserId, CreatePastoralNoteRequest request, CancellationToken ct)
    {
        var note = new PastoralNote
        {
            Id = Guid.NewGuid(), DokCaseId = caseId, AuthorUserId = authorUserId, Content = request.Content, CreatedAtUtc = DateTime.UtcNow
        };
        _db.PastoralNotes.Add(note);
        await _db.SaveChangesAsync(ct);

        var authorEmail = await _db.Users.Where(u => u.Id == authorUserId).Select(u => u.Email).FirstOrDefaultAsync(ct);
        return ToDto(note, authorEmail);
    }

    public async Task<PastoralNoteAccess> GetAccessAsync(Guid caseId, Guid noteId, string currentUserId, bool isPrivileged, CancellationToken ct)
    {
        var authorId = await _db.PastoralNotes.AsNoTracking()
            .Where(n => n.Id == noteId && n.DokCaseId == caseId)
            .Select(n => n.AuthorUserId)
            .FirstOrDefaultAsync(ct);
        if (authorId is null) return PastoralNoteAccess.NotFound;
        return isPrivileged || authorId == currentUserId ? PastoralNoteAccess.Visible : PastoralNoteAccess.Hidden;
    }

    public Task<bool> HasNotesFromOthersAsync(Guid caseId, string currentUserId, CancellationToken ct) =>
        _db.PastoralNotes.AnyAsync(n => n.DokCaseId == caseId && n.AuthorUserId != currentUserId, ct);

    public async Task<PastoralNoteDto?> UpdateAsync(Guid caseId, Guid noteId, string currentUserId, CreatePastoralNoteRequest request, CancellationToken ct)
    {
        var note = await _db.PastoralNotes.FirstOrDefaultAsync(n => n.Id == noteId && n.DokCaseId == caseId && n.AuthorUserId == currentUserId, ct);
        if (note is null) return null;
        var content = request.Content?.Trim() ?? "";
        if (content.Length == 0) throw new InvalidOperationException("Treść notatki nie może być pusta.");

        note.Content = content;
        note.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        var authorEmail = await _db.Users.Where(u => u.Id == currentUserId).Select(u => u.Email).FirstOrDefaultAsync(ct);
        var attachments = await AttachmentLookup.ForOwnersAsync(_db, AttachmentOwnerType.PastoralNote, new[] { noteId }, ct);
        return ToDto(note, authorEmail, attachments.GetValueOrDefault(noteId));
    }

    public async Task<bool> DeleteAsync(Guid caseId, Guid noteId, string currentUserId, CancellationToken ct)
    {
        var note = await _db.PastoralNotes.FirstOrDefaultAsync(n => n.Id == noteId && n.DokCaseId == caseId && n.AuthorUserId == currentUserId, ct);
        if (note is null) return false;
        _db.PastoralNotes.Remove(note);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static PastoralNoteDto ToDto(PastoralNote n, string? authorEmail, IReadOnlyList<DokPortal.Application.Attachments.AttachmentDto>? attachments = null) => new()
    {
        Id = n.Id,
        DokCaseId = n.DokCaseId,
        AuthorUserId = n.AuthorUserId,
        AuthorEmail = authorEmail,
        Content = n.Content,
        CreatedAtUtc = n.CreatedAtUtc,
        UpdatedAtUtc = n.UpdatedAtUtc,
        Attachments = attachments ?? Array.Empty<DokPortal.Application.Attachments.AttachmentDto>()
    };
}
