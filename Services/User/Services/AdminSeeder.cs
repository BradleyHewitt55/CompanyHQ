using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CompanyHQ.Services.Postgres.Data;
using CompanyHQ.Services.Postgres.Contracts;
using CompanyHQ.Services.Postgres.Services;
using CompanyHQ.Services.Users.Contracts.Enums;
using CompanyHQ.Services.Users.Contracts.Models;

namespace CompanyHQ.Services.Users.Services;

/// <summary>
/// Ensures the administrator from <see cref="AdminSeedOptions"/> exists.
/// </summary>
public sealed class AdminSeeder(
    AdminSeedOptions options,
    IPasswordHasher<User> passwordHasher,
    ILogger<AdminSeeder> logger) : IDataSeeder
{
    public int Order => 0;

    public async Task SeedAsync(PostgresDbContext context, CancellationToken cancellationToken)
    {
        var clientRole = await EnsureRoleAsync(context, "Client", AccessPermissions.Client, cancellationToken);
        var adminRole = await EnsureRoleAsync(context, "Admin", AccessPermissions.All, cancellationToken);
        await context.Set<User>().Where(user => user.AccessRoleId == null && user.Role == UserRole.Client)
            .ExecuteUpdateAsync(update => update.SetProperty(user => user.AccessRoleId, clientRole.Id), cancellationToken);
        await context.Set<User>().Where(user => user.AccessRoleId == null && user.Role == UserRole.Admin)
            .ExecuteUpdateAsync(update => update.SetProperty(user => user.AccessRoleId, adminRole.Id), cancellationToken);

        if (!options.IsConfigured)
        {
            logger.LogWarning(
                "No admin seed credentials configured; set Seed:Admin:Email and Seed:Admin:Password.");
            return;
        }

        var email = options.Email!.Trim().ToLowerInvariant();

        var existing = await context.Set<User>()
            .SingleOrDefaultAsync(user => user.Email == email, cancellationToken);
        if (existing is not null)
        {
            if (existing.Role is not UserRole.Admin)
            {
                throw new InvalidOperationException(
                    $"The configured admin email '{email}' belongs to a non-admin account.");
            }

            // Passwords are deliberately not re-applied on boot. Verification is seed-owned for this
            // account, though: the operator never receives or clicks an email verification link.
            if (!existing.EmailVerified)
            {
                existing.EmailVerified = true;
                await context.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Verified existing seeded admin {Email}.", email);
                return;
            }

            logger.LogInformation("Admin {Email} already exists and is verified.", email);
            return;
        }

        var admin = new User
        {
            Email = email,
            PasswordHash = string.Empty,
            Role = UserRole.Admin,
            AccessRoleId = adminRole.Id,
            FirstName = options.FirstName,
            LastName = options.LastName,
            // The operator never clicked a verification link; without this the seeded admin is
            // locked out of every verified-email-gated endpoint (message send) from the first boot.
            EmailVerified = true
        };

        admin.PasswordHash = passwordHasher.HashPassword(admin, options.Password!);

        context.Set<User>().Add(admin);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded admin {Email}.", email);
    }

    private static async Task<AccessRole> EnsureRoleAsync(
        PostgresDbContext context, string name, IReadOnlyList<string> permissions, CancellationToken cancellationToken)
    {
        var existing = await context.Set<AccessRole>().FirstOrDefaultAsync(role => role.Name == name, cancellationToken);
        if (existing is not null) return existing;
        var role = new AccessRole { Name = name, Permissions = AccessPermissions.Normalize(permissions) };
        context.Set<AccessRole>().Add(role);
        await context.SaveChangesAsync(cancellationToken);
        return role;
    }
}
