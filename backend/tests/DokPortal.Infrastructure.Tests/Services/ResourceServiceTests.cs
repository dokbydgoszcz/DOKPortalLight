using DokPortal.Application.Resources;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class ResourceServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CreateResourceRequest NewRequest(string title, string? description = null) => new() { Title = title, Description = description };

    [Fact]
    public async Task Create_ThenGet_ReturnsTheResource_WithNoFilesYet()
    {
        await using var db = CreateContext();
        var service = new ResourceService(db);

        var created = await service.CreateAsync(NewRequest("Konspekty spotkań", "Materiały na rok formacyjny"), "user-1", default);
        var loaded = await service.GetByIdAsync(created.Id, default);

        Assert.Equal(("Konspekty spotkań", "Materiały na rok formacyjny"), (loaded!.Title, loaded.Description));
        Assert.Empty(loaded.Files);
    }

    [Fact]
    public async Task List_ShowsResourcesAlphabetically_WithTheirFiles_AndSearchesTitleAndDescription()
    {
        await using var db = CreateContext();
        var service = new ResourceService(db);
        var b = await service.CreateAsync(NewRequest("Bierzmowanie – scenariusze"), "u", default);
        await service.CreateAsync(NewRequest("Chrzest dorosłych", "obrzędy katechumenatu"), "u", default);
        await service.CreateAsync(NewRequest("Anna – wzory pism"), "u", default);
        db.Attachments.Add(new Attachment
        {
            Id = Guid.NewGuid(), OwnerType = AttachmentOwnerType.Resource, OwnerId = b.Id, FileName = "scenariusz.pdf", ContentType = "application/pdf",
            SizeBytes = 10, BlobPath = "x", UploadedByUserId = "u", UploadedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var all = await service.ListAsync(null, default);
        var byTitle = await service.ListAsync("bierzm", default);
        var byDescription = await service.ListAsync("KATECHUMENATU", default);

        Assert.Equal(new[] { "Anna – wzory pism", "Bierzmowanie – scenariusze", "Chrzest dorosłych" }, all.Select(r => r.Title));
        Assert.Equal("scenariusz.pdf", Assert.Single(all.Single(r => r.Id == b.Id).Files).FileName);
        Assert.Equal(new[] { "Bierzmowanie – scenariusze" }, byTitle.Select(r => r.Title));
        Assert.Equal(new[] { "Chrzest dorosłych" }, byDescription.Select(r => r.Title));
    }

    [Fact]
    public async Task Update_ChangesTitleAndDescription_AndAnUnknownIdGivesNull()
    {
        await using var db = CreateContext();
        var service = new ResourceService(db);
        var created = await service.CreateAsync(NewRequest("Stary tytuł"), "u", default);

        var updated = await service.UpdateAsync(created.Id, new UpdateResourceRequest { Title = "Nowy tytuł", Description = "Opis" }, default);
        var missing = await service.UpdateAsync(Guid.NewGuid(), new UpdateResourceRequest { Title = "x" }, default);

        Assert.Equal(("Nowy tytuł", "Opis"), (updated!.Title, updated.Description));
        Assert.Null(missing);
    }

    [Fact]
    public async Task Delete_HidesTheResource_AndReportsAMissingOne()
    {
        await using var db = CreateContext();
        var service = new ResourceService(db);
        var created = await service.CreateAsync(NewRequest("Do usunięcia"), "u", default);

        var deleted = await service.DeleteAsync(created.Id, "u", default);
        var again = await service.DeleteAsync(created.Id, "u", default);

        Assert.True(deleted);
        Assert.False(again);
        Assert.Empty(await service.ListAsync(null, default));
        Assert.Null(await service.GetByIdAsync(created.Id, default));
    }
}
