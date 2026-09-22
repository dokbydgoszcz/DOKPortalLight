using DokPortal.Domain.Enums;

namespace DokPortal.Domain.Entities;

public class BudgetEntry
{
    public Guid Id { get; set; }
    public BudgetFund Fund { get; set; }
    public DateOnly EntryDate { get; set; }
    public required string Description { get; set; }
    public required string Category { get; set; }
    public BudgetEntryType Type { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
