using DokPortal.Application.Attachments;
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
    /// <summary>Faktury, paragony i inne dowody księgowe.</summary>
    public IReadOnlyList<AttachmentDto> Attachments { get; init; } = Array.Empty<AttachmentDto>();
}

/// <summary>Zestawienie okresu: saldo przed okresem, przychody i wydatki w okresie, saldo na koniec.</summary>
public class BudgetSummaryDto
{
    public required decimal OpeningBalance { get; init; }
    public required decimal Income { get; init; }
    public required decimal Expense { get; init; }
    public required decimal ClosingBalance { get; init; }
}
