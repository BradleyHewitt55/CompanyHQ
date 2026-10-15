using CompanyHQ.Postgres.Contracts.Models;

namespace CompanyHQ.Company.Contracts.Models;

/// <summary>Independent of the public profile. A singleton serializes concurrent catalog changes.</summary>
public sealed class CompanyServiceState : BaseModel
{
    public static readonly Guid SingletonId = new("00000000-0000-0000-0000-000000000002");
    public string SelectionsJson { get; set; } = "{}";
}
