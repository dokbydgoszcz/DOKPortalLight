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

        need.ParishId = request.ParishId;
        need.Description = description;
        await _db.SaveChangesAsync(ct);
        return await LoadAsync(id, ct);
    }

    public async Task<ParishNeedDto?> AssignAsync(Guid id, Guid personId, CancellationToken ct)
    {
        var need = await _db.ParishNeeds.Include(n => n.Assignments).FirstOrDefaultAsync(n => n.Id == id, ct);
        if (need is null) return null;
        if (!await _db.People.AnyAsync(p => p.Id == personId, ct)) throw new InvalidOperationException("Nie znaleziono osoby.");

        var isNew = need.Assignments.All(a => a.PersonId != personId);
        if (isNew)
        {
            _db.ParishNeedAssignments.Add(new ParishNeedAssignment
            {
                Id = Guid.NewGuid(), ParishNeedId = id, PersonId = personId, AssignedAtUtc = DateTime.UtcNow
            });
            await RecordMissionAsync(need, personId, ct);
        }
        if (need.Status == ParishNeedStatus.Open) need.Status = ParishNeedStatus.Assigned;
        await _db.SaveChangesAsync(ct);
        if (isNew) await NotifyAsync(need, personId, ct);
        return await LoadAsync(id, ct);
    }

    /// <summary>
    /// Skierowanie do parafii zakłada rekord misji kanonicznej na rok (osoba zostaje katechistą). Jeśli osoba ma już misję
    /// bez miejsca (udzieloną „Udziel posłania”), uzupełniamy w niej parafię; jeśli ma już aktualną misję w tej parafii – nic nie dodajemy.
    /// </summary>
    private async Task RecordMissionAsync(ParishNeed need, Guid personId, CancellationToken ct)
    {
        var parishName = await _db.Parishes.Where(p => p.Id == need.ParishId).Select(p => p.Name).FirstAsync(ct);
        var today = _time.Today();
        var missions = await _db.CanonicalMissions.Where(m => m.PersonId == personId).ToListAsync(ct);

        var unplaced = missions.Where(m => string.IsNullOrWhiteSpace(m.ServicePlace)).OrderByDescending(m => m.MissionEndDate).FirstOrDefault();
        if (unplaced is not null)
        {
            unplaced.ServicePlace = parishName;
            unplaced.UpdatedAtUtc = DateTime.UtcNow;
        }
        else if (!missions.Any(m => m.ServicePlace == parishName && m.MissionEndDate >= today))
        {
            _db.CanonicalMissions.Add(new CanonicalMission
            {
                Id = Guid.NewGuid(),
                PersonId = personId,
                ServicePlace = parishName,
                MissionStartDate = today,
                MissionEndDate = today.AddYears(1),
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
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
            await _db.SaveChangesAsync(ct);
        }
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
