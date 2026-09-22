using DokPortal.Application.Budget;
using DokPortal.Domain.Constants;
using DokPortal.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/budget")]
[Authorize]
public class BudgetController : ControllerBase
{
    private readonly IBudgetService _budgetService;

    public BudgetController(IBudgetService budgetService) => _budgetService = budgetService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BudgetEntryDto>>> GetEntries([FromQuery] BudgetFund fund, CancellationToken ct)
        => Ok(await _budgetService.GetEntriesAsync(fund, ct));

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorSKSP}")]
    public async Task<ActionResult<BudgetEntryDto>> Create(CreateBudgetEntryRequest request, CancellationToken ct)
    {
        var created = await _budgetService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetEntries), new { fund = created.Fund }, created);
    }
}
