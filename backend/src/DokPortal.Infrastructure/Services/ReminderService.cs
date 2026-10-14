using DokPortal.Application.Common;
using DokPortal.Application.NameDays;
using DokPortal.Application.Reminders;
using DokPortal.Domain.Constants;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DokPortal.Infrastructure.Services;

public class ReminderService : IReminderService
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<ReminderService> _logger;
    private readonly INameDayService _nameDayService;

    public ReminderService(AppDbContext db, IEmailSender emailSender, ILogger<ReminderService> logger, INameDayService nameDayService)
    {
        _db = db;
        _emailSender = emailSender;
        _logger = logger;
        _nameDayService = nameDayService;
    }

    private sealed record MissingDocumentRow(
        Guid DocumentId,
        string DocumentName,
        Guid DokCaseId,
        string PersonFirstName,
        string PersonLastName,
        string? CatechistEmail);

    public async Task<MissingDocumentsReminderResultDto> RunMissingDocumentsReminderAsync(CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-7);

        var rows = await (
            from doc in _db.CaseDocuments
            join dokCase in _db.DokCases on doc.DokCaseId equals dokCase.Id
            join person in _db.People on dokCase.PersonId equals person.Id
            join catechist in _db.People on dokCase.CatechistPersonId equals catechist.Id
            where !doc.IsProvided && (doc.LastReminderSentAtUtc == null || doc.LastReminderSentAtUtc <= cutoff)
            where dokCase.Stage != DokStage.Graduate && dokCase.InterruptedOn == null
            select new MissingDocumentRow(doc.Id, doc.Name, dokCase.Id, person.FirstName, person.LastName, catechist.Email)
        ).ToListAsync(ct);

        var byCase = rows.GroupBy(r => r.DokCaseId).ToList();

        var emailsSentToCatechists = 0;
        var failedSends = 0;
        var documentIdsToStamp = new List<Guid>();
        var digestLines = new List<string>();

        foreach (var group in byCase)
        {
            var first = group.First();
            var personFullName = $"{first.PersonFirstName} {first.PersonLastName}";
            var documentNames = group.Select(r => r.DocumentName).ToList();
            digestLines.Add($"{personFullName}: {string.Join(", ", documentNames)}");

            if (string.IsNullOrWhiteSpace(first.CatechistEmail))
            {
                continue;
            }

            var body = $"Przypomnienie: w sprawie DOK podopiecznego {personFullName} brakuje następujących dokumentów:\n- {string.Join("\n- ", documentNames)}";
            try
            {
                await _emailSender.SendAsync(first.CatechistEmail!, "Brakujące dokumenty — przypomnienie", body, ct);
                emailsSentToCatechists++;
                documentIdsToStamp.AddRange(group.Select(r => r.DocumentId));
            }
            catch (Exception ex)
            {
                failedSends++;
                _logger.LogWarning(ex, "Nie udało się wysłać przypomnienia o brakujących dokumentach do katechisty dla sprawy {DokCaseId}", first.DokCaseId);
            }
        }

        if (documentIdsToStamp.Count > 0)
        {
            var now = DateTime.UtcNow;
            var documentsToStamp = await _db.CaseDocuments
                .Where(d => documentIdsToStamp.Contains(d.Id))
                .ToListAsync(ct);
            foreach (var document in documentsToStamp)
            {
                document.LastReminderSentAtUtc = now;
            }
            await _db.SaveChangesAsync(ct);
        }

        var directorsSummarySent = false;
        if (digestLines.Count > 0)
        {
            var directorEmails = await (
                from userRole in _db.UserRoles
                join role in _db.Roles on userRole.RoleId equals role.Id
                join user in _db.Users on userRole.UserId equals user.Id
                where role.Name == AppRoles.DyrektorDOK && user.Email != null && user.Email != ""
                select user.Email!
            ).Distinct().ToListAsync(ct);

            if (directorEmails.Count > 0)
            {
                var digestBody = "Podsumowanie brakujących dokumentów w sprawach DOK:\n\n" + string.Join("\n", digestLines);
                foreach (var email in directorEmails)
                {
                    try
                    {
                        await _emailSender.SendAsync(email, "Brakujące dokumenty — podsumowanie tygodniowe", digestBody, ct);
                        directorsSummarySent = true;
                    }
                    catch (Exception ex)
                    {
                        failedSends++;
                        _logger.LogWarning(ex, "Nie udało się wysłać podsumowania brakujących dokumentów do jednego z dyrektorów");
                    }
                }
            }
        }

        return new MissingDocumentsReminderResultDto
        {
            CasesProcessed = byCase.Count,
            EmailsSentToCatechists = emailsSentToCatechists,
            DirectorsSummarySent = directorsSummarySent,
            FailedSends = failedSends
        };
    }

    private sealed record UpcomingMeetingRow(
        Guid MeetingId,
        DateOnly MeetingDate,
        TimeOnly? StartTime,
        string PersonFirstName,
        string PersonLastName,
        string? CatechistEmail);

    public async Task<UpcomingMeetingsReminderResultDto> RunUpcomingMeetingsReminderAsync(CancellationToken ct)
    {
        // Okno „dziś–jutro”: spóźniony przebieg (opóźniony cron) nadal przypomni o dzisiejszym spotkaniu.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tomorrow = today.AddDays(1);

        var rows = await (
            from meeting in _db.Meetings
            where meeting.DokCaseId != null && meeting.MeetingDate >= today && meeting.MeetingDate <= tomorrow && meeting.ReminderSentAtUtc == null
            join dokCase in _db.DokCases on meeting.DokCaseId equals dokCase.Id
            where dokCase.Stage != DokStage.Graduate && dokCase.InterruptedOn == null
            join person in _db.People on dokCase.PersonId equals person.Id
            join catechist in _db.People on dokCase.CatechistPersonId equals catechist.Id
            select new UpcomingMeetingRow(meeting.Id, meeting.MeetingDate, meeting.StartTime, person.FirstName, person.LastName, catechist.Email)
        ).ToListAsync(ct);

        var emailsSentToCatechists = 0;
        var failedSends = 0;
        var meetingIdsToStamp = new List<Guid>();

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.CatechistEmail))
            {
                continue;
            }

            var personFullName = $"{row.PersonFirstName} {row.PersonLastName}";
            var when = row.MeetingDate == today ? "dziś" : "jutro";
            var hour = row.StartTime is { } t ? $" o {t:HH\\:mm}" : "";
            var body = $"Przypomnienie: {when} ({row.MeetingDate:yyyy-MM-dd}){hour} odbywa się spotkanie z podopiecznym {personFullName}.";
            try
            {
                await _emailSender.SendAsync(row.CatechistEmail!, "Nadchodzące spotkanie — przypomnienie", body, ct);
                emailsSentToCatechists++;
                meetingIdsToStamp.Add(row.MeetingId);
            }
            catch (Exception ex)
            {
                failedSends++;
                _logger.LogWarning(ex, "Nie udało się wysłać przypomnienia o spotkaniu {MeetingId} do katechisty", row.MeetingId);
            }
        }

        if (meetingIdsToStamp.Count > 0)
        {
            var now = DateTime.UtcNow;
            var meetingsToStamp = await _db.Meetings
                .Where(m => meetingIdsToStamp.Contains(m.Id))
                .ToListAsync(ct);
            foreach (var meeting in meetingsToStamp)
            {
                meeting.ReminderSentAtUtc = now;
            }
            await _db.SaveChangesAsync(ct);
        }

        return new UpcomingMeetingsReminderResultDto
        {
            MeetingsProcessed = rows.Count,
            EmailsSentToCatechists = emailsSentToCatechists,
            FailedSends = failedSends
        };
    }

    public async Task<UpcomingNameDaysReminderResultDto> RunUpcomingNameDaysReminderAsync(CancellationToken ct)
    {
        var upcoming = await _nameDayService.GetUpcomingAsync(7, ct);

        if (upcoming.Count == 0)
        {
            return new UpcomingNameDaysReminderResultDto
            {
                NameDaysFound = 0,
                RecipientsNotified = 0,
                FailedSends = 0
            };
        }

        var lines = upcoming.Select(d => $"- {d.FullName} — {d.NameDayMonth:D2}.{d.NameDayDay:D2} (za {d.DaysUntil} dni)");
        var body = "Imieniny w najbliższym tygodniu:\n" + string.Join("\n", lines);

        // Lista imienin to dane osób: dostają ją tylko aktywne konta z dostępem do Osób (administrator albo rola z People.View/Manage).
        var rolesWithPeople = _db.RolePermissions
            .Where(p => p.Permission == Permissions.PeopleView || p.Permission == Permissions.PeopleManage)
            .Select(p => p.RoleName);
        var recipientEmails = await (
            from user in _db.Users
            where user.Email != null && user.Email != "" && !user.IsDeactivated
            join userRole in _db.UserRoles on user.Id equals userRole.UserId
            join role in _db.Roles on userRole.RoleId equals role.Id
            where role.Name == AppRoles.Administrator || rolesWithPeople.Contains(role.Name!)
            select user.Email!
        ).Distinct().ToListAsync(ct);

        var recipientsNotified = 0;
        var failedSends = 0;
        foreach (var email in recipientEmails)
        {
            try
            {
                await _emailSender.SendAsync(email, "Nadchodzące imieniny", body, ct);
                recipientsNotified++;
            }
            catch (Exception ex)
            {
                failedSends++;
                _logger.LogWarning(ex, "Nie udało się wysłać przypomnienia o imieninach do jednego z adresatów");
            }
        }

        return new UpcomingNameDaysReminderResultDto
        {
            NameDaysFound = upcoming.Count,
            RecipientsNotified = recipientsNotified,
            FailedSends = failedSends
        };
    }
}
