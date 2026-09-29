namespace CompanyHQ.Services.Users.Contracts.Ports;

/// <summary>
/// Lets another service veto an account deletion, and then release whatever it was holding once the
/// deletion is allowed to go ahead. Zero or more are registered; the deletion asks every one.
/// </summary>
/// <remarks>
/// A service that references User cannot be referenced back, so it contributes a guard through DI
/// instead. A row that should simply go with the account needs no guard — give it a cascading
/// foreign key to the user.
/// </remarks>
public interface IAccountDeletionGuard
{
    /// <summary>
    /// Null when this service has no objection. Otherwise a caller-facing reason, which the delete
    /// endpoint returns as a 409 so a client knows what it still has to resolve.
    /// </summary>
    Task<string?> InspectAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops whatever rows would otherwise block the delete. Only called after every guard has
    /// passed <see cref="InspectAsync"/>, and must be safe to call when there is nothing to release.
    /// </summary>
    Task ReleaseAsync(Guid userId, CancellationToken cancellationToken = default);
}
