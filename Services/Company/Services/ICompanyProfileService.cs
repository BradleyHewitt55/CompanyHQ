using CompanyHQ.Company.Contracts.Dtos;
using CompanyHQ.Company.Contracts.Forms;
using CompanyHQ.Company.Contracts.Ports;

namespace CompanyHQ.Company.Services;

/// <summary>The company profile and its administrator write; reads come from <see cref="ICompanyProfileReader"/>.</summary>
public interface ICompanyProfileService : ICompanyProfileReader
{
    /// <summary>Creates or replaces the profile. Throws when the caller is not an administrator.</summary>
    Task<ClientCompanyProfileDto> UpdateAsync(
        UpdateCompanyProfileRequest request,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default);
}
