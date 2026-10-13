using DokPortal.Application.DokCases;
using DokPortal.Application.Mailing;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

/// <summary>Podopieczny rezygnuje lub przerywa formację: sprawa zostaje (z historią, notatkami, frekwencją), ale wypada z bieżącej pracy.</summary>
public class DokCaseInterruptionTests
{
    private static readonly FixedTimeProvider Time = new(2026, 10, 13);
    private static readonly DateOnly Today = new(2026, 10, 13);
    private static readonly DokCaseActor Actor = new("user-1", "dyrektor@example.org");

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<Person> AddPersonAsync(AppDbContext db, string last, string? email = null)
    {
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = last, Email = email, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.Add(person);
        await db.SaveChangesAsync();
        return person;
    }

    private static async Task<DokCase> AddCaseAsync(AppDbContext db, Person student, Person catechist, DokStage stage = DokStage.Evangelization, DateTime? stageSince = null)
    {
        var dokCase = new DokCase
        {
            Id = Guid.NewGuid(), PersonId = student.Id, CatechistPersonId = catechist.Id, Path = DokPath.Confirmation, Stage = stage,
            StageSinceUtc = stageSince ?? DateTime.UtcNow,
            CompletedAtUtc = stage == DokStage.Graduate ? DateTime.UtcNow : null, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        db.DokCases.Add(dokCase);
        await db.SaveChangesAsync();
        return dokCase;
    }

    [Fact]
    public async Task Interrupt_KeepsTheCaseWithDateAndReason_AndWritesTheHistory()
    {
        await using var db = CreateContext();
        var service = new DokCaseService(db, time: Time);
        var dokCase = await AddCaseAsync(db, await AddPersonAsync(db, "Podopieczny"), await AddPersonAsync(db, "Katechista"));

        var result = await service.InterruptAsync(dokCase.Id, Today.AddDays(-2), "Rezygnacja – przeprowadzka", default, Actor);
        var history = await service.GetHistoryAsync(dokCase.Id, default);

        Assert.Equal(Today.AddDays(-2), result!.InterruptedOn);
        Assert.Equal("Rezygnacja – przeprowadzka", result.InterruptionReason);
        Assert.Equal(DokStage.Evangelization, result.Stage);
        var entry = Assert.Single(history!);
        Assert.Equal("Przerwano (11.10.2026): Rezygnacja – przeprowadzka", entry.Description);
        Assert.Equal("dyrektor@example.org", entry.PerformedByEmail);
        Assert.Null(await service.InterruptAsync(Guid.NewGuid(), Today, "x", default));
    }

    [Fact]
    public async Task Interrupt_IsRefusedForGraduatesTwiceOrInTheFuture_AndEditingWaitsForResume()
    {
        await using var db = CreateContext();
        var service = new DokCaseService(db, time: Time);
        var catechist = await AddPersonAsync(db, "Katechista");
        var graduate = await AddCaseAsync(db, await AddPersonAsync(db, "Absolwent"), catechist, DokStage.Graduate);
        var open = await AddCaseAsync(db, await AddPersonAsync(db, "Podopieczny"), catechist);

        var a = await Assert.ThrowsAsync<InvalidOperationException>(() => service.InterruptAsync(graduate.Id, Today, "x", default));
        var future = await Assert.ThrowsAsync<InvalidOperationException>(() => service.InterruptAsync(open.Id, Today.AddDays(1), "x", default));
        await service.InterruptAsync(open.Id, Today, "Rezygnacja", default);
        var twice = await Assert.ThrowsAsync<InvalidOperationException>(() => service.InterruptAsync(open.Id, Today, "x", default));
        var edit = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(open.Id, new UpdateDokCaseRequest
        {
            PersonId = open.PersonId, Path = open.Path, Stage = DokStage.Graduate, CatechistPersonId = catechist.Id
        }, default));

        Assert.Contains("absolwent", a.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("przyszł", future.Message);
        Assert.Contains("już przerwana", twice.Message);
        Assert.Contains("wznów", edit.Message);
    }

    [Fact]
    public async Task Resume_ClearsTheInterruption_AndTheStageClockDoesNotCountThePause()
    {
        await using var db = CreateContext();
        var service = new DokCaseService(db, time: Time);
        var since = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var dokCase = await AddCaseAsync(db, await AddPersonAsync(db, "Podopieczny"), await AddPersonAsync(db, "Katechista"), stageSince: since);
        await service.InterruptAsync(dokCase.Id, Today.AddDays(-30), "Choroba", default, Actor);

        var resumed = await service.ResumeAsync(dokCase.Id, default, Actor);
        var history = await service.GetHistoryAsync(dokCase.Id, default);

        Assert.Null(resumed!.InterruptedOn);
        Assert.Null(resumed.InterruptionReason);
        Assert.Equal(since.AddDays(30), (await db.DokCases.AsNoTracking().SingleAsync(c => c.Id == dokCase.Id)).StageSinceUtc);
        Assert.Equal("Wznowiono", history![0].Description);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResumeAsync(dokCase.Id, default));
    }

