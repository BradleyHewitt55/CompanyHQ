using CompanyHQ.Services.Persistence.Contracts.Models;

namespace CompanyHQ.Services.Users.Contracts.Models;

/// <summary>A reusable set of permissions shared by people and agent connections.</summary>
public sealed class AccessRole : BaseModel
{
    public required string Name { get; set; }
    public string[] Permissions { get; set; } = [];
}
