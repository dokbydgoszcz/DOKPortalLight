using DokPortal.Application.Common;
using DokPortal.Application.People;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class PersonService : IPersonService
{
    private readonly AppDbContext _db;

    public PersonService(AppDbContext db) => _db = db;

    public async Task<PagedResult<PersonDto>> SearchAsync(string? query, int page, int pageSize, CancellationToken ct)
    {
        var q = _db.People.Include(p => p.Parish).AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLower();
            q = q.Where(p =>
                p.FirstName.ToLower().Contains(term) ||
                p.LastName.ToLower().Contains(term) ||
                (p.Email != null && p.Email.ToLower().Contains(term)) ||
                (p.Parish != null && p.Parish.Name.ToLower().Contains(term)));
        }

        var total = await q.CountAsync(ct);
        var entities = await q
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<PersonDto>
        {
            Items = entities.Select(ToDto).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PersonDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var person = await _db.People.Include(p => p.Parish).AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        return person is null ? null : ToDto(person);
    }

    public async Task<PersonDto> CreateAsync(CreatePersonRequest request, CancellationToken ct)
    {
        var person = new Person
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Phone = request.Phone,
            BirthDate = request.BirthDate,
            ParishId = request.ParishId,
            Notes = request.Notes,
            NameDayMonth = request.NameDayMonth,
            NameDayDay = request.NameDayDay,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _db.People.Add(person);
        await _db.SaveChangesAsync(ct);
        return (await GetByIdAsync(person.Id, ct))!;
    }

    public async Task<PersonDto?> UpdateAsync(Guid id, UpdatePersonRequest request, CancellationToken ct)
    {
        var person = await _db.People.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (person is null) return null;

        person.FirstName = request.FirstName;
        person.LastName = request.LastName;
        person.Email = request.Email;
        person.Phone = request.Phone;
        person.BirthDate = request.BirthDate;
        person.ParishId = request.ParishId;
        person.Notes = request.Notes;
        person.NameDayMonth = request.NameDayMonth;
        person.NameDayDay = request.NameDayDay;
        person.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    private static PersonDto ToDto(Person p) => new()
    {
        Id = p.Id,
        FirstName = p.FirstName,
        LastName = p.LastName,
        FullName = p.FullName,
        Email = p.Email,
        Phone = p.Phone,
        BirthDate = p.BirthDate,
        ParishId = p.ParishId,
        ParishName = p.Parish?.Name,
        Notes = p.Notes,
        NameDayMonth = p.NameDayMonth,
        NameDayDay = p.NameDayDay
    };
}
