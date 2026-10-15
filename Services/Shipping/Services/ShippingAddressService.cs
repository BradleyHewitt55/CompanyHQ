using CompanyHQ.ServiceCatalog.Contracts;
using CompanyHQ.Shipping.Contracts.Dtos;
using CompanyHQ.Shipping.Contracts.Ports;
namespace CompanyHQ.Shipping.Services;
/// <summary>Bounded provider-neutral address search and hydration.</summary>
public sealed class ShippingAddressService(IServiceAvailability? availability, IShippingAddressDirectory? directory=null)
{
    /// <summary>Retains the original strict constructor for existing consumers.</summary>
    public ShippingAddressService(IShippingAddressDirectory? directory=null) : this(null, directory) { }

    public Task<IReadOnlyList<ShippingAddressCandidateDto>> SearchAsync(string query,int limit=10,CancellationToken ct=default)
    {
        availability?.RequireAsync("shipping", false, ct).GetAwaiter().GetResult();
        if(string.IsNullOrWhiteSpace(query) || query.Length>500 || limit is <1 or >20)throw new ArgumentException("Provide an address query up to 500 characters and limit 1 to 20.");
        if(directory is null)throw new ShippingCapabilityUnavailableException("Address search is not configured.");
        return directory.SearchAsync(query.Trim(),limit,ct);
    }
    public Task<ShippingAddressCandidateDto?> HydrateAsync(string reference,CancellationToken ct=default)
    {
        availability?.RequireAsync("shipping", false, ct).GetAwaiter().GetResult();
        if(string.IsNullOrWhiteSpace(reference) || reference.Length>1000)throw new ArgumentException("Invalid address reference.");
        if(directory is null)throw new ShippingCapabilityUnavailableException("Address hydration is not configured.");
        return directory.HydrateAsync(reference,ct);
    }
}
public sealed class ShippingCapabilityUnavailableException(string message) : Exception(message);
