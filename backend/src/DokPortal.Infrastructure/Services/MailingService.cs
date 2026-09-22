using DokPortal.Application.Mailing;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class MailingService : IMailingService
{
    private readonly AppDbContext _db;

    public MailingService(AppDbContext db) => _db = db;

    public async Task<int> GetRecipientCountAsync(MailingGroup group, CancellationToken ct) => group switch
    {
        MailingGroup.CandidatesSksp => await _db.Candidates.CountAsync(ct),
        MailingGroup.Missionaries => await _db.CanonicalMissions.Select(m => m.PersonId).Distinct().CountAsync(ct),
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

    public async Task<MailingCampaignDto?> SendAsync(Guid id, CancellationToken ct)
    {
        var campaign = await _db.MailingCampaigns.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (campaign is null) return null;

        campaign.RecipientCount = await GetRecipientCountAsync(campaign.Group, ct);
        campaign.Status = CampaignStatus.Sent;
        campaign.SentAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(campaign);
    }

    private static MailingCampaignDto ToDto(MailingCampaign c) => new()
    {
        Id = c.Id,
        Subject = c.Subject,
        Body = c.Body,
        Group = c.Group,
        RecipientCount = c.RecipientCount,
        Status = c.Status,
        CreatedAtUtc = c.CreatedAtUtc,
        SentAtUtc = c.SentAtUtc
    };
}
