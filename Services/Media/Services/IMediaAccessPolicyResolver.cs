using CompanyHQ.Services.Media.Contracts.Ports;
using CompanyHQ.Services.Media.Contracts.Enums;

namespace CompanyHQ.Services.Media.Services;

/// <summary>Resolves the one consumer policy responsible for a media scope.</summary>
public interface IMediaAccessPolicyResolver
{
    Task<bool> CanReadAsync(
        MediaScope scope,
        Guid mediaObjectId,
        Guid viewerUserId,
        CancellationToken cancellationToken = default);
}
