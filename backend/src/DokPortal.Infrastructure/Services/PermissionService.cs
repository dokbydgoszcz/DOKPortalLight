using System.Text.RegularExpressions;
using DokPortal.Application.Permissions;
using DokPortal.Domain.Constants;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DokPortal.Infrastructure.Services;

public class PermissionService : IPermissionService
{
    private const string CacheKey = "role-permissions-map";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
    private static readonly Regex RoleNamePattern = new(@"^[\p{L}\d][\p{L}\d \-]*$", RegexOptions.Compiled);

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

    public async Task<IReadOnlyList<string>> ListRoleNamesAsync(CancellationToken ct)
    {
        var names = await _db.Roles.AsNoTracking().Select(r => r.Name!).ToListAsync(ct);
        return OrderRoles(names);
    }

    public async Task<PermissionMatrixDto> GetMatrixAsync(CancellationToken ct)
    {
        var map = await GetMapAsync(ct);
        var names = await ListRoleNamesAsync(ct);
        var editable = names.Where(n => n != AppRoles.Administrator).ToList();

        return new PermissionMatrixDto
        {
            Roles = editable.Select(n => new RoleInfoDto { Name = n, IsSystem = AppRoles.All.Contains(n) }).ToList(),
            Permissions = PermissionCatalog.All,
            Grants = editable.ToDictionary(
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

        var normalizedRole = role.ToUpperInvariant();
        var roleEntity = await _db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.NormalizedName == normalizedRole, ct)
            ?? throw new InvalidOperationException($"Nieznana rola: {role}.");
        var roleName = roleEntity.Name!;

        var unknown = permissions.FirstOrDefault(p => !PermissionCatalog.IsKnown(p));
        if (unknown is not null)
        {
            throw new InvalidOperationException($"Nieznane uprawnienie: {unknown}.");
        }

        var existing = await _db.RolePermissions.Where(r => r.RoleName == roleName).ToListAsync(ct);
        _db.RolePermissions.RemoveRange(existing);
        foreach (var permission in permissions.Distinct())
        {
            _db.RolePermissions.Add(new RolePermission { RoleName = roleName, Permission = permission });
        }
        await _db.SaveChangesAsync(ct);

        _cache.Remove(CacheKey);
    }

    public async Task<RoleInfoDto> CreateRoleAsync(string name, CancellationToken ct)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length is < 3 or > 50)
        {
            throw new InvalidOperationException("Nazwa roli musi mieć od 3 do 50 znaków.");
        }
        if (!RoleNamePattern.IsMatch(trimmed))
        {
            throw new InvalidOperationException("Nazwa roli może zawierać tylko litery, cyfry, spacje i myślniki.");
        }

        var normalized = trimmed.ToUpperInvariant();
        if (await _db.Roles.AnyAsync(r => r.NormalizedName == normalized, ct))
        {
            throw new InvalidOperationException($"Rola „{trimmed}” już istnieje.");
        }

        _db.Roles.Add(new IdentityRole(trimmed) { NormalizedName = normalized });
        await _db.SaveChangesAsync(ct);
        return new RoleInfoDto { Name = trimmed, IsSystem = false };
    }

    public async Task DeleteRoleAsync(string role, CancellationToken ct)
    {
        if (AppRoles.All.Contains(role))
        {
            throw new InvalidOperationException("Roli systemowej nie można usunąć.");
        }

        var normalized = role.ToUpperInvariant();
        var entity = await _db.Roles.FirstOrDefaultAsync(r => r.NormalizedName == normalized, ct)
            ?? throw new InvalidOperationException($"Nieznana rola: {role}.");

        var assigned = await _db.UserRoles.CountAsync(ur => ur.RoleId == entity.Id, ct);
        if (assigned > 0)
        {
            throw new InvalidOperationException($"Roli „{entity.Name}” nie można usunąć — ma ją przypisaną liczba użytkowników: {assigned}.");
        }

        _db.RolePermissions.RemoveRange(await _db.RolePermissions.Where(r => r.RoleName == entity.Name).ToListAsync(ct));
        _db.Roles.Remove(entity);
        await _db.SaveChangesAsync(ct);

        _cache.Remove(CacheKey);
    }

    private static IReadOnlyList<string> OrderRoles(IEnumerable<string> names)
    {
        var list = names.ToList();
        return list.Where(n => AppRoles.All.Contains(n)).OrderBy(n => Array.IndexOf(AppRoles.All, n))
            .Concat(list.Where(n => !AppRoles.All.Contains(n)).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase))
            .ToList();
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
