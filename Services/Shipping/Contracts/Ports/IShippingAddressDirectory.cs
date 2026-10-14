using CompanyHQ.Services.Shipping.Contracts.Dtos;
namespace CompanyHQ.Services.Shipping.Contracts.Ports;
/// <summary>Optional company address search/hydration provider. References are opaque to Shipping.</summary>
public interface IShippingAddressDirectory
{
    Task<IReadOnlyList<ShippingAddressCandidateDto>> SearchAsync(string query,int limit,CancellationToken cancellationToken=default);
    Task<ShippingAddressCandidateDto?> HydrateAsync(string reference,CancellationToken cancellationToken=default);
}
