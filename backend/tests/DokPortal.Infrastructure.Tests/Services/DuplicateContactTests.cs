using DokPortal.Application.People;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Identity;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class DuplicateContactTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CreatePersonRequest Request(string first, string last, string? email = null, string? phone = null, bool confirm = false) => new()
    {
        FirstName = first, LastName = last, Email = email, Phone = phone, ConfirmDuplicate = confirm
    };

    private static UpdatePersonRequest Update(string first, string last, string? email = null, string? phone = null, bool confirm = false) => new()
    {
        FirstName = first, LastName = last, Email = email, Phone = phone, ConfirmDuplicate = confirm
    };

    // --- normalizacja ---

    [Theory]
    [InlineData("Jan.Kowalski@Example.ORG", "jan.kowalski@example.org")]
    [InlineData("  jan@example.org ", "jan@example.org")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void NormalizeEmail_TrimsAndLowercases(string? input, string? expected) =>
        Assert.Equal(expected, PersonContactNormalizer.NormalizeEmail(input));

    [Theory]
    [InlineData("600 100 200", "600100200")]
    [InlineData("600-100-200", "600100200")]
    [InlineData("+48 600 100 200", "600100200")]
    [InlineData("0048600100200", "600100200")]
    [InlineData("48600100200", "600100200")]
    [InlineData("(52) 345-67-89", "523456789")]
    [InlineData("12345", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void NormalizePhone_KeepsDigitsWithoutThePolishPrefix_AndIgnoresTooShortNumbers(string? input, string? expected) =>
        Assert.Equal(expected, PersonContactNormalizer.NormalizePhone(input));

    // --- e-mail: twarda blokada ---

    [Fact]
    public async Task Create_RejectsAnEmailAlreadyUsedByAnotherPerson_IgnoringCaseAndSpaces()
    {
        await using var db = CreateContext();
        var service = new PersonService(db);
        await service.CreateAsync(Request("Jan", "Kowalski", "jan@example.org"), default);

        var ex = await Assert.ThrowsAsync<DuplicatePersonException>(() =>
            service.CreateAsync(Request("Janusz", "Inny", "  JAN@example.org "), default));

        Assert.Equal("EmailTaken", ex.Code);
        Assert.Equal("Jan Kowalski", Assert.Single(ex.Matches).FullName);
        Assert.Contains("Jan Kowalski", ex.Message);
        Assert.Equal(1, await db.People.CountAsync());
    }

    [Fact]
    public async Task Create_EmailConflictCannotBeBypassedWithConfirmation()
    {
        await using var db = CreateContext();
        var service = new PersonService(db);
        await service.CreateAsync(Request("Jan", "Kowalski", "jan@example.org"), default);

        await Assert.ThrowsAsync<DuplicatePersonException>(() =>
            service.CreateAsync(Request("Janusz", "Inny", "jan@example.org", confirm: true), default));
    }

    [Fact]
    public async Task Create_IgnoresSoftDeletedPeopleAndPeopleWithoutEmail()
    {
        await using var db = CreateContext();
        var service = new PersonService(db);
        var removed = await service.CreateAsync(Request("Usunięty", "Zenon", "dup@example.org"), default);
        await service.DeleteAsync(removed.Id, "admin", default);
        await service.CreateAsync(Request("Bez", "Maila"), default);

        var created = await service.CreateAsync(Request("Nowy", "Adam", "dup@example.org"), default);
        var another = await service.CreateAsync(Request("Drugi", "Bez"), default);

        Assert.Equal("dup@example.org", created.Email);
        Assert.Null(another.Email);
    }

    [Fact]
    public async Task Create_RejectsTheLoginOfAnAccountLinkedToAnotherPerson_ButNotOfAnUnlinkedAccount()
    {
        await using var db = CreateContext();
        var service = new PersonService(db);
        var owner = await service.CreateAsync(Request("Anna", "Maj", "anna.prywatny@example.org"), default);
        db.Users.AddRange(
            new AppUser { Id = "u1", UserName = "anna@example.org", Email = "anna@example.org", PersonId = owner.Id },
            new AppUser { Id = "u2", UserName = "wolne@example.org", Email = "wolne@example.org", PersonId = null });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<DuplicatePersonException>(() =>
            service.CreateAsync(Request("Ktoś", "Inny", "ANNA@example.org"), default));
        var allowed = await service.CreateAsync(Request("Ktoś", "Wolny", "wolne@example.org"), default);

        Assert.Equal("EmailTaken", ex.Code);
        Assert.Contains("Anna Maj", ex.Message);
        Assert.Equal("wolne@example.org", allowed.Email);
    }

    [Fact]
    public async Task Update_AllowsKeepingOwnEmail_AndAnAccountLinkedToTheSamePerson()
    {
        await using var db = CreateContext();
        var service = new PersonService(db);
        var person = await service.CreateAsync(Request("Anna", "Maj", "anna@example.org"), default);
        db.Users.Add(new AppUser { Id = "u1", UserName = "anna@example.org", Email = "anna@example.org", PersonId = person.Id });
        await db.SaveChangesAsync();

        var updated = await service.UpdateAsync(person.Id, Update("Anna", "Majewska", "ANNA@example.org"), default);

        Assert.Equal("Majewska", updated!.LastName);
    }

    [Fact]
    public async Task Update_RejectsChangingTheEmailToOneUsedByAnotherPerson()
    {
        await using var db = CreateContext();
        var service = new PersonService(db);
        await service.CreateAsync(Request("Jan", "Kowalski", "jan@example.org"), default);
        var other = await service.CreateAsync(Request("Piotr", "Nowak", "piotr@example.org"), default);

        var ex = await Assert.ThrowsAsync<DuplicatePersonException>(() =>
            service.UpdateAsync(other.Id, Update("Piotr", "Nowak", "jan@example.org"), default));

        Assert.Equal("EmailTaken", ex.Code);
        Assert.Equal("piotr@example.org", (await service.GetByIdAsync(other.Id, default))!.Email);
    }

    [Fact]
    public async Task Update_DoesNotRecheckAnEmailThatWasNotChanged_SoLegacyDuplicatesCanStillBeEdited()
    {
        await using var db = CreateContext();
        var now = DateTime.UtcNow;
        var a = new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski", Email = "dup@example.org", CreatedAtUtc = now, UpdatedAtUtc = now };
        var b = new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Drugi", Email = "dup@example.org", CreatedAtUtc = now, UpdatedAtUtc = now };
        db.People.AddRange(a, b);
        await db.SaveChangesAsync();
        var service = new PersonService(db);

        var updated = await service.UpdateAsync(b.Id, Update("Jan", "Poprawiony", "dup@example.org"), default);

        Assert.Equal("Poprawiony", updated!.LastName);
    }

    // --- telefon: ostrzeżenie z potwierdzeniem ---

    [Fact]
    public async Task Create_WarnsAboutASharedPhone_ComparedAfterNormalization_AndSavesWhenConfirmed()
    {
        await using var db = CreateContext();
        var service = new PersonService(db);
        await service.CreateAsync(Request("Jan", "Kowalski", phone: "600 100 200"), default);

        var ex = await Assert.ThrowsAsync<DuplicatePersonException>(() =>
            service.CreateAsync(Request("Maria", "Kowalska", phone: "+48 600-100-200"), default));
        var saved = await service.CreateAsync(Request("Maria", "Kowalska", phone: "+48 600-100-200", confirm: true), default);

        Assert.Equal("PhoneDuplicate", ex.Code);
        Assert.Equal("Jan Kowalski", Assert.Single(ex.Matches).FullName);
        Assert.Equal("phone", ex.Matches[0].MatchedOn);
        Assert.Equal("+48 600-100-200", saved.Phone);
        Assert.Equal(2, await db.People.CountAsync());
    }

    [Fact]
    public async Task Update_WarnsOnlyWhenThePhoneChangesToAnotherPersonsNumber()
    {
        await using var db = CreateContext();
        var service = new PersonService(db);
        await service.CreateAsync(Request("Jan", "Kowalski", phone: "600100200"), default);
        var other = await service.CreateAsync(Request("Piotr", "Nowak", phone: "511222333"), default);

        var sameAsBefore = await service.UpdateAsync(other.Id, Update("Piotr", "Nowak", phone: "511 222 333"), default);
        var ex = await Assert.ThrowsAsync<DuplicatePersonException>(() =>
            service.UpdateAsync(other.Id, Update("Piotr", "Nowak", phone: "600-100-200"), default));
        var confirmed = await service.UpdateAsync(other.Id, Update("Piotr", "Nowak", phone: "600-100-200", confirm: true), default);

        Assert.NotNull(sameAsBefore);
        Assert.Equal("PhoneDuplicate", ex.Code);
        Assert.Equal("600-100-200", confirmed!.Phone);
    }

    [Fact]
    public async Task EmailConflictWinsOverAPhoneWarning_WhenBothMatch()
    {
        await using var db = CreateContext();
        var service = new PersonService(db);
        await service.CreateAsync(Request("Jan", "Kowalski", "jan@example.org", "600100200"), default);

        var ex = await Assert.ThrowsAsync<DuplicatePersonException>(() =>
            service.CreateAsync(Request("Inny", "Człowiek", "jan@example.org", "600100200", confirm: true), default));

        Assert.Equal("EmailTaken", ex.Code);
    }
}
