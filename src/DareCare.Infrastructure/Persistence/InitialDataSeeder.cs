using DareCare.Domain;
using DareCare.Domain.Entities;
using DareCare.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DareCare.Infrastructure.Persistence;

public static class InitialDataSeeder
{
    public static async Task SeedAsync(IServiceProvider provider, string? adminEmail, string? adminPassword)
    {
        var roles = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in AppRoles.All)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                var result = await roles.CreateAsync(new IdentityRole<Guid>(role));
                if (!result.Succeeded) throw new InvalidOperationException($"Cannot seed role: {role}.");
            }
        }

        var db = provider.GetRequiredService<DareCareDbContext>();
        if (!await db.Skills.AnyAsync())
        {
            db.Skills.AddRange(
                new Skill { Id = Guid.Parse("f39d259c-e632-40c4-95a5-e986b0569273"), Name = "Elderly Care" },
                new Skill { Id = Guid.Parse("c4e88d77-d2fd-40d3-8e8c-8f77037de55e"), Name = "Wound Dressing" },
                new Skill { Id = Guid.Parse("a8606b4d-34f6-4b79-991e-148764bc20f0"), Name = "Injection Administration" }
            );
            await db.SaveChangesAsync();
        }
        if (!await db.CareServices.AnyAsync())
        {
            var elderly = await db.Skills.SingleAsync(x => x.Name == "Elderly Care");
            var wound = await db.Skills.SingleAsync(x => x.Name == "Wound Dressing");
            var injection = await db.Skills.SingleAsync(x => x.Name == "Injection Administration");
            db.CareServices.AddRange(
                new CareService { Name = "Elderly Home Nursing", Description = "One-hour elder-care home visit", DurationMinutes = 60, BasePrice = 300m, RequiredSkillId = elderly.Id },
                new CareService { Name = "Wound Dressing", Description = "Wound care in the patient's home", DurationMinutes = 60, BasePrice = 250m, RequiredSkillId = wound.Id },
                new CareService { Name = "Injection Visit", Description = "Home injection by qualified nurse", DurationMinutes = 30, BasePrice = 200m, RequiredSkillId = injection.Id }
            );
            await db.SaveChangesAsync();
        }

        // Bootstrap is opt-in, never hardcode an administrator password in source.
        if (!string.IsNullOrWhiteSpace(adminEmail) || !string.IsNullOrWhiteSpace(adminPassword))
        {
            if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
                throw new InvalidOperationException("Both BootstrapAdmin:Email and BootstrapAdmin:Password are required.");
            var users = provider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByEmailAsync(adminEmail);
            var wasCreatedNow = user is null;
            if (user is null)
            {
                user = new ApplicationUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true, FullName = "DareCare Administrator" };
                var created = await users.CreateAsync(user, adminPassword);
                if (!created.Succeeded)
                    throw new InvalidOperationException("Admin bootstrap failed: check password strength and account uniqueness.");
            }
            if (!await users.IsInRoleAsync(user, AppRoles.Admin))
            {
                // Never escalate ANY pre-existing account, even one that currently has no roles.
                if (!wasCreatedNow)
                    throw new InvalidOperationException("Bootstrap email belongs to an existing non-admin user.");
                var result = await users.AddToRoleAsync(user, AppRoles.Admin);
                if (!result.Succeeded) throw new InvalidOperationException("Could not assign the Admin role.");
            }
        }
    }
}
