using CompanyHQ.Services.Shipping.Contracts.Dtos;
using CompanyHQ.Services.Shipping.Contracts.Ports;
namespace CompanyHQ.Services.Shipping.Services;
/// <summary>Bounded provider-neutral address search and hydration.</summary>
public sealed class ShippingAddressService(IShippingAddressDirectory? directory=null)
{
    public Task<IReadOnlyList<ShippingAddressCandidateDto>> SearchAsync(string query,int limit=10,CancellationToken ct=default)
    {
        if(string.IsNullOrWhiteSpace(query) || query.Length>500 || limit is <1 or >20)throw new ArgumentException("Provide an address query up to 500 characters and limit 1 to 20.");
        if(directory is null)throw new ShippingCapabilityUnavailableException("Address search is not configured.");
        return directory.SearchAsync(query.Trim(),limit,ct);
    }
    public Task<ShippingAddressCandidateDto?> HydrateAsync(string reference,CancellationToken ct=default)
    {
        if(string.IsNullOrWhiteSpace(reference) || reference.Length>1000)throw new ArgumentException("Invalid address reference.");
        if(directory is null)throw new ShippingCapabilityUnavailableException("Address hydration is not configured.");
        return directory.HydrateAsync(reference,ct);
    }
}
public sealed class ShippingCapabilityUnavailableException(string message) : Exception(message);
