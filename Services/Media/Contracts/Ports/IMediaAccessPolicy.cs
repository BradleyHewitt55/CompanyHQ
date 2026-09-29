using CompanyHQ.Services.Media.Contracts.Enums;

namespace CompanyHQ.Services.Media.Contracts.Ports;

/// <summary>
/// Consumer-owned read authorization for one private media scope. The consuming service implements
/// it by referencing this Contracts assembly only; a scope with no policy is unreadable except to
/// administrators.
/// </summary>
public interface IMediaAccessPolicy
{
    MediaScope Scope { get; }

    Task<bool> CanReadAsync(
        Guid mediaObjectId,
        Guid viewerUserId,
        CancellationToken cancellationToken = default);
}
