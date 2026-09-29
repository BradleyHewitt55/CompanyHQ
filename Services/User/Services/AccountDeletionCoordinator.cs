using System.Data;

using Microsoft.EntityFrameworkCore;

using CompanyHQ.Services.Postgres.Data;
using CompanyHQ.Services.Postgres.Contracts;
using CompanyHQ.Services.Postgres.Services;
using CompanyHQ.Services.Users.Contracts.Enums;
using CompanyHQ.Services.Users.Contracts.Models;
using CompanyHQ.Services.Users.Contracts.Ports;

namespace CompanyHQ.Services.Users.Services;

/// <inheritdoc cref="IAccountDeletionCoordinator" />
/// <remarks>
/// Takes <see cref="PostgresDbContext"/> because the guards' releases, the operator's row lock, and
/// the user delete must commit as one serializable transaction.
/// </remarks>
public sealed class AccountDeletionCoordinator(
    IPostgresService<User> users,
    PostgresDbContext context,
    IEnumerable<IAccountDeletionGuard> guards) : IAccountDeletionCoordinator
{
    public Task<(AccountDeletionOutcome Result, string? Reason)> DeleteAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        DeleteAsync(userId, actingAdministratorId: null, cancellationToken: cancellationToken);

    public Task<(AccountDeletionOutcome Result, string? Reason)> DeleteAsync(
        Guid userId,
        Guid? actingAdministratorId,
        CancellationToken cancellationToken = default)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        Func<Task<(AccountDeletionOutcome Result, string? Reason)>> work = async () =>
        {
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            if (actingAdministratorId is { } administratorId
                && (administratorId == Guid.Empty
                    || !await CanManageUsersAsync(administratorId, userId, cancellationToken)))
            {
                await transaction.CommitAsync(cancellationToken);
                return (AccountDeletionOutcome.NotAuthorized, (string?)null);
            }

            if (!await users.ExistsAsync(userId, cancellationToken))
            {
                await transaction.CommitAsync(cancellationToken);
                return (AccountDeletionOutcome.NotFound, (string?)null);
            }

            // Every guard is asked before any of them releases, so a veto from the second guard
            // cannot leave the first one's rows already dropped.
            foreach (var guard in guards)
            {
                if (await guard.InspectAsync(userId, cancellationToken) is { } reason)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return (AccountDeletionOutcome.Blocked, reason);
                }
            }

            foreach (var guard in guards)
            {
                await guard.ReleaseAsync(userId, cancellationToken);
            }

            var deleted = await users.DeleteAsync(userId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return deleted
                ? (AccountDeletionOutcome.Deleted, (string?)null)
                : (AccountDeletionOutcome.NotFound, (string?)null);
        };

        return strategy.ExecuteAsync(
            work,
            static async (_, operation, _) => await operation(),
            verifySucceeded: null,
            cancellationToken);
    }

    private async Task<bool> CanManageUsersAsync(
        Guid actorId,
        Guid targetId,
        CancellationToken cancellationToken)
    {
        var actor = await context.Set<User>()
            .FromSqlInterpolated($"SELECT * FROM \"user\" WHERE id = {actorId} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

        if (actor is null) return false;
        var actorPermissions = actor.AccessRoleId is { } roleId
            ? await context.Set<AccessRole>().AsNoTracking().Where(role => role.Id == roleId)
                .Select(role => role.Permissions).FirstOrDefaultAsync(cancellationToken) ?? []
            : actor.Role is UserRole.Admin ? AccessPermissions.All : AccessPermissions.Client;
        if (!actorPermissions.Contains(AccessPermissions.ManageUsers, StringComparer.Ordinal)) return false;

        var target = await context.Set<User>().AsNoTracking()
            .Where(user => user.Id == targetId)
            .Select(user => new { user.Role, user.AccessRoleId,
                Permissions = user.AccessRole == null ? null : user.AccessRole.Permissions })
            .FirstOrDefaultAsync(cancellationToken);
        if (target is null) return true;
        var targetPermissions = target.AccessRoleId is null
            ? target.Role is UserRole.Admin ? AccessPermissions.All : AccessPermissions.Client
            : target.Permissions ?? [];
        return !targetPermissions.Except(actorPermissions, StringComparer.Ordinal).Any();
    }
}
