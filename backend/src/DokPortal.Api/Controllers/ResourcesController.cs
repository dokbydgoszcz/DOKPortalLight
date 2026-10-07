using System.IdentityModel.Tokens.Jwt;
using DokPortal.Api.Authorization;
using DokPortal.Application.Attachments;
using DokPortal.Application.Resources;
using DokPortal.Domain.Constants;
using DokPortal.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

/// <summary>Globalna biblioteka zasobów dla katechistów (poza sprawami i superwizjami).</summary>
[ApiController]
[Route("api/resources")]
[Authorize]
public class ResourcesController : ControllerBase
{
    private readonly IResourceService _resourceService;
    private readonly IAttachmentService _attachmentService;

    public ResourcesController(IResourceService resourceService, IAttachmentService attachmentService)
    {
        _resourceService = resourceService;
        _attachmentService = attachmentService;
    }

    private string CurrentUserId => User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;

    [HttpGet]
    [HasPermission(Permissions.ResourcesView)]
    public async Task<ActionResult<IReadOnlyList<ResourceDto>>> List([FromQuery] string? query, CancellationToken ct)
        => Ok(await _resourceService.ListAsync(query, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.ResourcesView)]
    public async Task<ActionResult<ResourceDto>> GetById(Guid id, CancellationToken ct)
    {
        var resource = await _resourceService.GetByIdAsync(id, ct);
        return resource is null ? NotFound() : Ok(resource);
    }

    [HttpPost]
    [HasPermission(Permissions.ResourcesManage)]
    public async Task<ActionResult<ResourceDto>> Create(CreateResourceRequest request, CancellationToken ct)
    {
        var created = await _resourceService.CreateAsync(request, CurrentUserId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.ResourcesManage)]
    public async Task<ActionResult<ResourceDto>> Update(Guid id, UpdateResourceRequest request, CancellationToken ct)
    {
        var updated = await _resourceService.UpdateAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.ResourcesManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => await _resourceService.DeleteAsync(id, CurrentUserId, ct) ? NoContent() : NotFound();

    [HttpPost("{id:guid}/attachments")]
    [HasPermission(Permissions.ResourcesManage)]
    [RequestSizeLimit(AttachmentRules.MaxRequestBytes)]
    public async Task<ActionResult<AttachmentDto>> AddAttachment(Guid id, IFormFile file, CancellationToken ct)
    {
        if (await _resourceService.GetByIdAsync(id, ct) is null) return NotFound();

        await using var stream = file.OpenReadStream();
        return Ok(await _attachmentService.AddAsync(AttachmentOwnerType.Resource, id, stream, file.FileName, file.Length, CurrentUserId, ct));
    }

    [HttpGet("{id:guid}/attachments/{attachmentId:guid}/download")]
    [HasPermission(Permissions.ResourcesView)]
    public async Task<IActionResult> DownloadAttachment(Guid id, Guid attachmentId, CancellationToken ct)
    {
        if (await _resourceService.GetByIdAsync(id, ct) is null) return NotFound();

        var result = await _attachmentService.DownloadAsync(AttachmentOwnerType.Resource, id, attachmentId, ct);
        if (result is null) return NotFound();

        var (file, fileName) = result.Value;
        return File(file.Content, file.ContentType, fileName);
    }

    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    [HasPermission(Permissions.ResourcesManage)]
    public async Task<IActionResult> DeleteAttachment(Guid id, Guid attachmentId, CancellationToken ct)
    {
        if (await _resourceService.GetByIdAsync(id, ct) is null) return NotFound();

        return await _attachmentService.DeleteAsync(AttachmentOwnerType.Resource, id, attachmentId, ct) ? NoContent() : NotFound();
    }
}
