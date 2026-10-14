using DokPortal.Application.SkspClasses;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class SkspClassService : ISkspClassService
{
    private readonly AppDbContext _db;

    public SkspClassService(AppDbContext db) => _db = db;

    private IQueryable<SkspClass> WithDetails() =>
        _db.SkspClasses.Include(c => c.Attendees).ThenInclude(a => a.Candidate).ThenInclude(c => c!.Person);

    public async Task<IReadOnlyList<SkspClassDto>> GetAllAsync(int? year, CancellationToken ct)
    {
        var q = WithDetails().AsNoTracking();
        if (year is { } y) q = q.Where(c => c.Year == y);
        var classes = await q.OrderByDescending(c => c.ClassDate).ToListAsync(ct);
        return classes.OrderByDescending(c => c.ClassDate).ThenBy(c => c.StartTime).Select(ToDto).ToList();
    }

    private static void Validate(SaveSkspClassRequest request)
    {
        if (request.Year is < 1 or > 3) throw new InvalidOperationException("Rok formacji musi być z zakresu 1–3.");
        if (string.IsNullOrWhiteSpace(request.Topic)) throw new InvalidOperationException("Podaj temat zajęć.");
        if (request.EndTime is not null && request.StartTime is null)
            throw new InvalidOperationException("Podaj godzinę rozpoczęcia, skoro jest godzina zakończenia.");
        if (request.StartTime is { } start && request.EndTime is { } end && end <= start)
            throw new InvalidOperationException("Godzina zakończenia musi być późniejsza niż rozpoczęcia.");
    }

    /// <summary>Kandydaci, którzy teraz są w formacji na danym roku.</summary>
    private Task<List<Guid>> ActiveCandidateIdsAsync(int year, CancellationToken ct) =>
        _db.Candidates.Where(c => c.FormationYear == year && !c.IsFormationCompleted && !c.IsFormationStopped)
            .Select(c => c.Id).ToListAsync(ct);

    public async Task<SkspClassDto> CreateAsync(SaveSkspClassRequest request, CancellationToken ct)
    {
        Validate(request);
        var skspClass = new SkspClass
        {
            Id = Guid.NewGuid(),
            Year = request.Year,
            ClassDate = request.ClassDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Topic = request.Topic.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            Attendees = (await ActiveCandidateIdsAsync(request.Year, ct))
                .Select(id => new SkspClassAttendee { Id = Guid.NewGuid(), CandidateId = id })
                .ToList()
        };
        _db.SkspClasses.Add(skspClass);
        await _db.SaveChangesAsync(ct);
        return (await GetAsync(skspClass.Id, ct))!;
    }

    private async Task<SkspClassDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var skspClass = await WithDetails().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        return skspClass is null ? null : ToDto(skspClass);
    }

    public async Task<SkspClassDto?> UpdateAsync(Guid id, SaveSkspClassRequest request, CancellationToken ct)
    {
        Validate(request);
        var skspClass = await _db.SkspClasses.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (skspClass is null) return null;
        if (request.Year != skspClass.Year)
            throw new InvalidOperationException("Nie można zmienić roku zajęć z listą obecności – usuń je i dodaj dla właściwego roku.");

        skspClass.ClassDate = request.ClassDate;
        skspClass.StartTime = request.StartTime;
        skspClass.EndTime = request.EndTime;
        skspClass.Topic = request.Topic.Trim();
        skspClass.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<SkspClassDto?> SetAttendanceAsync(Guid id, IReadOnlyList<SkspAttendanceItem> items, CancellationToken ct)
    {
        var skspClass = await _db.SkspClasses.Include(c => c.Attendees).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (skspClass is null) return null;
        var byCandidate = skspClass.Attendees.ToDictionary(a => a.CandidateId);
        foreach (var item in items)
        {
            if (!byCandidate.TryGetValue(item.CandidateId, out var attendee))
                throw new InvalidOperationException("Tego kandydata nie ma na liście obecności tych zajęć.");
            attendee.IsPresent = item.IsPresent;
        }
        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<SkspClassDto?> RefreshAttendeesAsync(Guid id, CancellationToken ct)
    {
        var skspClass = await _db.SkspClasses.Include(c => c.Attendees).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (skspClass is null) return null;
        var listed = skspClass.Attendees.Select(a => a.CandidateId).ToHashSet();
        foreach (var candidateId in await ActiveCandidateIdsAsync(skspClass.Year, ct))
        {
            if (listed.Contains(candidateId)) continue;
            _db.SkspClassAttendees.Add(new SkspClassAttendee { Id = Guid.NewGuid(), SkspClassId = skspClass.Id, CandidateId = candidateId });
        }
        await _db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct)
    {
        var skspClass = await _db.SkspClasses.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (skspClass is null) return false;
        skspClass.DeletedAtUtc = DateTime.UtcNow;
        skspClass.DeletedBy = deletedBy;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static SkspClassDto ToDto(SkspClass c) => new()
    {
        Id = c.Id,
        Year = c.Year,
        ClassDate = c.ClassDate,
        StartTime = c.StartTime,
        EndTime = c.EndTime,
        Topic = c.Topic,
        Notes = c.Notes,
        Attendees = c.Attendees
            .Select(a => new SkspClassAttendeeDto { CandidateId = a.CandidateId, PersonFullName = a.Candidate?.Person?.FullName ?? string.Empty, IsPresent = a.IsPresent })
            .OrderBy(a => a.PersonFullName)
            .ToList()
    };
}
