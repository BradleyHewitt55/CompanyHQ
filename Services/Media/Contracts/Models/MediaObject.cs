using CompanyHQ.Media.Contracts.Enums;
using CompanyHQ.Persistence.Contracts.Models;

namespace CompanyHQ.Media.Contracts.Models;

/// <summary>Postgres metadata for one CompanyHQ media object.</summary>
public sealed class MediaObject : BaseModel
{
    public required Guid OwnerUserId { get; set; }

    public required string Key { get; set; }

    public required string ContentType { get; set; }

    public long SizeBytes { get; set; }

    public int CleanupFailureCount { get; set; }

    public MediaStatus Status { get; set; }

    public MediaScope Scope { get; set; }
}
