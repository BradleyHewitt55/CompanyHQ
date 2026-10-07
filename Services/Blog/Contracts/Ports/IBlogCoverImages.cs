namespace CompanyHQ.Services.Blogs.Contracts.Ports;

/// <summary>Optional host integration for managed public cover images.</summary>
public interface IBlogCoverImages
{
    /// <summary>Returns the durable URL of a ready image uploaded by this author, or null.</summary>
    Task<string?> GetReadyUrlAsync(Guid mediaObjectId, Guid actingUserId, CancellationToken cancellationToken = default);

    /// <summary>Queues cleanup of a previously stored cover after its last blog reference is removed.</summary>
    Task RetireAsync(Guid mediaObjectId, CancellationToken cancellationToken = default);
}
