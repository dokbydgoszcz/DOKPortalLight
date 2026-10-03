using DokPortal.Application.DokCases;

namespace DokPortal.Infrastructure.Services;

/// <summary>Bez ograniczeń – dla usług systemowych i testów serwisów, które nie dotyczą zakresu.</summary>
public sealed class AllCasesScopeProvider : ICaseScopeProvider
{
    public Task<CaseScope> GetAsync(CancellationToken ct) => Task.FromResult(CaseScope.All);
}
