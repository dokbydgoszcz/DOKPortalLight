using DokPortal.Application.PastoralNotes;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class PastoralNoteServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    private static async Task<Guid> SeedCaseAsync(AppDbContext db)
    {
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var catechist = new Person { Id = Guid.NewGuid(), FirstName = "Anna", LastName = "Maj", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.AddRange(person, catechist);
        var dokCase = new DokCase
        {
            Id = Guid.NewGuid(), PersonId = person.Id, Path = DokPath.Confirmation, Stage = DokStage.Evangelization,
            CatechistPersonId = catechist.Id, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        db.DokCases.Add(dokCase);
        await db.SaveChangesAsync();
        return dokCase.Id;
    }

    [Fact]
    public async Task GetVisibleForCaseAsync_NonPrivilegedUser_SeesOnlyOwnNote()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var caseId = await SeedCaseAsync(db);
        var service = new PastoralNoteService(db);
        await service.CreateAsync(caseId, "author-a", new CreatePastoralNoteRequest { Content = "Notatka A" }, default);
        await service.CreateAsync(caseId, "author-b", new CreatePastoralNoteRequest { Content = "Notatka B" }, default);

        var visibleToA = await service.GetVisibleForCaseAsync(caseId, "author-a", isPrivileged: false, default);

        Assert.Single(visibleToA);
        Assert.Equal("Notatka A", visibleToA[0].Content);
    }

    [Fact]
    public async Task GetVisibleForCaseAsync_PrivilegedUser_SeesAllNotes()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var caseId = await SeedCaseAsync(db);
        var service = new PastoralNoteService(db);
        await service.CreateAsync(caseId, "author-a", new CreatePastoralNoteRequest { Content = "Notatka A" }, default);
        await service.CreateAsync(caseId, "author-b", new CreatePastoralNoteRequest { Content = "Notatka B" }, default);

        var visibleToDirector = await service.GetVisibleForCaseAsync(caseId, "director-user", isPrivileged: true, default);

        Assert.Equal(2, visibleToDirector.Count);
    }

    [Fact]
    public async Task GetAccessAsync_DistinguishesMissingHiddenAndVisibleNotes()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var caseId = await SeedCaseAsync(db);
        var otherCaseId = await SeedCaseAsync(db);
        var service = new PastoralNoteService(db);
        var note = await service.CreateAsync(caseId, "author-a", new CreatePastoralNoteRequest { Content = "A" }, default);

        Assert.Equal(PastoralNoteAccess.Visible, await service.GetAccessAsync(caseId, note.Id, "author-a", false, default));
        Assert.Equal(PastoralNoteAccess.Visible, await service.GetAccessAsync(caseId, note.Id, "director", true, default));
        Assert.Equal(PastoralNoteAccess.Hidden, await service.GetAccessAsync(caseId, note.Id, "author-b", false, default));
        Assert.Equal(PastoralNoteAccess.NotFound, await service.GetAccessAsync(caseId, Guid.NewGuid(), "author-a", false, default));
        Assert.Equal(PastoralNoteAccess.NotFound, await service.GetAccessAsync(otherCaseId, note.Id, "author-a", true, default));
    }

    [Fact]
    public async Task GetVisibleForCaseAsync_ListsTheAttachmentsOfEachNote()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var caseId = await SeedCaseAsync(db);
        var service = new PastoralNoteService(db);
        var withFiles = await service.CreateAsync(caseId, "author-a", new CreatePastoralNoteRequest { Content = "Ze skanem" }, default);
        var without = await service.CreateAsync(caseId, "author-a", new CreatePastoralNoteRequest { Content = "Bez" }, default);
        Assert.Empty(withFiles.Attachments);
        db.Attachments.Add(new Attachment
        {
            Id = Guid.NewGuid(), OwnerType = AttachmentOwnerType.PastoralNote, OwnerId = withFiles.Id, FileName = "kindle.pdf",
            ContentType = "application/pdf", SizeBytes = 10, BlobPath = "x", UploadedByUserId = "author-a", UploadedAtUtc = DateTime.UtcNow
        });
        db.Attachments.Add(new Attachment
        {
            Id = Guid.NewGuid(), OwnerType = AttachmentOwnerType.Supervision, OwnerId = without.Id, FileName = "obcy.pdf",
            ContentType = "application/pdf", SizeBytes = 10, BlobPath = "y", UploadedByUserId = "author-a", UploadedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var notes = await service.GetVisibleForCaseAsync(caseId, "author-a", false, default);

        Assert.Equal("kindle.pdf", notes.Single(n => n.Id == withFiles.Id).Attachments.Single().FileName);
        Assert.Empty(notes.Single(n => n.Id == without.Id).Attachments);
    }
}
