using DokPortal.Application.Common;
using DokPortal.Application.Reminders;
using DokPortal.Domain.Constants;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DokPortal.Infrastructure.Services;

public class ReminderService : IReminderService
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<ReminderService> _logger;

    public ReminderService(AppDbContext db, IEmailSender emailSender, ILogger<ReminderService> logger)
    {
        _db = db;
        _emailSender = emailSender;
        _logger = logger;
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
                        _logger.LogWarning(ex, "Nie udało się wysłać podsumowania brakujących dokumentów do {Email}", email);
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
}
