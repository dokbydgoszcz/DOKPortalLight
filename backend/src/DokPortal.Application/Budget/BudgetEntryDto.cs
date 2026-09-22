using DokPortal.Domain.Enums;

namespace DokPortal.Application.Budget;

public class BudgetEntryDto
{
    public required Guid Id { get; init; }
    public required BudgetFund Fund { get; init; }
    public required DateOnly EntryDate { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required BudgetEntryType Type { get; init; }
    public required decimal Amount { get; init; }
}
