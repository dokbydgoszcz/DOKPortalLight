using DokPortal.Api.Authorization;
using System.IdentityModel.Tokens.Jwt;
using DokPortal.Application.Attachments;
using DokPortal.Application.Supervisions;
using DokPortal.Domain.Constants;
using DokPortal.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/supervisions")]
[Authorize]
public class SupervisionsController : ControllerBase
{
    private readonly ISupervisionService _supervisionService;
    private readonly IAttachmentService _attachmentService;

    public SupervisionsController(ISupervisionService supervisionService, IAttachmentService attachmentService)
    {
        _supervisionService = supervisionService;
        _attachmentService = attachmentService;
    }

    [HttpGet]
    [HasPermission(Permissions.SupervisionsView)]
    public async Task<ActionResult<IReadOnlyList<SupervisionDto>>> GetAll([FromQuery] Institution? institution, CancellationToken ct)
        => Ok(await _supervisionService.GetAllAsync(institution, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.SupervisionsView)]
    public async Task<ActionResult<SupervisionDto>> GetById(Guid id, CancellationToken ct)
    {
        var supervision = await _supervisionService.GetByIdAsync(id, ct);
        return supervision is null ? NotFound() : Ok(supervision);
    }

    [HttpPost]
    [HasPermission(Permissions.SupervisionsManage)]
    public async Task<ActionResult<SupervisionDto>> Create(CreateSupervisionRequest request, CancellationToken ct)
    {
        var created = await _supervisionService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAll), created);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.SupervisionsManage)]
    public async Task<ActionResult<SupervisionDto>> Update(Guid id, CreateSupervisionRequest request, CancellationToken ct)
    {
        var updated = await _supervisionService.UpdateAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.SupervisionsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var currentUserId = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        var deleted = await _supervisionService.DeleteAsync(id, currentUserId, ct);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("{id:guid}/attachments")]
    [HasPermission(Permissions.SupervisionsManage)]
    [RequestSizeLimit(AttachmentRules.MaxRequestBytes)]
    public async Task<ActionResult<AttachmentDto>> AddAttachment(Guid id, IFormFile file, CancellationToken ct)
    {
        if (await _supervisionService.GetByIdAsync(id, ct) is null) return NotFound();

        var currentUserId = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        await using var stream = file.OpenReadStream();
        var attachment = await _attachmentService.AddAsync(
            AttachmentOwnerType.Supervision, id, stream, file.FileName, file.Length, currentUserId, ct);
        return Ok(attachment);
    }

    [HttpGet("{id:guid}/attachments/{attachmentId:guid}/download")]
    [HasPermission(Permissions.SupervisionsView)]
    public async Task<IActionResult> DownloadAttachment(Guid id, Guid attachmentId, CancellationToken ct)
    {
        if (await _supervisionService.GetByIdAsync(id, ct) is null) return NotFound();

        var result = await _attachmentService.DownloadAsync(AttachmentOwnerType.Supervision, id, attachmentId, ct);
        if (result is null) return NotFound();

        var (file, fileName) = result.Value;
        return File(file.Content, file.ContentType, fileName);
    }

    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    [HasPermission(Permissions.SupervisionsManage)]
    public async Task<IActionResult> DeleteAttachment(Guid id, Guid attachmentId, CancellationToken ct)
    {
        if (await _supervisionService.GetByIdAsync(id, ct) is null) return NotFound();

        var deleted = await _attachmentService.DeleteAsync(AttachmentOwnerType.Supervision, id, attachmentId, ct);
        return deleted ? NoContent() : NotFound();
    }
}
