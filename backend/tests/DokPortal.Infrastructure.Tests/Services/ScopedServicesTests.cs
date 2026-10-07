using ClosedXML.Excel;
using DokPortal.Application.CaseDocuments;
using DokPortal.Application.Common;
using DokPortal.Application.DokCases;
using DokPortal.Application.Meetings;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

/// <summary>Sprawdza, że serwisy ograniczają dane do spraw katechisty, gdy zakres nie jest pełny.</summary>
public class ScopedServicesTests
{
    private sealed class StubScope : ICaseScopeProvider
    {
        private readonly CaseScope _scope;
        public StubScope(CaseScope scope) => _scope = scope;
        public Task<CaseScope> GetAsync(CancellationToken ct) => Task.FromResult(_scope);
    }

    private sealed class InMemoryFileStorage : IFileStorageService
    {
        public Dictionary<string, byte[]> Files { get; } = new();

        public async Task UploadAsync(string blobPath, Stream content, string contentType, CancellationToken ct)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct);
            Files[blobPath] = buffer.ToArray();
        }

        public Task<StoredFile?> DownloadAsync(string blobPath, CancellationToken ct) =>
            Task.FromResult<StoredFile?>(Files.TryGetValue(blobPath, out var bytes) ? new StoredFile(new MemoryStream(bytes), "application/pdf") : null);

        public Task DeleteAsync(string blobPath, CancellationToken ct)
        {
            Files.Remove(blobPath);
            return Task.CompletedTask;
        }
    }

    private sealed class World
    {
        public required AppDbContext Db { get; init; }
        public required Guid CatechistA { get; init; }
        public required Guid CatechistB { get; init; }
        public required Guid CaseA { get; init; }
        public required Guid CaseB { get; init; }
        public required Guid DocumentA { get; init; }
        public required Guid DocumentB { get; init; }
        public required Guid MeetingA { get; init; }
        public required Guid MeetingB { get; init; }
        public required Guid GroupMeeting { get; init; }

        public ICaseScopeProvider AsA => new StubScope(new CaseScope(false, CatechistA));
        public ICaseScopeProvider AsAll => new StubScope(CaseScope.All);
        public ICaseScopeProvider WithoutPerson => new StubScope(new CaseScope(false, null));
    }

    private static Person NewPerson(string first, string last) => new()
    {
        Id = Guid.NewGuid(), FirstName = first, LastName = last, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
    };

    private static async Task<World> SeedAsync()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var catechistA = NewPerson("Anna", "Maj");
        var catechistB = NewPerson("Beata", "Lis");
        var studentA = NewPerson("Jan", "Kowalski");
        var studentB = NewPerson("Piotr", "Nowak");
        db.People.AddRange(catechistA, catechistB, studentA, studentB);

        var caseA = new DokCase { Id = Guid.NewGuid(), PersonId = studentA.Id, CatechistPersonId = catechistA.Id, Path = DokPath.Confirmation, Stage = DokStage.Evangelization, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var caseB = new DokCase { Id = Guid.NewGuid(), PersonId = studentB.Id, CatechistPersonId = catechistB.Id, Path = DokPath.Confirmation, Stage = DokStage.CloserFormation, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.DokCases.AddRange(caseA, caseB);

        var docA = new CaseDocument { Id = Guid.NewGuid(), DokCaseId = caseA.Id, Name = "Metryka A", IsProvided = false, CreatedAtUtc = DateTime.UtcNow };
        var docB = new CaseDocument { Id = Guid.NewGuid(), DokCaseId = caseB.Id, Name = "Metryka B", IsProvided = false, CreatedAtUtc = DateTime.UtcNow };
        db.CaseDocuments.AddRange(docA, docB);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var meetingA = new Meeting { Id = Guid.NewGuid(), DokCaseId = caseA.Id, MeetingDate = today.AddDays(1), CreatedAtUtc = DateTime.UtcNow };
        var meetingB = new Meeting { Id = Guid.NewGuid(), DokCaseId = caseB.Id, MeetingDate = today.AddDays(2), CreatedAtUtc = DateTime.UtcNow };
        var group = new Meeting { Id = Guid.NewGuid(), GroupLabel = "Grupa wieczorna", MeetingDate = today.AddDays(3), CreatedAtUtc = DateTime.UtcNow };
        db.Meetings.AddRange(meetingA, meetingB, group);
        await db.SaveChangesAsync();

        return new World
        {
            Db = db, CatechistA = catechistA.Id, CatechistB = catechistB.Id, CaseA = caseA.Id, CaseB = caseB.Id,
            DocumentA = docA.Id, DocumentB = docB.Id, MeetingA = meetingA.Id, MeetingB = meetingB.Id, GroupMeeting = group.Id
        };
    }

    // --- DokCaseService ---

    [Fact]
    public async Task DokCases_SearchReturnsOnlyOwnCases_AndGetByIdHidesForeignOnes()
    {
        var w = await SeedAsync();
        var service = new DokCaseService(w.Db, w.AsA);

        var result = await service.SearchAsync(null, 1, 20, default);

        Assert.Equal(new[] { "Jan Kowalski" }, result.Items.Select(i => i.PersonFullName));
        Assert.Equal(1, result.TotalCount);
        Assert.NotNull(await service.GetByIdAsync(w.CaseA, default));
        Assert.Null(await service.GetByIdAsync(w.CaseB, default));
    }

    [Fact]
    public async Task DokCases_ViewAllSeesEveryCase_AndNoPersonSeesNone()
    {
        var w = await SeedAsync();

        Assert.Equal(2, (await new DokCaseService(w.Db, w.AsAll).SearchAsync(null, 1, 20, default)).TotalCount);
        Assert.Equal(0, (await new DokCaseService(w.Db, w.WithoutPerson).SearchAsync(null, 1, 20, default)).TotalCount);
    }

    [Fact]
    public async Task DokCases_UpdateAndDeleteOfAForeignCaseDoNothing()
    {
        var w = await SeedAsync();
        var service = new DokCaseService(w.Db, w.AsA);
        var foreign = await w.Db.DokCases.AsNoTracking().FirstAsync(c => c.Id == w.CaseB);

        var updated = await service.UpdateAsync(w.CaseB, new UpdateDokCaseRequest
        {
            PersonId = foreign.PersonId, Path = DokPath.Conversion, Stage = DokStage.Graduate, CatechistPersonId = w.CatechistA
        }, default);
        var deleted = await service.DeleteAsync(w.CaseB, "user-a", default);

        Assert.Null(updated);
        Assert.False(deleted);
        var stored = await w.Db.DokCases.AsNoTracking().FirstAsync(c => c.Id == w.CaseB);
        Assert.Equal(DokPath.Confirmation, stored.Path);
        Assert.Equal(w.CatechistB, stored.CatechistPersonId);
    }

    [Fact]
    public async Task DokCases_OwnCaseCanBeUpdatedAndDeleted()
    {
        var w = await SeedAsync();
        var service = new DokCaseService(w.Db, w.AsA);
        var own = await w.Db.DokCases.AsNoTracking().FirstAsync(c => c.Id == w.CaseA);

        var updated = await service.UpdateAsync(w.CaseA, new UpdateDokCaseRequest
        {
            PersonId = own.PersonId, Path = DokPath.Confirmation, Stage = DokStage.Graduate, CatechistPersonId = w.CatechistA
        }, default);

        Assert.Equal(DokStage.Graduate, updated!.Stage);
        Assert.True(await service.DeleteAsync(w.CaseA, "user-a", default));
    }

    // --- CaseDocumentService ---

    [Fact]
    public async Task CaseDocuments_OwnCaseWorksEndToEnd()
    {
        var w = await SeedAsync();
        var storage = new InMemoryFileStorage();
        var service = new CaseDocumentService(w.Db, storage, w.AsA);

        var list = await service.GetForCaseAsync(w.CaseA, default);
        var created = await service.CreateAsync(w.CaseA, new CreateCaseDocumentRequest { Name = "Zaświadczenie" }, default);
        using var content = new MemoryStream(new byte[] { 1, 2, 3 });
        var uploaded = await service.UploadFileAsync(w.CaseA, w.DocumentA, content, "m.pdf", "application/pdf", 3, default);
        var downloaded = await service.DownloadFileAsync(w.CaseA, w.DocumentA, default);

        Assert.Single(list);
        Assert.NotNull(created);
        Assert.True(uploaded!.IsProvided);
        Assert.NotNull(downloaded);
    }

    [Fact]
    public async Task CaseDocuments_ForeignCaseBehavesLikeANonExistentOne()
    {
        var w = await SeedAsync();
        var storage = new InMemoryFileStorage();
        var service = new CaseDocumentService(w.Db, storage, w.AsA);

        Assert.Empty(await service.GetForCaseAsync(w.CaseB, default));
        Assert.Null(await service.CreateAsync(w.CaseB, new CreateCaseDocumentRequest { Name = "Cudza" }, default));
        Assert.Null(await service.SetProvidedAsync(w.CaseB, w.DocumentB, true, default));
        using var content = new MemoryStream(new byte[] { 1 });
        Assert.Null(await service.UploadFileAsync(w.CaseB, w.DocumentB, content, "x.pdf", "application/pdf", 1, default));
        Assert.Null(await service.DownloadFileAsync(w.CaseB, w.DocumentB, default));
        Assert.Empty(storage.Files);
        Assert.Equal(2, await w.Db.CaseDocuments.CountAsync());
    }

    // --- MeetingService ---

    [Fact]
    public async Task Meetings_ListAndGetShowOnlyMeetingsOfOwnCases()
    {
        var w = await SeedAsync();
        var service = new MeetingService(w.Db, w.AsA);

        var all = await service.GetAllAsync(default);

        Assert.Equal(new[] { w.MeetingA }, all.Select(m => m.Id));
        Assert.NotNull(await service.GetByIdAsync(w.MeetingA, default));
        Assert.Null(await service.GetByIdAsync(w.MeetingB, default));
        Assert.Null(await service.GetByIdAsync(w.GroupMeeting, default));
    }

    [Fact]
    public async Task Meetings_ViewAllSeesEverythingIncludingGroupMeetings()
    {
        var w = await SeedAsync();

        var all = await new MeetingService(w.Db, w.AsAll).GetAllAsync(default);

        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task Meetings_UpdateAndDeleteOfAForeignMeetingDoNothing()
    {
        var w = await SeedAsync();
        var service = new MeetingService(w.Db, w.AsA);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var updated = await service.UpdateAsync(w.MeetingB, new CreateMeetingRequest { DokCaseId = w.CaseB, MeetingDate = today, Notes = "zmiana" }, default);
        var deleted = await service.DeleteAsync(w.MeetingB, "user-a", default);

        Assert.Null(updated);
        Assert.False(deleted);
        Assert.Null((await w.Db.Meetings.AsNoTracking().FirstAsync(m => m.Id == w.MeetingB)).Notes);
    }

    [Fact]
    public async Task Meetings_CreatingOrMovingAMeetingToAForeignCaseIsRejected()
    {
        var w = await SeedAsync();
        var service = new MeetingService(w.Db, w.AsA);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateMeetingRequest { DokCaseId = w.CaseB, MeetingDate = today }, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(w.MeetingA, new CreateMeetingRequest { DokCaseId = w.CaseB, MeetingDate = today }, default));

        var created = await service.CreateAsync(new CreateMeetingRequest { DokCaseId = w.CaseA, MeetingDate = today }, default);
        Assert.Equal(w.CaseA, created.DokCaseId);
    }

    // --- DashboardService ---

    [Fact]
    public async Task Dashboard_CountsOnlyOwnCasesDocumentsAndMeetings()
    {
        var w = await SeedAsync();

        var own = await new DashboardService(w.Db, w.AsA).GetSummaryAsync(default);
        var all = await new DashboardService(w.Db, w.AsAll).GetSummaryAsync(default);

        Assert.Equal(1, own.DokCasesByStage.Single(s => s.Stage == "Evangelization").Count);
        Assert.Equal(0, own.DokCasesByStage.Single(s => s.Stage == "CloserFormation").Count);
        Assert.Equal(1, own.MissingDocumentsCasesCount);
        Assert.Equal(1, own.UpcomingMeetingsCount);
        Assert.Equal(2, all.MissingDocumentsCasesCount);
        Assert.Equal(3, all.UpcomingMeetingsCount);
    }

    // --- ExportService ---

    [Fact]
    public async Task Export_DokCasesAndMeetingsContainOnlyOwnRows()
    {
        var w = await SeedAsync();
        var service = new ExportService(w.Db, w.AsA);

        var cases = new XLWorkbook(new MemoryStream(await service.ExportDokCasesAsync(default))).Worksheet(1);
        var meetings = new XLWorkbook(new MemoryStream(await service.ExportMeetingsAsync(default))).Worksheet(1);

        Assert.Equal("Jan Kowalski", cases.Cell(2, 1).GetString());
        Assert.True(cases.Cell(3, 1).IsEmpty());
        Assert.Equal("Jan Kowalski", meetings.Cell(2, 2).GetString());
        Assert.True(meetings.Cell(3, 2).IsEmpty());
    }
}
