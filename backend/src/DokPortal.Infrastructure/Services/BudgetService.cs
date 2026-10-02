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

    public async Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct)
    {
        var entry = await _db.BudgetEntries.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entry is null) return false;

        entry.DeletedAtUtc = DateTime.UtcNow;
        entry.DeletedBy = deletedBy;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<BudgetFund?> GetFundAsync(Guid id, CancellationToken ct)
    {
        var entry = await _db.BudgetEntries.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
        return entry?.Fund;
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
