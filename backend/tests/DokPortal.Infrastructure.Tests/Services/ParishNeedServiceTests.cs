using DokPortal.Application.ParishNeeds;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class ParishNeedServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Person NewPerson(string first, string last) =>
        new() { Id = Guid.NewGuid(), FirstName = first, LastName = last, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };

    private record Fixture(AppDbContext Db, ParishNeedService Service, Parish Parish, Person Marek, Person Anna, ParishNeedDto Need);

    private static async Task<Fixture> SeedAsync()
    {
        var db = CreateContext();
        var parish = new Parish { Id = Guid.NewGuid(), Name = "św. Mateusza" };
        var marek = NewPerson("Marek", "Zielinski");
        var anna = NewPerson("Anna", "Maj");
        db.Parishes.Add(parish);
        db.People.AddRange(marek, anna);
        await db.SaveChangesAsync();
        var service = new ParishNeedService(db);
        var need = await service.CreateAsync(new CreateParishNeedRequest { ParishId = parish.Id, Description = "Katechista do przygotowania dorosłych" }, default);
        return new Fixture(db, service, parish, marek, anna, need);
    }

    [Fact]
    public async Task CreateAsync_StartsOpenWithNobodyAssigned()
    {
        var f = await SeedAsync();

        Assert.Equal("Open", f.Need.Status);
        Assert.Empty(f.Need.AssignedPeople);
    }

    [Fact]
    public async Task AssignAsync_SetsStatusAndListsThePerson()
    {
        var f = await SeedAsync();

        var assigned = await f.Service.AssignAsync(f.Need.Id, f.Marek.Id, default);

        Assert.Equal("Assigned", assigned!.Status);
        var person = Assert.Single(assigned.AssignedPeople);
        Assert.Equal("Marek Zielinski", person.FullName);
        Assert.Equal(f.Marek.Id, person.PersonId);
    }

    [Fact]
    public async Task AssignAsync_AddsAnotherPersonWithoutReplacingTheFirst()
    {
        var f = await SeedAsync();
        await f.Service.AssignAsync(f.Need.Id, f.Marek.Id, default);
        (await f.Db.ParishNeedAssignments.SingleAsync()).AssignedAtUtc = DateTime.UtcNow.AddMinutes(-5);
        await f.Db.SaveChangesAsync();

        var both = await f.Service.AssignAsync(f.Need.Id, f.Anna.Id, default);

        Assert.Equal(new[] { "Marek Zielinski", "Anna Maj" }, both!.AssignedPeople.Select(p => p.FullName));
    }

    [Fact]
    public async Task AssignAsync_TheSamePersonTwice_ChangesNothing()
    {
        var f = await SeedAsync();
        await f.Service.AssignAsync(f.Need.Id, f.Marek.Id, default);

        var again = await f.Service.AssignAsync(f.Need.Id, f.Marek.Id, default);

        Assert.Single(again!.AssignedPeople);
        Assert.Equal(1, await f.Db.ParishNeedAssignments.CountAsync());
    }

    [Fact]
    public async Task AssignAsync_ReturnsNullForAMissingNeed_AndRejectsAMissingPerson()
    {
        var f = await SeedAsync();

        Assert.Null(await f.Service.AssignAsync(Guid.NewGuid(), f.Marek.Id, default));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.AssignAsync(f.Need.Id, Guid.NewGuid(), default));
        Assert.Contains("osoby", ex.Message);
    }

    [Fact]
    public async Task UnassignAsync_RemovesOnePerson_AndTheNeedStaysAssignedWhileSomeoneIsLeft()
    {
        var f = await SeedAsync();
        await f.Service.AssignAsync(f.Need.Id, f.Marek.Id, default);
        await f.Service.AssignAsync(f.Need.Id, f.Anna.Id, default);

        var result = await f.Service.UnassignAsync(f.Need.Id, f.Marek.Id, default);

        Assert.Equal("Assigned", result!.Status);
        Assert.Equal("Anna Maj", Assert.Single(result.AssignedPeople).FullName);
    }

    [Fact]
    public async Task UnassignAsync_TheLastPerson_ReopensTheNeed()
    {
        var f = await SeedAsync();
        await f.Service.AssignAsync(f.Need.Id, f.Marek.Id, default);

        var result = await f.Service.UnassignAsync(f.Need.Id, f.Marek.Id, default);

        Assert.Equal("Open", result!.Status);
        Assert.Empty(result.AssignedPeople);
    }

    [Fact]
    public async Task UnassignAsync_AClosedNeedStaysClosed_AndAnUnknownPersonChangesNothing()
    {
        var f = await SeedAsync();
        await f.Service.AssignAsync(f.Need.Id, f.Marek.Id, default);
        (await f.Db.ParishNeeds.SingleAsync()).Status = ParishNeedStatus.Closed;
        await f.Db.SaveChangesAsync();

        var stranger = await f.Service.UnassignAsync(f.Need.Id, f.Anna.Id, default);
        var last = await f.Service.UnassignAsync(f.Need.Id, f.Marek.Id, default);

        Assert.Single(stranger!.AssignedPeople);
        Assert.Equal("Closed", last!.Status);
        Assert.Null(await f.Service.UnassignAsync(Guid.NewGuid(), f.Marek.Id, default));
    }

    [Fact]
    public async Task UpdateAsync_ChangesParishAndDescription_AlsoAfterAssignment_KeepingThePeople()
    {
        var f = await SeedAsync();
        var otherParish = new Parish { Id = Guid.NewGuid(), Name = "św. Pawła" };
        f.Db.Parishes.Add(otherParish);
        await f.Db.SaveChangesAsync();
        await f.Service.AssignAsync(f.Need.Id, f.Marek.Id, default);

        var updated = await f.Service.UpdateAsync(f.Need.Id, new UpdateParishNeedRequest { ParishId = otherParish.Id, Description = "  Dwóch katechistów  " }, default);

        Assert.Equal("św. Pawła", updated!.ParishName);
        Assert.Equal("Dwóch katechistów", updated.Description);
        Assert.Equal("Assigned", updated.Status);
        Assert.Single(updated.AssignedPeople);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNullForAMissingNeed_AndRejectsABlankDescriptionOrUnknownParish()
    {
        var f = await SeedAsync();

        Assert.Null(await f.Service.UpdateAsync(Guid.NewGuid(), new UpdateParishNeedRequest { ParishId = f.Parish.Id, Description = "x" }, default));
        var blank = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.UpdateAsync(f.Need.Id, new UpdateParishNeedRequest { ParishId = f.Parish.Id, Description = "   " }, default));
        var parish = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Service.UpdateAsync(f.Need.Id, new UpdateParishNeedRequest { ParishId = Guid.NewGuid(), Description = "x" }, default));

        Assert.Contains("opis", blank.Message);
        Assert.Contains("parafii", parish.Message);
    }

    [Fact]
    public async Task GetAllAsync_ListsTheAssignedPeopleOfEachNeed()
    {
        var f = await SeedAsync();
        await f.Service.AssignAsync(f.Need.Id, f.Marek.Id, default);
        await f.Service.AssignAsync(f.Need.Id, f.Anna.Id, default);

        var all = await f.Service.GetAllAsync(default);

        Assert.Equal(2, all.Single().AssignedPeople.Count);
    }
}
