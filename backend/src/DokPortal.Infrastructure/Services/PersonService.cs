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
        await EnsureContactIsUniqueAsync(null, null, null, request, ct);

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
        await EnsureContactIsUniqueAsync(id, person.Email, person.Phone, request, ct);

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

    /// <summary>
    /// E-mail musi być unikalny (twarda blokada): nie może go mieć inna osoba ani konto powiązane z inną osobą.
    /// Telefon tylko ostrzega, chyba że potwierdzono zapis. Przy edycji sprawdzamy wyłącznie zmienione pola,
    /// żeby dało się poprawiać osoby, które mają już duplikaty z wcześniejszych danych.
    /// </summary>
    private async Task EnsureContactIsUniqueAsync(Guid? selfId, string? currentEmail, string? currentPhone, CreatePersonRequest request, CancellationToken ct)
    {
        var email = PersonContactNormalizer.NormalizeEmail(request.Email);
        if (email is not null && email != PersonContactNormalizer.NormalizeEmail(currentEmail))
        {
            var holder = await _db.People.AsNoTracking()
                .Where(p => p.Id != selfId && p.Email != null && p.Email.ToLower() == email)
                .Select(p => new { p.Id, p.FirstName, p.LastName })
                .FirstOrDefaultAsync(ct);
            if (holder is not null)
            {
                var name = $"{holder.FirstName} {holder.LastName}";
                throw new DuplicatePersonException("EmailTaken",
                    $"Ten adres e-mail ma już: {name}. Adres e-mail musi być unikalny.",
                    new[] { new DuplicateMatch(holder.Id, name, "email") });
            }

            var account = await _db.Users.AsNoTracking()
                .Where(u => u.PersonId != null && u.PersonId != selfId && u.Email != null && u.Email.ToLower() == email)
                .Select(u => u.PersonId)
                .FirstOrDefaultAsync(ct);
            if (account is { } accountPersonId)
            {
                var owner = await _db.People.AsNoTracking().Where(p => p.Id == accountPersonId)
                    .Select(p => new { p.FirstName, p.LastName }).FirstOrDefaultAsync(ct);
                var ownerName = owner is null ? "innej osoby" : $"{owner.FirstName} {owner.LastName}";
                throw new DuplicatePersonException("EmailTaken",
                    $"Ten adres e-mail jest loginem konta osoby: {ownerName}. Adres e-mail musi być unikalny.",
                    new[] { new DuplicateMatch(accountPersonId, ownerName, "account") });
            }
        }

        var phone = PersonContactNormalizer.NormalizePhone(request.Phone);
        if (phone is not null && !request.ConfirmDuplicate && phone != PersonContactNormalizer.NormalizePhone(currentPhone))
        {
            var withPhone = await _db.People.AsNoTracking()
                .Where(p => p.Id != selfId && p.Phone != null)
                .Select(p => new { p.Id, p.FirstName, p.LastName, p.Phone })
                .ToListAsync(ct);
            var matches = withPhone
                .Where(p => PersonContactNormalizer.NormalizePhone(p.Phone) == phone)
                .Select(p => new DuplicateMatch(p.Id, $"{p.FirstName} {p.LastName}", "phone"))
                .ToList();
            if (matches.Count > 0)
            {
                throw new DuplicatePersonException("PhoneDuplicate",
                    $"Ten numer telefonu ma już: {string.Join(", ", matches.Select(m => m.FullName))}.", matches);
            }
        }
    }

    public async Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct)
    {
        var person = await _db.People.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (person is null) return false;

        person.DeletedAtUtc = DateTime.UtcNow;
        person.DeletedBy = deletedBy;
        await _db.SaveChangesAsync(ct);
        return true;
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
