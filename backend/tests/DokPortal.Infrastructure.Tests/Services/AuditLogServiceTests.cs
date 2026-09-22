using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class AuditLogServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);

    [Fact]
    public async Task LogAsync_ThenListAsync_ReturnsNewestFirst()
    {
        await using var db = CreateContext(Guid.NewGuid().ToString());
        var service = new AuditLogService(db);

        await service.LogAsync("user-1", "user1@example.org", "ReadPastoralNotes", "Jan Kowalski", AuditResult.Blocked, default);
        await service.LogAsync("user-2", "user2@example.org", "AssignUserRoles", "target@example.org", AuditResult.Allowed, default);

        var entries = await service.ListAsync(default);

        Assert.Equal(2, entries.Count);
        Assert.Equal("AssignUserRoles", entries[0].Action);
        Assert.Equal(AuditResult.Blocked, entries[1].Result);
    }
}
