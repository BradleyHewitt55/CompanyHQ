using CompanyHQ.Media.Contracts.Ports;
using CompanyHQ.Media.Contracts.Enums;

namespace CompanyHQ.Media.Services;

/// <summary>Resolves the one consumer policy responsible for a media scope.</summary>
public interface IMediaAccessPolicyResolver
{
    Task<bool> CanReadAsync(
        MediaScope scope,
        Guid mediaObjectId,
        Guid viewerUserId,
        CancellationToken cancellationToken = default);
}
