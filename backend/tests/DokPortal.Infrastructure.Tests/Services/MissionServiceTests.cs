using DokPortal.Application.Missions;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class MissionServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    private static async Task<Guid> SeedPersonAsync(AppDbContext db, string firstName, string lastName)
    {
        var person = new Person
        {
            Id = Guid.NewGuid(), FirstName = firstName, LastName = lastName,
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        db.People.Add(person);
        await db.SaveChangesAsync();
        return person.Id;
    }

    [Fact]
    public async Task CreateAsync_WithEndDateFarInFuture_ReturnsWaznaStatus()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var personId = await SeedPersonAsync(db, "Marek", "Zielinski");
        var service = new MissionService(db);

        var created = await service.CreateAsync(new CreateMissionRequest
        {
            PersonId = personId,
            ServicePlace = "Parafia św. Józefa",
            MissionStartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MissionEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2))
        }, default);

        Assert.Equal("ważna", created.Status);
    }

    [Fact]
    public async Task CreateAsync_WithEndDateWithin30Days_ReturnsWygasaStatus()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var personId = await SeedPersonAsync(db, "Anna", "Maj");
        var service = new MissionService(db);

        var created = await service.CreateAsync(new CreateMissionRequest
        {
            PersonId = personId,
            ServicePlace = "Parafia św. Mateusza",
            MissionStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-3)),
            MissionEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10))
        }, default);

        Assert.Equal("wygasa", created.Status);
    }

    [Fact]
    public async Task CreateAsync_WithPastEndDate_ReturnsWygaslaStatus()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var personId = await SeedPersonAsync(db, "Maria", "Kaczmarek");
        var service = new MissionService(db);

        var created = await service.CreateAsync(new CreateMissionRequest
        {
            PersonId = personId,
            ServicePlace = "DOK",
            MissionStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-5)),
            MissionEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))
        }, default);

        Assert.Equal("wygasła", created.Status);
    }

    [Fact]
    public async Task SentToDok_DefaultsToFalse_AndIsStoredWhenSet()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var personId = await SeedPersonAsync(db, "Marek", "Zielinski");
        var service = new MissionService(db);
        var start = DateOnly.FromDateTime(DateTime.UtcNow);
        var end = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2));

        var plain = await service.CreateAsync(new CreateMissionRequest { PersonId = personId, ServicePlace = "A", MissionStartDate = start, MissionEndDate = end }, default);
        var sent = await service.CreateAsync(new CreateMissionRequest { PersonId = personId, ServicePlace = "B", MissionStartDate = start, MissionEndDate = end, SentToDok = true }, default);

        Assert.False(plain.SentToDok);
        Assert.True(sent.SentToDok);
        Assert.True((await service.GetByIdAsync(sent.Id, default))!.SentToDok);
        var page = await service.SearchAsync(null, 1, 20, default);
        Assert.Equal(new[] { false, true }, page.Items.OrderBy(i => i.ServicePlace).Select(i => i.SentToDok));
    }

    [Fact]
    public async Task UpdateAsync_ChangesTheSentToDokFlag()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var personId = await SeedPersonAsync(db, "Anna", "Maj");
        var service = new MissionService(db);
        var start = DateOnly.FromDateTime(DateTime.UtcNow);
        var end = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2));
        var created = await service.CreateAsync(new CreateMissionRequest { PersonId = personId, ServicePlace = "A", MissionStartDate = start, MissionEndDate = end }, default);

        var updated = await service.UpdateAsync(created.Id, new UpdateMissionRequest
        {
            PersonId = personId, ServicePlace = "A", MissionStartDate = start, MissionEndDate = end, SentToDok = true
        }, default);

        Assert.True(updated!.SentToDok);
    }
}
