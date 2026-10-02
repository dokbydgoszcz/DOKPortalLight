using Microsoft.AspNetCore.Authorization;

namespace DokPortal.Api.Authorization;

public class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission) => Policy = permission;
}
