using DokPortal.Application.Common;
using DokPortal.Application.Mailing;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class MailingService : IMailingService
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _emailSender;

    private readonly TimeProvider _time;

    public MailingService(AppDbContext db, IEmailSender emailSender, TimeProvider? time = null)
    {
        _db = db;
        _emailSender = emailSender;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Katechiści: osoby z misją oraz te, które ukończyły formację i czekają na udzielenie posługi.</summary>
    private IQueryable<Guid> CatechistPersonIds()
    {
        var withMission = _db.CanonicalMissions.Select(m => m.PersonId);
        var awaiting = _db.Candidates.Completed().Select(c => c.PersonId);
        return withMission.Union(awaiting);
    }

    public async Task<int> GetRecipientCountAsync(MailingGroup group, CancellationToken ct) => group switch
    {
        MailingGroup.CandidatesSksp => await _db.Candidates.InFormation().CountAsync(ct),
        MailingGroup.Missionaries => await CatechistPersonIds().CountAsync(ct),
        MailingGroup.DokGraduates => await _db.DokCases.CountAsync(c => c.Stage == DokStage.Graduate, ct),
        MailingGroup.DokCases => await _db.DokCases.CountAsync(ct),
        _ => throw new ArgumentOutOfRangeException(nameof(group))
    };

    public async Task<IReadOnlyList<MailingCampaignDto>> ListAsync(CancellationToken ct)
    {
        var campaigns = await _db.MailingCampaigns.AsNoTracking()
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(ct);
        return campaigns.Select(ToDto).ToList();
    }

    public async Task<MailingCampaignDto> CreateAsync(CreateMailingCampaignRequest request, CancellationToken ct)
    {
        var campaign = new MailingCampaign
        {
            Id = Guid.NewGuid(),
            Subject = request.Subject,
            Body = request.Body,
            Group = request.Group,
            RecipientCount = await GetRecipientCountAsync(request.Group, ct),
            Status = CampaignStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.MailingCampaigns.Add(campaign);
        await _db.SaveChangesAsync(ct);
        return ToDto(campaign);
    }

    public async Task SendTestAsync(string toEmail, CancellationToken ct)
    {
        try
        {
            await _emailSender.SendAsync(
                toEmail,
                "Wiadomość testowa z DOK Portal",
                "To jest wiadomość testowa. Jeśli ją czytasz, wysyłanie e-maili z DOK Portal działa.",
                ct);
        }
        catch (InvalidOperationException)
        {
            throw; // brak konfiguracji SMTP ma już czytelny komunikat
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Nie udało się wysłać wiadomości: {ex.Message}", ex);
        }
    }

    public async Task<bool> DeleteDraftAsync(Guid id, CancellationToken ct)
    {
        var campaign = await _db.MailingCampaigns.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (campaign is null) return false;
        if (campaign.Status != CampaignStatus.Draft)
        {
            throw new InvalidOperationException("Wysłanej kampanii nie można usunąć – zostaje w historii.");
        }

        _db.MailingCampaigns.Remove(campaign);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<MailingCampaignDto?> SendAsync(Guid id, CancellationToken ct)
    {
        var campaign = await _db.MailingCampaigns.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (campaign is null) return null;

        if (campaign.Status == CampaignStatus.Sent)
        {
            throw new InvalidOperationException("Ta kampania została już wysłana.");
        }

        var recipientEmails = (await GetRecipientEmailsAsync(campaign.Group, ct)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var delivered = 0;
        var failed = 0;
        string? firstError = null;
        foreach (var email in recipientEmails)
        {
            try
            {
                await _emailSender.SendAsync(email, campaign.Subject, campaign.Body, ct);
                delivered++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (InvalidOperationException) when (delivered == 0 && failed == 0)
            {
                throw; // brak konfiguracji SMTP (nic jeszcze nie poszło) ma własny, czytelny komunikat
            }
            catch (Exception ex)
            {
                failed++;
                firstError ??= ex.Message;
            }
        }

        if (delivered == 0 && failed > 0)
        {
            throw new InvalidOperationException($"Nie udało się wysłać żadnej wiadomości ({firstError}).");
        }

        campaign.RecipientCount = delivered;
        campaign.FailedCount = failed;
        campaign.Status = CampaignStatus.Sent;
        campaign.SentAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(campaign);
    }

    private Task<List<string>> GetRecipientEmailsAsync(MailingGroup group, CancellationToken ct)
    {
        var personIds = group switch
        {
            MailingGroup.CandidatesSksp => _db.Candidates.InFormation().Select(c => c.PersonId),
            MailingGroup.Missionaries => CatechistPersonIds(),
            MailingGroup.DokGraduates => _db.DokCases.Where(c => c.Stage == DokStage.Graduate).Select(c => c.PersonId),
            MailingGroup.DokCases => _db.DokCases.Select(c => c.PersonId),
            _ => throw new ArgumentOutOfRangeException(nameof(group))
        };

        return _db.People
            .Where(p => personIds.Contains(p.Id) && p.Email != null && p.Email != "")
            .Select(p => p.Email!)
            .Distinct()
            .ToListAsync(ct);
    }

    private static MailingCampaignDto ToDto(MailingCampaign c) => new()
    {
        Id = c.Id,
        Subject = c.Subject,
        Body = c.Body,
        Group = c.Group,
        RecipientCount = c.RecipientCount,
        FailedCount = c.FailedCount,
        Status = c.Status,
        CreatedAtUtc = c.CreatedAtUtc,
        SentAtUtc = c.SentAtUtc
    };
}
