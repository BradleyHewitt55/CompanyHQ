using CompanyHQ.ServiceCatalog.Contracts;
using CompanyHQ.Sales.Contracts.Ports;
namespace CompanyHQ.Sales.Services;
/// <summary>Registered only for authenticated checkout; its customer port is a required startup dependency.</summary>
public sealed class SalesCustomerResolution(IServiceAvailability? availability, ISalesCustomer customers)
{
    /// <summary>Retains the original strict constructor for existing consumers.</summary>
    public SalesCustomerResolution(ISalesCustomer customers) : this(null, customers) { }

    public async Task<SalesCustomer> ResolveAsync(Guid userId,CancellationToken ct){
        await (availability?.RequireAsync("sales", false, ct) ?? Task.CompletedTask);
        return await customers.GetAsync(userId,ct) ?? throw new UnauthorizedAccessException();
    }
}
