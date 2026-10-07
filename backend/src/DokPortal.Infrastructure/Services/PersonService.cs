using DokPortal.Application.Common;
using DokPortal.Application.People;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class PersonService : IPersonService
{
    private readonly AppDbContext _db;

    public PersonService(AppDbContext db) => _db = db;

    private IQueryable<Person> PeopleWithDetails() =>
        _db.People.Include(p => p.Parish).Include(p => p.Functions).ThenInclude(f => f.Parish).AsNoTracking();

    private static string FunctionLabel(FunctionType type) => type switch
    {
        FunctionType.Catechist => "Katechista",
        FunctionType.Acolyte => "Akolita",
        FunctionType.Lector => "Lektor",
        FunctionType.Pastor => "Proboszcz",
        _ => type.ToString()
    };

    public async Task<PagedResult<PersonDto>> SearchAsync(string? query, int page, int pageSize, CancellationToken ct, FunctionType? function = null)
    {
        var q = PeopleWithDetails();

        if (function is { } wanted)
        {
            q = q.Where(p => p.Functions.Any(f => f.Type == wanted));
        }

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
        var person = await PeopleWithDetails().FirstOrDefaultAsync(p => p.Id == id, ct);
        return person is null ? null : ToDto(person);
    }

    public async Task<PersonDto> CreateAsync(CreatePersonRequest request, CancellationToken ct)
    {
        await EnsureContactIsUniqueAsync(null, null, null, request, ct);
        if (request.Functions is not null)
        {
            await ValidateFunctionsAsync(null, request.Functions, ct);
        }

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
        foreach (var input in request.Functions ?? Array.Empty<PersonFunctionInput>())
        {
            _db.PersonFunctions.Add(NewFunction(person.Id, input));
        }
        await _db.SaveChangesAsync(ct);
        return (await GetByIdAsync(person.Id, ct))!;
    }

    public async Task<PersonDto?> UpdateAsync(Guid id, UpdatePersonRequest request, CancellationToken ct)
    {
        var person = await _db.People.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (person is null) return null;
        await EnsureContactIsUniqueAsync(id, person.Email, person.Phone, request, ct);
        if (request.Functions is not null)
        {
            await ValidateFunctionsAsync(id, request.Functions, ct);
        }

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

        if (request.Functions is not null)
        {
            await SyncFunctionsAsync(id, request.Functions, ct);
        }

        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    private static PersonFunction NewFunction(Guid personId, PersonFunctionInput input) => new()
    {
        Id = Guid.NewGuid(),
        PersonId = personId,
        Type = input.Type,
        ParishId = input.ParishId,
        InstitutedOn = input.InstitutedOn,
        Notes = input.Notes
    };

    /// <summary>Funkcja o tym samym typie (i parafii) zostaje i dostaje nowe właściwości; reszta znika, nowe dochodzą.</summary>
    private async Task SyncFunctionsAsync(Guid personId, IReadOnlyList<PersonFunctionInput> inputs, CancellationToken ct)
    {
        var existing = await _db.PersonFunctions.Where(f => f.PersonId == personId).ToListAsync(ct);
        foreach (var input in inputs)
        {
            var match = existing.FirstOrDefault(f => f.Type == input.Type && f.ParishId == input.ParishId);
            if (match is null)
            {
                // Dodajemy przez DbSet: dziecko z gotowym Guid dodane do śledzonej kolekcji byłoby uznane za istniejące.
                _db.PersonFunctions.Add(NewFunction(personId, input));
                continue;
            }

            match.InstitutedOn = input.InstitutedOn;
            match.Notes = input.Notes;
            existing.Remove(match);
        }

        _db.PersonFunctions.RemoveRange(existing);
    }

    /// <summary>Każda funkcja tylko raz (proboszcz – raz na parafię); parafia tylko dla proboszcza; parafia ma jednego proboszcza.</summary>
    private async Task ValidateFunctionsAsync(Guid? selfId, IReadOnlyList<PersonFunctionInput> inputs, CancellationToken ct)
    {
        var seen = new HashSet<(FunctionType, Guid?)>();
        foreach (var input in inputs)
        {
            if (input.ParishId is not null && input.Type != FunctionType.Pastor)
            {
                throw new InvalidOperationException($"Parafię można wskazać tylko dla proboszcza (funkcja: {FunctionLabel(input.Type)}).");
            }

            if (!seen.Add((input.Type, input.ParishId)))
            {
                throw new InvalidOperationException($"Funkcję „{FunctionLabel(input.Type)}” można dodać tylko raz.");
            }
        }

        var parishIds = inputs.Where(i => i.ParishId is not null).Select(i => i.ParishId!.Value).Distinct().ToList();
        if (parishIds.Count == 0) return;

        var parishes = await _db.Parishes.AsNoTracking().Where(p => parishIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        foreach (var parishId in parishIds)
        {
            if (!parishes.TryGetValue(parishId, out var parishName))
            {
                throw new InvalidOperationException("Nie znaleziono parafii.");
            }

            var current = await _db.PersonFunctions.AsNoTracking()
                .Where(f => f.Type == FunctionType.Pastor && f.ParishId == parishId && f.PersonId != selfId)
                .Select(f => new { f.Person!.FirstName, f.Person.LastName })
                .FirstOrDefaultAsync(ct);
            if (current is not null)
            {
                throw new InvalidOperationException(
                    $"Parafia „{parishName}” ma już proboszcza: {current.FirstName} {current.LastName}.");
            }
        }
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
        NameDayDay = p.NameDayDay,
        Functions = p.Functions
            .OrderBy(f => f.Type).ThenBy(f => f.Parish?.Name)
            .Select(f => new PersonFunctionDto
            {
                Id = f.Id,
                Type = f.Type,
                ParishId = f.ParishId,
                ParishName = f.Parish?.Name,
                InstitutedOn = f.InstitutedOn,
                Notes = f.Notes
            }).ToList()
    };
}
