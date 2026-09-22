using DokPortal.Domain.Constants;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Identity;
using DokPortal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DokPortal.Infrastructure.Seed;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var configuration = services.GetRequiredService<IConfiguration>();
        var adminEmail = configuration["SeedAdmin:Email"];
        var adminPassword = configuration["SeedAdmin:Password"];

        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            var userManager = services.GetRequiredService<UserManager<AppUser>>();
            var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
            if (existingAdmin is null)
            {
                var admin = new AppUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
                var result = await userManager.CreateAsync(admin, adminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, AppRoles.Administrator);
                }
            }
        }

        var db = services.GetRequiredService<AppDbContext>();
        if (!await db.Parishes.AnyAsync())
        {
            db.Parishes.AddRange(
                new Parish { Id = Guid.NewGuid(), Name = "św. Mateusza" },
                new Parish { Id = Guid.NewGuid(), Name = "Chrystusa Króla" },
                new Parish { Id = Guid.NewGuid(), Name = "św. Józefa" });
            await db.SaveChangesAsync();
        }
    }
}
