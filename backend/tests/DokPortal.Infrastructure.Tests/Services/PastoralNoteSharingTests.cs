using DokPortal.Application.PastoralNotes;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Identity;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

/// <summary>„Udostępnij moje notatki”: autor pokazuje swoje notatki ze sprawy obecnemu katechiście prowadzącemu.</summary>
public class PastoralNoteSharingTests
{
    private sealed record World(AppDbContext Db, PastoralNoteService Service, DokCase Case, Person Previous, Person Next,
        AppUser PreviousUser, AppUser NextUser, AppUser Stranger);

    private static async Task<World> SeedAsync()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        Person NewPerson(string last) => new() { Id = Guid.NewGuid(), FirstName = "Jan", LastName = last, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var student = NewPerson("Podopieczny");
        var previous = NewPerson("Poprzedni");
        var next = NewPerson("Nastepny");
        var stranger = NewPerson("Obcy");
        db.People.AddRange(student, previous, next, stranger);
        AppUser NewUser(Person p) => new() { Id = Guid.NewGuid().ToString(), UserName = p.LastName, Email = $"{p.LastName}@example.org", PersonId = p.Id };
        var previousUser = NewUser(previous);
        var nextUser = NewUser(next);
        var strangerUser = NewUser(stranger);
        db.Users.AddRange(previousUser, nextUser, strangerUser);
        // Sprawa została już przekazana nowemu katechiście.
        var dokCase = new DokCase
        {
            Id = Guid.NewGuid(), PersonId = student.Id, CatechistPersonId = next.Id, Path = DokPath.Confirmation, Stage = DokStage.Evangelization,
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        db.DokCases.Add(dokCase);
        await db.SaveChangesAsync();
        return new World(db, new PastoralNoteService(db), dokCase, previous, next, previousUser, nextUser, strangerUser);
    }

    private static Task<PastoralNoteDto> NoteAsync(World w, string authorId, string content) =>
        w.Service.CreateAsync(w.Case.Id, authorId, new CreatePastoralNoteRequest { Content = content }, default);

    [Fact]
    public async Task SharedNotes_AreVisibleToTheCurrentCatechist_IncludingLaterOnes_ButNotToOthers()
    {
        var w = await SeedAsync();
        var first = await NoteAsync(w, w.PreviousUser.Id, "Pierwsza rozmowa");
        Assert.Empty(await w.Service.GetVisibleForCaseAsync(w.Case.Id, w.NextUser.Id, false, default));

        var sharing = await w.Service.ShareWithCurrentCatechistAsync(w.Case.Id, w.PreviousUser.Id, default);
        var later = await NoteAsync(w, w.PreviousUser.Id, "Dopisana później");

        var forNext = await w.Service.GetVisibleForCaseAsync(w.Case.Id, w.NextUser.Id, false, default);
        Assert.Equal(new[] { later.Id, first.Id }, forNext.Select(n => n.Id));
        Assert.Equal(PastoralNoteAccess.Visible, await w.Service.GetAccessAsync(w.Case.Id, first.Id, w.NextUser.Id, false, default));
        Assert.Empty(await w.Service.GetVisibleForCaseAsync(w.Case.Id, w.Stranger.Id, false, default));
        Assert.Equal(PastoralNoteAccess.Hidden, await w.Service.GetAccessAsync(w.Case.Id, first.Id, w.Stranger.Id, false, default));
        Assert.Equal("Jan Nastepny", Assert.Single(sharing.SharedWith).FullName);
        Assert.False(sharing.CanShareWithCurrentCatechist);
    }

    [Fact]
    public async Task RevokingTheShare_HidesTheNotesAgain()
    {
        var w = await SeedAsync();
        var note = await NoteAsync(w, w.PreviousUser.Id, "Notatka");
        await w.Service.ShareWithCurrentCatechistAsync(w.Case.Id, w.PreviousUser.Id, default);

        var sharing = await w.Service.RevokeShareAsync(w.Case.Id, w.PreviousUser.Id, w.Next.Id, default);

        Assert.Empty(sharing.SharedWith);
        Assert.True(sharing.CanShareWithCurrentCatechist);
        Assert.Equal(PastoralNoteAccess.Hidden, await w.Service.GetAccessAsync(w.Case.Id, note.Id, w.NextUser.Id, false, default));
    }

    [Fact]
    public async Task SharingNeedsOwnNotes_AndACatechistOtherThanTheAuthor()
    {
        var w = await SeedAsync();

        var none = await Assert.ThrowsAsync<InvalidOperationException>(() => w.Service.ShareWithCurrentCatechistAsync(w.Case.Id, w.PreviousUser.Id, default));
        await NoteAsync(w, w.NextUser.Id, "Notatka obecnego katechisty");
        var self = await Assert.ThrowsAsync<InvalidOperationException>(() => w.Service.ShareWithCurrentCatechistAsync(w.Case.Id, w.NextUser.Id, default));
        var info = await w.Service.GetSharingAsync(w.Case.Id, w.PreviousUser.Id, default);

        Assert.Contains("nie masz notatek", none.Message);
        Assert.Contains("prowadzisz", self.Message);
        Assert.False(info.HasOwnNotes);
        Assert.Equal("Jan Nastepny", info.CurrentCatechistFullName);
    }

    [Fact]
    public async Task SharingTwice_DoesNotDuplicate()
    {
        var w = await SeedAsync();
        await NoteAsync(w, w.PreviousUser.Id, "Notatka");

        await w.Service.ShareWithCurrentCatechistAsync(w.Case.Id, w.PreviousUser.Id, default);
        var again = await w.Service.ShareWithCurrentCatechistAsync(w.Case.Id, w.PreviousUser.Id, default);

        Assert.Single(again.SharedWith);
        Assert.Single(await w.Db.PastoralNoteShares.ToListAsync());
    }

    [Fact]
    public async Task HandedOverCases_ListTheAuthorsNotesInCasesSomeoneElseNowLeads()
    {
        var w = await SeedAsync();
        await NoteAsync(w, w.PreviousUser.Id, "Notatka 1");
        await NoteAsync(w, w.PreviousUser.Id, "Notatka 2");
        await NoteAsync(w, w.NextUser.Id, "Notatka obecnego");

        var before = await w.Service.GetHandedOverAsync(w.PreviousUser.Id, default);
        await w.Service.ShareWithCurrentCatechistAsync(w.Case.Id, w.PreviousUser.Id, default);
        var after = await w.Service.GetHandedOverAsync(w.PreviousUser.Id, default);

        var item = Assert.Single(before);
        Assert.Equal(w.Case.Id, item.CaseId);
        Assert.Equal("Jan Podopieczny", item.PersonFullName);
        Assert.Equal("Jan Nastepny", item.CurrentCatechistFullName);
        Assert.Equal(2, item.NotesCount);
        Assert.False(item.SharedWithCurrentCatechist);
        Assert.True(Assert.Single(after).SharedWithCurrentCatechist);
        Assert.Empty(await w.Service.GetHandedOverAsync(w.NextUser.Id, default));
        Assert.Equal("Jan Podopieczny", (await w.Service.GetSharingAsync(w.Case.Id, w.PreviousUser.Id, default)).CasePersonFullName);
    }
}
