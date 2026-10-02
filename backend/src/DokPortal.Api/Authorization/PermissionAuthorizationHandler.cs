using DokPortal.Application.Permissions;
using Microsoft.AspNetCore.Authorization;

namespace DokPortal.Api.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionService _permissionService;

    public PermissionAuthorizationHandler(IPermissionService permissionService) => _permissionService = permissionService;

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var roles = context.User.FindAll("role").Select(c => c.Value).ToList();
        if (roles.Count == 0)
        {
            return;
        }

        var permissions = await _permissionService.GetPermissionsForRolesAsync(roles, CancellationToken.None);
        if (permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}
