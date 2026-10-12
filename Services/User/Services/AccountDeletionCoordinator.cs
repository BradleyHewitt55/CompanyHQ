using System.Data;


using CompanyHQ.Services.Persistence.Services;
using CompanyHQ.Services.Postgres.Contracts;
using CompanyHQ.Services.Users.Contracts.Enums;
using CompanyHQ.Services.Users.Contracts.Models;
using CompanyHQ.Services.Users.Contracts.Ports;

namespace CompanyHQ.Services.Users.Services;

/// <inheritdoc cref="IAccountDeletionCoordinator" />
/// <remarks>
/// Takes <see cref="IPersistence"/> because the guards' releases, the operator's row lock, and
/// the user delete must commit as one serializable transaction.
/// </remarks>
public sealed class AccountDeletionCoordinator(
    IRepository<User> users,
    IPersistence context,
    IEnumerable<IAccountDeletionGuard> guards) : IAccountDeletionCoordinator
{
    public Task<(AccountDeletionOutcome Result, string? Reason)> DeleteAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        DeleteAsync(userId, actingAdministratorId: null, cancellationToken: cancellationToken);

    public Task<(AccountDeletionOutcome Result, string? Reason)> DeleteAsync(
        Guid userId,
        Guid? actingAdministratorId,
        CancellationToken cancellationToken = default) =>
        context.InTransactionAsync(async _ =>
        {
            if (actingAdministratorId is { } administratorId
                && (administratorId == Guid.Empty
                    || !await CanManageUsersAsync(administratorId, userId, cancellationToken)))
            {
                return (AccountDeletionOutcome.NotAuthorized, (string?)null);
            }

            if (!await users.ExistsAsync(userId, cancellationToken))
            {
                return (AccountDeletionOutcome.NotFound, (string?)null);
            }

            // Every guard is asked before any of them releases, so a veto from the second guard
            // cannot leave the first one's rows already dropped.
            foreach (var guard in guards)
            {
                if (await guard.InspectAsync(userId, cancellationToken) is { } reason)
                {
                    return (AccountDeletionOutcome.Blocked, reason);
                }
            }

            foreach (var guard in guards)
            {
                await guard.ReleaseAsync(userId, cancellationToken);
            }

            var deleted = await users.DeleteAsync(userId, cancellationToken);

            return deleted
                ? (AccountDeletionOutcome.Deleted, (string?)null)
                : (AccountDeletionOutcome.NotFound, (string?)null);
        }, IsolationLevel.Serializable, cancellationToken);

    private async Task<bool> CanManageUsersAsync(
        Guid actorId,
        Guid targetId,
        CancellationToken cancellationToken)
    {
        var actor = await context.Locked<User>()
            .FirstOrDefaultAsync(user => user.Id == actorId, cancellationToken);

        if (actor is null) return false;
        var actorPermissions = actor.AccessRoleId is { } roleId
            ? await context.Query<AccessRole>().Where(role => role.Id == roleId)
                .Select(role => role.Permissions).FirstOrDefaultAsync(cancellationToken) ?? []
            : actor.Role is UserRole.Admin ? AccessPermissions.All : AccessPermissions.Client;
        if (!actorPermissions.Contains(AccessPermissions.ManageUsers, StringComparer.Ordinal)) return false;

        var target = await context.Query<User>()
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
