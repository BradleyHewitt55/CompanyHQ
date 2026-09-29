namespace CompanyHQ.Services.Users.Services;

/// <summary>Reads current account authority for in-process callers.</summary>
public interface IUserAuthorizationService
{
    Task<bool> IsAdministratorAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default);
}
