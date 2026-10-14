using CompanyHQ.Persistence.Contracts.Models;
using CompanyHQ.Company.Contracts.Dtos;

namespace CompanyHQ.Company.Contracts.Models;

/// <summary>The one row describing the operating company behind CompanyHQ.</summary>
public sealed class CompanyProfile : BaseModel
{
    /// <summary>
    /// The profile is a singleton, so the row carries a fixed key instead of a "only one row"
    /// constraint: a second insert collides on the primary key rather than silently duplicating.
    /// </summary>
    public static readonly Guid SingletonId = new("0192f000-0000-7000-8000-000000000001");

    public required string Name { get; set; }

    public required string Website { get; set; }

    public required string SupportEmail { get; set; }

    public required string InfoEmail { get; set; }

    public ClientCompanyEmailDto[]? PublicEmails { get; set; }

    public string? LogoUrl { get; set; }

    public required string Description { get; set; }

    public string? FacebookAccount { get; set; }

    public string? InstagramAccount { get; set; }

    public string? WhatsAppNumber { get; set; }
}
