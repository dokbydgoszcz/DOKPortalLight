using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class GraduateSearchTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task AddCaseAsync(AppDbContext db, string firstName, string lastName, DokStage stage, DateTime? completedAtUtc = null, bool deleted = false)
    {
        var person = new Person { Id = Guid.NewGuid(), FirstName = firstName, LastName = lastName, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var catechist = new Person { Id = Guid.NewGuid(), FirstName = "Katechista", LastName = "Prowadzący", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.People.AddRange(person, catechist);
        db.DokCases.Add(new DokCase
        {
            Id = Guid.NewGuid(), PersonId = person.Id, CatechistPersonId = catechist.Id, Path = DokPath.Confirmation, Stage = stage,
            CompletedAtUtc = completedAtUtc, DeletedAtUtc = deleted ? DateTime.UtcNow : null,
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task SearchGraduatesAsync_ReturnsOnlyGraduates_NewestCompletionFirst()
    {
        await using var db = CreateContext();
        await AddCaseAsync(db, "Jan", "Kowalski", DokStage.Evangelization);
        await AddCaseAsync(db, "Anna", "Maj", DokStage.Graduate, new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));
        await AddCaseAsync(db, "Piotr", "Nowak", DokStage.Graduate, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        await AddCaseAsync(db, "Ewa", "Zielińska", DokStage.CloserFormation);
        var service = new DokCaseService(db);

        var result = await service.SearchGraduatesAsync(null, 1, 20, default);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(new[] { "Piotr Nowak", "Anna Maj" }, result.Items.Select(i => i.PersonFullName));
    }

    [Fact]
    public async Task SearchGraduatesAsync_ExcludesSoftDeletedCases()
    {
        await using var db = CreateContext();
        await AddCaseAsync(db, "Anna", "Maj", DokStage.Graduate, DateTime.UtcNow);
        await AddCaseAsync(db, "Piotr", "Nowak", DokStage.Graduate, DateTime.UtcNow, deleted: true);
        var service = new DokCaseService(db);

        var result = await service.SearchGraduatesAsync(null, 1, 20, default);

        Assert.Single(result.Items);
        Assert.Equal("Anna Maj", result.Items[0].PersonFullName);
    }

    [Theory]
    [InlineData("maj")]
    [InlineData("  ANNA ")]
    public async Task SearchGraduatesAsync_FiltersByFirstOrLastName_IgnoringCaseAndWhitespace(string query)
    {
        await using var db = CreateContext();
        await AddCaseAsync(db, "Anna", "Maj", DokStage.Graduate, DateTime.UtcNow);
        await AddCaseAsync(db, "Piotr", "Nowak", DokStage.Graduate, DateTime.UtcNow);
        var service = new DokCaseService(db);

        var result = await service.SearchGraduatesAsync(query, 1, 20, default);

        Assert.Single(result.Items);
        Assert.Equal("Anna Maj", result.Items[0].PersonFullName);
    }

    [Fact]
    public async Task SearchGraduatesAsync_Paginates_AndReportsTotalAcrossAllPages()
    {
        await using var db = CreateContext();
        for (var i = 0; i < 25; i++)
        {
            await AddCaseAsync(db, "Osoba", $"Nazwisko{i:D2}", DokStage.Graduate, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i));
        }
        var service = new DokCaseService(db);

        var page1 = await service.SearchGraduatesAsync(null, 1, 20, default);
        var page2 = await service.SearchGraduatesAsync(null, 2, 20, default);

        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(20, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);
        Assert.Equal("Osoba Nazwisko24", page1.Items[0].PersonFullName);
        Assert.Equal("Osoba Nazwisko00", page2.Items[^1].PersonFullName);
    }
}
