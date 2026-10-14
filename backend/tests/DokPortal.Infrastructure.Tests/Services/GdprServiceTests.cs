using System.Text.Json;
using DokPortal.Application.Common;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Identity;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

/// <summary>RODO: kopia danych osoby i usunięcie danych przez anonimizację.</summary>
public class GdprServiceTests
{
    private sealed class Storage : IFileStorageService
    {
        public HashSet<string> Files { get; } = new();
        public bool FailDeletes { get; init; }
        public Task UploadAsync(string blobPath, Stream content, string contentType, CancellationToken ct) { Files.Add(blobPath); return Task.CompletedTask; }
        public Task<StoredFile?> DownloadAsync(string blobPath, CancellationToken ct) => Task.FromResult<StoredFile?>(null);
        public Task DeleteAsync(string blobPath, CancellationToken ct)
        {
            if (FailDeletes) throw new IOException("magazyn niedostępny");
            Files.Remove(blobPath);
            return Task.CompletedTask;
        }
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Person NewPerson(string first, string last, string? email = null) => new()
    {
        Id = Guid.NewGuid(), FirstName = first, LastName = last, Email = email, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
    };

    private sealed record Seed(Person Student, Person Catechist, DokCase Case, Candidate Candidate);

    private static async Task<Seed> SeedAsync(AppDbContext db, Storage storage)
    {
        var parish = new Parish { Id = Guid.NewGuid(), Name = "św. Mateusza" };
        var student = NewPerson("Jan", "Kowalski", "jan@example.org");
        student.Phone = "600 100 200";
        student.BirthDate = new DateOnly(1990, 5, 1);
        student.ParishId = parish.Id;
        student.Notes = "Uwaga o osobie";
        student.NameDayMonth = 6;
        student.NameDayDay = 24;
        student.PhotoBlobPath = "photos/jan.jpg";
        student.PhotoContentType = "image/jpeg";
        student.PhotoUpdatedAtUtc = DateTime.UtcNow;
        student.MailingUnsubscribeToken = "tok";
        student.Functions.Add(new PersonFunction { Id = Guid.NewGuid(), PersonId = student.Id, Type = FunctionType.Catechist, Notes = "notatka funkcji" });
        var catechist = NewPerson("Anna", "Maj", "anna@example.org");
        db.Parishes.Add(parish);
        db.People.AddRange(student, catechist);
        var dokCase = new DokCase
        {
            Id = Guid.NewGuid(), PersonId = student.Id, CatechistPersonId = catechist.Id, Path = DokPath.Confirmation, Stage = DokStage.Catechumenate,
            StageSinceUtc = DateTime.UtcNow, InterruptionReason = "sprawy rodzinne", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        db.DokCases.Add(dokCase);
        db.DokCaseEvents.Add(new DokCaseEvent { Id = Guid.NewGuid(), DokCaseId = dokCase.Id, Sequence = 1, Kind = DokCaseEventKind.StageChanged, FromValue = "Evangelization", ToValue = "Catechumenate", AtUtc = DateTime.UtcNow });
        var note = new PastoralNote { Id = Guid.NewGuid(), DokCaseId = dokCase.Id, AuthorUserId = "u", Content = "TAJNA-TRESC", CreatedAtUtc = DateTime.UtcNow };
        db.PastoralNotes.Add(note);
        db.PastoralNoteShares.Add(new PastoralNoteShare { Id = Guid.NewGuid(), DokCaseId = dokCase.Id, AuthorUserId = "u", SharedWithPersonId = catechist.Id, SharedAtUtc = DateTime.UtcNow });
        db.Attachments.Add(new Attachment { Id = Guid.NewGuid(), OwnerType = AttachmentOwnerType.PastoralNote, OwnerId = note.Id, FileName = "n.pdf", ContentType = "application/pdf", BlobPath = "notes/n.pdf", UploadedByUserId = "u", UploadedAtUtc = DateTime.UtcNow });
        db.CaseDocuments.Add(new CaseDocument { Id = Guid.NewGuid(), DokCaseId = dokCase.Id, Name = "Metryka chrztu", IsProvided = true, BlobPath = "cases/metryka.pdf", OriginalFileName = "metryka.pdf", ContentType = "application/pdf", FileSizeBytes = 10, CreatedAtUtc = DateTime.UtcNow });
        db.Meetings.Add(new Meeting { Id = Guid.NewGuid(), DokCaseId = dokCase.Id, MeetingDate = new DateOnly(2026, 10, 1), IsAttended = true, Notes = "rozmowa", CreatedAtUtc = DateTime.UtcNow });
        var candidate = new Candidate { Id = Guid.NewGuid(), PersonId = student.Id, FormationYear = 2, FormationStopNote = "choroba", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        candidate.Events.Add(new CandidateFormationEvent { Id = Guid.NewGuid(), CandidateId = candidate.Id, Sequence = 1, Kind = CandidateFormationEventKind.Enrolled, AtUtc = DateTime.UtcNow, Note = "notatka" });
        db.Candidates.Add(candidate);
        var mission = new CanonicalMission { Id = Guid.NewGuid(), PersonId = student.Id, ServicePlace = "św. Mateusza", MissionStartDate = new DateOnly(2025, 9, 1), MissionEndDate = new DateOnly(2028, 8, 31), CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.CanonicalMissions.Add(mission);
        db.Attachments.Add(new Attachment { Id = Guid.NewGuid(), OwnerType = AttachmentOwnerType.Mission, OwnerId = mission.Id, FileName = "dekret.pdf", ContentType = "application/pdf", BlobPath = "missions/dekret.pdf", UploadedByUserId = "u", UploadedAtUtc = DateTime.UtcNow });
        db.GeneratedDocuments.Add(new GeneratedDocument { Id = Guid.NewGuid(), PersonId = student.Id, Template = DocumentTemplate.LetterToBishop, GeneratedByUserId = "u", BlobPath = "docs/list.docx", AdditionalNotes = "uwagi", CreatedAtUtc = DateTime.UtcNow });
        var campaign = new MailingCampaign { Id = Guid.NewGuid(), Subject = "Zaproszenie", Body = "B", Status = CampaignStatus.Sent, CreatedAtUtc = DateTime.UtcNow };
        var draft = new MailingCampaign { Id = Guid.NewGuid(), Subject = "Szkic", Body = "B", Group = MailingGroup.Custom, Status = CampaignStatus.Draft, CreatedAtUtc = DateTime.UtcNow };
        db.MailingCampaigns.AddRange(campaign, draft);
        db.MailingCampaignRecipients.Add(new MailingCampaignRecipient { CampaignId = draft.Id, PersonId = student.Id });
        db.MailingDeliveries.Add(new MailingDelivery { Id = Guid.NewGuid(), CampaignId = campaign.Id, PersonId = student.Id, Email = "jan@example.org", Status = MailingDeliveryStatus.Sent, AttemptedAtUtc = DateTime.UtcNow });
        db.Users.Add(new AppUser { Id = "user-jan-123", UserName = "jan@example.org", Email = "jan@example.org", PersonId = student.Id, CalendarFeedToken = "kal", SecurityStamp = "stary" });
        await db.SaveChangesAsync();
        foreach (var blob in new[] { "photos/jan.jpg", "notes/n.pdf", "cases/metryka.pdf", "missions/dekret.pdf", "docs/list.docx" }) storage.Files.Add(blob);
        return new Seed(student, catechist, dokCase, candidate);
    }

    [Fact]
    public async Task TheExport_ContainsWhatThePortalKnowsAboutThePerson_ButNotTheConfidentialNotes()
    {
        await using var db = CreateContext();
        var seed = await SeedAsync(db, new Storage());
        var service = new GdprService(db);

        var data = await service.ExportAsync(seed.Student.Id, default);
        var json = JsonSerializer.Serialize(data);

        Assert.Contains("\"imie\":\"Jan\"", json);
        Assert.Contains("600 100 200", json);
        Assert.Contains("Metryka chrztu", json);
        Assert.Contains("Anna Maj", json);
        Assert.Contains("rozmowa", json);
        Assert.Contains("choroba", json);
        Assert.Contains("Zaproszenie", json);
        Assert.Contains("1 notatek katechisty", json);
        Assert.DoesNotContain("TAJNA-TRESC", json);
        Assert.Null(await service.ExportAsync(Guid.NewGuid(), default));

        var catechistData = JsonSerializer.Serialize(await service.ExportAsync(seed.Catechist.Id, default));
        Assert.Contains("1 spraw DOK", catechistData);
    }

    [Fact]
    public async Task Anonymizing_RemovesPersonalData_FilesAndTheLogin_ButKeepsTheStatistics()
    {
        await using var db = CreateContext();
        var storage = new Storage();
        var seed = await SeedAsync(db, storage);

        Assert.True(await new GdprService(db, storage, logger: NullLogger<GdprService>.Instance).AnonymizeAsync(seed.Student.Id, default));

        var person = await db.People.Include(p => p.Functions).SingleAsync(p => p.Id == seed.Student.Id);
        Assert.Equal("Osoba", person.FirstName);
        Assert.StartsWith("zanonimizowana ", person.LastName);
        Assert.All(new object?[] { person.Email, person.Phone, person.BirthDate, person.ParishId, person.Notes, person.NameDayMonth, person.PhotoBlobPath, person.MailingUnsubscribeToken }, Assert.Null);
        Assert.NotNull(person.AnonymizedAtUtc);
        Assert.NotNull(person.MailingOptOutAtUtc);
        Assert.Null(person.Functions.Single().Notes);

        Assert.Equal(DokStage.Catechumenate, (await db.DokCases.SingleAsync()).Stage);
        Assert.Null((await db.DokCases.SingleAsync()).InterruptionReason);
        Assert.Empty(db.PastoralNotes);
        Assert.Empty(db.PastoralNoteShares);
        Assert.Empty(db.GeneratedDocuments);
        Assert.Empty(db.Attachments);
        Assert.Empty(db.MailingCampaignRecipients);
        Assert.Null((await db.CaseDocuments.SingleAsync()).BlobPath);
        Assert.True((await db.CaseDocuments.SingleAsync()).IsProvided);
        Assert.Null((await db.Meetings.SingleAsync()).Notes);
        Assert.True((await db.Meetings.SingleAsync()).IsAttended);
        var candidate = await db.Candidates.Include(c => c.Events).SingleAsync();
        Assert.Null(candidate.FormationStopNote);
        Assert.Null(candidate.Events.Single().Note);
        Assert.Equal("zanonimizowano", (await db.MailingDeliveries.SingleAsync()).Email);
        var user = await db.Users.SingleAsync();
        Assert.True(user.IsDeactivated);
        Assert.EndsWith("@usunieto.invalid", user.Email);
        Assert.Null(user.CalendarFeedToken);
        Assert.NotEqual("stary", user.SecurityStamp);
        Assert.Empty(storage.Files);

        var again = await Assert.ThrowsAsync<InvalidOperationException>(() => new GdprService(db).AnonymizeAsync(seed.Student.Id, default));
        Assert.Contains("już", again.Message);
        Assert.False(await new GdprService(db).AnonymizeAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task ACatechistWithOpenCases_MustHandThemOverFirst_AndStorageErrorsDoNotUndoTheAnonymization()
    {
        await using var db = CreateContext();
        var storage = new Storage { FailDeletes = true };
        var seed = await SeedAsync(db, storage);

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => new GdprService(db, storage).AnonymizeAsync(seed.Catechist.Id, default));
        Assert.Contains("przekaż", refused.Message);

        seed.Case.CompletedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        Assert.True(await new GdprService(db, storage, logger: NullLogger<GdprService>.Instance).AnonymizeAsync(seed.Catechist.Id, default));
        Assert.True(await new GdprService(db, storage, logger: NullLogger<GdprService>.Instance).AnonymizeAsync(seed.Student.Id, default));
        Assert.Contains("photos/jan.jpg", storage.Files);
    }
}