    [Fact]
    public async Task AnInterruptedCase_DoesNotBlockANewCase_ButThenCannotBeResumed()
    {
        await using var db = CreateContext();
        var service = new DokCaseService(db, time: Time);
        var student = await AddPersonAsync(db, "Podopieczny");
        var catechist = await AddPersonAsync(db, "Katechista");
        var old = await AddCaseAsync(db, student, catechist);
        await service.InterruptAsync(old.Id, Today, "Przerwa", default);

        await service.CreateAsync(new CreateDokCaseRequest
        {
            PersonId = student.Id, Path = DokPath.Confirmation, Stage = DokStage.Evangelization, CatechistPersonId = catechist.Id
        }, default);
        var resume = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ResumeAsync(old.Id, default));

        Assert.Contains("w toku", resume.Message);
    }

    [Fact]
    public async Task TheList_CanShowOnlyActiveOrOnlyInterruptedCases()
    {
        await using var db = CreateContext();
        var service = new DokCaseService(db, time: Time);
        var catechist = await AddPersonAsync(db, "Katechista");
        var active = await AddCaseAsync(db, await AddPersonAsync(db, "A"), catechist);
        var stopped = await AddCaseAsync(db, await AddPersonAsync(db, "B"), catechist);
        await service.InterruptAsync(stopped.Id, Today, "Rezygnacja", default);

        var all = await service.SearchAsync(null, 1, 50, default);
        var onlyActive = await service.SearchAsync(null, 1, 50, default, status: DokCaseStatusFilter.Active);
        var onlyStopped = await service.SearchAsync(null, 1, 50, default, status: DokCaseStatusFilter.Interrupted);

        Assert.Equal(2, all.TotalCount);
        Assert.Equal(active.Id, Assert.Single(onlyActive.Items).Id);
        Assert.Equal(stopped.Id, Assert.Single(onlyStopped.Items).Id);
    }

    [Fact]
    public async Task InterruptedCases_LeaveTheDashboardRemindersAndMailing()
    {
        await using var db = CreateContext();
        var service = new DokCaseService(db, time: Time);
        var catechist = await AddPersonAsync(db, "Katechista", "kat@example.org");
        var student = await AddPersonAsync(db, "Podopieczny", "pod@example.org");
        var dokCase = await AddCaseAsync(db, student, catechist, stageSince: DateTime.UtcNow.AddYears(-2));
        db.CaseDocuments.Add(new CaseDocument { Id = Guid.NewGuid(), DokCaseId = dokCase.Id, Name = "Metryka", IsProvided = false, CreatedAtUtc = DateTime.UtcNow });
        db.Meetings.Add(new Meeting { Id = Guid.NewGuid(), DokCaseId = dokCase.Id, MeetingDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        await service.InterruptAsync(dokCase.Id, Today, "Rezygnacja", default);

        var summary = await new DashboardService(db, time: Time).GetSummaryAsync(default);
        var sender = new RecordingSender();
        var reminders = new ReminderService(db, sender, NullLogger<ReminderService>.Instance, new NameDayService(db));
        await reminders.RunMissingDocumentsReminderAsync(default);
        await reminders.RunUpcomingMeetingsReminderAsync(default);
        var recipients = await new MailingService(db, sender).GetRecipientCountAsync(MailingGroup.DokCases, default);

        Assert.Equal(0, summary.DokCasesByStage.Sum(s => s.Count));
        Assert.Empty(summary.StalledCases);
        Assert.Equal(0, summary.MissingDocumentsCasesCount);
        Assert.Empty(sender.Sent);
        Assert.Equal(0, recipients);
    }

    private sealed class RecordingSender : DokPortal.Application.Common.IEmailSender
    {
        public List<string> Sent { get; } = new();
        public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct)
        {
            Sent.Add(toEmail);
            return Task.CompletedTask;
        }
    }
}
