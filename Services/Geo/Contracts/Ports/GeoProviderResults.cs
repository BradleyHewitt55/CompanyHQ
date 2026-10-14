namespace CompanyHQ.Geo.Contracts.Ports;

/// <summary>
/// A named area as a provider knows it. The coordinates are null when the provider gave no usable
/// position; Geo drops such an area from a search rather than guessing one.
/// </summary>
public sealed record GeoProviderArea(
    double? Latitude,
    double? Longitude,
    string? DisplayName,
    string? Locality,
    string? Region,
    string? CountryCode);

/// <summary>
/// One place a provider found. <see cref="Kind"/> is one of the kinds that were asked for,
/// <see cref="Name"/> is null for an unnamed place, and the contact details are the business's
/// published ones, null wherever the provider holds none.
/// </summary>
public sealed record GeoProviderPlace(
    double Latitude,
    double Longitude,
    string Kind,
    string? Name,
    string? Address,
    string? Phone);
