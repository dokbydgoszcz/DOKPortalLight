using DokPortal.Application.Budget;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class BudgetService : IBudgetService
{
    private readonly AppDbContext _db;

    public BudgetService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<BudgetEntryDto>> GetEntriesAsync(BudgetFund fund, CancellationToken ct)
    {
        var entries = await _db.BudgetEntries.AsNoTracking()
            .Where(e => e.Fund == fund)
            .OrderByDescending(e => e.EntryDate)
            .ToListAsync(ct);
        return entries.Select(ToDto).ToList();
    }

    public async Task<BudgetEntryDto> CreateAsync(CreateBudgetEntryRequest request, CancellationToken ct)
    {
        var entry = new BudgetEntry
        {
            Id = Guid.NewGuid(),
            Fund = request.Fund,
            EntryDate = request.EntryDate,
            Description = request.Description,
            Category = request.Category,
            Type = request.Type,
            Amount = request.Amount,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.BudgetEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
        return ToDto(entry);
    }

    private static BudgetEntryDto ToDto(BudgetEntry e) => new()
    {
        Id = e.Id,
        Fund = e.Fund,
        EntryDate = e.EntryDate,
        Description = e.Description,
        Category = e.Category,
        Type = e.Type,
        Amount = e.Amount
    };
}
