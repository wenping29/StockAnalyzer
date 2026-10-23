using FundAnalysis.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace FundAnalysis.Data;

/// <summary>首次启动时初始化角色和默认管理员。</summary>
public static class DbSeeder
{
    public static readonly string[] AllRoles = ["Admin", "VIP", "User"];

    public static async Task SeedAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var config = services.GetRequiredService<IConfiguration>();

        foreach (var role in AllRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        const string adminName = "admin";
        if (await userManager.FindByNameAsync(adminName) is null)
        {
            var admin = new AppUser
            {
                UserName = adminName,
                Email = "admin@fundanalysis.local",
                EmailConfirmed = true,
            };
            var password = config["Seed:AdminPassword"] ?? "Admin@123";
            var result = await userManager.CreateAsync(admin, password);
            if (result.Succeeded)
                await userManager.AddToRoleAsync(admin, "Admin");
        }
    }
}
