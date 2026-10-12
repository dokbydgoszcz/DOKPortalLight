using DokPortal.Application.CaseDocuments;
using DokPortal.Application.Common;
using DokPortal.Application.DokCases;
using DokPortal.Application.Meetings;
using DokPortal.Application.PastoralNotes;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

/// <summary>Proces DOK: edycja sprawy z historią, absolwenci, przypomnienia, notatki i dokumenty (przegląd 2026-10-08, A1, A7–A10, B3).</summary>
public class DokProcessTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class FixedScope(CaseScope scope) : ICaseScopeProvider
    {
        public Task<CaseScope> GetAsync(CancellationToken ct) => Task.FromResult(scope);
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = new();

        public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct)
        {
            Sent.Add((toEmail, subject, body));
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryStorage : IFileStorageService
    {
        public HashSet<string> Paths { get; } = new();

        public Task UploadAsync(string blobPath, Stream content, string contentType, CancellationToken ct)
        {
            Paths.Add(blobPath);
            return Task.CompletedTask;
        }

        public Task<StoredFile?> DownloadAsync(string blobPath, CancellationToken ct) => Task.FromResult<StoredFile?>(null);

        public Task DeleteAsync(string blobPath, CancellationToken ct)
        {
            Paths.Remove(blobPath);
            return Task.CompletedTask;
        }
    }

    private static readonly DokCaseActor Actor = new("user-1", "dyrektor@example.org");

    private static async Task<Person> AddPersonAsync(AppDbContext db, string first, string last, string? email = null)
    {
        var person = new Person { Id = Guid.NewGuid(), FirstName = first, LastName = last, Email = email, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.Add(person);
        await db.SaveChangesAsync();
        return person;
    }

    private static async Task<DokCase> AddCaseAsync(AppDbContext db, Person student, Person catechist, DokStage stage = DokStage.Evangelization)
    {
        var dokCase = new DokCase
        {
            Id = Guid.NewGuid(), PersonId = student.Id, CatechistPersonId = catechist.Id, Path = DokPath.Confirmation, Stage = stage,
            CompletedAtUtc = stage == DokStage.Graduate ? DateTime.UtcNow : null, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        db.DokCases.Add(dokCase);
        await db.SaveChangesAsync();
        return dokCase;
    }

    private static UpdateDokCaseRequest Update(DokCase c, DokStage? stage = null, Guid? catechist = null, Guid? mentor = null, Guid? person = null) => new()
    {
        PersonId = person ?? c.PersonId,
        Path = c.Path,
        Stage = stage ?? c.Stage,
        CatechistPersonId = catechist ?? c.CatechistPersonId,
        MentorPersonId = mentor ?? c.MentorPersonId
    };

    // ---------- Edycja sprawy i historia ----------

    [Fact]
    public async Task Update_RecordsStageCatechistAndMentorChanges_WithTheAuthor_NewestFirst()
    {
        await using var db = CreateContext();
        var student = await AddPersonAsync(db, "Jan", "Kowalski");
        var anna = await AddPersonAsync(db, "Anna", "Maj");
        var beata = await AddPersonAsync(db, "Beata", "Lis");
        var mentor = await AddPersonAsync(db, "Marek", "Mentor");
        var dokCase = await AddCaseAsync(db, student, anna);
        var service = new DokCaseService(db);

        await service.UpdateAsync(dokCase.Id, Update(dokCase, stage: DokStage.Graduate), default, Actor);
        await service.UpdateAsync(dokCase.Id, Update(dokCase, stage: DokStage.Graduate, catechist: beata.Id, mentor: mentor.Id), default, Actor);
        var history = await service.GetHistoryAsync(dokCase.Id, default);

        Assert.NotNull(history);
        Assert.Equal(3, history!.Count);
        Assert.Equal("Mentor: — → Marek Mentor", history[0].Description);
        Assert.Equal("Katechista: Anna Maj → Beata Lis", history[1].Description);
        Assert.Equal("Etap: Ewangelizacja → Absolwent", history[2].Description);
        Assert.All(history, e => Assert.Equal("dyrektor@example.org", e.PerformedByEmail));
    }

    [Fact]
    public async Task Update_WithoutChanges_AddsNoHistory_AndHistoryOfAnInvisibleCaseIsNull()
    {
        await using var db = CreateContext();
        var student = await AddPersonAsync(db, "Jan", "Kowalski");
        var anna = await AddPersonAsync(db, "Anna", "Maj");
        var dokCase = await AddCaseAsync(db, student, anna);

        await new DokCaseService(db).UpdateAsync(dokCase.Id, Update(dokCase), default, Actor);

        Assert.Empty((await new DokCaseService(db).GetHistoryAsync(dokCase.Id, default))!);
        var stranger = new DokCaseService(db, new FixedScope(new CaseScope(false, Guid.NewGuid())));
        Assert.Null(await stranger.GetHistoryAsync(dokCase.Id, default));
    }

    [Fact]
    public async Task Update_CannotChangeTheStudent_NorPointToAMissingCatechistOrMentor()
    {
        await using var db = CreateContext();
        var student = await AddPersonAsync(db, "Jan", "Kowalski");
        var other = await AddPersonAsync(db, "Inny", "Podopieczny");
        var anna = await AddPersonAsync(db, "Anna", "Maj");
        var dokCase = await AddCaseAsync(db, student, anna);
        var service = new DokCaseService(db);

        var person = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(dokCase.Id, Update(dokCase, person: other.Id), default));
        var catechist = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(dokCase.Id, Update(dokCase, catechist: Guid.NewGuid()), default));
        var mentor = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(dokCase.Id, Update(dokCase, mentor: Guid.NewGuid()), default));

        Assert.Contains("podopiecznego", person.Message);
        Assert.Contains("katechisty", catechist.Message);
        Assert.Contains("mentora", mentor.Message);
    }

    [Fact]
    public async Task Create_RejectsAMissingStudentOrCatechist_AndADuplicateActiveCase()
    {
        await using var db = CreateContext();
        var student = await AddPersonAsync(db, "Jan", "Kowalski");
        var anna = await AddPersonAsync(db, "Anna", "Maj");
        await AddCaseAsync(db, student, anna);
        var service = new DokCaseService(db);
        CreateDokCaseRequest Request(Guid person, Guid catechist) => new()
        {
            PersonId = person, Path = DokPath.Confirmation, Stage = DokStage.Evangelization, CatechistPersonId = catechist
        };

        var missingStudent = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Request(Guid.NewGuid(), anna.Id), default));
        var missingCatechist = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Request(student.Id, Guid.NewGuid()), default));
        var duplicate = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Request(student.Id, anna.Id), default));

        Assert.Contains("podopiecznego", missingStudent.Message);
        Assert.Contains("katechisty", missingCatechist.Message);
        Assert.Contains("ma już sprawę", duplicate.Message);
    }

    [Fact]
    public async Task LeavingTheGraduateStage_ClearsTheCompletionDate()
    {
        await using var db = CreateContext();
        var student = await AddPersonAsync(db, "Jan", "Kowalski");
        var anna = await AddPersonAsync(db, "Anna", "Maj");
        var dokCase = await AddCaseAsync(db, student, anna, DokStage.Graduate);

        var updated = await new DokCaseService(db).UpdateAsync(dokCase.Id, Update(dokCase, stage: DokStage.Evangelization), default);

        Assert.Null(updated!.CompletedAtUtc);
    }

    [Fact]
    public async Task ChangingTheCatechist_EmailsTheNewOne_AndAFailingMailDoesNotUndoTheChange()
    {
        await using var db = CreateContext();
        var student = await AddPersonAsync(db, "Jan", "Kowalski");
        var anna = await AddPersonAsync(db, "Anna", "Maj", "anna@example.org");
        var beata = await AddPersonAsync(db, "Beata", "Lis", "beata@example.org");
        var dokCase = await AddCaseAsync(db, student, anna);
        var sender = new RecordingEmailSender();

        await new DokCaseService(db, emailSender: sender).UpdateAsync(dokCase.Id, Update(dokCase, catechist: beata.Id), default, Actor);

        var mail = Assert.Single(sender.Sent);
        Assert.Equal("beata@example.org", mail.To);
        Assert.Contains("Jan Kowalski", mail.Body);
        Assert.Contains("Anna Maj", mail.Body);
    }

    // ---------- Absolwenci ----------

    [Fact]
    public async Task Graduates_AreLimitedToTheCatechistsOwnCases()
    {
        await using var db = CreateContext();
        var anna = await AddPersonAsync(db, "Anna", "Maj");
        var beata = await AddPersonAsync(db, "Beata", "Lis");
        await AddCaseAsync(db, await AddPersonAsync(db, "Jan", "Annowy"), anna, DokStage.Graduate);
        await AddCaseAsync(db, await AddPersonAsync(db, "Piotr", "Beatowy"), beata, DokStage.Graduate);

        var result = await new DokCaseService(db, new FixedScope(new CaseScope(false, anna.Id))).SearchGraduatesAsync(null, 1, 20, default);

        Assert.Equal("Jan Annowy", Assert.Single(result.Items).PersonFullName);
    }

    // ---------- Przypomnienia ----------

    [Fact]
    public async Task Reminders_SkipGraduates_ForMissingDocumentsAndMeetings()
    {
        await using var db = CreateContext();
        var anna = await AddPersonAsync(db, "Anna", "Maj", "anna@example.org");
        var graduate = await AddCaseAsync(db, await AddPersonAsync(db, "Jan", "Absolwent"), anna, DokStage.Graduate);
        db.CaseDocuments.Add(new CaseDocument { Id = Guid.NewGuid(), DokCaseId = graduate.Id, Name = "Metryka", CreatedAtUtc = DateTime.UtcNow });
        db.Meetings.Add(new Meeting { Id = Guid.NewGuid(), DokCaseId = graduate.Id, MeetingDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var sender = new RecordingEmailSender();
        var service = new ReminderService(db, sender, NullLogger<ReminderService>.Instance, new NameDayService(db));

        var documents = await service.RunMissingDocumentsReminderAsync(default);
        var meetings = await service.RunUpcomingMeetingsReminderAsync(default);

        Assert.Equal(0, documents.CasesProcessed);
        Assert.Equal(0, meetings.MeetingsProcessed);
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task MeetingReminder_AlsoCatchesTodaysMeeting_WhenTheRunWasLate()
    {
        await using var db = CreateContext();
        var anna = await AddPersonAsync(db, "Anna", "Maj", "anna@example.org");
        var dokCase = await AddCaseAsync(db, await AddPersonAsync(db, "Jan", "Kowalski"), anna);
        db.Meetings.Add(new Meeting { Id = Guid.NewGuid(), DokCaseId = dokCase.Id, MeetingDate = DateOnly.FromDateTime(DateTime.UtcNow), CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var sender = new RecordingEmailSender();

        await new ReminderService(db, sender, NullLogger<ReminderService>.Instance, new NameDayService(db)).RunUpcomingMeetingsReminderAsync(default);

        Assert.Contains("dziś", Assert.Single(sender.Sent).Body);
    }

    [Fact]
    public async Task MovingAMeetingToAnotherDate_SendsItsReminderAgain()
    {
        await using var db = CreateContext();
        var anna = await AddPersonAsync(db, "Anna", "Maj");
        var dokCase = await AddCaseAsync(db, await AddPersonAsync(db, "Jan", "Kowalski"), anna);
        var meeting = new Meeting
        {
            Id = Guid.NewGuid(), DokCaseId = dokCase.Id, MeetingDate = new DateOnly(2026, 10, 1), ReminderSentAtUtc = DateTime.UtcNow, CreatedAtUtc = DateTime.UtcNow
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();
        var service = new MeetingService(db);

        await service.UpdateAsync(meeting.Id, new CreateMeetingRequest { DokCaseId = dokCase.Id, MeetingDate = new DateOnly(2026, 10, 1), Notes = "x" }, default);
        Assert.NotNull((await db.Meetings.AsNoTracking().SingleAsync()).ReminderSentAtUtc);
        await service.UpdateAsync(meeting.Id, new CreateMeetingRequest { DokCaseId = dokCase.Id, MeetingDate = new DateOnly(2026, 10, 20) }, default);

        Assert.Null((await db.Meetings.AsNoTracking().SingleAsync()).ReminderSentAtUtc);
    }

    // ---------- Notatki duszpasterskie ----------

    [Fact]
    public async Task OnlyTheAuthor_CanEditOrDeleteANote()
    {
        await using var db = CreateContext();
        var dokCase = await AddCaseAsync(db, await AddPersonAsync(db, "Jan", "Kowalski"), await AddPersonAsync(db, "Anna", "Maj"));
        var service = new PastoralNoteService(db);
        var note = await service.CreateAsync(dokCase.Id, "author", new CreatePastoralNoteRequest { Content = "Pierwsza wersja" }, default);

        Assert.Null(await service.UpdateAsync(dokCase.Id, note.Id, "someone-else", new CreatePastoralNoteRequest { Content = "x" }, default));
        Assert.False(await service.DeleteAsync(dokCase.Id, note.Id, "someone-else", default));
        var edited = await service.UpdateAsync(dokCase.Id, note.Id, "author", new CreatePastoralNoteRequest { Content = "Poprawiona" }, default);

        Assert.Equal("Poprawiona", edited!.Content);
        Assert.NotNull(edited.UpdatedAtUtc);
        Assert.True(await service.DeleteAsync(dokCase.Id, note.Id, "author", default));
        Assert.Empty(await db.PastoralNotes.ToListAsync());
    }

    // ---------- Dokumenty sprawy ----------

    [Fact]
    public async Task ACaseDocument_CanBeRenamedAndDeleted_TogetherWithItsFile()
    {
        await using var db = CreateContext();
        var dokCase = await AddCaseAsync(db, await AddPersonAsync(db, "Jan", "Kowalski"), await AddPersonAsync(db, "Anna", "Maj"));
        var storage = new MemoryStorage();
        var service = new CaseDocumentService(db, storage);
        var document = await service.CreateAsync(dokCase.Id, new CreateCaseDocumentRequest { Name = "Metryka" }, default);
        await service.UploadFileAsync(dokCase.Id, document!.Id, new MemoryStream(new byte[] { 1 }), "metryka.pdf", "application/pdf", 1, default);

        var renamed = await service.RenameAsync(dokCase.Id, document.Id, "  Metryka chrztu  ", default);
        var deleted = await service.DeleteAsync(dokCase.Id, document.Id, default);

        Assert.Equal("Metryka chrztu", renamed!.Name);
        Assert.True(deleted);
        Assert.Empty(storage.Paths);
        Assert.Empty(await service.GetForCaseAsync(dokCase.Id, default));
        var another = await service.CreateAsync(dokCase.Id, new CreateCaseDocumentRequest { Name = "x" }, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RenameAsync(dokCase.Id, another!.Id, " ", default));
    }

    [Fact]
    public async Task ReplacingTheFile_RemovesThePreviousOne()
    {
        await using var db = CreateContext();
        var dokCase = await AddCaseAsync(db, await AddPersonAsync(db, "Jan", "Kowalski"), await AddPersonAsync(db, "Anna", "Maj"));
        var storage = new MemoryStorage();
        var service = new CaseDocumentService(db, storage);
        var document = await service.CreateAsync(dokCase.Id, new CreateCaseDocumentRequest { Name = "Metryka" }, default);

        await service.UploadFileAsync(dokCase.Id, document!.Id, new MemoryStream(new byte[] { 1 }), "stara.pdf", "application/pdf", 1, default);
        await service.UploadFileAsync(dokCase.Id, document.Id, new MemoryStream(new byte[] { 1 }), "nowa.pdf", "application/pdf", 1, default);

        Assert.Equal(new[] { $"case-documents/{dokCase.Id}/{document.Id}/nowa.pdf" }, storage.Paths);
    }
}
