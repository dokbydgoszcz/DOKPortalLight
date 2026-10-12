using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

/// <summary>
/// Funkcja Katechista idzie za misjami kanonicznymi: misja ją nadaje, a gdy osobie nie zostaje żadna misja, funkcja znika.
/// Ręczne nadanie funkcji bez misji jest dozwolone; zdjęcie funkcji przy ważnej misji blokuje <see cref="PersonService"/>.
/// </summary>
internal static class CatechistFunction
{
    /// <summary>Katechista jest funkcją osoby: dodaje ją (bez zapisu), jeśli osoba jeszcze jej nie ma.</summary>
    public static async Task EnsureAsync(AppDbContext db, Guid personId, CancellationToken ct)
    {
        var has = await db.PersonFunctions.AnyAsync(f => f.PersonId == personId && f.Type == FunctionType.Catechist, ct);
        if (!has)
        {
            db.PersonFunctions.Add(new PersonFunction { Id = Guid.NewGuid(), PersonId = personId, Type = FunctionType.Catechist });
        }
    }

    /// <summary>
    /// Po usunięciu lub przeniesieniu misji: jeśli osoba nie ma już żadnej misji, zdejmuje jej funkcję Katechista (bez zapisu).
    /// Wywołuj po zapisaniu zmiany misji, bo sprawdza stan bazy.
    /// </summary>
    public static async Task RemoveIfNoMissionAsync(AppDbContext db, Guid personId, CancellationToken ct)
    {
        if (await db.CanonicalMissions.AnyAsync(m => m.PersonId == personId, ct)) return;
        var functions = await db.PersonFunctions.Where(f => f.PersonId == personId && f.Type == FunctionType.Catechist).ToListAsync(ct);
        db.PersonFunctions.RemoveRange(functions);
    }
}
