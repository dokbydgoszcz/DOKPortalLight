using DokPortal.Infrastructure.Identity;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Seed;

public class DbSeederTests
{
    private static ServiceProvider BuildServices(string dbName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        services
            .AddIdentityCore<AppUser>(o => o.Password.RequiredLength = 8)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();

        var configValues = new Dictionary<string, string?>
        {
            ["SeedAdmin:Email"] = "admin@dokportal.local",
            ["SeedAdmin:Password"] = "Sekret123!"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
        services.AddSingleton<IConfiguration>(configuration);

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task SeedAsync_CreatesRolesAdminUserAndParishes()
    {
        var provider = BuildServices(Guid.NewGuid().ToString());

        await DbSeeder.SeedAsync(provider);

        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True(await roleManager.RoleExistsAsync("Administrator"));
        Assert.True(await roleManager.RoleExistsAsync("KatechistaProwadzacy"));

        var userManager = provider.GetRequiredService<UserManager<AppUser>>();
        var admin = await userManager.FindByEmailAsync("admin@dokportal.local");
        Assert.NotNull(admin);
        Assert.True(await userManager.IsInRoleAsync(admin!, "Administrator"));

        var db = provider.GetRequiredService<AppDbContext>();
        Assert.True(await db.Parishes.AnyAsync());
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent_WhenRunTwice()
    {
        var provider = BuildServices(Guid.NewGuid().ToString());

        await DbSeeder.SeedAsync(provider);
        await DbSeeder.SeedAsync(provider);

        var db = provider.GetRequiredService<AppDbContext>();
        Assert.Equal(3, await db.Parishes.CountAsync());
    }
}
