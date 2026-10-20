using System.Text.Json;
using DokPortal.Application.FormSubmissions;
using DokPortal.Application.People;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

/// <summary>
/// Kolejka zgłoszeń z formularzy (Tally): webhook zapisuje zgłoszenie, osoba z prawem edycji osób
/// przegląda je, poprawia dane i zapisuje jako nową osobę albo aktualizację istniejącej – albo odrzuca.
/// </summary>
public class FormSubmissionService : IFormSubmissionService
{
    public const string TallySource = "Tally";
    private const int MaxListed = 500;
    private readonly AppDbContext _db;
    private readonly IFormFileFetcher _files;
    private readonly TimeProvider _time;

    public FormSubmissionService(AppDbContext db, IFormFileFetcher files, TimeProvider? time = null)
    {
        _db = db;
        _files = files;
        _time = time ?? TimeProvider.System;
    }

    public async Task<TallyReceiveResult> ReceiveTallyAsync(string json, CancellationToken ct)
    {
        TallyEvent? tally;
        try
        {
            tally = FormSubmissionParser.ParseTally(json);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Nieprawidłowe dane zgłoszenia (to nie jest JSON z Tally).");
        }
        if (tally is null || tally.EventType != "FORM_RESPONSE") return TallyReceiveResult.Ignored;
        if (await _db.FormSubmissions.AnyAsync(s => s.Source == TallySource && s.ExternalId == tally.SubmissionId, ct))
            return TallyReceiveResult.Duplicate;

        _db.FormSubmissions.Add(new FormSubmission
        {
            Id = Guid.NewGuid(),
            Source = TallySource,
            ExternalId = Cut(tally.SubmissionId, 64)!,
            FormId = Cut(tally.FormId, 64),
            FormName = Cut(tally.FormName, 200),
            SubmittedAtUtc = tally.SubmittedAtUtc,
            ReceivedAtUtc = _time.GetUtcNow().UtcDateTime,
            FieldsJson = JsonSerializer.Serialize(tally.Fields, FormSubmissionParser.Json),
            Status = FormSubmissionStatus.Pending
        });
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Tally ponowił wysyłkę równolegle – zgłoszenie już jest.
            return TallyReceiveResult.Duplicate;
        }
        return TallyReceiveResult.Created;
    }

    public Task<int> PendingCountAsync(CancellationToken ct) =>
        _db.FormSubmissions.CountAsync(s => s.Status == FormSubmissionStatus.Pending, ct);

    public async Task<IReadOnlyList<FormSubmissionDto>> ListAsync(FormSubmissionStatus status, CancellationToken ct)
    {
        var query = _db.FormSubmissions.AsNoTracking().Where(s => s.Status == status);
        query = status == FormSubmissionStatus.Pending
            ? query.OrderBy(s => s.SubmittedAtUtc)
            : query.OrderByDescending(s => s.ReviewedAtUtc).ThenByDescending(s => s.SubmittedAtUtc);
        var submissions = await query.Take(MaxListed).ToListAsync(ct);
        if (submissions.Count == 0) return Array.Empty<FormSubmissionDto>();
        var people = await LoadPeopleAsync(ct);
        return submissions.Select(s => ToDto(s, people)).ToList();
    }

    public async Task<FormSubmissionDto?> ApplyAsync(Guid id, ApplyFormSubmissionRequest request, string reviewerEmail, CancellationToken ct)
    {
        var submission = await _db.FormSubmissions.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (submission is null) return null;
        if (submission.Status != FormSubmissionStatus.Pending) throw new InvalidOperationException("To zgłoszenie zostało już przetworzone.");

        var first = Value(request.FirstName, 100, "Imię");
        var last = Value(request.LastName, 100, "Nazwisko");
        var email = Value(request.Email, 256, "E-mail")?.ToLowerInvariant();
        var phone = Value(request.Phone, 50, "Telefon");
        var street = Value(request.AddressStreet, 200, "Ulica");
        var postal = Value(request.AddressPostalCode, 20, "Kod pocztowy");
        var city = Value(request.AddressCity, 100, "Miejscowość");
        if (email is not null && !email.Contains('@')) throw new InvalidOperationException("E-mail wygląda na niepoprawny.");
        if ((request.NameDayDay is null) != (request.NameDayMonth is null))
            throw new InvalidOperationException("Imieniny: podaj dzień i miesiąc albo zostaw oba puste.");
        if (request.NameDayMonth is { } m && (m is < 1 or > 12 || request.NameDayDay is not { } d || d < 1 || d > DateTime.DaysInMonth(2024, m)))
            throw new InvalidOperationException("Imieniny: nie ma takiego dnia w tym miesiącu.");

        var now = _time.GetUtcNow().UtcDateTime;
        Person person;
        if (request.PersonId is { } personId)
        {
            person = await _db.People.FirstOrDefaultAsync(p => p.Id == personId && p.AnonymizedAtUtc == null, ct)
                ?? throw new InvalidOperationException("Nie ma takiej osoby (może została usunięta).");
        }
        else
        {
            if (first is null || last is null) throw new InvalidOperationException("Nowa osoba musi mieć imię i nazwisko.");
            person = new Person { Id = Guid.NewGuid(), FirstName = first, LastName = last, CreatedAtUtc = now, UpdatedAtUtc = now };
            _db.People.Add(person);
        }

        if (email is not null && !string.Equals(PersonContactNormalizer.NormalizeEmail(person.Email), email, StringComparison.Ordinal))
        {
            var owner = await _db.People
                .Where(p => p.Id != person.Id && p.Email != null && p.Email.ToLower() == email)
                .Select(p => p.FirstName + " " + p.LastName)
                .FirstOrDefaultAsync(ct);
            if (owner is not null)
                throw new InvalidOperationException($"Ten e-mail ma już w bazie {owner} – wybierz tę osobę do aktualizacji albo popraw e-mail.");
        }

        // Puste pola nie kasują danych, które już są.
        if (first is not null) person.FirstName = first;
        if (last is not null) person.LastName = last;
        if (email is not null) person.Email = email;
        if (phone is not null) person.Phone = phone;
        if (street is not null) person.AddressStreet = street;
        if (postal is not null) person.AddressPostalCode = postal;
        if (city is not null) person.AddressCity = city;
        if (request.NameDayMonth is not null)
        {
            person.NameDayMonth = request.NameDayMonth;
            person.NameDayDay = request.NameDayDay;
        }
        person.UpdatedAtUtc = now;

        submission.Status = FormSubmissionStatus.Applied;
        submission.PersonId = person.Id;
        submission.ReviewedByEmail = reviewerEmail;
        submission.ReviewedAtUtc = now;
        await _db.SaveChangesAsync(ct);
        return ToDto(submission, await LoadPeopleAsync(ct));
    }

    public async Task<FormSubmissionDto?> RejectAsync(Guid id, string reviewerEmail, CancellationToken ct)
    {
        var submission = await _db.FormSubmissions.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (submission is null) return null;
        if (submission.Status != FormSubmissionStatus.Pending) throw new InvalidOperationException("To zgłoszenie zostało już przetworzone.");
        submission.Status = FormSubmissionStatus.Rejected;
        submission.ReviewedByEmail = reviewerEmail;
        submission.ReviewedAtUtc = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct);
        return ToDto(submission, await LoadPeopleAsync(ct));
    }

    public async Task<FormSubmissionDto?> ReopenAsync(Guid id, CancellationToken ct)
    {
        var submission = await _db.FormSubmissions.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (submission is null) return null;
        if (submission.Status != FormSubmissionStatus.Rejected) throw new InvalidOperationException("Przywrócić można tylko odrzucone zgłoszenie.");
        submission.Status = FormSubmissionStatus.Pending;
        submission.ReviewedByEmail = null;
        submission.ReviewedAtUtc = null;
        await _db.SaveChangesAsync(ct);
        return ToDto(submission, await LoadPeopleAsync(ct));
    }

    public async Task<DownloadedFile?> GetFileAsync(Guid id, int index, CancellationToken ct)
    {
        var json = await _db.FormSubmissions.Where(s => s.Id == id).Select(s => s.FieldsJson).FirstOrDefaultAsync(ct);
        if (json is null) return null;
        var files = FormSubmissionParser.AllFiles(ReadFields(json));
        if (index < 0 || index >= files.Count) return null;
        return await _files.FetchAsync(files[index], ct);
    }

    private sealed record PersonRow(Guid Id, string FirstName, string LastName, string? Email, string? Phone, string? AddressStreet,
        string? AddressPostalCode, string? AddressCity, int? NameDayDay, int? NameDayMonth, bool HasPhoto, List<FunctionType> Functions);

    private Task<List<PersonRow>> LoadPeopleAsync(CancellationToken ct) =>
        _db.People.AsNoTracking()
            .Where(p => p.AnonymizedAtUtc == null)
            .Select(p => new PersonRow(p.Id, p.FirstName, p.LastName, p.Email, p.Phone, p.AddressStreet, p.AddressPostalCode, p.AddressCity,
                p.NameDayDay, p.NameDayMonth, p.PhotoBlobPath != null, p.Functions.Select(f => f.Type).ToList()))
            .ToListAsync(ct);

    private static IReadOnlyList<FormSubmissionField> ReadFields(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<FormSubmissionField>>(json, FormSubmissionParser.Json) ?? new List<FormSubmissionField>();
        }
        catch (JsonException)
        {
            return new List<FormSubmissionField>();
        }
    }

    private static FormSubmissionDto ToDto(FormSubmission s, List<PersonRow> people)
    {
        var fields = ReadFields(s.FieldsJson);
        var draft = FormSubmissionParser.Draft(fields);
        var linked = s.PersonId is { } pid ? people.FirstOrDefault(p => p.Id == pid) : null;
        return new FormSubmissionDto
        {
            Id = s.Id,
            Source = s.Source,
            FormName = s.FormName,
            SubmittedAtUtc = s.SubmittedAtUtc,
            Status = s.Status,
            Fields = fields,
            Files = FormSubmissionParser.AllFiles(fields),
            Draft = draft,
            Candidates = s.Status == FormSubmissionStatus.Pending ? Candidates(draft, people) : Array.Empty<FormSubmissionCandidateDto>(),
            PersonId = s.PersonId,
            PersonFullName = linked is null ? null : $"{linked.FirstName} {linked.LastName}",
            ReviewedByEmail = s.ReviewedByEmail,
            ReviewedAtUtc = s.ReviewedAtUtc
        };
    }

    private static IReadOnlyList<FormSubmissionCandidateDto> Candidates(PersonDraft draft, List<PersonRow> people)
    {
        var email = PersonContactNormalizer.NormalizeEmail(draft.Email);
        var phone = PersonContactNormalizer.NormalizePhone(draft.Phone);
        var name = draft.FirstName is null || draft.LastName is null ? null : Norm(draft.FirstName) + "|" + Norm(draft.LastName);
        var result = new List<(int Rank, FormSubmissionCandidateDto Dto)>();
        foreach (var p in people)
        {
            var reasons = new List<string>();
            if (email is not null && PersonContactNormalizer.NormalizeEmail(p.Email) == email) reasons.Add("e-mail");
            if (phone is not null && PersonContactNormalizer.NormalizePhone(p.Phone) == phone) reasons.Add("telefon");
            if (name is not null && Norm(p.FirstName) + "|" + Norm(p.LastName) == name) reasons.Add("imię i nazwisko");
            if (reasons.Count == 0) continue;
            var rank = (reasons.Contains("e-mail") ? 0 : 4) + (reasons.Contains("imię i nazwisko") ? 0 : 2) + (reasons.Contains("telefon") ? 0 : 1);
            result.Add((rank, new FormSubmissionCandidateDto
            {
                PersonId = p.Id,
                FullName = $"{p.FirstName} {p.LastName}",
                MatchedBy = string.Join(", ", reasons),
                FirstName = p.FirstName,
                LastName = p.LastName,
                Email = p.Email,
                Phone = p.Phone,
                AddressStreet = p.AddressStreet,
                AddressPostalCode = p.AddressPostalCode,
                AddressCity = p.AddressCity,
                NameDayDay = p.NameDayDay,
                NameDayMonth = p.NameDayMonth,
                HasPhoto = p.HasPhoto,
                Functions = p.Functions.Select(f => f.ToString()).Distinct().ToList()
            }));
        }
        return result.OrderBy(r => r.Rank).ThenBy(r => r.Dto.FullName).Select(r => r.Dto).Take(10).ToList();
    }

    private static string Norm(string text) => string.Join(' ', text.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static string? Value(string? text, int max, string label)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var value = string.Join(' ', text.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (value.Length > max) throw new InvalidOperationException($"{label}: najwyżej {max} znaków.");
        return value;
    }

    private static string? Cut(string? text, int max) => text is null ? null : text.Length <= max ? text : text[..max];
}
