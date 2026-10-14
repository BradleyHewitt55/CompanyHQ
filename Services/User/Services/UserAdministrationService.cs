using System.Data;

using Microsoft.AspNetCore.Identity;

using CompanyHQ.Postgres.Contracts;
using CompanyHQ.Persistence.Services;
using CompanyHQ.Users.Contracts.Dtos;
using CompanyHQ.Users.Contracts.Enums;
using CompanyHQ.Users.Contracts.Forms;
using CompanyHQ.Users.Contracts.Models;

namespace CompanyHQ.Users.Services;

/// <summary>
/// Performs user writes only after locking and rechecking the acting administrator in the same
/// transaction as the mutation.
/// </summary>
public sealed class UserAdministrationService(
    IPersistence context,
    IPasswordHasher<User> passwordHasher) : IUserAdministrationService
{
    public async Task<UserAdministrationOutcome> CreateAsync(
        Guid actingAdministratorId,
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (InvalidRoleName(request.RoleName))
            return UserAdministrationOutcome.Failure(UserAdministrationError.InvalidRole);
        try
        {
            return await InAdminTransactionAsync(actingAdministratorId, async actorPermissions =>
            {
                var email = Normalise(request.Email);
                if (await context.Tracked<User>().AnyAsync(user => user.Email == email, cancellationToken))
                {
                    return UserAdministrationOutcome.Failure(UserAdministrationError.EmailTaken);
                }

                var user = new User
                {
                    Email = email,
                    PasswordHash = string.Empty,
                    FirstName = request.FirstName?.Trim(),
                    LastName = request.LastName?.Trim(),
                    Bio = request.Bio?.Trim(),
                    Role = request.Role,
                    McpDisabledTools = [.. McpCapabilityCatalog.NormalizeDisabled(request.McpDisabledTools)]
                };
                var chosenRole = await ResolveRoleAsync(request.RoleName ?? request.Role.ToString(), cancellationToken);
                if (chosenRole.Permissions.Except(actorPermissions, StringComparer.Ordinal).Any())
                    return UserAdministrationOutcome.Failure(UserAdministrationError.NotAdministrator);
                user.AccessRole = chosenRole;
                user.Role = LegacyRole(chosenRole.Name);
                user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
                context.Add(user);
                await context.SaveChangesAsync(cancellationToken);

                return UserAdministrationOutcome.Success(user.AsAdmin());
            }, cancellationToken);
        }
        catch (UniqueConstraintException exception) when (exception.Is("ix_user_email"))
        {
            return UserAdministrationOutcome.Failure(UserAdministrationError.EmailTaken);
        }
    }

    public async Task<UserAdministrationOutcome> UpdateAsync(
        Guid actingAdministratorId,
        Guid userId,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (InvalidRoleName(request.RoleName))
            return UserAdministrationOutcome.Failure(UserAdministrationError.InvalidRole);
        try
        {
            return await InAdminTransactionAsync(actingAdministratorId, async actorPermissions =>
            {
                var user = await context.Tracked<User>()
                    .FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

                if (user is null)
                {
                    return UserAdministrationOutcome.Failure(UserAdministrationError.UserNotFound);
                }

                var targetPermissions = user.AccessRoleId is { } targetRoleId
                    ? await context.Tracked<AccessRole>().Where(role => role.Id == targetRoleId)
                        .Select(role => role.Permissions).FirstOrDefaultAsync(cancellationToken) ?? []
                    : user.Role is UserRole.Admin ? AccessPermissions.All : AccessPermissions.Client;
                if (targetPermissions.Except(actorPermissions, StringComparer.Ordinal).Any())
                    return UserAdministrationOutcome.Failure(UserAdministrationError.NotAdministrator);

                if (request.Email is not null)
                {
                    var email = Normalise(request.Email);
                    if (await context.Tracked<User>().AnyAsync(
                            candidate => candidate.Email == email && candidate.Id != userId,
                            cancellationToken))
                    {
                        return UserAdministrationOutcome.Failure(UserAdministrationError.EmailTaken);
                    }

                    user.Email = email;
                }

                if (request.FirstName is not null)
                {
                    user.FirstName = request.FirstName.Trim();
                }

                if (request.LastName is not null)
                {
                    user.LastName = request.LastName.Trim();
                }

                if (request.Bio is not null)
                {
                    user.Bio = request.Bio.Trim();
                }

                if (request.RoleName is not null || request.Role is not null)
                {
                    var chosenRole = await ResolveRoleAsync(request.RoleName ?? request.Role!.Value.ToString(), cancellationToken);
                    if (chosenRole.Permissions.Except(actorPermissions, StringComparer.Ordinal).Any())
                        return UserAdministrationOutcome.Failure(UserAdministrationError.NotAdministrator);
                    user.AccessRole = chosenRole;
                    user.Role = LegacyRole(chosenRole.Name);
                }

                if (request.Password is not null)
                {
                    user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
                }

                if (request.McpDisabledTools is not null)
                {
                    user.McpDisabledTools = [.. McpCapabilityCatalog.NormalizeDisabled(request.McpDisabledTools)];
                }

                await context.SaveChangesAsync(cancellationToken);
                return UserAdministrationOutcome.Success(user.AsAdmin());
            }, cancellationToken);
        }
        catch (UniqueConstraintException exception) when (exception.Is("ix_user_email"))
        {
            return UserAdministrationOutcome.Failure(UserAdministrationError.EmailTaken);
        }
    }

    private async Task<UserAdministrationOutcome> InAdminTransactionAsync(
        Guid actingAdministratorId,
        Func<IReadOnlyList<string>, Task<UserAdministrationOutcome>> operation,
        CancellationToken cancellationToken)
    {
        if (actingAdministratorId == Guid.Empty)
        {
            return UserAdministrationOutcome.Failure(UserAdministrationError.NotAdministrator);
        }

        return await context.InTransactionAsync(async _ =>
        {
            var actor = await context.Locked<User>()
                .FirstOrDefaultAsync(user => user.Id == actingAdministratorId, cancellationToken);

            var actorPermissions = actor?.AccessRoleId is { } assignedRoleId
                ? await context.Tracked<AccessRole>().Where(role => role.Id == assignedRoleId)
                    .Select(role => role.Permissions).FirstOrDefaultAsync(cancellationToken) ?? []
                : actor?.Role is UserRole.Admin ? AccessPermissions.All : [];
            if (!actorPermissions.Contains(AccessPermissions.ManageUsers, StringComparer.Ordinal))
            {
                return UserAdministrationOutcome.Failure(UserAdministrationError.NotAdministrator);
            }

            return await operation(actorPermissions);
        }, IsolationLevel.Serializable, cancellationToken);
    }

    private static string Normalise(string email) => email.Trim().ToLowerInvariant();

    private static bool InvalidRoleName(string? name) =>
        name is not null && (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 80);

    private static UserRole LegacyRole(string name) => name switch
    {
        nameof(UserRole.Admin) => UserRole.Admin,
        _ => UserRole.Client
    };

    private async Task<AccessRole> ResolveRoleAsync(string name, CancellationToken cancellationToken)
    {
        name = name.Trim();
        if (name.Length is 0 or > 80) throw new ArgumentException("Role name must contain 1 to 80 characters.");
        var existing = await context.Tracked<AccessRole>().FirstOrDefaultAsync(role => role.Name == name, cancellationToken);
        if (existing is not null) return existing;
        var permissions = name == nameof(UserRole.Admin) ? AccessPermissions.All : AccessPermissions.Client;
        var created = new AccessRole { Name = name, Permissions = AccessPermissions.Normalize(permissions) };
        context.Add(created);
        return created;
    }
}
