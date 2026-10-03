using System.IdentityModel.Tokens.Jwt;
using DokPortal.Application.DokCases;
using DokPortal.Domain.Constants;
using DokPortal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Api.Authorization;

/// <summary>
/// Zakres spraw bieżącego żądania: pełny dla uprawnienia DokCases.ViewAll, w przeciwnym razie
/// sprawy, w których konto użytkownika (przez AppUser.PersonId) jest katechistą prowadzącym.
/// PersonId czytamy z bazy, nie z tokenu, żeby zmiana powiązania konta działała od razu.
/// </summary>
public class HttpCaseScopeProvider : ICaseScopeProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorization;
    private readonly AppDbContext _db;
    private Task<CaseScope>? _cached;

    public HttpCaseScopeProvider(IHttpContextAccessor httpContextAccessor, IAuthorizationService authorization, AppDbContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorization = authorization;
        _db = db;
    }

    public Task<CaseScope> GetAsync(CancellationToken ct) => _cached ??= ResolveAsync(ct);

    private async Task<CaseScope> ResolveAsync(CancellationToken ct)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return new CaseScope(false, null);
        }

        if ((await _authorization.AuthorizeAsync(user, null, Permissions.DokCasesViewAll)).Succeeded)
        {
            return CaseScope.All;
        }

        var userId = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (userId is null)
        {
            return new CaseScope(false, null);
        }

        var personId = await _db.Users.Where(u => u.Id == userId).Select(u => u.PersonId).FirstOrDefaultAsync(ct);
        return new CaseScope(false, personId);
    }
}
