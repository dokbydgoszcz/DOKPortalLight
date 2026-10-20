using System.IdentityModel.Tokens.Jwt;
using DokPortal.Api.Authorization;
using DokPortal.Api.Filters;
using DokPortal.Application.FormSubmissions;
using DokPortal.Domain.Constants;
using DokPortal.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

/// <summary>Przegląd zgłoszeń z formularzy (Tally) i przenoszenie ich do osób.</summary>
[ApiController]
[Route("api/form-submissions")]
[Authorize]
[HasPermission(Permissions.PeopleManage)]
public class FormSubmissionsController : ControllerBase
{
    private readonly IFormSubmissionService _submissions;

    public FormSubmissionsController(IFormSubmissionService submissions) => _submissions = submissions;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FormSubmissionDto>>> List([FromQuery] FormSubmissionStatus status = FormSubmissionStatus.Pending, CancellationToken ct = default) =>
        Ok(await _submissions.ListAsync(status, ct));

    [HttpGet("pending-count")]
    public async Task<ActionResult<int>> PendingCount(CancellationToken ct) => Ok(await _submissions.PendingCountAsync(ct));

    [HttpPost("{id:guid}/apply")]
    [Audit("ApplyFormSubmission")]
    public async Task<ActionResult<FormSubmissionDto>> Apply(Guid id, ApplyFormSubmissionRequest request, CancellationToken ct) =>
        await _submissions.ApplyAsync(id, request, ReviewerEmail(), ct) is { } dto ? Ok(dto) : NotFound();

    [HttpPost("{id:guid}/reject")]
    [Audit("RejectFormSubmission")]
    public async Task<ActionResult<FormSubmissionDto>> Reject(Guid id, CancellationToken ct) =>
        await _submissions.RejectAsync(id, ReviewerEmail(), ct) is { } dto ? Ok(dto) : NotFound();

    [HttpPost("{id:guid}/reopen")]
    public async Task<ActionResult<FormSubmissionDto>> Reopen(Guid id, CancellationToken ct) =>
        await _submissions.ReopenAsync(id, ct) is { } dto ? Ok(dto) : NotFound();

    /// <summary>Plik ze zgłoszenia (np. zdjęcie) – pobrany z Tally przez serwer, bo linki Tally są prywatne.</summary>
    [HttpGet("{id:guid}/files/{index:int}")]
    public async Task<IActionResult> Download(Guid id, int index, CancellationToken ct)
    {
        var file = await _submissions.GetFileAsync(id, index, ct);
        if (file is null) return NotFound();
        Response.Headers.CacheControl = "private, max-age=300";
        return File(file.Content, file.ContentType, file.Name);
    }

    private string ReviewerEmail() => User.FindFirst(JwtRegisteredClaimNames.Email)?.Value ?? "";
}
