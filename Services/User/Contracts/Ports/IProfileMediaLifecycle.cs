namespace CompanyHQ.Users.Contracts.Ports;

/// <summary>
/// Retires a managed profile image once the profile stops referencing it. Optional: supplied by
/// Media when it is plugged in; without it a replaced image URL is simply forgotten.
/// </summary>
public interface IProfileMediaLifecycle
{
    Task ReleaseUserProfileImageAsync(
        Guid ownerUserId,
        string? previousUrl,
        string? currentUrl,
        CancellationToken cancellationToken = default);
}
