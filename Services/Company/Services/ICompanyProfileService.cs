using CompanyHQ.Services.Company.Contracts.Dtos;
using CompanyHQ.Services.Company.Contracts.Forms;
using CompanyHQ.Services.Company.Contracts.Ports;

namespace CompanyHQ.Services.Company.Services;

/// <summary>The company profile and its administrator write; reads come from <see cref="ICompanyProfileReader"/>.</summary>
public interface ICompanyProfileService : ICompanyProfileReader
{
    /// <summary>Creates or replaces the profile. Throws when the caller is not an administrator.</summary>
    Task<ClientCompanyProfileDto> UpdateAsync(
        UpdateCompanyProfileRequest request,
        Guid? actingUserId = null,
        CancellationToken cancellationToken = default);
}
