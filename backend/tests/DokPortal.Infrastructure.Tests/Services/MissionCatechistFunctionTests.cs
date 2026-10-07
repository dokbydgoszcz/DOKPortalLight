using DokPortal.Application.Missions;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

/// <summary>Katechista jest funkcją osoby: misja kanoniczna nadaje ją automatycznie (raz), usunięcie misji jej nie odbiera.</summary>
public class MissionCatechistFunctionTests
{
    private static readonly FixedTimeProvider Time = new(2026, 10, 3);

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<Person> AddPersonAsync(AppDbContext db, string last)
    {
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = last, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.Add(person);
        await db.SaveChangesAsync();
        return person;
    }

    private static CreateMissionRequest NewMission(Guid personId) => new()
    {
        PersonId = personId, ServicePlace = "Parafia", MissionStartDate = new DateOnly(2026, 9, 1), MissionEndDate = new DateOnly(2027, 9, 1)
    };

    private static Task<List<FunctionType>> FunctionsOfAsync(AppDbContext db, Guid personId) =>
        db.PersonFunctions.AsNoTracking().Where(f => f.PersonId == personId).Select(f => f.Type).ToListAsync();

    [Fact]
    public async Task CreatingAMission_GivesThePersonTheCatechistFunction()
    {
        await using var db = CreateContext();
        var person = await AddPersonAsync(db, "Kowalski");

        await new MissionService(db, Time).CreateAsync(NewMission(person.Id), default);

        Assert.Equal(new[] { FunctionType.Catechist }, await FunctionsOfAsync(db, person.Id));
    }

    [Fact]
    public async Task ASecondMission_DoesNotDuplicateTheFunction_AndKeepsOtherFunctions()
    {
        await using var db = CreateContext();
        var person = await AddPersonAsync(db, "Kowalski");
        db.PersonFunctions.Add(new PersonFunction { Id = Guid.NewGuid(), PersonId = person.Id, Type = FunctionType.Lector });
        await db.SaveChangesAsync();
        var service = new MissionService(db, Time);

        await service.CreateAsync(NewMission(person.Id), default);
        await service.CreateAsync(NewMission(person.Id), default);

        Assert.Equal(new[] { FunctionType.Catechist, FunctionType.Lector }, (await FunctionsOfAsync(db, person.Id)).OrderBy(t => t));
    }

    [Fact]
    public async Task ChangingTheMissionsPerson_GivesTheNewPersonTheFunction()
    {
        await using var db = CreateContext();
        var first = await AddPersonAsync(db, "Pierwszy");
        var second = await AddPersonAsync(db, "Drugi");
        var service = new MissionService(db, Time);
        var mission = await service.CreateAsync(NewMission(first.Id), default);

        await service.UpdateAsync(mission.Id, new UpdateMissionRequest
        {
            PersonId = second.Id, ServicePlace = "Parafia", MissionStartDate = new DateOnly(2026, 9, 1), MissionEndDate = new DateOnly(2027, 9, 1)
        }, default);

        Assert.Equal(new[] { FunctionType.Catechist }, await FunctionsOfAsync(db, second.Id));
    }

    [Fact]
    public async Task DeletingTheMission_KeepsTheFunction()
    {
        await using var db = CreateContext();
        var person = await AddPersonAsync(db, "Kowalski");
        var service = new MissionService(db, Time);
        var mission = await service.CreateAsync(NewMission(person.Id), default);

        await service.DeleteAsync(mission.Id, "u", default);

        Assert.Equal(new[] { FunctionType.Catechist }, await FunctionsOfAsync(db, person.Id));
    }

    [Fact]
    public async Task GrantingThePosting_AlsoMakesThePersonACatechist()
    {
        await using var db = CreateContext();
        var person = await AddPersonAsync(db, "Kowalski");
        db.Candidates.Add(new Candidate
        {
            Id = Guid.NewGuid(), PersonId = person.Id, FormationYear = 3, IsFormationCompleted = true,
            FormationYearSinceUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        await new MissionService(db, Time).GrantAsync(person.Id, default);

        Assert.Equal(new[] { FunctionType.Catechist }, await FunctionsOfAsync(db, person.Id));
    }
}
