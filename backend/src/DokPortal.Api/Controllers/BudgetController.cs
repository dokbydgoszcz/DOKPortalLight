using System.IdentityModel.Tokens.Jwt;
using DokPortal.Api.Filters;
using DokPortal.Application.Attachments;
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
    private readonly IAttachmentService _attachmentService;

    public BudgetController(IBudgetService budgetService, IAuthorizationService authorization, IAttachmentService attachmentService)
    {
        _budgetService = budgetService;
        _authorization = authorization;
        _attachmentService = attachmentService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BudgetEntryDto>>> GetEntries(
        [FromQuery] BudgetFund fund, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null, CancellationToken ct = default)
    {
        if (!await IsAllowedAsync(fund, manage: false)) return Forbid();

        return Ok(await _budgetService.GetEntriesAsync(fund, ct, from, to));
    }

    /// <summary>Saldo otwarcia, przychody, wydatki i saldo na koniec okresu.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<BudgetSummaryDto>> Summary(
        [FromQuery] BudgetFund fund, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null, CancellationToken ct = default)
    {
        if (!await IsAllowedAsync(fund, manage: false)) return Forbid();
        return Ok(await _budgetService.GetSummaryAsync(fund, from, to, ct));
    }

    [HttpGet("export")]
    [Audit("ExportBudget")]
    public async Task<IActionResult> Export(
        [FromQuery] BudgetFund fund, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null, CancellationToken ct = default)
    {
        if (!await IsAllowedAsync(fund, manage: false)) return Forbid();
        var bytes = await _budgetService.ExportAsync(fund, from, to, ct);
        var name = fund == BudgetFund.DOK ? "budzet-dok" : "budzet-sksp";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{name}.xlsx");
    }

    [HttpPost]
    [Audit("CreateBudgetEntry")]
    public async Task<ActionResult<BudgetEntryDto>> Create(CreateBudgetEntryRequest request, CancellationToken ct)
    {
        if (!await IsAllowedAsync(request.Fund, manage: true)) return Forbid();

        var created = await _budgetService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetEntries), new { fund = created.Fund }, created);
    }

    [HttpPut("{id:guid}")]
    [Audit("UpdateBudgetEntry")]
    public async Task<ActionResult<BudgetEntryDto>> Update(Guid id, UpdateBudgetEntryRequest request, CancellationToken ct)
    {
        var fund = await _budgetService.GetFundAsync(id, ct);
        if (fund is null) return NotFound();
        if (!await IsAllowedAsync(fund.Value, manage: true)) return Forbid();

        var updated = await _budgetService.UpdateAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [Audit("DeleteBudgetEntry")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var fund = await _budgetService.GetFundAsync(id, ct);
        if (fund is null) return NotFound();
        if (!await IsAllowedAsync(fund.Value, manage: true)) return Forbid();

        var currentUserId = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        var deleted = await _budgetService.DeleteAsync(id, currentUserId, ct);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("{id:guid}/attachments")]
    [Audit("UploadBudgetAttachment")]
    [RequestSizeLimit(AttachmentRules.MaxRequestBytes)]
    public async Task<ActionResult<AttachmentDto>> AddAttachment(Guid id, IFormFile file, CancellationToken ct)
    {
        var fund = await _budgetService.GetFundAsync(id, ct);
        if (fund is null) return NotFound();
        if (!await IsAllowedAsync(fund.Value, manage: true)) return Forbid();

        var currentUserId = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        await using var stream = file.OpenReadStream();
        return Ok(await _attachmentService.AddAsync(AttachmentOwnerType.BudgetEntry, id, stream, file.FileName, file.Length, currentUserId, ct));
    }

    [HttpGet("{id:guid}/attachments/{attachmentId:guid}/download")]
    [Audit("DownloadBudgetAttachment")]
    public async Task<IActionResult> DownloadAttachment(Guid id, Guid attachmentId, CancellationToken ct)
    {
        var fund = await _budgetService.GetFundAsync(id, ct);
        if (fund is null) return NotFound();
        if (!await IsAllowedAsync(fund.Value, manage: false)) return Forbid();

        var result = await _attachmentService.DownloadAsync(AttachmentOwnerType.BudgetEntry, id, attachmentId, ct);
        if (result is null) return NotFound();
        var (file, fileName) = result.Value;
        return File(file.Content, file.ContentType, fileName);
    }

    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    [Audit("DeleteBudgetAttachment")]
    public async Task<IActionResult> DeleteAttachment(Guid id, Guid attachmentId, CancellationToken ct)
    {
        var fund = await _budgetService.GetFundAsync(id, ct);
        if (fund is null) return NotFound();
        if (!await IsAllowedAsync(fund.Value, manage: true)) return Forbid();

        var deleted = await _attachmentService.DeleteAsync(AttachmentOwnerType.BudgetEntry, id, attachmentId, ct);
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
