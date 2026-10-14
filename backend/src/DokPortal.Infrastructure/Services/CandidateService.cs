using DokPortal.Application.Candidates;
using DokPortal.Application.Common;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class CandidateService : ICandidateService
{
    private const int FormationYears = 3;
    private const int MaxAdvanceBatch = 500;

    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public CandidateService(AppDbContext db, TimeProvider? time = null)
    {
        _db = db;
        _time = time ?? TimeProvider.System;
    }

    public async Task<PagedResult<CandidateDto>> SearchAsync(int? year, int page, int pageSize, CancellationToken ct)
    {
        var q = WithDetails().AsNoTracking().AsQueryable();

        if (year.HasValue)
        {
            q = q.Where(c => !c.IsFormationStopped && !c.IsFormationCompleted && c.FormationYear == year.Value);
        }

        var total = await q.CountAsync(ct);
        var entities = await q
            .OrderBy(c => c.IsFormationCompleted).ThenBy(c => c.FormationYear).ThenBy(c => c.Person!.LastName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var stats = await ClassStatsAsync(entities.Select(c => c.Id).ToList(), ct);
        return new PagedResult<CandidateDto>
        {
            Items = entities.Select(c => ToDto(c, stats.GetValueOrDefault(c.Id))).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    private IQueryable<Candidate> WithDetails() =>
        _db.Candidates.Include(c => c.Person).ThenInclude(p => p!.Parish).Include(c => c.Retreats).Include(c => c.Events);

    public async Task<CandidateDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var candidate = await WithDetails().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (candidate is null) return null;
        var stats = await ClassStatsAsync(new[] { id }, ct);
        return ToDto(candidate, stats.GetValueOrDefault(id));
    }

    /// <summary>Obecność z list zajęć SKŚP: (obecny, zaznaczone) na kandydata; zajęcia bez zaznaczonej obecności się nie liczą.</summary>
    private async Task<Dictionary<Guid, (int Attended, int Recorded)>> ClassStatsAsync(IReadOnlyCollection<Guid> candidateIds, CancellationToken ct)
    {
        var rows = await _db.SkspClassAttendees
            .Where(a => candidateIds.Contains(a.CandidateId) && a.IsPresent != null)
            .GroupBy(a => a.CandidateId)
            .Select(g => new { g.Key, Attended = g.Count(a => a.IsPresent == true), Recorded = g.Count() })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.Key, r => (r.Attended, r.Recorded));
    }

    /// <summary>Frekwencja: z list obecności zajęć, a gdy ich nie ma – wpisana ręcznie.</summary>
    private static int? EffectiveAttendance(int? manual, (int Attended, int Recorded) stats) =>
        CandidateCompletion.EffectiveAttendance(manual, stats.Attended, stats.Recorded);

    /// <summary>Ukończenie formacji z brakami wymaga wyraźnego potwierdzenia.</summary>
    private async Task EnsureCanCompleteAsync(Guid? candidateId, CreateCandidateRequest request, CancellationToken ct)
    {
        if (request.ConfirmIncomplete) return;
        var stats = candidateId is { } id ? (await ClassStatsAsync(new[] { id }, ct)).GetValueOrDefault(id) : default;
        var gaps = CandidateCompletion.Gaps(request.Retreats, request.OpinionsCollected, 2, EffectiveAttendance(request.AttendancePercentage, stats));
        if (gaps.Count > 0)
            throw new InvalidOperationException($"Kandydat nie spełnia warunków ukończenia formacji: {CandidateCompletion.Describe(gaps)}. Zaznacz „Potwierdzam ukończenie mimo braków”, jeśli mimo to formacja jest ukończona.");
    }

    /// <summary>Jedna osoba = jedna kandydatura (duplikaty zawyżały liczniki i listę oczekujących na posłanie).</summary>
    private async Task EnsureSingleCandidacyAsync(Guid personId, Guid? selfId, CancellationToken ct)
    {
        if (await _db.Candidates.AnyAsync(c => c.PersonId == personId && c.Id != selfId, ct))
            throw new InvalidOperationException("Ta osoba ma już kandydaturę w SKŚP – edytuj istniejącą.");
    }

    public async Task<CandidateDto> CreateAsync(CreateCandidateRequest request, CancellationToken ct, CandidateActor? actor = null)
    {
        EnsureRequestIsValid(request);
        await EnsureSingleCandidacyAsync(request.PersonId, null, ct);
        if (request.IsFormationCompleted) await EnsureCanCompleteAsync(null, request, ct);
        var now = _time.GetUtcNow().UtcDateTime;
        var candidate = new Candidate
        {
            Id = Guid.NewGuid(),
            PersonId = request.PersonId,
            FormationYear = request.IsFormationCompleted ? FormationYears : request.Year,
            IsFormationCompleted = request.IsFormationCompleted,
            FormationYearSinceUtc = now,
            IsFormationStopped = request.IsFormationStopped,
            FormationStopNote = request.IsFormationStopped ? request.FormationStopNote!.Trim() : null,
            AttendancePercentage = request.AttendancePercentage,
            OpinionsCollected = request.OpinionsCollected,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        Record(candidate, CandidateFormationEventKind.Enrolled, null, candidate.FormationYear, now, actor);
        foreach (var retreat in request.Retreats)
        {
            candidate.Retreats.Add(new CandidateRetreat { Id = Guid.NewGuid(), Year = retreat.Year, IsCompleted = retreat.IsCompleted });
        }
        _db.Candidates.Add(candidate);
        await _db.SaveChangesAsync(ct);
        return (await GetByIdAsync(candidate.Id, ct))!;
    }

    public async Task<CandidateDto?> UpdateAsync(Guid id, UpdateCandidateRequest request, CancellationToken ct, CandidateActor? actor = null)
    {
        EnsureRequestIsValid(request);
        var candidate = await _db.Candidates.Include(c => c.Retreats).Include(c => c.Events).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (candidate is null) return null;
        // Sprawdzamy tylko zmianę osoby, żeby dało się poprawiać dawne duplikaty.
        if (request.PersonId != candidate.PersonId) await EnsureSingleCandidacyAsync(request.PersonId, id, ct);
        if (candidate.IsFormationCompleted && !request.IsFormationCompleted
            && await _db.CanonicalMissions.AnyAsync(m => m.PersonId == candidate.PersonId, ct))
            throw new InvalidOperationException("Tej osobie udzielono już posłania (ma misję) – nie można cofnąć ukończenia formacji.");
        if (request.IsFormationCompleted && !candidate.IsFormationCompleted) await EnsureCanCompleteAsync(id, request, ct);

        candidate.PersonId = request.PersonId;
        var newYear = request.IsFormationCompleted ? FormationYears : request.Year;
        if (newYear != candidate.FormationYear || request.IsFormationCompleted != candidate.IsFormationCompleted)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            var becameCompleted = request.IsFormationCompleted && !candidate.IsFormationCompleted;
            Record(candidate, becameCompleted ? CandidateFormationEventKind.Completed : CandidateFormationEventKind.Changed,
                candidate.FormationYear, becameCompleted ? null : newYear, now, actor);
            candidate.FormationYear = newYear;
            candidate.IsFormationCompleted = request.IsFormationCompleted;
            candidate.FormationYearSinceUtc = now;
        }
        if (request.IsFormationStopped != candidate.IsFormationStopped)
        {
            // Zatrzymanie i wznowienie zostają w historii kandydata (z powodem zatrzymania).
            Record(candidate, request.IsFormationStopped ? CandidateFormationEventKind.Stopped : CandidateFormationEventKind.Resumed,
                candidate.FormationYear, candidate.FormationYear, _time.GetUtcNow().UtcDateTime, actor,
                request.IsFormationStopped ? request.FormationStopNote!.Trim() : null);
        }
        candidate.IsFormationStopped = request.IsFormationStopped;
        candidate.FormationStopNote = request.IsFormationStopped ? request.FormationStopNote!.Trim() : null;
        candidate.AttendancePercentage = request.AttendancePercentage;
        candidate.OpinionsCollected = request.OpinionsCollected;
        candidate.UpdatedAtUtc = DateTime.UtcNow;
        ReplaceRetreats(candidate, request.Retreats);

        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<AdvanceCandidatesResultDto> AdvanceAsync(IReadOnlyCollection<Guid> candidateIds, CandidateActor actor, CancellationToken ct, bool confirmIncomplete = false)
    {
        var ids = candidateIds.Distinct().ToList();
        if (ids.Count == 0) throw new InvalidOperationException("Zaznacz co najmniej jednego kandydata.");
        if (ids.Count > MaxAdvanceBatch) throw new InvalidOperationException($"Naraz można przenieść co najwyżej {MaxAdvanceBatch} kandydatów.");

        var candidates = await _db.Candidates.Include(c => c.Person).Include(c => c.Events).Include(c => c.Retreats)
            .Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var stats = await ClassStatsAsync(ids, ct);
        var now = _time.GetUtcNow().UtcDateTime;
        var advanced = 0;
        var completed = 0;
        var skipped = new List<SkippedCandidateDto>();

        foreach (var id in ids)
        {
            if (!candidates.TryGetValue(id, out var candidate))
            {
                skipped.Add(new SkippedCandidateDto { CandidateId = id, Reason = "Nie znaleziono kandydata." });
                continue;
            }
            if (candidate.IsFormationStopped)
            {
                skipped.Add(new SkippedCandidateDto { CandidateId = id, PersonFullName = candidate.Person!.FullName, Reason = "Formacja zatrzymana." });
                continue;
            }
            if (candidate.IsFormationCompleted)
            {
                skipped.Add(new SkippedCandidateDto { CandidateId = id, PersonFullName = candidate.Person!.FullName, Reason = "Już ukończył formację." });
                continue;
            }

            if (candidate.FormationYear < FormationYears)
            {
                Record(candidate, CandidateFormationEventKind.Advanced, candidate.FormationYear, candidate.FormationYear + 1, now, actor);
                candidate.FormationYear++;
                advanced++;
            }
            else
            {
                var gaps = CandidateCompletion.Gaps(
                    candidate.Retreats.Select(r => new CandidateRetreatDto { Year = r.Year, IsCompleted = r.IsCompleted }),
                    candidate.OpinionsCollected, candidate.OpinionsRequired,
                    EffectiveAttendance(candidate.AttendancePercentage, stats.GetValueOrDefault(candidate.Id)));
                if (gaps.Count > 0 && !confirmIncomplete)
                {
                    skipped.Add(new SkippedCandidateDto { CandidateId = id, PersonFullName = candidate.Person!.FullName, Reason = $"Braki do ukończenia: {CandidateCompletion.Describe(gaps)}." });
                    continue;
                }
                Record(candidate, CandidateFormationEventKind.Completed, candidate.FormationYear, null, now, actor);
                candidate.IsFormationCompleted = true;
                completed++;
            }
            candidate.FormationYearSinceUtc = now;
            candidate.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return new AdvanceCandidatesResultDto { Advanced = advanced, Completed = completed, Skipped = skipped };
    }

    /// <summary>Dodaje zdarzenie jawnie przez DbSet: element z kluczem nadanym po stronie klienta, dodany do śledzonej kolekcji, EF uznałby za istniejący.</summary>
    private void Record(Candidate candidate, CandidateFormationEventKind kind, int? from, int? to, DateTime at, CandidateActor? actor, string? note = null)
    {
        _db.CandidateFormationEvents.Add(new CandidateFormationEvent
        {
            Id = Guid.NewGuid(),
            CandidateId = candidate.Id,
            Sequence = candidate.Events.Count == 0 ? 1 : candidate.Events.Max(e => e.Sequence) + 1,
            Kind = kind,
            FromYear = from,
            ToYear = to,
            AtUtc = at,
            PerformedByUserId = actor?.UserId,
            PerformedByEmail = actor?.Email,
            Note = note
        });
    }

    public async Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct)
    {
        var candidate = await _db.Candidates.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (candidate is null) return false;

        candidate.DeletedAtUtc = DateTime.UtcNow;
        candidate.DeletedBy = deletedBy;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Zastępuje rekolekcje kandydata podanym zestawem: zmienia istniejące lata, usuwa brakujące, dodaje nowe.</summary>
    private void ReplaceRetreats(Candidate candidate, IReadOnlyList<CandidateRetreatDto> requested)
    {
        var wantedYears = requested.Select(r => r.Year).ToHashSet();
        foreach (var gone in candidate.Retreats.Where(r => !wantedYears.Contains(r.Year)).ToList())
        {
            _db.CandidateRetreats.Remove(gone);
        }
        foreach (var wanted in requested)
        {
            var existing = candidate.Retreats.FirstOrDefault(r => r.Year == wanted.Year);
            if (existing is null)
            {
                _db.CandidateRetreats.Add(new CandidateRetreat { Id = Guid.NewGuid(), CandidateId = candidate.Id, Year = wanted.Year, IsCompleted = wanted.IsCompleted });
            }
            else
            {
                existing.IsCompleted = wanted.IsCompleted;
            }
        }
    }

    private static void EnsureRequestIsValid(CreateCandidateRequest request)
    {
        if (request.Year is < 1 or > FormationYears)
        {
            throw new InvalidOperationException("Rok formacji musi być z zakresu 1–3.");
        }
        if (request.IsFormationStopped && string.IsNullOrWhiteSpace(request.FormationStopNote))
        {
            throw new InvalidOperationException("Podaj powód zatrzymania formacji.");
        }
        EnsureRetreatsAreValid(request.Retreats);
    }

    private static void EnsureRetreatsAreValid(IReadOnlyList<CandidateRetreatDto> retreats)
    {
        if (retreats.Any(r => r.Year is < 1 or > 3))
        {
            throw new InvalidOperationException("Rok rekolekcji musi być z zakresu 1–3.");
        }
        if (retreats.GroupBy(r => r.Year).Any(g => g.Count() > 1))
        {
            throw new InvalidOperationException("Dla jednego roku można zapisać tylko jedne rekolekcje.");
        }
    }

    private static CandidateDto ToDto(Candidate c, (int Attended, int Recorded) stats)
    {
        var retreats = c.Retreats.OrderBy(r => r.Year).Select(r => new CandidateRetreatDto { Year = r.Year, IsCompleted = r.IsCompleted }).ToList();
        var attendance = EffectiveAttendance(c.AttendancePercentage, stats);
        return new CandidateDto
    {
        Id = c.Id,
        PersonId = c.PersonId,
        PersonFullName = c.Person!.FullName,
        ParishName = c.Person.Parish?.Name,
        Year = c.FormationYear,
        Status = (c.IsFormationStopped ? CandidateFormationStatus.Stopped : c.IsFormationCompleted ? CandidateFormationStatus.Completed : CandidateFormationStatus.InFormation).ToString(),
        IsFormationCompleted = c.IsFormationCompleted,
        YearSinceUtc = c.FormationYearSinceUtc,
        IsFormationStopped = c.IsFormationStopped,
        FormationStopNote = c.FormationStopNote,
        AttendancePercentage = attendance,
        AttendanceFromClasses = stats.Recorded > 0,
        ClassesAttended = stats.Attended,
        ClassesRecorded = stats.Recorded,
        CompletionGaps = CandidateCompletion.Gaps(retreats, c.OpinionsCollected, c.OpinionsRequired, attendance),
        OpinionsCollected = c.OpinionsCollected,
        OpinionsRequired = c.OpinionsRequired,
        Retreats = retreats,
        Events = c.Events.OrderByDescending(e => e.Sequence).Select(e => new CandidateFormationEventDto
        {
            Kind = e.Kind.ToString(), FromYear = e.FromYear, ToYear = e.ToYear, AtUtc = e.AtUtc, PerformedBy = e.PerformedByEmail, Note = e.Note
        }).ToList()
    };
    }
}
