using Microsoft.AspNetCore.Authorization;

namespace DokPortal.Api.Authorization;

/// <summary>Wymaga jednego uprawnienia albo – dla <see cref="HasAnyPermissionAttribute"/> – dowolnego z kilku.</summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permission) : this(new[] { permission })
    {
    }

    public PermissionRequirement(IReadOnlyList<string> anyOf)
    {
        AnyOf = anyOf;
        Permission = anyOf[0];
    }

    /// <summary>Pierwsze (dla pojedynczego wymagania – jedyne) uprawnienie.</summary>
    public string Permission { get; }

    public IReadOnlyList<string> AnyOf { get; }
}
