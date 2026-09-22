using DokPortal.Application.Mailing;
using DokPortal.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/mailing/campaigns")]
[Authorize]
public class MailingController : ControllerBase
{
    private readonly IMailingService _mailingService;

    public MailingController(IMailingService mailingService) => _mailingService = mailingService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MailingCampaignDto>>> List(CancellationToken ct)
        => Ok(await _mailingService.ListAsync(ct));

    [HttpPost]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorSKSP},{AppRoles.DyrektorDOK}")]
    public async Task<ActionResult<MailingCampaignDto>> Create(CreateMailingCampaignRequest request, CancellationToken ct)
        => Ok(await _mailingService.CreateAsync(request, ct));

    [HttpPost("{id:guid}/send")]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorSKSP},{AppRoles.DyrektorDOK}")]
    public async Task<ActionResult<MailingCampaignDto>> Send(Guid id, CancellationToken ct)
    {
        var sent = await _mailingService.SendAsync(id, ct);
        return sent is null ? NotFound() : Ok(sent);
    }
}
