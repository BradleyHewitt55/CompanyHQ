namespace CompanyHQ.Services.Geo.Contracts.Dtos;

/// <summary>One place of a configured kind inside the requested viewport.</summary>
/// <remarks>
/// <see cref="Address"/> and <see cref="Phone"/> are optional because map data is uneven: most
/// places carry a name and a position, far fewer carry contact details. The client
/// hides the action rather than rendering a button that cannot do anything.
/// </remarks>
public sealed record NearbyPlaceDto(
    double Latitude,
    double Longitude,
    string Name,
    /// <summary>One of <c>Geo:NearbyPlaceKinds</c>; with OpenStreetMap, its <c>amenity</c> value.</summary>
    string Kind,
    string? Address = null,
    string? Phone = null);
