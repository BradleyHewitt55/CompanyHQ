using CompanyHQ.Company.Contracts.Dtos;
using CompanyHQ.Company.Contracts.Forms;
using CompanyHQ.ServiceCatalog.Contracts;

namespace CompanyHQ.Company.Services;

public interface ICompanyServiceCatalog
{
    Task<IReadOnlyList<ServiceCatalogDto>> ListAsync(Guid? actingUserId, CancellationToken cancellationToken = default);
    Task<ServiceCatalogDto?> GetAsync(string key, Guid? actingUserId, CancellationToken cancellationToken = default);
    Task<ServiceChangeResult> ChangeAsync(string key, ChangeServiceRequest request, Guid? actingUserId,
        CancellationToken cancellationToken = default);
}
