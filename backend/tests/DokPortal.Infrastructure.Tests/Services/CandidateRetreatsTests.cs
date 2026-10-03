using DokPortal.Application.Candidates;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

/// <summary>Rekolekcje kandydata: jeden rekord na rok formacji (1–3), zapisywane razem z kandydatem.</summary>
public class CandidateRetreatsTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<Guid> SeedPersonAsync(AppDbContext db)
    {
        var person = new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.Add(person);
        await db.SaveChangesAsync();
        return person.Id;
    }

    private static CandidateRetreatDto Retreat(int year, bool completed) => new() { Year = year, IsCompleted = completed };

    [Fact]
    public async Task CreateAsync_StoresTheRetreatsPerYear_OrderedByYear()
    {
        await using var db = CreateContext();
        var service = new CandidateService(db);

        var created = await service.CreateAsync(new CreateCandidateRequest
        {
            PersonId = await SeedPersonAsync(db), Year = 2, OpinionsCollected = 0,
            Retreats = new[] { Retreat(2, false), Retreat(1, true) }
        }, default);

        Assert.Equal(new[] { (1, true), (2, false) }, created.Retreats.Select(r => (r.Year, r.IsCompleted)));
        Assert.Equal(2, await db.CandidateRetreats.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_WithoutRetreats_GivesAnEmptyList()
    {
        await using var db = CreateContext();
        var service = new CandidateService(db);

        var created = await service.CreateAsync(new CreateCandidateRequest
        {
            PersonId = await SeedPersonAsync(db), Year = 1, OpinionsCollected = 0
        }, default);

        Assert.Empty(created.Retreats);
    }

    [Fact]
    public async Task UpdateAsync_ReplacesTheWholeSet_ChangingAddingAndRemovingRetreats()
    {
        await using var db = CreateContext();
        var service = new CandidateService(db);
        var personId = await SeedPersonAsync(db);
        var created = await service.CreateAsync(new CreateCandidateRequest
        {
            PersonId = personId, Year = 2, OpinionsCollected = 0,
            Retreats = new[] { Retreat(1, true), Retreat(2, false) }
        }, default);

        var updated = await service.UpdateAsync(created.Id, new UpdateCandidateRequest
        {
            PersonId = personId, Year = 2, OpinionsCollected = 1,
            Retreats = new[] { Retreat(2, true), Retreat(3, false) }
        }, default);

        Assert.Equal(new[] { (2, true), (3, false) }, updated!.Retreats.Select(r => (r.Year, r.IsCompleted)));
        Assert.Equal(2, await db.CandidateRetreats.CountAsync());
    }

    [Fact]
    public async Task UpdateAsync_WithNoRetreats_ClearsThem()
    {
        await using var db = CreateContext();
        var service = new CandidateService(db);
        var personId = await SeedPersonAsync(db);
        var created = await service.CreateAsync(new CreateCandidateRequest
        {
            PersonId = personId, Year = 1, OpinionsCollected = 0, Retreats = new[] { Retreat(1, true) }
        }, default);

        var updated = await service.UpdateAsync(created.Id, new UpdateCandidateRequest { PersonId = personId, Year = 1, OpinionsCollected = 0 }, default);

        Assert.Empty(updated!.Retreats);
        Assert.Empty(db.CandidateRetreats);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    public async Task Create_And_Update_RejectAYearOutsideOneToThree(int year)
    {
        await using var db = CreateContext();
        var service = new CandidateService(db);
        var personId = await SeedPersonAsync(db);
        var created = await service.CreateAsync(new CreateCandidateRequest { PersonId = personId, Year = 1, OpinionsCollected = 0 }, default);

        var onCreate = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(new CreateCandidateRequest
        {
            PersonId = personId, Year = 1, OpinionsCollected = 0, Retreats = new[] { Retreat(year, true) }
        }, default));
        var onUpdate = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(created.Id, new UpdateCandidateRequest
        {
            PersonId = personId, Year = 1, OpinionsCollected = 0, Retreats = new[] { Retreat(year, true) }
        }, default));

        Assert.Contains("1–3", onCreate.Message);
        Assert.Contains("1–3", onUpdate.Message);
    }

    [Fact]
    public async Task Create_And_Update_RejectTwoRetreatsInTheSameYear()
    {
        await using var db = CreateContext();
        var service = new CandidateService(db);
        var personId = await SeedPersonAsync(db);
        var created = await service.CreateAsync(new CreateCandidateRequest { PersonId = personId, Year = 1, OpinionsCollected = 0 }, default);
        var duplicate = new[] { Retreat(1, true), Retreat(1, false) };

        var onCreate = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(new CreateCandidateRequest
        {
            PersonId = personId, Year = 1, OpinionsCollected = 0, Retreats = duplicate
        }, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(created.Id, new UpdateCandidateRequest
        {
            PersonId = personId, Year = 1, OpinionsCollected = 0, Retreats = duplicate
        }, default));

        Assert.Contains("jedne rekolekcje", onCreate.Message);
        Assert.Empty(db.CandidateRetreats);
    }

    [Fact]
    public async Task SearchAsync_AndGetByIdAsync_ListTheRetreats_AndDeletedCandidatesHideThem()
    {
        await using var db = CreateContext();
        var service = new CandidateService(db);
        var created = await service.CreateAsync(new CreateCandidateRequest
        {
            PersonId = await SeedPersonAsync(db), Year = 1, OpinionsCollected = 0, Retreats = new[] { Retreat(1, true) }
        }, default);

        var page = await service.SearchAsync(null, 1, 20, default);
        var byId = await service.GetByIdAsync(created.Id, default);

        Assert.Single(page.Items.Single().Retreats);
        Assert.Single(byId!.Retreats);

        await service.DeleteAsync(created.Id, "u", default);
        Assert.Empty(db.CandidateRetreats);
    }
}
