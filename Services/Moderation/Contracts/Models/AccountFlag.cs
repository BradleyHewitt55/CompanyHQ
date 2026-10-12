using CompanyHQ.Services.Moderation.Contracts.Enums;
using CompanyHQ.Services.Persistence.Contracts.Models;

namespace CompanyHQ.Services.Moderation.Contracts.Models;

/// <summary>A standing judgement about an account, separate from reports of individual targets.</summary>
public sealed class AccountFlag : BaseModel
{
    public Guid UserId { get; set; }

    public required string Kind { get; set; }

    public AccountFlagStatus Status { get; set; } = AccountFlagStatus.Active;

    public FlagOrigin RaisedBy { get; set; }

    public Guid? RaisedByUserId { get; set; }

    public required string Evidence { get; set; }

    public Guid? ClearedByUserId { get; set; }

    public DateTimeOffset? ClearedAt { get; set; }

    public string? ReviewNote { get; set; }
}
