using CompanyHQ.Services.Moderation.Contracts.Enums;
using CompanyHQ.Services.Postgres.Contracts.Models;

namespace CompanyHQ.Services.Moderation.Contracts.Models;

/// <summary>One target hidden from one caller.</summary>
public sealed class HiddenContent : BaseModel
{
    public Guid UserId { get; set; }

    public required string TargetKind { get; set; }

    public Guid TargetId { get; set; }

    public HiddenContentSource Source { get; set; } = HiddenContentSource.Legacy;

    public Guid SourceId { get; set; }
}
