using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

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
}
