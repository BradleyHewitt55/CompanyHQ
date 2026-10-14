using CompanyHQ.Sales.Contracts.Ports;
namespace CompanyHQ.Sales.Services;
/// <summary>Registered only for authenticated checkout; its customer port is a required startup dependency.</summary>
public sealed class SalesCustomerResolution(ISalesCustomer customers)
{
    public async Task<SalesCustomer> ResolveAsync(Guid userId,CancellationToken ct)=>await customers.GetAsync(userId,ct) ?? throw new UnauthorizedAccessException();
}
