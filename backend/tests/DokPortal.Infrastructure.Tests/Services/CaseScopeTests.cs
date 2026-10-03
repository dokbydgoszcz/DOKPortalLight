using DokPortal.Application.DokCases;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class CaseScopeTests
{
    private static async Task<(AppDbContext Db, Guid CatechistA, Guid CatechistB)> SeedAsync()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var a = new Person { Id = Guid.NewGuid(), FirstName = "Anna", LastName = "Maj", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var b = new Person { Id = Guid.NewGuid(), FirstName = "Beata", LastName = "Lis", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var p1 = new Person { Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var p2 = new Person { Id = Guid.NewGuid(), FirstName = "Piotr", LastName = "Nowak", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.AddRange(a, b, p1, p2);
        db.DokCases.AddRange(
            new DokCase { Id = Guid.NewGuid(), PersonId = p1.Id, CatechistPersonId = a.Id, Path = DokPath.Confirmation, Stage = DokStage.Formation, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
            new DokCase { Id = Guid.NewGuid(), PersonId = p2.Id, CatechistPersonId = b.Id, Path = DokPath.Confirmation, Stage = DokStage.Formation, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return (db, a.Id, b.Id);
    }

    [Fact]
    public async Task ForScope_ViewAll_ReturnsEveryCase()
    {
        var (db, _, _) = await SeedAsync();

        Assert.Equal(2, await db.DokCases.ForScope(CaseScope.All).CountAsync());
    }

    [Fact]
    public async Task ForScope_OwnCases_ReturnsOnlyCasesOfThatCatechist()
    {
        var (db, catechistA, _) = await SeedAsync();

        var cases = await db.DokCases.ForScope(new CaseScope(false, catechistA)).ToListAsync();

        Assert.Single(cases);
        Assert.Equal(catechistA, cases[0].CatechistPersonId);
    }

    [Fact]
    public async Task ForScope_UnknownPerson_ReturnsNothing()
    {
        var (db, _, _) = await SeedAsync();

        Assert.Empty(await db.DokCases.ForScope(new CaseScope(false, Guid.NewGuid())).ToListAsync());
    }

    [Fact]
    public async Task ForScope_WithoutPersonAndWithoutViewAll_ReturnsNothing()
    {
        var (db, _, _) = await SeedAsync();

        Assert.Empty(await db.DokCases.ForScope(new CaseScope(false, null)).ToListAsync());
    }
}
