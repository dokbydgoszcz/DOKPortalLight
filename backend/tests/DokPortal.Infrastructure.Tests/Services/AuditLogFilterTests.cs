using DokPortal.Application.AuditLog;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class AuditLogFilterTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static AuditLogEntry Entry(string email, string action, string obj, AuditResult result, DateTime at) => new()
    {
        Id = Guid.NewGuid(), TimestampUtc = at, UserId = email, UserEmail = email, Action = action, ObjectDescription = obj, Result = result
    };

    private static async Task<AuditLogService> SeedAsync(AppDbContext db)
    {
        db.AuditLogEntries.AddRange(
            Entry("anna@example.org", "ReadPastoralNotes", "Jan Kowalski", AuditResult.Allowed, new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc)),
            Entry("beata@example.org", "ReadPastoralNotes", "Piotr Nowak", AuditResult.Blocked, new DateTime(2026, 10, 2, 23, 59, 0, DateTimeKind.Utc)),
            Entry("admin@example.org", "AssignUserRoles", "anna@example.org", AuditResult.Allowed, new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc)),
            Entry("admin@example.org", "ResetUserPassword", "beata@example.org", AuditResult.Allowed, new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();
        return new AuditLogService(db);
    }

    [Fact]
    public async Task ListAsync_WithoutFilters_ReturnsEverythingNewestFirst()
    {
        await using var db = CreateContext();
        var service = await SeedAsync(db);

        var all = await service.ListAsync(new AuditLogFilter(), default);

        Assert.Equal(new[] { "ResetUserPassword", "AssignUserRoles", "ReadPastoralNotes", "ReadPastoralNotes" }, all.Select(e => e.Action));
    }

    [Theory]
    [InlineData("anna", 2)]
    [InlineData("KOWALSKI", 1)]
    [InlineData("  assign ", 1)]
    [InlineData("nic-takiego", 0)]
    public async Task ListAsync_SearchesUserActionAndObjectIgnoringCase(string search, int expected)
    {
        await using var db = CreateContext();
        var service = await SeedAsync(db);

        var found = await service.ListAsync(new AuditLogFilter { Search = search }, default);

        Assert.Equal(expected, found.Count);
    }

    [Fact]
    public async Task ListAsync_FiltersByActionAndResult()
    {
        await using var db = CreateContext();
        var service = await SeedAsync(db);

        var byAction = await service.ListAsync(new AuditLogFilter { Action = "ReadPastoralNotes" }, default);
        var blocked = await service.ListAsync(new AuditLogFilter { Result = AuditResult.Blocked }, default);
        var both = await service.ListAsync(new AuditLogFilter { Action = "AssignUserRoles", Result = AuditResult.Blocked }, default);

        Assert.Equal(2, byAction.Count);
        Assert.Equal("Piotr Nowak", Assert.Single(blocked).ObjectDescription);
        Assert.Empty(both);
    }

    [Fact]
    public async Task ListAsync_DateRangeIsInclusiveOfWholeDays()
    {
        await using var db = CreateContext();
        var service = await SeedAsync(db);

        var middle = await service.ListAsync(new AuditLogFilter { From = new DateOnly(2026, 10, 2), To = new DateOnly(2026, 10, 3) }, default);
        var onlyFrom = await service.ListAsync(new AuditLogFilter { From = new DateOnly(2026, 10, 4) }, default);
        var onlyTo = await service.ListAsync(new AuditLogFilter { To = new DateOnly(2026, 10, 1) }, default);

        Assert.Equal(new[] { "AssignUserRoles", "ReadPastoralNotes" }, middle.Select(e => e.Action));
        Assert.Equal("ResetUserPassword", Assert.Single(onlyFrom).Action);
        Assert.Equal("Jan Kowalski", Assert.Single(onlyTo).ObjectDescription);
    }

    [Fact]
    public async Task ListAsync_TakeLimitsToTheNewestEntries_AndIsClamped()
    {
        await using var db = CreateContext();
        var service = await SeedAsync(db);

        var two = await service.ListAsync(new AuditLogFilter { Take = 2 }, default);
        var zero = await service.ListAsync(new AuditLogFilter { Take = 0 }, default);
        var huge = await service.ListAsync(new AuditLogFilter { Take = 1_000_000 }, default);

        Assert.Equal(new[] { "ResetUserPassword", "AssignUserRoles" }, two.Select(e => e.Action));
        Assert.Single(zero);
        Assert.Equal(4, huge.Count);
    }

    [Fact]
    public async Task ListActionsAsync_ReturnsDistinctActionNamesSorted()
    {
        await using var db = CreateContext();
        var service = await SeedAsync(db);

        var actions = await service.ListActionsAsync(default);

        Assert.Equal(new[] { "AssignUserRoles", "ReadPastoralNotes", "ResetUserPassword" }, actions);
    }
}
