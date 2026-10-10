using Microsoft.AspNetCore.Identity;

namespace AuthenticationService.DataSeeding
{
    public static class RoleSeeder
    {
        public static readonly string[] Roles = new string[]
        {
            "Admin",
            "User",
        };
        public static async Task SeedRolesAsync(IServiceProvider services) {
            using var scope = services.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            foreach (var roleName in Roles)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }
        }
    }
}
