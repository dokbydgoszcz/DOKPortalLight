using Microsoft.AspNetCore.Authorization;

namespace DokPortal.Api.Authorization;

/// <summary>Dostęp, gdy użytkownik ma którekolwiek z podanych uprawnień.</summary>
public class HasAnyPermissionAttribute : AuthorizeAttribute
{
    public const string Prefix = "any:";

    public HasAnyPermissionAttribute(params string[] permissions) => Policy = Prefix + string.Join('|', permissions);
}
