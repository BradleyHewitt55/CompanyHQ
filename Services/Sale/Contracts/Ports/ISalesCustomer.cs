namespace CompanyHQ.Services.Sales.Contracts.Ports;
/// <summary>Authenticated profile resolution prevents checkout from impersonating another customer.</summary>
public interface ISalesCustomer
{
    Task<SalesCustomer?> GetAsync(Guid userId, CancellationToken cancellationToken = default);
}
public sealed record SalesCustomer(string FirstName, string LastName, string Email, string? Phone);
