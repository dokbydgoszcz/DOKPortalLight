using DokPortal.Application.Common;
using DokPortal.Application.ParishNeeds;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DokPortal.Infrastructure.Services;

public class ParishNeedService : IParishNeedService
{
    private readonly AppDbContext _db;
    private readonly IEmailSender? _emailSender;
    private readonly TimeProvider _time;
    private readonly ILogger<ParishNeedService>? _logger;

    public ParishNeedService(AppDbContext db, IEmailSender? emailSender = null, TimeProvider? time = null, ILogger<ParishNeedService>? logger = null)
    {
        _db = db;
        _emailSender = emailSender;
        _time = time ?? TimeProvider.System;
        _logger = logger;
    }

    private IQueryable<ParishNeed> NeedsWithDetails() =>
        _db.ParishNeeds.Include(n => n.Parish).Include(n => n.Assignments).ThenInclude(a => a.Person);

    public async Task<IReadOnlyList<ParishNeedDto>> GetAllAsync(CancellationToken ct)
    {
        var needs = await NeedsWithDetails().AsNoTracking().OrderByDescending(n => n.CreatedAtUtc).ToListAsync(ct);
        return needs.Select(ToDto).ToList();
    }

    public async Task<ParishNeedDto> CreateAsync(CreateParishNeedRequest request, CancellationToken ct)
    {
        var need = new ParishNeed
        {
            Id = Guid.NewGuid(),
            ParishId = request.ParishId,
            Description = request.Description,
            Status = ParishNeedStatus.Open,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.ParishNeeds.Add(need);
        await _db.SaveChangesAsync(ct);

        return ToDto(await NeedsWithDetails().AsNoTracking().FirstAsync(n => n.Id == need.Id, ct));
    }

    public async Task<ParishNeedDto?> UpdateAsync(Guid id, UpdateParishNeedRequest request, CancellationToken ct)
    {
        var need = await _db.ParishNeeds.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (need is null) return null;

        var description = request.Description.Trim();
        if (description.Length == 0) throw new InvalidOperationException("Podaj opis zapotrzebowania.");
        if (!await _db.Parishes.AnyAsync(p => p.Id == request.ParishId, ct)) throw new InvalidOperationException("Nie znaleziono parafii.");
        if (request.ParishId != need.ParishId && await _db.ParishNeedAssignments.AnyAsync(a => a.ParishNeedId == id, ct))
        {
            throw new InvalidOperationException("Do zapotrzebowania są skierowane osoby – najpierw je odepnij, potem zmień parafię.");
        }

        need.ParishId = request.ParishId;
        need.Description = description;
        await _db.SaveChangesAsync(ct);
        return await LoadAsync(id, ct);
    }

    public async Task<ParishNeedDto?> AssignAsync(Guid id, Guid personId, CancellationToken ct)
    {
        var need = await _db.ParishNeeds.Include(n => n.Assignments).FirstOrDefaultAsync(n => n.Id == id, ct);
        if (need is null) return null;
        if (need.Status == ParishNeedStatus.Closed) throw new InvalidOperationException("Zapotrzebowanie jest zamknięte – otwórz je ponownie, żeby kogoś skierować.");
        if (!await _db.People.AnyAsync(p => p.Id == personId, ct)) throw new InvalidOperationException("Nie znaleziono osoby.");
        if (await _db.Candidates.AnyAsync(c => c.PersonId == personId && !c.IsFormationCompleted, ct))
        {
            throw new InvalidOperationException("Ta osoba jest kandydatem w trakcie formacji – skierować można dopiero po jej ukończeniu.");
        }

        var isNew = need.Assignments.All(a => a.PersonId != personId);
        if (isNew)
        {
            var assignment = new ParishNeedAssignment
            {
                Id = Guid.NewGuid(), ParishNeedId = id, PersonId = personId, AssignedAtUtc = DateTime.UtcNow
            };
            await RecordMissionAsync(need, assignment, ct);
            _db.ParishNeedAssignments.Add(assignment);
        }
        if (need.Status == ParishNeedStatus.Open) need.Status = ParishNeedStatus.Assigned;
        await _db.SaveChangesAsync(ct);
        if (isNew) await NotifyAsync(need, personId, ct);
        return await LoadAsync(id, ct);
    }

    /// <summary>
    /// Skierowanie do parafii zakłada rekord misji kanonicznej na rok (osoba zostaje katechistą). Jeśli osoba ma ważną misję
    /// bez miejsca (udzieloną „Udziel posłania”), uzupełniamy w niej parafię; jeśli ma już ważną misję w tej parafii – nic nie dodajemy.
    /// Parafię rozpoznajemy po identyfikatorze (zmiana nazwy parafii nie tworzy duplikatu). Skierowanie zapamiętuje swoją misję,
    /// żeby odpięcie mogło ją cofnąć.
    /// </summary>
    private async Task RecordMissionAsync(ParishNeed need, ParishNeedAssignment assignment, CancellationToken ct)
    {
        var personId = assignment.PersonId;
        var parishName = await _db.Parishes.Where(p => p.Id == need.ParishId).Select(p => p.Name).FirstAsync(ct);
        var today = _time.Today();
        var valid = await _db.CanonicalMissions.Where(m => m.PersonId == personId && m.MissionEndDate >= today).ToListAsync(ct);

        var existing = valid.FirstOrDefault(m => m.ParishId == need.ParishId)
            ?? valid.FirstOrDefault(m => m.ParishId == null && m.ServicePlace == parishName);
        var unplaced = valid.Where(m => string.IsNullOrWhiteSpace(m.ServicePlace)).OrderByDescending(m => m.MissionEndDate).FirstOrDefault();
        if (existing is not null)
        {
            assignment.MissionId = existing.Id;
        }
        else if (unplaced is not null)
        {
            unplaced.ServicePlace = parishName;
            unplaced.ParishId = need.ParishId;
            unplaced.UpdatedAtUtc = DateTime.UtcNow;
            assignment.MissionId = unplaced.Id;
            assignment.MissionPlaceFilledByAssignment = true;
        }
        else
        {
            var mission = new CanonicalMission
            {
                Id = Guid.NewGuid(),
                PersonId = personId,
                ServicePlace = parishName,
                ParishId = need.ParishId,
                MissionStartDate = today,
                MissionEndDate = today.AddYears(1),
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _db.CanonicalMissions.Add(mission);
            assignment.MissionId = mission.Id;
            assignment.MissionCreatedByAssignment = true;
        }

        await CatechistFunction.EnsureAsync(_db, personId, ct);
    }

    /// <summary>Proboszcz i katechista dostają e-mail z danymi kontaktowymi drugiej strony. Błąd poczty nie cofa skierowania.</summary>
    private async Task NotifyAsync(ParishNeed need, Guid personId, CancellationToken ct)
    {
        if (_emailSender is null) return;

        var parish = await _db.Parishes.AsNoTracking().FirstAsync(p => p.Id == need.ParishId, ct);
        var catechist = await _db.People.AsNoTracking().FirstAsync(p => p.Id == personId, ct);
        var pastor = await _db.PersonFunctions.AsNoTracking()
            .Where(f => f.Type == FunctionType.Pastor && f.ParishId == need.ParishId)
            .Select(f => f.Person!)
            .FirstOrDefaultAsync(ct);

        var subject = $"Skierowanie katechisty do parafii {parish.Name}";
        await TrySendAsync(pastor?.Email, subject,
            $"Szczęść Boże,\n\nDo parafii „{parish.Name}” został skierowany katechista: {catechist.FullName}.\n" +
            $"Zapotrzebowanie: {need.Description}\n\nDane kontaktowe katechisty:\n{Contact(catechist)}\n\n" +
            "Prosimy o bezpośredni kontakt z katechistą.", ct);

        var pastorPart = pastor is null
            ? "Parafia nie ma jeszcze przypisanego proboszcza w systemie – dane kontaktowe prześlemy po jego wskazaniu."
            : $"Dane kontaktowe proboszcza ({pastor.FullName}):\n{Contact(pastor)}";
        await TrySendAsync(catechist.Email, subject,
            $"Szczęść Boże,\n\nZostałeś(-aś) skierowany(-a) do parafii „{parish.Name}”.\n" +
            $"Zapotrzebowanie: {need.Description}\n\n{pastorPart}\n\n" +
            "Prosimy o bezpośredni kontakt z parafią.", ct);
    }

    private static string Contact(Person p) =>
        $"- e-mail: {(string.IsNullOrWhiteSpace(p.Email) ? "brak" : p.Email)}\n- telefon: {(string.IsNullOrWhiteSpace(p.Phone) ? "brak" : p.Phone)}";

    private async Task TrySendAsync(string? to, string subject, string body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(to)) return;
        try
        {
            await _emailSender!.SendAsync(to, subject, body, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Nie udało się wysłać powiadomienia o skierowaniu do {Recipient}", to);
        }
    }

    public async Task<ParishNeedDto?> UnassignAsync(Guid id, Guid personId, CancellationToken ct)
    {
        var need = await _db.ParishNeeds.Include(n => n.Assignments).FirstOrDefaultAsync(n => n.Id == id, ct);
        if (need is null) return null;

        var assignment = need.Assignments.FirstOrDefault(a => a.PersonId == personId);
        if (assignment is not null)
        {
            _db.ParishNeedAssignments.Remove(assignment);
            if (need.Assignments.Count == 1 && need.Status == ParishNeedStatus.Assigned) need.Status = ParishNeedStatus.Open;
            await RevertMissionAsync(assignment, ct);
            await _db.SaveChangesAsync(ct);
            await CatechistFunction.RemoveIfNoMissionAsync(_db, personId, ct);
            await _db.SaveChangesAsync(ct);
        }
        return await LoadAsync(id, ct);
    }

    /// <summary>
    /// Cofa skutek skierowania w misji: misję założoną przez skierowanie usuwa, a w misji tylko uzupełnionej czyści parafię.
    /// Misji, z której korzysta inne skierowanie tej osoby, nie ruszamy. Starsze skierowania (bez zapamiętanej misji) zostają bez zmian.
    /// </summary>
    private async Task RevertMissionAsync(ParishNeedAssignment assignment, CancellationToken ct)
    {
        if (assignment.MissionId is not { } missionId) return;
        if (await _db.ParishNeedAssignments.AnyAsync(a => a.Id != assignment.Id && a.MissionId == missionId, ct)) return;
        var mission = await _db.CanonicalMissions.FirstOrDefaultAsync(m => m.Id == missionId, ct);
        if (mission is null) return;

        if (assignment.MissionCreatedByAssignment)
        {
            mission.DeletedAtUtc = DateTime.UtcNow;
            mission.DeletedBy = "odpięcie skierowania";
        }
        else if (assignment.MissionPlaceFilledByAssignment)
        {
            mission.ServicePlace = "";
            mission.ParishId = null;
            mission.UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    public async Task<ParishNeedDto?> SetClosedAsync(Guid id, bool closed, CancellationToken ct)
    {
        var need = await _db.ParishNeeds.Include(n => n.Assignments).FirstOrDefaultAsync(n => n.Id == id, ct);
        if (need is null) return null;

        need.Status = closed ? ParishNeedStatus.Closed
            : need.Assignments.Count > 0 ? ParishNeedStatus.Assigned : ParishNeedStatus.Open;
        await _db.SaveChangesAsync(ct);
        return await LoadAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct)
    {
        var need = await _db.ParishNeeds.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (need is null) return false;

        need.DeletedAtUtc = DateTime.UtcNow;
        need.DeletedBy = deletedBy;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<ParishNeedDto> LoadAsync(Guid id, CancellationToken ct) =>
        ToDto(await NeedsWithDetails().AsNoTracking().FirstAsync(n => n.Id == id, ct));

    private static ParishNeedDto ToDto(ParishNeed n) => new()
    {
        Id = n.Id,
        ParishId = n.ParishId,
        ParishName = n.Parish!.Name,
        Description = n.Description,
        Status = n.Status.ToString(),
        AssignedPeople = n.Assignments
            .OrderBy(a => a.AssignedAtUtc).ThenBy(a => a.Person!.LastName)
            .Select(a => new AssignedPersonDto { PersonId = a.PersonId, FullName = a.Person!.FullName, AssignedAtUtc = a.AssignedAtUtc })
            .ToList()
    };
}
