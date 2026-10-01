using DokPortal.Application.PastoralNotes;
using DokPortal.Domain.Entities;
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

        return notes.Select(n => ToDto(n, authorEmails.GetValueOrDefault(n.AuthorUserId))).ToList();
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

    public Task<bool> HasNotesFromOthersAsync(Guid caseId, string currentUserId, CancellationToken ct) =>
        _db.PastoralNotes.AnyAsync(n => n.DokCaseId == caseId && n.AuthorUserId != currentUserId, ct);

    private static PastoralNoteDto ToDto(PastoralNote n, string? authorEmail) => new()
    {
        Id = n.Id,
        DokCaseId = n.DokCaseId,
        AuthorUserId = n.AuthorUserId,
        AuthorEmail = authorEmail,
        Content = n.Content,
        CreatedAtUtc = n.CreatedAtUtc
    };
}
