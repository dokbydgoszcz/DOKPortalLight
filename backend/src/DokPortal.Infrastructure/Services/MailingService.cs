using DokPortal.Application.Common;
using DokPortal.Application.Mailing;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class MailingService : IMailingService
{
    /// <summary>Łączny rozmiar załączników kampanii – serwery poczty zwykle przyjmują do 25 MB z narzutem kodowania.</summary>
    public const long MaxAttachmentsBytes = 15 * 1024 * 1024;

    private readonly AppDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly IFileStorageService? _storage;

    private readonly TimeProvider _time;

    public MailingService(AppDbContext db, IEmailSender emailSender, TimeProvider? time = null, IFileStorageService? storage = null)
    {
        _db = db;
        _emailSender = emailSender;
        _time = time ?? TimeProvider.System;
        _storage = storage;
    }

    /// <summary>Katechiści: osoby z misją oraz te, które ukończyły formację i czekają na udzielenie posługi.</summary>
    private IQueryable<Guid> CatechistPersonIds()
    {
        var withMission = _db.CanonicalMissions.Select(m => m.PersonId);
        var awaiting = _db.Candidates.Completed().Select(c => c.PersonId);
        return withMission.Union(awaiting);
    }

    /// <summary>Osoby należące do grupy (dla własnej listy – podane osoby).</summary>
    private IQueryable<Guid> PersonIdsFor(MailingGroup group, IReadOnlyCollection<Guid>? personIds) => group switch
    {
        MailingGroup.CandidatesSksp => _db.Candidates.InFormation().Select(c => c.PersonId),
        MailingGroup.Missionaries => CatechistPersonIds(),
        MailingGroup.DokGraduates => _db.DokCases.Where(c => c.Stage == DokStage.Graduate).Select(c => c.PersonId),
        MailingGroup.DokCases => _db.DokCases.Where(c => c.InterruptedOn == null).Select(c => c.PersonId),
        MailingGroup.Pastors => _db.PersonFunctions.Where(f => f.Type == FunctionType.Pastor).Select(f => f.PersonId),
        MailingGroup.Formators => _db.Formators.Select(f => f.PersonId),
        MailingGroup.Custom => _db.People.Where(p => (personIds ?? Array.Empty<Guid>()).Contains(p.Id)).Select(p => p.Id),
        _ => throw new ArgumentOutOfRangeException(nameof(group))
    };

    public async Task<int> GetRecipientCountAsync(MailingGroup group, CancellationToken ct) => group switch
    {
        // Dotychczasowe grupy liczą członków grupy (np. sprawy), nowe – osoby.
        MailingGroup.CandidatesSksp => await _db.Candidates.InFormation().CountAsync(ct),
        MailingGroup.Missionaries => await CatechistPersonIds().CountAsync(ct),
        MailingGroup.DokGraduates => await _db.DokCases.CountAsync(c => c.Stage == DokStage.Graduate, ct),
        MailingGroup.DokCases => await _db.DokCases.CountAsync(c => c.InterruptedOn == null, ct),
        MailingGroup.Custom => 0,
        _ => await PersonIdsFor(group, null).Distinct().CountAsync(ct)
    };

    private async Task<int> CountForAsync(MailingGroup group, IReadOnlyCollection<Guid>? personIds, CancellationToken ct) =>
        group == MailingGroup.Custom ? await PersonIdsFor(group, personIds).CountAsync(ct) : await GetRecipientCountAsync(group, ct);

    public async Task<IReadOnlyList<MailingRecipientDto>> GetRecipientsAsync(MailingGroup group, IReadOnlyList<Guid>? personIds, CancellationToken ct)
    {
        var ids = PersonIdsFor(group, personIds);
        var people = await _db.People.AsNoTracking().Where(p => ids.Contains(p.Id))
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .Select(p => new { p.Id, p.FirstName, p.LastName, p.Email })
            .ToListAsync(ct);
        return people.Select(p => new MailingRecipientDto
        {
            PersonId = p.Id,
            FullName = $"{p.FirstName} {p.LastName}",
            Email = string.IsNullOrWhiteSpace(p.Email) ? null : p.Email
        }).ToList();
    }

    public async Task<IReadOnlyList<MailingRecipientDto>?> GetCampaignRecipientsAsync(Guid id, CancellationToken ct)
    {
        var campaign = await _db.MailingCampaigns.AsNoTracking().Include(c => c.Recipients).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (campaign is null) return null;
        return await GetRecipientsAsync(campaign.Group, campaign.Recipients.Select(r => r.PersonId).ToList(), ct);
    }

    public async Task<IReadOnlyList<MailingCampaignDto>> ListAsync(CancellationToken ct)
    {
        var campaigns = await _db.MailingCampaigns.AsNoTracking().Include(c => c.Recipients)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(ct);
        var attachments = await AttachmentLookup.ForOwnersAsync(_db, AttachmentOwnerType.MailingCampaign, campaigns.Select(c => c.Id).ToList(), ct);
        return campaigns.Select(c => ToDto(c, attachments.GetValueOrDefault(c.Id))).ToList();
    }

    /// <summary>Własna lista zawiera tylko istniejące osoby (bez powtórzeń); inne grupy listy nie mają.</summary>
    private async Task<List<Guid>> ValidPersonIdsAsync(CreateMailingCampaignRequest request, CancellationToken ct)
    {
        if (request.Group != MailingGroup.Custom) return new List<Guid>();
        var wanted = (request.PersonIds ?? Array.Empty<Guid>()).Distinct().ToList();
        var existing = await _db.People.Where(p => wanted.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct);
        if (existing.Count != wanted.Count) throw new InvalidOperationException("Na liście są osoby, których nie ma w bazie.");
        return existing;
    }

    public async Task<MailingCampaignDto> CreateAsync(CreateMailingCampaignRequest request, CancellationToken ct)
    {
        var personIds = await ValidPersonIdsAsync(request, ct);
        var campaign = new MailingCampaign
        {
            Id = Guid.NewGuid(),
            Subject = request.Subject.Trim(),
            Body = request.Body,
            Group = request.Group,
            RecipientCount = await CountForAsync(request.Group, personIds, ct),
            Status = CampaignStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            Recipients = personIds.Select(id => new MailingCampaignRecipient { PersonId = id }).ToList()
        };
        _db.MailingCampaigns.Add(campaign);
        await _db.SaveChangesAsync(ct);
        return ToDto(campaign, null);
    }

    public async Task<MailingCampaignDto?> UpdateDraftAsync(Guid id, CreateMailingCampaignRequest request, CancellationToken ct)
    {
        var campaign = await _db.MailingCampaigns.Include(c => c.Recipients).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (campaign is null) return null;
        if (campaign.Status != CampaignStatus.Draft)
            throw new InvalidOperationException("Wysłanej kampanii nie można zmienić – zostaje w historii tak, jak wyszła.");

        var personIds = await ValidPersonIdsAsync(request, ct);
        campaign.Subject = request.Subject.Trim();
        campaign.Body = request.Body;
        campaign.Group = request.Group;
        campaign.RecipientCount = await CountForAsync(request.Group, personIds, ct);
        campaign.UpdatedAtUtc = DateTime.UtcNow;
        _db.MailingCampaignRecipients.RemoveRange(campaign.Recipients.Where(r => !personIds.Contains(r.PersonId)));
        foreach (var personId in personIds.Where(pid => campaign.Recipients.All(r => r.PersonId != pid)))
        {
            _db.MailingCampaignRecipients.Add(new MailingCampaignRecipient { CampaignId = id, PersonId = personId });
        }
        await _db.SaveChangesAsync(ct);

        var attachments = await AttachmentLookup.ForOwnersAsync(_db, AttachmentOwnerType.MailingCampaign, new[] { id }, ct);
        return ToDto(await _db.MailingCampaigns.AsNoTracking().Include(c => c.Recipients).FirstAsync(c => c.Id == id, ct), attachments.GetValueOrDefault(id));
    }

    public async Task<bool?> IsDraftAsync(Guid id, CancellationToken ct)
    {
        var status = await _db.MailingCampaigns.Where(c => c.Id == id).Select(c => (CampaignStatus?)c.Status).FirstOrDefaultAsync(ct);
        return status is null ? null : status == CampaignStatus.Draft;
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

    /// <summary>Pliki kampanii wczytane do pamięci (wysyłane z każdą wiadomością).</summary>
    private async Task<List<EmailAttachment>> LoadAttachmentsAsync(Guid campaignId, CancellationToken ct)
    {
        var rows = await _db.Attachments.AsNoTracking()
            .Where(a => a.OwnerType == AttachmentOwnerType.MailingCampaign && a.OwnerId == campaignId)
            .OrderBy(a => a.UploadedAtUtc).ToListAsync(ct);
        if (rows.Count == 0) return new List<EmailAttachment>();
        if (_storage is null) throw new InvalidOperationException("Magazyn plików nie jest dostępny – nie można dołączyć załączników.");

        var files = new List<EmailAttachment>();
        foreach (var row in rows)
        {
            var stored = await _storage.DownloadAsync(row.BlobPath, ct)
                ?? throw new InvalidOperationException($"Nie znaleziono pliku „{row.FileName}” w magazynie – usuń go z kampanii i dodaj ponownie.");
            using var buffer = new MemoryStream();
            await stored.Content.CopyToAsync(buffer, ct);
            await stored.Content.DisposeAsync();
            files.Add(new EmailAttachment(row.FileName, row.ContentType, buffer.ToArray()));
        }
        return files;
    }

    public async Task<MailingCampaignDto?> SendAsync(Guid id, CancellationToken ct)
    {
        var campaign = await _db.MailingCampaigns.Include(c => c.Recipients).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (campaign is null) return null;

        if (campaign.Status == CampaignStatus.Sent)
        {
            throw new InvalidOperationException("Ta kampania została już wysłana.");
        }

        var attachments = await LoadAttachmentsAsync(id, ct);
        var personIds = PersonIdsFor(campaign.Group, campaign.Recipients.Select(r => r.PersonId).ToList());
        var recipientEmails = (await _db.People
                .Where(p => personIds.Contains(p.Id) && p.Email != null && p.Email != "")
                .Select(p => p.Email!)
                .Distinct()
                .ToListAsync(ct))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var delivered = 0;
        var failed = 0;
        string? firstError = null;
        foreach (var email in recipientEmails)
        {
            try
            {
                await _emailSender.SendAsync(email, campaign.Subject, campaign.Body, attachments, ct);
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
        var files = await AttachmentLookup.ForOwnersAsync(_db, AttachmentOwnerType.MailingCampaign, new[] { id }, ct);
        return ToDto(campaign, files.GetValueOrDefault(id));
    }

    private static MailingCampaignDto ToDto(MailingCampaign c, IReadOnlyList<DokPortal.Application.Attachments.AttachmentDto>? attachments) => new()
    {
        Id = c.Id,
        Subject = c.Subject,
        Body = c.Body,
        Group = c.Group,
        RecipientCount = c.RecipientCount,
        FailedCount = c.FailedCount,
        Status = c.Status,
        CreatedAtUtc = c.CreatedAtUtc,
        SentAtUtc = c.SentAtUtc,
        PersonIds = c.Recipients.Select(r => r.PersonId).ToList(),
        Attachments = attachments ?? (IReadOnlyList<DokPortal.Application.Attachments.AttachmentDto>)Array.Empty<DokPortal.Application.Attachments.AttachmentDto>()
    };
}
