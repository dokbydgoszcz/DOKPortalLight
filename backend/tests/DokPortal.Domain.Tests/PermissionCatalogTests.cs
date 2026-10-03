using System.Reflection;
using System.Text.RegularExpressions;
using DokPortal.Domain.Constants;
using Xunit;

namespace DokPortal.Domain.Tests;

public class PermissionCatalogTests
{
    private static readonly string[] DeclaredConstants = typeof(Permissions)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral)
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToArray();

    [Fact]
    public void Catalog_HasExpectedNumberOfUniquePermissions()
    {
        Assert.Equal(42, PermissionCatalog.All.Count);
        Assert.Equal(42, PermissionCatalog.AllNames.Count);
    }

    [Fact]
    public void Catalog_NamesFollowModuleDotActionFormat()
    {
        Assert.All(PermissionCatalog.All, p =>
        {
            Assert.Matches(new Regex(@"^[A-Za-z]+\.[A-Za-z]+$"), p.Name);
            Assert.False(string.IsNullOrWhiteSpace(p.Module));
            Assert.False(string.IsNullOrWhiteSpace(p.Label));
        });
    }

    [Fact]
    public void Catalog_ContainsExactlyTheDeclaredConstants()
    {
        Assert.Equal(DeclaredConstants.OrderBy(x => x), PermissionCatalog.AllNames.OrderBy(x => x));
    }

    [Fact]
    public void IsKnown_RecognizesCatalogNamesOnly()
    {
        Assert.True(PermissionCatalog.IsKnown(Permissions.PeopleManage));
        Assert.False(PermissionCatalog.IsKnown("People.Fly"));
    }

    [Fact]
    public void DefaultGrants_UseKnownPermissionsAndEditableRolesOnly()
    {
        foreach (var (role, permissions) in DefaultRolePermissions.Grants)
        {
            Assert.Contains(role, AppRoles.All);
            Assert.NotEqual(AppRoles.Administrator, role);
            Assert.Equal(permissions.Length, permissions.Distinct().Count());
            Assert.All(permissions, p => Assert.True(PermissionCatalog.IsKnown(p), $"{role}: nieznane uprawnienie {p}"));
        }
    }

    [Fact]
    public void DokCasesViewAll_IsGrantedToEveryRoleThatViewsCases_ExceptTheCatechist()
    {
        foreach (var (role, permissions) in DefaultRolePermissions.Grants)
        {
            var viewsCases = permissions.Contains(Permissions.DokCasesView);
            var seesAll = permissions.Contains(Permissions.DokCasesViewAll);

            if (role == AppRoles.KatechistaProwadzacy)
            {
                Assert.True(viewsCases);
                Assert.False(seesAll, "Katechista ma widzieć tylko swoich podopiecznych.");
            }
            else
            {
                Assert.Equal(viewsCases, seesAll);
            }
        }
    }
}
