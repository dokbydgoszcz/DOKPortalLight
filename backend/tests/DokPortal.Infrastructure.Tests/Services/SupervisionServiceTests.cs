using DokPortal.Application.Supervisions;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class SupervisionServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task CreateAsync_ThenGetAllAsync_FiltersByInstitution()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var service = new SupervisionService(db);

        await service.CreateAsync(new CreateSupervisionRequest
        {
            Institution = Institution.DOK, GroupLabel = "Grupa A", SupervisionDate = DateOnly.FromDateTime(DateTime.UtcNow)
        }, default);
        await service.CreateAsync(new CreateSupervisionRequest
        {
            Institution = Institution.SKSP, GroupLabel = "Grupa B", SupervisionDate = DateOnly.FromDateTime(DateTime.UtcNow)
        }, default);

        var dokOnly = await service.GetAllAsync(Institution.DOK, default);

        Assert.Single(dokOnly);
        Assert.Equal("Grupa A", dokOnly[0].GroupLabel);
    }

    [Fact]
    public async Task GetAllAsync_GetByIdAsync_AndUpdateAsync_ListTheAttachments()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var service = new SupervisionService(db);
        var created = await service.CreateAsync(new CreateSupervisionRequest
        {
            Institution = Institution.DOK, GroupLabel = "Grupa A", SupervisionDate = DateOnly.FromDateTime(DateTime.UtcNow)
        }, default);
        var empty = await service.CreateAsync(new CreateSupervisionRequest
        {
            Institution = Institution.DOK, GroupLabel = "Grupa B", SupervisionDate = DateOnly.FromDateTime(DateTime.UtcNow)
        }, default);
        Assert.Empty(created.Attachments);
        db.Attachments.Add(new DokPortal.Domain.Entities.Attachment
        {
            Id = Guid.NewGuid(), OwnerType = AttachmentOwnerType.Supervision, OwnerId = created.Id, FileName = "protokol.pdf",
            ContentType = "application/pdf", SizeBytes = 10, BlobPath = "x", UploadedByUserId = "u", UploadedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var all = await service.GetAllAsync(null, default);
        var byId = await service.GetByIdAsync(created.Id, default);
        var updated = await service.UpdateAsync(created.Id, new CreateSupervisionRequest
        {
            Institution = Institution.DOK, GroupLabel = "Grupa A2", SupervisionDate = DateOnly.FromDateTime(DateTime.UtcNow)
        }, default);

        Assert.Equal("protokol.pdf", all.Single(s => s.Id == created.Id).Attachments.Single().FileName);
        Assert.Empty(all.Single(s => s.Id == empty.Id).Attachments);
        Assert.Equal("protokol.pdf", byId!.Attachments.Single().FileName);
        Assert.Equal("protokol.pdf", updated!.Attachments.Single().FileName);
    }
}
