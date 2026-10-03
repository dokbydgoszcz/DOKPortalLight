namespace DokPortal.Application.DokCases;

public interface ICaseScopeProvider
{
    Task<CaseScope> GetAsync(CancellationToken ct);
}
