using CompanyHQ.Services.Company.Contracts.Dtos;

namespace CompanyHQ.Services.Company.Contracts.Ports;

/// <summary>
/// The company profile as another service reads it (for example, to route a contact message to the
/// support address). Callers reference this Contracts assembly only, never the Company service.
/// </summary>
public interface ICompanyProfileReader
{
    /// <summary>Returns the stored profile, or null before an administrator has saved one.</summary>
    Task<ClientCompanyProfileDto?> GetAsync(CancellationToken cancellationToken = default);
}
