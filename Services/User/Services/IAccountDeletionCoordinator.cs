using CompanyHQ.Users.Contracts.Enums;

namespace CompanyHQ.Users.Services;

/// <summary>
/// The one place an account is deleted: every <c>IAccountDeletionGuard</c> inspects and releases
/// inside the same serializable transaction as the user delete.
/// </summary>
public interface IAccountDeletionCoordinator
{
    /// <summary>Deletes the caller's own account.</summary>
    Task<(AccountDeletionOutcome Result, string? Reason)> DeleteAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a target on behalf of an operator. The acting row is locked and checked inside the
    /// same transaction as every release, so a stale session cannot write after demotion.
    /// </summary>
    Task<(AccountDeletionOutcome Result, string? Reason)> DeleteAsync(
        Guid userId,
        Guid? actingAdministratorId,
        CancellationToken cancellationToken = default);
}
