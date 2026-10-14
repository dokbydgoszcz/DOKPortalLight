using DokPortal.Application.Common;
using DokPortal.Application.DokCases;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Domain.Formation;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DokPortal.Infrastructure.Services;

public class DokCaseService : IDokCaseService
{
    private readonly AppDbContext _db;
    private readonly ICaseScopeProvider _scope;

    private readonly TimeProvider _time;
    private readonly IEmailSender? _emailSender;
    private readonly ILogger<DokCaseService>? _logger;

    public DokCaseService(AppDbContext db, ICaseScopeProvider? scope = null, TimeProvider? time = null,
        IEmailSender? emailSender = null, ILogger<DokCaseService>? logger = null)
    {
        _db = db;
        _scope = scope ?? new AllCasesScopeProvider();
        _time = time ?? TimeProvider.System;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task<PagedResult<DokCaseDto>> SearchAsync(DokPath? path, int page, int pageSize, CancellationToken ct, DokStage? stage = null, DokCaseStatusFilter? status = null)
    {
        var scope = await _scope.GetAsync(ct);
        var q = _db.DokCases.ForScope(scope)
            .Include(c => c.Person).ThenInclude(p => p!.Parish)
            .Include(c => c.CatechistPerson)
            .Include(c => c.MentorPerson)
            .AsNoTracking().AsQueryable();

        if (path.HasValue)
        {
            q = q.Where(c => c.Path == path.Value);
        }

        if (stage.HasValue)
        {
            q = q.Where(c => c.Stage == stage.Value);
        }

        if (status == DokCaseStatusFilter.Active) q = q.Where(c => c.InterruptedOn == null);
        if (status == DokCaseStatusFilter.Interrupted) q = q.Where(c => c.InterruptedOn != null);

        var total = await q.CountAsync(ct);
        var entities = await q
            .OrderBy(c => c.Person!.LastName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<DokCaseDto>
        {
            Items = await ToDtosAsync(entities, ct),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<DokCaseDto>> SearchGraduatesAsync(string? query, int page, int pageSize, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var q = _db.DokCases.ForScope(scope)
            .Include(c => c.Person).ThenInclude(p => p!.Parish)
            .Include(c => c.CatechistPerson)
            .Include(c => c.MentorPerson)
            .AsNoTracking()
            .Where(c => c.Stage == DokStage.Graduate);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLower();
            q = q.Where(c => c.Person!.FirstName.ToLower().Contains(term) || c.Person.LastName.ToLower().Contains(term));
        }

        var total = await q.CountAsync(ct);
        var entities = await q
            .OrderByDescending(c => c.CompletedAtUtc)
            .ThenBy(c => c.Person!.LastName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<DokCaseDto>
        {
            Items = await ToDtosAsync(entities, ct),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<DokCaseDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        return await LoadAsync(_db.DokCases.ForScope(scope), id, ct);
    }

    private async Task<DokCaseDto?> LoadAsync(IQueryable<DokCase> source, Guid id, CancellationToken ct)
    {
        var dokCase = await source
            .Include(c => c.Person).ThenInclude(p => p!.Parish)
            .Include(c => c.CatechistPerson)
            .Include(c => c.MentorPerson)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (dokCase is null) return null;
        return (await ToDtosAsync(new List<DokCase> { dokCase }, ct))[0];
    }

    /// <summary>Mapuje sprawy na DTO, doliczając frekwencję (jedno zapytanie zbiorcze dla całej listy).</summary>
    private async Task<List<DokCaseDto>> ToDtosAsync(IReadOnlyCollection<DokCase> entities, CancellationToken ct)
    {
        var ids = entities.Select(e => e.Id).ToList();
        // Spotkania indywidualne (obecność na samym spotkaniu) oraz zajęcia grupowe (obecność uczestnika).
        var individual = await _db.Meetings.AsNoTracking()
            .Where(m => m.DokCaseId != null && ids.Contains(m.DokCaseId.Value) && m.IsAttended != null)
            .GroupBy(m => m.DokCaseId!.Value)
            .Select(g => new { CaseId = g.Key, Recorded = g.Count(), Attended = g.Count(m => m.IsAttended == true) })
            .ToDictionaryAsync(x => x.CaseId, ct);
        var group = await _db.MeetingAttendees.AsNoTracking()
            .Where(a => ids.Contains(a.DokCaseId) && a.IsAttended != null)
            .GroupBy(a => a.DokCaseId)
            .Select(g => new { CaseId = g.Key, Recorded = g.Count(), Attended = g.Count(a => a.IsAttended == true) })
            .ToDictionaryAsync(x => x.CaseId, ct);

        var lastMeetings = await LastMeetingDates.ForCasesAsync(_db, ids, _time.Today(), ct);

        return entities.Select(e =>
        {
            individual.TryGetValue(e.Id, out var i);
            group.TryGetValue(e.Id, out var g);
            var dto = ToDto(e, (i?.Recorded ?? 0) + (g?.Recorded ?? 0), (i?.Attended ?? 0) + (g?.Attended ?? 0));
            return lastMeetings.TryGetValue(e.Id, out var last) ? WithLastMeeting(dto, last) : dto;
        }).ToList();
    }

    /// <summary>Etap musi należeć do etapów wybranej ścieżki (np. „Wybranie” istnieje tylko w Kandydatach do Chrztu).</summary>
    private static void EnsureStageMatchesPath(DokPath path, DokStage stage)
    {
        if (!DokStages.IsValid(path, stage))
        {
            throw new InvalidOperationException(
                $"Etap „{DokStages.Label(stage)}” nie pasuje do ścieżki „{DokStages.Label(path)}”.");
        }
    }

    /// <summary>Osoby wskazane w sprawie muszą istnieć; podopieczny ma na danej ścieżce tylko jedną trwającą (nie absolwencką) sprawę.</summary>
    private async Task ValidatePeopleAsync(CreateDokCaseRequest request, Guid? selfId, CancellationToken ct)
    {
        if (!await _db.People.AnyAsync(p => p.Id == request.PersonId, ct))
            throw new InvalidOperationException("Nie znaleziono podopiecznego.");
        if (!await _db.People.AnyAsync(p => p.Id == request.CatechistPersonId, ct))
            throw new InvalidOperationException("Nie znaleziono katechisty prowadzącego.");
        if (request.MentorPersonId is { } mentorId && !await _db.People.AnyAsync(p => p.Id == mentorId, ct))
            throw new InvalidOperationException("Nie znaleziono mentora.");
        if (request.Stage != DokStage.Graduate && await HasOtherCaseInProgressAsync(request.PersonId, request.Path, selfId, ct))
            throw new InvalidOperationException("Ta osoba ma już sprawę DOK w toku na tej ścieżce – zmień ją zamiast zakładać nową.");
    }

    /// <summary>Inna trwająca sprawa osoby na tej ścieżce (nie absolwencka i nie przerwana).</summary>
    private Task<bool> HasOtherCaseInProgressAsync(Guid personId, DokPath path, Guid? selfId, CancellationToken ct) =>
        _db.DokCases.AnyAsync(c => c.PersonId == personId && c.Path == path && c.Id != selfId
                                   && c.Stage != DokStage.Graduate && c.InterruptedOn == null, ct);

    public async Task<DokCaseDto> CreateAsync(CreateDokCaseRequest request, CancellationToken ct)
    {
        EnsureStageMatchesPath(request.Path, request.Stage);
        await ValidatePeopleAsync(request, null, ct);
        var dokCase = new DokCase
        {
            Id = Guid.NewGuid(),
            PersonId = request.PersonId,
            Path = request.Path,
            Stage = request.Stage,
            StageSinceUtc = _time.GetUtcNow().UtcDateTime,
            CatechistPersonId = request.CatechistPersonId,
            MentorPersonId = request.MentorPersonId,
            CompletedAtUtc = request.Stage == DokStage.Graduate ? DateTime.UtcNow : null,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _db.DokCases.Add(dokCase);
        await _db.SaveChangesAsync(ct);
        var created = (await LoadAsync(_db.DokCases, dokCase.Id, ct))!;
        await NotifyCaseCreatedAsync(created, ct);
        return created;
    }

    /// <summary>Katechista prowadzący (i mentor) nowej sprawy dostają e-mail. Błąd poczty nie cofa założenia sprawy.</summary>
    private async Task NotifyCaseCreatedAsync(DokCaseDto dokCase, CancellationToken ct)
    {
        if (_emailSender is null) return;
        var what = $"podopiecznego {dokCase.PersonFullName} (ścieżka: {DokStages.Label(dokCase.Path)}, etap: {DokStages.Label(dokCase.Stage)})";
        var recipients = new List<(Guid? PersonId, string Subject, string Body)>
        {
            (dokCase.CatechistPersonId, "Nowa sprawa DOK",
                $"Szczęść Boże,\n\nw DOK Portal założono sprawę {what}, w której jesteś katechistą prowadzącym.\n\nSprawę znajdziesz w DOK Portal → Podopieczni DOK."),
            (dokCase.MentorPersonId, "Nowa sprawa DOK – mentor",
                $"Szczęść Boże,\n\nw DOK Portal założono sprawę {what}, w której jesteś mentorem.")
        };
        foreach (var (personId, subject, body) in recipients)
        {
            if (personId is null) continue;
            var email = await _db.People.Where(p => p.Id == personId).Select(p => p.Email).FirstOrDefaultAsync(ct);
            if (string.IsNullOrWhiteSpace(email)) continue;
            try
            {
                await _emailSender.SendAsync(email, subject, body, ct);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Nie udało się powiadomić o nowej sprawie {DokCaseId}", dokCase.Id);
            }
        }
    }

    public async Task<DokCaseDto?> UpdateAsync(Guid id, UpdateDokCaseRequest request, CancellationToken ct, DokCaseActor? actor = null)
    {
        EnsureStageMatchesPath(request.Path, request.Stage);
        var scope = await _scope.GetAsync(ct);
        var dokCase = await _db.DokCases.ForScope(scope).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (dokCase is null) return null;
        if (request.PersonId != dokCase.PersonId)
            throw new InvalidOperationException("Nie można zmienić podopiecznego w istniejącej sprawie – załóż nową sprawę.");
        if (dokCase.InterruptedOn is not null)
            throw new InvalidOperationException("Sprawa jest przerwana – najpierw ją wznów, potem zmieniaj etap lub katechistę.");
        await ValidatePeopleAsync(request, id, ct);

        var now = _time.GetUtcNow().UtcDateTime;
        var events = new List<DokCaseEvent>();
        void Track(DokCaseEventKind kind, string? from, string? to)
        {
            events.Add(new DokCaseEvent
            {
                Id = Guid.NewGuid(), DokCaseId = id, Kind = kind, FromValue = from, ToValue = to, AtUtc = now,
                PerformedByUserId = actor?.UserId, PerformedByEmail = actor?.Email
            });
        }

        var previousCatechistId = dokCase.CatechistPersonId;
        if (dokCase.Path != request.Path) Track(DokCaseEventKind.PathChanged, DokStages.Label(dokCase.Path), DokStages.Label(request.Path));
        if (dokCase.Stage != request.Stage)
        {
            Track(DokCaseEventKind.StageChanged, DokStages.Label(dokCase.Stage), DokStages.Label(request.Stage));
            dokCase.StageSinceUtc = now;
            dokCase.CompletedAtUtc = request.Stage == DokStage.Graduate ? now : null;
        }
        if (dokCase.CatechistPersonId != request.CatechistPersonId)
            Track(DokCaseEventKind.CatechistChanged, await NameOfAsync(dokCase.CatechistPersonId, ct), await NameOfAsync(request.CatechistPersonId, ct));
        if (dokCase.MentorPersonId != request.MentorPersonId)
            Track(DokCaseEventKind.MentorChanged, await NameOfAsync(dokCase.MentorPersonId, ct), await NameOfAsync(request.MentorPersonId, ct));

        dokCase.Path = request.Path;
        dokCase.Stage = request.Stage;
        dokCase.CatechistPersonId = request.CatechistPersonId;
        dokCase.MentorPersonId = request.MentorPersonId;
        dokCase.UpdatedAtUtc = DateTime.UtcNow;

        var sequence = await _db.DokCaseEvents.Where(e => e.DokCaseId == id).Select(e => (int?)e.Sequence).MaxAsync(ct) ?? 0;
        foreach (var e in events)
        {
            e.Sequence = ++sequence;
            _db.DokCaseEvents.Add(e);
        }

        await _db.SaveChangesAsync(ct);
        var result = await LoadAsync(_db.DokCases, id, ct);
        if (previousCatechistId != request.CatechistPersonId)
        {
            await NotifyNewCatechistAsync(result!, previousCatechistId, ct);
        }
        return result;
    }

    private async Task AddEventAsync(Guid caseId, DokCaseEventKind kind, string? from, string? to, DateTime at, DokCaseActor? actor, CancellationToken ct)
    {
        var sequence = await _db.DokCaseEvents.Where(e => e.DokCaseId == caseId).Select(e => (int?)e.Sequence).MaxAsync(ct) ?? 0;
        _db.DokCaseEvents.Add(new DokCaseEvent
        {
            Id = Guid.NewGuid(), DokCaseId = caseId, Kind = kind, FromValue = from, ToValue = to, AtUtc = at, Sequence = sequence + 1,
            PerformedByUserId = actor?.UserId, PerformedByEmail = actor?.Email
        });
    }

    public async Task<DokCaseDto?> InterruptAsync(Guid id, DateOnly date, string reason, CancellationToken ct, DokCaseActor? actor = null)
    {
        var scope = await _scope.GetAsync(ct);
        var dokCase = await _db.DokCases.ForScope(scope).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (dokCase is null) return null;
        if (dokCase.Stage == DokStage.Graduate)
            throw new InvalidOperationException("Sprawa absolwenta jest zakończona – nie można jej przerwać.");
        if (dokCase.InterruptedOn is not null)
            throw new InvalidOperationException("Sprawa jest już przerwana.");
        if (date > _time.Today())
            throw new InvalidOperationException("Data przerwania nie może być w przyszłości.");

        var now = _time.GetUtcNow().UtcDateTime;
        dokCase.InterruptedOn = date;
        dokCase.InterruptionReason = reason.Trim();
        dokCase.UpdatedAtUtc = now;
        await AddEventAsync(id, DokCaseEventKind.Interrupted, null, $"{date:dd.MM.yyyy}|{dokCase.InterruptionReason}", now, actor, ct);
        await _db.SaveChangesAsync(ct);
        return await LoadAsync(_db.DokCases, id, ct);
    }

    public async Task<DokCaseDto?> ResumeAsync(Guid id, CancellationToken ct, DokCaseActor? actor = null)
    {
        var scope = await _scope.GetAsync(ct);
        var dokCase = await _db.DokCases.ForScope(scope).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (dokCase is null) return null;
        if (dokCase.InterruptedOn is not { } interruptedOn)
            throw new InvalidOperationException("Sprawa nie jest przerwana.");
        if (await HasOtherCaseInProgressAsync(dokCase.PersonId, dokCase.Path, id, ct))
            throw new InvalidOperationException("Podopieczny ma już inną sprawę w toku na tej ścieżce – nie można wznowić tej.");

        // Przerwa nie liczy się do czasu na etapie (karta „Wymaga uwagi”).
        var pausedDays = Math.Max(0, _time.Today().DayNumber - interruptedOn.DayNumber);
        var now = _time.GetUtcNow().UtcDateTime;
        dokCase.StageSinceUtc = dokCase.StageSinceUtc.AddDays(pausedDays);
        dokCase.InterruptedOn = null;
        dokCase.InterruptionReason = null;
        dokCase.UpdatedAtUtc = now;
        await AddEventAsync(id, DokCaseEventKind.Resumed, null, null, now, actor, ct);
        await _db.SaveChangesAsync(ct);
        return await LoadAsync(_db.DokCases, id, ct);
    }

    private async Task<string?> NameOfAsync(Guid? personId, CancellationToken ct) =>
        personId is null ? null : await _db.People.IgnoreQueryFilters().Where(p => p.Id == personId)
            .Select(p => p.FirstName + " " + p.LastName).FirstOrDefaultAsync(ct);

    /// <summary>
    /// Nowy katechista prowadzący dostaje e-mail o przekazaniu sprawy, a poprzedni (jeśli pisał notatki) – informację, że może je udostępnić.
    /// Błąd poczty nie cofa zmiany.
    /// </summary>
    private async Task NotifyNewCatechistAsync(DokCaseDto dokCase, Guid previousCatechistId, CancellationToken ct)
    {
        if (_emailSender is null) return;
        var email = await _db.People.Where(p => p.Id == dokCase.CatechistPersonId).Select(p => p.Email).FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(email))
        {
            var previous = await NameOfAsync(previousCatechistId, ct) ?? "—";
            var body = $"Szczęść Boże,\n\nzostała Ci przekazana sprawa DOK podopiecznego {dokCase.PersonFullName} " +
                       $"(ścieżka: {DokStages.Label(dokCase.Path)}, etap: {DokStages.Label(dokCase.Stage)}).\n" +
                       $"Dotychczas prowadził(a) ją: {previous}.\n\nSprawę znajdziesz w DOK Portal → Podopieczni DOK.";
            try
            {
                await _emailSender.SendAsync(email, "Przekazanie sprawy DOK", body, ct);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Nie udało się powiadomić nowego katechisty o przekazaniu sprawy {DokCaseId}", dokCase.Id);
            }
        }
        await TellPreviousCatechistAboutSharingAsync(dokCase, previousCatechistId, ct);
    }

    /// <summary>
    /// Poprzedni katechista, który pisał notatki w sprawie, dostaje informację, że może je udostępnić następcy
    /// (notatki są poufne – udostępnia tylko autor; bez tego następca ich nie widzi).
    /// </summary>
    private async Task TellPreviousCatechistAboutSharingAsync(DokCaseDto dokCase, Guid previousCatechistId, CancellationToken ct)
    {
        var account = await _db.Users.Where(u => u.PersonId == previousCatechistId && !u.IsDeactivated)
            .Select(u => new { u.Id, u.Email }).FirstOrDefaultAsync(ct);
        if (account is null || string.IsNullOrWhiteSpace(account.Email)) return;
        if (!await _db.PastoralNotes.AnyAsync(n => n.DokCaseId == dokCase.Id && n.AuthorUserId == account.Id, ct)) return;

        var body = $"Szczęść Boże,\n\nsprawę DOK podopiecznego {dokCase.PersonFullName} prowadzi teraz: {dokCase.CatechistFullName}.\n" +
                   "Twoje notatki duszpasterskie z tej sprawy są poufne i następca ich nie widzi. Jeśli chcesz mu je przekazać, " +
                   "otwórz DOK Portal → Pulpit → „Twoje notatki w przekazanych sprawach” i kliknij „Udostępnij”.";
        try
        {
            await _emailSender!.SendAsync(account.Email, "Sprawa DOK przekazana – Twoje notatki", body, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Nie udało się powiadomić poprzedniego katechisty o możliwości udostępnienia notatek {DokCaseId}", dokCase.Id);
        }
    }

    public async Task<IReadOnlyList<DokCaseEventDto>?> GetHistoryAsync(Guid id, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        if (!await _db.DokCases.ForScope(scope).AnyAsync(c => c.Id == id, ct)) return null;

        var events = await _db.DokCaseEvents.AsNoTracking().Where(e => e.DokCaseId == id)
            .OrderByDescending(e => e.AtUtc).ThenByDescending(e => e.Sequence).ToListAsync(ct);
        return events.Select(e => new DokCaseEventDto
        {
            Kind = e.Kind.ToString(),
            Description = Describe(e),
            AtUtc = e.AtUtc,
            PerformedByEmail = e.PerformedByEmail
        }).ToList();
    }

    private static string Describe(DokCaseEvent e)
    {
        if (e.Kind == DokCaseEventKind.Resumed) return "Wznowiono";
        if (e.Kind == DokCaseEventKind.Interrupted)
        {
            var parts = (e.ToValue ?? "").Split('|', 2);
            return parts.Length == 2 ? $"Przerwano ({parts[0]}): {parts[1]}" : $"Przerwano: {e.ToValue}";
        }
        return $"{EventLabel(e.Kind)}: {e.FromValue ?? "—"} → {e.ToValue ?? "—"}";
    }

    private static string EventLabel(DokCaseEventKind kind) => kind switch
    {
        DokCaseEventKind.StageChanged => "Etap",
        DokCaseEventKind.PathChanged => "Ścieżka",
        DokCaseEventKind.CatechistChanged => "Katechista",
        DokCaseEventKind.MentorChanged => "Mentor",
        _ => kind.ToString()
    };

    public async Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct)
    {
        var scope = await _scope.GetAsync(ct);
        var dokCase = await _db.DokCases.ForScope(scope).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (dokCase is null) return false;

        dokCase.DeletedAtUtc = DateTime.UtcNow;
        dokCase.DeletedBy = deletedBy;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static DokCaseDto WithLastMeeting(DokCaseDto d, DateOnly last) => new()
    {
        Id = d.Id, PersonId = d.PersonId, PersonFullName = d.PersonFullName, ParishName = d.ParishName, Path = d.Path, Stage = d.Stage,
        CatechistPersonId = d.CatechistPersonId, CatechistFullName = d.CatechistFullName, MentorPersonId = d.MentorPersonId,
        MentorFullName = d.MentorFullName, LastMeetingDate = last, CompletedAtUtc = d.CompletedAtUtc,
        InterruptedOn = d.InterruptedOn, InterruptionReason = d.InterruptionReason,
        MeetingsRecorded = d.MeetingsRecorded, MeetingsAttended = d.MeetingsAttended
    };

    private static DokCaseDto ToDto(DokCase c, int meetingsRecorded, int meetingsAttended) => new()
    {
        Id = c.Id,
        PersonId = c.PersonId,
        PersonFullName = c.Person!.FullName,
        ParishName = c.Person.Parish?.Name,
        Path = c.Path,
        Stage = c.Stage,
        CatechistPersonId = c.CatechistPersonId,
        CatechistFullName = c.CatechistPerson!.FullName,
        MentorPersonId = c.MentorPersonId,
        MentorFullName = c.MentorPerson?.FullName,
        LastMeetingDate = c.LastMeetingDate,
        CompletedAtUtc = c.CompletedAtUtc,
        InterruptedOn = c.InterruptedOn,
        InterruptionReason = c.InterruptionReason,
        MeetingsRecorded = meetingsRecorded,
        MeetingsAttended = meetingsAttended
    };
}
