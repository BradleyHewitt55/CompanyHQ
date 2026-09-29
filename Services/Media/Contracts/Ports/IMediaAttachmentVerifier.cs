using CompanyHQ.Services.Media.Contracts.Enums;

namespace CompanyHQ.Services.Media.Contracts.Ports;

/// <summary>
/// What a consuming service asks before it links an uploaded object to its own row: is this object
/// ready, in my scope, and owned by the caller? Consumers reference this Contracts assembly only and
/// never read Media's table.
/// </summary>
public interface IMediaAttachmentVerifier
{
    Task<bool> IsReadyAsync(
        Guid mediaObjectId,
        Guid ownerUserId,
        MediaScope scope,
        CancellationToken cancellationToken = default);
}
