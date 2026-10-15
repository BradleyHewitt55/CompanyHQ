using CompanyHQ.ServiceCatalog.Contracts;

namespace CompanyHQ.Company.Contracts.Ports;

/// <summary>Reads safe menu entries using the account's current persisted role.</summary>
public interface ICompanyServiceNavigation
{
    Task<IReadOnlyList<ServiceNavigationDto>> ListAsync(Guid actingUserId, CancellationToken cancellationToken = default);
}
