using DokPortal.Domain.Constants;
using Xunit;

namespace DokPortal.Domain.Tests;

/// <summary>Biblioteka zasobów dla katechistów: wgrywają Superwizor i Dyrektorzy, przegląda każdy z uprawnieniem.</summary>
public class ResourcePermissionsTests
{
    [Fact]
    public void ThePermissionsAreInTheCatalog()
    {
        Assert.Equal("Resources.View", Permissions.ResourcesView);
        Assert.Equal("Resources.Manage", Permissions.ResourcesManage);
        Assert.True(PermissionCatalog.IsKnown(Permissions.ResourcesView));
        Assert.True(PermissionCatalog.IsKnown(Permissions.ResourcesManage));
    }

    [Theory]
    [InlineData(AppRoles.Superwizor)]
    [InlineData(AppRoles.DyrektorDOK)]
    [InlineData(AppRoles.DyrektorSKSP)]
    public void SupervisorAndDirectors_CanUploadAndView(string role)
    {
        var grants = DefaultRolePermissions.Grants[role];

        Assert.Contains(Permissions.ResourcesView, grants);
        Assert.Contains(Permissions.ResourcesManage, grants);
    }

    [Fact]
    public void TheLeadingCatechist_CanOnlyView_AndTheBishopHasNoAccessByDefault()
    {
        var catechist = DefaultRolePermissions.Grants[AppRoles.KatechistaProwadzacy];

        Assert.Contains(Permissions.ResourcesView, catechist);
        Assert.DoesNotContain(Permissions.ResourcesManage, catechist);
        Assert.DoesNotContain(Permissions.ResourcesView, DefaultRolePermissions.Grants[AppRoles.Biskup]);
    }
}
