using DawwerOS.Business.Services.Interfaces;
using DawwerOS.DAL.Context;
using DawwerOS.DAL.Entities;
using DawwerOS.DAL.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DawwerOS.Business.Common;

public static class DbInitializer
{
    /// <summary>
    /// Seeds an initial Super Admin user if no Admin user exists in the database.
    /// </summary>
    public static async Task SeedSuperAdminAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DbInitializer));
        var context = services.GetRequiredService<AppDbContext>();
        var passwordHasher = services.GetRequiredService<IPasswordHasherService>();
        var configuration = services.GetRequiredService<IConfiguration>();

        var adminEmail = configuration["AdminSeed:Email"] ?? "admin@dawwer.com";
        var adminPassword = configuration["AdminSeed:Password"] ?? "SuperAdmin@2026!";
        var adminPhone = configuration["AdminSeed:PhoneNumber"] ?? "0590000000";
        var adminFullName = configuration["AdminSeed:FullName"] ?? "Super Admin";

        var normalizedEmail = adminEmail.Trim().ToLowerInvariant();

        // Check if user with this email already exists
        var adminUser = await context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

        if (adminUser != null)
        {
            logger.LogInformation("Super Admin user ({Email}) already exists. Skipping seed.", normalizedEmail);
            return;
        }

        logger.LogInformation("Seeding initial Super Admin user ({Email})...", normalizedEmail);

        var hashedPassword = passwordHasher.HashPassword(adminPassword);
        var utcNow = DateTime.UtcNow;

        var superAdmin = new User
        {
            FullName = adminFullName.Trim(),
            Email = normalizedEmail,
            PhoneNumber = adminPhone.Trim(),
            PasswordHash = hashedPassword,
            Role = UserRole.Admin,
            Status = UserStatus.Active,
            IsEmailVerified = true,
            EmailVerifiedAt = utcNow,
            IsPhoneVerified = true,
            PhoneVerifiedAt = utcNow,
            CreatedAt = utcNow
        };

        await context.Users.AddAsync(superAdmin, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Initial Super Admin user seeded successfully with Id: {UserId}", superAdmin.Id);
    }
}
