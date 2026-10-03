using DokPortal.Application.Mailing;
using DokPortal.Application.Parishes;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class ParishAndCampaignEditTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    // --- Parafie: edycja ---

    [Fact]
    public async Task Parish_UpdateAsync_ChangesNameAndCity()
    {
        await using var db = CreateContext();
        var service = new ParishService(db);
        var created = await service.CreateAsync(new CreateParishRequest { Name = "św. Jana", City = "Bydgoszcz" }, default);

        var updated = await service.UpdateAsync(created.Id, new UpdateParishRequest { Name = "św. Jana Chrzciciela", City = "Toruń" }, default);

        Assert.Equal("św. Jana Chrzciciela", updated!.Name);
        Assert.Equal("Toruń", updated.City);
        var stored = Assert.Single(await service.GetAllAsync(default));
        Assert.Equal("św. Jana Chrzciciela", stored.Name);
    }

    [Fact]
    public async Task Parish_UpdateAsync_ReturnsNull_ForUnknownOrDeletedParish()
    {
        await using var db = CreateContext();
        var service = new ParishService(db);
        var created = await service.CreateAsync(new CreateParishRequest { Name = "Usuwana" }, default);
        await service.DeleteAsync(created.Id, "admin", default);

        Assert.Null(await service.UpdateAsync(Guid.NewGuid(), new UpdateParishRequest { Name = "X" }, default));
        Assert.Null(await service.UpdateAsync(created.Id, new UpdateParishRequest { Name = "X" }, default));
    }

    // --- Kampanie: usuwanie szkicu ---

    private static async Task<MailingCampaign> AddCampaignAsync(AppDbContext db, CampaignStatus status)
    {
        var campaign = new MailingCampaign
        {
            Id = Guid.NewGuid(), Subject = "Temat", Body = "Treść", Group = MailingGroup.DokCases,
            Status = status, CreatedAtUtc = DateTime.UtcNow, SentAtUtc = status == CampaignStatus.Sent ? DateTime.UtcNow : null
        };
        db.MailingCampaigns.Add(campaign);
        await db.SaveChangesAsync();
        return campaign;
    }

    [Fact]
    public async Task Campaign_DeleteDraftAsync_RemovesADraft()
    {
        await using var db = CreateContext();
        var draft = await AddCampaignAsync(db, CampaignStatus.Draft);
        var service = new MailingService(db, new NullEmailSender());

        var deleted = await service.DeleteDraftAsync(draft.Id, default);

        Assert.True(deleted);
        Assert.Empty(await db.MailingCampaigns.ToListAsync());
    }

    [Fact]
    public async Task Campaign_DeleteDraftAsync_ReturnsFalse_ForUnknownCampaign()
    {
        await using var db = CreateContext();

        Assert.False(await new MailingService(db, new NullEmailSender()).DeleteDraftAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Campaign_DeleteDraftAsync_RefusesToDeleteASentCampaign()
    {
        await using var db = CreateContext();
        var sent = await AddCampaignAsync(db, CampaignStatus.Sent);
        var service = new MailingService(db, new NullEmailSender());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteDraftAsync(sent.Id, default));

        Assert.Contains("Wysłanej", ex.Message);
        Assert.Single(await db.MailingCampaigns.ToListAsync());
    }
}
