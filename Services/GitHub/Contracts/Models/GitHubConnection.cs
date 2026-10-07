using CompanyHQ.Services.Postgres.Contracts.Models;

namespace CompanyHQ.Services.GitHub.Contracts.Models;

/// <summary>The one repository this deployment files issues in, with its access token encrypted.</summary>
public sealed class GitHubConnection : BaseModel
{
    /// <summary>A fixed key instead of an "only one row" constraint: a second insert collides.</summary>
    public static readonly Guid SingletonId = new("0192f000-0000-7000-8000-000000000002");

    public required string Repository { get; set; }

    public required string TokenEncrypted { get; set; }
}
