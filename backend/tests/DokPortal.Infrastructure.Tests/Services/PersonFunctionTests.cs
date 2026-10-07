using DokPortal.Application.People;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

/// <summary>Osoba ma funkcje (akolita, lektor, proboszcz, katechista); funkcja może mieć własne właściwości.</summary>
public class PersonFunctionTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CreatePersonRequest NewPerson(string last, IReadOnlyList<PersonFunctionInput>? functions = null, string first = "Jan") => new()
    {
        FirstName = first, LastName = last, Functions = functions
    };

    private static PersonFunctionInput Fn(FunctionType type, Guid? parishId = null, DateOnly? on = null, string? notes = null) =>
        new() { Type = type, ParishId = parishId, InstitutedOn = on, Notes = notes };

    private static async Task<Parish> AddParishAsync(AppDbContext db, string name)
    {
        var parish = new Parish { Id = Guid.NewGuid(), Name = name };
        db.Parishes.Add(parish);
        await db.SaveChangesAsync();
        return parish;
    }

    [Fact]
    public async Task APersonCanHaveSeveralFunctions_WithTheirOwnProperties()
    {
        await using var db = CreateContext();
        var parish = await AddParishAsync(db, "św. Jana");
        var service = new PersonService(db);

        var created = await service.CreateAsync(NewPerson("Kowalski", new[]
        {
            Fn(FunctionType.Acolyte, on: new DateOnly(2020, 5, 1), notes: "Ustanowiony w katedrze"),
            Fn(FunctionType.Lector),
            Fn(FunctionType.Pastor, parish.Id)
        }), default);

        Assert.Equal(3, created.Functions.Count);
        var acolyte = created.Functions.Single(f => f.Type == FunctionType.Acolyte);
        Assert.Equal((new DateOnly(2020, 5, 1), "Ustanowiony w katedrze"), (acolyte.InstitutedOn, acolyte.Notes));
        var pastor = created.Functions.Single(f => f.Type == FunctionType.Pastor);
        Assert.Equal(("św. Jana", parish.Id), (pastor.ParishName, pastor.ParishId));
    }

    [Fact]
    public async Task APersonWithoutFunctions_HasAnEmptyList()
    {
        await using var db = CreateContext();

        var created = await new PersonService(db).CreateAsync(NewPerson("Kowalski"), default);

        Assert.Empty(created.Functions);
    }

    [Fact]
    public async Task Update_WithoutAFunctionsList_LeavesTheFunctionsAlone_AndAnEmptyListClearsThem()
    {
        await using var db = CreateContext();
        var service = new PersonService(db);
        var created = await service.CreateAsync(NewPerson("Kowalski", new[] { Fn(FunctionType.Lector) }), default);

        var untouched = await service.UpdateAsync(created.Id, new UpdatePersonRequest { FirstName = "Jan", LastName = "Nowak" }, default);
        var cleared = await service.UpdateAsync(created.Id, new UpdatePersonRequest { FirstName = "Jan", LastName = "Nowak", Functions = Array.Empty<PersonFunctionInput>() }, default);

        Assert.Single(untouched!.Functions);
        Assert.Empty(cleared!.Functions);
    }

    [Fact]
    public async Task Update_ReplacesTheSet_KeepingTheMatchingOnesAndChangingTheirProperties()
    {
        await using var db = CreateContext();
        var service = new PersonService(db);
        var created = await service.CreateAsync(NewPerson("Kowalski", new[] { Fn(FunctionType.Acolyte), Fn(FunctionType.Lector) }), default);
        var acolyteId = created.Functions.Single(f => f.Type == FunctionType.Acolyte).Id;

        var updated = await service.UpdateAsync(created.Id, new UpdatePersonRequest
        {
            FirstName = "Jan", LastName = "Kowalski",
            Functions = new[] { Fn(FunctionType.Acolyte, notes: "Nowa notatka"), Fn(FunctionType.Catechist) }
        }, default);

        Assert.Equal(new[] { FunctionType.Catechist, FunctionType.Acolyte }.OrderBy(t => t), updated!.Functions.Select(f => f.Type).OrderBy(t => t));
        Assert.Equal(acolyteId, updated.Functions.Single(f => f.Type == FunctionType.Acolyte).Id);
        Assert.Equal("Nowa notatka", updated.Functions.Single(f => f.Type == FunctionType.Acolyte).Notes);
    }

    [Fact]
    public async Task AParishCanHaveOnlyOnePastor_AndTheMessageNamesHim()
    {
        await using var db = CreateContext();
        var parish = await AddParishAsync(db, "św. Jana");
        var service = new PersonService(db);
        await service.CreateAsync(NewPerson("Pierwszy", new[] { Fn(FunctionType.Pastor, parish.Id) }, "Ks. Adam"), default);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(NewPerson("Drugi", new[] { Fn(FunctionType.Pastor, parish.Id) }, "Ks. Piotr"), default));

        Assert.Contains("św. Jana", ex.Message);
        Assert.Contains("Ks. Adam Pierwszy", ex.Message);
    }

    [Fact]
    public async Task APastorMayHaveSeveralParishes_AndKeepsHisOwnParishWhenSavedAgain()
    {
        await using var db = CreateContext();
        var first = await AddParishAsync(db, "św. Jana");
        var second = await AddParishAsync(db, "św. Pawła");
        var service = new PersonService(db);
        var created = await service.CreateAsync(NewPerson("Kowalski", new[] { Fn(FunctionType.Pastor, first.Id), Fn(FunctionType.Pastor, second.Id) }), default);

        var saved = await service.UpdateAsync(created.Id, new UpdatePersonRequest
        {
            FirstName = "Jan", LastName = "Kowalski", Functions = new[] { Fn(FunctionType.Pastor, first.Id), Fn(FunctionType.Pastor, second.Id) }
        }, default);

        Assert.Equal(2, saved!.Functions.Count);
    }

    [Fact]
    public async Task APastorWithoutAParish_IsAllowed_AndWaitsToBeAssigned()
    {
        await using var db = CreateContext();

        var created = await new PersonService(db).CreateAsync(NewPerson("Kowalski", new[] { Fn(FunctionType.Pastor) }), default);

        var pastor = Assert.Single(created.Functions);
        Assert.Null(pastor.ParishId);
    }

    [Fact]
    public async Task Validation_RejectsRepeatedFunctions_AParishOnANonPastor_AndAnUnknownParish()
    {
        await using var db = CreateContext();
        var parish = await AddParishAsync(db, "św. Jana");
        var service = new PersonService(db);

        var twice = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(NewPerson("A", new[] { Fn(FunctionType.Lector), Fn(FunctionType.Lector) }), default));
        var sameParishTwice = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(NewPerson("B", new[] { Fn(FunctionType.Pastor, parish.Id), Fn(FunctionType.Pastor, parish.Id) }), default));
        var wrongParish = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(NewPerson("C", new[] { Fn(FunctionType.Acolyte, parish.Id) }), default));
        var unknown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(NewPerson("D", new[] { Fn(FunctionType.Pastor, Guid.NewGuid()) }), default));

        Assert.Contains("tylko raz", twice.Message);
        Assert.Contains("tylko raz", sameParishTwice.Message);
        Assert.Contains("tylko dla proboszcza", wrongParish.Message);
        Assert.Contains("Nie znaleziono parafii", unknown.Message);
        Assert.Empty(db.People);
    }

    [Fact]
    public async Task Search_CanFilterByFunction_AndListsTheFunctionsOfEachPerson()
    {
        await using var db = CreateContext();
        var service = new PersonService(db);
        await service.CreateAsync(NewPerson("Akolita", new[] { Fn(FunctionType.Acolyte) }), default);
        await service.CreateAsync(NewPerson("Lektor", new[] { Fn(FunctionType.Lector), Fn(FunctionType.Acolyte) }), default);
        await service.CreateAsync(NewPerson("Zwykly"), default);

        var all = await service.SearchAsync(null, 1, 20, default);
        var acolytes = await service.SearchAsync(null, 1, 20, default, FunctionType.Acolyte);
        var lectors = await service.SearchAsync(null, 1, 20, default, FunctionType.Lector);

        Assert.Equal(3, all.TotalCount);
        Assert.Equal(new[] { "Jan Akolita", "Jan Lektor" }, acolytes.Items.Select(p => p.FullName));
        Assert.Equal(2, acolytes.TotalCount);
        Assert.Equal(new[] { "Jan Lektor" }, lectors.Items.Select(p => p.FullName));
        Assert.Equal(2, lectors.Items.Single().Functions.Count);
    }

    [Fact]
    public async Task Search_ByFunction_CombinesWithTheTextQuery()
    {
        await using var db = CreateContext();
        var service = new PersonService(db);
        await service.CreateAsync(NewPerson("Kowalski", new[] { Fn(FunctionType.Acolyte) }), default);
        await service.CreateAsync(NewPerson("Nowak", new[] { Fn(FunctionType.Acolyte) }), default);

        var found = await service.SearchAsync("kowal", 1, 20, default, FunctionType.Acolyte);

        Assert.Equal(new[] { "Jan Kowalski" }, found.Items.Select(p => p.FullName));
    }

    [Fact]
    public async Task ADeletedPerson_NoLongerShowsUpInTheFunctionLists()
    {
        await using var db = CreateContext();
        var service = new PersonService(db);
        var created = await service.CreateAsync(NewPerson("Kowalski", new[] { Fn(FunctionType.Lector) }), default);

        await service.DeleteAsync(created.Id, "u", default);

        Assert.Empty((await service.SearchAsync(null, 1, 20, default, FunctionType.Lector)).Items);
    }
}
