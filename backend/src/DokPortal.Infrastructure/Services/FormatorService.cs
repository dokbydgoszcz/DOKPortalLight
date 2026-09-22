using DokPortal.Application.Formators;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class FormatorService : IFormatorService
{
    private readonly AppDbContext _db;

    public FormatorService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<FormatorDto>> GetAllAsync(CancellationToken ct)
    {
        var formators = await _db.Formators.Include(f => f.Person).AsNoTracking()
            .OrderBy(f => f.Person!.LastName)
            .ToListAsync(ct);
        return formators.Select(ToDto).ToList();
    }

    public async Task<FormatorDto> CreateAsync(CreateFormatorRequest request, CancellationToken ct)
    {
        var formator = new Formator { Id = Guid.NewGuid(), PersonId = request.PersonId, Function = request.Function };
        _db.Formators.Add(formator);
        await _db.SaveChangesAsync(ct);

        var saved = await _db.Formators.Include(f => f.Person).AsNoTracking().FirstAsync(f => f.Id == formator.Id, ct);
        return ToDto(saved);
    }

    private static FormatorDto ToDto(Formator f) => new()
    {
        Id = f.Id,
        PersonId = f.PersonId,
        PersonFullName = f.Person!.FullName,
        PersonEmail = f.Person.Email,
        PersonPhone = f.Person.Phone,
        Function = f.Function
    };
}
