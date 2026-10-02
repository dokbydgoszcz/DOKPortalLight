using DokPortal.Application.Budget;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class BudgetServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task CreateAsync_ThenGetEntriesAsync_FiltersOnlyRequestedFund()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var service = new BudgetService(db);

        await service.CreateAsync(new CreateBudgetEntryRequest
        {
            Fund = BudgetFund.SKSP, EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Description = "Wynajem sali", Category = "Organizacja", Type = BudgetEntryType.Expense, Amount = 450m
        }, default);
        await service.CreateAsync(new CreateBudgetEntryRequest
        {
            Fund = BudgetFund.DOK, EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Description = "Materiały formacyjne", Category = "Materiały", Type = BudgetEntryType.Expense, Amount = 780m
        }, default);

        var skspEntries = await service.GetEntriesAsync(BudgetFund.SKSP, default);

        Assert.Single(skspEntries);
        Assert.Equal("Wynajem sali", skspEntries[0].Description);
    }

    [Fact]
    public async Task GetFundAsync_ReturnsFundOfExistingEntry_AndNullForMissingOrDeleted()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var service = new BudgetService(db);
        var dokEntry = await service.CreateAsync(new CreateBudgetEntryRequest
        {
            Fund = BudgetFund.DOK, EntryDate = new DateOnly(2026, 9, 18), Description = "Opis",
            Category = "Kat", Type = BudgetEntryType.Expense, Amount = 10m
        }, default);

        Assert.Equal(BudgetFund.DOK, await service.GetFundAsync(dokEntry.Id, default));
        Assert.Null(await service.GetFundAsync(Guid.NewGuid(), default));

        await service.DeleteAsync(dokEntry.Id, "user-1", default);
        Assert.Null(await service.GetFundAsync(dokEntry.Id, default));
    }
}
