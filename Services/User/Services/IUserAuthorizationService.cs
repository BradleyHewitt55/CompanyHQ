namespace CompanyHQ.Services.Users.Services;

/// <summary>Reads current account authority for in-process callers.</summary>
public interface IUserAuthorizationService
{
    Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default);

    /// <summary>Every account whose role currently grants <paramref name="permission"/>, oldest first.</summary>
    Task<IReadOnlyList<Guid>> ListUserIdsAsync(string permission, CancellationToken cancellationToken = default);
}
