using DokPortal.Application.Common;
using DokPortal.Application.People;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DokPortal.Infrastructure.Services;

public class GdprService : IGdprService
{
    private readonly AppDbContext _db;
    private readonly IFileStorageService? _storage;
    private readonly TimeProvider _time;
    private readonly ILogger<GdprService>? _logger;

    public GdprService(AppDbContext db, IFileStorageService? storage = null, TimeProvider? time = null, ILogger<GdprService>? logger = null)
    {
        _db = db;
        _storage = storage;
        _time = time ?? TimeProvider.System;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, object?>?> ExportAsync(Guid personId, CancellationToken ct)
    {
        var person = await _db.People.IgnoreQueryFilters().AsNoTracking().Include(p => p.Parish).Include(p => p.Functions).ThenInclude(f => f.Parish)
            .FirstOrDefaultAsync(p => p.Id == personId, ct);
        if (person is null) return null;

        var cases = await _db.DokCases.IgnoreQueryFilters().AsNoTracking()
            .Include(c => c.CatechistPerson).Include(c => c.MentorPerson)
            .Where(c => c.PersonId == personId).OrderBy(c => c.CreatedAtUtc).ToListAsync(ct);
        var caseIds = cases.Select(c => c.Id).ToList();
        var caseEvents = await _db.DokCaseEvents.IgnoreQueryFilters().AsNoTracking().Where(e => caseIds.Contains(e.DokCaseId)).OrderBy(e => e.AtUtc).ToListAsync(ct);
        var caseDocuments = await _db.CaseDocuments.IgnoreQueryFilters().AsNoTracking().Where(d => caseIds.Contains(d.DokCaseId)).ToListAsync(ct);
        var meetings = await _db.Meetings.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.DeletedAtUtc == null && (m.DokCaseId != null && caseIds.Contains(m.DokCaseId.Value) || m.Attendees.Any(a => caseIds.Contains(a.DokCaseId))))
            .Include(m => m.Attendees).OrderBy(m => m.MeetingDate).ToListAsync(ct);
        var noteCount = await _db.PastoralNotes.IgnoreQueryFilters().CountAsync(n => caseIds.Contains(n.DokCaseId), ct);

        var candidates = await _db.Candidates.IgnoreQueryFilters().AsNoTracking().Include(c => c.Retreats).Include(c => c.Events)
            .Where(c => c.PersonId == personId).ToListAsync(ct);
        var candidateIds = candidates.Select(c => c.Id).ToList();
        var classes = await _db.SkspClassAttendees.IgnoreQueryFilters().AsNoTracking().Include(a => a.SkspClass)
            .Where(a => candidateIds.Contains(a.CandidateId) && a.SkspClass!.DeletedAtUtc == null)
            .OrderBy(a => a.SkspClass!.ClassDate).ToListAsync(ct);
        var missions = await _db.CanonicalMissions.IgnoreQueryFilters().AsNoTracking().Where(m => m.PersonId == personId).OrderBy(m => m.MissionStartDate).ToListAsync(ct);
        var referrals = await _db.ParishNeedAssignments.IgnoreQueryFilters().AsNoTracking().Include(a => a.ParishNeed).ThenInclude(n => n!.Parish)
            .Where(a => a.PersonId == personId).OrderBy(a => a.AssignedAtUtc).ToListAsync(ct);
        var supervisions = await _db.SupervisionAttendees.IgnoreQueryFilters().AsNoTracking().Include(a => a.Supervision)
            .Where(a => a.PersonId == personId && a.Supervision!.DeletedAtUtc == null).OrderBy(a => a.Supervision!.SupervisionDate).ToListAsync(ct);
        var formator = await _db.Formators.IgnoreQueryFilters().AsNoTracking().Where(f => f.PersonId == personId && f.DeletedAtUtc == null).Select(f => f.Function).ToListAsync(ct);
        var documents = await _db.GeneratedDocuments.AsNoTracking().Where(d => d.PersonId == personId).OrderBy(d => d.CreatedAtUtc).ToListAsync(ct);
        var mailings = await _db.MailingDeliveries.AsNoTracking().Include(d => d.Campaign)
            .Where(d => d.PersonId == personId && d.Status == MailingDeliveryStatus.Sent).OrderBy(d => d.AttemptedAtUtc).ToListAsync(ct);
        var leadsCases = await _db.DokCases.IgnoreQueryFilters().CountAsync(c => c.CatechistPersonId == personId || c.MentorPersonId == personId, ct);
        var account = await _db.Users.AsNoTracking().Where(u => u.PersonId == personId)
            .Select(u => new { u.Email, u.IsDeactivated, MfaEnabled = u.TwoFactorEnabled }).FirstOrDefaultAsync(ct);
        var accountRoles = account is null ? new List<string>() : await (
            from user in _db.Users
            where user.PersonId == personId
            join userRole in _db.UserRoles on user.Id equals userRole.UserId
            join role in _db.Roles on userRole.RoleId equals role.Id
            select role.Name!).ToListAsync(ct);

        return new Dictionary<string, object?>
        {
            ["opis"] = "Kopia danych osobowych zapisanych w DOK Portal (Diecezjalny Ośrodek Katechumenalny i Szkoła Katechistów św. Pawła w Bydgoszczy).",
            ["wygenerowano"] = _time.GetUtcNow().UtcDateTime,
            ["osoba"] = new
            {
                imie = person.FirstName,
                nazwisko = person.LastName,
                email = person.Email,
                telefon = person.Phone,
                adres = string.Join(", ", new[] { person.AddressStreet, string.Join(" ", new[] { person.AddressPostalCode, person.AddressCity }.Where(s => !string.IsNullOrWhiteSpace(s))) }.Where(s => !string.IsNullOrWhiteSpace(s))) is { Length: > 0 } address ? address : null,
                dataUrodzenia = person.BirthDate,
                parafia = person.Parish?.Name,
                imieniny = person.NameDayMonth is { } m && person.NameDayDay is { } d ? $"{d:00}.{m:00}" : null,
                uwagi = person.Notes,
                zdjecie = person.PhotoBlobPath is null ? "brak" : "jest (plik przekazywany osobno na prośbę)",
                wypisanyZMailingu = person.MailingOptOutAtUtc,
                utworzono = person.CreatedAtUtc,
                zmieniono = person.UpdatedAtUtc,
                funkcje = person.Functions.Select(f => new { funkcja = f.Type.ToString(), parafia = f.Parish?.Name, od = f.InstitutedOn, notatka = f.Notes })
            },
            ["konto"] = account is null ? null : new { login = account.Email, role = accountRoles, wylaczone = account.IsDeactivated, weryfikacjaDwuetapowa = account.MfaEnabled },
            ["dokSprawy"] = cases.Select(c => new
            {
                sciezka = c.Path.ToString(),
                etap = c.Stage.ToString(),
                etapOd = c.StageSinceUtc,
                katechista = c.CatechistPerson?.FullName,
                mentor = c.MentorPerson?.FullName,
                przerwano = c.InterruptedOn,
                powodPrzerwania = c.InterruptionReason,
                zakonczono = c.CompletedAtUtc,
                usunieto = c.DeletedAtUtc,
                historia = caseEvents.Where(e => e.DokCaseId == c.Id).Select(e => new { kiedy = e.AtUtc, zmiana = e.Kind.ToString(), z = e.FromValue, na = e.ToValue }),
                dokumenty = caseDocuments.Where(d => d.DokCaseId == c.Id).Select(d => new { nazwa = d.Name, dostarczony = d.IsProvided, plik = d.OriginalFileName })
            }),
            ["spotkania"] = meetings.Select(m => new
            {
                data = m.MeetingDate,
                od = m.StartTime,
                @do = m.EndTime,
                grupa = m.GroupLabel,
                obecny = m.DokCaseId is { } id && caseIds.Contains(id) ? m.IsAttended : m.Attendees.FirstOrDefault(a => caseIds.Contains(a.DokCaseId))?.IsAttended,
                notatka = m.DokCaseId is { } own && caseIds.Contains(own) ? m.Notes : null
            }),
            ["notatkiDuszpasterskie"] = noteCount == 0 ? null
                : $"{noteCount} notatek katechisty – są poufne; o ich udostępnieniu decyduje administrator (art. 15 ust. 4 RODO: prawa innych osób).",
            ["skspKandydat"] = candidates.Select(c => new
            {
                rokFormacji = c.FormationYear,
                ukonczyl = c.IsFormationCompleted,
                zatrzymano = c.IsFormationStopped,
                powodZatrzymania = c.FormationStopNote,
                frekwencjaRecznie = c.AttendancePercentage,
                opinie = $"{c.OpinionsCollected}/{c.OpinionsRequired}",
                rekolekcje = c.Retreats.OrderBy(r => r.Year).Select(r => new { rok = r.Year, zaliczone = r.IsCompleted }),
                historia = c.Events.OrderBy(e => e.AtUtc).Select(e => new { kiedy = e.AtUtc, zmiana = e.Kind.ToString(), z = e.FromYear, na = e.ToYear, notatka = e.Note }),
                zajecia = classes.Where(a => a.CandidateId == c.Id).Select(a => new { data = a.SkspClass!.ClassDate, temat = a.SkspClass.Topic, obecny = a.IsPresent })
            }),
            ["misjeKanoniczne"] = missions.Select(m => new { miejsce = m.ServicePlace, od = m.MissionStartDate, @do = m.MissionEndDate, udzielono = m.GrantedDate, grupaSuperwizyjna = m.SupervisionGroup, poslanyDoDok = m.SentToDok }),
            ["skierowaniaDoParafii"] = referrals.Select(r => new { parafia = r.ParishNeed?.Parish?.Name, zapotrzebowanie = r.ParishNeed?.Description, kiedy = r.AssignedAtUtc }),
            ["superwizje"] = supervisions.Select(s => new { data = s.Supervision!.SupervisionDate, grupa = s.Supervision.GroupLabel, temat = s.Supervision.Topic, obecny = s.IsPresent }),
            ["formator"] = formator,
            ["pisma"] = documents.Select(d => new { wzor = d.Title ?? d.Template.ToString(), kiedy = d.CreatedAtUtc, uwagi = d.AdditionalNotes }),
            ["mailing"] = mailings.Select(d => new { temat = d.Campaign?.Subject, kiedy = d.AttemptedAtUtc, adres = d.Email }),
            ["prowadzoneSprawy"] = leadsCases == 0 ? null : $"{leadsCases} spraw DOK, w których osoba jest katechistą lub mentorem (dane podopiecznych nie są częścią tej kopii)."
        };
    }

