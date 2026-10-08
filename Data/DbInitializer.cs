using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;

namespace AcxiomCRM.Data
{
    public static class DbInitializer
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider, IConfiguration configuration)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

            // Ensure database is created
            await context.Database.EnsureCreatedAsync();

            // Seed only the 3 required system roles
            string[] roleNames = { Roles.Admin, Roles.Manager, Roles.SalesExecutive };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // Seed initial administrative and team accounts securely from environment/config if provided
            var seedPassword = configuration["SeedSettings:DefaultInitialPassword"] 
                               ?? configuration["SEED_DEFAULT_PASSWORD"]
                               ?? "Admin@12345";

            var adminEmail = "admin@acxiomcrm.local";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "System Administrator",
                    EmailConfirmed = true,
                    IsActive = true
                };
                var result = await userManager.CreateAsync(adminUser, seedPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, Roles.Admin);
                }
            }

            var managerEmail = "manager@acxiomcrm.local";
            var managerUser = await userManager.FindByEmailAsync(managerEmail);
            if (managerUser == null)
            {
                managerUser = new ApplicationUser
                {
                    UserName = managerEmail,
                    Email = managerEmail,
                    FullName = "Sales Manager",
                    EmailConfirmed = true,
                    IsActive = true
                };
                var result = await userManager.CreateAsync(managerUser, seedPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(managerUser, Roles.Manager);
                }
            }

            var salesEmail = "sales@acxiomcrm.local";
            var salesUser = await userManager.FindByEmailAsync(salesEmail);
            if (salesUser == null)
            {
                salesUser = new ApplicationUser
                {
                    UserName = salesEmail,
                    Email = salesEmail,
                    FullName = "Sales Executive",
                    EmailConfirmed = true,
                    IsActive = true
                };
                var result = await userManager.CreateAsync(salesUser, seedPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(salesUser, Roles.SalesExecutive);
                }
            }
        }
    }
}
