using DokPortal.Api.Authorization;
using System.IdentityModel.Tokens.Jwt;
using DokPortal.Application.Common;
using DokPortal.Application.People;
using DokPortal.Domain.Constants;
using DokPortal.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/people")]
[Authorize]
public class PeopleController : ControllerBase
{
    private readonly IPersonService _personService;

    public PeopleController(IPersonService personService) => _personService = personService;

    /// <summary>
    /// Dane osób (kontakt, data urodzenia, uwagi) widzi rola z „Osoby — przeglądanie/zarządzanie” albo rola, która wybiera osoby
    /// do swojej pracy (sprawy DOK, kandydaci, misje, giełda, formatorzy, pisma, konta). Katechista prowadzący domyślnie nie.
    /// </summary>
    private const int MaxPageSize = 200;

    [HttpGet]
    [HasAnyPermission(Permissions.PeopleView, Permissions.PeopleManage, Permissions.DokCasesManage, Permissions.CandidatesManage,
        Permissions.MissionsManage, Permissions.ParishNeedsManage, Permissions.FormatorsManage, Permissions.DocumentsGenerate, Permissions.UsersManage)]
    public async Task<ActionResult<PagedResult<PersonDto>>> Search(
        [FromQuery] string? query, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] FunctionType? function = null, CancellationToken ct = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize, MaxPageSize);
        return Ok(await _personService.SearchAsync(query, page, pageSize, ct, function));
    }

    [HttpGet("{id:guid}")]
    [HasAnyPermission(Permissions.PeopleView, Permissions.PeopleManage, Permissions.DokCasesManage, Permissions.CandidatesManage,
        Permissions.MissionsManage, Permissions.ParishNeedsManage, Permissions.FormatorsManage, Permissions.DocumentsGenerate, Permissions.UsersManage)]
    public async Task<ActionResult<PersonDto>> GetById(Guid id, CancellationToken ct)
    {
        var person = await _personService.GetByIdAsync(id, ct);
        return person is null ? NotFound() : Ok(person);
    }

    [HttpPost]
    [HasPermission(Permissions.PeopleManage)]
    public async Task<ActionResult<PersonDto>> Create(CreatePersonRequest request, CancellationToken ct)
    {
        var created = await _personService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.PeopleManage)]
    public async Task<ActionResult<PersonDto>> Update(Guid id, UpdatePersonRequest request, CancellationToken ct)
    {
        var updated = await _personService.UpdateAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.PeopleManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var currentUserId = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        var deleted = await _personService.DeleteAsync(id, currentUserId, ct);
        return deleted ? NoContent() : NotFound();
    }
}
