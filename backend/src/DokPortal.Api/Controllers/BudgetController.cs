using System.IdentityModel.Tokens.Jwt;
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
    private readonly IAuthorizationService _authorization;

    public BudgetController(IBudgetService budgetService, IAuthorizationService authorization)
    {
        _budgetService = budgetService;
        _authorization = authorization;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BudgetEntryDto>>> GetEntries([FromQuery] BudgetFund fund, CancellationToken ct)
    {
        if (!await IsAllowedAsync(fund, manage: false)) return Forbid();

        return Ok(await _budgetService.GetEntriesAsync(fund, ct));
    }

    [HttpPost]
    public async Task<ActionResult<BudgetEntryDto>> Create(CreateBudgetEntryRequest request, CancellationToken ct)
    {
        if (!await IsAllowedAsync(request.Fund, manage: true)) return Forbid();

        var created = await _budgetService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetEntries), new { fund = created.Fund }, created);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var fund = await _budgetService.GetFundAsync(id, ct);
        if (fund is null) return NotFound();
        if (!await IsAllowedAsync(fund.Value, manage: true)) return Forbid();

        var currentUserId = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        var deleted = await _budgetService.DeleteAsync(id, currentUserId, ct);
        return deleted ? NoContent() : NotFound();
    }

    private async Task<bool> IsAllowedAsync(BudgetFund fund, bool manage)
    {
        var permission = (fund, manage) switch
        {
            (BudgetFund.SKSP, false) => Permissions.BudgetSkspView,
            (BudgetFund.SKSP, true) => Permissions.BudgetSkspManage,
            (BudgetFund.DOK, false) => Permissions.BudgetDokView,
            (BudgetFund.DOK, true) => Permissions.BudgetDokManage,
            _ => throw new InvalidOperationException("Nieznany fundusz budżetu.")
        };
        return (await _authorization.AuthorizeAsync(User, null, permission)).Succeeded;
    }
}
