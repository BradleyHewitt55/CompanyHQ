namespace CompanyHQ.Sales.Contracts.Ports;
/// <summary>Active shipping method facts used for explicit hypothetical profit scenarios, not a delivery quote.</summary>
public interface ISalesProfitShipping
{
    Task<IReadOnlyList<SalesProfitShippingMethod>> GetMethodsAsync(CancellationToken cancellationToken=default);
}
public sealed record SalesProfitShippingMethod(Guid Id,string? Code,decimal Cost);
