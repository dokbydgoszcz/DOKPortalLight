using DokPortal.Domain.Enums;

namespace DokPortal.Application.Budget;

public interface IBudgetService
{
    /// <summary>Wpisy funduszu, opcjonalnie z okresu [from, to] (obie daty włącznie); najnowsze pierwsze.</summary>
    Task<IReadOnlyList<BudgetEntryDto>> GetEntriesAsync(BudgetFund fund, CancellationToken ct, DateOnly? from = null, DateOnly? to = null);
    Task<BudgetSummaryDto> GetSummaryAsync(BudgetFund fund, DateOnly? from, DateOnly? to, CancellationToken ct);
    /// <summary>Wpisy okresu i saldo do Excela.</summary>
    Task<byte[]> ExportAsync(BudgetFund fund, DateOnly? from, DateOnly? to, CancellationToken ct);
    Task<BudgetEntryDto> CreateAsync(CreateBudgetEntryRequest request, CancellationToken ct);
    Task<BudgetEntryDto?> UpdateAsync(Guid id, UpdateBudgetEntryRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, string deletedBy, CancellationToken ct);
    Task<BudgetFund?> GetFundAsync(Guid id, CancellationToken ct);
}
