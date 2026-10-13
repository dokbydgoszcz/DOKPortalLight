using DokPortal.Api.Filters;
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
    private readonly IPersonOverviewService _overview;
    private readonly IAuthorizationService _authorization;

    public PeopleController(IPersonService personService, IPersonOverviewService overview, IAuthorizationService authorization)
    {
        _personService = personService;
        _overview = overview;
        _authorization = authorization;
    }

    /// <summary>Karta osoby: sprawy, misje, kandydatura, pisma, superwizje, konto – każda sekcja tylko z dostępem do jej ekranu.</summary>
    [HttpGet("{id:guid}/overview")]
    [HasPermission(Permissions.PeopleView)]
    public async Task<ActionResult<PersonOverviewDto>> Overview(Guid id, CancellationToken ct)
    {
        async Task<bool> Can(string permission) => (await _authorization.AuthorizeAsync(User, null, permission)).Succeeded;
        var access = new PersonOverviewAccess(
            await Can(Permissions.DokCasesView), await Can(Permissions.CandidatesView), await Can(Permissions.MissionsView),
            await Can(Permissions.ParishNeedsView), await Can(Permissions.FormatorsView), await Can(Permissions.DocumentsView),
            await Can(Permissions.SupervisionsView), await Can(Permissions.UsersManage));
        var overview = await _overview.GetAsync(id, access, ct);
        return overview is null ? NotFound() : Ok(overview);
    }

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
    [Audit("CreatePerson")]
    [HasPermission(Permissions.PeopleManage)]
    public async Task<ActionResult<PersonDto>> Create(CreatePersonRequest request, CancellationToken ct)
    {
        var created = await _personService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Audit("UpdatePerson")]
    [HasPermission(Permissions.PeopleManage)]
    public async Task<ActionResult<PersonDto>> Update(Guid id, UpdatePersonRequest request, CancellationToken ct)
    {
        var updated = await _personService.UpdateAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [Audit("DeletePerson")]
    [HasPermission(Permissions.PeopleManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var currentUserId = User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        var deleted = await _personService.DeleteAsync(id, currentUserId, ct);
        return deleted ? NoContent() : NotFound();
    }
}
