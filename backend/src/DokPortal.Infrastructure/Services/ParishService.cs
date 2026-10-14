using DokPortal.Application.Parishes;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DokPortal.Infrastructure.Services;

public class ParishService : IParishService
{
    private readonly AppDbContext _db;
    private readonly IEmailSender? _emailSender;
    private readonly ILogger<ParishService>? _logger;

    public ParishService(AppDbContext db, IEmailSender? emailSender = null, ILogger<ParishService>? logger = null)
    {
        _db = db;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ParishDto>> GetAllAsync(CancellationToken ct)
    {
        var parishes = await _db.Parishes.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);
        var pastors = await PastorsAsync(parishes.Select(p => p.Id).ToList(), ct);
        return parishes.Select(p => ToDto(p, pastors)).ToList();
    }

    /// <summary>Proboszczowie parafii: osoby z funkcją Proboszcz wskazującą daną parafię.</summary>
    private async Task<Dictionary<Guid, (Guid PersonId, string FullName)>> PastorsAsync(IReadOnlyCollection<Guid> parishIds, CancellationToken ct)
    {
        var rows = await _db.PersonFunctions.AsNoTracking()
            .Where(f => f.Type == FunctionType.Pastor && f.ParishId != null && parishIds.Contains(f.ParishId.Value))
            .Select(f => new { ParishId = f.ParishId!.Value, f.PersonId, f.Person!.FirstName, f.Person.LastName })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.ParishId).ToDictionary(g => g.Key, g => (g.First().PersonId, $"{g.First().FirstName} {g.First().LastName}"));
    }

    private static ParishDto ToDto(Parish p, IReadOnlyDictionary<Guid, (Guid PersonId, string FullName)> pastors) => new()
    {
        Id = p.Id,
        Name = p.Name,
        City = p.City,
        PastorPersonId = pastors.TryGetValue(p.Id, out var pastor) ? pastor.PersonId : null,
        PastorFullName = pastors.TryGetValue(p.Id, out var named) ? named.FullName : null
    };

    private async Task<ParishDto> LoadAsync(Parish parish, CancellationToken ct) =>
        ToDto(parish, await PastorsAsync(new[] { parish.Id }, ct));

    /// <summary>
    /// Ustawia proboszcza parafii: dotychczasowy traci przypisanie (zostaje Proboszczem „czekającym na przydział”),
    /// nowy dostaje funkcję Proboszcz z tą parafią (jego dotychczasowa funkcja przechodzi na tę parafię).
    /// </summary>
    /// <summary>Ustawia proboszcza parafii; zwraca id nowo wskazanego proboszcza (null, gdy nic się nie zmieniło albo go zdjęto).</summary>
    private async Task<Guid?> SetPastorAsync(Guid parishId, Guid? personId, CancellationToken ct)
    {
        var current = await _db.PersonFunctions.Where(f => f.Type == FunctionType.Pastor && f.ParishId == parishId).ToListAsync(ct);
        if (personId is not null && current.Any(f => f.PersonId == personId)) return null;
        foreach (var function in current) function.ParishId = null;
        if (personId is not { } newPastorId) return null;

        if (!await _db.People.AnyAsync(p => p.Id == newPastorId, ct))
            throw new InvalidOperationException("Nie znaleziono osoby wskazanej jako proboszcz.");
        var existing = await _db.PersonFunctions
            .Where(f => f.PersonId == newPastorId && f.Type == FunctionType.Pastor)
            .OrderBy(f => f.ParishId == null ? 0 : 1)
            .FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            existing.ParishId = parishId;
        }
        else
        {
            _db.PersonFunctions.Add(new PersonFunction { Id = Guid.NewGuid(), PersonId = newPastorId, Type = FunctionType.Pastor, ParishId = parishId });
        }
        return newPastorId;
    }

    public async Task<ParishDto> CreateAsync(CreateParishRequest request, CancellationToken ct)
    {
        var parish = new Parish { Id = Guid.NewGuid(), Name = request.Name, City = request.City };
        _db.Parishes.Add(parish);
        await SetPastorAsync(parish.Id, request.PastorPersonId, ct);
        await _db.SaveChangesAsync(ct);
        // Nowa parafia nie ma jeszcze skierowań – nie ma o czym pisać.
        return await LoadAsync(parish, ct);
    }

    public async Task<ParishDto?> UpdateAsync(Guid id, UpdateParishRequest request, CancellationToken ct)
    {
        var parish = await _db.Parishes.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (parish is null) return null;

        parish.Name = request.Name;
        parish.City = request.City;
        // Misje skierowane do tej parafii pokazują jej nazwę jako miejsce posługi.
        var missions = await _db.CanonicalMissions.Where(m => m.ParishId == id).ToListAsync(ct);
        foreach (var mission in missions)
        {
            mission.ServicePlace = request.Name;
        }
        var newPastor = await SetPastorAsync(id, request.PastorPersonId, ct);
        await _db.SaveChangesAsync(ct);
        if (newPastor is { } pastorId)
            await PastorReferralNotifier.NotifyAsync(_db, _emailSender, _logger, id, pastorId, ct);
        return await LoadAsync(parish, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct)
    {
        var parish = await _db.Parishes.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (parish is null) return false;
        await EnsureCanDeleteAsync(id, ct);

        parish.DeletedAtUtc = DateTime.UtcNow;
        parish.DeletedBy = deletedBy;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Parafii z osobami, proboszczem, zapotrzebowaniami lub misjami nie usuwamy – zostałyby odwołania do nieistniejącej parafii.</summary>
    private async Task EnsureCanDeleteAsync(Guid id, CancellationToken ct)
    {
        var reasons = new List<string>();
        void Add(int count, string text) { if (count > 0) reasons.Add($"{text} ({count})"); }

        Add(await _db.People.CountAsync(p => p.ParishId == id, ct), "przypisane osoby");
        Add(await _db.PersonFunctions.CountAsync(f => f.Type == FunctionType.Pastor && f.ParishId == id, ct), "proboszcz");
        Add(await _db.ParishNeeds.CountAsync(n => n.ParishId == id, ct), "zapotrzebowania na katechistów");
        Add(await _db.CanonicalMissions.CountAsync(m => m.ParishId == id, ct), "misje kanoniczne");

        if (reasons.Count > 0)
        {
            throw new InvalidOperationException($"Nie można usunąć parafii, bo ma: {string.Join("; ", reasons)}. Najpierw je przenieś lub usuń.");
        }
    }
}
