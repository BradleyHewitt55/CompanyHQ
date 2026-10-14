using CompanyHQ.Persistence.Contracts.Models;

namespace CompanyHQ.Moderation.Contracts.Models;

/// <summary>One directional block record; consumers apply its effect in both directions.</summary>
public sealed class UserBlock : BaseModel
{
    public Guid BlockerUserId { get; set; }

    public Guid BlockedUserId { get; set; }

    public string? Reason { get; set; }
}
