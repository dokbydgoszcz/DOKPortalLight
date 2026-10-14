using DokPortal.Application.Common;
using DokPortal.Application.Mailing;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace DokPortal.Infrastructure.Services;

public class MailingService : IMailingService
{
    /// <summary>Łączny rozmiar załączników kampanii – serwery poczty zwykle przyjmują do 25 MB z narzutem kodowania.</summary>
    public const long MaxAttachmentsBytes = 15 * 1024 * 1024;

    private readonly AppDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly IFileStorageService? _storage;

    private readonly TimeProvider _time;
    private readonly MailingOptions _options;
    private readonly PortalUrl? _portalUrl;

    public MailingService(AppDbContext db, IEmailSender emailSender, TimeProvider? time = null, IFileStorageService? storage = null,
        MailingOptions? options = null, PortalUrl? portalUrl = null)
    {
        _options = options ?? new MailingOptions();
        _portalUrl = portalUrl;
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
            .Select(p => new { p.Id, p.FirstName, p.LastName, p.Email, p.MailingOptOutAtUtc })
            .ToListAsync(ct);
        return people.Select(p => new MailingRecipientDto
        {
            PersonId = p.Id,
            FullName = $"{p.FirstName} {p.LastName}",
            Email = string.IsNullOrWhiteSpace(p.Email) ? null : p.Email,
            OptedOut = p.MailingOptOutAtUtc != null
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
        var pending = await _db.MailingDeliveries.Where(d => d.Status == MailingDeliveryStatus.Pending)
            .GroupBy(d => d.CampaignId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, ct);
        return campaigns.Select(c => ToDto(c, attachments.GetValueOrDefault(c.Id), pending.GetValueOrDefault(c.Id))).ToList();
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

    /// <summary>Po tylu nieudanych próbach adres uznajemy za błędny (wcześniejsze błędy mogą być chwilowe, np. „spróbuj później”).</summary>
    private const int MaxAttempts = 3;

    public async Task<MailingCampaignDto?> SendAsync(Guid id, CancellationToken ct)
    {
        var campaign = await _db.MailingCampaigns.Include(c => c.Recipients).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (campaign is null) return null;

        if (campaign.Status == CampaignStatus.Sent)
        {
            throw new InvalidOperationException("Ta kampania została już wysłana.");
        }
        if (campaign.Status == CampaignStatus.Sending)
        {
            throw new InvalidOperationException("Ta kampania jest już w trakcie wysyłki.");
        }

        var attachments = await LoadAttachmentsAsync(id, ct);
        var personIds = PersonIdsFor(campaign.Group, campaign.Recipients.Select(r => r.PersonId).ToList());
        var people = await _db.People
            .Where(p => personIds.Contains(p.Id) && p.Email != null && p.Email != "" && p.MailingOptOutAtUtc == null)
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .Select(p => new { p.Id, Email = p.Email! })
            .ToListAsync(ct);
        var deliveries = people
            .DistinctBy(p => p.Email.Trim().ToLowerInvariant())
            .Select((p, index) => new MailingDelivery
            {
                Id = Guid.NewGuid(), CampaignId = id, PersonId = p.Id, Email = p.Email.Trim(), Status = MailingDeliveryStatus.Pending, Position = index
            })
            .ToList();
        _db.MailingDeliveries.AddRange(deliveries);
        campaign.Status = CampaignStatus.Sending;
        campaign.RecipientCount = 0;
        campaign.FailedCount = 0;
        await _db.SaveChangesAsync(ct);

        // Pierwsze wiadomości od razu: błąd ustawień poczty (albo złe hasło) ma wyjść przy kliknięciu „Wyślij”, a nie po cichu w tle.
        var delivered = 0;
        string? firstError = null;
        var tried = 0;
        if (await RemainingTodayAsync(ct) > 0)
        {
            foreach (var delivery in deliveries.Take(MaxAttempts))
            {
                tried++;
                try
                {
                    await DeliverAsync(campaign, delivery, attachments, ct);
                    delivered++;
                    break;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    if (ex is InvalidOperationException && tried == 1)
                    {
                        await RollBackQueueAsync(campaign, deliveries, ct);
                        throw; // brak konfiguracji SMTP ma własny, czytelny komunikat
                    }
                    firstError ??= ex.Message;
                }
            }
            if (delivered == 0 && tried > 0)
            {
                await RollBackQueueAsync(campaign, deliveries, ct);
                throw new InvalidOperationException($"Nie udało się wysłać żadnej wiadomości ({firstError}).");
            }
        }

        await FinishIfDoneAsync(campaign, ct);
        return await GetDtoAsync(campaign, ct);
    }

    /// <summary>Nic nie wyszło – kampania wraca do szkicu, kolejka znika.</summary>
    private async Task RollBackQueueAsync(MailingCampaign campaign, List<MailingDelivery> deliveries, CancellationToken ct)
    {
        _db.MailingDeliveries.RemoveRange(deliveries);
        campaign.Status = CampaignStatus.Draft;
        campaign.RecipientCount = 0;
        campaign.FailedCount = 0;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<int> RemainingTodayAsync(CancellationToken ct)
    {
        var since = _time.GetUtcNow().UtcDateTime.AddHours(-24);
        var used = await _db.MailingDeliveries.CountAsync(d => d.AttemptedAtUtc != null && d.AttemptedAtUtc > since, ct);
        return Math.Max(0, _options.DailyLimit - used);
    }

    /// <summary>Wysyła jedną wiadomość i zapisuje wynik od razu (restart w połowie nie wyśle jej drugi raz).</summary>
    private async Task DeliverAsync(MailingCampaign campaign, MailingDelivery delivery, IReadOnlyList<EmailAttachment> attachments, CancellationToken ct)
    {
        delivery.Attempts++;
        delivery.AttemptedAtUtc = _time.GetUtcNow().UtcDateTime;
        try
        {
            var body = campaign.Body + await FooterAsync(delivery, ct);
            await _emailSender.SendAsync(delivery.Email, campaign.Subject, body, attachments, ct);
            delivery.Status = MailingDeliveryStatus.Sent;
            delivery.LastError = null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            delivery.LastError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            if (delivery.Attempts >= MaxAttempts) delivery.Status = MailingDeliveryStatus.Failed;
            await _db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        await _db.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Stopka z linkiem „wypisz się” (osobny dla każdej osoby).</summary>
    private async Task<string> FooterAsync(MailingDelivery delivery, CancellationToken ct)
    {
        var footer = "\n\n—\nWiadomość z DOK Portal – Diecezjalny Ośrodek Katechumenalny i Szkoła Katechistów św. Pawła w Bydgoszczy.";
        if (delivery.PersonId is not { } personId || _portalUrl is null || string.IsNullOrWhiteSpace(_portalUrl.BaseUrl)) return footer;
        var person = await _db.People.FirstOrDefaultAsync(p => p.Id == personId, ct);
        if (person is null) return footer;
        if (person.MailingUnsubscribeToken is null)
        {
            person.MailingUnsubscribeToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(20)).ToLowerInvariant();
        }
        return footer + $"\nNie chcesz dostawać takich wiadomości? Wypisz się: {_portalUrl.Link("wypisz/" + person.MailingUnsubscribeToken)}";
    }

    /// <summary>Kampania bez oczekujących wiadomości staje się „wysłana” z policzonym wynikiem.</summary>
    private async Task FinishIfDoneAsync(MailingCampaign campaign, CancellationToken ct)
    {
        var counts = await _db.MailingDeliveries.Where(d => d.CampaignId == campaign.Id)
            .GroupBy(d => d.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        int Count(MailingDeliveryStatus status) => counts.FirstOrDefault(c => c.Key == status)?.Count ?? 0;
        campaign.RecipientCount = Count(MailingDeliveryStatus.Sent);
        campaign.FailedCount = Count(MailingDeliveryStatus.Failed);
        if (Count(MailingDeliveryStatus.Pending) == 0)
        {
            campaign.Status = CampaignStatus.Sent;
            campaign.SentAtUtc = _time.GetUtcNow().UtcDateTime;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<MailingQueueResultDto> ProcessQueueAsync(CancellationToken ct)
    {
        var sent = 0;
        var failed = 0;
        var consecutiveFailures = 0;
        var remaining = await RemainingTodayAsync(ct);
        var attachmentCache = new Dictionary<Guid, IReadOnlyList<EmailAttachment>>();
        var touched = new Dictionary<Guid, MailingCampaign>();
        // Kampanie, które zostały „w trakcie” bez kolejki (np. wszystkie adresy wypisane), też trzeba domknąć.
        foreach (var stale in await _db.MailingCampaigns.Where(c => c.Status == CampaignStatus.Sending).ToListAsync(ct))
            touched[stale.Id] = stale;

        while (remaining > 0 && consecutiveFailures < MaxAttempts)
        {
            var delivery = await _db.MailingDeliveries
                .Where(d => d.Status == MailingDeliveryStatus.Pending && d.Campaign!.Status == CampaignStatus.Sending)
                .OrderBy(d => d.Attempts).ThenBy(d => d.Campaign!.CreatedAtUtc).ThenBy(d => d.Position)
                .FirstOrDefaultAsync(ct);
            if (delivery is null) break;
            var campaign = touched.TryGetValue(delivery.CampaignId, out var known)
                ? known
                : touched[delivery.CampaignId] = (await _db.MailingCampaigns.FirstAsync(c => c.Id == delivery.CampaignId, ct));

            if (delivery.PersonId is { } personId &&
                await _db.People.AnyAsync(p => p.Id == personId && p.MailingOptOutAtUtc != null, ct))
            {
                delivery.Status = MailingDeliveryStatus.Skipped;
                await _db.SaveChangesAsync(ct);
                continue;
            }

            if (!attachmentCache.TryGetValue(campaign.Id, out var attachments))
            {
                attachments = await LoadAttachmentsAsync(campaign.Id, ct);
                attachmentCache[campaign.Id] = attachments;
            }

            if (sent + failed > 0 && _options.DelayBetweenMessages > TimeSpan.Zero)
                await Task.Delay(_options.DelayBetweenMessages, ct);
            remaining--;
            try
            {
                await DeliverAsync(campaign, delivery, attachments, ct);
                sent++;
                consecutiveFailures = 0;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (InvalidOperationException) when (sent == 0)
            {
                // Poczta nie jest skonfigurowana – nie ma sensu próbować dalej (próba się nie liczy).
                delivery.Attempts--;
                delivery.Status = MailingDeliveryStatus.Pending;
                await _db.SaveChangesAsync(ct);
                break;
            }
            catch (Exception)
            {
                failed++;
                consecutiveFailures++;
            }
        }

        foreach (var campaign in touched.Values) await FinishIfDoneAsync(campaign, ct);
        var pending = await _db.MailingDeliveries.CountAsync(d => d.Status == MailingDeliveryStatus.Pending && d.Campaign!.Status == CampaignStatus.Sending, ct);
        return new MailingQueueResultDto
        {
            Sent = sent,
            Failed = failed,
            Pending = pending,
            DailyLimit = _options.DailyLimit,
            SentLast24h = _options.DailyLimit - await RemainingTodayAsync(ct)
        };
    }

    public async Task<MailingSubscriptionDto?> GetSubscriptionAsync(string token, CancellationToken ct)
    {
        var person = await FindByUnsubscribeTokenAsync(token, ct);
        return person is null ? null : new MailingSubscriptionDto { FirstName = person.FirstName, OptedOut = person.MailingOptOutAtUtc != null };
    }

    public async Task<MailingSubscriptionDto?> SetOptOutAsync(string token, bool optOut, CancellationToken ct)
    {
        var person = await FindByUnsubscribeTokenAsync(token, ct);
        if (person is null) return null;
        person.MailingOptOutAtUtc = optOut ? person.MailingOptOutAtUtc ?? _time.GetUtcNow().UtcDateTime : null;
        await _db.SaveChangesAsync(ct);
        return new MailingSubscriptionDto { FirstName = person.FirstName, OptedOut = optOut };
    }

    private async Task<Person?> FindByUnsubscribeTokenAsync(string token, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(token) || token.Length > 64
            ? null
            : await _db.People.FirstOrDefaultAsync(p => p.MailingUnsubscribeToken == token, ct);

    private async Task<MailingCampaignDto> GetDtoAsync(MailingCampaign campaign, CancellationToken ct)
    {
        var files = await AttachmentLookup.ForOwnersAsync(_db, AttachmentOwnerType.MailingCampaign, new[] { campaign.Id }, ct);
        var pending = await _db.MailingDeliveries.CountAsync(d => d.CampaignId == campaign.Id && d.Status == MailingDeliveryStatus.Pending, ct);
        return ToDto(campaign, files.GetValueOrDefault(campaign.Id), pending);
    }

    private static MailingCampaignDto ToDto(MailingCampaign c, IReadOnlyList<DokPortal.Application.Attachments.AttachmentDto>? attachments, int pending = 0) => new()
    {
        PendingCount = pending,
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
