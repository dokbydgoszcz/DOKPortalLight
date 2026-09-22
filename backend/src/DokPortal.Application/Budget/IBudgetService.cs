using DokPortal.Domain.Enums;

namespace DokPortal.Application.Budget;

public interface IBudgetService
{
    Task<IReadOnlyList<BudgetEntryDto>> GetEntriesAsync(BudgetFund fund, CancellationToken ct);
    Task<BudgetEntryDto> CreateAsync(CreateBudgetEntryRequest request, CancellationToken ct);
}
