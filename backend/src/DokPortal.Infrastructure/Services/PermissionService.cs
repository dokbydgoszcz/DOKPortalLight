using DokPortal.Application.Permissions;
using DokPortal.Domain.Constants;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DokPortal.Infrastructure.Services;

public class PermissionService : IPermissionService
{
    private const string CacheKey = "role-permissions-map";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public PermissionService(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<IReadOnlySet<string>> GetPermissionsForRolesAsync(IEnumerable<string> roles, CancellationToken ct)
    {
        var roleList = roles.ToList();
        if (roleList.Contains(AppRoles.Administrator))
        {
            return PermissionCatalog.AllNames;
        }

        var map = await GetMapAsync(ct);
        var result = new HashSet<string>();
        foreach (var role in roleList)
        {
            if (map.TryGetValue(role, out var permissions))
            {
                result.UnionWith(permissions);
            }
        }
        return result;
    }

    public async Task<PermissionMatrixDto> GetMatrixAsync(CancellationToken ct)
    {
        var map = await GetMapAsync(ct);
        var editableRoles = AppRoles.All.Where(r => r != AppRoles.Administrator).ToList();

        return new PermissionMatrixDto
        {
            Roles = editableRoles,
            Permissions = PermissionCatalog.All,
            Grants = editableRoles.ToDictionary(
                role => role,
                role => (IReadOnlyList<string>)(map.TryGetValue(role, out var permissions)
                    ? permissions.OrderBy(p => p).ToList()
                    : new List<string>()))
        };
    }

    public async Task UpdateRolePermissionsAsync(string role, IReadOnlyCollection<string> permissions, CancellationToken ct)
    {
        if (role == AppRoles.Administrator)
        {
            throw new InvalidOperationException("Uprawnień Administratora nie można zmieniać — zawsze ma wszystkie.");
        }
        if (!AppRoles.All.Contains(role))
        {
            throw new InvalidOperationException($"Nieznana rola: {role}.");
        }
        var unknown = permissions.FirstOrDefault(p => !PermissionCatalog.IsKnown(p));
        if (unknown is not null)
        {
            throw new InvalidOperationException($"Nieznane uprawnienie: {unknown}.");
        }

        var existing = await _db.RolePermissions.Where(r => r.RoleName == role).ToListAsync(ct);
        _db.RolePermissions.RemoveRange(existing);
        foreach (var permission in permissions.Distinct())
        {
            _db.RolePermissions.Add(new RolePermission { RoleName = role, Permission = permission });
        }
        await _db.SaveChangesAsync(ct);

        _cache.Remove(CacheKey);
    }

    private async Task<Dictionary<string, HashSet<string>>> GetMapAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(CacheKey, out Dictionary<string, HashSet<string>>? cached) && cached is not null)
        {
            return cached;
        }

        var rows = await _db.RolePermissions.AsNoTracking().ToListAsync(ct);
        var map = rows
            .GroupBy(r => r.RoleName)
            .ToDictionary(g => g.Key, g => g.Select(r => r.Permission).ToHashSet());
        _cache.Set(CacheKey, map, CacheTtl);
        return map;
    }
}