    public async Task<bool> AnonymizeAsync(Guid personId, CancellationToken ct)
    {
        var person = await _db.People.IgnoreQueryFilters().Include(p => p.Functions).FirstOrDefaultAsync(p => p.Id == personId, ct);
        if (person is null) return false;
        if (person.AnonymizedAtUtc is not null) throw new InvalidOperationException("Dane tej osoby zostały już zanonimizowane.");
        var openLed = await _db.DokCases.CountAsync(c => (c.CatechistPersonId == personId || c.MentorPersonId == personId)
            && c.PersonId != personId && c.CompletedAtUtc == null && c.InterruptedOn == null, ct);
        if (openLed > 0)
            throw new InvalidOperationException($"Ta osoba prowadzi {openLed} otwartych spraw DOK (jako katechista lub mentor). Najpierw przekaż je komuś innemu.");

        var now = _time.GetUtcNow().UtcDateTime;
        var blobs = new List<string>();

        if (person.PhotoBlobPath is not null) blobs.Add(person.PhotoBlobPath);
        person.FirstName = "Osoba";
        person.LastName = $"zanonimizowana {person.Id.ToString()[..6]}";
        person.Email = null;
        person.Phone = null;
        person.AddressStreet = null;
        person.AddressPostalCode = null;
        person.AddressCity = null;
        person.Sex = null;
        person.BirthDate = null;
        person.ParishId = null;
        person.Notes = null;
        person.NameDayMonth = null;
        person.NameDayDay = null;
        person.PhotoBlobPath = null;
        person.PhotoContentType = null;
        person.PhotoUpdatedAtUtc = null;
        person.MailingOptOutAtUtc ??= now;
        person.MailingUnsubscribeToken = null;
        person.AnonymizedAtUtc = now;
        person.UpdatedAtUtc = now;
        foreach (var function in person.Functions) function.Notes = null;

        // Sprawy DOK tej osoby: powody, notatki duszpasterskie, pliki dokumentów, notatki ze spotkań.
        var cases = await _db.DokCases.IgnoreQueryFilters().Where(c => c.PersonId == personId).ToListAsync(ct);
        var caseIds = cases.Select(c => c.Id).ToList();
        foreach (var dokCase in cases) dokCase.InterruptionReason = null;
        var notes = await _db.PastoralNotes.IgnoreQueryFilters().Where(n => caseIds.Contains(n.DokCaseId)).ToListAsync(ct);
        var noteIds = notes.Select(n => n.Id).ToList();
        await RemoveAttachmentsAsync(AttachmentOwnerType.PastoralNote, noteIds, blobs, ct);
        _db.PastoralNotes.RemoveRange(notes);
        _db.PastoralNoteShares.RemoveRange(await _db.PastoralNoteShares.IgnoreQueryFilters().Where(s => caseIds.Contains(s.DokCaseId)).ToListAsync(ct));
        foreach (var document in await _db.CaseDocuments.IgnoreQueryFilters().Where(d => caseIds.Contains(d.DokCaseId)).ToListAsync(ct))
        {
            if (document.BlobPath is not null) blobs.Add(document.BlobPath);
            document.BlobPath = null;
            document.OriginalFileName = null;
            document.ContentType = null;
            document.FileSizeBytes = null;
        }
        foreach (var meeting in await _db.Meetings.IgnoreQueryFilters().Where(m => m.DokCaseId != null && caseIds.Contains(m.DokCaseId.Value)).ToListAsync(ct))
            meeting.Notes = null;

        // SKŚP: powód zatrzymania i notatki w historii.
        var candidates = await _db.Candidates.IgnoreQueryFilters().Include(c => c.Events).Where(c => c.PersonId == personId).ToListAsync(ct);
        foreach (var candidate in candidates)
        {
            candidate.FormationStopNote = null;
            foreach (var formationEvent in candidate.Events) formationEvent.Note = null;
        }

        // Pisma i skany misji zawierają imię i nazwisko – pliki usuwamy.
        var generated = await _db.GeneratedDocuments.Where(d => d.PersonId == personId).ToListAsync(ct);
        blobs.AddRange(generated.Where(d => d.BlobPath != null).Select(d => d.BlobPath!));
        _db.GeneratedDocuments.RemoveRange(generated);
        // Zgłoszenia z formularzy (Tally) zawierają dane osobowe – znikają razem z danymi osoby.
        _db.FormSubmissions.RemoveRange(await _db.FormSubmissions.Where(f => f.PersonId == personId).ToListAsync(ct));
        var missionIds = await _db.CanonicalMissions.IgnoreQueryFilters().Where(m => m.PersonId == personId).Select(m => m.Id).ToListAsync(ct);
        await RemoveAttachmentsAsync(AttachmentOwnerType.Mission, missionIds, blobs, ct);

        foreach (var delivery in await _db.MailingDeliveries.Where(d => d.PersonId == personId).ToListAsync(ct))
            delivery.Email = "zanonimizowano";
        _db.MailingCampaignRecipients.RemoveRange(await _db.MailingCampaignRecipients
            .Where(r => r.PersonId == personId && r.Campaign!.Status == CampaignStatus.Draft).ToListAsync(ct));

        // Konto: wyłączone, login zastąpiony, otwarte sesje wygasają.
        foreach (var user in await _db.Users.Where(u => u.PersonId == personId).ToListAsync(ct))
        {
            var login = $"zanonimizowano-{user.Id[..8]}@usunieto.invalid";
            user.Email = login;
            user.NormalizedEmail = login.ToUpperInvariant();
            user.UserName = login;
            user.NormalizedUserName = login.ToUpperInvariant();
            user.PhoneNumber = null;
            user.IsDeactivated = true;
            user.CalendarFeedToken = null;
            user.SecurityStamp = Guid.NewGuid().ToString("N");
        }

        await _db.SaveChangesAsync(ct);

        foreach (var blob in blobs)
        {
            try
            {
                if (_storage is not null) await _storage.DeleteAsync(blob, ct);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Anonimizacja: nie udało się usunąć pliku {Blob}", blob);
            }
        }
        return true;
    }

    private async Task RemoveAttachmentsAsync(AttachmentOwnerType type, IReadOnlyCollection<Guid> ownerIds, List<string> blobs, CancellationToken ct)
    {
        if (ownerIds.Count == 0) return;
        var attachments = await _db.Attachments.Where(a => a.OwnerType == type && ownerIds.Contains(a.OwnerId)).ToListAsync(ct);
        blobs.AddRange(attachments.Select(a => a.BlobPath));
        _db.Attachments.RemoveRange(attachments);
    }
}
