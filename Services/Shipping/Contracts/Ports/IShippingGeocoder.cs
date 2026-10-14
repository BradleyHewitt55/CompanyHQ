using CompanyHQ.Shipping.Contracts.Dtos;

namespace CompanyHQ.Shipping.Contracts.Ports;

/// <summary>
/// Turns an address into coordinates. Optional: without it only an address that already carries
/// coordinates can be measured, so a distance-limited method refuses every other one.
/// </summary>
public interface IShippingGeocoder
{
    /// <summary>Null when the address cannot be placed.</summary>
    Task<ShippingCoordinates?> LocateAsync(ShippingAddressDto address, CancellationToken cancellationToken = default);
}

public sealed record ShippingCoordinates(double Latitude, double Longitude);
