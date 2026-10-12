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
            var sharedAuthors = await SharedAuthorsAsync(caseId, currentUserId, ct);
            q = q.Where(n => n.AuthorUserId == currentUserId || sharedAuthors.Contains(n.AuthorUserId));
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
        if (isPrivileged || authorId == currentUserId) return PastoralNoteAccess.Visible;
        return (await SharedAuthorsAsync(caseId, currentUserId, ct)).Contains(authorId) ? PastoralNoteAccess.Visible : PastoralNoteAccess.Hidden;
    }

    /// <summary>Czy w sprawie są notatki, których użytkownik nie widzi (cudze i nieudostępnione mu).</summary>
    public async Task<bool> HasNotesFromOthersAsync(Guid caseId, string currentUserId, CancellationToken ct)
    {
        var sharedAuthors = await SharedAuthorsAsync(caseId, currentUserId, ct);
        return await _db.PastoralNotes.AnyAsync(
            n => n.DokCaseId == caseId && n.AuthorUserId != currentUserId && !sharedAuthors.Contains(n.AuthorUserId), ct);
    }

    /// <summary>Autorzy, którzy udostępnili swoje notatki w tej sprawie osobie powiązanej z kontem użytkownika.</summary>
    private async Task<List<string>> SharedAuthorsAsync(Guid caseId, string currentUserId, CancellationToken ct)
    {
        var personId = await _db.Users.Where(u => u.Id == currentUserId).Select(u => u.PersonId).FirstOrDefaultAsync(ct);
        if (personId is null) return new List<string>();
        return await _db.PastoralNoteShares.AsNoTracking()
            .Where(s => s.DokCaseId == caseId && s.SharedWithPersonId == personId)
            .Select(s => s.AuthorUserId).ToListAsync(ct);
    }

    public async Task<bool> HasOwnNotesAsync(Guid caseId, string currentUserId, CancellationToken ct) =>
        await _db.DokCases.AnyAsync(c => c.Id == caseId, ct)
        && await _db.PastoralNotes.AnyAsync(n => n.DokCaseId == caseId && n.AuthorUserId == currentUserId, ct);

    public async Task<IReadOnlyList<HandedOverCaseDto>> GetHandedOverAsync(string currentUserId, CancellationToken ct)
    {
        var ownPersonId = await _db.Users.Where(u => u.Id == currentUserId).Select(u => u.PersonId).FirstOrDefaultAsync(ct);
        var counts = await _db.PastoralNotes.AsNoTracking()
            .Where(n => n.AuthorUserId == currentUserId)
            .GroupBy(n => n.DokCaseId)
            .Select(g => new { CaseId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var caseIds = counts.Select(c => c.CaseId).ToList();
        var cases = await _db.DokCases.AsNoTracking().Include(c => c.Person).Include(c => c.CatechistPerson)
            .Where(c => caseIds.Contains(c.Id) && c.CatechistPersonId != ownPersonId)
            .ToListAsync(ct);
        var shares = await _db.PastoralNoteShares.AsNoTracking()
            .Where(s => s.AuthorUserId == currentUserId && caseIds.Contains(s.DokCaseId))
            .Select(s => new { s.DokCaseId, s.SharedWithPersonId }).ToListAsync(ct);
        return cases
            .OrderBy(c => c.Person!.LastName).ThenBy(c => c.Person!.FirstName)
            .Select(c => new HandedOverCaseDto
            {
                CaseId = c.Id,
                PersonFullName = c.Person!.FullName,
                CurrentCatechistPersonId = c.CatechistPersonId,
                CurrentCatechistFullName = c.CatechistPerson?.FullName ?? "",
                NotesCount = counts.First(x => x.CaseId == c.Id).Count,
                SharedWithCurrentCatechist = shares.Any(s => s.DokCaseId == c.Id && s.SharedWithPersonId == c.CatechistPersonId)
            }).ToList();
    }

    public async Task<NoteSharingDto> GetSharingAsync(Guid caseId, string currentUserId, CancellationToken ct)
    {
        var dokCase = await _db.DokCases.AsNoTracking().Include(c => c.CatechistPerson).Include(c => c.Person).FirstAsync(c => c.Id == caseId, ct);
        var hasOwnNotes = await _db.PastoralNotes.AnyAsync(n => n.DokCaseId == caseId && n.AuthorUserId == currentUserId, ct);
        var ownPersonId = await _db.Users.Where(u => u.Id == currentUserId).Select(u => u.PersonId).FirstOrDefaultAsync(ct);
        var shares = await _db.PastoralNoteShares.AsNoTracking().Include(s => s.SharedWithPerson)
            .Where(s => s.DokCaseId == caseId && s.AuthorUserId == currentUserId)
            .OrderBy(s => s.SharedAtUtc).ToListAsync(ct);
        return new NoteSharingDto
        {
            CasePersonFullName = dokCase.Person?.FullName ?? "",
            HasOwnNotes = hasOwnNotes,
            CurrentCatechistPersonId = dokCase.CatechistPersonId,
            CurrentCatechistFullName = dokCase.CatechistPerson?.FullName ?? "",
            CanShareWithCurrentCatechist = hasOwnNotes && ownPersonId != dokCase.CatechistPersonId
                && shares.All(s => s.SharedWithPersonId != dokCase.CatechistPersonId),
            SharedWith = shares.Select(s => new NoteShareDto
            {
                PersonId = s.SharedWithPersonId, FullName = s.SharedWithPerson?.FullName ?? "", SharedAtUtc = s.SharedAtUtc
            }).ToList()
        };
    }

    public async Task<NoteSharingDto> ShareWithCurrentCatechistAsync(Guid caseId, string currentUserId, CancellationToken ct)
    {
        var catechistId = await _db.DokCases.Where(c => c.Id == caseId).Select(c => c.CatechistPersonId).FirstAsync(ct);
        if (!await _db.PastoralNotes.AnyAsync(n => n.DokCaseId == caseId && n.AuthorUserId == currentUserId, ct))
            throw new InvalidOperationException("W tej sprawie nie masz notatek do udostępnienia.");
        var ownPersonId = await _db.Users.Where(u => u.Id == currentUserId).Select(u => u.PersonId).FirstOrDefaultAsync(ct);
        if (ownPersonId == catechistId)
            throw new InvalidOperationException("To ty prowadzisz tę sprawę – twoje notatki są dla ciebie widoczne.");

        if (!await _db.PastoralNoteShares.AnyAsync(s => s.DokCaseId == caseId && s.AuthorUserId == currentUserId && s.SharedWithPersonId == catechistId, ct))
        {
            _db.PastoralNoteShares.Add(new PastoralNoteShare
            {
                Id = Guid.NewGuid(), DokCaseId = caseId, AuthorUserId = currentUserId, SharedWithPersonId = catechistId, SharedAtUtc = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }
        return await GetSharingAsync(caseId, currentUserId, ct);
    }

    public async Task<NoteSharingDto> RevokeShareAsync(Guid caseId, string currentUserId, Guid personId, CancellationToken ct)
    {
        var shares = await _db.PastoralNoteShares
            .Where(s => s.DokCaseId == caseId && s.AuthorUserId == currentUserId && s.SharedWithPersonId == personId).ToListAsync(ct);
        _db.PastoralNoteShares.RemoveRange(shares);
        await _db.SaveChangesAsync(ct);
        return await GetSharingAsync(caseId, currentUserId, ct);
    }

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
